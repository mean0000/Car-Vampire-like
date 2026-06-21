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
    static readonly int DashXHash = Animator.StringToHash("DashX");
    static readonly int DashYHash = Animator.StringToHash("DashY");
    static readonly int CounterHash = Animator.StringToHash("Counter");
    static readonly int Skill01Hash = Animator.StringToHash("Skill01");

    // ★상체 액션 레이어 인덱스(KatanaMelee.controller). 공격/반격/스킬(Action 태그)은 이 레이어에 있고,
    //   Base(0)는 Locomotion+Dash 풀바디. 평시 weight 0(Base가 상체까지)·액션 중에만 weight 1(상체 override).
    const int UpperLayer = 1;

    Animator _animator;
    PlayerAim _aim;
    PlayerMotor _motor;
    Vector3 _lastPos;
    bool _wasDashing;
    bool _attacking;   // 공격 커밋 중 = facing 잠금(_lockedFace)을 적용할지 결정(PlayerBrain이 busy로 매 프레임 갱신).
                       // ★루트모션 게이트로는 안 쓰인다 — 풀 트윈스틱서 공격 런지 폐기, OnAnimatorMove는 IsDashing만 본다.
    Vector3 _lockedFace;   // 콤보 단 시작 시 잠근 facing — 공격 중 몸/런지 방향 고정(단 사이엔 재캡처)

    /// <summary>공격 클립 타격 정점(AnimationEvent OnAttackHit)이 발화 → 무기가 구독해 판정.</summary>
    public event System.Action<int> AttackHit;
    /// <summary>캔슬 윈도우 시작(AnimationEvent OnComboWindow) — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public event System.Action ComboWindow;
    /// <summary>공격 클립 끝(AnimationEvent OnComboEnd) — 다음 단 안 갔으면 콤보 종료.</summary>
    public event System.Action ComboEnd;
    /// <summary>로코모션 클립 발 디딤(AnimationEvent OnFootstep) — PlayerFootsteps가 구독해 발소리.</summary>
    public event System.Action Footstep;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        // 공격 클립은 루트모션(클립에 박힌 거리만큼 전진) — deltaPosition을 받으려면 true.
        // 단 자동적용(자식만 이동)은 OnAnimatorMove 정의로 차단되고, 우리가 공격 중에만 루트(부모)에 수동 적용한다.
        // 로코모션 클립은 In_Place(루트≈0)라 비공격 프레임의 delta는 무시한다.
        _animator.applyRootMotion = true;
        if (moveSource == null) moveSource = transform.parent;
        if (moveSource != null)
        {
            _aim = moveSource.GetComponentInParent<PlayerAim>();
            _motor = moveSource.GetComponentInParent<PlayerMotor>();
        }
        // 배선 실패를 무음으로 두지 않는다 — _motor 없으면 공격 루트모션이 조용히 죽으므로 즉시 노출(Codex).
        if (_motor == null)
            Debug.LogError("[PlayerAnimatorDriver] PlayerMotor를 부모 체인에서 못 찾음 — 공격 루트모션이 적용되지 않는다. 프리팹 계층(비주얼 자식이 PlayerMotor 하위인지) 확인.", this);
    }

    void OnEnable() { if (moveSource != null) _lastPos = moveSource.position; }

    /// <summary>PlayerBrain이 매 프레임 마지막에 호출.</summary>
    public void Tick()
    {
        if (moveSource == null) return;
        // ★상체 액션 레이어 weight — 액션 클립 재생 중에만 1(상체를 override해 공격), 평시 0(Base 풀바디 로코모션이
        //   상체까지 흐름 → 비공격 시 팔 freeze/T포즈 없음). 하드 1/0(즉발). dt와 무관하게 매 프레임 갱신(히트스탑 중에도 유지).
        _animator.SetLayerWeight(UpperLayer, IsActionPlaying ? 1f : 0f);
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

        // facing: 공격 커밋 중엔 단계 시작 시 잠근 방향(런지가 그쪽으로 직진, 마우스 돌려도 안 꺾임).
        // 평시엔 조준(없으면 이동 방향). 비주얼(this)만 회전 — 루트는 안 돈다.
        Vector3 face = (_attacking && _lockedFace.sqrMagnitude > 0.0001f)
            ? _lockedFace
            : (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
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

        // 대시: 시작 엣지에서 방향(어느 Step 클립)을 잠그고, bool 엣지로 상태 진입/복귀를 구동한다.
        // DashX/DashY = facing 프레임의 카디널 방향(우=+X, 전진=+Y) → 블렌드트리가 한 Step 클립을 100% 고른다.
        // 한 동작=한 클립: 대시 진행 중 이 값은 고정(매 프레임 갱신 안 함) — 모션 정체성 보존.
        if (_motor != null)
        {
            if (_motor.DashStartedThisFrame)
            {
                _animator.SetFloat(DashXHash, _motor.DashLocalX);
                _animator.SetFloat(DashYHash, _motor.DashLocalY);
            }
            bool dashAnim = _motor.IsDashing;
            if (dashAnim != _wasDashing) { _animator.SetBool(DashHash, dashAnim); _wasDashing = dashAnim; }
        }
    }

    /// <summary>콤보 단 설정(0=idle, 1/2/3) — AnimatorController가 ComboStep으로 Combo 상태를 전환한다.
    /// 단 시작(step≥1) = 이 단의 facing 잠금 지점(이후 마우스를 돌려도 몸/런지 방향은 이 순간 조준에 고정).</summary>
    public void SetCombo(int step)
    {
        if (step >= 1)
        {
            if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
                _lockedFace = _aim.Direction;
        }
        else _lockedFace = Vector3.zero;   // 콤보 종료 — 잔존 잠금 제거(self-cancel 경로에서 옛 방향 재사용 방지)
        _animator.SetInteger(ComboStepHash, step);
    }

    /// <summary>공격 커밋 여부 — true인 프레임에만 루트모션 변위를 루트로 적용(PlayerBrain이 busy로 갱신).</summary>
    public void SetAttacking(bool attacking) => _attacking = attacking;

    /// <summary>★레일: 지금 '액션' 클립(공격/반격)이 실제로 재생 중인가 — busy의 단일 진실 소스(애니가 진실).
    /// 컨트롤러에서 Combo1/2/3·Counter 상태에 "Action" 태그를 단다. 새 무기/액션은 상태에 같은 태그만 달면
    /// 코드 수정 없이 busy/잠금에 자동 편입(OCP 확장점). 전이 중이면 다음 상태도 봐서 진입 1프레임 갭을 메운다.
    /// ★REQUIRED: 모든 '공격/반격' 액션 상태에 Animator State Tag "Action" 필수. 누락 시 유예(0.12s) 뒤 busy가
    ///   풀려 이동 누수 — 단 KatanaWeapon의 진입 실패 자가치유가 에디터 경고로 잡아준다(런타임 안전망).</summary>
    public bool IsActionPlaying
    {
        get
        {
            if (_animator == null) return false;
            // ★상체 액션 레이어(UpperLayer)를 읽는다 — 공격/반격/스킬 상태와 "Action" 태그는 이제 layer 1에만 있다.
            //   (layer 0을 읽으면 항상 false → busy가 유예 0.12s밖에 못 잡아 이동 누수.)
            if (_animator.GetCurrentAnimatorStateInfo(UpperLayer).IsTag("Action")) return true;
            // 전이 진행 중엔 도착 상태가 Action이면 이미 액션 진입으로 친다(요청→진입 갭의 busy 누수 방지).
            if (_animator.IsInTransition(UpperLayer) && _animator.GetNextAnimatorStateInfo(UpperLayer).IsTag("Action")) return true;
            return false;
        }
    }

    /// <summary>★패링 반격(Skill02) — 컨트롤러 Any→Counter 트리거. 카타나가 카운터 창 입력 시 호출.
    /// 반격은 공격이라 busy로 잠긴다(루트모션은 풀 트윈스틱서 폐기 — 위치는 하체 로코모션 소유). 몸 facing을
    /// 현재 조준에 잠가 히트박스 _aimDir과 통일(콤보 SetCombo의 잠금과 동형). 종료 시 SetCombo(0)이 잠금 해제.
    /// 컨트롤러에 Counter 트리거가 없으면 SetTrigger는 무음 무동작(안전).</summary>
    public void TriggerCounter()
    {
        if (_animator == null) return;
        if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            _lockedFace = _aim.Direction;
        // ★AnyState 경쟁 해소: 직전 대시의 Dash bool이 남아 있으면 Any→Dash(우선순위 0)가 Any→Counter(2)를 이긴다.
        //   반격 요청 시 Dash bool을 즉시 꺼 Counter가 확실히 진입하게 한다(엣지 추적도 동기화).
        _animator.SetBool(DashHash, false);
        _wasDashing = false;
        _animator.SetTrigger(CounterHash);
    }

    /// <summary>★우클릭 스킬(Skill01) — 컨트롤러 Any→Skill01 트리거. Counter와 동형(공격이라 루트모션 적용,
    /// facing 잠금, Dash bool 정리로 AnyState 경쟁 해소). 컨트롤러에 Skill01 트리거가 없으면 무음 무동작(안전).</summary>
    public void TriggerSkill()
    {
        if (_animator == null) return;
        if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            _lockedFace = _aim.Direction;
        _animator.SetBool(DashHash, false);
        _wasDashing = false;
        _animator.SetTrigger(Skill01Hash);
    }

    /// <summary>공격·대시 클립의 루트모션을 루트(PlayerMotor)로 넘긴다 — 애니가 진실(전진/회피 거리는 클립이 소유).
    /// 공격 커밋 중이거나 대시 창 동안만 적용한다. 그 외(In_Place 로코모션, delta≈0)는 무시해
    /// PlayerMotor 이동과 충돌하지 않는다. 자동 루트모션은 비주얼 자식만 옮기므로 쓰지 않고, 이 수동 경로로
    /// 부모 루트를 옮긴다. 위치 단일 소유는 PlayerMotor.ApplyRootStep(같은 프레임 이중 적용 가드 내장).</summary>
    void OnAnimatorMove()
    {
        if (_motor == null) return;
        // ★풀 트윈스틱: 공격 런지(루트모션) 폐기 — 공격 중 위치는 하체 로코모션(PlayerMotor)이 소유한다.
        //   상체 액션 레이어는 마스크로 Root를 제외하므로 deltaPosition에 공격 기여가 없지만, 이중 안전으로 _attacking을 게이트에서 뺀다.
        //   대시 창 동안만 호출하나 ApplyRootStep이 대시엔 양보(코드 버스트가 위치 소유)하므로 실질 무동작 — 구조 일관성용으로 남김.
        if (!_motor.IsDashing) return;
        _motor.ApplyRootStep(_animator.deltaPosition);
    }

    // ── AnimationEvent 수신(함수명 고정 — Animation 에이전트가 이 이름으로 클립에 심는다) ──
    /// <summary>타격 정점 — 무기 판정으로 릴레이.</summary>
    public void OnAttackHit(int hitFrameIndex) => AttackHit?.Invoke(hitFrameIndex);
    /// <summary>캔슬 윈도우 시작 — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public void OnComboWindow() => ComboWindow?.Invoke();
    /// <summary>공격 클립 끝 — 다음 단 안 갔으면 콤보 종료(idle 복귀).</summary>
    public void OnComboEnd() => ComboEnd?.Invoke();
    /// <summary>로코모션 클립 발 디딤 프레임 — 발소리로 릴레이(디바운스는 구독자가).</summary>
    public void OnFootstep() => Footstep?.Invoke();
}
