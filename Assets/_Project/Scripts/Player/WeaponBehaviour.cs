using UnityEngine;

/// <summary>
/// 무기 4클래스(카타나/대검/권총/드론)의 공통 베이스 — 런당 1택으로 하나만 활성한다.
/// 인터페이스는 얇게 유지한다: 입력 의미를 무기 중립(Primary/Secondary)으로만 받고, 세부 상태는
/// 각 무기 내부에 숨긴다. (codex 권고 — 4무기 합집합 인터페이스 금지. 카타나를 먼저 구체화하고,
/// 공통화는 2번째 무기 구현 때 실제 중복이 보일 때 추출한다.)
///
/// 공격 판정은 코드 타이머가 아니라 클립 AnimationEvent(<see cref="PlayerAnimatorDriver.AttackHit"/>)로
/// 발동한다 — 애니가 진실. 단 self-cancel 하드컷으로 이벤트가 누락될 수 있어, 구체 무기가 타이머 폴백을 둔다.
/// <see cref="PlayerBrain"/>이 Initialize로 소유자·애니 드라이버를 주입하고 매 프레임 Tick한다.
/// </summary>
public abstract class WeaponBehaviour : MonoBehaviour
{
    protected Transform Owner { get; private set; }                  // 판정 원점(플레이어 루트)
    protected PlayerAnimatorDriver AnimatorDriver { get; private set; }

    public virtual void Initialize(Transform owner, PlayerAnimatorDriver animator)
    {
        Owner = owner;
        // 재진입 가드(Stab H-1 / Codex ③): 재초기화 시 이전 구독을 먼저 해제 — 이중 구독 사고(OperationTimer 동형) 차단.
        if (AnimatorDriver != null) AnimatorDriver.AttackHit -= OnHitFrame;
        AnimatorDriver = animator;
        if (animator != null) animator.AttackHit += OnHitFrame;
    }

    public virtual void Cleanup()
    {
        if (AnimatorDriver != null) AnimatorDriver.AttackHit -= OnHitFrame;
    }

    // 무기가 Brain보다 먼저 파괴돼도 구독이 살아남지 않게(Codex ③). −= 는 멱등이라 Brain.OnDestroy의 Cleanup과 중복 안전.
    // ⚠️ 파생 무기가 OnDestroy를 오버라이드하면 base.OnDestroy()를 반드시 호출할 것.
    protected virtual void OnDestroy() => Cleanup();

    [Tooltip("★레일: 액션 요청→Animator 진입까지의 busy 유예(초). 전이 1~2프레임 갭 동안 잠금을 유지해 이동 누수를 막는다.")]
    [SerializeField] float actionPendingGrace = 0.12f;
    float _actionGrace;   // >0 = 액션 진입 대기 중(busy 유지). Animator가 Action 상태에 들면 IsActionPlaying이 이어받는다.

    /// <summary>PlayerBrain이 매 프레임 Aim 다음에 호출. 템플릿 메서드 — 액션 유예를 베이스가 강제 감쇠한 뒤
    /// 구체 무기 <see cref="OnTick"/>을 부른다(구체 무기가 유예 처리를 빼먹을 수 없다 = 레일을 자동으로 탄다).</summary>
    public void Tick(in PlayerInputState input, Vector3 aimDir)
    {
        if (_actionGrace > 0f) _actionGrace -= Time.deltaTime;   // Animator와 같은 시간축(스케일) — 슬로모 중 함께 늘어짐
        OnTick(input, aimDir);
    }

    /// <summary>구체 무기의 프레임 로직(구 Tick). aimDir = 조준 방향(= 공격 방향).</summary>
    protected abstract void OnTick(in PlayerInputState input, Vector3 aimDir);

    /// <summary>★레일: 액션(공격/반격) 애니를 요청하는 순간 호출 — 진입 유예를 켠다. busy가 즉시 true가 되어
    /// Animator 진입 전 이동 누수를 막고, 이후 <see cref="PlayerAnimatorDriver.IsActionPlaying"/>(애니 진실)이 busy를 이어받는다.</summary>
    protected void BeginAction() => _actionGrace = actionPendingGrace;

    /// <summary>★레일: 공격 커밋 중인가 = 요청 유예 OR Animator가 실제 Action 상태(애니가 진실).
    /// 진입 실패(전이 경쟁·시간정지)해도 유예 만료 + IsActionPlaying=false면 자동 해제(자가치유 — 카운터 워치독을 일반화).
    /// PlayerBrain이 이동 잠금 등 우선순위 판정에 쓴다. 구체 무기가 추가 커밋 상태를 더하려면 override 후 base.IsBusy와 OR.</summary>
    public virtual bool IsBusy => _actionGrace > 0f || (AnimatorDriver != null && AnimatorDriver.IsActionPlaying);

    /// <summary>외부(회피 등 최우선 입력)가 현재 동작을 즉시 캔슬할 때 호출 — 상태를 idle로 하드컷.
    /// 베이스는 액션 유예를 끈다(캔슬 즉시 busy 반영). 구체 무기가 override하면 base.Cancel()을 반드시 호출.</summary>
    public virtual void Cancel() => _actionGrace = 0f;

    /// <summary>패링(퍼펙트 회피) 성공 시 호출 — 반격 입력 창을 연다. PlayerBrain이 PlayerHealth.Parried에 배선.
    /// 기본 무동작. 카타나가 Skill02 카운터 창을 연다(다른 무기는 자기 반격을 두거나 무시).</summary>
    public virtual void ArmCounter() { }

    /// <summary>공격 클립의 타격 정점(AnimationEvent OnAttackHit). 구체 무기가 이 프레임에 판정을 실행한다.</summary>
    protected virtual void OnHitFrame(int hitFrameIndex) { }
}
