using UnityEngine;

/// <summary>
/// 플레이어 허트박스(피격받는 쪽) + 체력. <see cref="IDamageable"/> 구현 — 적 공격 코드가 Player 레이어를
/// 쿼리해 GetComponentInParent&lt;IDamageable&gt;로 이 컴포넌트의 TakeHit을 부른다(KatanaWeapon이 적에게 하는 것의 대칭).
///
/// ★무적 연동: <see cref="PlayerMotor.IsInvulnerable"/>(대시 i-frame) 중이면 피해 무시 — 회피가 진짜 회피가 된다.
/// 추가로 피격 직후 짧은 무적(hitInvulnDuration)으로 한 프레임에 여러 번 맞아 즉사하는 것을 막는다(뱀서 관용).
///
/// 허트박스 콜라이더는 플레이어 루트의 기존 캡슐(Player 레이어)을 재사용한다 — 별도 콜라이더 안 만든다.
/// 사망은 이벤트로만 알린다(GameOver/정산 연결은 추후). 디버그 뷰가 <see cref="IsInvulnerable"/>을 읽어 무적 상태를 표시.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("체력")]
    [SerializeField, Min(1)] int maxHp = 100;
    [Tooltip("피격 직후 무적(초) — 한 프레임 다중 피격/즉사 방지. 0이면 끔.")]
    [SerializeField, Min(0f)] float hitInvulnDuration = 0.5f;

    [Header("퍼펙트 회피(보상 창)")]
    [Tooltip("★퍼펙트 회피(패링) 창(초) — 대시 시작 후 이 시간 내에 적 공격이 닿으면 보상(슬로모+반격)+피해 무효. " +
             "★i-frame과 독립(타이밍 기반): 캔슬 대시로 수동 무적이 줄어도 이 창은 그대로 — 스킬 보상은 유지된다. " +
             "짧게 둘수록 '대시 시작 프레임'에만 보상(타이트). ⚠️iframeDuration보다 크게 두면 대시 끝난 뒤 피격에도 패링이 터지니 작게.")]
    [SerializeField, Min(0.02f)] float perfectDodgeWindow = 0.15f;

    PlayerMotor _motor;
    int _hp;
    float _hitIframe;
    bool _parryFiredThisDash;   // 한 대시당 보상 1회만(다중 피격 시 슬로모 중복 발동 방지)
    float _lastDashStart = -999f;   // 직전에 본 대시 시작 시각 — 변화로 새 대시 감지(i-frame 유무와 무관 — 캔슬 대시 대응, Stab H-2)

    public int CurrentHp => _hp;
    public int MaxHp => maxHp;
    public bool IsDead => _hp <= 0;
    /// <summary>현재 무적인가 — 대시 i-frame(회피) OR 피격 직후 무적. 디버그 뷰/피해 게이트가 읽는다.</summary>
    public bool IsInvulnerable => (_motor != null && _motor.IsInvulnerable) || _hitIframe > 0f;

    /// <summary>피해를 입은 직후 (현재 HP, 최대 HP). HUD/피드백 연결용.</summary>
    public event System.Action<int, int> Damaged;
    /// <summary>HP 0 도달. GameOver/정산 연결용.</summary>
    public event System.Action Died;
    /// <summary>★퍼펙트 회피 성공 — 대시 시작 후 perfectDodgeWindow 내에 적 공격이 닿은 순간(대시당 1회).
    /// 슬로모/히트스탑·반격 창 등 보상 트리거. 피격 직후 무적(_hitIframe)은 제외(대시 출처만).</summary>
    public event System.Action Parried;

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
        _hp = maxHp;
#if UNITY_EDITOR
        // dashInvulnerable이 꺼져 있으면 _motor.IsInvulnerable이 항상 false → 퍼펙트 회피 미발화 = Parried/반격 무음(Stab H-1).
        if (_motor != null && !_motor.DashInvulnerable)
            Debug.LogWarning("[PlayerHealth] PlayerMotor.dashInvulnerable=false → 회피 무적이 없어 Parried(퍼펙트 회피)가 절대 동작하지 않습니다. 의도 확인.", this);
#endif
    }

    void Update()
    {
        if (_hitIframe > 0f) _hitIframe -= Time.deltaTime;
        // (퍼펙트 회피 발화 허용 리셋은 TakeHit 진입부로 옮겼다 — 리셋/판정을 원자화해
        //  "Update가 Motor.Tick보다 먼저 도는 프레임" 레이스를 제거, Stab High.)
    }

    /// <summary>IDamageable — 적 공격이 호출. 무적 중이면 무시(회피 성공). 아니면 HP 깎고 피격 무적 부여.</summary>
    public void TakeHit(int damage, Vector3 from, float knockback)
    {
        if (IsDead) return;

        // ★새 대시 감지 → 패링 발화 허용 리셋(대시당 1회). ★Update가 아니라 여기(TakeHit 진입)서 — 리셋과 패링 판정을
        //   원자적으로 묶어 "Update가 Motor.Tick보다 먼저 도는 프레임에 새 대시 직후 피격 시 이전 대시 fired 플래그가
        //   남아 패링 누락"되는 Script Execution Order 레이스를 제거한다(Stab High).
        if (_motor != null && _motor.DashStartTime != _lastDashStart)
        {
            _parryFiredThisDash = false;
            _lastDashStart = _motor.DashStartTime;
        }

        // ★퍼펙트 회피(패링) — 대시 시작 후 perfectDodgeWindow 내에 적 공격이 닿으면 보상(슬로모)+피해 무효.
        //   ★i-frame과 독립(옵션 A): 캔슬 대시로 수동 무적이 줄거나 0이어도 타이밍만 맞으면 패링은 뜬다 —
        //     스킬 보상(퍼펙트 회피)은 살리고, 깎이는 건 패링 못한 일반 피격의 수동 안전뿐.
        //   age는 unscaledTime 기준(슬로모가 창을 안 늘림). dashInvulnerable 켜진 대시만(회피기 정체성). 대시당 1회.
        //   피격 직후 무적(_hitIframe)은 대시 출처가 아니므로 DashStartTime 타이밍에서 자연 제외된다.
        if (_motor != null && _motor.DashInvulnerable && !_parryFiredThisDash)
        {
            float age = Time.unscaledTime - _motor.DashStartTime;
            if (age >= 0f && age <= perfectDodgeWindow)
            {
                _parryFiredThisDash = true;
                Parried?.Invoke();
                return;   // 패링 = 피해 무효(수동 무적 유무와 무관)
            }
        }

        if (IsInvulnerable) return;   // 수동 무적(대시 i-frame 꼬리 OR 피격 직후) — 피해만 무효, 보상 없음(평범 회피)
        if (damage <= 0) return;      // 0이하 피해는 i-frame/이벤트 소모 없이 무시(무적 낭비·가짜 피드백 방지, Stab H-1)

        _hp = Mathf.Max(0, _hp - damage);
        _hitIframe = hitInvulnDuration;
        Damaged?.Invoke(_hp, maxHp);
#if UNITY_EDITOR
        Debug.Log($"[PlayerHealth] took {damage} → {_hp}/{maxHp}", this);   // 에디터 전용 — 빌드 로그 범람 방지
#endif

        if (_hp <= 0)
        {
#if UNITY_EDITOR
            Debug.Log("[PlayerHealth] DEAD", this);
#endif
            Died?.Invoke();
        }
    }
}
