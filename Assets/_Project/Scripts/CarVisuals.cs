using UnityEngine;

/// <summary>
/// 차체 비주얼 연출 (마리오카트 스타일).
/// 물리에 영향 없음 — 순수 시각 연출.
/// 서스펜션 바운스 + 과장된 기울기 + 착지 스쿼시.
/// </summary>
public class CarVisuals : MonoBehaviour
{
    [SerializeField] private CarController car;
    [SerializeField] private Transform bodyTransform;

    [Header("Cornering Roll")]
    [SerializeField] private float maxRollAngle = 25f;
    [SerializeField] private float rollSpeed    = 12f;

    [Header("Drift Roll")]
    [SerializeField] private float driftRollBonus   = 18f;
    [SerializeField] private float driftRollSpeed   = 16f;
    [SerializeField] private float driftRecoil      = 10f;
    [SerializeField] private float driftRecoilDecay = 5f;

    [Header("Accel/Brake Pitch")]
    [SerializeField] private float maxPitchAngle    = 14f;
    [SerializeField] private float pitchSpeed       = 10f;
    [SerializeField] private float accelSensitivity = 0.25f;
    [SerializeField] private float brakeSensitivity = 0.35f;

    [Header("Drift Tier Visuals")]
    [SerializeField] private float tierRollBonusPerTier = 5f;
    [SerializeField] private float tierPulseSpeed       = 8f;
    [SerializeField] private float tierPulseAmplitude   = 2f;

    [Header("Suspension")]
    [SerializeField] private float suspSpring      = 30f;   // 스프링 강도 (높을수록 빠르게 복귀)
    [SerializeField] private float suspDamping     = 8f;    // 감쇠 (높을수록 덜 출렁임)
    [SerializeField] private float suspBumpScale   = 0.15f; // 속도 기반 진동 세기
    [SerializeField] private float suspMaxOffset   = 0.25f; // 최대 상하 이동 (m)
    [SerializeField] private float landingSquash   = 0.4f;  // 착지 시 아래로 눌리는 양 (m)

    // runtime state — tilt
    private float _currentRoll;
    private float _currentDriftRoll;
    private float _driftRecoilAmount;
    private float _currentPitch;
    private float _prevFwdSpeed;
    private float _smoothAccel;
    private bool  _wasDrifting;
    private float _tierPulsePhase;
    private float _tierPulseOffset;  // 펄스를 별도 변수로 분리 (피드백 루프 방지)

    // runtime state — suspension
    private float _suspOffset;     // 현재 Y 오프셋
    private float _suspVelocity;   // 스프링 속도
    private bool  _wasGrounded;
    private Vector3 _baseLocalPos; // bodyTransform 초기 localPosition

    private void Start()
    {
        if (car == null) car = GetComponentInParent<CarController>();
        _prevFwdSpeed = car != null ? Vector3.Dot(car.FlatVelocity, car.transform.forward) : 0f;
        if (bodyTransform != null) _baseLocalPos = bodyTransform.localPosition;
    }

    private void Update()
    {
        if (car == null || bodyTransform == null) return;

        float dt    = Time.deltaTime;
        float speed = car.CurrentSpeed;
        Transform carT = car.transform;

        // ── 1. Accel/Brake Pitch ───────────────────────────────────
        float fwdSpeed = Vector3.Dot(car.FlatVelocity, carT.forward);
        float rawAccel = dt > 0f
            ? Mathf.Clamp((fwdSpeed - _prevFwdSpeed) / dt, -30f, 30f)
            : 0f;
        _smoothAccel  = Mathf.Lerp(_smoothAccel, rawAccel, 1f - Mathf.Exp(-5f * dt));
        _prevFwdSpeed += Mathf.Clamp(fwdSpeed - _prevFwdSpeed, -30f * dt, 30f * dt);

        float pitchSens   = _smoothAccel < 0f ? brakeSensitivity : accelSensitivity;
        float targetPitch = Mathf.Clamp(-_smoothAccel * pitchSens, -maxPitchAngle, maxPitchAngle);
        _currentPitch     = Mathf.Lerp(_currentPitch, targetPitch, 1f - Mathf.Exp(-pitchSpeed * dt));

        // ── 2. Cornering Roll ──────────────────────────────────────
        float totalSpeed   = car.FlatVelocity.magnitude;
        float lateralRatio = totalSpeed > 0.1f
            ? Mathf.Clamp(car.LateralSpeed / totalSpeed, -1f, 1f)
            : 0f;

        float targetRoll = lateralRatio * maxRollAngle;
        _currentRoll     = Mathf.Lerp(_currentRoll, targetRoll, 1f - Mathf.Exp(-rollSpeed * dt));

        // ── 3. Drift Roll ──────────────────────────────────────────
        bool isDrifting = car.IsDrifting;

        if (isDrifting)
        {
            int tier = car.DriftTier;
            float tierBonus = tier * tierRollBonusPerTier;
            float targetDriftRoll = lateralRatio * (driftRollBonus + tierBonus);
            _currentDriftRoll     = Mathf.Lerp(_currentDriftRoll, targetDriftRoll,
                                        1f - Mathf.Exp(-driftRollSpeed * dt));

            // 티어 2+: 사인파 펄스로 에너지 축적 느낌 (별도 변수로 피드백 루프 방지)
            if (tier >= 2)
            {
                _tierPulsePhase += dt * tierPulseSpeed * tier;
                _tierPulseOffset = Mathf.Sin(_tierPulsePhase) * tierPulseAmplitude * (tier - 1);
            }
            else
            {
                _tierPulsePhase = 0f;
                _tierPulseOffset = 0f;
            }
        }
        else
        {
            if (_wasDrifting && Mathf.Abs(_currentDriftRoll) > 0.5f)
                _driftRecoilAmount = Mathf.Sign(-_currentDriftRoll) * driftRecoil;

            _currentDriftRoll  = Mathf.Lerp(_currentDriftRoll, 0f,
                                     1f - Mathf.Exp(-driftRollSpeed * dt));
            _driftRecoilAmount = Mathf.Lerp(_driftRecoilAmount, 0f,
                                     1f - Mathf.Exp(-driftRecoilDecay * dt));
            _tierPulseOffset = 0f;
            _tierPulsePhase  = 0f;
        }
        _wasDrifting = isDrifting;

        // ── 4. Suspension Spring ───────────────────────────────────
        // 착지 충격: 공중 → 지면 전환 시 아래로 찍기
        bool grounded = car.IsGrounded;
        if (!_wasGrounded && grounded)
            _suspVelocity -= landingSquash * suspSpring;
        _wasGrounded = grounded;

        // 스프링 물리: F = -kx - cv (순수 스프링-댐퍼)
        float springForce = -suspSpring * _suspOffset - suspDamping * _suspVelocity;
        _suspVelocity += springForce * dt;
        _suspOffset   += _suspVelocity * dt;

        // 속도 기반 미세 진동 + 가감속 압축 → 위치 오프셋으로 직접 적용
        float speedBump = 0f;
        if (speed > 2f)
        {
            float freq = speed * 1.5f;
            speedBump = Mathf.Sin(Time.time * freq) * suspBumpScale * Mathf.Clamp01(speed / 20f);
        }
        float accelPush = -_smoothAccel * 0.003f;
        _suspOffset += (speedBump + accelPush) * dt;

        _suspOffset = Mathf.Clamp(_suspOffset, -suspMaxOffset, suspMaxOffset);

        // ── 최종 조합 ──────────────────────────────────────────────
        bodyTransform.localRotation = Quaternion.Euler(
            _currentPitch,
            0f,
            _currentRoll + _currentDriftRoll + _driftRecoilAmount + _tierPulseOffset);

        bodyTransform.localPosition = _baseLocalPos + new Vector3(0f, _suspOffset, 0f);
    }
}
