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

    [Header("Tumbling (패링 구르기)")]
    [Tooltip("패링 구르기 애니 재생 속도 배수 — 클수록 빠르고 스냅. 구르기 재생 중에만 적용, 그 외 1. " +
             "★거리에도 영향: 총 이동 ≈ 클립 × tumblingSpeed × tumblingDistanceScale (둘은 곱 관계).")]
    [SerializeField, Min(0.1f)] float tumblingSpeed = 1.5f;
    [Tooltip("패링 구르기 거리 배수 — 루트모션 변위에 곱(클수록 멀리). 1 = (속도 배율 적용된) 원본 거리.")]
    [SerializeField] float tumblingDistanceScale = 1.5f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int ComboStepHash = Animator.StringToHash("ComboStep");
    static readonly int DashHash = Animator.StringToHash("Dash");
    static readonly int DashXHash = Animator.StringToHash("DashX");
    static readonly int DashYHash = Animator.StringToHash("DashY");
    static readonly int TumblingHash = Animator.StringToHash("Tumbling");
    static readonly int CounterHash = Animator.StringToHash("Counter");

    Animator _animator;
    PlayerAim _aim;
    PlayerMotor _motor;
    Vector3 _lastPos;
    bool _wasDashing;
    bool _attacking;   // 공격 커밋 중 = 루트모션을 위치로 적용(PlayerBrain이 매 프레임 갱신)
    bool _tumbling;        // 패링 구르기 중 = Tumbling 클립 루트모션을 위치로 적용
    bool _enteredTumbling; // Tumbling 상태에 실제 진입했나 — 종료(이탈) 감지용
    float _tumblingTimeout; // 안전 타임아웃 — 상태 미진입 시 영구 Dash 억제 방지
    const float TumblingTimeoutSec = 3f;   // Tumbling 클립 길이보다 넉넉(정상 종료는 exited가 먼저, 이건 미진입 안전망)
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

    void OnDisable()
    {
        // ★구르기 중 비활성화(사망·씬전환·비주얼 swap) 시 모터 _tumbling 잔존 → 로코모션 영구 동결 방지(Stab F-1).
        if (_tumbling)
        {
            _tumbling = false;
            _enteredTumbling = false;
            _motor?.SetTumbling(false);
            if (_animator != null) _animator.speed = 1f;   // 비활성화 중에도 재생 속도 원복(전역 잔존 방지)
        }
    }

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
                _tumbling = false;          // 새 대시 시작 — 이전 패링 텀블링 억제 해제(이 대시는 Step으로)
                _enteredTumbling = false;
            }
            // ★대시 애니 선택 직전 체크: 패링 텀블링 중이면 Dash 상태 억제 → Any→Tumbling이 이김(안 씹힘). 이동은 코드 대시 그대로.
            bool dashAnim = _motor.IsDashing && !_tumbling;
            if (dashAnim != _wasDashing) { _animator.SetBool(DashHash, dashAnim); _wasDashing = dashAnim; }
        }

        // 패링 구르기 종료 감지 — Tumbling 상태에 들어갔다 나오면 모터에 로코모션 복귀 통지(루트모션 양도 해제).
        if (_tumbling)
        {
            _tumblingTimeout -= dt;
            bool inTum = _animator.GetCurrentAnimatorStateInfo(0).IsName("Tumbling");
            if (inTum) _enteredTumbling = true;
            bool exited = _enteredTumbling && !inTum && !_animator.IsInTransition(0);
            if (exited || _tumblingTimeout <= 0f)   // 정상 종료 OR 안전 타임아웃(상태 미진입 소프트락 방지)
            {
                if (_tumblingTimeout <= 0f && !_enteredTumbling)
                    Debug.LogWarning("[PlayerAnimatorDriver] Tumbling 타임아웃 — 'Tumbling' 상태 미진입. 컨트롤러 상태명/트랜지션 우선순위 확인.", this);
                _tumbling = false;
                _enteredTumbling = false;
                _motor?.SetTumbling(false);
            }
        }

        // ★재생 속도 매 프레임 구동 — 실제 Tumbling 상태 재생 중에만 배속. 미진입 타임아웃 창·종료 후 즉시 1로 복귀(리셋 누락/스턱 제거, Stab H-1/H-2).
        _animator.speed = (_tumbling && _enteredTumbling) ? Mathf.Max(0.1f, tumblingSpeed) : 1f;
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

    /// <summary>★퍼펙트 회피 — 진행 중 회피를 Step 대신 Tumbling(구르기)으로 전환(컨트롤러 Any→Tumbling 트리거).
    /// 방향은 대시 시작 시 잠근 DashX/DashY를 그대로 쓴다(같은 동작 정체성). 컨트롤러에 Tumbling 파라미터가
    /// 없으면 SetTrigger는 무음 무동작(안전).</summary>
    public void TriggerTumbling()
    {
        // ★[비활성화 2026-06-20] 패링→Tumbling 애니 전환 주석처리(복잡도/효율). 회피=Step 대시 유지. 필요 시 복구.
        /*
        if (_animator == null) return;
        _animator.SetTrigger(TumblingHash);
        _animator.SetBool(DashHash, false);   // ★Any→Dash(우선순위 높음)가 Tumbling을 덮지 않게 Dash bool 즉시 끔 → Any→Tumbling 발동
        _wasDashing = false;                  // Dash bool 직접 끈 것과 엣지 추적 동기화
        _tumbling = true;                     // Dash 애니 억제 + 루트모션 적용 게이트
        _enteredTumbling = false;
        _tumblingTimeout = TumblingTimeoutSec; // 순수 안전망(상태 미진입 소프트락 방지) — 어떤 Tumbling 클립보다 넉넉히
        _motor?.SetTumbling(true);            // 코드 대시 끊고 위치를 클립 루트모션에 — 공중회전 궤적/역동감은 클립이 소유
        // 재생 속도는 Tick이 매 프레임 구동(여기서 직접 안 건드림) — 리셋 누락 방지(Stab H-1/H-2).
        */
    }

    /// <summary>★패링 반격(Skill02) — 컨트롤러 Any→Counter 트리거. 카타나가 카운터 창 입력 시 호출.
    /// 반격은 공격이라 IsBusy(=_attacking)로 루트모션이 적용된다(별도 게이트 불필요). 몸 facing을
    /// 현재 조준에 잠가 히트박스 _aimDir과 통일(콤보 SetCombo의 잠금과 동형). 종료 시 SetCombo(0)이 잠금 해제.
    /// 컨트롤러에 Counter 트리거가 없으면 SetTrigger는 무음 무동작(안전).</summary>
    public void TriggerCounter()
    {
        if (_animator == null) return;
        if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            _lockedFace = _aim.Direction;
        _animator.SetTrigger(CounterHash);
    }

    /// <summary>공격·대시 클립의 루트모션을 루트(PlayerMotor)로 넘긴다 — 애니가 진실(전진/회피 거리는 클립이 소유).
    /// 공격 커밋 중이거나 대시 창 동안만 적용한다. 그 외(In_Place 로코모션, delta≈0)는 무시해
    /// PlayerMotor 이동과 충돌하지 않는다. 자동 루트모션은 비주얼 자식만 옮기므로 쓰지 않고, 이 수동 경로로
    /// 부모 루트를 옮긴다. 위치 단일 소유는 PlayerMotor.ApplyRootStep(같은 프레임 이중 적용 가드 내장).</summary>
    void OnAnimatorMove()
    {
        if (_motor == null) return;
        if (!_attacking && !_motor.IsDashing && !_tumbling) return;   // 텀블링 중엔 클립 루트모션을 위치로 적용(공중회전 궤적)
        Vector3 delta = _animator.deltaPosition;
        if (_tumbling && tumblingDistanceScale > 0f) delta *= tumblingDistanceScale;   // 구르기 거리 배수(루트모션 변위 스케일)
        _motor.ApplyRootStep(delta);
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
