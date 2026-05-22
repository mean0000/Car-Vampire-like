using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Engine")]
    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float engineForce = 30f;       // ForceMode.Acceleration 기준 (m/s²)
    [SerializeField] private float deceleration = 12f;      // 입력 없을 때 제동 (m/s²)

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 160f;        // deg/s

    [Header("Grip")]
    // 저속 = 강한 그립(안정), 고속 = 약한 그립(슬라이딩 허용)
    [SerializeField][Range(0f, 1f)] private float lowSpeedGrip = 0.85f;
    [SerializeField][Range(0f, 1f)] private float highSpeedGrip = 0.40f;
    // 0 = 완전 드리프트, 1 = 즉시 차 앞방향 고정
    [SerializeField][Range(0f, 1f)] private float velocitySteerBlend = 0.08f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ
                        | RigidbodyConstraints.FreezePositionY;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 5f; // [I-1] 충돌 후 비제어 스핀 방지
    }

    private void FixedUpdate()
    {
        // [W-1] FixedUpdate에서 직접 읽어 입력 타이밍 불일치 제거
        float inputForward = Input.GetAxisRaw("Vertical");
        float inputTurn = Input.GetAxisRaw("Horizontal");

        float speed = _rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);
        float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);

        // 가속: 저속에서 강하게 치고나가고 최고속 근처에서 자연 수렴
        if (Mathf.Abs(inputForward) > 0.01f)
        {
            float accel = engineForce * (1f - speedRatio * speedRatio);
            _rb.AddForce(transform.forward * inputForward * accel, ForceMode.Acceleration);
        }
        // [W-4] 임계값 낮춰 저속 크리프 제거
        else if (Mathf.Abs(forwardSpeed) > 0.01f)
        {
            _rb.AddForce(-transform.forward * Mathf.Sign(forwardSpeed) * deceleration, ForceMode.Acceleration);
        }

        // [C-2] AddForce는 프레임 끝에 반영되므로 maxSpeed 보장은 다음 스텝 시작 시점에 처리
        // 자연 수렴 커브(1 - speedRatio²)가 1차 방어선, 이 클램프는 안전망
        if (speed > maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;

        // 측면 마찰력: 속도 기반 동적 그립
        float grip = Mathf.Lerp(lowSpeedGrip, highSpeedGrip, speedRatio);
        Vector3 lateralVel = transform.right * Vector3.Dot(_rb.linearVelocity, transform.right);
        _rb.AddForce(-lateralVel * grip, ForceMode.VelocityChange);

        // 스티어링
        if (Mathf.Abs(forwardSpeed) > 0.5f)
        {
            float turnMult = Mathf.Lerp(0.4f, 1f, speedRatio);
            float turnDir = inputTurn * Mathf.Sign(forwardSpeed);
            Quaternion deltaRot = Quaternion.Euler(0f, turnDir * turnSpeed * turnMult * Time.fixedDeltaTime, 0f);

            // [W-2] MoveRotation 전에 newForward를 미리 계산해 블렌드에 사용
            if (Mathf.Abs(inputTurn) > 0.01f && speed > 1f)
            {
                Vector3 newForward = deltaRot * transform.forward;
                Vector3 blendedVelocity = Vector3.Lerp(
                    _rb.linearVelocity,
                    newForward * speed,
                    velocitySteerBlend
                );
                _rb.linearVelocity = blendedVelocity;
            }

            _rb.MoveRotation(_rb.rotation * deltaRot);
        }

        // 외부 충돌로 생긴 Y축 각속도 제거 (I-1 보완)
        _rb.angularVelocity = Vector3.zero;
    }

    // GDD: 좀비 충돌 시 속도 감소 (ZombieCollision에서 호출)
    public void ApplySpeedPenalty(float amount)
    {
        float reduced = Mathf.Max(0f, _rb.linearVelocity.magnitude - amount);
        // [C-1] reduced=0 일 때 normalized → NaN 방지
        _rb.linearVelocity = reduced > 0.001f
            ? _rb.linearVelocity.normalized * reduced
            : Vector3.zero;
    }

    public float CurrentSpeed => _rb.linearVelocity.magnitude;
    public float MaxSpeed => maxSpeed;
    public float LateralSpeed => Vector3.Dot(_rb.linearVelocity, transform.right);
}
