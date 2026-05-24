using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CarController : MonoBehaviour
{
    [Header("Feedback")]
    [SerializeField] private CameraController camController;
    [SerializeField] private float hitShakeBase = 0.2f;    // 충돌 기본 흔들림
    [SerializeField] private float hitShakeMax  = 0.8f;    // 충돌 최대 흔들림 (최고속 시)
    [SerializeField] private float boostShake   = 0f;      // 부스터 발동 흔들림
    [SerializeField] private float minSpeedAfterHit = 3f;  // 좀비 충돌 후 최저 속도
    [SerializeField] private float hitStopScale    = 0.4f; // 히트스탑 timeScale (0=완전정지, 1=없음)
    [SerializeField] private float hitStopDuration = 0.04f;// 히트스탑 지속 시간 (초)

    [Header("Boost")]
    [SerializeField] private float maxBoostFuel = 100f;
    [SerializeField] private float syncRatePerSecond = 0.05f; // 부스트 중 초당 SYNC RATE 상승량
    [SerializeField] private float driftFuelRate = 20f;     // 드리프트 중 초당 연료 충전량
    [SerializeField] private float boostDrainRate = 30f;    // 초당 연료 소모
    [SerializeField] private float boostImpulseSpeed = 50f; // 부스터 활성화 순간 도달 속도 (m/s)
    [SerializeField] private float boostForce = 15f;        // 순간 이후 boostMaxSpeed까지 점진 가속력
    [SerializeField] private float boostMaxSpeed = 35f;     // 부스터 중 속도 상한

    [Header("Engine")]
    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float engineForce = 30f;       // ForceMode.Acceleration 기준 (m/s²)
    [SerializeField] private float deceleration = 8f;        // 입력 없을 때 명시적 감속 (m/s²)

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 200f;        // deg/s

    [Header("Grip")]
    // 저속 = 강한 그립(안정), 고속 = 약한 그립(슬라이딩 허용)
    [SerializeField][Range(0f, 1f)] private float lowSpeedGrip = 0.6f;
    [SerializeField][Range(0f, 1f)] private float highSpeedGrip = 0.5f;
    [SerializeField] private float gripRate = 13f;           // velocity → 전방 정렬 속도

    [Header("Physics Feel (토글 비교용)")]
    [SerializeField] private bool physicsFeel = false;       // true = 물리 기반 느낌, false = 현재 미끄러운 느낌
    [SerializeField][Range(0f, 1f)] private float physicsLowSpeedGrip  = 0.85f;
    [SerializeField][Range(0f, 1f)] private float physicsHighSpeedGrip = 0.5f;

    [Header("Drift")]
    [SerializeField][Range(0f, 1f)] private float driftGripMultiplier = 0.06f; // Shift 시 그립 배율
    [SerializeField] private float driftEntryKick = 4f; // 드리프트 진입 시 횡방향 초기 킥 (m/s)
    [SerializeField][Range(0f, 1f)] private float driftVelRotScale = 0.15f; // 드리프트 중 velocity rotation 비율 (낮을수록 슬립 앵글 큼)

    [Header("Ground")]
    [SerializeField] private float groundCheckDist = 0.3f;  // 피벗 기준 하향 레이 길이 (콜라이더 크기에 맞게 조정)
    [SerializeField] private LayerMask groundLayer = 1;     // Default layer (Inspector에서 조정)

    private float _boostFuel = 0f;
    private bool _isBoosting = false;
    private bool _wasBoostingLastFrame = false;

    private bool _boostImpulseActive = false; // 임펄스 프레임 클램프 면제

    // 키 입력 감지
    private bool _shiftKey; // 현재 프레임 Shift 눌림 여부
    private bool _wasShiftLastFrame;
    private float _driftKickCooldown; // 드리프트 킥 쿨다운 (빠른 반복 입력 방지)

    private Rigidbody _rb;
    private Collider _col;
    private Vector3 _cachedVel; // FixedUpdate 당 linearVelocity 캐시 (프로퍼티 GC 절감)

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 4f;
    }

    private bool IsGrounded(out Vector3 groundNormal)
    {
        Vector3 origin = new Vector3(transform.position.x, _col.bounds.min.y + 0.05f, transform.position.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDist, groundLayer, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            return true;
        }
        groundNormal = Vector3.up;
        return false;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F)) _boostFuel = maxBoostFuel;
#endif
    }

    private void FixedUpdate()
    {
        // Shift는 레벨-트리거이므로 FixedUpdate에서 직접 폴링
        _shiftKey = Input.GetKey(KeyCode.LeftShift);

        _cachedVel = _rb.linearVelocity;

        float inputForward = Input.GetAxisRaw("Vertical");
        float inputTurn = Input.GetAxisRaw("Horizontal");

        Vector3 vel = _rb.linearVelocity;
        Vector3 flatVel = new Vector3(vel.x, 0f, vel.z);   // XZ 성분만 (중력 Y 제외)
        float speed = flatVel.magnitude;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);
        float forwardSpeed = Vector3.Dot(flatVel, transform.forward);

        bool grounded = IsGrounded(out Vector3 groundNormal);

        // 부스터 상태 결정 (클램프보다 먼저 평가 → 이 프레임 상한 즉시 적용 / grounded 포함으로 IsBoostActive = "실제 활성")
        _isBoosting = Input.GetKey(KeyCode.Space) && _boostFuel > 0f && grounded;

        if (grounded)
        {
            // 가속
            if (Mathf.Abs(inputForward) > 0.01f)
            {
                float accel = engineForce * (1f - speedRatio * speedRatio);
                // 경사면 방향으로 force 투영 — 언덕을 오를 수 있도록 (수직 벽 NaN 방지)
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
            float activeMaxSpeed = _isBoosting ? boostMaxSpeed : maxSpeed;
            if (!_boostImpulseActive && curFlat.magnitude > activeMaxSpeed)
            {
                Vector3 clamped = curFlat.normalized * activeMaxSpeed;
                _rb.linearVelocity = new Vector3(clamped.x, curVel.y, clamped.z);
            }

            // 클램프 후 파생 변수 최종 갱신
            flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            speed = flatVel.magnitude;
            speedRatio = Mathf.Clamp01(speed / maxSpeed);
            forwardSpeed = Vector3.Dot(flatVel, transform.forward);

            // 측면 마찰: velocity → 차 전방 정렬 (Y 보존)
            float activeLow  = physicsFeel ? physicsLowSpeedGrip  : lowSpeedGrip;
            float activeHigh = physicsFeel ? physicsHighSpeedGrip : highSpeedGrip;
            float grip = Mathf.Lerp(activeLow, activeHigh, speedRatio);
            if (_shiftKey) grip *= driftGripMultiplier;
            if (speed > 0.5f && Mathf.Abs(forwardSpeed) > 0.5f)
            {
                Vector3 targetDir = forwardSpeed >= 0f ? transform.forward : -transform.forward;
                float blendT = 1f - Mathf.Exp(-grip * gripRate * Time.fixedDeltaTime);
                Vector3 blended = Vector3.Lerp(flatVel, targetDir * Mathf.Min(speed, activeMaxSpeed), blendT);
                _rb.linearVelocity = new Vector3(blended.x, _rb.linearVelocity.y, blended.z);
            }
        }

        // 스티어링 (그립 수정 이후 forwardSpeed 재참조)
        float steerForwardSpeed = Vector3.Dot(new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z), transform.forward);
        if (grounded && Mathf.Abs(steerForwardSpeed) > 0.5f)
        {
            float turnMult = Mathf.Lerp(1f, 0.6f, speedRatio);
            float turnDir = inputTurn * Mathf.Sign(steerForwardSpeed);
            Quaternion deltaRot = Quaternion.Euler(0f, turnDir * turnSpeed * turnMult * Time.fixedDeltaTime, 0f);
            _rb.MoveRotation(_rb.rotation * deltaRot);

            // 조향 시 velocity도 회전 — 드리프트 중엔 스케일 감소해 슬립 앵글 발생
            Vector3 sv = _rb.linearVelocity;
            Vector3 sFlat = new Vector3(sv.x, 0f, sv.z);
            Vector3 sTurned = deltaRot * sFlat;
            float velRotInfluence = IsDrifting ? driftVelRotScale : 1f;
            Vector3 finalFlat = Vector3.Lerp(sFlat, sTurned, velRotInfluence);
            _rb.linearVelocity = new Vector3(finalFlat.x, sv.y, finalFlat.z);
        }

        // 부스터: 활성화 첫 프레임에 즉시 속도 점프, 이후 연료 소모하며 상한 유지
        if (_isBoosting)
        {
            Vector3 bv = _rb.linearVelocity;
            Vector3 dir = transform.forward;

            if (!_wasBoostingLastFrame)
            {
                // 1단계: 첫 프레임 — boostImpulseSpeed까지 즉시 점프 (다음 프레임 클램프 면제)
                _boostImpulseActive = true;
                float impulseTarget = Mathf.Max(new Vector3(bv.x, 0f, bv.z).magnitude, boostImpulseSpeed);
                _rb.linearVelocity = new Vector3(dir.x * impulseTarget, bv.y, dir.z * impulseTarget);
                if (camController != null)
                    camController.TriggerShake(boostShake);
            }
            else
            {
                // 2단계: 이후 프레임 — boostMaxSpeed까지 점진 가속 (클램프 복귀)
                _boostImpulseActive = false;
                Vector3 slopeDir = Vector3.ProjectOnPlane(dir, groundNormal);
                if (slopeDir.sqrMagnitude < 0.001f) slopeDir = dir;
                else slopeDir.Normalize();
                _rb.AddForce(slopeDir * boostForce, ForceMode.Acceleration);
                Vector3 after = _rb.linearVelocity;
                Vector3 afterFlat = new Vector3(after.x, 0f, after.z);
                if (afterFlat.magnitude > boostMaxSpeed)
                {
                    Vector3 capped = afterFlat.normalized * boostMaxSpeed;
                    _rb.linearVelocity = new Vector3(capped.x, after.y, capped.z);
                }
            }

            _boostFuel = Mathf.Max(0f, _boostFuel - boostDrainRate * Time.fixedDeltaTime);
            SyncRateManager.Instance?.AddSync(syncRatePerSecond * Time.fixedDeltaTime);
            if (_boostFuel <= 0f) _isBoosting = false;
        }
        if (!_isBoosting) _boostImpulseActive = false;
        _wasBoostingLastFrame = _isBoosting;

        // 드리프트 중 연료 충전 (부스트 중엔 충전 안 함)
        if (IsDrifting && !_isBoosting)
            _boostFuel = Mathf.Min(_boostFuel + driftFuelRate * Time.fixedDeltaTime, maxBoostFuel);

        // 드리프트 진입 킥: Shift를 막 눌렀을 때 횡방향으로 차를 밀어냄 (쿨다운으로 연속 발동 방지)
        if (_driftKickCooldown > 0f) _driftKickCooldown -= Time.fixedDeltaTime;
        if (_shiftKey && !_wasShiftLastFrame && speed > 5f && !_isBoosting && _driftKickCooldown <= 0f)
        {
            float hInput = Input.GetAxisRaw("Horizontal");
            float kickDir = Mathf.Abs(hInput) > 0.1f ? -Mathf.Sign(hInput) : 1f;
            _rb.AddForce(transform.right * kickDir * driftEntryKick, ForceMode.VelocityChange);
            _driftKickCooldown = 0.5f;
        }
        _wasShiftLastFrame = _shiftKey;

        _cachedVel = _rb.linearVelocity;
    }

    // GDD: 좀비 충돌 시 속도 감소 (ZombieCollision에서 호출)
    public void ApplySpeedPenalty(float amount)
    {
        if (_rb == null) return;
        Vector3 vel = _rb.linearVelocity;
        float flatSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
        // 최저 속도 보장 (이미 minSpeed 이하면 더 줄이지 않음)
        float floor = Mathf.Min(flatSpeed, minSpeedAfterHit);
        float reduced = Mathf.Max(floor, flatSpeed - amount);
        Vector3 flatDir = flatSpeed > 0.001f ? new Vector3(vel.x, 0f, vel.z).normalized : Vector3.zero;
        _rb.linearVelocity = flatDir * reduced + new Vector3(0f, vel.y, 0f);
        _cachedVel = _rb.linearVelocity;
        if (camController != null)
        {
            float speedRatio = MaxSpeed > 0f ? Mathf.Clamp01(flatSpeed / MaxSpeed) : 0f;
            camController.TriggerShake(Mathf.Lerp(hitShakeBase, hitShakeMax, speedRatio));
        }
        StopCoroutine("HitStopRoutine");
        StartCoroutine("HitStopRoutine");
    }

    private void OnDisable()
    {
        // HitStopRoutine 실행 중 오브젝트 비활성화 시 timeScale 복구
        // 업그레이드 메뉴 또는 게임오버 패널이 열려있으면 건드리지 않음 (각 UI가 직접 복구 책임)
        bool upgradeOpen = UpgradeMenuUI.Instance != null && UpgradeMenuUI.Instance.IsPanelOpen;
        bool gameOverOpen = GameOverUI.Instance != null && GameOverUI.Instance.IsPanelOpen;
        if (!upgradeOpen && !gameOverOpen)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    private void OnDestroy()
    {
        if (Time.timeScale < 1f && UpgradeMenuUI.Instance == null && GameOverUI.Instance == null)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    private System.Collections.IEnumerator HitStopRoutine()
    {
        Time.timeScale = hitStopScale;
        Time.fixedDeltaTime = 0.02f * hitStopScale;
        float elapsed = 0f;
        while (elapsed < hitStopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        // 업그레이드 메뉴 또는 게임오버 패널이 열려있으면 timeScale을 건드리지 않음
        bool upgradeOpen = UpgradeMenuUI.Instance != null && UpgradeMenuUI.Instance.IsPanelOpen;
        bool gameOverOpen = GameOverUI.Instance != null && GameOverUI.Instance.IsPanelOpen;
        if (!upgradeOpen && !gameOverOpen)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    public void UpgradeMaxSpeed(float multiplier) { maxSpeed *= multiplier; }
    public void UpgradeBoostCapacity(float multiplier) { maxBoostFuel *= multiplier; }
    public void UpgradeDriftFuelRate(float multiplier) { driftFuelRate *= multiplier; }

    public float CurrentSpeed => new Vector3(_cachedVel.x, 0f, _cachedVel.z).magnitude;
    public float MaxSpeed => maxSpeed;
    public Vector3 FlatVelocity => new Vector3(_cachedVel.x, 0f, _cachedVel.z);
    public float LateralSpeed => Vector3.Dot(_cachedVel, transform.right);
    public float BoostFuelRatio => maxBoostFuel > 0f ? _boostFuel / maxBoostFuel : 0f;
    public bool IsBoostActive => _isBoosting;
    public bool IsDrifting => _shiftKey && CurrentSpeed > 5f;
}
