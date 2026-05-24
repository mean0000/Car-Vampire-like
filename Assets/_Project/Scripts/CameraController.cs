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
    [SerializeField] private float boostFollowMultiplier = 5f; // 부스트 중 followSpeed 배율

    [Header("Lookahead")]
    [SerializeField, Min(0f)] private float lookaheadBias = 0f;   // 정지 시 최소 오프셋 (0 = 차 중앙)
    [SerializeField, Min(0f)] private float lookaheadMax  = 3.0f;
    [SerializeField, Min(0f)] private float boostLookaheadMax = 10f; // 부스트 시 전방 오프셋 (차가 화면에서 벗어나지 않도록)
    [SerializeField] private float lookaheadAttackSpeed  = 8f;   // 속도 증가 시 빠르게 빌드업
    [SerializeField] private float lookaheadReleaseSpeed = 4f;   // 감속/정지 시 천천히 복귀 → 브레이킹 쏠림
    [SerializeField, Min(0f)] private float lateralLookaheadMax = 3f; // 좌우 이동 시 카메라 횡방향 오프셋

    [Header("Screen Anchor")]
    [SerializeField, Range(0.1f, 0.9f)] private float carViewportY = 0.42f;
    // 0.5 = 화면 정중앙, 0.42 = 중앙보다 약간 아래 (앞이 더 잘 보임)

    [Header("FOV & Height")]
    [SerializeField] private float fovBase      = 50f;
    [SerializeField] private float fovMax       = 72f;
    [SerializeField] private float fovSpeed     = 8f;
    [SerializeField] private float heightOffset = 18f;
    [SerializeField] private float heightBonus  = 8f;
    [SerializeField] private float pitchAngle   = 80f;       // X축 회전 (권장 75~85)

    [Header("Dynamic Angle")]
    [SerializeField] private float pitchMin       = 35f;   // 체이스뷰 최저 피치각 (고속)
    [SerializeField] private float pitchChangeSpeed = 0.4f; // 피치 보간 속도
    [SerializeField] private float yawTrackSpeed  = 3f;    // yaw가 차 방향 추적 속도
    [SerializeField] private float chaseBackDist  = 18f;   // 고속 시 후방 오프셋 거리
    [SerializeField] private float chaseHeight    = 12f;   // 체이스뷰 카메라 높이

    [Header("Tilt")]
    [SerializeField] private float pitchSensitivity = 0.03f; // °per m/s²
    [SerializeField] private float maxPitchOffset   = 4f;    // °
    [SerializeField] private float tiltSpeed        = 6f;
    [SerializeField] private float accelFilterSpeed = 4f;    // 가속도 저역통과 필터 속도
    [SerializeField] private float maxDirPitchOffset = 3f;  // 진행 방향 전후 틸트 최대각
    [SerializeField] private float maxDirRollOffset  = 3f;  // 진행 방향 좌우 틸트 최대각

    [Header("Shake")]
    [SerializeField] private float shakeDamping = 5f;

    [Header("Suspension")]
    [SerializeField] private float suspensionAmplitude = 0.04f; // 서스펜션 진동 크기
    [SerializeField] private float suspensionFrequency = 12f;   // 진동 주파수
    [SerializeField] private float suspensionSpeedMin  = 0.2f;  // 진동 시작 speedRatio 임계값

    // 캐시
    private Camera _cam;

    // Follow 상태
    private Vector3 _camPos;            // logical follow position (XZ 추적용)
    private Vector3 _logicalPosition;   // shake 없는 순수 카메라 위치
    private bool _wasBoostActive;  // 부스트 첫 프레임 감지용

    // Lookahead 상태 (스칼라: 차 전방 방향 기준 거리)
    private float _lookaheadDist;
    private float _lateralDist;

    // Yaw 상태
    private float _yaw;                   // 현재 카메라 Y 회전값 (고속 시 차 방향으로 추적)
    private float _currentPitch;          // 동적 피치 (속도에 따라 변함)
    private float _currentHeight;         // 동적 높이 (부스트 시 즉시 변화 방지)

    // FOV & Tilt 상태
    private float _prevSpeed;
    private float _accel;               // 저역통과 필터링된 가속도
    private float _pitchOffset;
    private float _dirPitchOffset;
    private float _dirRollOffset;

    // Shake 상태
    private float   _shakeIntensity;
    private float   _shakeTime;
    private Vector3 _shakeOffset;
    private float _suspensionTime;

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
        _yaw = 0f;
        _currentPitch = pitchAngle;
        _currentHeight = heightOffset;
        transform.rotation = Quaternion.Euler(pitchAngle, _yaw, 0f);
        _cam.fieldOfView = fovBase;
    }

    private void LateUpdate()
    {
        if (car == null) return;

        float dt = Time.deltaTime;
        float currentSpeed = car.CurrentSpeed;
        float speedRatio = car.MaxSpeed > 0f
            ? Mathf.Clamp01(currentSpeed / car.MaxSpeed)
            : 0f;

        // LateUpdate 당 1회 캐싱 (반복 프로퍼티 호출 절감)
        Vector3 carPos      = car.transform.position;
        Vector3 carForward  = car.transform.forward;
        Vector3 carRight    = car.transform.right;
        Vector3 carFlatVel  = car.FlatVelocity;

        // 가속도 계산 + 저역통과 필터 (deltaTime == 0 방어)
        float rawAccel = dt > 0f ? (currentSpeed - _prevSpeed) / dt : 0f;
        _accel = Mathf.Lerp(_accel, rawAccel, 1f - Mathf.Exp(-accelFilterSpeed * dt));
        _prevSpeed = currentSpeed;

        UpdateLookahead(speedRatio, dt, carRight, carFlatVel);
        UpdateTilt(dt, carForward, carRight, carFlatVel);
        UpdateFOV(speedRatio, dt);
        ApplyShake(dt);      // shake offset 먼저 계산
        UpdateFollow(speedRatio, dt, carForward, carRight, carPos); // 그 다음 현재 프레임 shake 적용
    }

    // ── 1. Lookahead (비대칭 지수 감쇠) ──────────────────────────────
    // 속도가 오를 때는 빠르게 앞을 보고, 멈출 때는 천천히 복귀
    // → 브레이킹 쏠림 체감
    private void UpdateLookahead(float speedRatio, float dt, Vector3 carRight, Vector3 carFlatVel)
    {
        float activeLookaheadMax = car.IsBoostActive ? boostLookaheadMax : lookaheadMax;
        float lookaheadTarget = lookaheadBias + activeLookaheadMax * speedRatio;

        // 타겟이 현재보다 크면(가속) → attackSpeed, 작으면(감속/정지) → releaseSpeed
        float speed = lookaheadTarget > _lookaheadDist
            ? lookaheadAttackSpeed
            : lookaheadReleaseSpeed;

        _lookaheadDist = Mathf.Lerp(_lookaheadDist, lookaheadTarget,
            1f - Mathf.Exp(-speed * dt));

        // 횡방향 lookahead: 차 우측 속도 성분 기반
        Vector3 flatCarRight = carRight;
        flatCarRight.y = 0f;
        float lateralSpeed = flatCarRight.sqrMagnitude > 0.001f
            ? Vector3.Dot(carFlatVel, flatCarRight.normalized)
            : 0f;
        float lateralSpeedRatio = car.MaxSpeed > 0f
            ? Mathf.Clamp(lateralSpeed / car.MaxSpeed, -1f, 1f)
            : 0f;
        float lateralTarget = lateralLookaheadMax * lateralSpeedRatio;
        float lateralSpeed2 = Mathf.Abs(lateralTarget) > Mathf.Abs(_lateralDist)
            ? lookaheadAttackSpeed
            : lookaheadReleaseSpeed;
        _lateralDist = Mathf.Lerp(_lateralDist, lateralTarget,
            1f - Mathf.Exp(-lateralSpeed2 * dt));
    }

    // ── 2. Follow (지수 감쇠 위치 추적) ───────────────────────────────
    // 차를 즉시 따라가지 않아 빠르게 달릴 때 차가 "앞으로 나가는 느낌"이 남
    private void UpdateFollow(float speedRatio, float dt, Vector3 carForward, Vector3 carRight, Vector3 carPos)
    {
        // 1. 동적 피치: 저속=pitchAngle(탑다운), 고속=pitchMin(체이스뷰)
        float targetPitch = Mathf.Lerp(pitchAngle, pitchMin, speedRatio);
        _currentPitch = Mathf.Lerp(_currentPitch, targetPitch,
            1f - Mathf.Exp(-pitchChangeSpeed * dt));

        // 2. 동적 yaw: 고속일수록 빠르게 차 방향으로 따라감 (저속에서도 최소 추적)
        float carYaw = car.transform.eulerAngles.y;
        float effectiveYawSpeed = Mathf.Lerp(0.5f, yawTrackSpeed, speedRatio);
        _yaw = Mathf.LerpAngle(_yaw, carYaw,
            1f - Mathf.Exp(-effectiveYawSpeed * dt));

        // 3. 높이: 고속 시 낮아짐 (탑다운→체이스) — smooth lerp로 부스트 즉시 변화 방지
        float targetHeight = Mathf.Lerp(heightOffset, chaseHeight, speedRatio * speedRatio);
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight,
            1f - Mathf.Exp(-pitchChangeSpeed * dt));
        float height = _currentHeight;

        // 4. 후방 오프셋 + lookahead: 전방 flat 벡터 공유
        Vector3 forward = carForward; forward.y = 0f;
        Vector3 right = carRight; right.y = 0f;
        bool hasFwd = forward.sqrMagnitude > 0.001f;
        Vector3 fwdNorm = hasFwd ? forward.normalized : Vector3.zero;
        Vector3 backOffset = hasFwd ? -fwdNorm * (chaseBackDist * speedRatio) : Vector3.zero;
        Vector3 lookaheadOffset = fwdNorm * _lookaheadDist
                                + (right.sqrMagnitude > 0.001f ? right.normalized * _lateralDist : Vector3.zero);

        Vector3 targetPos = carPos + lookaheadOffset + backOffset;

        // 부스트 진입 첫 프레임: _camPos를 즉시 차 위치로 스냅 (속도 점프 보정)
        bool isBoosting = car.IsBoostActive;
        if (isBoosting && !_wasBoostActive)
            _camPos = targetPos;
        _wasBoostActive = isBoosting;

        // XZ 추적 — 부스트 중엔 followSpeed 배율 적용
        float effectiveFollow = isBoosting ? followSpeed * boostFollowMultiplier : followSpeed;
        _camPos = Vector3.Lerp(_camPos, targetPos,
            1f - Mathf.Exp(-effectiveFollow * dt));

        _logicalPosition = new Vector3(_camPos.x, carPos.y + height, _camPos.z);

        // pitch(브레이킹 쏠림) + 동적 yaw + roll(방향 기울기) — pitchMin 하한 보장
        float finalPitch = Mathf.Max(pitchMin, _currentPitch + _pitchOffset - _dirPitchOffset);
        transform.rotation = Quaternion.Euler(finalPitch, _yaw, -_dirRollOffset);

        // ── 화면 앵커: 고속 시 앵커 영향 감소 (체이스뷰와 충돌 방지) ──
        transform.position = _logicalPosition;
        Vector3 vp = _cam.WorldToViewportPoint(carPos);
        if (vp.z > 0.1f)
        {
            float vpErr  = carViewportY - vp.y;
            float scale  = 2f * vp.z * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float sinP   = Mathf.Abs(transform.forward.y);
            if (sinP > 0.1f)
            {
                Vector3 backDir = new Vector3(-transform.up.x, 0f, -transform.up.z);
                if (backDir.sqrMagnitude > 0.001f)
                {
                    Vector3 correction = backDir.normalized * (vpErr * scale / sinP);
                    _logicalPosition += correction;
                }
            }
        }
        transform.position = _logicalPosition + _shakeOffset;
    }

    // ── 3. 동적 틸트 ──────────────────────────────────────────────────
    // Pitch: 저역통과 필터링된 가속도 기반 (braking = 앞으로 쏠림)
    // Roll: LateralSpeed 기반 방향 기울기
    private void UpdateTilt(float dt, Vector3 carForward, Vector3 carRight, Vector3 carFlatVel)
    {
        float tiltScale = car.IsBoostActive ? 0.2f : 1f; // 부스트 중 틸트 과반응 억제
        float targetPitch = Mathf.Clamp(_accel * pitchSensitivity * tiltScale,
            -maxPitchOffset, maxPitchOffset);
        _pitchOffset = Mathf.Lerp(_pitchOffset, targetPitch,
            1f - Mathf.Exp(-tiltSpeed * dt));

        // 진행 방향 틸트: 차량 로컬 forward/right 기준으로 투영 (고정 카메라에서도 방향 무관하게 올바름)
        Vector3 flatVel = carFlatVel;
        float speed = flatVel.magnitude;
        float speedRef = Mathf.Max(car.MaxSpeed, speed > 0.001f ? speed : 0f);
        if (speedRef < 0.001f) speedRef = 1f;
        Vector3 carFwd    = carForward; carFwd.y = 0f; carFwd.Normalize();
        Vector3 carRight2 = carRight;   carRight2.y = 0f; carRight2.Normalize();
        float forwardComp = Vector3.Dot(flatVel, carFwd)    / speedRef;   // 차 전방 성분 → pitch
        float rightComp   = Vector3.Dot(flatVel, carRight2) / speedRef;  // 차 우측 성분 → roll

        float targetDirPitch = Mathf.Clamp(forwardComp * maxDirPitchOffset, -maxDirPitchOffset, maxDirPitchOffset);
        float targetDirRoll  = Mathf.Clamp(rightComp  * maxDirRollOffset,  -maxDirRollOffset,  maxDirRollOffset);

        _dirPitchOffset = Mathf.Lerp(_dirPitchOffset, targetDirPitch, 1f - Mathf.Exp(-tiltSpeed * dt));
        _dirRollOffset  = Mathf.Lerp(_dirRollOffset,  targetDirRoll,  1f - Mathf.Exp(-tiltSpeed * dt));
    }

    // ── 4. FOV 동적 변화 ──────────────────────────────────────────────
    private void UpdateFOV(float speedRatio, float dt)
    {
        // FOV 고정 — 속도 기반 확대/축소 제거 (멀미 방지)
        _cam.fieldOfView = fovBase;
    }

    // ── 5. 충돌 흔들림 (Perlin Noise) ─────────────────────────────────
    // logical/rendered position 분리: _logicalPosition은 흔들림 없음
    private void ApplyShake(float dt)
    {
        // 충돌 흔들림
        if (_shakeIntensity <= 0.001f)
        {
            _shakeOffset = Vector3.zero;
            _shakeIntensity = 0f;
        }
        else
        {
            _shakeTime = (_shakeTime + dt * 20f) % 1000f;
            float offsetX = (Mathf.PerlinNoise(_shakeTime,         0f) - 0.5f) * 2f * _shakeIntensity;
            float offsetZ = (Mathf.PerlinNoise(0f, _shakeTime + 1f) - 0.5f) * 2f * _shakeIntensity;
            _shakeOffset = new Vector3(offsetX, 0f, offsetZ);
            _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f,
                1f - Mathf.Exp(-shakeDamping * dt));
        }

        // 서스펜션 진동: 충돌 shake 중엔 억제, 속도에 비례한 저주파 연속 진동
        if (_shakeIntensity > 0.001f) return;
        if (car != null)
        {
            float speedRatio = car.MaxSpeed > 0f ? Mathf.Clamp01(car.CurrentSpeed / car.MaxSpeed) : 0f;
            float susStrength = Mathf.Max(0f, speedRatio - suspensionSpeedMin) * suspensionAmplitude;
            if (susStrength > 0.001f)
            {
                _suspensionTime = (_suspensionTime + dt * suspensionFrequency) % 1000f;
                float susX = (Mathf.PerlinNoise(_suspensionTime,       10f) - 0.5f) * 2f * susStrength;
                float susY = (Mathf.PerlinNoise(10f, _suspensionTime + 5f) - 0.5f) * 2f * susStrength;
                _shakeOffset += new Vector3(susX, susY, 0f);
            }
        }
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
