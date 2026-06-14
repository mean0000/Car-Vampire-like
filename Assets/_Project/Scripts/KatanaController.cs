using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카타나 전투 — 증명 슬라이스 Phase1. 거합/참격 두 모드를 *손맛이 정반대로* 느껴지게 구동한다.
/// MeleeAttacker와 같은 패턴(PlayerCombat이 소유·구동하는 순수 C# 헬퍼 — 런타임 수명주기 함정 회피).
///
/// 스펙: docs/03_reference/2026-06-13-weapon-progression-proposal.md §17.1.A(거합)·§17.1.B(참격).
///
/// ★거합(고수) — "정지 충전 → 조준선 발도 돌진 → 킬 환류로 게이지 리필":
///   - LMB 평타 = 느린 묵직 단발(공속 55%, 데미지 풀 유지). 난사 불가 = "촥…쉬었다…촥".
///   - RMB 홀드 = 게이지 충전(정지 시 2배 빠름, 걸으며 느림, 달리면 더 느림).
///   - RMB 릴리스 = 발도 돌진(조준선 직선, 차지 비례 2.5~6m, 캡슐 캐스트 폭 0.8m, 적 관통·벽 정지).
///   - 발도 킬 1체당 게이지 +35% + 다음 발도 쿨 0(연속 처형 사슬).
///   - 가독: 발밑 시안 링(충전량) + 발도 시안 잔상 선(0.3s).
///
/// ★참격(뉴비) — "베기 적중 → 콤보↑ → 공속↑ → 5단 임계 → 참격파, 피격=일부 손실":
///   - LMB 평타 = 풀 데미지(50°×1.8m). 적중 3회당 콤보 1단, 단당 쿨 −15%(5단 −60%).
///   - 5단 도달 시 RMB = 참격파(확장 부채꼴 90°×5m, 0.4s간 1.8→5m 전진, 평타×3+넉백6).
///   - 킬 시 +1단, 피격 시 −1~2단(통째 리셋 ❌). 콤보 윈도우 2.0s.
///   - 가독: 발밑 시안 호 게이지(1단 짧은 호→5단 완전 링) + 콤보 높을수록 베기 트레일 길고 밝게.
///
/// ★헌법(player self-cancel): 충전/콤보 중 대시·모드전환으로 *조기 종료 + 하드컷*(crossfade 뭉갬 ❌).
///   몬스터만 commit-lock. 발도 돌진은 짧은 commit이지만 플레이어 자기 의도라 대시로 캔슬 허용.
/// </summary>
public class KatanaController
{
    public enum Mode { Iai = 0, Slash = 1 }   // 거합 / 참격

    readonly Transform _owner;
    readonly float _muzzleHeight;
    readonly LayerMask _zombieMask;
    readonly LayerMask _obstacleMask;
    readonly PlayerController _player;        // 대시 self-cancel 감지 + 발도 돌진 지면 정렬
    readonly MoreMountains.Feedbacks.MMSpringScale _ownerSpring;

    WeaponLoadout.Weapon _profile;            // 평타 베이스값(데미지·리치·부채꼴)
    Mode _mode = Mode.Iai;
    public Mode CurrentMode => _mode;

    // ── 공통 베이스 데미지 ──
    int BaseDamage => _profile.damage + PlayerStats.DamageBonus;

    // ════════════════════════════════════════════════════════════
    //  거합 노브 (전부 인스펙터 노출은 PlayerCombat 측 — 여기선 시작값 상수)
    // ════════════════════════════════════════════════════════════
    [System.Serializable]
    public class IaiKnobs
    {
        [Tooltip("★거합 평타 스윙 간격(초) — 한 칼 한 칼의 *무게*. 큰 값 = 또박또박 묵직(촥…쉬었다…촥). "
               + "기관총처럼 줄줄 나가면 무게가 사라진다. 0.82 = 일반 검(0.35) 대비 묵직 단발. 메타 FireRateMult로 단축.")]
        [Min(0.05f)] public float lightBaseCadence = 0.82f;
        [Tooltip("(레거시 참고용 — 현재 cadence는 lightBaseCadence가 직접 결정) 거합 평타 공속 배수.")]
        [Range(0.3f, 1f)] public float lightSpeedMult = 0.55f;
        [Tooltip("거합 평타 부채꼴 반각(도).")]
        public float lightArcHalfAngle = 45f;
        [Tooltip("거합 평타 리치(m).")]
        public float lightRange = 1.6f;

        [Tooltip("거합 충전 0→100% 시간 — 정지 시(초). 이동 중엔 2배 느림.")]
        public float chargeTimeStill = 1.2f;
        [Tooltip("거합 충전 0→100% 시간 — 걸으며(초).")]
        public float chargeTimeMoving = 2.4f;
        [Tooltip("발도 성립 최소 차지 — 이 미만이면 오발 없이 취소.")]
        [Range(0f, 1f)] public float minCharge01 = 0.125f;   // 0.15s/1.2s ≈ 0.125

        [Tooltip("발도 돌진 거리 — 차지 0%(m).")]
        public float lungeDistMin = 2.5f;
        [Tooltip("발도 돌진 거리 — 차지 100%(m).")]
        public float lungeDistMax = 6f;
        [Tooltip("발도 캡슐 캐스트 폭(반경 m). 0.8m 폭 = 반경 0.4m.")]
        public float lungeRadius = 0.4f;
        [Tooltip("발도 돌진 속도(m/s) — 클수록 즉발에 가깝게 순간 이동.")]
        public float lungeSpeed = 38f;

        [Tooltip("발도 데미지 배수 — 차지 0%(평타×N).")]
        public float lungeDmgMultMin = 1.5f;
        [Tooltip("발도 데미지 배수 — 차지 100%(평타×N).")]
        public float lungeDmgMultMax = 4f;

        [Tooltip("발도 킬 1체당 게이지 환류(+, 0~1). 2킬≈만충.")]
        [Range(0f, 1f)] public float killRefill = 0.35f;
        [Tooltip("발도 후 쿨(초) — 킬 시 0으로 단축(환류 사슬).")]
        public float lungeCooldown = 0.4f;

        [Tooltip("발도 릴리스 스냅 어시스트 각(도) — 조준선 ±N° 내 가장 가까운 적으로 방향 보정. 풀 락온 아님.")]
        public float snapAssistDeg = 10f;
    }

    // ════════════════════════════════════════════════════════════
    //  참격 노브
    // ════════════════════════════════════════════════════════════
    [System.Serializable]
    public class SlashKnobs
    {
        [Tooltip("참격 평타 부채꼴 반각(도) — 거합보다 약간 넓다.")]
        public float arcHalfAngle = 50f;
        [Tooltip("참격 평타 리치(m).")]
        public float range = 1.8f;

        [Tooltip("★참격 base(0단) 스윙 간격(초) — 콤보 0일 때 한 베기의 무게. 콤보 쌓이면 cooldownCutPerTier로 *가속*(보상). "
               + "큰 값 = base가 또박또박 묵직(기관총 ❌). 0.55 = 신중한 시작. 메타 FireRateMult로 단축.")]
        [Min(0.05f)] public float baseCadence = 0.55f;

        [Tooltip("콤보 상한 단수.")]
        [Range(1, 8)] public int maxTier = 5;
        [Tooltip("콤보 1단 상승에 필요한 적중 횟수.")]
        public int hitsPerTier = 3;
        [Tooltip("단당 스윙 쿨 감소율(0.15=−15%/단). 상한 = maxTier×이 값.")]
        [Range(0f, 0.3f)] public float cooldownCutPerTier = 0.15f;

        [Tooltip("콤보 윈도우(초) — 마지막 베기 후 이 시간 내 적중 시 유지/상승.")]
        public float comboWindow = 2.0f;
        [Tooltip("윈도우 만료 후 단 감쇠 속도(단/초) — 즉시 0 아님.")]
        public float decayPerSec = 1f;

        [Tooltip("피격 시 콤보 손실 단수(최소).")]
        [Min(0)] public int hitLossMin = 1;
        [Tooltip("피격 시 콤보 손실 단수(최대).")]
        [Min(0)] public int hitLossMax = 2;

        [Tooltip("참격파 사거리(m) 끝값. 0.4s간 평타 리치→이 값으로 확장.")]
        public float waveRange = 5f;
        [Tooltip("참격파 부채꼴 반각(도).")]
        public float waveHalfAngle = 90f;
        [Tooltip("참격파 확장 시간(초).")]
        public float waveDuration = 0.4f;
        [Tooltip("참격파 데미지 배수(평타×N).")]
        public float waveDmgMult = 3f;
        [Tooltip("참격파 넉백(m/s).")]
        public float waveKnockback = 6f;
    }

    IaiKnobs _iai;
    SlashKnobs _slash;

    // ── 런타임 상태 (공통) ──
    float _cooldownTimer;            // 평타 쿨
    Vector3 _aimDir = Vector3.forward;
    Vector3 _prevPos;                // 정지/이동 판정용 위치 델타
    bool _prevDashing;               // 대시 엣지(self-cancel 감지)

    readonly HashSet<ZombieController> _hitThisSwing = new HashSet<ZombieController>();
    readonly HashSet<ZombieController> _lungeHits = new HashSet<ZombieController>();

    // ── 거합 상태 ──
    float _charge01;                 // 거합 게이지(0~1)
    bool _charging;                  // RMB 홀드 중 충전
    float _lungeCooldownTimer;       // 발도 후 쿨
    float _decayDelayTimer;          // 충전 후 미발사 → 감쇠 지연
    bool _lunging;                   // 발도 돌진 진행 중
    Vector3 _lungeDir;
    float _lungeRemaining;           // 남은 돌진 거리(m)
    Vector3 _lungeStart;             // 돌진 시작 위치(실제 누적 이동거리 산출용)
    int _lungeDamage;
    bool _lungeKilled;               // 이번 발도에서 킬 발생(환류 = 쿨 0 유지 판정)

    // ── 진행형 슬래시 VFX 핸들(발도/참격파) ── 스폰 시 잡고, 매 프레임 실제 거리/반경으로 Drive*().
    //   Gen 가드: 핸들을 잡은 후 풀이 재활용해 다른 공격에 쓰이면 Gen이 바뀌어 stale 드라이브를 차단.
    SlashVfxFX _lungeFx; int _lungeFxGen;
    SlashVfxFX _waveFx;  int _waveFxGen;

    // ── 참격 상태 ──
    int _comboTier;                  // 0~maxTier
    int _hitAccum;                   // 현재 단 내 누적 적중(hitsPerTier마다 +1단)
    float _comboTimer;               // 콤보 윈도우 잔여(초)
    float _decayFractional;          // 감쇠 누적(부분 단 처리)
    bool _waveActive;               // 참격파 진행 중
    float _waveElapsed;
    Vector3 _waveDir;
    readonly HashSet<ZombieController> _waveHits = new HashSet<ZombieController>();

    // ── 가독(LineRenderer 자작 — laser/브라켓과 같은 1차 프리미티브, renderQueue 3100) ──
    //   발밑 게이지만 LineRenderer 유지. 슬래시/참격파 궤적·범위는 전부 SlashVfxFX(하이브리드)로 일원화 —
    //   ★M-1: 자작 _slashTrail/_waveArc는 ThreatArc 범위 레이어와 이중 렌더라 제거(시각=판정 단일 소스).
    LineRenderer _footRing;          // 발밑 링/호 게이지(거합 충전 = 풀 링 채움, 참격 콤보 = 호 길이)
    const int RingSegments = 48;
    static readonly Color CyanDim = new Color(0.2f, 0.85f, 0.95f, 0.5f);
    static readonly Color CyanBright = new Color(0.4f, 1f, 1f, 1f);
    const float RingRadius = 1.1f;   // 발밑 링 반경(m) — 부감에서 캐릭터 둘레로 읽힘

    // ── 슬래시 궤적 VFX 풀((C)하이브리드 — 화려한 기성 슬래시 + ThreatArc 범위 SDF) ──
    //   자작 LineRenderer 트레일(_slashTrail/_waveArc)을 대체한다. 풀은 자가 부트스트랩, 첫 사용에 1회 참조.
    //   null이면(빌드 스트립·머티리얼 부재 등) 무음 폴백 — 게임 로직 무영향.
    //   ★C-1: 영구 resolved 캐시 금지 — 씬 리로드 등으로 풀이 파괴되면 _slashVfx가 *fake-null*(파괴된 UnityEngine.Object,
    //   == null은 true지만 ?. 무음 폴백으로 영구 침묵)이 되어 슬래시가 안 뜬다. SmashImpactPool 규약대로 매번 재확인.
    SlashVfxPool _slashVfx;
    SlashVfxPool SlashVfx
    {
        get
        {
            if (_slashVfx == null) _slashVfx = SlashVfxPool.Instance;   // fake-null이면 Instance가 재발견/재생성
            return _slashVfx;
        }
    }

    public KatanaController(Transform owner, WeaponLoadout.Weapon profile, float muzzleHeight,
                            LayerMask zombieMask, LayerMask obstacleMask,
                            IaiKnobs iai, SlashKnobs slash)
    {
        _owner = owner;
        _profile = profile;
        _muzzleHeight = muzzleHeight;
        _zombieMask = zombieMask;
        _obstacleMask = obstacleMask;
        _player = owner.GetComponent<PlayerController>();
        _ownerSpring = owner.GetComponent<MoreMountains.Feedbacks.MMSpringScale>();
        _iai = iai ?? new IaiKnobs();
        _slash = slash ?? new SlashKnobs();

        _prevPos = owner.position;

        _footRing = CreateLine("KatanaFootRing", RingSegments + 1, 0.06f);

        // 피격 시 참격 콤보 손실 — 정적 이벤트 구독(거합 모드에선 무시).
        PlayerController.OnPlayerDamaged += OnPlayerHit;
    }

    public void SetMode(Mode m)
    {
        if (_mode == m) return;
        // 하드컷: 모드 전환 시 진행 중이던 충전/콤보/발도/참격파 즉시 종료(crossfade 뭉갬 ❌).
        HardCancelAll();
        _mode = m;
        _comboTier = 0; _hitAccum = 0; _comboTimer = 0f;
        _charge01 = 0f;
    }

    public string ModeName => _mode == Mode.Iai ? "거합" : "참격";

    // ── 검증 전용 읽기 접근자(에디터 자동화 — 리플렉션 회피용). 게임 로직 의존 금지. ──
    public int DebugComboTier => _comboTier;
    public float DebugCharge01 => _charge01;
    public bool DebugLunging => _lunging;
    public bool DebugWaveActive => _waveActive;
    public bool DebugCharging => _charging;
    /// <summary>검증 전용: 대시 시작 self-cancel 경로를 강제 발화(실제 IsDashing 엣지 대체).</summary>
    public void DebugForceDashCancel() => OnDashStarted();

    /// <summary>PlayerCombat.Update에서 매 프레임 호출.</summary>
    public void Tick(bool attackHeld, bool aimHeld, bool aimPressed, bool aimReleased, Vector3 aimDir, bool locked)
    {
        _aimDir = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : _aimDir;

        float dt = Time.deltaTime;
        if (_cooldownTimer > 0f) _cooldownTimer -= dt;
        if (_lungeCooldownTimer > 0f) _lungeCooldownTimer -= dt;

        // ★self-cancel: 대시 시작 엣지 = 진행 중 충전/콤보 흐름/발도/참격파를 하드컷.
        bool dashing = _player != null && _player.IsDashing;
        if (dashing && !_prevDashing)
            OnDashStarted();
        _prevDashing = dashing;

        if (locked)
        {
            HardCancelAll();
            UpdateVisuals();
            _prevPos = _owner.position;
            return;
        }

        if (_mode == Mode.Iai) TickIai(attackHeld, aimHeld, aimReleased, dt);
        else TickSlash(attackHeld, aimPressed, dt);

        UpdateVisuals();
        _prevPos = _owner.position;
    }

    // ════════════════════════════════════════════════════════════
    //  거합
    // ════════════════════════════════════════════════════════════
    void TickIai(bool attackHeld, bool aimHeld, bool aimReleased, float dt)
    {
        // 발도 돌진 진행 중엔 입력 무시(짧은 자기 commit — 단, 대시 self-cancel은 OnDashStarted가 가로챔).
        if (_lunging) { StepLunge(dt); return; }

        // 평타: 느린 묵직 단발. 충전 중이어도 좌클릭 평타는 가능(난사는 공속으로 자연 차단).
        if (attackHeld && _cooldownTimer <= 0f && _lungeCooldownTimer <= 0f)
            IaiLightSwing();

        // 충전: RMB 홀드. 정지 시 2배 빠름, 걸으며 느림.
        if (aimHeld)
        {
            _charging = true;
            float speed = MovementSpeed();
            // 정지(거의 0) → chargeTimeStill, 이동 → chargeTimeMoving. 달리기(빠름)면 더 느리게 살짝 가중.
            bool still = speed < 0.4f;
            float chargeTime = still ? _iai.chargeTimeStill : _iai.chargeTimeMoving;
            if (speed > 4.5f) chargeTime *= 1.3f;   // 달리기 페널티(살짝)
            _charge01 = Mathf.Min(1f, _charge01 + dt / Mathf.Max(0.01f, chargeTime));
            _decayDelayTimer = 0f;   // 충전 중엔 감쇠 안 함
        }
        else
        {
            _charging = false;
            // 미충전 보관 감쇠: 3s 유예 후 초당 −20%.
            if (_charge01 > 0f)
            {
                _decayDelayTimer += dt;
                if (_decayDelayTimer > 3f)
                    _charge01 = Mathf.Max(0f, _charge01 - 0.2f * dt);
            }
        }

        // 발도 릴리스: RMB 떼는 순간. 최소 차지 미달이면 오발 없이 취소.
        if (aimReleased)
        {
            if (_charge01 >= _iai.minCharge01)
                StartLunge();
            // 미달이면 차지 유지(취소 — "오발 없이"). 게이지 그대로 두고 감쇠에 맡긴다.
            _charging = false;
        }
    }

    void IaiLightSwing()
    {
        // ★거합 평타 cadence = 노브가 직접 결정(또박또박 무게). 메타 FireRateMult만 단축에 반영.
        // 구 "_profile.fireCooldown ÷ lightSpeedMult" 파생식 폐기 — base 한 방의 무게를 노브로 명확히 노출.
        _cooldownTimer = _iai.lightBaseCadence / Mathf.Max(0.01f, PlayerStats.FireRateMult);

        SwingFan(_iai.lightArcHalfAngle, _iai.lightRange, BaseDamage, _profile.knockback,
                 _profile.stagger, _profile.hitstop, applyWeakpointOpen: false);
        // ★(C)하이브리드: 화려한 시안 부채꼴 슬래시 + ThreatArc 부채꼴 범위. 거합 평타는 콤보 없음(묵직 단발 tier01=0).
        SlashVfx?.Acquire()?.PlayFan(_owner.position, _aimDir, _iai.lightRange, _iai.lightArcHalfAngle, 0f);
        _ownerSpring?.Bump(new Vector3(-0.1f, 0.06f, -0.1f));
    }

    void StartLunge()
    {
        // 스냅 어시스트: 조준선 ±N° 내 가장 가까운 적으로 방향 살짝 보정(풀 락온 아님).
        Vector3 dir = _aimDir;
        ZombieController snap = FindSnapTarget(dir, _iai.snapAssistDeg);
        if (snap != null)
        {
            Vector3 to = snap.transform.position - _owner.position; to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) dir = to.normalized;
        }

        // 경계 가드(M-3): min>max 역전 노브에도 "차지 높을수록 멀리"가 유지되게 max를 min 이상으로 클램프.
        float distMax = Mathf.Max(_iai.lungeDistMin, _iai.lungeDistMax);
        float charge = Mathf.Clamp01(_charge01);   // VFX 길이/밝기용 — 아래서 _charge01이 0으로 소비되기 전 캡처.
        float dist = Mathf.Lerp(_iai.lungeDistMin, distMax, _charge01);
        // 카메라 밖 방지: 조준점까지 거리로 클램프(조준점 너머로 안 나감)는 PlayerCombat이 cursorDist를
        // 안 넘겨주므로 여기선 단순 거리만(증명 슬라이스 — 클램프는 노브 세션 추가). 벽 클램프는 StepLunge.
        _lungeDir = dir;
        _lungeRemaining = dist;
        _lungeStart = _owner.position;   // 실제 누적 이동거리 산출 기준
        _lunging = true;
        _lungeKilled = false;
        _lungeHits.Clear();

        // ★위치 단일 소유 + i-frame: 발도 이동은 PlayerController가 가드 경로로 수행한다.
        // BeginLungeMotion = 일반 이동 잠금 + 돌진 구간 무적(충전 중은 무방비 — 여기서만 켬).
        _player?.BeginLungeMotion();

        // 발도 데미지 = 평타 × (차지 비례 1.5~4). 경계 가드(M-3): max를 min 이상으로 클램프(역전 방지).
        float dmgMultMax = Mathf.Max(_iai.lungeDmgMultMin, _iai.lungeDmgMultMax);
        float mult = Mathf.Lerp(_iai.lungeDmgMultMin, dmgMultMax, _charge01);
        _lungeDamage = Mathf.Max(1, Mathf.RoundToInt(BaseDamage * mult));

        _charge01 = 0f;   // 게이지 소비
        _charging = false;

        // ★(C)하이브리드 진행형: 화려한 시안 직선 찌르기 슬래시 + ThreatArc 레인 범위.
        //   목표 거리로 미리 찍지 않는다 — 실제 누적 이동(StepLunge에서 DrivePierce)으로 늘인다(벽=거기서 멈춤).
        _lungeFx = SlashVfx?.Acquire();
        if (_lungeFx != null)
        {
            _lungeFx.PlayPierce(_owner.position, _lungeDir, _iai.lungeRadius, charge);
            _lungeFxGen = _lungeFx.Gen;
        }
        _ownerSpring?.Bump(new Vector3(-0.2f, 0.12f, -0.2f));
        NoiseManager.Instance?.EmitImpulse(_profile.gunshotNoise);
    }

    void StepLunge(float dt)
    {
        Vector3 originBefore = _owner.position + Vector3.up * 0.5f;
        float desired = Mathf.Min(_lungeRemaining, _iai.lungeSpeed * dt);

        // ★위치 단일 소유: 실제 이동을 PlayerController에 위임(WallGuardedStep+지면 샘플링+초기겹침 탈출).
        // 반환 = 가드 후 실제 평면 이동 거리(벽에 막히면 desired보다 짧다 → 벽 정지 판정).
        float moved = _player != null
            ? _player.MoveLungeStep(_lungeDir * desired)
            : MoveLungeFallback(_lungeDir * desired);   // _player 부재 시 폴백(평면 이동)

        // 캡슐 캐스트 판정: 이번 스텝의 *실제 이동 구간*에서 좀비 전원 타격(관통, 중복 제외).
        // 벽 앞까지만 이동했으면 벽 너머 적은 자연히 사거리 밖(occlusion은 가드가 이미 처리).
        if (moved > 0.001f)
        {
            var hits = Physics.SphereCastAll(originBefore, _iai.lungeRadius, _lungeDir, moved,
                                             _zombieMask, QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                var z = h.collider.GetComponentInParent<ZombieController>();
                if (z == null || _lungeHits.Contains(z)) continue;
                _lungeHits.Add(z);
                bool killed = z.TakeMeleeHit(_lungeDamage, _owner.position, _profile.knockback * 1.5f,
                                             _profile.stagger, _profile.deathStyle, _profile.hitstop * 1.5f);
                if (killed)
                {
                    // ★킬 환류: 게이지 +killRefill, 다음 발도 쿨 0(연속 처형 사슬).
                    _charge01 = Mathf.Min(1f, _charge01 + _iai.killRefill);
                    _lungeKilled = true;
                }
            }
        }

        _lungeRemaining -= moved;

        // ★진행형 VFX 동기: 실제 누적 이동거리(시작점→현재, 돌진축 투영)로 절단선·레인을 늘인다.
        //   벽에 막혀 moved가 0이면 traveled도 안 늘어 VFX도 거기서 멈춤(허공 찌르기 제거).
        if (_lungeFx != null && _lungeFx.Gen == _lungeFxGen)
        {
            Vector3 fromStart = _owner.position - _lungeStart; fromStart.y = 0f;
            float traveled = Vector3.Dot(fromStart, _lungeDir);   // 돌진축 투영(역방향 보정 충돌 방지)
            _lungeFx.DrivePierce(traveled, _owner.position);
        }

        // 벽 정지: 가드가 요청보다 크게 깎았으면(이동 거의 0) 더 못 간다 → 즉시 종료.
        bool wallStopped = desired > 0.001f && moved < desired - 0.001f && moved < 0.01f;
        if (_lungeRemaining <= 0.001f || wallStopped)
        {
            _lunging = false;
            _lungeFx = null;   // 핸들 해제(이후 자체 페이드/Return은 FX가 관리)
            _player?.EndLungeMotion();
            // 환류: 발도 중 킬이 났으면 쿨 0(즉시 연속 발도). 안 났으면 일반 쿨 0.4s.
            _lungeCooldownTimer = _lungeKilled ? 0f : _iai.lungeCooldown;
        }
    }

    /// <summary>_player 부재(테스트 등) 폴백 — 단순 평면 이동. 정상 경로는 PlayerController.MoveLungeStep.</summary>
    float MoveLungeFallback(Vector3 worldStep)
    {
        worldStep.y = 0f;
        _owner.position += worldStep;
        return worldStep.magnitude;
    }

    // ════════════════════════════════════════════════════════════
    //  참격
    // ════════════════════════════════════════════════════════════
    void TickSlash(bool attackHeld, bool aimPressed, float dt)
    {
        // 참격파 진행 중엔 확장 판정만.
        if (_waveActive) { StepWave(dt); }

        // 콤보 윈도우 감쇠: 마지막 적중 후 시간 흐르면 단계 서서히 감소.
        if (_comboTimer > 0f)
        {
            _comboTimer -= dt;
        }
        else if (_comboTier > 0)
        {
            _decayFractional += _slash.decayPerSec * dt;
            while (_decayFractional >= 1f && _comboTier > 0)
            {
                _comboTier--;
                _decayFractional -= 1f;
            }
            if (_comboTier == 0) { _hitAccum = 0; _decayFractional = 0f; }
        }

        // 평타: 풀 데미지, 콤보 단에 따라 쿨 단축.
        if (attackHeld && _cooldownTimer <= 0f && !_waveActive)
            SlashLightSwing();

        // 참격파: 5단(임계) 도달 + RMB 누름. 임계 미만이면 PlayerCombat이 일반 정조준으로 처리(여기선 무발동).
        if (aimPressed && _comboTier >= _slash.maxTier && !_waveActive)
            StartWave();
    }

    void SlashLightSwing()
    {
        // ★base cadence = 노브(또박또박 무게). 콤보 단수만큼 cut으로 *가속*(계속 맞히면 빨라지는 보상은 보존).
        // 구 "_profile.fireCooldown" 파생 폐기 — base 한 방의 무게를 baseCadence로 명확히 노출.
        float cut = 1f - Mathf.Min(_slash.maxTier, _comboTier) * _slash.cooldownCutPerTier;
        _cooldownTimer = (_slash.baseCadence / Mathf.Max(0.01f, PlayerStats.FireRateMult)) * Mathf.Max(0.1f, cut);

        int hits = SwingFan(_slash.arcHalfAngle, _slash.range, BaseDamage, _profile.knockback,
                            _profile.stagger, _profile.hitstop, applyWeakpointOpen: false,
                            onKill: () => AddComboTier(1));   // ★킬 +1단

        // 적중 누적 → hitsPerTier마다 +1단(상한). 콤보 윈도우 갱신.
        if (hits > 0)
        {
            _comboTimer = _slash.comboWindow;
            _hitAccum += hits;
            while (_hitAccum >= _slash.hitsPerTier && _comboTier < _slash.maxTier)
            {
                _hitAccum -= _slash.hitsPerTier;
                _comboTier++;
            }
            if (_comboTier >= _slash.maxTier) _hitAccum = 0;
        }

        // ★(C)하이브리드 + 콤보 스케일링: 1단 흐린 짧은 호 → 5단 밝은 큰 호(+마젠타 살짝, HDR). PlayFan이 tier01로 처리.
        //   화려한 시안 부채꼴 슬래시 + ThreatArc 부채꼴 범위(실제 50°×1.8m 판정 그대로).
        float t01 = _comboTier / (float)Mathf.Max(1, _slash.maxTier);
        SlashVfx?.Acquire()?.PlayFan(_owner.position, _aimDir, _slash.range, _slash.arcHalfAngle, t01);
        _ownerSpring?.Bump(new Vector3(-0.08f, 0.05f, -0.08f));
    }

    void AddComboTier(int n)
    {
        _comboTier = Mathf.Min(_slash.maxTier, _comboTier + n);
        _comboTimer = _slash.comboWindow;
        if (_comboTier >= _slash.maxTier) _hitAccum = 0;
    }

    void StartWave()
    {
        _waveActive = true;
        _waveElapsed = 0f;
        _waveDir = _aimDir;   // 릴리스 순간 조준 방향 고정(흔들림 무시).
        _waveHits.Clear();
        _comboTier = 0;       // 참격파 = 게이지 전소비(다시 쌓아야).
        _hitAccum = 0;
        _comboTimer = 0f;
        // ★(C)하이브리드 임계 연출 진행형: 시안 코어 + 마젠타 엣지(오버드라이브 캐넌) 확장 부채꼴 파동 + ThreatArc 확장 부채꼴.
        //   즉시 5m로 안 찍는다 — StepWave의 실제 radius(1.8→5m)를 매 프레임 DriveWave로 동기(확장이 판정과 일치).
        _waveFx = SlashVfx?.Acquire();
        if (_waveFx != null)
        {
            _waveFx.PlayWave(_owner.position, _waveDir, _slash.range, _slash.waveRange, _slash.waveHalfAngle);
            _waveFxGen = _waveFx.Gen;
        }
        NoiseManager.Instance?.EmitImpulse(_profile.gunshotNoise * 1.5f);
        _ownerSpring?.Bump(new Vector3(-0.25f, 0.15f, -0.25f));
    }

    void StepWave(float dt)
    {
        _waveElapsed += dt;
        float t = Mathf.Clamp01(_waveElapsed / Mathf.Max(0.01f, _slash.waveDuration));
        float radius = Mathf.Lerp(_slash.range, _slash.waveRange, t);   // 1.8→5m 전진 확장

        // ★진행형 VFX 동기: 판정 radius 그대로 VFX 확장(즉시 5m 점프 제거 — 휩쓰는 확장이 보임).
        if (_waveFx != null && _waveFx.Gen == _waveFxGen)
            _waveFx.DriveWave(radius, _slash.range, _slash.waveRange);

        // 확장 부채꼴 판정 — 이미 맞은 적 제외(파동이 휩쓸고 지나감).
        Vector3 origin = _owner.position;
        Vector3 eye = origin + Vector3.up * _muzzleHeight;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(BaseDamage * _slash.waveDmgMult));
        Collider[] cols = Physics.OverlapSphere(origin, radius + 0.5f, _zombieMask, QueryTriggerInteraction.Collide);
        foreach (var col in cols)
        {
            var z = col.GetComponentInParent<ZombieController>();
            if (z == null || _waveHits.Contains(z)) continue;
            Vector3 to = z.transform.position - origin; to.y = 0f;
            float d = to.magnitude;
            if (d > radius + 0.5f) continue;
            if (d > 0.9f && Vector3.Angle(_waveDir, to) > _slash.waveHalfAngle) continue;
            // 벽 너머 제외.
            Vector3 los = (z.transform.position + Vector3.up * _muzzleHeight) - eye;
            float ll = los.magnitude;
            if (ll > 0.001f && Physics.Raycast(eye, los / ll, ll, _obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            _waveHits.Add(z);
            z.TakeMeleeHit(dmg, origin, _slash.waveKnockback, _profile.stagger, _profile.deathStyle, _profile.hitstop);
        }

        if (t >= 1f) { _waveActive = false; _waveFx = null; }   // 핸들 해제(이후 자체 페이드는 FX가 관리)
    }

    // ════════════════════════════════════════════════════════════
    //  공통 — 부채꼴 스윙 판정(MeleeAttacker.Swing 패턴 재사용)
    // ════════════════════════════════════════════════════════════
    int SwingFan(float arcHalf, float range, int dmg, float knockback, float stagger, float hitstop,
                 bool applyWeakpointOpen, System.Action onKill = null)
    {
        Vector3 origin = _owner.position;
        Vector3 eye = origin + Vector3.up * _muzzleHeight;
        const float pointBlank = 0.9f;

        _hitThisSwing.Clear();
        int hitCount = 0;
        bool hitAny = false;
        Vector3 firstHitPos = origin + _aimDir * range;

        float gather = range + 0.5f;
        Collider[] cols = Physics.OverlapSphere(origin, gather, _zombieMask, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            var z = c.GetComponentInParent<ZombieController>();
            if (z == null || _hitThisSwing.Contains(z)) continue;

            Vector3 to = z.transform.position - origin; to.y = 0f;
            float dist = to.magnitude;
            if (dist > gather) continue;
            if (dist > pointBlank)
            {
                if (Vector3.Angle(_aimDir, to) > arcHalf) continue;
                Vector3 dir = (z.transform.position + Vector3.up * _muzzleHeight) - eye;
                float dl = dir.magnitude;
                if (dl > 0.001f && Physics.Raycast(eye, dir / dl, dl, _obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;
            }

            _hitThisSwing.Add(z);
            if (!hitAny) firstHitPos = z.transform.position;
            hitAny = true;
            hitCount++;
            bool killed = z.TakeMeleeHit(dmg, origin, knockback, stagger, _profile.deathStyle, hitstop);
            if (killed) onKill?.Invoke();
        }

        NoiseManager.Instance?.EmitImpulse(_profile.gunshotNoise);
        if (hitAny) MeleeSfx.PlayHit(_profile.deathStyle, firstHitPos);
        else MeleeSfx.PlayWhiff(eye);
        return hitCount;
    }

    ZombieController FindSnapTarget(Vector3 aimDir, float maxDeg)
    {
        ZombieController best = null;
        float bestDist = float.MaxValue;
        Collider[] cols = Physics.OverlapSphere(_owner.position, _iai.lungeDistMax + 1f, _zombieMask,
                                                QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            var z = c.GetComponentInParent<ZombieController>();
            if (z == null || z.IsDead) continue;
            Vector3 to = z.transform.position - _owner.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.1f) continue;
            if (Vector3.Angle(aimDir, to) > maxDeg) continue;
            if (dist < bestDist) { bestDist = dist; best = z; }
        }
        return best;
    }

    // ════════════════════════════════════════════════════════════
    //  self-cancel / 정리
    // ════════════════════════════════════════════════════════════
    void OnDashStarted()
    {
        // ★헌법: 대시는 충전/콤보 흐름을 하드컷한다(crossfade ❌).
        if (_mode == Mode.Iai)
        {
            // 충전 중이면 충전만 끊는다(게이지 보존 — 대시 후 이어 충전 가능). 발도 진행 중이면 발도 중단.
            _charging = false;
            if (_lunging)
            {
                _lunging = false;
                _lungeRemaining = 0f;
                _lungeFx = null;             // 진행형 핸들 해제(이후 자체 페이드/Return은 FX가 관리)
                _player?.EndLungeMotion();   // 이동 잠금/무적 해제(leak 방지)
            }
        }
        // 참격: 공격 캔슬 대시 = 게이지 유지(흐름 유지). 콤보는 안 깎되 윈도우만 갱신해 끊김 방지.
        else
        {
            if (_comboTier > 0) _comboTimer = _slash.comboWindow;
            // 참격파 진행 중 대시 캔슬 = 즉시 중단.
            if (_waveActive) { _waveActive = false; _waveFx = null; }
        }
    }

    void OnPlayerHit(float amount)
    {
        // 참격 모드만 — 피격 시 콤보 일부 손실(통째 리셋 ❌).
        if (_mode != Mode.Slash || _comboTier <= 0) return;
        // 경계 가드(M-3): min>max 역전·음수 노브에도 loss가 음수가 되지 않게(콤보 증가 footgun 차단).
        int lo = Mathf.Max(0, Mathf.Min(_slash.hitLossMin, _slash.hitLossMax));
        int hi = Mathf.Max(lo, _slash.hitLossMax);
        int loss = Mathf.Max(0, Random.Range(lo, hi + 1));
        _comboTier = Mathf.Max(0, _comboTier - loss);
        _hitAccum = 0;
        if (_comboTier == 0) { _comboTimer = 0f; _decayFractional = 0f; }
    }

    void HardCancelAll()
    {
        _charging = false;
        if (_lunging) _player?.EndLungeMotion();   // 이동 잠금/무적 해제(모드전환·locked·Cleanup 경유 leak 방지)
        _lunging = false;
        _lungeRemaining = 0f;
        _waveActive = false;
        _lungeFx = null; _waveFx = null;           // 진행형 핸들 해제(stale 드라이브 차단 — FX는 자체 페이드)
    }

    // ════════════════════════════════════════════════════════════
    //  가독 비주얼
    // ════════════════════════════════════════════════════════════
    void UpdateVisuals()
    {
        // 발밑 게이지: 거합 = 충전량(풀 링 비율), 참격 = 콤보 단(호 길이).
        float fill01;
        Color ringColor;
        if (_mode == Mode.Iai)
        {
            fill01 = _charge01;
            // 만충 = 형광 펄스.
            float pulse = _charge01 >= 0.999f ? (0.85f + 0.15f * Mathf.Sin(Time.time * 14f)) : 1f;
            ringColor = Color.Lerp(CyanDim, CyanBright, _charge01) * pulse;
        }
        else
        {
            float tierFill = (_comboTier + Mathf.Clamp01(_hitAccum / (float)Mathf.Max(1, _slash.hitsPerTier)))
                             / Mathf.Max(1, _slash.maxTier);
            fill01 = Mathf.Clamp01(tierFill);
            float t01 = _comboTier / (float)_slash.maxTier;
            float pulse = _comboTier >= _slash.maxTier ? (0.8f + 0.2f * Mathf.Sin(Time.time * 16f)) : 1f;
            ringColor = Color.Lerp(CyanDim, CyanBright, t01) * pulse;
        }
        DrawFootRing(fill01, ringColor);
        // (슬래시 트레일·참격파 외곽선은 SlashVfxFX 하이브리드가 전담 — 여기선 발밑 게이지만.)
    }

    void DrawFootRing(float fill01, Color color)
    {
        if (_footRing == null) return;
        if (fill01 <= 0.001f) { _footRing.enabled = false; return; }
        _footRing.enabled = true;
        _footRing.startColor = color;
        _footRing.endColor = color;

        Vector3 center = _owner.position + Vector3.up * 0.06f;
        // 호: 조준 방향을 중심으로 양쪽으로 fill만큼 벌어진 원호(거합도 동일 — 채움=호 길이).
        // 12시(전방=_aimDir) 기준 시계/반시계 대칭으로 fill 비율만큼.
        int segs = RingSegments;
        float halfSpan = 180f * fill01;   // fill 1 = 360° 완전 링
        Vector3 fwd = _aimDir;
        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float ang = Mathf.Lerp(-halfSpan, halfSpan, t);
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
            _footRing.SetPosition(i, center + dir * RingRadius);
        }
    }

    LineRenderer CreateLine(string name, int posCount, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_owner, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = posCount;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        lr.material = new Material(sh);
        lr.material.renderQueue = 3100;   // 정보선 — 투명 큐 후순위(항상 위)
        lr.enabled = false;
        return lr;
    }

    float MovementSpeed()
    {
        // 프레임 위치 델타로 속도 추정(PlayerAfterimage가 _dashDir을 델타로 추정하는 동형 패턴).
        Vector3 d = _owner.position - _prevPos; d.y = 0f;
        return d.magnitude / Mathf.Max(0.0001f, Time.deltaTime);
    }

    /// <summary>PlayerCombat.OnDestroy에서 호출 — 런타임 생성물 정리 + 이벤트 해제.</summary>
    public void Cleanup()
    {
        if (_lunging) _player?.EndLungeMotion();   // 발도 도중 스왑/파괴 시 이동 잠금/무적 해제
        PlayerController.OnPlayerDamaged -= OnPlayerHit;
        DestroyLine(_footRing);
    }

    static void DestroyLine(LineRenderer lr)
    {
        if (lr == null) return;
        if (lr.material != null) Object.Destroy(lr.material);
        Object.Destroy(lr.gameObject);
    }
}
