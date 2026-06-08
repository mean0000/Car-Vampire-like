using UnityEngine;

/// <summary>
/// 톱다운 트윈스틱 플레이어의 비주얼 로코모션 드라이버.
///
/// 핵심: facing(어디를 보는가)과 movement(어디로 가는가)를 분리한다.
/// - facing  = 마우스 조준 방향(PlayerCombat.AimDirection). 정지해도 항상 조준을 향해 회전.
/// - movement= PlayerController가 옮기는 실제 변위. 조준 프레임에 투영해 (MoveX,MoveY)로 환산.
///   → 마우스는 앞을 보는데 S로 뒤로 가면 "앞 보며 뒷걸음", D면 "앞 보며 우측 스트레이프".
/// - speed   = 변위 크기(m/s)를 idle(0)/walk(1)/run(2) 블렌드 값으로 매핑.
///
/// PlayerController는 transform.position만 옮기고(물리X·회전X) 루트를 절대 안 돌린다.
/// 회전은 이 컴포넌트(CharacterVisual)에만 적용 — 루트를 돌리면 자식 카메라까지 돈다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerLocomotionAnimator : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동을 추적할 대상. 비우면 부모(Player 루트)를 사용.")]
    [SerializeField] Transform moveSource;
    [Tooltip("조준 방향 소스. 비우면 moveSource에서 PlayerCombat을 찾는다. 없으면 이동 방향으로 facing 폴백.")]
    [SerializeField] PlayerCombat aimSource;

    [Header("Speed Mapping (m/s → blend)")]
    [Tooltip("이 속도에서 Speed 블렌드=1(걷기).")]
    [SerializeField] float walkSpeedRef = 5f;
    [Tooltip("이 속도에서 Speed 블렌드=2(달리기).")]
    [SerializeField] float runSpeedRef = 8.5f;
    [Tooltip("측정 속도 상한 — 스폰/텔레포트 점프로 인한 블렌드 오버슈트 방지.")]
    [SerializeField] float maxSpeed = 12f;
    [Tooltip("이 속도(m/s) 미만이면 정지로 간주 — 방향(MoveX/Y)을 0으로(회전은 계속).")]
    [SerializeField] float moveThreshold = 0.3f;

    [Header("Damping")]
    [SerializeField] float speedDamp = 0.1f;
    [Tooltip("방향(MoveX/MoveY) 댐핑 — 급격한 방향 전환을 부드럽게.")]
    [SerializeField] float dirDamp = 0.08f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");

    Animator _animator;
    Vector3 _lastPos;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;   // 위치는 PlayerController가 주도, 애니는 표현용
        if (moveSource == null) moveSource = transform.parent;
        if (aimSource == null && moveSource != null) aimSource = moveSource.GetComponentInParent<PlayerCombat>();
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

        // 평면(XZ) 변위만 — 지면 추종에 의한 y 변화는 로코모션과 무관.
        Vector3 cur = moveSource.position;
        Vector3 delta = cur - _lastPos;
        _lastPos = cur;
        delta.y = 0f;

        // 스폰/텔레포트/리스폰으로 루트가 순간이동하면 한 프레임 변위가 비물리적으로 커진다.
        // 그 가짜 변위로 방향(MoveX/Y)이 엉뚱하게 튀지 않도록, 비물리 속도면 기준점만 리셋하고 스킵.
        float rawSpeed = delta.magnitude / dt;
        if (rawSpeed > maxSpeed * 3f) return;   // _lastPos는 이미 cur로 갱신됨 — 다음 프레임부터 정상 측정

        float speed = Mathf.Min(rawSpeed, maxSpeed);
        bool moving = speed > moveThreshold;

        // --- facing: 조준 방향(없으면 이동 방향) ---
        Vector3 face;
        if (aimSource != null && aimSource.AimDirection.sqrMagnitude > 0.0001f)
            face = aimSource.AimDirection;
        else if (moving)
            face = delta.normalized;
        else
            face = transform.forward;   // 정지 + 조준원 없음 → 현재 방향 유지

        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
        {
            face.Normalize();
            // 몸 회전은 조준(_aimDir)을 즉시 추종한다. 트레일링(지연)은 조준 쪽(PlayerCombat.aimResponsiveness)에서만
            // 만들어지므로, 몸은 그 "이미 지연된 조준"을 그대로 바라본다 → 몸·탄도가 한 박자로 정확히 일치.
            transform.rotation = Quaternion.LookRotation(face, Vector3.up);
        }

        // --- movement: 조준(facing) 프레임에 투영 → (MoveX 우측, MoveY 전방) ---
        Vector3 facingFwd = transform.forward; facingFwd.y = 0f;
        if (facingFwd.sqrMagnitude < 0.0001f) facingFwd = Vector3.forward;   // 루트가 수직을 보면 zero-벡터화 방지
        facingFwd.Normalize();
        Vector3 facingRight = Vector3.Cross(Vector3.up, facingFwd);   // 좌수계: up×fwd = right

        Vector3 moveDir = moving ? delta.normalized : Vector3.zero;
        float moveX = Vector3.Dot(moveDir, facingRight);   // + = 우측 스트레이프
        float moveY = Vector3.Dot(moveDir, facingFwd);     // + = 전진, - = 뒷걸음

        // --- speed 블렌드 매핑: 0(idle)→1(walk)→2(run) ---
        float blendSpeed;
        if (speed <= walkSpeedRef)
            blendSpeed = walkSpeedRef > 0f ? speed / walkSpeedRef : 0f;
        else
            blendSpeed = 1f + (speed - walkSpeedRef) / Mathf.Max(0.01f, runSpeedRef - walkSpeedRef);
        blendSpeed = Mathf.Clamp(blendSpeed, 0f, 2f);

        _animator.SetFloat(SpeedHash, blendSpeed, speedDamp, dt);
        _animator.SetFloat(MoveXHash, moveX, dirDamp, dt);
        _animator.SetFloat(MoveYHash, moveY, dirDamp, dt);
    }
}
