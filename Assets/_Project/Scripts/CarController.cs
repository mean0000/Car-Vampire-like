using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CarController : MonoBehaviour
{
    [Header("Feedback")]
    [SerializeField] private CameraController camController;
    [SerializeField] private float hitShakeBase = 0.2f;    // 충돌 기본 흔들림
    [SerializeField] private float hitShakeMax  = 0.8f;    // 충돌 최대 흔들림 (최고속 시)
    [SerializeField] private float boostShake   = 0.6f;    // 부스터 발동 흔들림
    [SerializeField] private float minSpeedAfterHit = 3f;  // 좀비 충돌 후 최저 속도
    [SerializeField] private float hitStopScale    = 0.4f; // 히트스탑 timeScale (0=완전정지, 1=없음)
    [SerializeField] private float hitStopDuration = 0.04f;// 히트스탑 지속 시간 (초)

    [Header("Boost")]
    [SerializeField] private float maxBoostFuel = 100f;
    [SerializeField] private float boostFuelPerHit = 20f;   // 좀비 1마리 충돌당 충전
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
    [SerializeField][Range(0f, 1f)] private float highSpeedGrip = 0.15f;
    [SerializeField] private float gripRate = 8f;           // velocity → 전방 정렬 속도

    [Header("Drift")]
    [SerializeField][Range(0f, 1f)] private float driftGripMultiplier = 0.06f; // Shift 시 그립 배율

    [Header("Spinout")]
    [SerializeField] private float spinoutThreshold = 0.7f;    // |lateral|/speed 비율 임계값 (드리프트 중 초과 시 발동)
    [SerializeField] private float spinoutMinSpeed = 8f;        // 최소 발동 속도 (m/s)
    [SerializeField][Min(0.01f)] private float spinoutDuration = 0.4f; // 스핀 시간 (초)
    [SerializeField] private float spinoutSpeedRetain = 0.65f;  // 스핀 후 속도 유지율
    [SerializeField] private float spinoutArcStrength = 0.35f;  // 호선 곡률 (0=직진, 1=차와 같은 속도로 회전)

    [Header("Ground")]
    [SerializeField] private float groundCheckDist = 0.3f;  // 피벗 기준 하향 레이 길이 (콜라이더 크기에 맞게 조정)
    [SerializeField] private LayerMask groundLayer = 1;     // Default layer (Inspector에서 조정)

    private float _boostFuel = 0f;
    private bool _isBoosting = false;
    private bool _wasBoostingLastFrame = false;

    private bool _boostImpulseActive = false; // 임펄스 프레임 클램프 면제

    private bool _isSpinning = false;
    private float _spinAngle = 0f;      // 현재까지 회전한 누적 각도
    private float _targetAngle = 180f;  // 목표 각도 (기본 180°, 입력 시 360°)
    private float _spinDegPerSec;       // Fix H-2-new: 스핀 각속도 (mid-spin 업그레이드 시 재계산)
    private float _spinSign = 1f;  // 스핀 방향 (+1 = 우, -1 = 좌)
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
        if (Input.GetKeyDown(KeyCode.F)) _boostFuel = maxBoostFuel; // 테스트용 연료 충전
    }

    private void FixedUpdate()
    {
        // Shift는 레벨-트리거이므로 FixedUpdate에서 직접 폴링
        _shiftKey = Input.GetKey(KeyCode.LeftShift);

        _cachedVel = _rb.linearVelocity;

        if (_isSpinning)
        {
            _isBoosting = false; // 스핀 중엔 부스터 비활성 (stale true 방지)
            _boostImpulseActive = false;
            HandleSpinout();
            _wasBoostingLastFrame = false; // 스핀 중에도 동기화 — 스핀 종료 후 첫 프레임 impulse 여부를 예측 가능하게 유지
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

        // 부스터 상태 결정 (클램프보다 먼저 평가 → 이 프레임 상한 즉시 적용 / grounded 포함으로 IsBoostActive = "실제 활성")
        _isBoosting = Input.GetKey(KeyCode.Space) && _boostFuel > 0f && !_isSpinning && grounded;

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
            float grip = Mathf.Lerp(lowSpeedGrip, highSpeedGrip, speedRatio);
            if (_shiftKey) grip *= driftGripMultiplier; // 드리프트 중 그립 감소
            if (speed > 0.5f && Mathf.Abs(forwardSpeed) > 0.5f)
            {
                Vector3 targetDir = forwardSpeed >= 0f ? transform.forward : -transform.forward;
                float steerGripReduction = Mathf.Lerp(1f, 0.2f, Mathf.Abs(inputTurn));
                float blendT = 1f - Mathf.Exp(-grip * gripRate * steerGripReduction * Time.fixedDeltaTime);
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
        }

        // 부스터: 활성화 첫 프레임에 즉시 속도 점프, 이후 연료 소모하며 상한 유지
        if (_isBoosting)
        {
            Vector3 bv = _rb.linearVelocity;
            Vector3 bf = new Vector3(bv.x, 0f, bv.z);
            Vector3 dir = bf.sqrMagnitude > 0.001f ? bf.normalized : transform.forward;

            if (!_wasBoostingLastFrame)
            {
                // 1단계: 첫 프레임 — boostImpulseSpeed까지 즉시 점프 (다음 프레임 클램프 면제)
                _boostImpulseActive = true;
                float impulseTarget = Mathf.Max(bf.magnitude, boostImpulseSpeed);
                _rb.linearVelocity = new Vector3(dir.x * impulseTarget, bv.y, dir.z * impulseTarget);
                if (camController != null)
                    camController.TriggerShake(boostShake);
            }
            else
            {
                // 2단계: 이후 프레임 — boostMaxSpeed까지 점진 가속 (클램프 복귀)
                _boostImpulseActive = false;
                _rb.AddForce(dir * boostForce, ForceMode.Acceleration);
                Vector3 after = _rb.linearVelocity;
                Vector3 afterFlat = new Vector3(after.x, 0f, after.z);
                if (afterFlat.magnitude > boostMaxSpeed)
                {
                    Vector3 capped = afterFlat.normalized * boostMaxSpeed;
                    _rb.linearVelocity = new Vector3(capped.x, after.y, capped.z);
                }
            }

            _boostFuel = Mathf.Max(0f, _boostFuel - boostDrainRate * Time.fixedDeltaTime);
            if (_boostFuel <= 0f) _isBoosting = false;
        }
        if (!_isBoosting) _boostImpulseActive = false;
        _wasBoostingLastFrame = _isBoosting;

        _cachedVel = _rb.linearVelocity;
        _sDown = false; _aDown = false; _dDown = false;
    }

    private void TriggerSpinout(float spinSign, float triggerTurnDir)
    {
        if (_isSpinning) return;
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
        }

        _cachedVel = _rb.linearVelocity;
        _sDown = false; _aDown = false; _dDown = false;
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
        _boostFuel = Mathf.Min(_boostFuel + boostFuelPerHit, maxBoostFuel);
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
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
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
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    public float CurrentSpeed => new Vector3(_cachedVel.x, 0f, _cachedVel.z).magnitude;
    public float MaxSpeed => maxSpeed;
    public Vector3 FlatVelocity => new Vector3(_cachedVel.x, 0f, _cachedVel.z);
    public float LateralSpeed => Vector3.Dot(_cachedVel, transform.right);
    public bool IsSpinning => _isSpinning;
    public float BoostFuelRatio => maxBoostFuel > 0f ? _boostFuel / maxBoostFuel : 0f;
    public bool IsBoostActive => _isBoosting;
}
