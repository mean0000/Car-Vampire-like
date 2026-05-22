using UnityEngine;

/// <summary>
/// 탑다운 카메라 컨트롤러.
/// 모든 보간은 지수 감쇠(Exponential Decay Lerp) 기반.
/// SmoothDamp 미사용.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private CarController car;

    [Header("Follow")]
    [SerializeField] private float followSpeed      = 5f;    // 클수록 카메라가 빨리 따라붙음
    [SerializeField] private float yawSpeed         = 5f;    // 클수록 빠르게 차 방향 따라감

    [Header("Lookahead")]
    [SerializeField, Min(0f)] private float lookaheadBias = 0f;   // 정지 시 최소 오프셋 (0 = 차 중앙)
    [SerializeField, Min(0f)] private float lookaheadMax  = 0.5f;
    [SerializeField] private float lookaheadAttackSpeed  = 8f;   // 속도 증가 시 빠르게 빌드업
    [SerializeField] private float lookaheadReleaseSpeed = 4f;   // 감속/정지 시 천천히 복귀 → 브레이킹 쏠림

    [Header("Screen Anchor")]
    [SerializeField, Range(0.1f, 0.9f)] private float carViewportY = 0.42f;
    // 0.5 = 화면 정중앙, 0.42 = 중앙보다 약간 아래 (앞이 더 잘 보임)

    [Header("FOV & Height")]
    [SerializeField] private float fovBase      = 55f;
    [SerializeField] private float fovMax       = 80f;
    [SerializeField] private float fovSpeed     = 3f;
    [SerializeField] private float heightOffset = 20f;
    [SerializeField] private float heightBonus  = 3f;
    [SerializeField] private float pitchAngle   = 80f;       // X축 회전 (권장 75~85)

    [Header("Tilt")]
    [SerializeField] private float pitchSensitivity = 0.03f; // °per m/s²
    [SerializeField] private float maxPitchOffset   = 4f;    // °
    [SerializeField] private float rollSensitivity  = 0.25f; // °per m/s
    [SerializeField] private float maxRollOffset    = 3f;    // °
    [SerializeField] private float tiltSpeed        = 6f;
    [SerializeField] private float accelFilterSpeed = 4f;    // 가속도 저역통과 필터 속도

    [Header("Shake")]
    [SerializeField] private float shakeDamping = 5f;

    // 캐시
    private Camera _cam;

    // Follow 상태
    private Vector3 _camPos;            // logical follow position (XZ 추적용)
    private Vector3 _logicalPosition;   // shake 없는 순수 카메라 위치

    // Lookahead 상태 (스칼라: 차 전방 방향 기준 거리)
    private float _lookaheadDist;

    // Yaw 상태
    private float _yaw;                   // 현재 카메라 Y 회전값

    // FOV & Tilt 상태
    private float _prevSpeed;
    private float _accel;               // 저역통과 필터링된 가속도
    private float _pitchOffset;
    private float _rollOffset;

    // Shake 상태
    private float   _shakeIntensity;
    private float   _shakeTime;
    private Vector3 _shakeOffset;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (car == null)
        {
            Debug.LogWarning("[CameraController] car가 할당되지 않았습니다.", this);
            return;
        }

        // 카메라를 차량 위치에 바로 배치해 첫 프레임 점프 방지
        _prevSpeed = car.CurrentSpeed;
        _camPos = car.transform.position;
        _lookaheadDist = 0f;  // 첫 프레임 lookahead 점프 방지
        _logicalPosition = _camPos + Vector3.up * heightOffset;
        transform.position = _logicalPosition;
        _yaw = Mathf.Atan2(car.transform.forward.x, car.transform.forward.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(pitchAngle, _yaw, 0f);
        _cam.fieldOfView = fovBase;
    }

    private void LateUpdate()
    {
        if (car == null) return;

        float dt = Time.deltaTime;
        float speedRatio = car.MaxSpeed > 0f
            ? Mathf.Clamp01(car.CurrentSpeed / car.MaxSpeed)
            : 0f;

        // 가속도 계산 + 저역통과 필터 (deltaTime == 0 방어)
        float rawAccel = dt > 0f ? (car.CurrentSpeed - _prevSpeed) / dt : 0f;
        _accel = Mathf.Lerp(_accel, rawAccel, 1f - Mathf.Exp(-accelFilterSpeed * dt));
        _prevSpeed = car.CurrentSpeed;

        UpdateLookahead(speedRatio, dt);
        UpdateTilt(dt);
        UpdateFOV(speedRatio, dt);
        ApplyShake(dt);      // shake offset 먼저 계산
        UpdateFollow(speedRatio, dt); // 그 다음 현재 프레임 shake 적용
    }

    // ── 1. Lookahead (비대칭 지수 감쇠) ──────────────────────────────
    // 속도가 오를 때는 빠르게 앞을 보고, 멈출 때는 천천히 복귀
    // → 브레이킹 쏠림 체감
    private void UpdateLookahead(float speedRatio, float dt)
    {
        float lookaheadTarget = lookaheadBias + lookaheadMax * speedRatio;

        // 타겟이 현재보다 크면(가속) → attackSpeed, 작으면(감속/정지) → releaseSpeed
        float speed = lookaheadTarget > _lookaheadDist
            ? lookaheadAttackSpeed
            : lookaheadReleaseSpeed;

        _lookaheadDist = Mathf.Lerp(_lookaheadDist, lookaheadTarget,
            1f - Mathf.Exp(-speed * dt));
    }

    // ── 2. Follow (지수 감쇠 위치 추적) ───────────────────────────────
    // 차를 즉시 따라가지 않아 빠르게 달릴 때 차가 "앞으로 나가는 느낌"이 남
    private void UpdateFollow(float speedRatio, float dt)
    {
        float height = heightOffset + heightBonus * speedRatio;

        // lookahead: 차의 전방 방향으로 _lookaheadDist만큼 오프셋
        Vector3 forward = car.transform.forward;
        forward.y = 0f;
        Vector3 lookaheadOffset = forward.sqrMagnitude > 0.001f
            ? forward.normalized * _lookaheadDist
            : Vector3.zero;

        Vector3 targetPos = car.transform.position + lookaheadOffset;

        // XZ만 지수 감쇠로 추적 (Y는 height로 고정)
        _camPos = Vector3.Lerp(_camPos, targetPos,
            1f - Mathf.Exp(-followSpeed * dt));

        _logicalPosition = new Vector3(_camPos.x, car.transform.position.y + height, _camPos.z);

        // 차 전방 방향 → 목표 Y 각도 (수평 성분 기준, zero vector 방어)
        Vector3 fwdFlat = car.transform.forward;
        fwdFlat.y = 0f;
        float targetYaw = fwdFlat.sqrMagnitude > 0.001f
            ? Mathf.Atan2(fwdFlat.x, fwdFlat.z) * Mathf.Rad2Deg
            : _yaw;
        // LerpAngle: 360→0 경계 꺾임 방지, 지수 감쇠
        _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-yawSpeed * dt));

        // pitch(브레이킹 쏠림) + yaw(차 방향 추종) + roll(방향 기울기)
        transform.rotation = Quaternion.Euler(
            pitchAngle + _pitchOffset,
            _yaw,
            _rollOffset);

        // ── 화면 앵커: 차가 carViewportY 위치에 오도록 카메라 XZ 보정 ──
        // rotation이 먼저 설정된 후 WorldToViewportPoint를 호출해야 정확함
        transform.position = _logicalPosition; // 임시 설정 (shake 없이)
        Vector3 vp = _cam.WorldToViewportPoint(car.transform.position);
        if (vp.z > 0.1f)
        {
            float vpErr  = carViewportY - vp.y;
            float scale  = 2f * vp.z * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float sinP   = Mathf.Abs(transform.forward.y); // ≈ sin(pitchAngle)
            if (sinP > 0.01f)
            {
                Vector3 backDir = new Vector3(-transform.up.x, 0f, -transform.up.z);
                if (backDir.sqrMagnitude > 0.001f)
                {
                    Vector3 correction = backDir.normalized * (vpErr * scale / sinP);
                    _logicalPosition += correction;
                    // _camPos도 보정: 다음 프레임 지수 감쇠가 보정된 위치에서 시작하도록
                    _camPos += new Vector3(correction.x, 0f, correction.z);
                }
            }
        }
        // 최종 위치 적용
        transform.position = _logicalPosition + _shakeOffset;
    }

    // ── 3. 동적 틸트 ──────────────────────────────────────────────────
    // Pitch: 저역통과 필터링된 가속도 기반 (braking = 앞으로 쏠림)
    // Roll: LateralSpeed 기반 방향 기울기
    private void UpdateTilt(float dt)
    {
        float targetPitch = Mathf.Clamp(_accel * pitchSensitivity,
            -maxPitchOffset, maxPitchOffset);
        _pitchOffset = Mathf.Lerp(_pitchOffset, targetPitch,
            1f - Mathf.Exp(-tiltSpeed * dt));

        float lateralSpeed = car.LateralSpeed;
        if (float.IsNaN(lateralSpeed)) lateralSpeed = 0f;
        float targetRoll = Mathf.Clamp(-lateralSpeed * rollSensitivity,
            -maxRollOffset, maxRollOffset);
        _rollOffset = Mathf.Lerp(_rollOffset, targetRoll,
            1f - Mathf.Exp(-tiltSpeed * dt));
    }

    // ── 4. FOV 동적 변화 ──────────────────────────────────────────────
    private void UpdateFOV(float speedRatio, float dt)
    {
        float targetFov = Mathf.Lerp(fovBase, fovMax, speedRatio);
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov,
            1f - Mathf.Exp(-fovSpeed * dt));
    }

    // ── 5. 충돌 흔들림 (Perlin Noise) ─────────────────────────────────
    // logical/rendered position 분리: _logicalPosition은 흔들림 없음
    private void ApplyShake(float dt)
    {
        if (_shakeIntensity <= 0.001f)
        {
            _shakeOffset = Vector3.zero;
            _shakeIntensity = 0f;
            return;
        }

        _shakeTime = (_shakeTime + dt * 20f) % 1000f;

        float offsetX = (Mathf.PerlinNoise(_shakeTime,         0f) - 0.5f) * 2f * _shakeIntensity;
        float offsetZ = (Mathf.PerlinNoise(0f, _shakeTime + 1f) - 0.5f) * 2f * _shakeIntensity;
        _shakeOffset = new Vector3(offsetX, 0f, offsetZ);

        // rendered position은 UpdateFollow 내에서 이미 _shakeOffset을 더했음
        // 여기서는 감쇠만 처리
        _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f,
            1f - Mathf.Exp(-shakeDamping * dt));
    }

    /// <summary>
    /// 충돌 이벤트 발생 시 외부에서 호출 (예: ZombieCollision).
    /// </summary>
    public void TriggerShake(float intensity)
    {
        intensity = Mathf.Clamp(intensity, 0f, 2f);
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }
}
