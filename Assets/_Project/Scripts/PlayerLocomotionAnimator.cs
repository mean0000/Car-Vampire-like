using UnityEngine;

/// <summary>
/// 톱다운 플레이어의 비주얼 로코모션 드라이버.
/// PlayerController는 transform.position을 직접 옮기므로(물리X·회전X), 이 스크립트는
/// "추적 대상(기본=부모 = Player 루트)"의 평면 위치 변화로 속도를 역산해 Animator의
/// Speed(블렌드트리)에 먹이고, 캐릭터 비주얼 자신만 이동 방향으로 회전시킨다.
///
/// PlayerController/씬 카메라(Player 자식)를 건드리지 않기 위해, 회전은 이 컴포넌트가
/// 붙은 GameObject(CharacterVisual)에만 적용한다 — 루트를 돌리면 카메라까지 돈다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerLocomotionAnimator : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동을 추적할 대상. 비우면 부모(Player 루트)를 사용.")]
    [SerializeField] Transform moveSource;

    [Header("Tuning")]
    [Tooltip("Speed 파라미터 댐핑(클수록 부드럽지만 반응 느림).")]
    [SerializeField] float speedDamp = 0.1f;
    [Tooltip("측정 속도 상한 — 스폰/텔레포트 점프로 인한 블렌드 오버슈트 방지.")]
    [SerializeField] float maxSpeed = 9f;
    [Tooltip("회전 속도(도/초).")]
    [SerializeField] float turnSpeed = 720f;
    [Tooltip("이 속도(m/s) 미만이면 정지로 간주 — 회전·방향 갱신 안 함.")]
    [SerializeField] float moveThreshold = 0.3f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    Animator _animator;
    Vector3 _lastPos;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;   // 위치는 PlayerController가 주도, 애니는 표현용
        if (moveSource == null) moveSource = transform.parent;
    }

    void OnEnable()
    {
        if (moveSource != null) _lastPos = moveSource.position;
    }

    void Update()
    {
        if (moveSource == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 cur = moveSource.position;
        Vector3 delta = cur - _lastPos;
        _lastPos = cur;

        // 평면(XZ) 변위만 사용 — 지면 추종에 의한 y 변화는 로코모션과 무관.
        delta.y = 0f;
        float speed = Mathf.Min(delta.magnitude / dt, maxSpeed);

        _animator.SetFloat(SpeedHash, speed, speedDamp, dt);

        // 이동 중일 때만 진행 방향으로 회전. 정지 시 마지막 방향 유지.
        // 임계는 속도(m/s) 기준 — 프레임당 변위로 비교하면 프레임레이트에 따라 달라짐.
        if (speed > moveThreshold)
        {
            Quaternion target = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }
    }
}
