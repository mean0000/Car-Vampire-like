using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
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

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ
                        | RigidbodyConstraints.FreezePositionY;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 4f; // [I-1] 충돌 후 비제어 스핀 방지
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
        else if (speed > 0.5f)
        {
            // Y 성분 제거 후 감속 (FreezePositionY 충돌 방지)
            Vector3 flatVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            // 오버슈트 방지: 이 프레임에 완전히 멈출 수 있는 힘 이하로 제한
            float brakingForce = Mathf.Min(deceleration, flatVel.magnitude / Time.fixedDeltaTime);
            _rb.AddForce(-flatVel.normalized * brakingForce, ForceMode.Acceleration);
        }
        else if (speed > 0f)
        {
            _rb.linearVelocity = Vector3.zero; // 0.5f 이하: 완전 정지
        }

        // [C-2] AddForce는 프레임 끝에 반영되므로 maxSpeed 보장은 다음 스텝 시작 시점에 처리
        // 자연 수렴 커브(1 - speedRatio²)가 1차 방어선, 이 클램프는 안전망
        if (speed > maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;

        // 측면 마찰력: velocity를 차 전방으로 점진적 정렬 → 유기적 드리프트
        float grip = Mathf.Lerp(lowSpeedGrip, highSpeedGrip, speedRatio);
        if (speed > 0.5f && Mathf.Abs(forwardSpeed) > 0.5f)
        {
            Vector3 targetDir = forwardSpeed >= 0f ? transform.forward : -transform.forward;
            float steerGripReduction = Mathf.Lerp(1f, 0.2f, Mathf.Abs(inputTurn));
            float blendT = 1f - Mathf.Exp(-grip * gripRate * steerGripReduction * Time.fixedDeltaTime);
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, targetDir * Mathf.Min(speed, maxSpeed), blendT);
        }

        // 스티어링
        if (Mathf.Abs(forwardSpeed) > 0.5f)
        {
            float turnMult = Mathf.Lerp(1f, 0.4f, speedRatio);
            float turnDir = inputTurn * Mathf.Sign(forwardSpeed);
            Quaternion deltaRot = Quaternion.Euler(0f, turnDir * turnSpeed * turnMult * Time.fixedDeltaTime, 0f);
            _rb.MoveRotation(_rb.rotation * deltaRot);
        }
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
