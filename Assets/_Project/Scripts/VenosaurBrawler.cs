// Venosaur 근접 격투 드라이버 — ★"묵직한 브루저 클로월"(2026-06-14): Dimaxillosaurus 클로월 틀의 *직접 재활용*이되 무게 노브만 더 무겁게.
//   멀리서 발견 → 그 자리에서 포효(오프너 1회) → 좌/우 단발 클로 L→R→L→R 무한 교대가 곧 이동수단(별도 달리기/걷기 접근 없음).
//   클로 클립 루트모션(실측 L 2.413m / R 4.094m 전진)이 몬스터를 플레이어 쪽으로 데려간다. 디스인게이지 없음 — 끊임없는 벽.
//   ★Dimax(슬렌더 리치, 빠른 "휘릭")과의 차이 = 무게. Venosaur = 묵직 헌치드 수각 → 이즈 램프를 *전체적으로 느리게*(둔중 cocking + 박힌 히트
//    + 무거운 팔로스루). 단 굼뜨지 않게(북극성 #2 속도감) — "느림"이 아니라 "무게"로 읽히게 deliberate windup + planted strike.
//
// ★Caniathrox 추격(돌진/도약)·Venodonte 사수(원거리)·Dimaxillosaurus 슬렌더 클로(빠른 휘릭)에 이은 **네 번째 활용 = "묵직 브루저 클로월"**.
//   (틀 자체는 Dimax와 동일 = 클로월 상태 시퀀스. 신규는 클립 교체 + 무게 노브 + ★L/R 전진 비대칭 처리.)
//
// ════════ 헌법 (불가침 — 애니 에이전트 3원칙) ════════
//   제0원칙: 정체성 동작(단발 클로) 재생 중엔 그 클립만 돈다. crossfade로 뭉개지 않는다.
//            → 상태머신(VenosaurBrawler.controller)이 강제(정체성 전이=CUT dur0). 단발 1회 = 완결 후 다음 손.
//            ★단발은 Windup(0~9)+Strike(9~15)+FollowOut(15~21)+Recovery(21~30) 네 구간으로 분할(30프레임 기준 — Dimax는 35였음, 실측 재유도).
//             같은 take 4분할이라 경계 포즈 비트-동일(연속). CUT여도 포즈 점프 0 = crossfade 아님(헌법 준수).
//   제1원칙: 공격은 상태 시퀀스다.
//            오프너:  Idle →(타깃 인지 즉시)→ Roar(앵티시페이션·1회) → L_Windup(첫 단발 진입).
//            단발 시퀀스: L_Windup →[CUT 연속]→ L_Strike →[CUT]→ L_FollowOut →[CUT]→ L_Recovery
//                    →[CUT, Idle 우회]→ R_Windup →…→ L_Windup (Roar·Idle 둘 다 생략, *무봉제* 무한 교대).
//                    각 단발 = 4구간(무게 이즈 램프), 순차·완결. 디스인게이지·접근 상태 없음 — 클로질이 전진.
//   제2원칙: 애니메이션이 진실. 전진은 단발 클립 루트모션이 만든다(applyRootMotion=true, 코드 포물선/위치이동 금지).
//            ★★L/R 전진 비대칭(실측 L 2.413m vs R 4.094m): 이건 *클립 저작 차이*(R이 더 큰 런지) — 애니가 진실(제2원칙).
//             기본은 *비대칭 보존*(둔중 브루저의 불균등 보폭 = 더 살아있음, 북극성 #1). 보존이 "절뚝"으로 읽히면 유저 ▶ 판정에 따라
//             per-hand gain으로 균등화(증폭이지 발명 아님 — Dimax AdvanceGain 메커니즘). 기본 gain 1.0 = 비대칭 그대로.
//            ★스윙 속도는 구간별 정적 speed의 *무게 이즈 램프*(per-frame 코드 speed 곡선 ❌ — 헌법 위반).
//             Windup 1.1(둔중 cocking — 보이는 윈드업=위협 텔레그래프) → Strike 1.0(박힌 풀웨이트 컨택) → FollowOut 1.6(무거운 팔로스루)
//             → Recovery 1.9(중립 복귀, Dimax 2.5보다 느림). 전 구간 Dimax보다 느림 = 무게. ★히트 모먼트는 Strike 구간 AnimationEvent(ClawHit).
//
//   ★★회전 경계(헌법 — Dimax v8 개정 계승, 유저 승인): 추적(재조준)을 *각 클로의 Windup*에 접는다.
//            회전 O = Roar(오프너 조준) · Idle(미교전 대기) · Windup(cocking 구간 frame 0~9, 아직 내지르기 전).
//            회전 0 = Strike · FollowOut · Recovery (commit~회수 frame 9~30). 일단 내지르기 시작하면 방향 고정 — 궤적 보존.
using System.Collections.Generic;
using UnityEngine;

public class VenosaurBrawler : MonoBehaviour
{
    static readonly List<VenosaurBrawler> Roster = new List<VenosaurBrawler>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStaticState() => Roster.Clear();

    [Header("참조 (스포너가 와이어링)")]
    public Transform model;
    public Animator modelAnimator;
    public Transform target;
    public RuntimeAnimatorController attackController;
    [Tooltip("공유 공격 토큰 풀 — 동시 교전 수 제한용 시스템(보존). ★클로월에선 비게이팅(획득 실패해도 전진은 멈추지 않음 — 모두가 벽).")]
    public AttackTokenPool tokenPool;

    [Header("★클로 컨택 임팩트 VFX (ClawHit AnimationEvent 훅)")]
    [Tooltip("SmashImpactPool 공유 풀 — 스포너가 생성·주입. null이면 임팩트 VFX 생략(모션은 그대로).")]
    public SmashImpactPool clawImpactPool;

    [Header("★시그니처 오라 (MonsterSignatureAura — 스포너가 주입)")]
    [Tooltip("부위 발광 컴포넌트. ClawHit 시 PulseAttack() 호출. null이면 오라 펄스 생략.")]
    public MonsterSignatureAura signatureAura;

    [Header("★컨택 임팩트 노브 — 유저 판정 대기 (미검증)")]
    [Tooltip("임팩트 VFX 켜기/끄기(디버그).")]
    [SerializeField] bool clawImpactEnabled = true;
    [Tooltip("충격파 HDR 색. 레드오렌지(색 캐넌 §5 — 적 위협). ★슬램(1f,0.32f,0.10f)보다 채도를 눌러 절제 — 클로는 슬램보다 작아야 한다.")]
    [SerializeField] Color clawShockColor = new Color(1f, 0.28f, 0.08f, 1f);
    [Tooltip("충격파 밝기(가산). ★슬램(1.8) 절반 수준(0.9) — 맞는 게 보상처럼 보이면 안 됨. 뽕 경제 보호.")]
    [SerializeField, Range(0f, 4f)] float clawIntensity = 0.9f;
    [Tooltip("중심 섬광 강도. 0=없음, 1=밝은 핵. ★스냅 정점을 순간 강조 — 0.55면 짧게 번쩍이고 사라짐.")]
    [SerializeField, Range(0f, 1f)] float clawCoreFlash = 0.55f;
    [Tooltip("링 폭(0~1). ★좁을수록 날카롭고 넓을수록 두툼. 클로라 좁게(0.28 — 슬램 0.35보다 날카로운 선).")]
    [SerializeField, Range(0.05f, 0.5f)] float clawRingWidth = 0.28f;
    [Tooltip("충격파 반경(m). ★Venosaur 클로 리치 고려 — 너무 크면 슬램과 혼동. 1.0~1.4m 사이(슬램 r3 대비 1/3 이하).")]
    [SerializeField, Range(0.4f, 2.5f)] float clawRadius = 1.2f;
    [Tooltip("충격파 수명(초). ★빠르게 명멸 — 0.12~0.18s. 슬램(0.35)보다 훨씬 짧게.")]
    [SerializeField, Range(0.05f, 0.4f)] float clawShockDuration = 0.14f;
    [Tooltip("그을림 잔흔 수명(초). ★클로는 잔흔 거의 없음 — 0.1s면 충분(슬램 1.2 대비 매우 짧음).")]
    [SerializeField, Range(0.05f, 0.6f)] float clawScorchDuration = 0.10f;
    [Tooltip("먼지 파티클 스케일 배율. ★슬램(1.0) 대비 절반(0.45) — 작고 빠른 클로 접촉.")]
    [SerializeField, Range(0.1f, 2f)] float clawDustScale = 0.45f;
    [Tooltip("그을림 색(알파 포함). ★슬램과 동일(어두운 잔흔), 크기만 작아 거슬리지 않음.")]
    [SerializeField] Color clawScorchColor = new Color(0.12f, 0.08f, 0.05f, 0.6f);
    [Tooltip("클로 손 끝 본 이름 — Transform.Find로 재귀 검색. 미발견 시 model.forward×clawReach 폴백.")]
    [SerializeField] string leftHandBoneName  = "Venosaur_ L Hand";    // ★Protofactor Venosaur 리그 실제 본 이름(프리팹 YAML 실측, 공백 포함). 다른 종 재활용 시 그 종 본 이름으로 교체.
    [SerializeField] string rightHandBoneName = "Venosaur_ R Hand";
    [Tooltip("손 본 미발견 시 클로 컨택 추정 거리(m) — model.position + model.forward × clawReach.")]
    [SerializeField, Range(0.5f, 4f)] float clawReach = 2.0f;

    // 런타임 — 손 본 캐시(첫 ClawHit 시 1회 탐색)
    Transform _leftHandBone;
    Transform _rightHandBone;
    bool _bonesSearched;

    [Header("조향/회전 (★추적 노브 — 유저 ▶ 판정)")]
    [Tooltip("★추적(재조준) 회전 속도(도/초). 회전 O = Roar·Idle(미교전)·Windup(각 클로 cocking 구간 frame 0~9). Strike/FollowOut/Recovery 발동 중엔 0(궤적 보존).\n★묵직 브루저는 Windup이 길어(둔중 cocking) 추적창이 Dimax보다 넓다 — Windup ~0.273s(9f/30fps÷1.1배속) × turnSpeed. 너무 잘 따라와 회피 불가하면 ↓.")]
    [SerializeField] float turnSpeed = 300f;   // ★Dimax 360보다 낮게(둔중 브루저 = 굼뜬 조향, 사이드스텝에 약함 = 단독 약점을 회전으로도 표현)

    [Header("Separation — 동료 회피 (군중 AI, 벽 간격 유지)")]
    // ★★v2 재튜닝(2026-06-14, 유저 ▶ "몬스터끼리 겹친다"): 덩치 큰 브루저라 더 벌어져야 함. 반경↑·가중치↑.
    //   ★단 전진은 안 막는다(벽 유지) — separation은 FaceTarget에서 *heading 0.4 가중치로만* 섞임(조향만 살짝 휨, 루트모션 전진은 항상 계속).
    //    따라서 강한 separation = 벽이 *옆으로 퍼질* 뿐 멈추거나 백오프하지 않음(멀뚱 방지). 겹침만 풀고 전진은 보존.
    [SerializeField] float separationRadius = 3.6f;   // ★v1 2.6→3.6(Venosaur 덩치 큼 — 겹침 방지. 한 스윙 R 6.96m라 넓게 벌어져도 벽 유지)
    [Range(0f, 3f)][SerializeField] float separationWeight = 1.6f;   // ★v1 1.0→1.6(밀어내는 힘 강화 — 겹침 박멸)

    // 애니 파라미터 — VenosaurBrawler.controller와 공유.
    static readonly int PAttack = Animator.StringToHash("attack");     // 오프너: Idle→Roar (타깃 인지 1회)
    static readonly int PChainL = Animator.StringToHash("chainL");     // 좌 단발 체인 — R_Recovery→L_Windup 직행(주) / Idle→L_Windup(폴백).
    static readonly int PChainR = Animator.StringToHash("chainR");     // 우 단발 체인 — L_Recovery→R_Windup 직행(주) / Idle→R_Windup(폴백).
    static readonly int SIdle   = Animator.StringToHash("Idle");
    static readonly int SRoar   = Animator.StringToHash("Roar");
    // ★★스윙 = 무게 이즈 4구간 분할(30프레임 기준 — Dimax 35에서 실측 재유도). 각 단발 = Windup→Strike→FollowOut→Recovery.
    static readonly int SLWindup   = Animator.StringToHash("LeftClaw_Windup");
    static readonly int SLStrike   = Animator.StringToHash("LeftClaw_Strike");
    static readonly int SLFollow   = Animator.StringToHash("LeftClaw_FollowOut");
    static readonly int SLRecov    = Animator.StringToHash("LeftClaw_Recovery");
    static readonly int SRWindup   = Animator.StringToHash("RightClaw_Windup");
    static readonly int SRStrike   = Animator.StringToHash("RightClaw_Strike");
    static readonly int SRFollow   = Animator.StringToHash("RightClaw_FollowOut");
    static readonly int SRRecov    = Animator.StringToHash("RightClaw_Recovery");

    // ════════ ★★무게 이즈 램프 = 둔중 브루저 + ★강약 대비(위협감) (SSOT — 빌드스크립트가 state.speed로 참조) ════════
    //   ★★v2 재튜닝(2026-06-14, 유저 ▶ "공격 강약 부족 = 위협적이지 않다"): 위협감(북극성 #3)은 *강약 대비*에서 온다.
    //     v1(1.1/1.0/1.6/1.9)은 Windup·Strike가 거의 같은 속도(1.1×≈1.0×) = 스냅이 없는 밋밋한 공격. → 대비를 키운다.
    //   ★대비 설계 = 느리게 응축(텔레그래프 보임) → 확 내지르는 스냅(박힘) → 무겁게 밀고나감(carry) → 묵직 settle:
    //     Windup 0.70(★더 느린 응축 — cocking 길어져 예고가 더 보임=위협 빌드업, 플레이어수용성: 피할 시간을 줌)
    //     → Strike 2.4(★확 박히는 스냅 — Windup 대비 ~3.4배속 = 강약 핵심. 빠른 컨택, ClawHit 여기)
    //     → FollowOut 1.3(무거운 팔로스루 — 스냅 직후 무게가 밀고 나감. 빠른 스냅과 대비되는 묵직 carry)
    //     → Recovery 1.7(묵직 중립 복귀).
    //   ★Windup/Strike 속도 대비: v1 1.10× → v2 0.29×(=Strike가 Windup의 3.4배). 이 *대비*가 위협의 본질.
    //   ★헌법: per-frame 코드 speed 곡선 ❌ — 강약은 *구간별 정적 state.speed의 램프 모양*으로만 만든다(이 4개 const).
    //   ★ClawHit은 Strike 클립 norm 0.5 고정(클립 정규화) — Strike가 빨라져도 같은 포즈에서 컨택, 이벤트 재타이밍 불필요.
    //   ★유저 ▶ 판정: ①스냅이 충분히 박히나(위협적) ②Windup이 너무 길어 굼뜨나(북극성 #2) → Windup↑.
    public const float WindupSpeed    = 0.70f;  // frame 0~9   — ★느린 응축(coiling). 긴 텔레그래프=위협 빌드업+플레이어 반응창. v1 1.1.
    public const float StrikeSpeed    = 2.4f;   // frame 9~15  — ★확 내지르는 스냅(Windup 3.4배속). ★ClawHit 여기(norm0.5 고정). v1 1.0.
    public const float FollowSpeed    = 1.3f;   // frame 15~21 — 무거운 팔로스루. 스냅 직후 무게가 밀고 나감(빠른 스냅과 대비). v1 1.6.
    public const float RecoverySpeed  = 1.7f;   // frame 21~30 — 묵직 중립 복귀. v1 1.9.

    // ════════ ★전진 증폭 게인 (per-hand) — 유저 승인 헌법 미세 확장 (Dimax AdvanceGain 계승) ════════
    //   ★★Venosaur 고유 문제 = L/R 전진 비대칭(실측 L 2.413m vs R 4.094m, R이 ~70% 더 큰 런지). Dimax는 L/R 대칭(둘 다 2.22m)이라 단일 게인이었음.
    //   기본 정책 = *비대칭 보존*(둘 다 1.0): 둔중 브루저의 불균등 보폭은 *더 살아있음*(북극성 #1 애니가 주인). 클립이 진실.
    //   ★단 보존이 "절뚝(limp)"으로 읽히면(유저 ▶ 판정) per-hand gain으로 균등화 — 예: 약한 쪽(L) gain↑로 R에 맞추거나, 강한 쪽(R) gain↓로 L에 맞춤.
    //    이것도 *증폭이지 발명 아님*(클립 자신의 deltaPosition × gain, 방향·궤적·타이밍은 클립이 진실). 헌법 정신 보존.
    //   ★튜닝 가이드: 균등화하려면 LeftAdvanceGain = R총전진/L총전진 ≈ 4.094/2.413 ≈ 1.697(L을 R에 맞춤) 또는 RightAdvanceGain = 2.413/4.094 ≈ 0.589(R을 L로 낮춤).
    //    기본은 둘 다 1.0(비대칭 그대로) — 유저가 플레이로 "절뚝 거슬림" 판정하면 그때 조정.
    //   ★★v2 재튜닝(2026-06-14, 유저 ▶ "너무 느리다 — 벽으로 안 느껴진다, 최소 플레이어 걷기 이상"): 지속 전진을 플레이어 걷기(5.5 m/s) *이상*으로.
    //     v1은 gain 1.0 = 지속 ~3.97 m/s < 걷기 5.5 → 걸어서 빠짐(벽 아님). v2는 둘 다 1.5로 올려 *지속 전진을 걷기 위로* 끌어올림.
    //     ★L/R 비대칭은 보존(둘 다 *같은 배율* 1.5 → R이 여전히 70% 더 큰 런지, 불균등 보폭=살아있음). 균등화가 아니라 *동등 증폭*.
    //     ★헤드리스 실측(Animator 스텝, NEW 램프): 순수전진 지속 = 4.906 m/s × gain. gain 1.5 → 7.36 m/s(순수), 랩 효율 ~0.92 → ~6.8 m/s 예상.
    //       → 걷기 5.5 *위*(벽: 걸으면 못 빠짐) · 질주 9.0 *아래*(질주하면 ~2.2 m/s 떨굼 = 탈출 밸브 보존). R-swing ~6.1m(강한 런지, 비텔레포트).
    //     ★증폭이지 발명 아님(클립 deltaPosition × gain, 방향·궤적·타이밍은 클립이 진실 — 헌법 제2원칙). Dimax AdvanceGain 1.3 메커니즘과 동일.
    //   ★유저 ▶ 판정: ①벽으로 다가오나(걷기로 못 빠지나) — 그래도 굼뜨면 ↑(1.7=순수8.3/랩~7.7, 더 집요). ②질주로도 못 빠지면 ↓(단 걷기 5.5 이상 유지).
    //     튜닝 공식: 목표 랩속도 ≈ 4.906 × gain × 0.92 → gain ≈ 목표 / 4.51.
    public const float LeftAdvanceGain  = 1.5f;   // ★좌 단발 전진 게인(v1 1.0→v2 1.5 = 클립 2.413m × 1.5 ≈ 3.62m/swing). 비대칭 보존(R과 같은 배율).
    public const float RightAdvanceGain = 1.5f;   // ★우 단발 전진 게인(v1 1.0→v2 1.5 = 클립 4.094m × 1.5 ≈ 6.14m/swing). 비대칭 보존(L과 같은 배율).

    bool _holdsToken;       // ★토큰 보유(비게이팅 — 슬롯 못 잡아도 전진은 계속). 수명 = 교전 수명(오프너~OnDisable).
    bool _engaged;          // ★공격 체인 활성(Roar 오프너 이미 마침). true면 Idle에서 Roar 생략하고 단발 직행.
    bool _windupSetup;      // ★현재 Windup 진입에 대해 1회 셋업 끝났나(엣지 가드). Windup이 아닌 상태에선 false로 리셋.
    bool _recovChained;     // ★현재 Recovery 진입에 대해 다음손 chain trigger 이미 쐈나(엣지 가드).
    bool _firedThisIdle;    // ★이번 Idle 체류 중 trigger 이미 쐈나 — 전이 1프레임 지연 동안 중복/오발 방지.
    bool _nextRight;        // ★좌우 교대 — 다음 단발이 우손인가. 오프너(Roar→L_Windup) 후 true(다음은 R). Windup 진입마다 토글.

    void Awake()
    {
        if (modelAnimator == null) { Debug.LogError("[VenosaurBrawler] modelAnimator 미할당"); enabled = false; return; }
        // ★양산 안전망: OnAnimatorMove는 Animator와 *같은 GO*의 컴포넌트에서만 발화한다. 드라이버가 Animator와 다른 GO에 붙으면
        //   콜백이 안 불려 전진이 *에러 없이* 0이 된다(silent — "제자리 클로질"로만 보임). 클로월 틀을 다른 종 프리팹으로 재활용할 때 함정.
        if (modelAnimator.gameObject != gameObject)
            Debug.LogError($"[VenosaurBrawler] modelAnimator가 드라이버와 다른 GameObject('{modelAnimator.gameObject.name}')에 있습니다 — OnAnimatorMove 미발화로 전진이 0이 됩니다. 드라이버를 Animator와 같은 루트 GO에 붙이세요.");
        if (attackController != null) modelAnimator.runtimeAnimatorController = attackController;
        else Debug.LogError("[VenosaurBrawler] attackController 미할당");
        // ★루트모션이 전진을 만든다(제2원칙). applyRootMotion=true = deltaPosition/deltaRotation을 채워둔다 —
        //   OnAnimatorMove를 구현했으므로 자동 적용은 꺼지고 콜백에서 수동 적용한다(전진만 per-hand 게인 증폭).
        modelAnimator.applyRootMotion = true;
        _engaged = false;
    }

    void OnEnable()  { if (!Roster.Contains(this)) Roster.Add(this); }
    void OnDisable()
    {
        Roster.Remove(this);
        ReleaseToken();   // ★토큰 반납 + ResetCombatState(체인 플래그·trigger 일괄 — disable/re-enable 소프트락 차단)
    }

    void ReleaseToken()
    {
        if (_holdsToken && tokenPool != null) tokenPool.Release();
        _holdsToken = false;
        ResetCombatState();
    }

    void ResetCombatState()
    {
        _engaged = false;
        _windupSetup = false;
        _recovChained = false;
        _firedThisIdle = false;
        _nextRight = false;   // 다음 교전은 오프너(Roar)→L_Windup부터, 그 다음이 R.
        _bonesSearched = false;   // ★본 캐시 무효화 — 재활성/모델 교체 시 다음 ClawHit에서 재탐색(stale Transform 참조 차단).
        if (modelAnimator != null)
        {
            modelAnimator.ResetTrigger(PAttack);
            modelAnimator.ResetTrigger(PChainL);
            modelAnimator.ResetTrigger(PChainR);
        }
    }

    Vector3 SeparationDir()
    {
        Vector3 me = model.position; me.y = 0f;
        Vector3 sep = Vector3.zero;
        if (separationWeight <= 0f || separationRadius <= 0f) return sep;
        float r2 = separationRadius * separationRadius;
        for (int i = 0; i < Roster.Count; i++)
        {
            var o = Roster[i];
            if (o == null || o == this || o.model == null) continue;
            Vector3 away = me - new Vector3(o.model.position.x, 0f, o.model.position.z);
            float d2 = away.sqrMagnitude;
            if (d2 > 0.0001f && d2 < r2)
                sep += away.normalized * (1f - Mathf.Sqrt(d2) / separationRadius);
        }
        return sep * separationWeight;
    }

    // 플레이어를 향해 yaw 회전(Roar/Idle/Windup 재조준 전용 — Strike/FollowOut/Recovery 발동 중엔 호출 안 함).
    void FaceTarget()
    {
        Vector3 toT = target.position - model.position; toT.y = 0f;
        if (toT.sqrMagnitude < 0.0001f) return;
        Vector3 dir = (toT.normalized + SeparationDir() * 0.4f); dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = toT.normalized;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        model.rotation = Quaternion.RotateTowards(model.rotation, want, turnSpeed * Time.deltaTime);
    }

    void Update()
    {
        if (modelAnimator == null) { ReleaseToken(); return; }
        modelAnimator.speed = 1f;   // 매 프레임 리셋(상태.speed는 컨트롤러가 들고 있음 — 클로/Roar 배속). 코드 매프레임 스크럽 금지.
        if (target == null || model == null) { ReleaseToken(); return; }

        var info = modelAnimator.GetCurrentAnimatorStateInfo(0);
        int s = info.shortNameHash;

        bool inWindup = (s == SLWindup || s == SRWindup);
        bool inRecov  = (s == SLRecov  || s == SRRecov);
        if (!inWindup) _windupSetup = false;
        if (!inRecov)  _recovChained = false;

        // ── Idle: 미교전 결정 허브(오프너). 끊임없는 좌우 후 교전 중엔 사실상 안 들름(Recovery→반대 Windup 직행). ──
        if (s == SIdle)
        {
            FaceTarget();            // ★Idle 재조준 = 회전 O(발동 중이 아님).

            if (!_engaged)
            {
                if (!_firedThisIdle)
                {
                    if (!_holdsToken && tokenPool != null && tokenPool.TryAcquire()) _holdsToken = true;
                    modelAnimator.SetTrigger(PAttack);
                    _firedThisIdle = true;
                }
            }
            else if (!_firedThisIdle)
            {
                // ★디제너릿 재개: 교전 중인데 Idle로 떨어졌다(폴백) → 즉시 다음 손으로 벽 재개.
                modelAnimator.SetTrigger(_nextRight ? PChainR : PChainL);
                _firedThisIdle = true;
            }
        }
        // ── Roar(앵티시페이션·위협 텔레그래프): 플레이어 향해 회전 O. ExitTime에 자동 L_Windup. ──
        else if (s == SRoar)
        {
            _firedThisIdle = false;
            modelAnimator.ResetTrigger(PChainL);
            modelAnimator.ResetTrigger(PChainR);
            FaceTarget();
        }
        // ── Windup(단발 첫 구간, frame 0~9 = cocking): ★회전 O(추적을 여기 접음). ──
        else if (inWindup)
        {
            if (!_windupSetup)
            {
                _windupSetup = true;
                _firedThisIdle = false;
                modelAnimator.ResetTrigger(PAttack);
                modelAnimator.ResetTrigger(PChainL);
                modelAnimator.ResetTrigger(PChainR);
                _engaged = true;
                _nextRight = (s == SLWindup);   // 방금 진입한 손의 반대를 다음 손으로 예약.
            }
            FaceTarget();   // ★cocking 중 추적 = 회전만(위치는 루트모션). 내지르는 Strike~Recovery 궤적은 안 휨.
        }
        // ── Strike / FollowOut(컨택~후기 팔로스루, frame 9~21): ★회전 0(commit됨 — 궤적 보존). ──
        else if (s == SLStrike || s == SRStrike || s == SLFollow || s == SRFollow)
        {
            // 회전·이동·trigger 없음 — 내지른 타격이 무게 램프로 재생될 뿐. Strike에만 ClawHit(컨택) 이벤트.
        }
        // ── Recovery(마지막 구간, frame 21~30 = 중립 복귀): ★회전 0. ★진입 1회 다음손 chain trigger(Idle 우회 직행). ──
        else if (inRecov)
        {
            if (!_recovChained)
            {
                _recovChained = true;
                modelAnimator.SetTrigger(_nextRight ? PChainR : PChainL);
            }
        }
    }

    // ════════ ★루트모션 수동 적용 — per-hand 전진 증폭(OnAnimatorMove) ════════
    //   applyRootMotion=true → Unity 자동 적용이 이 콜백으로 위임됨 → 여기서 수동 적용(클로 구간만 per-hand 게인).
    //   ★발화 보장: Animator가 붙은 GameObject(=프리팹 루트, 검증됨)에 드라이버가 AddComponent → 같은 GO → 발화 보장.
    //   ★헌법: deltaPosition은 *클립 자신의* 전진 델타 — 게인은 *크기*만 키운다(방향·궤적·타이밍은 클립이 진실).
    //   ★★per-hand: L/R 전진 비대칭(L 2.413m vs R 4.094m)이라 게인이 손마다 다를 수 있음(균등화 옵션). 기본은 둘 다 1.0 = 비대칭 보존.
    //   ★게인은 *클로 구간(8상태)에만* — Idle/Roar는 제자리 상태라 게인 1×(평시 드리프트 비증폭).
    void OnAnimatorMove()
    {
        if (modelAnimator == null || model == null) return;
        int s = modelAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        bool inLeft  = s == SLWindup || s == SLStrike || s == SLFollow || s == SLRecov;
        bool inRight = s == SRWindup || s == SRStrike || s == SRFollow || s == SRRecov;
        float gain = inLeft ? LeftAdvanceGain : (inRight ? RightAdvanceGain : 1f);   // ★클로 구간만 per-hand 게인 / 그 외 1×.
        model.position += modelAnimator.deltaPosition * gain;   // ★전진(클립 전진 델타 × per-hand 게인) — 크기만 스케일.
        model.rotation *= modelAnimator.deltaRotation;          // 회전은 그대로(클립 회전 0 → ≈identity). 조향=FaceTarget.
    }

    // ════════ ★AnimationEvent 콜백 — 단발 Left/RightClawsAttackForward_RM 클립의 컨택 프레임(Strike 구간)에서 호출 ════════
    /// <summary>각 클로 컨택 정점 프레임 → 임팩트 VFX 스폰 + 오라 펄스 + 향후 데미지 훅.
    /// ev.stringParameter: "L" = 좌손, "R" = 우손 (AnimationEvent에서 지정 — 지정 없으면 현재 교대 상태로 판단).
    /// ★Animation 헌법 준수: 타이밍은 AnimationEvent가 진실 — 코드가 임의 타이밍 발명하지 않음.</summary>
    public void ClawHit(AnimationEvent ev)
    {
        // ★닿는 순간(클립 컨택 정점 frame 12). 데미지/히트박스는 게임플레이 단계에서 이 이벤트에 훅(애니=타이밍의 진실).
        FireClawImpact(ev);

        // ★시그니처 오라 공격 펄스 — 변이 핵심부가 컨택 정점에서 확 터짐.
        //   null이면 생략(오라 없는 씬/테스트 환경 하위호환).
        signatureAura?.PulseAttack();
    }

    // ════════ ★컨택 임팩트 VFX — ClawHit에서 호출 (한 줄 스폰 훅) ════════
    //   설계 원칙:
    //   - 신규 셰이더 0 — SmashShock 그대로 재활용, 색·크기·수명만 절제 버전.
    //   - 적 공격 임팩트 = 플레이어 처치 킬버스트보다 *약하게* (뽕 경제 보호 — 맞는 게 보상처럼 보이면 안 됨).
    //   - 위치: 손 본(Hand_L/R) 우선 → 미발견 시 model.forward × clawReach 폴백(실 히트박스 없는 랩 대응).
    void FireClawImpact(AnimationEvent ev)
    {
        if (!clawImpactEnabled || clawImpactPool == null || model == null) return;

        // ── 손 본 캐시 (1회 탐색) ──
        if (!_bonesSearched)
        {
            _bonesSearched = true;
            _leftHandBone  = FindBoneRecursive(model, leftHandBoneName);
            _rightHandBone = FindBoneRecursive(model, rightHandBoneName);
            if (_leftHandBone == null)
                Debug.LogWarning($"[VenosaurBrawler] '{leftHandBoneName}' 본 미발견 — clawReach 폴백 사용. 본 이름을 Inspector에서 확인하세요.");
            if (_rightHandBone == null)
                Debug.LogWarning($"[VenosaurBrawler] '{rightHandBoneName}' 본 미발견 — clawReach 폴백 사용.");
        }

        // ── 손 판별: AnimationEvent.stringParameter "L"/"R" 우선, 없으면 현재 교대 상태로 추론 ──
        //   ★L/R 판별은 VFX 위치 정확도를 위한 것 — 임팩트 자체는 어느 손이든 동일한 처리.
        bool isLeft;
        if (ev != null && !string.IsNullOrEmpty(ev.stringParameter))
            isLeft = ev.stringParameter == "L";
        else
        {
            // ★stringParameter 미지정(현재 클립 이벤트엔 안 박혀 있음) → 현재 재생 중인 Strike 상태를 직접 읽는다(애니가 진실).
            //   ClawHit은 Strike 구간에서만 발화 → shortNameHash가 곧 현재 손. (_nextRight는 *다음* 손 예약값이라 반전 함정 — 쓰면 L/R VFX가 항상 뒤바뀜.)
            int sh = modelAnimator != null ? modelAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash : 0;
            isLeft = (sh == SLStrike);
        }

        // ── 임팩트 위치 계산 ──
        Vector3 origin;
        Transform bone = isLeft ? _leftHandBone : _rightHandBone;
        if (bone != null)
        {
            origin = bone.position;
            origin.y = Mathf.Max(origin.y, 0f);   // 지면 아래 클램프(루트모션 전진 중 y 오차)
        }
        else
        {
            // 폴백: model 전방 clawReach m 지점
            Vector3 fwd = model.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();
            origin = model.position + fwd * clawReach;
            origin.y = 0f;
        }

        // ── VFX 스폰 — VfxDirector 경유(중앙 임팩트 라우팅, culling 미적용 — 임팩트는 culling 없음).
        //   ★H-2: Instance getter는 자동생성 부작용이 있으므로, 폴백 경로에서는 HasInstance/Existing을 써서
        //     "디렉터가 없는 씬에서 폴백이 디렉터를 자동생성하는 레이스"를 차단한다.
        //   디렉터가 존재하고 풀이 등록돼 있으면 RequestImpact 경유 → 없으면 직접 폴백.
        var director = VfxDirector.Existing;   // ★자동생성 없는 접근(null이면 디렉터 미존재).
        bool usedDirector = false;
        if (director != null && clawImpactPool != null)
        {
            var fx = director.RequestImpact(
                origin, clawRadius, clawShockColor, clawIntensity,
                clawCoreFlash, clawRingWidth, clawShockDuration, clawScorchDuration,
                clawDustScale, clawScorchColor);
            usedDirector = fx != null;   // RequestImpact가 null이면 풀 미등록 → 직접 폴백.
        }

        if (!usedDirector && clawImpactPool != null)
        {
            // 디렉터 없는 환경(구 스포너, 테스트씬) 또는 RequestImpact 미등록 풀 → 기존 직접 경로.
            var fx = clawImpactPool.Acquire();
            if (fx != null)
                fx.Play(origin, clawRadius, clawShockColor, clawIntensity,
                        clawCoreFlash, clawRingWidth, clawShockDuration, clawScorchDuration,
                        clawDustScale, clawScorchColor);
        }
    }

    /// <summary>Transform 트리를 재귀 탐색해 이름이 일치하는 자식을 반환. 부분 일치(Contains) 허용.</summary>
    static Transform FindBoneRecursive(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        // 정확히 일치하는 것 우선
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;
        }
        // 부분 일치(예: "mixamorig:LeftHand"에서 "Hand_L" 검색 실패 시 "Hand" 포함 검색)
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name.Contains(name)) return c;
            var found = FindBoneRecursive(c, name);
            if (found != null) return found;
        }
        return null;
    }
}
