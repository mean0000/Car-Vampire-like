using UnityEngine;

/// <summary>
/// 비주얼 캐릭터(Humanoid Animator)에 붙는다. 두 역할:
///  ① 구동: 매 프레임 Motor/Aim 상태 → 로코모션 파라미터(Speed/MoveX/MoveY/Dash).
///     facing(조준)과 movement(이동)를 분리 — 마우스로 앞을 보며 S로 뒷걸음, D로 우측 스트레이프.
///  ② 릴레이: 공격 클립의 AnimationEvent(<see cref="OnAttackHit"/>)를 받아 <see cref="AttackHit"/>로
///     무기에 전달한다(애니가 진실 — 타격 타이밍은 클립이 소유).
///
/// 파라미터 규약은 Animation 에이전트의 컨트롤러와 고정 합의: Speed/MoveX/MoveY(float), Attack(trigger), Dash(bool).
/// 루트는 안 돌린다(자식 카메라 보호) — 회전은 이 비주얼 transform에만 적용한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorDriver : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동을 추적할 대상. 비우면 부모(플레이어 루트). Aim/Motor는 이 대상의 부모 체인에서 찾는다.")]
    [SerializeField] Transform moveSource;

    [Header("Speed Mapping (m/s → blend)")]
    [SerializeField] float walkSpeedRef = 5f;
    [SerializeField] float runSpeedRef = 8.5f;
    [Tooltip("측정 속도 상한 — 스폰/텔레포트 점프로 인한 블렌드 오버슈트 방지.")]
    [SerializeField] float maxSpeed = 12f;
    [Tooltip("이 속도(m/s) 미만이면 정지로 간주 — 방향(MoveX/Y)을 0으로(회전은 계속).")]
    [SerializeField] float moveThreshold = 0.3f;

    [Header("Damping")]
    [SerializeField] float speedDamp = 0.1f;
    [SerializeField] float dirDamp = 0.08f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int ComboStepHash = Animator.StringToHash("ComboStep");
    static readonly int DashHash = Animator.StringToHash("Dash");

    Animator _animator;
    PlayerAim _aim;
    PlayerMotor _motor;
    Vector3 _lastPos;
    bool _wasDashing;

    /// <summary>공격 클립 타격 정점(AnimationEvent OnAttackHit)이 발화 → 무기가 구독해 판정.</summary>
    public event System.Action<int> AttackHit;
    /// <summary>캔슬 윈도우 시작(AnimationEvent OnComboWindow) — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public event System.Action ComboWindow;
    /// <summary>공격 클립 끝(AnimationEvent OnComboEnd) — 다음 단 안 갔으면 콤보 종료.</summary>
    public event System.Action ComboEnd;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;   // 위치는 PlayerMotor가 주도, 애니는 표현용
        if (moveSource == null) moveSource = transform.parent;
        if (moveSource != null)
        {
            _aim = moveSource.GetComponentInParent<PlayerAim>();
            _motor = moveSource.GetComponentInParent<PlayerMotor>();
        }
    }

    void OnEnable() { if (moveSource != null) _lastPos = moveSource.position; }

    /// <summary>PlayerBrain이 매 프레임 마지막에 호출.</summary>
    public void Tick()
    {
        if (moveSource == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 평면(XZ) 변위만 — 지면 추종 y 변화는 로코모션과 무관.
        Vector3 cur = moveSource.position;
        Vector3 delta = cur - _lastPos; _lastPos = cur; delta.y = 0f;

        // 스폰/텔레포트로 루트가 순간이동하면 가짜 변위로 방향이 튄다 — 기준만 리셋하고 스킵.
        float rawSpeed = delta.magnitude / dt;
        if (rawSpeed > maxSpeed * 3f) return;
        float speed = Mathf.Min(rawSpeed, maxSpeed);
        bool moving = speed > moveThreshold;

        // facing = 조준(없으면 이동 방향). 비주얼(this)만 회전 — 루트는 안 돈다.
        Vector3 face = (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            ? _aim.Direction
            : (moving ? delta.normalized : transform.forward);
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);

        // movement = facing 프레임 투영 → MoveX(우측 스트레이프) / MoveY(전진 +, 뒷걸음 −)
        Vector3 fwd = transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);   // 좌수계: up×fwd = right
        Vector3 moveDir = moving ? delta.normalized : Vector3.zero;
        float moveX = Vector3.Dot(moveDir, right);
        float moveY = Vector3.Dot(moveDir, fwd);

        // 8방향 스냅(딱딱) — 이동 방향을 가장 가까운 45도로 양자화해 블렌드트리에서 한 클립만 100% 활성한다.
        // 대각선 입력 시 F+R+FR 가중 블렌드로 애매해지던 것을 제거(이동 자체는 연속, 애니 방향만 8단계).
        if (moving)
        {
            float ang = Mathf.Atan2(moveX, moveY);                       // 이동 방향각(rad)
            ang = Mathf.Round(ang / (Mathf.PI / 4f)) * (Mathf.PI / 4f);  // 45도 단위 반올림
            moveX = Mathf.Sin(ang);
            moveY = Mathf.Cos(ang);
        }

        // speed 블렌드 0(idle)→1(walk)→2(run)
        float blend = speed <= walkSpeedRef
            ? (walkSpeedRef > 0f ? speed / walkSpeedRef : 0f)
            : 1f + (speed - walkSpeedRef) / Mathf.Max(0.01f, runSpeedRef - walkSpeedRef);
        blend = Mathf.Clamp(blend, 0f, 2f);

        _animator.SetFloat(SpeedHash, blend, speedDamp, dt);
        _animator.SetFloat(MoveXHash, moveX, dirDamp, dt);
        _animator.SetFloat(MoveYHash, moveY, dirDamp, dt);

        // 대시 bool 엣지(상태 변할 때만 SetBool)
        if (_motor != null)
        {
            bool dashing = _motor.IsDashing;
            if (dashing != _wasDashing) { _animator.SetBool(DashHash, dashing); _wasDashing = dashing; }
        }
    }

    /// <summary>콤보 단 설정(0=idle, 1/2/3) — AnimatorController가 ComboStep으로 Combo 상태를 전환한다.</summary>
    public void SetCombo(int step) => _animator.SetInteger(ComboStepHash, step);

    // ── AnimationEvent 수신(함수명 고정 — Animation 에이전트가 이 이름으로 클립에 심는다) ──
    /// <summary>타격 정점 — 무기 판정으로 릴레이.</summary>
    public void OnAttackHit(int hitFrameIndex) => AttackHit?.Invoke(hitFrameIndex);
    /// <summary>캔슬 윈도우 시작 — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public void OnComboWindow() => ComboWindow?.Invoke();
    /// <summary>공격 클립 끝 — 다음 단 안 갔으면 콤보 종료(idle 복귀).</summary>
    public void OnComboEnd() => ComboEnd?.Invoke();
}
