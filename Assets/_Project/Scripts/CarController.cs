using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CarController : MonoBehaviour
{
    [Header("Engine")]
    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float engineForce = 30f;       // ForceMode.Acceleration 기준 (m/s²)
    [SerializeField] private float deceleration = 8f;        // 입력 없을 때 명시적 감속 (m/s²)

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 160f;        // deg/s

    [Header("Grip")]
    // 저속 = 강한 그립(안정), 고속 = 약한 그립(슬라이딩 허용)
    [SerializeField][Range(0f, 1f)] private float lowSpeedGrip = 0.6f;
    [SerializeField][Range(0f, 1f)] private float highSpeedGrip = 0.15f;
    [SerializeField] private float gripRate = 8f;           // velocity → 전방 정렬 속도

    [Header("Drift")]
    [SerializeField][Range(0f, 1f)] private float driftGripMultiplier = 0.15f; // Shift 시 그립 배율

    [Header("Spinout")]
    [SerializeField] private float spinoutThreshold = 0.7f;    // |lateral|/speed 비율 임계값 (드리프트 중 초과 시 발동)
    [SerializeField] private float spinoutMinSpeed = 8f;        // 최소 발동 속도 (m/s)
    [SerializeField][Min(0.01f)] private float spinoutDuration = 0.4f; // 스핀 시간 (초)
    [SerializeField] private float spinoutSpeedRetain = 0.65f;  // 스핀 후 속도 유지율
    [SerializeField] private float spinoutArcStrength = 0.35f;  // 호선 곡률 (0=직진, 1=차와 같은 속도로 회전)

    [Header("Ground")]
    [SerializeField] private float groundCheckDist = 0.3f;  // 피벗 기준 하향 레이 길이 (콜라이더 크기에 맞게 조정)
    [SerializeField] private LayerMask groundLayer = 1;     // Default layer (Inspector에서 조정)

    private bool _isSpinning = false;
    private float _spinAngle = 0f;      // 현재까지 회전한 누적 각도
    private float _targetAngle = 180f;  // 목표 각도 (기본 180°, 입력 시 360°)
    private float _spinDegPerSec;       // Fix H-2-new: 스핀 각속도 (mid-spin 업그레이드 시 재계산)
    private float _spinSign = 1f;  // 스핀 방향 (+1 = 우, -1 = 좌)
    private float _spinCooldown = 0f;
    private float _spinElapsed = 0f;   // Fix H-4: 무한 스핀 잠금 방지용 경과 시간

    // 키 입력 감지 (Update에서 GetKeyDown 캡처, FixedUpdate에서 소비 후 클리어)
    private bool _sDown, _aDown, _dDown;
    private bool _shiftKey; // 현재 프레임 Shift 눌림 여부

    // 360° 시퀀스 감지
    private int   _spinSeqState  = 0;    // 0=대기, 1=S 눌림(측면 키 대기)
    private float _spinSeqTimer  = 0f;
    private float _triggerTurnDir = 0f; // +1=우측 발동, -1=좌측 발동

    private Rigidbody _rb;
    private Collider _col;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 4f;
    }

    private bool IsGrounded()
    {
        Vector3 origin = new Vector3(transform.position.x, _col.bounds.min.y + 0.05f, transform.position.z);
        return Physics.Raycast(origin, Vector3.down, groundCheckDist, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S)) _sDown = true;
        if (Input.GetKeyDown(KeyCode.A)) _aDown = true;
        if (Input.GetKeyDown(KeyCode.D)) _dDown = true;
    }

    private void FixedUpdate()
    {
        // Shift는 레벨-트리거이므로 FixedUpdate에서 직접 폴링
        bool shiftKey = Input.GetKey(KeyCode.LeftShift);
        _shiftKey = shiftKey;

        if (_isSpinning)
        {
            HandleSpinout();
            return;
        }

        float inputForward = Input.GetAxisRaw("Vertical");
        float inputTurn = Input.GetAxisRaw("Horizontal");

        Vector3 vel = _rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);   // XZ 성분만 (중력 Y 제외)
        float speed = flatVel.magnitude;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);
        float forwardSpeed = Vector3.Dot(flatVel, transform.forward);

        bool grounded = IsGrounded();
        Vector3 preGripFlat = Vector3.zero; // 그립 적용 전 속도 (스핀아웃 트리거용)

        if (grounded)
        {
            // 가속
            if (Mathf.Abs(inputForward) > 0.01f)
            {
                float accel = engineForce * (1f - speedRatio * speedRatio);
                _rb.AddForce(transform.forward * inputForward * accel, ForceMode.Acceleration);
            }
            // 감속
            else if (speed > 0.5f)
            {
                float newSpeed = Mathf.Max(0f, speed - deceleration * Time.fixedDeltaTime);
                flatVel = flatVel.normalized * newSpeed;
                _rb.linearVelocity = new Vector3(flatVel.x, _rb.linearVelocity.y, flatVel.z);
            }
            // 완전 정지 (Y 보존)
            else if (speed > 0f)
            {
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            }

            // velocity 직접 수정 후 파생 변수 재계산
            flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            speed = flatVel.magnitude;
            speedRatio = Mathf.Clamp01(speed / maxSpeed);
            forwardSpeed = Vector3.Dot(flatVel, transform.forward);

            // 최고속 클램프 (Y 보존) — 현재 velocity 재참조
            Vector3 curVel = _rb.linearVelocity;
            Vector3 curFlat = new Vector3(curVel.x, 0f, curVel.z);
            if (curFlat.magnitude > maxSpeed)
            {
                Vector3 clamped = curFlat.normalized * maxSpeed;
                _rb.linearVelocity = new Vector3(clamped.x, curVel.y, clamped.z);
            }

            // 클램프 후 파생 변수 최종 갱신
            flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            speed = flatVel.magnitude;
            speedRatio = Mathf.Clamp01(speed / maxSpeed);
            forwardSpeed = Vector3.Dot(flatVel, transform.forward);

            // 그립 적용 전 속도 캡처 (스핀아웃 트리거용)
            preGripFlat = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

            // 측면 마찰: velocity → 차 전방 정렬 (Y 보존)
            float grip = Mathf.Lerp(lowSpeedGrip, highSpeedGrip, speedRatio);
            if (_shiftKey) grip *= driftGripMultiplier; // 드리프트 중 그립 감소
            if (speed > 0.5f && Mathf.Abs(forwardSpeed) > 0.5f)
            {
                Vector3 targetDir = forwardSpeed >= 0f ? transform.forward : -transform.forward;
                float steerGripReduction = Mathf.Lerp(1f, 0.2f, Mathf.Abs(inputTurn));
                float blendT = 1f - Mathf.Exp(-grip * gripRate * steerGripReduction * Time.fixedDeltaTime);
                Vector3 blended = Vector3.Lerp(flatVel, targetDir * Mathf.Min(speed, maxSpeed), blendT);
                _rb.linearVelocity = new Vector3(blended.x, _rb.linearVelocity.y, blended.z);
            }
        }

        // 스티어링 (그립 수정 이후 forwardSpeed 재참조)
        float steerForwardSpeed = Vector3.Dot(new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z), transform.forward);
        if (grounded && Mathf.Abs(steerForwardSpeed) > 0.5f)
        {
            float turnMult = Mathf.Lerp(1f, 0.4f, speedRatio);
            float turnDir = inputTurn * Mathf.Sign(steerForwardSpeed);
            Quaternion deltaRot = Quaternion.Euler(0f, turnDir * turnSpeed * turnMult * Time.fixedDeltaTime, 0f);
            _rb.MoveRotation(_rb.rotation * deltaRot);
        }

        // 스핀아웃 트리거: 드리프트(Shift) 중 측면 슬립 임계값 초과 시 발동
        if (grounded) _spinCooldown -= Time.fixedDeltaTime;
        float preGripSpeed = preGripFlat.magnitude;
        if (grounded && _spinCooldown <= 0f && _shiftKey && preGripSpeed > spinoutMinSpeed)
        {
            if (preGripSpeed > 0.01f)
            {
                float lateralRatio = Mathf.Abs(Vector3.Dot(preGripFlat, transform.right)) / preGripSpeed;
                if (lateralRatio > spinoutThreshold)
                {
                    float lateral = Vector3.Dot(preGripFlat, transform.right);
                    float fwd = Vector3.Dot(preGripFlat, transform.forward);
                    float spinSign = Mathf.Sign(-lateral * fwd);
                    TriggerSpinout(spinSign, Mathf.Sign(lateral));
                }
            }
        }

        _sDown = false; _aDown = false; _dDown = false;
    }

    private void TriggerSpinout(float spinSign, float triggerTurnDir)
    {
        Vector3 vel = _rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);
        if (flatVel.sqrMagnitude < 0.01f) return;

        _isSpinning = true;
        _spinAngle = 0f;
        _targetAngle = 180f;
        _spinDegPerSec = _targetAngle / spinoutDuration;
        _spinSign = spinSign;
        _triggerTurnDir = triggerTurnDir;
        _spinSeqState  = 0;
        _spinSeqTimer  = 0f;
        _spinElapsed   = 0f;

        // 속도 유지율 적용 (Y 보존)
        _rb.linearVelocity = flatVel.normalized * (flatVel.magnitude * spinoutSpeedRetain)
                             + new Vector3(0f, vel.y, 0f);
    }

    private void HandleSpinout()
    {
        // Fix H-4: 경과 시간 누적 (공중 여부 무관) — 무한 잠금 방지
        _spinElapsed += Time.fixedDeltaTime;
        if (_spinElapsed > spinoutDuration * 3f)
        {
            _isSpinning = false;
            _spinCooldown = spinoutDuration;
            _sDown = false; _aDown = false; _dDown = false;
            return;
        }

        // 360° 시퀀스 감지: S 누른 후 반대 방향 키 (우측 발동→A, 좌측 발동→D)
        _spinSeqTimer -= Time.fixedDeltaTime;
        if (_spinSeqState == 0 && _sDown)
        {
            _spinSeqState = 1;
            _spinSeqTimer = 0.5f;
        }
        // Fix C-2: else if → if so simultaneous S+A input in same tick works
        if (_spinSeqState == 1)
        {
            if (_spinSeqTimer <= 0f)
            {
                _spinSeqState = 0; // 시간 초과 리셋
            }
            else
            {
                bool completionKey = _triggerTurnDir > 0f ? _aDown : _dDown;
                if (completionKey)
                {
                    _targetAngle = 360f;
                    float remainingAngle = _targetAngle - _spinAngle;
                    float remainingTime = Mathf.Max(spinoutDuration * 0.5f, spinoutDuration - _spinElapsed);
                    _spinDegPerSec = remainingAngle / remainingTime;
                    _spinSeqState = 2; // 완료
                }
            }
        }

        if (IsGrounded())
        {
            // Fix H-2-new: 클래스 필드 _spinDegPerSec 사용 → mid-spin 360° 업그레이드 시 각속도 연속성 보장
            float spinDeg = _spinSign * _spinDegPerSec * Time.fixedDeltaTime;
            _spinAngle += Mathf.Abs(spinDeg);

            _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, spinDeg, 0f));

            // 호선: velocity 방향을 arcStrength 비율로 회전
            Vector3 vel = _rb.linearVelocity;
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            if (flat.magnitude > 0.01f)
            {
                float velRotDeg = spinDeg * spinoutArcStrength;
                Vector3 rotated = Quaternion.Euler(0f, velRotDeg, 0f) * flat;
                _rb.linearVelocity = new Vector3(rotated.x, vel.y, rotated.z);
            }
        }

        if (_spinAngle >= _targetAngle)
        {
            _isSpinning = false;
            _spinCooldown = 0.1f;
        }

        _sDown = false; _aDown = false; _dDown = false;
    }

    // GDD: 좀비 충돌 시 속도 감소 (ZombieCollision에서 호출)
    public void ApplySpeedPenalty(float amount)
    {
        Vector3 vel = _rb.linearVelocity;
        float flatSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
        float reduced = Mathf.Max(0f, flatSpeed - amount);
        Vector3 flatDir = flatSpeed > 0.001f ? new Vector3(vel.x, 0f, vel.z).normalized : Vector3.zero;
        _rb.linearVelocity = flatDir * reduced + new Vector3(0f, vel.y, 0f);
    }

    public float CurrentSpeed => new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;
    public float MaxSpeed => maxSpeed;
    public float LateralSpeed => Vector3.Dot(_rb.linearVelocity, transform.right);
    public bool IsSpinning => _isSpinning;
}
