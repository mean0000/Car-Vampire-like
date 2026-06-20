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

    [Header("타격 — 콤보 단별")]
    [Tooltip("★통합 콤보 공격 세트(판정+슬래시 단일 진실). PlayerAttackVfx와 같은 에셋을 넣는다. " +
             "단별 range/arc/offset/damage/knockback과 슬래시를 이 SO에서 조정.")]
    [SerializeField] ComboAttackSet comboSet;
    [Tooltip("시야(LOS) 레이캐스트 눈높이(m) — 공통.")]
    [SerializeField] float eyeHeight = 1f;

    /// <summary>이 단의 실효 사거리 = range(폴백 1.8) × (rangeFromSlashScale면 그 단 슬래시 scale).</summary>
    float EffectiveRange(in ComboAttackSet.ComboAttackStep h)
    {
        float r = h.range > 0f ? h.range : 1.8f;
        if (h.rangeFromSlashScale) r *= (h.scale > 0f ? h.scale : 1f);
        return r;
    }

    [Header("Layers")]
    [SerializeField] LayerMask enemyMask = 1 << 7;
    [SerializeField] LayerMask obstacleMask = 1 << 8;

    [Header("카운터 (패링 반격 — Skill02)")]
    [Tooltip("패링 성공 후 반격(Skill02) 입력 창(초, 실시간). 이 안에 좌클릭하면 반격 발동.")]
    [SerializeField] float counterWindow = 0.6f;
    [Tooltip("반격 사거리(m).")]
    [SerializeField] float counterRange = 3f;
    [Tooltip("반격 부채꼴 半각(deg).")]
    [SerializeField] float counterArcHalf = 70f;
    [Tooltip("반격 히트존 전진(m).")]
    [SerializeField] float counterForwardOffset = 0.5f;
    [Tooltip("반격 데미지(보상 — 콤보보다 강하게).")]
    [SerializeField] int counterDamage = 12;
    [Tooltip("반격 넉백(m/s).")]
    [SerializeField] float counterKnockback = 6f;
    [Tooltip("반격 안전 워치독(초, 스케일 시간) — Skill02 클립(≈2.58s)+여유. 정상 종료는 클립 OnComboEnd가 하지만, " +
             "이벤트 누락/하드컷 인터럽트 시 _countering 고착(반 소프트락)을 이 시간 뒤 강제 종료로 막는다(Stab H-1·Codex #1 수렴). " +
             "★진행플래그 자가치유(OnTick reconcile)가 더 빨리 잡으므로 현재는 백스톱 성격.")]
    [SerializeField] float counterMaxDuration = 3.5f;

    [Header("스킬 (RMB — Skill01)")]
    [Tooltip("★스킬 데이터 SO(판정+타이밍+VFX+사운드 통합). 비우면 RMB 스킬 비활성. " +
             "ComboAttackSet과 같은 데이터 주도 규약 — Katana_Cham_Skill01Set 같은 에셋을 넣는다.")]
    [SerializeField] SkillSet skillSet;
    [Tooltip("★스킬 VFX가 Weapon 기준일 때(슬래시) 정합 앵커 — 무기(칼) transform. 콤보 슬래시와 같은 Katana_Mesh를 넣는다. " +
             "Player 기준(불렛)일 땐 안 쓰임.")]
    [SerializeField] Transform weaponAnchor;

    int _step;            // 0=idle, 1..comboMax 진행 중
    bool _windowOpen;     // 캔슬 윈도우(다음 단 입력 가능) — AnimationEvent가 연다
    bool _buffered;       // 입력 버퍼
    float _bufferTimer;
    bool _hitDone;        // 현재 단 타격 1회 가드
    float _startCdTimer;
    float _lastAdvanceTime = -1f;   // 마지막 Advance 시각 — 직후 이전 클립의 지연 OnComboEnd를 무시(Stab M-1 경합 방어)
    float _counterTimer;            // 패링 후 반격 입력 창(unscaled — 슬로모 무관, 관대)
    bool _countering;               // 반격(Skill02) 진행 중 — 콤보 _step과 독립
    float _counterFallbackTimer;    // 반격 안전 워치독(스케일 시간) — OnComboEnd 누락 시 강제 종료
    bool _skilling;                 // 스킬(Skill01) 진행 중 — _step/_countering과 독립
    float _skillCdTimer;            // 스킬 쿨다운 잔여
    float _skillFallbackTimer;      // 스킬 안전 워치독(스케일 시간)

    Vector3 _aimDir = Vector3.forward;    // 단 시작 시 잠근 조준 — 타격 판정 방향(facing/런지와 통일). 단 진행 중 고정.
    Vector3 _liveAim = Vector3.forward;   // 매 프레임 최신 조준 — 단 시작(BeginCombo/Advance)에 _aimDir로 캡처.
    readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
    readonly Collider[] _overlap = new Collider[128];

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

    // busy는 베이스 레일(WeaponBehaviour.IsBusy = 액션유예 OR AnimatorDriver.IsActionPlaying)이 소유한다.
    // _step/_countering은 이제 '진행 로직'(어느 단·반격 분기)만 담당하고 busy를 직접 정하지 않는다(애니가 진실).

    /// <summary>패링 성공 시 호출 — 반격(Skill02) 입력 창을 연다. PlayerBrain이 PlayerHealth.Parried에 배선.</summary>
    public override void ArmCounter() => _counterTimer = counterWindow;

    /// <summary>회피 등 최우선 입력에 의한 콤보 즉시 캔슬 — idle 하드컷(self-cancel 캐넌).
    /// 대시 비주얼은 Animator의 Any→Dash가 덮는다. 콤보 단/버퍼/윈도우 전부 리셋.</summary>
    public override void Cancel()
    {
        _step = 0;
        _windowOpen = false;
        _buffered = false;
        _hitDone = false;
        _countering = false;     // 회피가 반격을 가로채면 반격도 취소(최우선 입력)
        _counterTimer = 0f;
        _counterFallbackTimer = 0f;
        _skilling = false;       // 회피가 스킬도 가로챔(쿨다운은 소비 유지 — 환불 안 함)
        _skillFallbackTimer = 0f;
        _lastAdvanceTime = -1f;
        AnimatorDriver?.SetCombo(0);
        base.Cancel();           // 레일: 액션 유예도 끔(캔슬 즉시 busy 해제)
    }

    protected override void OnTick(in PlayerInputState input, Vector3 aimDir)
    {
        if (aimDir.sqrMagnitude > 0.0001f) _liveAim = aimDir;   // 최신 조준 보관 — 단 시작 시 잠근다(진행 중엔 _aimDir 고정).
        float dt = Time.deltaTime;
        if (_startCdTimer > 0f) _startCdTimer -= dt;
        if (_counterTimer > 0f) _counterTimer -= Time.unscaledDeltaTime;   // 반격 창은 실시간(슬로모가 늘리거나 줄이지 않음 — 관대)
        // ★반격 워치독(백스톱): 정상 종료는 클립 OnComboEnd(@0.92), 더 빠른 복구는 아래 진행플래그 자가치유. 이건 최후 방어선 —
        //   둘 다 놓쳐 _countering이 고착(반 소프트락)되는 극단 케이스용. 클립 재생은 스케일 시간이라 Time.deltaTime로 감쇠(정렬).
        if (_countering)
        {
            _counterFallbackTimer -= Time.deltaTime;
            if (_counterFallbackTimer <= 0f) EndCounter();
        }
        if (_skillCdTimer > 0f) _skillCdTimer -= dt;
        if (_skilling)
        {
            _skillFallbackTimer -= Time.deltaTime;
            if (_skillFallbackTimer <= 0f) EndSkill();
        }

        // ★레일 자가치유(진행 플래그) — busy가 풀렸는데(유예 만료 + Animator가 Action 아님) _step/_countering/_skilling이 남아 있으면
        //   액션 진입 실패(전이 경쟁·★"Action" 태그 누락)로 보고 진행 상태도 닫는다. busy(이동)뿐 아니라 입력 게이트
        //   (특히 _countering이 입력을 묵살하는 것)까지 자가 복구 — Codex M. 정상 동작 중엔 IsBusy=true라 발화 안 함.
        if (!IsBusy && (_step > 0 || _countering || _skilling))
        {
            // 독립 if 3개 — 이론적 다중 플래그 동시 고착도 전부 닫는다(Stab H-2). 각 End/Reset은 SetCombo(0) 멱등이라 중복 안전.
            if (_skilling) EndSkill();
            if (_countering) EndCounter();
            if (_step > 0) ResetCombo();
#if UNITY_EDITOR
            Debug.LogWarning("[KatanaWeapon] 액션 진입 실패 자가치유 — Animator가 \"Action\" 상태에 못 들었다(유예 내 진입 실패). " +
                             "해당 액션 상태의 \"Action\" 태그와 AnyState 진입 전환을 확인하라.", this);
#endif
        }

        // 각 좌클릭 '누름'. ★반격 창 안 + idle이면 카운터(Skill02) 우선, 아니면 콤보(idle 1단 / 진행 중 버퍼).
        //   M-1 무효 근거: 패링은 항상 회피(대시) 뒤 — 대시가 콤보를 Cancel(→_step=0)하므로 창이 열릴 땐 _step==0이 보장된다.
        //   창 안 첫 좌클릭은 이 분기로 곧장 카운터가 되어, '콤보 진행 중 창' 시나리오는 정상 흐름에서 발생하지 않는다.
        if (input.primaryDown)
        {
            if (_countering || _skilling) { /* 반격/스킬 모션 중 입력 무시 — 클립 끝까지 커밋 */ }
            else if (_counterTimer > 0f && _step == 0) BeginCounter();
            else if (_step == 0)
            {
                if (_startCdTimer <= 0f) BeginCombo();
            }
            else
            {
                _buffered = true;
                _bufferTimer = inputBufferTime;
            }
        }

        // ★우클릭 스킬(Skill01) — SkillSet 할당 + idle + 쿨다운 준비 시 발동. 콤보/카운터/스킬 진행 중엔 IsBusy로 막힌다.
        //   (대시 중 RMB는 PlayerBrain이 억제 — 대시 커밋 보호.)
        if (input.secondaryDown && skillSet != null && !IsBusy && _skillCdTimer <= 0f)
            BeginSkill();

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
        _aimDir = _liveAim;   // 1단 시작 — 이 순간 조준으로 방향 잠금(facing/런지/타격 통일)
        _step = 1;
        _windowOpen = false;
        _hitDone = false;
        _startCdTimer = startCooldown;
        _lastAdvanceTime = -1f;
        AnimatorDriver?.SetCombo(1);
        BeginAction();   // 레일: 진입 유예 켬 → Animator가 Combo1(Action) 들 때까지 busy 유지
    }

    void Advance()
    {
        _aimDir = _liveAim;   // 다음 단 시작 — 단 사이 재조준 반영(이후 다시 고정)
        _buffered = false;
        _windowOpen = false;
        _hitDone = false;
        _step++;
        _lastAdvanceTime = Time.time;
        AnimatorDriver?.SetCombo(_step);
        BeginAction();   // 레일: 다음 단 전이 갭 유예(이미 Action 상태라 안전망 성격)
    }

    /// <summary>★패링 반격 — 카운터 창 안에서 좌클릭 시 Skill02 발동. 콤보 _step과 독립(busy로 잠김),
    /// 타격은 OnHitFrame이 _countering일 때 DoCounterHit(보상치), 종료는 OnComboEnd가 처리.</summary>
    void BeginCounter()
    {
        _aimDir = _liveAim;       // 반격 방향을 현재 조준으로 잠금(타격/모션 통일)
        _countering = true;
        _hitDone = false;
        _counterTimer = 0f;       // 창 소비
        _counterFallbackTimer = counterMaxDuration;   // 안전 워치독 가동(OnComboEnd 누락 대비)
        _windowOpen = false;
        AnimatorDriver?.TriggerCounter();
        BeginAction();   // 레일: 진입 유예 켬 → Animator가 Counter(Action) 들 때까지 busy 유지
    }

    /// <summary>반격 종료 공통 경로 — 클립 OnComboEnd(정상)와 워치독(폴백) 둘 다 여기로 합류.
    /// SetCombo(0)이 _lockedFace도 해제(Stab H-2 — 반격 facing 잠금이 함께 풀린다).</summary>
    void EndCounter()
    {
        _countering = false;
        _hitDone = false;
        _counterFallbackTimer = 0f;
        AnimatorDriver?.SetCombo(0);
    }

    /// <summary>★우클릭 스킬 — Skill01 발동. 반격과 동형(busy로 잠김, 타격=OnHitFrame _skilling 분기, 종료=OnComboEnd).
    /// 쿨다운 소비. facing은 발동 순간 조준에 잠금.</summary>
    void BeginSkill()
    {
        _aimDir = _liveAim;
        _skilling = true;
        _hitDone = false;
        _skillCdTimer = skillSet.timing.cooldown;
        _skillFallbackTimer = skillSet.timing.maxDuration > 0f ? skillSet.timing.maxDuration : 3.5f;
        AnimatorDriver?.TriggerSkill();
        BeginAction();   // 레일: 진입 유예 켬 → Animator가 Skill01(Action) 들 때까지 busy 유지
        // ★VFX/사운드는 발동 순간이 아니라 타격 순간(DoSkillHit @ OnAttackHit = 칼 벨 때)에 낸다.
    }

    /// <summary>스킬 VFX 스폰 — skillVfxPrefab을 조준 방향에 오리엔트해 띄우고 자동 소멸.
    /// PlayOnAwake=false 프리팹도 강제 재생(슬래시 VFX와 동일 함정 가드). 비어 있으면 무동작.</summary>
    void SpawnSkillVfx()
    {
        if (skillSet == null || skillSet.vfx == null || skillSet.vfx.prefab == null) return;
        var v = skillSet.vfx;
        Vector3 pos; Quaternion rot;
        if (v.basis == SkillSet.VfxBasis.Weapon && weaponAnchor != null)
        {
            // ★무기(칼) 앵커 기준 — 슬래시(휘두름 따라). 콤보 슬래시(PlayerAttackVfx)와 동일 수학.
            pos = weaponAnchor.TransformPoint(v.posOffset);
            rot = weaponAnchor.rotation * Quaternion.Euler(v.eulerOffset);
        }
        else
        {
            // ★플레이어 위치 + 조준 방향 기준 — 불렛/전방 발사. posOffset은 조준-로컬(z=앞·x=우·y=위).
            if (Owner == null) return;
            Vector3 fwd = _aimDir.sqrMagnitude > 0.0001f ? _aimDir.normalized : Owner.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            Quaternion aimRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            pos = Owner.position + aimRot * v.posOffset;
            rot = aimRot * Quaternion.Euler(v.eulerOffset);
        }
        var go = Instantiate(v.prefab, pos, rot);
        float s = v.scale > 0f ? v.scale : 1f;
        if (!Mathf.Approximately(s, 1f)) go.transform.localScale *= s;
        float spd = v.playbackSpeed > 0f ? v.playbackSpeed : 1f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            if (!Mathf.Approximately(spd, 1f)) { var main = ps.main; main.simulationSpeed *= spd; }
            ps.Play(false);
        }
        Destroy(go, v.lifetime > 0f ? v.lifetime : 1.5f);
    }

    AudioSource _skillSfxSource;   // 2D one-shot 소스(첫 사용 시 생성 — PlayerAttackSfx와 동일 정책)
    /// <summary>스킬 사운드 — 2D(거리감쇠 없음) PlayOneShot. 비어 있으면 무동작.</summary>
    void PlaySkillSfx()
    {
        if (skillSet == null || skillSet.sfx == null || skillSet.sfx.clip == null) return;
        if (_skillSfxSource == null)
        {
            _skillSfxSource = gameObject.AddComponent<AudioSource>();
            _skillSfxSource.playOnAwake = false;
            _skillSfxSource.spatialBlend = 0f;   // 2D — 손맛 사운드는 카메라 거리감쇠 X
        }
        _skillSfxSource.PlayOneShot(skillSet.sfx.clip, skillSet.sfx.volume);
    }

    /// <summary>스킬 종료 공통 경로 — 클립 OnComboEnd(정상)와 워치독/자가치유(폴백) 둘 다 여기로.</summary>
    void EndSkill()
    {
        _skilling = false;
        _hitDone = false;
        _skillFallbackTimer = 0f;
        AnimatorDriver?.SetCombo(0);
    }

    // ── AnimationEvent 릴레이(PlayerAnimatorDriver 경유) — 타이밍은 클립이 소유 ──
    protected override void OnHitFrame(int hitFrameIndex)   // 타격 정점
    {
        if (_skilling) { if (!_hitDone) { _hitDone = true; DoSkillHit(); } }       // 스킬 타격
        else if (_countering) { if (!_hitDone) { _hitDone = true; DoCounterHit(); } }   // 반격 타격(보상치)
        else if (_step >= 1 && !_hitDone) { _hitDone = true; DoSwingHit(); }
    }

    void OnComboWindow()   // 캔슬 윈도우 시작 — 다음 단 입력 받기 시작(각 좌클릭이 각 단)
    {
        if (_step >= 1) _windowOpen = true;
    }

    void OnComboEnd()      // 현재 단/반격 클립 끝 — 다음 단으로 안 넘어갔으면 종료(idle 복귀)
    {
        if (_skilling) { EndSkill(); return; }       // 스킬(Skill01) 정상 종료 — 클립 끝 OnComboEnd
        if (_countering) { EndCounter(); return; }   // 반격(Skill02) 정상 종료 — 클립 끝 OnComboEnd
        // Stab M-1 방어: Advance 직후엔 이전 클립이 CUT로 중단되며 그 OnComboEnd가 1프레임 늦게 샐 수 있다.
        // 그 지연 발화는 막 시작한 다음 단을 잘못 종료시키므로, Advance 후 짧은 관용창 내 OnComboEnd는 무시한다.
        // (Combo 클립 길이가 0.1s를 크게 웃돌아 현재 단의 정상 종료는 막지 않는다.)
        if (_lastAdvanceTime >= 0f && Time.time - _lastAdvanceTime < 0.1f) return;
        ResetCombo();
    }

    /// <summary>콤보 진행 상태를 idle로 닫는다 — 정상 종료(OnComboEnd)와 자가치유(진입 실패) 공통 경로.</summary>
    void ResetCombo()
    {
        _step = 0;
        _windowOpen = false;
        _buffered = false;
        _hitDone = false;
        _lastAdvanceTime = -1f;
        AnimatorDriver?.SetCombo(0);
    }

    /// <summary>현재 콤보 단의 공격 스텝(1-based). 범위 밖이면 마지막 단으로 클램프, 비면 안전 기본값.</summary>
    ComboAttackSet.ComboAttackStep GetHit(int step)
    {
        if (comboSet == null || comboSet.StepCount == 0)
        {
            Debug.LogWarning("[KatanaWeapon] comboSet 미할당/비어 있음 — 하드코딩 폴백 사용. Inspector에서 ComboAttackSet 확인 요망.", this);
            return new ComboAttackSet.ComboAttackStep { range = 1.8f, arcHalfAngle = 50f, forwardOffset = 0f, damage = 3, knockback = 4f };
        }
        comboSet.TryGetStep(step, out var s);
        return s;
    }

    // ── 디버그 시각화용 읽기전용 접근자(HitboxDebugManager 전용) — 전투 로직 무관 ──
    public Transform DebugOwner => Owner;
    public int DebugStep => _step;
    /// <summary>공격 중이면 잠근 방향(_aimDir), 평시엔 라이브 조준(_liveAim) — 프리뷰용.</summary>
    public Vector3 DebugAimDir => _step >= 1 ? _aimDir : _liveAim;
    public int DebugHitCount => comboSet != null ? comboSet.StepCount : 0;
    /// <summary>단별 히트박스 파라미터(1-based). DoSwingHit과 동일 폴백 가드.</summary>
    public bool DebugGetHit(int step, out float range, out float arcHalf, out float forwardOffset)
    {
        range = 1.8f; arcHalf = 50f; forwardOffset = 0f;
        if (comboSet == null || comboSet.StepCount == 0) return false;
        comboSet.TryGetStep(step, out var h);
        range = EffectiveRange(h);   // 슬래시 스케일 연동 포함 — 디버그 뷰가 실효 판정을 그린다
        arcHalf = h.arcHalfAngle > 0f ? h.arcHalfAngle : 50f;
        forwardOffset = Mathf.Max(0f, h.forwardOffset);
        return true;
    }

    void DoSwingHit()
    {
        ComboAttackSet.ComboAttackStep h = GetHit(_step);
        // 0/미설정 직렬화값 폴백 가드(필드 추가 시 default 0 함정).
        DoHit(EffectiveRange(h),                              // range × (rangeFromSlashScale면 그 단 슬래시 스케일)
              h.arcHalfAngle > 0f ? h.arcHalfAngle : 50f,
              Mathf.Max(0f, h.forwardOffset),
              h.damage > 0 ? h.damage : 1,
              h.knockback);
    }

    /// <summary>반격(Skill02) 타격 — 콤보보다 강한 보상치. DoHit 공통 경로 재사용.</summary>
    void DoCounterHit() => DoHit(counterRange,
                                 counterArcHalf > 0f ? counterArcHalf : 70f,   // 0이면 부채꼴이 모든 적을 배제(M-2)
                                 counterForwardOffset,
                                 counterDamage > 0 ? counterDamage : 1,
                                 counterKnockback);                            // 넉백 0은 유효(무넉백) — 가드 강제 안 함

    /// <summary>스킬(Skill01) 타격 — SkillSet 판정 + ★타격 순간(칼 벨 때) VFX·사운드.</summary>
    void DoSkillHit()
    {
        if (skillSet == null) return;
        var h = skillSet.hit;
        DoHit(h.range, h.arcHalfAngle > 0f ? h.arcHalfAngle : 80f, h.forwardOffset,
              h.damage > 0 ? h.damage : 1, h.knockback);
        SpawnSkillVfx();   // ★타격 순간 VFX
        PlaySkillSfx();    // ★타격 순간 사운드
    }

    /// <summary>부채꼴+사거리+LOS 판정으로 IDamageable에 타격. 콤보/반격 공통(파라미터만 다름).</summary>
    void DoHit(float range, float arcHalf, float forwardOffset, int dmgAmt, float kb)
    {
        if (Owner == null) return;

        // ★히트존 원점을 조준 방향으로 전진 — 보이는 슬래시 위치에 정합(Codex 권고; 발밑 중심 아님).
        Vector3 origin = Owner.position + _aimDir * Mathf.Max(0f, forwardOffset);
        Vector3 eye = origin + Vector3.up * eyeHeight;
        float gather = range + 0.5f;
        const float pointBlank = 0.9f;

        _hitThisSwing.Clear();
        int n = Physics.OverlapSphereNonAlloc(origin, gather, _overlap, enemyMask, QueryTriggerInteraction.Collide);
        if (n == _overlap.Length)
            Debug.LogWarning("[KatanaWeapon] OverlapSphere 버퍼(128)가 가득 — 일부 타격 누락 가능(버퍼 증대 검토).");
        for (int i = 0; i < n; i++)
        {
            var dmg = _overlap[i].GetComponentInParent<IDamageable>();
            if (dmg == null || _hitThisSwing.Contains(dmg)) continue;

            Vector3 to = _overlap[i].transform.position - origin; to.y = 0f;
            float dist = to.magnitude;
            if (dist > range) continue;
            if (dist > pointBlank)
            {
                if (Vector3.Angle(_aimDir, to) > arcHalf) continue;
                Vector3 los = (_overlap[i].transform.position + Vector3.up * eyeHeight) - eye;
                float ll = los.magnitude;
                if (ll > 0.001f && Physics.Raycast(eye, los / ll, ll, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
            }
            _hitThisSwing.Add(dmg);
            dmg.TakeHit(dmgAmt, origin, kb);
        }
    }
}
