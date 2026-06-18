using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카타나 — 좌클릭 콤보 3단(S1_Combo01_01→02→03). 각 좌클릭 '누름'이 다음 단을 낸다(홀드 자동연타 ❌).
///
/// 입력 버퍼: 공격 모션 중 누른 클릭을 inputBufferTime 동안 저장 → 캔슬 윈도우(클립 AnimationEvent
/// OnComboWindow)가 열리면 다음 단으로 캔슬 전환(격투게임식 관용). 타격/윈도우/끝 타이밍은 전부 클립
/// AnimationEvent가 소유한다(애니가 진실) — 코드는 입력 버퍼와 단 관리만 한다.
///
/// 판정 대상은 IDamageable. 발도/참격파는 카타나 카드 확정 후 별도로 얹는다.
/// </summary>
public class KatanaWeapon : WeaponBehaviour
{
    [Header("콤보")]
    [Tooltip("콤보 최대 단수.")]
    [SerializeField, Min(1)] int comboMax = 3;
    [Tooltip("입력 버퍼(초) — 공격 모션 중 누른 클릭을 이 시간 저장해 캔슬 윈도우가 열릴 때 발동. " +
             "캔슬창 타이밍(Combo1 0.483s)보다 길어야 1단 직후 얼리 클릭이 안 씹힌다(Stab M-2).")]
    [SerializeField, Min(0f)] float inputBufferTime = 0.5f;
    [Tooltip("1단 시작 직후 재시작 방지 최소 간격(초).")]
    [SerializeField, Min(0f)] float startCooldown = 0.08f;

    [Header("타격(공통)")]
    [SerializeField] float arcHalfAngle = 50f;
    [SerializeField] float range = 1.8f;
    [SerializeField] int damage = 3;
    [SerializeField] float knockback = 4f;
    [SerializeField] float eyeHeight = 1f;

    [Header("Layers")]
    [SerializeField] LayerMask enemyMask = 1 << 7;
    [SerializeField] LayerMask obstacleMask = 1 << 8;

    int _step;            // 0=idle, 1..comboMax 진행 중
    bool _windowOpen;     // 캔슬 윈도우(다음 단 입력 가능) — AnimationEvent가 연다
    bool _buffered;       // 입력 버퍼
    float _bufferTimer;
    bool _hitDone;        // 현재 단 타격 1회 가드
    float _startCdTimer;
    float _lastAdvanceTime = -1f;   // 마지막 Advance 시각 — 직후 이전 클립의 지연 OnComboEnd를 무시(Stab M-1 경합 방어)

    Vector3 _aimDir = Vector3.forward;
    readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
    readonly Collider[] _overlap = new Collider[64];

    public override void Initialize(Transform owner, PlayerAnimatorDriver animator)
    {
        // 재진입 가드(Stab H-2): 재초기화 시 콤보 이벤트 이전 구독 먼저 해제(base는 AttackHit 가드 내장).
        if (AnimatorDriver != null)
        {
            AnimatorDriver.ComboWindow -= OnComboWindow;
            AnimatorDriver.ComboEnd -= OnComboEnd;
        }
        base.Initialize(owner, animator);   // AttackHit += OnHitFrame (재진입 가드 내장)
        if (animator != null)
        {
            animator.ComboWindow += OnComboWindow;
            animator.ComboEnd += OnComboEnd;
        }
    }

    public override void Cleanup()
    {
        if (AnimatorDriver != null)
        {
            AnimatorDriver.ComboWindow -= OnComboWindow;
            AnimatorDriver.ComboEnd -= OnComboEnd;
        }
        base.Cleanup();
    }

    /// <summary>콤보 진행 중(1단 이상)이면 공격 커밋 — 이동 잠금(제자리 공격).</summary>
    public override bool IsBusy => _step >= 1;

    public override void Tick(in PlayerInputState input, Vector3 aimDir)
    {
        if (aimDir.sqrMagnitude > 0.0001f) _aimDir = aimDir;
        float dt = Time.deltaTime;
        if (_startCdTimer > 0f) _startCdTimer -= dt;

        // 각 좌클릭 '누름'이 콤보 입력. idle이면 1단 시작, 진행 중이면 버퍼.
        if (input.primaryDown)
        {
            if (_step == 0)
            {
                if (_startCdTimer <= 0f) BeginCombo();
            }
            else
            {
                _buffered = true;
                _bufferTimer = inputBufferTime;
            }
        }

        // 입력 버퍼 감쇠
        if (_buffered)
        {
            _bufferTimer -= dt;
            if (_bufferTimer <= 0f) _buffered = false;
        }

        // 캔슬 윈도우 열렸고 버퍼 입력 있으면 다음 단으로 캔슬 전환.
        if (_windowOpen && _buffered && _step >= 1 && _step < comboMax)
            Advance();
    }

    void BeginCombo()
    {
        _step = 1;
        _windowOpen = false;
        _hitDone = false;
        _startCdTimer = startCooldown;
        _lastAdvanceTime = -1f;
        AnimatorDriver?.SetCombo(1);
    }

    void Advance()
    {
        _buffered = false;
        _windowOpen = false;
        _hitDone = false;
        _step++;
        _lastAdvanceTime = Time.time;
        AnimatorDriver?.SetCombo(_step);
    }

    // ── AnimationEvent 릴레이(PlayerAnimatorDriver 경유) — 타이밍은 클립이 소유 ──
    protected override void OnHitFrame(int hitFrameIndex)   // 타격 정점
    {
        if (_step >= 1 && !_hitDone) { _hitDone = true; DoSwingHit(); }
    }

    void OnComboWindow()   // 캔슬 윈도우 시작 — 다음 단 입력 받기 시작(각 좌클릭이 각 단)
    {
        if (_step >= 1) _windowOpen = true;
    }

    void OnComboEnd()      // 현재 단 클립 끝 — 다음 단으로 안 넘어갔으면 콤보 종료(idle 복귀)
    {
        // Stab M-1 방어: Advance 직후엔 이전 클립이 CUT로 중단되며 그 OnComboEnd가 1프레임 늦게 샐 수 있다.
        // 그 지연 발화는 막 시작한 다음 단을 잘못 종료시키므로, Advance 후 짧은 관용창 내 OnComboEnd는 무시한다.
        // (Combo 클립 길이가 0.1s를 크게 웃돌아 현재 단의 정상 종료는 막지 않는다.)
        if (_lastAdvanceTime >= 0f && Time.time - _lastAdvanceTime < 0.1f) return;
        _step = 0;
        _windowOpen = false;
        _buffered = false;
        _lastAdvanceTime = -1f;
        AnimatorDriver?.SetCombo(0);
    }

    void DoSwingHit()
    {
        if (Owner == null) return;
        Vector3 origin = Owner.position;
        Vector3 eye = origin + Vector3.up * eyeHeight;
        float gather = range + 0.5f;
        const float pointBlank = 0.9f;

        _hitThisSwing.Clear();
        int n = Physics.OverlapSphereNonAlloc(origin, gather, _overlap, enemyMask, QueryTriggerInteraction.Collide);
        if (n == _overlap.Length)
            Debug.LogWarning("[KatanaWeapon] OverlapSphere 버퍼(64)가 가득 — 일부 타격 누락 가능(버퍼 증대 검토).");
        for (int i = 0; i < n; i++)
        {
            var dmg = _overlap[i].GetComponentInParent<IDamageable>();
            if (dmg == null || _hitThisSwing.Contains(dmg)) continue;

            Vector3 to = _overlap[i].transform.position - origin; to.y = 0f;
            float dist = to.magnitude;
            if (dist > range) continue;
            if (dist > pointBlank)
            {
                if (Vector3.Angle(_aimDir, to) > arcHalfAngle) continue;
                Vector3 los = (_overlap[i].transform.position + Vector3.up * eyeHeight) - eye;
                float ll = los.magnitude;
                if (ll > 0.001f && Physics.Raycast(eye, los / ll, ll, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
            }
            _hitThisSwing.Add(dmg);
            dmg.TakeHit(damage, origin, knockback);
        }
    }
}
