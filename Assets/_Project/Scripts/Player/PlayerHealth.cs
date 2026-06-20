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
    [Tooltip("★퍼펙트 회피 창(초) — 대시 시작 후 이 시간 내에 적 공격이 닿아야 보상(슬로모). i-frame 전체보다 짧게 두어 " +
             "'대시 시작 프레임'에만 보상한다. 대시 끝(꼬리)에 우연히 막힌 건 평범 회피(피해만 막고 보상 X). " +
             "★i-frame(PlayerMotor.iframeDuration)은 넉넉히 두고 이것만 좁혀라 — 회피 자체 시간은 안 줄어든다.")]
    [SerializeField, Min(0.02f)] float perfectDodgeWindow = 0.15f;

    PlayerMotor _motor;
    int _hp;
    float _hitIframe;
    bool _parryFiredThisDash;   // 한 대시당 보상 1회만(다중 피격 시 슬로모 중복 발동 방지)
    bool _wasMotorInvuln;       // 대시 무적 상승 엣지 감지 → 새 대시마다 보상 허용 리셋

    public int CurrentHp => _hp;
    public int MaxHp => maxHp;
    public bool IsDead => _hp <= 0;
    /// <summary>현재 무적인가 — 대시 i-frame(회피) OR 피격 직후 무적. 디버그 뷰/피해 게이트가 읽는다.</summary>
    public bool IsInvulnerable => (_motor != null && _motor.IsInvulnerable) || _hitIframe > 0f;

    /// <summary>피해를 입은 직후 (현재 HP, 최대 HP). HUD/피드백 연결용.</summary>
    public event System.Action<int, int> Damaged;
    /// <summary>HP 0 도달. GameOver/정산 연결용.</summary>
    public event System.Action Died;
    /// <summary>★회피 성공 — 대시 무적(회피)이 적 공격을 무효화한 순간(대시당 1회). 슬로모 등 보상 트리거.
    /// (Tumbling 비활성으로 좁은 패링 창 제거 — 대시로 공격을 막으면 무조건 발화. 피격 직후 무적은 제외.)</summary>
    public event System.Action Parried;

    void Awake()
    {
        _motor = GetComponent<PlayerMotor>();
        if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
        _hp = maxHp;
#if UNITY_EDITOR
        // dashInvulnerable이 꺼져 있으면 _motor.IsInvulnerable이 항상 false → PerfectDodge 미발화 = Tumbling 무음 사망(Stab H-1).
        if (_motor != null && !_motor.DashInvulnerable)
            Debug.LogWarning("[PlayerHealth] PlayerMotor.dashInvulnerable=false → 회피 무적이 없어 Parried/Tumbling이 절대 동작하지 않습니다. 의도 확인.", this);
#endif
    }

    void Update()
    {
        if (_hitIframe > 0f) _hitIframe -= Time.deltaTime;
        // 새 대시 시작(모터 무적 false→true 엣지)마다 PerfectDodge 발화 허용 리셋 — 대시당 1회 보장.
        bool motorInvuln = _motor != null && _motor.IsInvulnerable;
        if (motorInvuln && !_wasMotorInvuln) _parryFiredThisDash = false;
        _wasMotorInvuln = motorInvuln;
    }

    /// <summary>IDamageable — 적 공격이 호출. 무적 중이면 무시(회피 성공). 아니면 HP 깎고 피격 무적 부여.</summary>
    public void TakeHit(int damage, Vector3 from, float knockback)
    {
        if (IsDead) return;
        if (IsInvulnerable)
        {
            // ★퍼펙트 회피: 대시 '시작 프레임'(perfectDodgeWindow 내)에 적 공격이 닿았을 때만 보상(슬로모).
            //   대시 끝(꼬리)에 우연히 i-frame으로 막힌 건 평범 회피(피해만 막고 보상 X) — 대시-끝 슬로모 방지.
            //   age는 unscaledTime 기준이라 슬로모가 창을 늘리거나 줄이지 않는다. 피격 직후 무적(_hitIframe)은 제외(대시 출처만). 대시당 1회.
            if (_motor != null && _motor.IsInvulnerable && !_parryFiredThisDash)
            {
                float age = Time.unscaledTime - _motor.DashStartTime;
                if (age >= 0f && age <= perfectDodgeWindow)
                {
                    _parryFiredThisDash = true;
                    Parried?.Invoke();
                }
            }
            return;   // 무적 — 피해 무효
        }
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
