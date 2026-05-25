using UnityEngine;

/// <summary>차량 이동: 가속/감속/스티어링/그립/드리프트</summary>
[RequireComponent(typeof(Rigidbody))]
public class CarMovement : MonoBehaviour
{
    [Header("Engine")]
    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float engineForce = 30f;
    [SerializeField] private float deceleration = 8f;

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 200f;

    [Header("Grip")]
    [SerializeField][Range(0f,1f)] private float lowSpeedGrip  = 0.85f;
    [SerializeField][Range(0f,1f)] private float highSpeedGrip = 0.38f;
    [SerializeField] private float gripRate = 13f;

    [Header("Slope Align")]
    [SerializeField] private float slopeAlignSpeed = 12f;

    [Header("Drift")]
    [SerializeField][Range(0f,1f)] private float driftGripMultiplier = 0.06f;
    [SerializeField] private float driftEntryKick = 8f;
    [SerializeField][Range(0f,1f)] private float driftVelRotScale = 0.25f;

    private Rigidbody _rb;
    private CarGroundSensor _ground;
    private ChunkManager _chunkManager;
    private bool _shiftKey;
    private bool _wasShiftLastFrame;
    private float _driftKickCooldown;
    private float _activeMaxSpeed;  // CarBoost가 설정

    public bool IsDrifting => _shiftKey && CurrentSpeed > 5f;
    public float CurrentSpeed { get; private set; }
    public Vector3 FlatVelocity { get; private set; }
    public float LateralSpeed => Vector3.Dot(_rb.linearVelocity, transform.right);
    public float MaxSpeed => _activeMaxSpeed > 0f ? _activeMaxSpeed : maxSpeed;
    public float BaseMaxSpeed => maxSpeed;
    public bool  IsGrounded   => _ground != null && _ground.IsGrounded;

    public void SetActiveMaxSpeed(float s) => _activeMaxSpeed = s;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _ground = GetComponent<CarGroundSensor>();
        _chunkManager = FindFirstObjectByType<ChunkManager>();
        _activeMaxSpeed = maxSpeed;
        _rb.useGravity   = true;
        _rb.constraints  = RigidbodyConstraints.None;
        _rb.linearDamping  = 0f;
        _rb.angularDamping = 8f;

        // 지형 생성 전까지 차량 물리 정지
        if (_chunkManager != null && !_chunkManager.InitialChunksReady)
        {
            _rb.isKinematic = true;
        }
    }

    private void FixedUpdate()
    {
        // 지형 생성 완료 대기
        if (_chunkManager != null && !_chunkManager.InitialChunksReady)
            return;

        // kinematic 해제 (최초 1회)
        if (_rb.isKinematic)
            _rb.isKinematic = false;

        // 지면 감지를 먼저 실행하여 실행 순서 보장
        if (_ground != null) _ground.Probe();

        _shiftKey = Input.GetKey(KeyCode.LeftShift);

        float inputForward = Input.GetAxisRaw("Vertical");
        float inputTurn    = Input.GetAxisRaw("Horizontal");

        Vector3 vel = _rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);
        float speed = flatVel.magnitude;
        float speedRatio = Mathf.Clamp01(speed / MaxSpeed);
        float forwardSpeed = Vector3.Dot(flatVel, transform.forward);

        CurrentSpeed = speed;
        FlatVelocity = flatVel;

        bool grounded = _ground != null && _ground.IsGrounded;
        Vector3 groundNormal = _ground != null ? _ground.GroundNormal : Vector3.up;

        if (grounded)
        {
            // 가속
            if (Mathf.Abs(inputForward) > 0.01f)
            {
                float accel = engineForce * (1f - speedRatio * speedRatio);
                Vector3 slopeForward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
                if (slopeForward.sqrMagnitude < 0.001f) slopeForward = transform.forward;
                else slopeForward.Normalize();
                _rb.AddForce(slopeForward * inputForward * accel, ForceMode.Acceleration);
            }
            // 감속
            else if (speed > 0.5f)
            {
                float newSpeed = Mathf.Max(0f, speed - deceleration * Time.fixedDeltaTime);
                flatVel = flatVel.normalized * newSpeed;
                _rb.linearVelocity = new Vector3(flatVel.x, _rb.linearVelocity.y, flatVel.z);
            }
            else if (speed > 0f)
            {
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            }

            // 파생 변수 갱신
            flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            speed = flatVel.magnitude;
            speedRatio = Mathf.Clamp01(speed / MaxSpeed);
            forwardSpeed = Vector3.Dot(flatVel, transform.forward);

            // 최고속 클램프
            Vector3 curFlat = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            float capSpeed = MaxSpeed;
            if (curFlat.magnitude > capSpeed)
            {
                Vector3 capped = curFlat.normalized * capSpeed;
                _rb.linearVelocity = new Vector3(capped.x, _rb.linearVelocity.y, capped.z);
            }

            // 파생 변수 최종 갱신
            flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            speed = flatVel.magnitude;
            speedRatio = Mathf.Clamp01(speed / MaxSpeed);
            forwardSpeed = Vector3.Dot(flatVel, transform.forward);

            // 측면 그립
            float grip = Mathf.Lerp(lowSpeedGrip, highSpeedGrip, speedRatio);
            if (_shiftKey) grip *= driftGripMultiplier;
            if (speed > 0.5f && Mathf.Abs(forwardSpeed) > 0.5f)
            {
                Vector3 targetDir = forwardSpeed >= 0f ? transform.forward : -transform.forward;
                float blendT = 1f - Mathf.Exp(-grip * gripRate * Time.fixedDeltaTime);
                Vector3 blended = Vector3.Lerp(flatVel, targetDir * Mathf.Min(speed, capSpeed), blendT);
                _rb.linearVelocity = new Vector3(blended.x, _rb.linearVelocity.y, blended.z);
            }
        }

        // ── 스티어링 + 경사면 정렬 (MoveRotation으로 통합) ──
        float steerFwdSpeed = Vector3.Dot(new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z), transform.forward);

        // Euler yaw 추출 대신 현재 forward를 XZ 투영 → 짐벌락 회피
        Vector3 curFlatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (curFlatFwd.sqrMagnitude < 0.001f) curFlatFwd = Vector3.forward;
        else curFlatFwd.Normalize();

        Quaternion steerDelta = Quaternion.identity;
        if (grounded && Mathf.Abs(steerFwdSpeed) > 0.5f)
        {
            float turnMult = Mathf.Lerp(1f, 0.6f, speedRatio);
            float turnDir = inputTurn * Mathf.Sign(steerFwdSpeed);
            steerDelta = Quaternion.Euler(0f, turnDir * turnSpeed * turnMult * Time.fixedDeltaTime, 0f);

            // 조향 시 velocity도 회전
            Vector3 sv = _rb.linearVelocity;
            Vector3 sFlat = new Vector3(sv.x, 0f, sv.z);
            Vector3 sTurned = steerDelta * sFlat;
            float velRotInfluence = IsDrifting ? driftVelRotScale : 1f;
            Vector3 finalFlat = Vector3.Lerp(sFlat, sTurned, velRotInfluence);
            _rb.linearVelocity = new Vector3(finalFlat.x, sv.y, finalFlat.z);
        }

        // ── 조향 yaw 즉시 적용 + 경사면 pitch/roll만 Slerp ──
        // 1) 조향 yaw를 현재 회전에 직접 적용 (Slerp로 감쇠하지 않음)
        Quaternion steered = steerDelta * _rb.rotation;

        // 2) 경사면 정렬 목표: 조향된 forward + 지면 법선 기준 pitch/roll
        Vector3 targetUp = grounded ? groundNormal : Vector3.up;
        Vector3 steeredFlatFwd = steerDelta * curFlatFwd;
        Vector3 fwdOnSlope = Vector3.ProjectOnPlane(steeredFlatFwd, targetUp);
        if (fwdOnSlope.sqrMagnitude < 0.001f)
        {
            fwdOnSlope = Vector3.ProjectOnPlane(Vector3.Cross(targetUp, transform.right), targetUp);
            if (fwdOnSlope.sqrMagnitude < 0.001f) fwdOnSlope = Vector3.forward;
        }
        Quaternion slopeTarget = Quaternion.LookRotation(fwdOnSlope.normalized, targetUp);

        // 3) steered(정확한 yaw, 이전 pitch/roll) → slopeTarget(정확한 yaw, 정렬된 pitch/roll)
        //    yaw는 양쪽 동일하므로 Slerp는 pitch/roll만 보간
        float alignT = 1f - Mathf.Exp(-slopeAlignSpeed * Time.fixedDeltaTime);
        _rb.MoveRotation(Quaternion.Slerp(steered, slopeTarget, alignT));

        // 드리프트 진입 킥
        if (_driftKickCooldown > 0f) _driftKickCooldown -= Time.fixedDeltaTime;
        if (_shiftKey && !_wasShiftLastFrame && speed > 5f && _driftKickCooldown <= 0f)
        {
            float kickDir = Mathf.Abs(inputTurn) > 0.1f ? -Mathf.Sign(inputTurn) : 1f;
            _rb.AddForce(transform.right * kickDir * driftEntryKick, ForceMode.VelocityChange);
            _driftKickCooldown = 0.5f;
        }
        _wasShiftLastFrame = _shiftKey;

        CurrentSpeed = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;
        FlatVelocity  = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
    }

    public void UpgradeMaxSpeed(float multiplier) => maxSpeed *= multiplier;
}
