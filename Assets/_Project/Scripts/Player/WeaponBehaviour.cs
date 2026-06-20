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

    /// <summary>PlayerBrain이 매 프레임 Aim 다음에 호출. aimDir = 조준 방향(= 공격 방향).</summary>
    public abstract void Tick(in PlayerInputState input, Vector3 aimDir);

    /// <summary>공격(콤보 등) 커밋 중인가 — PlayerBrain이 이동 잠금 등 상태 우선순위 판정에 쓴다.</summary>
    public virtual bool IsBusy => false;

    /// <summary>외부(회피 등 최우선 입력)가 현재 동작을 즉시 캔슬할 때 호출 — 상태를 idle로 하드컷.
    /// 기본 무동작. 구체 무기가 콤보/충전 등 진행 상태를 리셋한다(플레이어 self-cancel 캐넌).</summary>
    public virtual void Cancel() { }

    /// <summary>패링(퍼펙트 회피) 성공 시 호출 — 반격 입력 창을 연다. PlayerBrain이 PlayerHealth.Parried에 배선.
    /// 기본 무동작. 카타나가 Skill02 카운터 창을 연다(다른 무기는 자기 반격을 두거나 무시).</summary>
    public virtual void ArmCounter() { }

    /// <summary>공격 클립의 타격 정점(AnimationEvent OnAttackHit). 구체 무기가 이 프레임에 판정을 실행한다.</summary>
    protected virtual void OnHitFrame(int hitFrameIndex) { }
}
