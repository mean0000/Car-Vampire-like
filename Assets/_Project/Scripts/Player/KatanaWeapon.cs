using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카타나 — 좌클릭 콤보 3단(S1_Combo01_01→02→03). 각 좌클릭 '누름'이 다음 단을 낸다(홀드 자동연타 ❌).
///
/// 입력 버퍼: 공격 모션 중 누른 클릭을 inputBufferTime 동안 저장 → 캔슬 윈도우(클립 AnimationEvent
/// OnComboWindow)가 열리면 다음 단으로 캔슬 전환(격투게임식 관용). 타격/윈도우/끝 타이밍은 전부 클립
/// AnimationEvent가 소유한다(애니가 진실) — 코드는 입력 버퍼와 단 관리만 한다.
///
/// ★액션 슬롯(2026-07-05 교통정리): 콤보 외 액션(차징스킬 RMB/스킬 E/궁극기 R/반격/대시베기)은 전부
/// <see cref="WeaponActionSet"/> SO 슬롯 5개로 데이터 주도 — 새 스킬 = 에셋 만들어 빈 슬롯에 꽂으면 끝(코드 무수정).
/// 런타임 상태(진행·쿨다운·워치독)는 슬롯당 <see cref="ActionRuntime"/>이 들고, Begin/Hit/End 수명주기는
/// BeginActionSlot→DoActionHit→EndAction 단일 경로다(구 _skilling/_countering/_dashAttacking 3벌의 일반화).
///
/// 판정 대상은 IDamageable.
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

    /// <summary>콜라이더 표면 최근접점 — ★Collider.ClosestPoint는 convex 전용 API(Stab H-2 가드):
    /// 비convex 메시 콜라이더(미래 로스터가 FBX 딸림 콜라이더를 쓸 경우)는 중심 폴백(구 피벗 동작) — 무음 오작동 대신 보수 강등.</summary>
    static Vector3 SurfacePoint(Collider col, Vector3 point)
    {
        if (col is MeshCollider mc && !mc.convex) return col.transform.position;
        return col.ClosestPoint(point);
    }

    /// <summary>실효 사거리 = hit.range(폴백 1.8) × (rangeFromVfxScale면 짝지어진 VFX scale). 콤보 단·액션 공용(공통 어휘).</summary>
    static float EffectiveRange(WeaponHitData h, WeaponVfxData v)
    {
        float r = h.range > 0f ? h.range : 1.8f;
        if (h.rangeFromVfxScale) r *= (v != null && v.scale > 0f ? v.scale : 1f);
        return r;
    }

    [Header("★가르기 (Layer 2 기본 동사 — 커서=베고 지나갈 선. 콤보 판정을 부채꼴→절단선으로)")]
    [Tooltip("켜면 콤보(평타) 판정이 부채꼴 대신 조준 방향 절단선(폭 lineCutWidth) — '내가 그은 선을 따라 호드가 갈라진다'. " +
             "끄면 기존 부채꼴(랩 A/B 토글). 반격/스킬/대시베기는 기존 판정 유지.")]
    [SerializeField] bool lineCutEnabled = true;
    [Tooltip("절단선 전체 폭(m) — 커서 갭 관용(마그네티즘 겸). 넓을수록 관대·덜 정밀(방향 선택 가치 보존 주의 — Codex 리스크).")]
    [SerializeField, Min(0.2f)] float lineCutWidth = 1.6f;

    [Header("★히트 글라이드 (공간층 배려 — 빗나가면 짧게, 맞히면 길게)")]
    [Tooltip("켜면 콤보 적중 시 조준 방향으로 짧은 추가 전진(PlayerMotor.AddGlide) — 루트모션 런지(클립 소유) 위의 적중 보상.")]
    [SerializeField] bool hitGlideEnabled = true;
    [Tooltip("적중 시 추가 전진 거리(m). 카탈로그: miss=클립 런지만 / hit=+이 값.")]
    [SerializeField, Min(0f)] float hitGlideDistance = 0.25f;
    [Tooltip("★커밋 글라이드(m) — 이번 스윙이 크리티컬(커밋 적 타격)이면 일반 글라이드 대신 이 거리로 파고든다. " +
             "'커밋=돌파 표식'의 물리 보상(읽고 베면 뚫린다 — 2026-07-04 나·Codex 수렴, Codex 제안 0.8~1.2). 0=차등 없음.")]
    [SerializeField, Min(0f)] float commitGlideDistance = 1.0f;
    [Tooltip("추가 전진에 걸리는 시간(초) — 짧을수록 스냅.")]
    [SerializeField, Min(0.02f)] float hitGlideDuration = 0.12f;

    [Header("Layers")]
    [SerializeField] LayerMask enemyMask = 1 << 7;
    [SerializeField] LayerMask obstacleMask = 1 << 8;

    [Header("★액션 슬롯 (WeaponActionSet SO — 2026-07-05 교통정리. 스킬 추가 = 에셋 만들어 꽂기, 코드 무수정)")]
    [Tooltip("RMB — 차징스킬(Skill01). SO의 charge.enabled면 홀드→릴리스 발동, 끄면 즉발. 비우면 RMB 비활성.")]
    [SerializeField] WeaponActionSet chargeSkillAction;
    [Tooltip("E — 스킬 슬롯(컨트롤 스킴 동결 2026-06-26: RMB차징·E·R궁). 비우면 비활성(스킬 미배정 상태).")]
    [SerializeField] WeaponActionSet skillAction;
    [Tooltip("R — 궁극기 슬롯. 비우면 비활성(궁극기 미배정 상태).")]
    [SerializeField] WeaponActionSet ultimateAction;
    [Tooltip("패링 반격(Skill02) — 패링 성공 후 counterWindow 안 좌클릭 시 발동. 비우면 반격 비활성.")]
    [SerializeField] WeaponActionSet counterAction;
    [Tooltip("대시 베기(DashAttack, 클립 Attack02) — 대시 중 좌클릭 파생 입력. 비우면 비활성.")]
    [SerializeField] WeaponActionSet dashAttackAction;

    [Header("카운터 창 (패링 → 반격 입력 유효 시간)")]
    [Tooltip("패링 성공 후 반격 입력 창(초, 실시간). 이 안에 좌클릭하면 counterAction 발동. " +
             "액션 데이터가 아니라 패링 시스템 배선이라 SO가 아닌 무기 필드(2026-07-05 이관 시 의도적 잔류).")]
    [SerializeField] float counterWindow = 0.6f;

    [Header("VFX 앵커")]
    [Tooltip("★액션 VFX가 Weapon 기준일 때(슬래시) 정합 앵커 — 무기(칼) transform. 콤보 슬래시와 같은 Katana_Mesh를 넣는다. " +
             "Player 기준(불렛)일 땐 안 쓰임.")]
    [SerializeField] Transform weaponAnchor;
    /// <summary>칼 메시(weaponAnchor) — PlayerAnimatorDriver가 달리기 시 숨길 대상을 자동 연결할 때 읽는다(수동 할당 누락 방지).</summary>
    public Transform WeaponAnchor => weaponAnchor;

    [Header("★맞받음 / 클래시 (읽고-베기 코어 동사 — 적이 *타격하는 순간* 베면 클래시)")]
    [Tooltip("켜면 적이 *실제 타격 실행 중*(IAttackCommit.IsStriking = 돌진·물기 발사)일 때 베면 클래시: 데미지 ×배수 + 전파 + 짧은 프리즈(탁) + 시안. " +
             "★커밋 전체가 아니라 *타격 순간*만(수동 겹침 아닌 의도적 맞받음). 흰 NOW 펄스가 그 순간 신호. 끄면 일반 콤보.")]
    [SerializeField] bool timingCritEnabled = true;
    [Tooltip("클래시 데미지 배수(평타 데미지 ×N). 보통 맞받은 적을 한 방에 — 단 '안전'이 아니라 '흐름/스펙터클' 보상.")]
    [SerializeField, Min(1f)] float critDamageMult = 4f;
    [Tooltip("★크리티컬 전파 반경(m) — 크리티컬 지점 주변 이 거리의 적에게 작은 피해(밀집 호드를 가르는 복도). 0이면 전파 끔.")]
    [SerializeField, Min(0f)] float critPropagationRadius = 3f;
    [Tooltip("전파 피해량(작게 — 호드를 '가르는' 연출이지 광역 학살 아님). 밀도 있을 때만 빛난다.")]
    [SerializeField, Min(0)] int critPropagationDamage = 1;
    [Tooltip("크리티컬 카메라 킥(m) — 평타/피니셔보다 크게(크리티컬 '팝'). 전역 timeScale 안 씀(캐넌).")]
    [SerializeField] float critKick = 0.4f;
    [Tooltip("★크리 창 확장(07-04) — 켜면 IsStriking(타격 실행)뿐 아니라 *커밋 전체*(IsCommittingAttack = 윈드업 포함)에 크리. " +
             "브루트(윈드업 0.8s=읽기 창, Strike 0.15s)에서 읽고-베기가 실제로 성립하게. ★기본 꺼짐 — 06-24 맞받음-only 동작 보존, ReadAndCut 랩 씬만 명시 ON(A/B).")]
    [SerializeField] bool critOnWindup = false;
    [Tooltip("★멀티킬 프리즈 임계(한 스윙 처치 수) — 이 수 이상 베면 클래시 프리즈('탁')+크리급 카메라 킥. 무쌍 '쓸려나가는 질량감'(07-04). " +
             "★전역 timeScale 직접 안 씀 — ParrySlowMotion(ClashFx) 소유 경로 재사용(내부 쿨다운 포함). 0=끔.")]
    [SerializeField, Min(0)] int multiKillFreezeCount = 3;

    [Header("타격 손맛 (스냅·경쾌 — 칼이 적에 닿는 순간만 발동, 헛스윙엔 안 남)")]
    // ★전역 히트스탑(Time.timeScale)은 이 프로젝트에서 금지 — 일시정지 UI(레벨업/상자/정산/사망)·ParrySlowMotion과
    //   timeScale 소유권 충돌(게이트 Codex P0/P1). 스냅 손맛은 timeScale 무관한 카메라 킥으로만. 미세 프리즈는
    //   추후 '피격자만 정지'(캐넌 — 진짜 적 구현 시) 경로로 재도입.
    [Tooltip("평타(1·2타) 카메라 킥(m) — 조준 방향으로 짧게 밀었다 복귀. 스냅 손맛의 주력. 작게.")]
    [SerializeField] float comboKick = 0.12f;
    [Tooltip("피니셔(콤보 마지막 타)·반격·대시베기·스킬 카메라 킥(m) — 무게는 여기에 실린다. 평타보다 크게.")]
    [SerializeField] float finisherKick = 0.28f;

    [Header("★타격 사운드 (임시 플레이스홀더 — 2D 손맛, 거리감쇠 X. 음색은 유저 귀 판정)")]
    // ★필-볼트 롤백: 기각되면 meleeSfxEnabled=false로 즉시 끈다(노브 0 경로). 2D PlayOneShot(PlaySkillSfx와 동일 정책).
    //   2층 구조: ①swish=스윙 시작(헛스윙 포함) ②impact=적중 순간만(FireHitFeedback=connected 가드라 빗맞음 자동 제외).
    //   ★정석 타이밍: impact는 OnAttackHit 정렬(애니가 진실). swish는 본래 윈드업 AnimationEvent가 소유해야 하나,
    //     임시 플레이스홀더라 코드(BeginCombo/Advance)에서 스윙 시작에 낸다 — 추후 클립 OnSwishWhoosh 이벤트로 이관 권장.
    [Tooltip("★마스터 토글 — 끄면 swish/impact 둘 다 무음(롤백 경로).")]
    [SerializeField] bool meleeSfxEnabled = true;
    [Tooltip("베기 swish(휘두름) 클립 — 스윙 시작에 재생(헛스윙도 남). 비우면 무음. 임시=Vefects Slash_Classic.")]
    [SerializeField] AudioClip swishClip;
    [Tooltip("swish 볼륨 — ★캐넌 첫 볼륨 0.03~0.15. 안 들리면 올린다(반대보다 안전).")]
    [SerializeField, Range(0f, 1f)] float swishVolume = 0.10f;
    [Tooltip("swish 기준 피치. 낮추면 무겁게/길게, 높이면 가볍게/날카롭게.")]
    [SerializeField, Range(0.5f, 2f)] float swishPitch = 1f;
    [Tooltip("swish 피치 랜덤 폭(±) — 연속 스윙 동일음 피로 완화. 0이면 고정.")]
    [SerializeField, Range(0f, 0.5f)] float swishPitchJitter = 0.05f;
    [Tooltip("임팩트 thud/cut 클립 — 적중 순간만 재생(헛스윙 무음). 비우면 무음. 임시=Vefects Impact_01.")]
    [SerializeField] AudioClip impactClip;
    [Tooltip("impact 볼륨 — ★캐넌 첫 볼륨 0.03~0.15. '한 방' 강조라 swish보다 약간 크게.")]
    [SerializeField, Range(0f, 1f)] float impactVolume = 0.14f;
    [Tooltip("impact 기준 피치 — DD 묵직: 살짝 낮춰 바디/무게. 더 낮추면 더 묵직.")]
    [SerializeField, Range(0.5f, 2f)] float impactPitch = 0.95f;
    [Tooltip("impact 피치 랜덤 폭(±) — 연타 동일음 피로 완화.")]
    [SerializeField, Range(0f, 0.5f)] float impactPitchJitter = 0.04f;

    [Header("★무빙 평타 (07-04 유저 지시 — 좌클릭 콤보 3타, 이동하며 베기)")]
    [Tooltip("★마스터 토글 — 콤보(평타) 중 이동 입력이 감쇠 배율로 계속 움직인다. 반격/스킬/대시베기는 기존 정지 커밋 유지. " +
             "★켜면 이동 self-cancel(Day2)은 자동 무력화 — 이동이 '이탈 신호'가 아니게 되고, 켠 채 두면 이동 홀드 시 매 타 ResetCombo로 3타 도달 불가라 상호배타(끄면 Day2 정지베기+이동캔슬 복귀 = A/B).")]
    [SerializeField] bool comboMoveEnabled = true;
    [Tooltip("콤보 중 이동 속도 배율(moveSpeed 기준). 0.6~0.7 권장 — 전력 이동이면 베기 무게가 죽는다. ⚠️공격 클립이 전신이라 발 미끄러짐 있음 — 상체분리(미착수) 전 화이트박스 트레이드오프.")]
    [SerializeField, Range(0.1f, 1f)] float comboMoveSpeedMult = 0.65f;

    [Header("★이동 self-cancel (후딜 제거 — DD/DMC식: 회수를 이동으로 끊어 즉시 로코모션)")]
    // ★캔슬창 이후(hit 지남)에만 — 윈드업~스트라이크는 이동 불가로 베기 무게/간지 보존(트윈스틱 붕괴 방지선).
    //   클릭(콤보지속)·회피(대시)가 이동캔슬보다 우선(Advance 분기 뒤 배치). 피니셔(comboMax)는 제외(대시-온리 커밋 캐넌 유지).
    [Tooltip("★마스터 토글 — 끄면 이동으로 회수 캔슬 안 됨(롤백/이전 동작).")]
    [SerializeField] bool moveCancelEnabled = true;
    [Tooltip("이동 캔슬 데드존 — 이 크기 이상의 이동 입력만 회수를 끊는다(미세 드리프트로 매 베기 캔슬 방지). 0.2 권장. " +
             "★하한 0.01 — 0이면 zero벡터(미입력)도 통과해 캔슬창마다 자동 리셋(Stab H-1). 끄려면 moveCancelEnabled 토글.")]
    [SerializeField, Range(0.01f, 1f)] float moveCancelDeadzone = 0.2f;

    /// <summary>★액션 슬롯 1개의 런타임 상태 — SO(<see cref="WeaponActionSet"/>)는 읽기 전용(공유 에셋 오염 금지),
    /// 가변값(진행·쿨다운·워치독·타격 카운터)은 전부 여기. 구 _countering/_skilling/_dashAttacking + 개별 타이머 3벌의 일반화.</summary>
    sealed class ActionRuntime
    {
        public readonly WeaponActionSet set;
        public bool active;           // 진행 중(구 _skilling 등) — 상호배타는 입력 게이트가 보장(_activeAction 단일)
        public float cooldownTimer;   // 쿨다운 잔여(구 _skillCdTimer)
        public float watchdogTimer;   // 안전 워치독(스케일 시간) — OnComboEnd 누락/하드컷 고착 방지(구 *FallbackTimer)
        public int hitSeq;            // 타격 누적 — 차징스킬은 ChargePhantomEmitter가 '벨 때' 감지용으로 폴링
        public ActionRuntime(WeaponActionSet set) { this.set = set; }
        public float Watchdog => set.timing.maxDuration > 0f ? set.timing.maxDuration : 3.5f;
    }

    int _step;            // 0=idle, 1..comboMax 진행 중
    bool _windowOpen;     // 캔슬 윈도우(다음 단 입력 가능) — AnimationEvent가 연다
    bool _buffered;       // 입력 버퍼
    float _bufferTimer;
    bool _hitDone;        // 현재 단 타격 1회 가드
    float _startCdTimer;
    float _lastAdvanceTime = -1f;   // 마지막 Advance 시각 — 직후 이전 클립의 지연 OnComboEnd를 무시(Stab M-1 경합 방어)
    float _counterTimer;            // 패링 후 반격 입력 창(unscaled — 슬로모 무관, 관대)
    ActionRuntime _chargeSkillRt, _skillRt, _ultimateRt, _counterRt, _dashAttackRt;   // 슬롯별 런타임(미배정 슬롯=null)
    ActionRuntime[] _allActions = System.Array.Empty<ActionRuntime>();   // 쿨다운/워치독 일괄 순회용(null 제외)
    ActionRuntime _activeAction;    // 진행 중 액션(콤보 _step과 독립) — null=액션 없음
    bool _charging;                 // ★차징스킬 차징 진행 중(RMB 홀드). ChargePhantomEmitter가 IsCharging을 읽어 팬텀 방출.
    float _chargeTime;              // 누적 차징 시간(초, charge.chargeMax 캡)

    Vector3 _aimDir = Vector3.forward;    // 단 시작 시 잠근 조준 — 타격 판정 방향(facing/런지와 통일). 단 진행 중 고정.
    Vector3 _liveAim = Vector3.forward;   // 매 프레임 최신 조준 — 단 시작(BeginCombo/Advance)에 _aimDir로 캡처.
    readonly HashSet<IDamageable> _hitThisSwing = new HashSet<IDamageable>();
    readonly Collider[] _overlap = new Collider[128];
    // ── ★타이밍-크리티컬 ── (전파는 DoHit 루프 종료 후 별 버퍼로 — _overlap 덮어쓰기 방지)
    readonly Collider[] _propOverlap = new Collider[128];   // ★H-3: _overlap과 동일 크기(조밀 무제한 호드서 전파 포화 완화)
    readonly List<Vector3> _critCenters = new List<Vector3>(8);   // 이번 스윙의 크리티컬 지점(루프 후 전파)
    bool _lastSwingCrit;   // 직전 DoHit에서 크리티컬이 났나(손맛 킥 강도 선택용)
    int _lastSwingKills;   // 직전 DoHit 처치 수(멀티킬 프리즈/킥 승급용)

    /// <summary>★칼 액션 활성(콤보·반격·스킬·대시베기) — SwarmChaser 스윙 가드(전방 접촉 무효, 07-04 배려)가 읽는다.
    /// ★콤보만이 아니라 모든 칼 액션 포함(Stab M-5) — "공격=방어" 기대와 체감 괴리 방지. (차징은 기존과 동일하게 제외.)</summary>
    public bool IsSwingActive => _step >= 1 || _activeAction != null;

    /// <summary>★스윙 가드 기준 방향 = 콤보 잠금 조준(_aimDir). ★플레이어 *루트*는 회전하지 않는다(모델 자식만 돎) —
    /// 외부에서 transform.forward를 쓰면 월드 +Z 고정 방향을 보호하는 버그(Codex P1, 07-04). 반드시 이걸 읽을 것.</summary>
    public Vector3 SwingGuardForward => _step >= 1 ? _aimDir : _liveAim;

    /// <summary>★무빙 평타(07-04) — 콤보 중에만 이동 배율 노출. 반격/스킬/대시베기=0(정지 커밋 유지).</summary>
    public override float ActionMoveMult => comboMoveEnabled && _step >= 1 ? comboMoveSpeedMult : 0f;
    ParrySlowMotion _clashFx;   // 클래시 프리즈 트리거(지연 해석·캐시). 플레이어의 ParrySlowMotion 재사용.
    bool _clashFxResolved;
    PlayerMotor _motor;         // ★히트 글라이드 대상(지연 해석·캐시) — 위치 소유자.
    bool _motorResolved;

    // ── ★AtomLab 디버그 채널 토글 캐시 — off↔on 스왑의 기준점(직렬화값 원본). ──
    float _origComboKick, _origFinisherKick;
    bool _origHitGlideEnabled;

    void Awake()
    {
        _origComboKick = comboKick;
        _origFinisherKick = finisherKick;
        _origHitGlideEnabled = hitGlideEnabled;
    }

    public override void Initialize(Transform owner, PlayerAnimatorDriver animator)
    {
        // ★Stab H-1: Owner가 바뀔 수 있으니 ClashFx 캐시 무효화(이전 Owner 계층의 stale ParrySlowMotion 잔존 → 클래시 프리즈 무음 누락 방지).
        _clashFx = null;
        _clashFxResolved = false;
        _motor = null;            // 동일 사유 — Motor 캐시도 무효화
        _motorResolved = false;

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
        BuildActionRuntimes();   // ★슬롯 SO → 런타임 구축 + 트리거 해시 캐시(재초기화 시 재구축 — 쿨다운 리셋은 Owner 스왑 한정이라 허용)
    }

    /// <summary>슬롯 SO 5개 → ActionRuntime 구축. 트리거 이름 해시 캐시(<see cref="WeaponAnimatorData.Resolve"/>) +
    /// 배선 실패(트리거 이름 누락)를 무음 강등으로 두지 않는다(Codex 리스크: SO 문자열 오타 — 즉시 에러 노출).</summary>
    void BuildActionRuntimes()
    {
        _chargeSkillRt = MakeRuntime(chargeSkillAction, "chargeSkillAction(RMB)");
        _skillRt = MakeRuntime(skillAction, "skillAction(E)");
        _ultimateRt = MakeRuntime(ultimateAction, "ultimateAction(R)");
        _counterRt = MakeRuntime(counterAction, "counterAction(패링반격)");
        _dashAttackRt = MakeRuntime(dashAttackAction, "dashAttackAction(대시베기)");
        var list = new List<ActionRuntime>(5);
        if (_chargeSkillRt != null) list.Add(_chargeSkillRt);
        if (_skillRt != null) list.Add(_skillRt);
        if (_ultimateRt != null) list.Add(_ultimateRt);
        if (_counterRt != null) list.Add(_counterRt);
        if (_dashAttackRt != null) list.Add(_dashAttackRt);
        _allActions = list.ToArray();
        _activeAction = null;
    }

    ActionRuntime MakeRuntime(WeaponActionSet set, string slotName)
    {
        if (set == null) return null;   // 빈 슬롯 = 그 액션 비활성(정상 — E/R은 스킬 배정 전까지 비움)
        set.animator.Resolve();
        if (set.animator.TriggerHash == 0)
            Debug.LogError($"[KatanaWeapon] {slotName} 슬롯 '{set.actionId}' — animator.triggerName이 비어 있어 진입 불가. SO를 확인하라.", this);
        if (set.charge.enabled && set.animator.ChargeTriggerHash == 0)
            Debug.LogError($"[KatanaWeapon] {slotName} 슬롯 '{set.actionId}' — charge.enabled인데 chargeTriggerName이 비어 있음(차징 진입 불가).", this);
        return new ActionRuntime(set);
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
    // _step/_activeAction은 이제 '진행 로직'(어느 단·어느 액션 분기)만 담당하고 busy를 직접 정하지 않는다(애니가 진실).

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
        _counterTimer = 0f;      // 회피가 반격 창도 소거(최우선 입력)
        if (_activeAction != null)   // 회피가 진행 중 액션(반격/스킬/대시베기)을 가로챔 — 쿨다운은 소비 유지(환불 안 함)
        {
            _activeAction.active = false;
            _activeAction.watchdogTimer = 0f;
            _activeAction = null;
        }
        // ★H-1(Stab High·Codex 수렴): 차징 중 하드컷 시 Charge/Hold(Action)→Locomotion 취소 트리거 — 비대시 Cancel(사망·스턴·미래) 시 speed0 홀드 영구 고착(소프트락) 방지
        if (_charging && _chargeSkillRt != null) AnimatorDriver?.TriggerActionCancel(_chargeSkillRt.set.animator.CancelTriggerHash);
        _charging = false;       // 회피(새 대시)가 차징도 가로챔(대시 우선)
        _chargeTime = 0f;
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
        // ★슬롯 쿨다운·워치독 일괄(구 반격/스킬/대시베기 개별 타이머의 일반화) — 워치독은 최후 방어선(정상 종료=클립
        //   OnComboEnd, 더 빠른 복구=아래 진행플래그 자가치유). 클립 재생은 스케일 시간이라 Time.deltaTime로 감쇠(정렬).
        for (int i = 0; i < _allActions.Length; i++)
        {
            var rt = _allActions[i];
            if (rt.cooldownTimer > 0f) rt.cooldownTimer -= dt;
            if (rt.active)
            {
                rt.watchdogTimer -= Time.deltaTime;
                if (rt.watchdogTimer <= 0f) EndAction(rt);
            }
        }

        // ★레일 자가치유(진행 플래그) — busy가 풀렸는데(유예 만료 + Animator가 Action 아님) 진행 플래그가 남아 있으면
        //   액션 진입 실패(전이 경쟁·★"Action" 태그 누락)로 보고 진행 상태도 닫는다. busy(이동)뿐 아니라 입력 게이트
        //   (특히 진행 액션이 입력을 묵살하는 것)까지 자가 복구 — Codex M. 정상 동작 중엔 IsBusy=true라 발화 안 함.
        if (!IsBusy && (_step > 0 || _activeAction != null || _charging))
        {
            // 독립 if — 이론적 다중 고착도 전부 닫는다(Stab H-2). 각 End/Reset은 SetCombo(0) 멱등이라 중복 안전.
            // ★M-1(Stab): _charging 고아 정리(다른 진행 플래그와 동형). IsBusy=false면 Animator는 이미 비-Action(Charge/Hold 탈출)이라 취소 트리거 불요.
            if (_charging) { _charging = false; _chargeTime = 0f; }
            if (_activeAction != null) EndAction(_activeAction);
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
            if (_activeAction != null || _charging) { /* 액션/차징 중 좌클릭 무시 — 커밋(차징 중엔 안 침) */ }
            // ★쿨다운 게이트 통일(Stab M-1): 현재 반격 cooldown=0이라 무변화지만, 데이터가 노출하는 노브는 게이트가 읽어야 한다("바꿨는데 안 먹힘" 사고 클래스 방지)
            else if (_counterTimer > 0f && _step == 0 && _counterRt != null && _counterRt.cooldownTimer <= 0f) BeginCounter();
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

        // ★대시 베기(DashAttack) — 대시 중 좌클릭을 PlayerBrain이 대시 끝 프레임에 주입. idle일 때만(콤보/액션/차징 중 무시).
        //   대시가 직전 콤보를 Cancel하므로 보통 _step==0이 보장됨.
        if (input.dashAttack && _dashAttackRt != null && _dashAttackRt.cooldownTimer <= 0f
            && _step == 0 && _activeAction == null && !_charging)   // ★쿨다운 게이트 통일(Stab M-1)
            BeginActionSlot(_dashAttackRt);

        // ★우클릭 차징스킬 — SO의 charge.enabled면 차징식, 아니면 즉발(레거시).
        //   차징: RMB 누름→진입(idle+쿨다운 준비), 홀드 동안 누적(chargeMax 캡), 뗌→발동. 차징 중 ChargePhantomEmitter가 팬텀 방출.
        //   (대시 중 RMB down은 PlayerBrain이 억제 — 대시 커밋 보호.)
        if (_chargeSkillRt != null && _chargeSkillRt.set.charge.enabled)
        {
            if (!_charging)
            {
                if (input.secondaryDown && !IsBusy && _chargeSkillRt.cooldownTimer <= 0f
                    && _step == 0 && _activeAction == null)
                    BeginCharge(_chargeSkillRt);
            }
            else
            {
                // ★뗌·입력유실·포커스손실·같은프레임 탭 전부 수렴 — secondaryHeld가 끊기면 차징 종료(고착 방지).
                //   secondaryUp 단독 의존 금지(Stab H-2 / Codex CRITICAL·HIGH 수렴): up 엣지를 놓치면 영구 고착 → 소프트락.
                if (!input.secondaryHeld) ReleaseCharge(_chargeSkillRt);
                else _chargeTime = Mathf.Min(_chargeTime + dt, _chargeSkillRt.set.charge.chargeMax);
            }
        }
        else if (_chargeSkillRt != null && input.secondaryDown && !IsBusy && _chargeSkillRt.cooldownTimer <= 0f)
            BeginActionSlot(_chargeSkillRt);   // 레거시 즉발(charge.enabled=false) — 구 BeginSkill 경로와 동일 게이트

        // ★E 스킬 / R 궁극기 슬롯(컨트롤 스킴 동결 2026-06-26) — 즉발 진입. 미배정(null)이면 무동작.
        //   게이트는 차징 진입과 동일(엄격 셋). 대시 중 억제는 PlayerBrain이 RMB와 동일 정책으로 처리.
        if (input.skillDown) TryBeginInstantAction(_skillRt);
        if (input.ultimateDown) TryBeginInstantAction(_ultimateRt);

        // 입력 버퍼 감쇠
        if (_buffered)
        {
            _bufferTimer -= dt;
            if (_bufferTimer <= 0f) _buffered = false;
        }

        // 캔슬 윈도우 열렸고 버퍼 입력 있으면 다음 단으로 캔슬 전환(1→2→3가 흐름).
        //   ★하데스식 피니셔: 마지막 단(comboMax)은 공격으로 재시작 못 한다 — 3타 회수는 대시로만 빠져나가는 커밋이다.
        //   무한 연타 억제는 인공 쿨다운이 아니라 "3타 회수 + 대시캔슬 리듬"으로 한다(공격→대시→공격).
        if (_windowOpen && _buffered && _step >= 1 && _step < comboMax)
            Advance();
        // ★이동 self-cancel(후딜 제거) — 캔슬창 이후(hit 지남) 이동 입력이 오면 회수를 끊고 로코모션으로(DD/DMC).
        //   Advance(클릭=콤보지속) 분기 뒤 else라 클릭이 이김. 회피(대시)는 PlayerBrain이 더 먼저 Cancel. 피니셔(comboMax) 제외.
        //   소프트캔슬(ResetCombo+base.Cancel) — 하드 Cancel()과 달리 counter/skill/charge·_counterTimer 안 건드림(Codex).
        else if (moveCancelEnabled
                 && !comboMoveEnabled   // ★무빙 평타와 상호배타(07-04) — 이동이 상시 허용이면 '이동=이탈 신호'가 성립 안 함(매 타 ResetCombo 사고 방지)
                 && _step >= 1 && _step < comboMax
                 && _windowOpen && _hitDone
                 && _activeAction == null && !_charging
                 && input.move.sqrMagnitude >= moveCancelDeadzone * moveCancelDeadzone)
        {
            ResetCombo();    // 콤보 진행 idle로(SetCombo(0) → Combo→Loco 전이 = 기존 복귀 경로)
            base.Cancel();   // 레일 유예 해제(busy 즉시 이동 양보)
        }
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
        PlayMeleeSwish();   // ★스윙 시작 = swish(휘두름). 헛스윙도 남(적중 불문). 강타 impact는 FireHitFeedback(connected).
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
        PlayMeleeSwish();   // ★다음 단 스윙 시작 = swish(각 단마다 휘두름음).
    }

    /// <summary>★액션 슬롯 공통 발동 — 구 BeginCounter/BeginSkill/BeginDashAttack의 일반화(동작 보존).
    /// 콤보 _step과 독립(busy로 잠김), 타격은 OnHitFrame이 _activeAction일 때 DoActionHit, 종료는 OnComboEnd/워치독.
    /// 쿨다운 소비 + facing은 발동 순간 조준에 잠금. ★VFX/사운드는 발동 순간이 아니라 타격 순간(칼 벨 때)에 낸다.</summary>
    void BeginActionSlot(ActionRuntime rt)
    {
        // ★게이트 수정(Codex HIGH): 트리거 미해결(이름 공백/오타)이면 상태·쿨다운 소비 전에 중단 —
        //   TriggerAction(0)은 무동작이라 "애니 없는 액션"에 입력 잠금+쿨다운 낭비가 생기는 것 차단(초기화 시 이미 에러 로그).
        if (rt.set.animator.TriggerHash == 0) return;
        _aimDir = _liveAim;       // 액션 방향을 현재 조준으로 잠금(타격/모션 통일)
        rt.active = true;
        _activeAction = rt;
        _hitDone = false;
        rt.cooldownTimer = rt.set.timing.cooldown;
        rt.watchdogTimer = rt.Watchdog;   // 안전 워치독 가동(OnComboEnd 누락 대비)
        AnimatorDriver?.TriggerAction(rt.set.animator.TriggerHash);
        BeginAction();   // 레일: 진입 유예 켬 → Animator가 해당 상태(Action 태그) 들 때까지 busy 유지
    }

    /// <summary>★패링 반격 — 카운터 창 안 좌클릭 시 counterAction 발동(창 소비 후 공통 경로).</summary>
    void BeginCounter()
    {
        _counterTimer = 0f;       // 창 소비
        _windowOpen = false;
        BeginActionSlot(_counterRt);
    }

    /// <summary>E/R 즉발 슬롯 진입 게이트 — 미배정(null)이면 무동작. 차징 진입과 동일한 엄격 게이트.</summary>
    void TryBeginInstantAction(ActionRuntime rt)
    {
        if (rt == null) return;
        if (IsBusy || rt.cooldownTimer > 0f || _step != 0 || _activeAction != null || _charging) return;
        BeginActionSlot(rt);
    }

    /// <summary>액션 종료 공통 경로 — 클립 OnComboEnd(정상)와 워치독/자가치유(폴백) 둘 다 여기로 합류.
    /// SetCombo(0)이 _lockedFace도 해제(Stab H-2 — 액션 facing 잠금이 함께 풀린다). 구 EndCounter/EndSkill/EndDashAttack 통합.</summary>
    void EndAction(ActionRuntime rt)
    {
        rt.active = false;
        rt.watchdogTimer = 0f;
        if (_activeAction == rt) _activeAction = null;
        _hitDone = false;
        AnimatorDriver?.SetCombo(0);
    }

    AudioSource _actionSfxSource;   // 2D one-shot 소스(첫 사용 시 생성 — PlayerAttackSfx와 동일 정책)
    /// <summary>액션 사운드 — 2D(거리감쇠 없음) PlayOneShot. 비어 있으면 무동작. (구 PlaySkillSfx 일반화.)</summary>
    void PlayActionSfx(WeaponSfxData sfx)
    {
        if (sfx == null || sfx.clip == null) return;
        if (_actionSfxSource == null)
        {
            _actionSfxSource = gameObject.AddComponent<AudioSource>();
            _actionSfxSource.playOnAwake = false;
            _actionSfxSource.spatialBlend = 0f;   // 2D — 손맛 사운드는 카메라 거리감쇠 X
        }
        _actionSfxSource.PlayOneShot(sfx.clip, sfx.volume);
    }

    // ── ★타격 사운드(임시 플레이스홀더) — swish/impact 2D one-shot. 피치 독립 위해 소스 분리(겹쳐도 서로 안 끌림). ──
    AudioSource _swishSource;    // 2D one-shot(swish 전용). 첫 사용 시 생성(PlaySkillSfx 정책).
    AudioSource _impactSource;   // 2D one-shot(impact 전용).

    /// <summary>★베기 swish — 스윙 시작에 2D 재생(헛스윙 포함). 피치 지터로 연속 스윙 단조로움 완화. 비었거나 토글 off면 무음.</summary>
    void PlayMeleeSwish()
    {
        if (!meleeSfxEnabled || swishClip == null) return;
        if (_swishSource == null) _swishSource = NewMeleeSfxSource();
        _swishSource.pitch = Mathf.Max(0.01f, swishPitch + Random.Range(-swishPitchJitter, swishPitchJitter));   // ★M-1(Stab): pitch 0 바닥 가드 — 튜닝 슬라이더가 0(무음)에 닿지 않게.
        _swishSource.PlayOneShot(swishClip, swishVolume);
    }

    /// <summary>★임팩트 thud/cut — 칼이 적에 닿았을 때만(FireHitFeedback이 connected에서만 호출 → 헛스윙 자동 제외, 가드 일원화).</summary>
    void PlayMeleeImpact()
    {
        if (!meleeSfxEnabled || impactClip == null) return;
        if (_impactSource == null) _impactSource = NewMeleeSfxSource();
        _impactSource.pitch = Mathf.Max(0.01f, impactPitch + Random.Range(-impactPitchJitter, impactPitchJitter));   // ★M-1(Stab): pitch 0 바닥 가드.
        _impactSource.PlayOneShot(impactClip, impactVolume);
    }

    /// <summary>2D one-shot 소스 생성 — 손맛 사운드는 카메라 거리감쇠 X(spatialBlend=0, PlaySkillSfx와 동일 정책).</summary>
    AudioSource NewMeleeSfxSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    /// <summary>★차징 시작 — RMB 누름. 전방(팬텀 방출 방향)을 현재 조준으로 잠금. 페이로드는 ReleaseCharge.</summary>
    void BeginCharge(ActionRuntime rt)
    {
        if (rt.set.animator.ChargeTriggerHash == 0) return;   // 미해결 차징 트리거 — 무애니 차징 진입 차단(BeginActionSlot 가드와 동형)
        _aimDir = _liveAim;
        _charging = true;
        _chargeTime = 0f;
        // ★눈에 보이는 차징: 윈드업(프레임 0→70) 재생 후 프레임 70에서 홀드(Skill01Charge→Skill01Hold).
        //   RMB 누르는 내내 홀드 유지(팬텀은 _charging으로 계속 방출). 발동(베기)은 ReleaseCharge→BeginActionSlot이 낸다.
        // ★L-1(Stab): 다른 액션 진입(BeginCombo/BeginActionSlot)과 달리 BeginAction() 의도적 생략 —
        //   불발(abort) 시 즉시 IsBusy=false라야 반응성 유지(유예 남으면 abort 후 묶임). 차징 중 입력 게이트는 _charging이 대신.
        AnimatorDriver?.TriggerCharge(rt.set.animator.ChargeTriggerHash, rt.set.animator.CancelTriggerHash);
    }

    /// <summary>★차징 종료(비트3 페이로드) — RMB 뗌. 최소 차징(charge.minChargeToFire) 이상이면 발동(BeginActionSlot), 미만이면 불발.
    /// 불발은 쿨다운도 소비 안 함(빈 탭 가드 — Stab M-2). 발동 경로/판정/VFX·SFX/쿨다운/종료는 전부 BeginActionSlot·DoActionHit이 소유
    /// (레거시 즉발과 단일 경로 — 분기 없음). 발동 방향은 BeginActionSlot이 발동 순간 조준(_liveAim)으로 재잠금.
    /// ★차징 시간→발동 강도 스케일(ChargeTime01)은 아직 안 건다 — '크레딧(저작감)' 다이얼로 유저 판정 후 얹는다(현재 차징은 팬텀 수만 구동).</summary>
    void ReleaseCharge(ActionRuntime rt)
    {
        // ★게이트 수렴 수정(Stab M-1 ≡ Codex RISK5): minChargeToFire가 chargeMax를 넘으면 _chargeTime(chargeMax 캡)이
        //   영원히 못 미쳐 *영구 무음 불발*. 사용처에서 chargeMax로 클램프 → 최악도 '풀차징에 발동'(영구 죽음 제거).
        var c = rt.set.charge;
        bool fire = _chargeTime >= Mathf.Min(c.minChargeToFire, c.chargeMax);
        _charging = false;
        _chargeTime = 0f;
        // ★발동: BeginActionSlot이 진행/쿨다운/워치독 세팅 + 트리거(→Skill01Strike, 프레임 70부터 베기 이어재생).
        //   데미지(DoActionHit)는 베기 구간의 OnAttackHit(@norm0.556=프레임80)에서만 발동(윈드업서 발동 안 함).
        // ★불발(최소차징 미달): 윈드업/홀드를 idle로 되돌린다 — 안 그러면 윈드업 트리거가 쏜 홀드에 고착(소프트락).
        if (fire) BeginActionSlot(rt);
        else AnimatorDriver?.TriggerActionCancel(rt.set.animator.CancelTriggerHash);
    }

    // ── AnimationEvent 릴레이(PlayerAnimatorDriver 경유) — 타이밍은 클립이 소유 ──
    protected override void OnHitFrame(int hitFrameIndex)   // 타격 정점
    {
        if (_activeAction != null) { if (!_hitDone) { _hitDone = true; DoActionHit(_activeAction); } }   // 액션(스킬/반격/대시베기) 타격
        else if (_step >= 1 && !_hitDone) { _hitDone = true; DoSwingHit(); }
    }

    void OnComboWindow()   // 캔슬 윈도우 시작 — 다음 단 입력 받기 시작(각 좌클릭이 각 단)
    {
        if (_step >= 1) _windowOpen = true;
    }

    void OnComboEnd()      // 현재 단/액션 클립 끝 — 다음 단으로 안 넘어갔으면 종료(idle 복귀)
    {
        if (_activeAction != null) { EndAction(_activeAction); return; }   // 액션(스킬/반격/대시베기) 정상 종료 — 클립 끝 OnComboEnd
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
        Motor()?.ClearActionMove();   // ★무빙 평타 즉시 종료(Codex P1) — 같은 프레임 stale 속도 꼬리 누수 차단
    }

    /// <summary>현재 콤보 단의 공격 스텝(1-based). 범위 밖이면 마지막 단으로 클램프, 비거나 스텝 손상(null 원소)이면 안전 기본값.
    /// ★TryGetStep 반환값을 버리지 않는다(Stab H-1 — class화로 null 스텝 경로가 생겨, 무시하면 DoSwingHit NRE로 평타가 조용히 증발).</summary>
    ComboAttackSet.ComboAttackStep GetHit(int step)
    {
        if (comboSet != null && comboSet.TryGetStep(step, out var s) && s.hit != null) return s;
        Debug.LogWarning("[KatanaWeapon] comboSet 미할당/비어 있음/스텝 손상 — 하드코딩 폴백 사용. ComboAttackSet 확인 요망.", this);
        return new ComboAttackSet.ComboAttackStep
        {
            hit = new WeaponHitData { range = 1.8f, arcHalfAngle = 50f, forwardOffset = 0f, damage = 3, knockback = 4f }
        };
    }

    #region ★AtomLab 디버그 채널 토글 (원자 테스트 랩 전용 — AtomLabRig가 구동. off=0/false 스왑, on=Awake 캐시값 복원)
    /// <summary>타격 사운드 채널 on/off — meleeSfxEnabled 직결(PlayMeleeSwish/PlayMeleeImpact가 이 값으로 가드).</summary>
    public void SetSfxEnabled(bool on) => meleeSfxEnabled = on;

    /// <summary>입력측 피드백 채널 on/off — 카메라 킥(comboKick/finisherKick)과 히트 글라이드(hitGlideEnabled) 일괄.
    /// off는 킥 0(FireHitFeedback의 AddKick이 사실상 무동작) + 글라이드 끔, on은 Awake 캐시값으로 복원.</summary>
    public void SetInputFeedbackEnabled(bool on)
    {
        comboKick = on ? _origComboKick : 0f;
        finisherKick = on ? _origFinisherKick : 0f;
        hitGlideEnabled = on && _origHitGlideEnabled;
    }
    #endregion

    // ── 디버그 시각화용 읽기전용 접근자(HitboxDebugManager 전용) — 전투 로직 무관 ──
    public Transform DebugOwner => Owner;
    // ── 차징 상태(ChargePhantomEmitter 전용 읽기) ──
    public bool IsCharging => _charging;
    /// <summary>★차징 *윈드업*(Skill01Charge 재생, 프레임 0→70) 중인가 — 팬텀은 이 동안만 방출(홀드/베기 제외 = 애니에 결속).
    /// _charging ∧ Animator가 윈드업 상태. 홀드(프레임70 동결) 진입 시 false → 방출 멈춤.</summary>
    public bool IsChargeWindup => _charging && AnimatorDriver != null && AnimatorDriver.IsInSkillChargeWindup;
    public float ChargeTime01 => _chargeSkillRt != null && _chargeSkillRt.set.charge.chargeMax > 0f
        ? Mathf.Clamp01(_chargeTime / _chargeSkillRt.set.charge.chargeMax) : 0f;
    /// <summary>차징 시작 시 잠근 조준 방향 — 팬텀 전방 방출 방향.</summary>
    public Vector3 ChargeAimDir => _aimDir;
    /// <summary>★차징스킬 베기 타격 누적 횟수 — ChargePhantomEmitter가 증가를 감지해 '벨 때' 슬래시 VFX 1발 발동(윈드업 아닌 실제 베기).
    /// (Codex 리스크 반영 — 팬텀 결합은 이 최소 접근자만 유지, 내부는 슬롯 런타임으로 위임.)</summary>
    public int SkillHitSeq => _chargeSkillRt != null ? _chargeSkillRt.hitSeq : 0;
    /// <summary>★차징스킬 판정 사거리(m) — ChargePhantomEmitter가 팬텀 전진 거리 상한으로 읽음(차징 공격 사거리만큼만 나감). 슬롯 비면 0.</summary>
    public float SkillRange => chargeSkillAction != null ? chargeSkillAction.hit.range : 0f;
    public int DebugStep => _step;
    /// <summary>공격 중이면 잠근 방향(_aimDir), 평시엔 라이브 조준(_liveAim) — 프리뷰용.</summary>
    public Vector3 DebugAimDir => _step >= 1 ? _aimDir : _liveAim;
    public int DebugHitCount => comboSet != null ? comboSet.StepCount : 0;
    /// <summary>단별 히트박스 파라미터(1-based). DoSwingHit과 동일 폴백 가드.</summary>
    public bool DebugGetHit(int step, out float range, out float arcHalf, out float forwardOffset)
    {
        range = 1.8f; arcHalf = 50f; forwardOffset = 0f;
        if (comboSet == null || !comboSet.TryGetStep(step, out var s) || s.hit == null) return false;   // ★null 스텝 가드(Stab H-1)
        range = EffectiveRange(s.hit, s.vfx);   // 슬래시 스케일 연동 포함 — 디버그 뷰가 실효 판정을 그린다
        arcHalf = s.hit.arcHalfAngle > 0f ? s.hit.arcHalfAngle : 50f;
        forwardOffset = Mathf.Max(0f, s.hit.forwardOffset);
        return true;
    }

    void DoSwingHit()
    {
        ComboAttackSet.ComboAttackStep s = GetHit(_step);
        WeaponHitData h = s.hit;
        // 0/미설정 직렬화값 폴백 가드(필드 추가 시 default 0 함정).
        bool connected = DoHit(EffectiveRange(h, s.vfx),      // range × (rangeFromVfxScale면 그 단 슬래시 스케일)
              h.arcHalfAngle > 0f ? h.arcHalfAngle : 50f,
              Mathf.Max(0f, h.forwardOffset),
              h.damage > 0 ? h.damage : 1,
              h.knockback,
              lineCutEnabled);   // ★가르기 — 콤보(평타)만 절단선 판정(반격/스킬/대시베기는 기존 부채꼴)
        // 피니셔(콤보 마지막 타)는 무게, 평타는 스냅 — 닿았을 때만. ★크리티컬이면 더 큰 '팝' 킥(critKick).
        bool finisher = comboSet != null && _step >= comboSet.StepCount;
        if (connected)
        {
            // ★멀티킬도 크리급 킥(07-04) — 한 스윙 다수 처치 = '쾅'. 글라이드는 커밋 크리만(돌파 표식 의미 보존).
            bool multiKill = multiKillFreezeCount > 0 && _lastSwingKills >= multiKillFreezeCount;
            float kick = (_lastSwingCrit || multiKill) ? critKick : (finisher ? finisherKick : comboKick);
            FireHitFeedback(kick, finisher || _lastSwingCrit || multiKill, false);   // ★평타(크리/멀티킬 포함)=줌펀치 제외(유저 07-04). 카메라 킥/스냅은 유지.
            // ★히트 글라이드 — 맞혔을 때만 짧은 추가 전진("빗나가면 짧게, 맞히면 길게"). 헛스윙은 클립 런지만.
            //   ★커밋 크리티컬이면 긴 글라이드(돌파 표식 — 읽고 베면 실제로 뚫린다): 갈린 틈에 몸이 들어가는 물리 보상.
            if (hitGlideEnabled)
            {
                float glide = (_lastSwingCrit && commitGlideDistance > 0f) ? commitGlideDistance : hitGlideDistance;
                Motor()?.AddGlide(_aimDir, glide, hitGlideDuration);
            }
        }
    }

    /// <summary>★액션(스킬/반격/대시베기) 타격 공통 — 구 DoCounterHit/DoDashAttackHit/DoSkillHit의 일반화(동작 보존:
    /// 반격/대시베기는 SO의 vfx.prefab/sfx.clip이 비어 무동작 = 기존과 동일). 판정 + 적중 피드백 +
    /// ★타격 순간(칼 벨 때) VFX·사운드(적중 불문 — 스윙 비주얼/사운드 규약).</summary>
    void DoActionHit(ActionRuntime rt)
    {
        var h = rt.set.hit;
        bool connected = DoHit(EffectiveRange(h, rt.set.vfx),
                               h.arcHalfAngle > 0f ? h.arcHalfAngle : 60f,   // 0이면 부채꼴이 모든 적을 배제(M-2)
                               Mathf.Max(0f, h.forwardOffset),
                               h.damage > 0 ? h.damage : 1,
                               h.knockback);                                 // 넉백 0은 유효(무넉백) — 가드 강제 안 함
        if (connected) FireHitFeedback(finisherKick, true, true);
        WeaponVfxSpawner.Spawn(rt.set.vfx, Owner, weaponAnchor, _aimDir);   // 스윙 비주얼(적중 불문, prefab 비면 무동작)
        PlayActionSfx(rt.set.sfx);                                          // 스윙 사운드(적중 불문, clip 비면 무음)
        rt.hitSeq++;   // ★'벨 때' 신호 — 차징스킬은 ChargePhantomEmitter가 이 증가를 폴링해 슬래시 VFX 1발 발동(윈드업 아닌 실제 베기에).
    }

    /// <summary>타격 손맛 발동 — 칼이 적에 닿았을 때만(헛스윙 제외). 카메라 킥(조준 방향, unscaled·스냅).
    /// ★전역 히트스탑은 의도적으로 안 씀 — 일시정지/슬로모와 timeScale 소유권 충돌(게이트 Codex P0/P1, 캐넌 "전역 timeScale 금지").</summary>
    void FireHitFeedback(float camKick, bool finisher, bool zoomPunch)
    {
        PlayMeleeImpact();   // ★임팩트 thud — FireHitFeedback은 connected에서만 호출되므로 헛스윙 자동 무음. 카메라 유무와 독립(맨 앞).
        var cam = PlayerCameraFollow.Active;
        if (cam == null) return;
        if (camKick > 0f) cam.AddKick(_aimDir, camKick);
        // ★줌펀치(NotifyHit FOV)는 스킬/반격/대시베기에만 — 평타 콤보엔 제외(유저 07-04: 매 평타 줌은 과함, 스펙터클을 피크에 아낀다).
        if (zoomPunch) cam.NotifyHit(finisher);   // 스킬 피니셔=큰 줌펀치 + 시네마틱 줌인(투-스테이트). 크기는 카메라 소유.
    }

    /// <summary>부채꼴(기본)/절단선(lineCut=가르기)+사거리+LOS 판정으로 IDamageable에 타격. 콤보/반격 공통(파라미터만 다름).
    /// 반환=하나라도 적중했는가(손맛 피드백을 헛스윙엔 안 내기 위함).</summary>
    bool DoHit(float range, float arcHalf, float forwardOffset, int dmgAmt, float kb, bool lineCut = false)
    {
        if (Owner == null) return false;

        // ★히트존 원점을 조준 방향으로 전진 — 보이는 슬래시 위치에 정합(Codex 권고; 발밑 중심 아님).
        Vector3 origin = Owner.position + _aimDir * Mathf.Max(0f, forwardOffset);
        Vector3 eye = origin + Vector3.up * eyeHeight;
        // ★가르기(lineCut)는 선분 대각(끝×폭/2)까지 수집 반경 확장 — 구석 적이 Overlap에서부터 안 빠지게(Codex P2).
        float gather = (lineCut
            ? Mathf.Sqrt(range * range + lineCutWidth * lineCutWidth * 0.25f)
            : range) + 0.5f;
        const float pointBlank = 0.9f;
        // ★절단선 축은 XZ 평면 정규화(Stab L-2) — _aimDir.y 잔존 시 투영(along)이 축소되는 것 방지. 판정 전용.
        Vector3 lineAxis = _aimDir; lineAxis.y = 0f;
        lineAxis = lineAxis.sqrMagnitude > 0.0001f ? lineAxis.normalized : Vector3.forward;

        _hitThisSwing.Clear();
        _critCenters.Clear();
        _lastSwingCrit = false;
        _lastSwingKills = 0;
        int n = Physics.OverlapSphereNonAlloc(origin, gather, _overlap, enemyMask, QueryTriggerInteraction.Collide);
        if (n == _overlap.Length)
            Debug.LogWarning("[KatanaWeapon] OverlapSphere 버퍼(128)가 가득 — 일부 타격 누락 가능(버퍼 증대 검토).");
        for (int i = 0; i < n; i++)
        {
            var dmg = _overlap[i].GetComponentInParent<IDamageable>();
            if (dmg == null || _hitThisSwing.Contains(dmg)) continue;

            // ★표면 거리 판정(P0-1, 관용 감사 G1 — 2026-07-05): 구 피벗(중심) 거리는 덩치 클수록 판정이 상대적으로
            //   짧아지고(Crassorrid r=1.8), 절단선 반폭(0.8m) < 잡몹 캡슐 반경(0.9m)이라 몸 가장자리 스침이 빗나갔다.
            //   콜라이더 표면 최근접점 기준 = 대상 반경이 모든 검사(거리·절단선 폭·각도)에 자동 포함.
            //   (적 허트박스=캡슐 관례라 ClosestPoint 유효 — convex 한정 API. origin이 콜라이더 안이면 origin 반환=밀착 타격.)
            Vector3 to = SurfacePoint(_overlap[i], origin) - origin; to.y = 0f;
            float dist = to.magnitude;
            // ★초근접 구제 우선(P0-2, Codex 게이트 지적): 표면 기준 pointBlank 안이면 모양(절단선/부채꼴)·각도 면제
            //   — 구 순서는 절단선 필터(등 뒤 -0.2m)가 먼저 돌아 밀착한 적이 구제를 못 받았다.
            //   ★표면 기준이라 면제 실효 반경이 덩치만큼 커진다(r1.8 브루트=중심 2.7m) — "몸 표면이 0.9m 안이면
            //   그게 밀착"이 맞는 시맨틱으로 수용(Codex M-1). 단 LOS는 면제 안 함(아래 — 유저 판정 07-05).
            if (dist > pointBlank)
            {
                if (lineCut)
                {
                    // ★가르기 — 절단선 *선분*(-0.2..range) 대 적 *표면*의 최근접 거리가 폭/2 이내면 타격("내가 그은 선").
                    //   ★게이트 수정(Codex HIGH, P0): ClosestPoint(origin)은 '원점 최근접 표면점'이라 폭 판정에 쓰면
                    //   몸이 절단선에 *옆으로* 걸친 큰 적을 놓친다 → 축 위에서 적 중심에 가장 가까운 점(axisPoint)을
                    //   먼저 구하고, *그 점* 기준 표면 거리로 폭을 잰다(수직 캡슐 허트박스엔 수학적으로 정확).
                    //   along 클램프가 선분 경계를 겸해 끝단(range)·등 뒤(-0.2m)에도 폭만큼의 둥근 관용이 생긴다
                    //   (범위 끝 관용 — 관용 감사 카탈로그 정합). 폭=커서 갭 관용(마그네티즘 겸).
                    Vector3 centerTo = _overlap[i].transform.position - origin; centerTo.y = 0f;
                    float along = Mathf.Clamp(Vector3.Dot(centerTo, lineAxis), -0.2f, range);
                    Vector3 axisPoint = origin + lineAxis * along;
                    Vector3 lateral = SurfacePoint(_overlap[i], axisPoint) - axisPoint; lateral.y = 0f;
                    float halfWidth = lineCutWidth * 0.5f;
                    if (lateral.sqrMagnitude > halfWidth * halfWidth) continue;
                }
                else
                {
                    if (dist > range) continue;
                    if (Vector3.Angle(_aimDir, to) > arcHalf) continue;
                }
            }
            // ★LOS는 항상 검사(유저 판정 07-05 — Stab H-1): 초근접 면제는 모양(절단선/부채꼴)·각도까지만, 벽 뒤는 못 벤다
            //   (가르기="내가 그은 선" 정체성 보존). 진짜 밀착이면 사이에 장애물이 물리적으로 못 끼므로 체감 실손 0 —
            //   표면 기준 전환이 만든 '얇은 벽 너머 대형 몸통 자동명중' 케이스만 제거.
            Vector3 los = (_overlap[i].transform.position + Vector3.up * eyeHeight) - eye;
            float ll = los.magnitude;
            if (ll > 0.001f && Physics.Raycast(eye, los / ll, ll, obstacleMask, QueryTriggerInteraction.Ignore))
                continue;
            _hitThisSwing.Add(dmg);

            // ★타이밍-크리티컬: 이 적이 *공격을 커밋 중*이면(IAttackCommit) 크리티컬 — 데미지 배수 + 전파 예약.
            //   커밋 아닌 적(접근·서성)은 일반 타격. "커밋한 놈을 베면 크리티컬"이 공격 타이밍 듀얼의 코어.
            bool crit = false;
            if (timingCritEnabled)
            {
                // ★크리 창: 기본=커밋 전체(critOnWindup — 브루트 윈드업 0.8s가 읽기 창, 07-04 확장).
                //   끄면 기존 맞받음-only(IsStriking = 돌진/물기/내려찍기 발사 순간만).
                var commit = _overlap[i].GetComponentInParent<IAttackCommit>();
                crit = commit != null && (commit.IsStriking || (critOnWindup && commit.IsCommittingAttack));
            }
            int outDmg = crit ? Mathf.Max(1, Mathf.RoundToInt(dmgAmt * critDamageMult)) : dmgAmt;
            if (crit)
            {
                // ★Codex#3/M-5: TakeHit(사망 가능) *전에* 시안 플래시 발동 — 비치사 크리티컬서 확실히 보이게.
                //   같은 컴포넌트가 IDamageable+ICritReact라 GetComponentInParent 중복 없이 dmg 캐스트로 해결.
                (dmg as ICritReact)?.OnCritHit();
                _critCenters.Add(_overlap[i].transform.position);
            }
            // ★멀티킬 카운트 — TakeHit 전후 생사 비교(receiver 캐스트, 다른 IDamageable 타입은 카운트 제외).
            var edr = dmg as EnemyDamageReceiver;
            bool wasAlive = edr != null && !edr.IsDead;
            dmg.TakeHit(outDmg, origin, kb);
            if (wasAlive && edr.IsDead) _lastSwingKills++;
        }

        // ★크리티컬 전파 — 루프 종료 후(_hitThisSwing 완성·_overlap 자유) 별 버퍼로. 크리티컬마다 주변 호드를 가르는 복도.
        if (_critCenters.Count > 0)
        {
            _lastSwingCrit = true;
            ClashFx()?.Clash();   // ★클래시 프리즈("탁") — 내부 쿨다운으로 호드 스터터 방지. 스윙당 1회.
            if (critPropagationRadius > 0f && critPropagationDamage > 0)
                for (int c = 0; c < _critCenters.Count; c++) Propagate(_critCenters[c], kb);
        }
        // ★멀티킬 프리즈(07-04 무쌍 '쓸려나가는 질량감') — 크리와 동일 소유 경로(ClashFx=ParrySlowMotion, 내부 쿨다운).
        //   전파 킬은 Propagate 내부라 이 카운트에 안 잡힘(직접 벤 것만 — '내가 그은 선'의 성과).
        else if (multiKillFreezeCount > 0 && _lastSwingKills >= multiKillFreezeCount)
            ClashFx()?.Clash();
        return _hitThisSwing.Count > 0;
    }

    /// <summary>크리티컬 전파 — center 주변 critPropagationRadius 내 적에게 작은 피해. 이미 이 스윙에 맞은 적은 제외.
    /// 밀집 호드를 가르는 '복도' 연출 = 크리티컬 보상의 핵심(데미지 증가가 아니라 공간 상태 전복).
    /// ★별 버퍼(_propOverlap) 사용 — DoHit이 순회 중인 _overlap을 덮어쓰지 않게.</summary>
    /// <summary>클래시 프리즈 발동원(플레이어 ParrySlowMotion) — 지연 1회 해석 후 캐시. 없으면 null(무음 폴백).</summary>
    ParrySlowMotion ClashFx()
    {
        if (!_clashFxResolved)
        {
            _clashFxResolved = true;
            if (Owner != null)
                _clashFx = Owner.GetComponentInChildren<ParrySlowMotion>(true) ?? Owner.GetComponentInParent<ParrySlowMotion>();
        }
        return _clashFx;
    }

    /// <summary>히트 글라이드 대상(PlayerMotor) — 지연 1회 해석 후 캐시(ClashFx와 동형). 없으면 null(글라이드 무동작).</summary>
    PlayerMotor Motor()
    {
        if (!_motorResolved)
        {
            _motorResolved = true;
            if (Owner != null)
                _motor = Owner.GetComponent<PlayerMotor>() ?? Owner.GetComponentInParent<PlayerMotor>();
        }
        return _motor;
    }

    void Propagate(Vector3 center, float kb)
    {
        int n = Physics.OverlapSphereNonAlloc(center, critPropagationRadius, _propOverlap, enemyMask, QueryTriggerInteraction.Collide);
        if (n == _propOverlap.Length)
            Debug.LogWarning("[KatanaWeapon] 전파 버퍼(128) 포화 — 일부 전파 피해 누락 가능(버퍼 증대 검토).");
        for (int i = 0; i < n; i++)
        {
            var d = _propOverlap[i].GetComponentInParent<IDamageable>();
            if (d == null || _hitThisSwing.Contains(d)) continue;   // 직접 타격·다른 전파 센터와 중복 차단
            _hitThisSwing.Add(d);
            d.TakeHit(critPropagationDamage, center, kb * 0.5f);
        }
    }
}
