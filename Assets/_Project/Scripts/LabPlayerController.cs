// 룩 랩 전용 탑다운 플레이어 — "여러 마리 달려오는 Caniathrox를 이동으로 피하기" 테스트용.
// ⚠️실제 게임용 PlayerController/PlayerCombat과 무관한 신규 컨트롤러. 비주얼은 캡슐로 충분(피하기 테스트).
//
// 설계:
//   - 입력 폴링 = Update (WASD + 방향키, 카메라 무관 월드 XZ 기준 — 탑다운이라 화면 방향 = 월드 방향).
//   - 물리 이동 = FixedUpdate (Rigidbody.MovePosition, 부드러운 가감속).
//   - 회전 = 진행 방향으로 부드럽게 바라봄(캡슐이라 시각적 의미는 작지만 "어디로 가는지" 읽힘).
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LabPlayerController : MonoBehaviour
{
    [Header("이동 (피하기 난이도 노브 — 적 이속과의 차이가 회피 여유)")]
    [Tooltip("기본(걷기) 속도(m/s). 적 접근 이속(approachSpeed 7.0)보다 느리게 — 걸으면 적이 따라붙는다.")]
    [SerializeField] float walkSpeed = 5.5f;
    [Tooltip("질주(Shift 홀드) 속도(m/s). 적 접근 이속보다 빠르게 — 질주해야 떨어뜨린다. 적 이속이 걷기<적<질주 사이에 오게 튜닝.")]
    [SerializeField] float sprintSpeed = 9.0f;
    [Tooltip("목표 속도까지 가감속 시간(초). 작을수록 즉각적(스냅), 클수록 미끄러짐.")]
    [SerializeField, Min(0.01f)] float accelTime = 0.08f;
    [Tooltip("진행 방향 회전 속도(도/초). 캡슐 방향 표시용.")]
    [SerializeField, Min(0f)] float turnSpeed = 720f;

    Rigidbody _rb;
    Vector3 _inputDir;          // 이번 프레임 입력 방향(정규화, XZ)
    Vector3 _velocity;          // 현재 평면 속도(SmoothDamp 상태 보존)
    Vector3 _smoothVel;         // SmoothDamp 내부 속도
    bool _sprinting;            // Shift 홀드 중인가(질주) — Update 폴링, FixedUpdate 소비

    /// <summary>현재 평면 속도(m/s) — 카메라 속도 리드 등 외부 참조용.</summary>
    public Vector3 PlanarVelocity => _velocity;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // 탑다운 캐릭터 표준 강체 설정 — 중력/기울어짐 없이 평면만.
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;   // 카메라가 부드럽게 따라오도록
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Update()
    {
        // 카메라 무관 월드 입력 — 탑다운이라 W=+Z(화면 위), D=+X(화면 오른쪽). 단순·예측가능이 회피 가독성에 유리.
        float h = Input.GetAxisRaw("Horizontal");   // A/D + ←/→
        float v = Input.GetAxisRaw("Vertical");     // W/S + ↑/↓
        _inputDir = new Vector3(h, 0f, v);
        if (_inputDir.sqrMagnitude > 1f) _inputDir.Normalize();   // 대각 입력이 더 빠르지 않게

        // 질주: Shift 홀드. 좌/우 Shift 모두. (홀드형 — 떼면 즉시 걷기로 복귀)
        _sprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    void FixedUpdate()
    {
        float speed = _sprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVel = _inputDir * speed;
        // 가감속 — 입력 떼면 부드럽게 멈추고, 방향 전환도 미끄러짐 없이 따라붙음.
        _velocity = Vector3.SmoothDamp(_velocity, targetVel, ref _smoothVel, accelTime,
                                       Mathf.Infinity, Time.fixedDeltaTime);

        if (_velocity.sqrMagnitude > 0.0001f)
            _rb.MovePosition(_rb.position + _velocity * Time.fixedDeltaTime);

        // 진행 방향으로 회전(이동 중일 때만 — 멈췄을 때 마지막 방향 유지).
        if (_inputDir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(_inputDir, Vector3.up);
            Quaternion next = Quaternion.RotateTowards(_rb.rotation, target, turnSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(next);
        }
    }
}
