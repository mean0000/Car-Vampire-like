using UnityEngine;

/// <summary>
/// 차체 비주얼 기울기 연출 (서스펜션 흉내).
/// 차체 메시가 있는 자식 오브젝트에 붙이거나,
/// bodyTransform에 비주얼 자식 Transform을 연결해서 사용.
/// 물리에 영향 없음 — 순수 시각 연출.
/// </summary>
public class CarVisuals : MonoBehaviour
{
    [SerializeField] private CarController car;
    [SerializeField] private Transform bodyTransform; // 차체 비주얼 Transform (자식 오브젝트)

    [Header("Roll (코너링 기울기)")]
    [SerializeField] private float maxRollAngle   = 8f;  // 최대 좌우 기울기 (도)
    [SerializeField] private float rollSpeed      = 6f;  // 기울기 반응 속도

    [Header("Pitch (가감속 기울기)")]
    [SerializeField] private float maxPitchAngle  = 4f;  // 최대 전후 기울기 (도)
    [SerializeField] private float pitchSpeed     = 5f;
    [SerializeField] private float accelSensitivity = 0.15f; // 가속도 → 피치 변환율

    private float _currentRoll;
    private float _currentPitch;
    private float _prevSpeed;
    private float _accel;

    private void Start()
    {
        if (car == null) car = GetComponentInParent<CarController>();
        if (bodyTransform == null)
            Debug.LogWarning("[CarVisuals] bodyTransform이 할당되지 않았습니다.", this);
        _prevSpeed = car != null ? car.CurrentSpeed : 0f;
    }

    private void Update()
    {
        if (car == null || bodyTransform == null) return;

        float dt = Time.deltaTime;
        float speed = car.CurrentSpeed;

        // 가속도 계산 (저역통과 필터)
        float rawAccel = dt > 0f ? (speed - _prevSpeed) / dt : 0f;
        _accel = Mathf.Lerp(_accel, rawAccel, 1f - Mathf.Exp(-4f * dt));
        _prevSpeed = speed;

        // Roll: 횡속도 기반 — 전체 속력 대비 횡속도 비율 (MaxSpeed 업그레이드와 무관)
        float totalSpeed = car.FlatVelocity.magnitude;
        float lateralRatio = totalSpeed > 0.1f
            ? Mathf.Clamp(car.LateralSpeed / totalSpeed, -1f, 1f)
            : 0f;
        float targetRoll = lateralRatio * maxRollAngle;
        _currentRoll = Mathf.Lerp(_currentRoll, targetRoll,
            1f - Mathf.Exp(-rollSpeed * dt));

        // Pitch: 가속 시 뒤로, 브레이킹 시 앞으로
        float targetPitch = Mathf.Clamp(-_accel * accelSensitivity, -maxPitchAngle, maxPitchAngle);
        _currentPitch = Mathf.Lerp(_currentPitch, targetPitch,
            1f - Mathf.Exp(-pitchSpeed * dt));

        bodyTransform.localRotation = Quaternion.Euler(_currentPitch, 0f, _currentRoll);
    }
}
