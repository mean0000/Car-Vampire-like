// Crassorrid 근접 브루트 드라이버 — ★4번째 몬스터 틀 = "접근형 브루트: 접근→정지→예고원 차오름→내려찍기 광역".
//   Caniathrox(근접 돌진)·Venodonte(원거리 사수)·Dimaxillosaurus(근접 클로월)에 이은 네 번째 틀.
//   ★이 슬라이스의 핵심 = 지어두고 한 번도 안 쓴 ThreatArc 텔레그래프 시스템(TelegraphPad/Pool)의 *첫 소비자*.
//
//   LV4 정예 브루트(7m 거구). 시그니처 = 전방 스매시(●r3 원 장판 · 1.2s 윈드업 · 양팔 들어올려 내려찍기).
//   ★위협감·무게 > 속도감(북극성): 큰 느린 윈드업 + committed 슬램의 *무게*. 굼떠 보이면 실패지만 빠를 필욘 없다.
//
//   행동 사이클(Caniathrox 접근형 패턴 차용 — Dimax 클로월=이동수단과 다름):
//     Idle →(타깃 인지)→ Approach(Run_RM 루트모션 보행 접근, ★Roar 오프너 제거 2026-06-14 = 인지 즉시 직행)
//       →(slamRange 안 도달)→ SmashWindup(양팔 수평 벌림·들어올림 = 탑뷰 가독 + ★장판 차오름)
//       → SmashStrike(내려찍기 commit, ★SmashHit 임팩트 이벤트 = 장판 발동) → SmashRecovery → Approach/Idle.
//
// ════════ 헌법 (불가침 — 애니 에이전트 3원칙) ════════
//   제0원칙: 정체성 동작(스매시) 재생 중엔 그 클립만 돈다. crossfade로 뭉개지 않는다.
//            → 상태머신(CrassorridBrawler.controller)이 강제(정체성 전이=CUT dur0).
//            ★스매시는 Windup→Strike→Recovery 세 구간으로 분할 — *같은 take(SmashAttack_RM)의 분할*이라
//             구간 경계 포즈가 비트-동일(연속). CUT여도 포즈 점프 0 = crossfade 아님(헌법 준수).
//   제1원칙: 공격은 상태 시퀀스다. 접근(루트모션 이동) → 정지(윈드업) → 내려찍기 → 회수 → 복귀. 각 단계 1클립=1상태, 순차·완결.
//   제2원칙: 애니메이션이 진실. 전진은 ★클립 루트모션(Run_RM 접근 / SmashAttack_RM 전진 3.514m)이 만든다
//            (applyRootMotion=true, 코드 포물선/위치이동 금지). 윈드업 속도는 *정적 state.speed*(코드 매프레임 스크럽 ❌).
//
//   ★★회전 경계(헌법): 회전 O = Approach(steering 곡선 추적 + 근접 시 마주보기) · SmashWindup(아직 내지르기 전 = cocking, 조준 허용).
//                       회전 0 = SmashStrike · SmashRecovery (commit~회수 — 내려찍기 궤적·전진 보존).
//            경계 판정질문 = "발동된(commit된) 슬램의 전방 궤적을 코드가 휘나" → Strike 진입 후부터 No.
//            ★Windup은 *내려찍기 직전*(팔 드는 중)이라 여기서 몸을 틀어 플레이어를 조준해도 슬램 궤적은 안 휜다 → 헌법 정신 부합.
//             단 ★장판은 Windup 진입 *시점*의 전방에 고정 스폰(차오르는 동안 위치 안 옮김 = 공정한 약속) — 회전해도 장판은 안 따라 돈다.
using System.Collections.Generic;
using UnityEngine;

public class CrassorridBrawler : MonoBehaviour
{
    static readonly List<CrassorridBrawler> Roster = new List<CrassorridBrawler>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStaticState() => Roster.Clear();

    [Header("참조 (스포너가 와이어링)")]
    public Transform model;
    public Animator modelAnimator;
    public Transform target;
    public RuntimeAnimatorController attackController;
    [Tooltip("(구)공유 공격 토큰 풀 — ★수 게이팅 폐기(2026-06-14, large-enemy-ai-research §C). 이제 동시 슬램은 BruteSlamCoordinator가 *각·박자*로 분산(수 제한 ❌). 필드는 스포너 주입 호환 위해 잔존하나 슬램 결정에 미사용(다른 종이 공유 풀 쓰면 보존).")]
    public AttackTokenPool tokenPool;
    [Tooltip("★장판 텔레그래프 공유 풀 — 이 종이 첫 소비자. 드라이버가 SmashWindup 진입에 SpawnCircle(전방 ●r3)을 호출.")]
    public TelegraphPool telegraphPool;
    [Tooltip("★슬램 임팩트 VFX 공유 풀 — SmashHit(닿는 순간)에 충격파·먼지·그을림을 텔레그래프 원점에 발동. null이면 임팩트 VFX 생략(모션·장판은 그대로).")]
    public SmashImpactPool impactPool;

    [Header("접근/공격 판정 (피하기 난이도 노브 — 유저 ▶ 판정)")]
    [Tooltip("★슬램 사거리(m) = '이 안이면 슬램'(2026-06-14 재설계). 거리가 이 값 이하로 떨어지면(아주 가까운 d≈0 포함) 플레이어로 돌아서 내려찍기 커밋. 밖이면 마주본 채 직선 접근. 스탠드오프 밴드·정면콘·백오프 전부 폐기 — 가까우면 굳지/물러나지 말고 *계속 찍는다*. 슬램 전진(3.514m)+AoE(r8)가 근접 플레이어를 덮으니 지나쳐도 적중. 시작값 4.0.")]
    [SerializeField] float slamRange = 4.0f;
    [Tooltip("Approach(Run_RM) 접근 이동 속도(m/s). 7m 거구라 묵직하게 — 플레이어 걷기(5.5)보다 살짝 느리거나 비슷(브루트는 추격이 본업 아님, 위협은 슬램). 시작값 5.0.")]
    [SerializeField] float approachSpeed = 5.0f;
    [Tooltip("스매시 1사이클 끝나고 다음 슬램 커밋까지의 쿨다운(초) = '큰 슬램 후 딜레이'. ★2026-06-14: 백오프 폐기 → 이 시간 동안 멀뚱 아니라 *마주본 채 코일된 준비 자세*로 페이스를 잡는다(느린 회수 RecoverySpeed 0.65와 함께 연타 리듬). 1.2.")]
    [SerializeField] float restBeforeApproach = 1.2f;

    [Header("Steering — 곡선 추적 회전")]
    [Tooltip("Approach/Roar/Windup 중 타겟 방향 회전 최대 속도(도/초). 브루트라 굼뜨게(작게) — 빠른 플레이어는 측면을 잡을 수 있게(공정).")]
    [SerializeField] float turnSpeed = 180f;

    [Header("Separation — 동료 회피 (군중 AI, 거구 간격)")]
    [SerializeField] float separationRadius = 3.0f;
    [Range(0f, 3f)][SerializeField] float separationWeight = 1.0f;

    [Header("Surround — 포위 슬롯")]
    [SerializeField] float surroundRadius = 2.0f;
    [SerializeField] float slotAngleDeg = 0f;

    [Header("★슬램 조율 (각·박자 분산 — 권위: large-enemy-ai-research §C, 유저 ▶ 판정)")]
    [Tooltip("각 분산 폭(도). 플레이어 기준 *이미 슬램 중인 피어 방위*와 내 방위 각차가 이 값 미만이면 같은 각 = 지금 슬램 금지(재배치). Aztez '같은 각에서 둘 안 옴'. 90°면 4분면당 하나꼴.")]
    [SerializeField, Range(30f, 150f)] float angleSpread = 90f;
    [Tooltip("미세 스태거 최소(초). 마지막 슬램 커밋부터 이 시간 미만이면 잠깐 대기 — 완전 같은 프레임 내려찍기(회피불가) 방지. 동시감은 유지.")]
    [SerializeField, Range(0f, 0.6f)] float staggerMin = 0.2f;
    [Tooltip("미세 스태거 최대(초). 막힌 직후 재질의까지의 상한 — 정보용(현재 로직은 staggerMin 경과로 자동 해제). 0.2~0.4 윈도.")]
    [SerializeField, Range(0f, 0.8f)] float staggerMax = 0.4f;
    [Tooltip("재배치 적극성(0~1). 막힌 브루트가 열린 방위로 surround 슬롯을 얼마나 *공격적으로* 틀지. 1=즉시 빈 틈으로, 낮으면 천천히 미끄러짐(굼뜬 거구 느낌).")]
    [SerializeField, Range(0f, 1f)] float repositionAggression = 1f;

    [Header("★장판 텔레그래프 (이 종이 첫 소비자 — 유저 ▶ 음색/가독성 판정)")]
    [Tooltip("스매시 착탄 원의 반경(m). ★유저 2026-06-14 '범위 훨씬 크게, 걸어선 못 빠지게' → 5.0→8.0. 이 값이 텔레그래프 원+충격파+균열+먼지 전부 스케일. 양팔 내려찍기 광역.")]
    [SerializeField] float telegraphRadius = 8.0f;
    [Tooltip("장판을 시전자 전방 어디에 깔지(m). 스매시가 전방으로 3.5m 전진하며 내려찍으므로, 착탄 중심을 앞에 둔다. 시작값 2.5(거구 앞 팔 닿는 지점).")]
    [SerializeField] float telegraphForwardOffset = 2.5f;
    [Tooltip("적 장판 색 = 레드-오렌지 단색(색 캐넌 §5). 톤은 유저 ▶ 베이스라인 라이팅에서 판정.")]
    [SerializeField] Color telegraphColor = new Color(1f, 0.30f, 0.08f, 1f);
    [Tooltip("장판 마스터 알파(전체 진하기).")]
    [SerializeField, Range(0f, 1f)] float telegraphAlpha = 0.85f;
    [Tooltip("채움(안쪽) 알파.")]
    [SerializeField, Range(0f, 1f)] float telegraphFillAlpha = 0.55f;
    [Tooltip("외곽선 알파(예고선).")]
    [SerializeField, Range(0f, 1f)] float telegraphEdgeAlpha = 1.0f;
    [Tooltip("외곽선 두께(월드 m).")]
    [SerializeField, Min(0f)] float telegraphEdgeWorld = 0.18f;
    [Tooltip("가득 찬 뒤(=임팩트) 장판 잔상 유지 시간(초). 히트 순간을 눈으로 잡게 짧게.")]
    [SerializeField, Min(0f)] float telegraphHold = 0.18f;

    [Header("★슬램 임팩트 주스 (SmashHit 닿는 순간 — 유저 ▶ 무게감/화려함 판정)")]
    [Tooltip("임팩트 VFX 전체 켜기/끄기(디버그·체감 비교용).")]
    [SerializeField] bool impactEnabled = true;
    [Tooltip("충격파 발광 링 HDR 색. 위협 레드오렌지(색 캐넌 §5). 가산이라 밝게 핀다.")]
    [SerializeField] Color impactShockColor = new Color(1f, 0.32f, 0.10f, 1f);
    [Tooltip("충격파 가산 밝기 배율(블룸 먹이). 1=차분 · 2~3=강렬. 거구라 묵직하게.")]
    [SerializeField, Min(0f)] float impactIntensity = 2.0f;
    [Tooltip("충격파 중심 섬광 강도(쾅의 흰핵).")]
    [SerializeField, Range(0f, 4f)] float impactCoreFlash = 1.6f;
    [Tooltip("충격파 링 시작 폭(m). 거구 슬램이라 두껍게.")]
    [SerializeField, Min(0.05f)] float impactRingWidth = 1.1f;
    [Tooltip("충격파 확장 수명(초). 짧고 빠르게 터져야 임팩트.")]
    [SerializeField, Min(0.05f)] float impactShockDuration = 0.45f;
    [Tooltip("바닥 그을림 자국 페이드 수명(초). 잔흔이라 충격파보다 길게.")]
    [SerializeField, Min(0.05f)] float impactScorchDuration = 1.2f;
    [Tooltip("바닥 그을림 색(어두운 탄흔). 알파블렌드로 바닥을 덮는다.")]
    [SerializeField] Color impactScorchColor = new Color(0.06f, 0.05f, 0.05f, 0.85f);
    [Tooltip("먼지 입자 크기 배율(거구라 크게).")]
    [SerializeField, Min(0.1f)] float impactDustSizeScale = 1.4f;

    [Header("★카메라 쉐이크 (Feel — 쾅의 물리적 무게)")]
    [Tooltip("쉐이크 강도(진폭, m). 거구 슬램이라 묵직하게. 너무 크면 멀미.")]
    [SerializeField, Min(0f)] float shakeAmplitude = 0.5f;
    [Tooltip("쉐이크 빈도(Hz). 높으면 날카롭고 낮으면 묵직. 거구는 낮게.")]
    [SerializeField, Min(0f)] float shakeFrequency = 18f;
    [Tooltip("쉐이크 지속(초). 짧게 — 쾅 하고 사라짐.")]
    [SerializeField, Min(0f)] float shakeDuration = 0.28f;

    [Header("★히트스탑 (임팩트 강조 시간 정지 — 프로젝트 HitStop.Do 재사용)")]
    [Tooltip("히트스탑 길이(초). ★0.04~0.06 권장 — 브루트 연타라 길면 거슬린다. 프로젝트 HitStop이 timeScale 0.05·OnDestroy 복원 가드(영구정지 불가능).")]
    [SerializeField, Range(0f, 0.12f)] float hitStopDuration = 0.05f;

    // ════════ ★★스매시 = 3구간 분할 (SmashAttack_RM 한 take를 frame 범위로 쪼갬) ════════
    //   ★왜 분할인가(헌법 준수): 한 state.speed는 클립 전체 균일 → "느린 무거운 윈드업 + 빠른 committed 슬램 + 회수"의
    //    속도 셰이핑을 단일 클립으로 불가 → 같은 take를 frame 범위만 다르게 3분할, 구간별 *정적 state.speed*로.
    //   같은 take 분할이라 경계 포즈(frame 15/30) 비트-동일 → CUT(dur0) 전이여도 포즈 점프 0(연속, crossfade 아님). 루트모션 손실 0.
    //   ★실측(SampleAnimation hand-Y, 2026-06-14): Windup 0~15(팔 1.9→5.28 들어올림) / Strike 15~30(내려찍기 crash, ★임팩트 frame 20) / Recovery 30~50(중립 복귀).
    //
    //   구간별 speed = 브루트 무게 셰이핑(노브 — 유저 ▶ 판정). 드라이버 const 단일 진실원(빌드스크립트가 state.speed로 참조).
    //     WindupSpeed 0.5  : ★느리고 무거운 들어올림. 0.5s native → 1.0s(LV4 텔레그래프 윈도 1.0~1.4s 진입). 거구의 무게 = 큰 느린 예고.
    //     StrikeSpeed 1.25 : 내려찍기 commit. 0.5s native(frame15~30) → 0.40s. 빠른 폭발적 슬램(무게가 떨어지는 가속).
    //     RecoverySpeed 1.4: 중립 복귀. 0.667s native(frame30~50) → 0.476s. 브리스크하게 재정비(다음 접근으로).
    public const float WindupSpeed   = 0.6f;   // ★들기 무겁되 살짝 빠르게(유저 2026-06-14 "공격 속도 빠르게"). 0.5→0.6: 윈드업 1.0s→0.83s,
                                               //   텔레그래프 ~0.88s = 큰 AoE(r8)를 *걸어선 못 빠지고 대시로만* 클리어("뛰어서 피하는거 말고 답 없게"). 여전히 읽히는 들기.
    public const float StrikeSpeed   = 3.4f;   // ★★빠른 폭발적 내려찍기(유저 "공격 속도 빠르게"). 2.8→3.4: 윈드업끝→임팩트 ~0.05s = 쾅.
    public const float RecoverySpeed = 0.65f;  // ★2026-06-14 1.4→0.65 = "찍은 뒤 천천히 손을 올림"(유저). frame30~50 회수(0.667s native)를
                                               //   0.667/0.65 ≈ 1.03s로 늘려 묵직하게. 손드는건 보통(Windup)/내려찍는건 빠름(Strike) 유지, 회수만 느리게.
                                               //   ★const 변경 → SetupData 재실행으로 SmashRecovery state.speed에 반영(빌드스크립트가 이 const 참조).

    // Run_RM 루트모션 네이티브 속도 (Animator 스텝 실측 2026-06-14 = 5.744m / 0.6s = 9.5728 m/s — 매우 빠른 질주 사이클).
    //   ★접근 속도 배율 = approachSpeed / RunNativeSpeed. Approach 상태에서만 modelAnimator.speed에 적용(발도 비례 가속 → 미끄러짐 최소).
    //   approachSpeed 5.0 / 9.5728 ≈ 0.522 배율 → 발이 빠르게 구르되 이동은 묵직한 5.0m/s(거구의 무게). Run 사이클이라 감속해도 발 미끄럼 적음.
    public const float RunNativeSpeed = 9.5728f;

    // 애니 파라미터 — CrassorridBrawler.controller와 공유. (★Roar 제거 2026-06-14: attack 트리거·Roar 상태 폐기.)
    static readonly int PApproach = Animator.StringToHash("isApproaching"); // Idle→Approach (루트모션 접근)
    static readonly int PSmash  = Animator.StringToHash("smash");         // Approach→SmashWindup (slamRange 도달)
    static readonly int SIdle      = Animator.StringToHash("Idle");
    static readonly int SApproach  = Animator.StringToHash("Approach");
    static readonly int SWindup    = Animator.StringToHash("SmashWindup");
    static readonly int SStrike    = Animator.StringToHash("SmashStrike");
    static readonly int SRecovery  = Animator.StringToHash("SmashRecovery");

    bool _holdsToken;
    bool _approaching;      // isApproaching 미러.
    bool _smashFired;       // 이 접근 사이클에 smash 트리거 쐈나(중복 발동 가드).
    bool _windupSpawned;    // ★이 Windup 진입에 장판 이미 스폰했나(엣지 가드 — 매 Windup 1회만).
    bool _slamRegistered;   // ★이 슬램이 조율자에 등록돼 있나(각·박자 분산 — 비대칭 해제 방지: Recovery/OnDisable에서 1회만 Unregister).
    // ★2026-06-14 슬램 재커밋 쿨다운 — "큰 슬램 후 딜레이". 이 시각 전엔 Approach에서 새 슬램 커밋 금지.
    //   ★핵심: 딜레이를 Idle 멀뚱서기로 채우지 않는다 — 즉시 Approach 재진입해 *백오프+마주보기*(스탠드오프 재확보)로 채운다.
    //   슬램 직후 브루트는 플레이어에 붙어있음(런지 착탄) → 이 시간에 뒤로 빠지며 square up = 자연스러운 비트(멀뚱 아님).
    float _slamCooldownUntil;

    // ── ★장판 텔레그래프 활성 추적 (gen 세대 가드 — 풀 회수로 주인 바뀌면 stale 조작 차단) ──
    TelegraphPad _activePad;
    int _activeGen = -1;

    // ── ★텔레그래프 원점 캐시 — Windup서 깐 약속 지점(공정). SmashHit가 *여기*에 임팩트를 떨군다
    //   (현재 model.forward로 재계산 ❌ — 슬램 도중 전진·회전했어도 약속 지점 = 실제 착탄점). ──
    Vector3 _telegraphOrigin;
    bool _hasTelegraphOrigin;

    void Awake()
    {
        if (modelAnimator == null) { Debug.LogError("[CrassorridBrawler] modelAnimator 미할당"); enabled = false; return; }
        // ★H-3: SmashHit AnimationEvent는 SendMessage라 *Animator와 같은 GameObject*의 컴포넌트에만 도달한다.
        //   스포너가 드라이버를 프리팹 루트(Animator도 루트, 실측 검증됨)에 AddComponent하므로 같은 GO여야 한다 → 어긋나면 무음 미발화.
        if (modelAnimator.gameObject != gameObject)
            Debug.LogError("[CrassorridBrawler] modelAnimator가 드라이버와 다른 GameObject — SmashHit AnimationEvent SendMessage 미도달 위험(장판 ForceFull 동기 실패). 드라이버를 Animator GO에 붙여라.");
        if (attackController != null) modelAnimator.runtimeAnimatorController = attackController;
        else Debug.LogError("[CrassorridBrawler] attackController 미할당");
        // ★루트모션이 전진을 만든다(제2원칙). OnAnimatorMove 미구현 → Unity 자동 적용(증폭 없음 — Dimax처럼 게인 안 씀, 클립 전진 그대로).
        modelAnimator.applyRootMotion = true;
        ResetCombatState();
    }

    void OnEnable()  { if (!Roster.Contains(this)) Roster.Add(this); }
    void OnDisable()
    {
        Roster.Remove(this);
        CancelTelegraph();   // ★시전 중 비활성/파괴 → 차오르던 장판 즉시 취소(시체 위에서 "닿는다" 거짓말 방지 — 공정성 §북극성6).
        ReleaseToken();
        UnregisterSlam();    // ★stale 방위 누수 차단(제일 위험): 죽은/풀회수 브루트 방위가 등록부에 남으면 산 브루트를 영영 막는다.
        // ★HIGH(Codex/Dimax 패턴): 비활성/파괴 시 교전 플래그 일괄 리셋 — 풀링/재활성 시 stale 플래그가 다음 교전에서
        //   smash 발동 또는 장판 스폰을 무음 스킵하는 비대칭 차단(_smashFired/_windupSpawned/_approaching 누락 방지).
        ResetCombatState();
    }

    // ★교전 상태 일괄 리셋 — 모든 진입/해제 경로가 경유(플래그 누락 비대칭 차단). trigger 리셋은 modelAnimator 가드.
    void ResetCombatState()
    {
        _smashFired = false;
        _windupSpawned = false;
        _approaching = false;
        _slamCooldownUntil = 0f;   // ★풀링 재활성 시 stale 쿨다운이 첫 슬램을 막지 않게 리셋(첫 교전은 쿨다운 0).
        if (modelAnimator != null)
        {
            modelAnimator.ResetTrigger(PSmash);
            modelAnimator.SetBool(PApproach, false);   // ★대칭 갭 수정(Codex): _approaching 미러만 끄면 Animator 불이 stale true로 남아
                                                       //   풀링 재활성/접근 중 소실 시 Idle 우회하고 Approach 즉시 재진입(장판 조기 스폰). SetApproaching과 대칭.
        }
    }

    void ReleaseToken()
    {
        if (_holdsToken && tokenPool != null) tokenPool.Release();
        _holdsToken = false;
    }

    // ★조율자에서 이 슬램 등록 해제 — Recovery 진입·OnDisable 경유. _slamRegistered 가드로 비대칭/중복 해제 차단.
    //   ★stale 방위 누수 차단(제일 위험): 죽은/회수된 브루트의 방위가 등록부에 남으면 산 브루트를 영영 막는다.
    void UnregisterSlam()
    {
        if (_slamRegistered)
        {
            BruteSlamCoordinator.Unregister(this);
            _slamRegistered = false;
        }
    }

    // ★막힌 브루트의 플랭킹 재배치 — 가장 열린 방위로 surround 슬롯을 튼다(맴돌기 아님 = 의도적 측면 이동).
    //   조율자가 슬램 중인 피어 방위들에서 가장 먼 "빈 틈"을 찾아줌 → slotAngleDeg(현재 방위 대비 상대각)로 변환.
    //   Approach 스티어(SlotTargetPoint/Steer)가 그 슬롯으로 데려감 = 루트모션 보행으로 미끄러지는 거구.
    void RepositionToFlank(float myAzimuth)
    {
        float openAz = BruteSlamCoordinator.OpenestAzimuth(this, myAzimuth);
        // ★SlotTargetPoint는 매 프레임 "플레이어→나"(=현재 방위)를 baseDir로 잡고 slotAngleDeg만큼 회전한다.
        //   따라서 목표 절대방위 openAz로 가려면 slotAngleDeg = (openAz - 현재 방위)로 *설정*(누적 ❌ — 누적하면 자기 꼬리 추적).
        //   브루트가 openAz로 미끄러질수록 현재 방위→openAz라 이 델타가 0으로 수렴(convergent, 안정). 굼뜬 거구 미끄러짐.
        float desiredDelta = Mathf.DeltaAngle(myAzimuth, openAz);   // -180~180
        slotAngleDeg = Mathf.Lerp(slotAngleDeg, desiredDelta, repositionAggression);
    }

    // 차오르던 장판을 즉시 풀로 반납(gen 가드 — 이미 회수돼 남의 것이면 무시).
    void CancelTelegraph()
    {
        if (_activePad != null) _activePad.CancelImmediate(_activeGen);
        _activePad = null;
        _activeGen = -1;
        _hasTelegraphOrigin = false;   // ★취소된 슬램은 임팩트도 없다(시체 위 거짓 착탄 방지 — 공정성 §북극성6).
    }

    public void SetSurroundSlot(float angleDeg) => slotAngleDeg = angleDeg;

    Vector3 SlotTargetPoint()
    {
        Vector3 p = target.position; p.y = 0f;
        if (surroundRadius <= 0.001f) return p;
        Vector3 toMe = model.position - target.position; toMe.y = 0f;
        Vector3 baseDir = toMe.sqrMagnitude > 0.0001f ? toMe.normalized : model.forward;
        Vector3 slotDir = Quaternion.AngleAxis(slotAngleDeg, Vector3.up) * baseDir;
        return p + slotDir * surroundRadius;
    }

    Vector3 SteerDirection()
    {
        Vector3 me = model.position; me.y = 0f;
        Vector3 seek = SlotTargetPoint() - me; seek.y = 0f;
        Vector3 seekDir = seek.sqrMagnitude > 0.0001f ? seek.normalized : model.forward;

        Vector3 sep = Vector3.zero;
        if (separationWeight > 0f && separationRadius > 0f)
        {
            float r2 = separationRadius * separationRadius;
            for (int i = 0; i < Roster.Count; i++)
            {
                var other = Roster[i];
                if (other == null || other == this || other.model == null) continue;
                Vector3 away = me - new Vector3(other.model.position.x, 0f, other.model.position.z);
                float d2 = away.sqrMagnitude;
                if (d2 > 0.0001f && d2 < r2)
                    sep += away.normalized * (1f - Mathf.Sqrt(d2) / separationRadius);
            }
        }
        Vector3 composite = seekDir + sep * separationWeight;
        return composite.sqrMagnitude > 0.0001f ? composite.normalized : seekDir;
    }

    void Steer()
    {
        Vector3 dir = SteerDirection();
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            model.rotation = Quaternion.RotateTowards(model.rotation, want, turnSpeed * Time.deltaTime);
        }
    }

    float PlanarDistanceToTarget()
    {
        Vector3 a = model.position, b = target.position; a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ★평면 플레이어 방향(나→플레이어, XZ). 0벡터면 model.forward 폴백.
    Vector3 PlanarToTarget()
    {
        Vector3 d = target.position - model.position; d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : model.forward;
    }

    // ★마주보기(공전 아님) — 교전 브루트는 surround 슬롯 무시하고 플레이어를 직접 향해 turnSpeed로 제자리 회전.
    //   "옆/뒤면 돌아서 마주봄" = 제자리 회전+재정렬(측면 공전 ❌). 위치는 안 만든다(회전만 = 방향=의도, 헌법 부합).
    void FacePlayerTurn()
    {
        Vector3 dir = PlanarToTarget();
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            model.rotation = Quaternion.RotateTowards(model.rotation, want, turnSpeed * Time.deltaTime);
        }
    }

    void SetApproaching(bool v)
    {
        _approaching = v;
        if (modelAnimator != null) modelAnimator.SetBool(PApproach, v);
    }

    void Update()
    {
        if (modelAnimator == null) { ReleaseToken(); CancelTelegraph(); UnregisterSlam(); return; }   // ★H-1(Stab+Codex): 조기리턴도 슬램 해제 — 안 풀면 죽은 방위 박제→산 브루트 영구차단
        // ★속도 단일 진실원: 매 프레임 1f로 리셋(상태.speed는 컨트롤러가 들고 있음 — Roar/스매시 구간 배속).
        //   Approach 브랜치에서만 배율로 올린다 → 이탈 경로 전부에서 배율 안 샘.
        modelAnimator.speed = 1f;
        if (target == null || model == null) { ReleaseToken(); CancelTelegraph(); UnregisterSlam(); return; }   // ★H-1: 동상

        var info = modelAnimator.GetCurrentAnimatorStateInfo(0);
        int s = info.shortNameHash;

        // ★H-1 엣지 가드(Dimax 패턴): Windup이 *아닐* 때 _windupSpawned를 false로 → Windup 진입 첫 프레임에만 1회 스폰.
        //   (구) Strike 분기에서만 리셋하면, Strike를 건너뛰고 Recovery→Idle로 떨어지는 비정상 복귀 경로에서 stale true가 남아
        //    다음 Windup 장판 스폰이 무음 스킵될 수 있다. 상태 기반 엣지 가드가 그 경로까지 자가치유.
        if (s != SWindup) _windupSpawned = false;

        // ★슬램 등록 엣지 가드(자가치유): 슬램 구간(Windup/Strike/Recovery)이 *아닌데* 아직 등록돼 있으면 해제.
        //   Recovery 정상 경로는 SRecovery 분기가 풀지만, Strike 건너뛰고 Idle/Approach로 떨어지는 비정상 복귀에서
        //   stale 방위가 누수돼 다른 산 브루트를 영영 막는 사고를 차단(_windupSpawned 가드와 같은 결).
        if (_slamRegistered && s != SWindup && s != SStrike && s != SRecovery)
            UnregisterSlam();

        // ── Idle: 결정 허브 / 사이클 사이 *짧은* 통과 (★Roar 오프너 제거 — 타깃 인지 즉시 Approach 직행) ──
        if (s == SIdle)
        {
            ReleaseToken();
            _smashFired = false;
            FacePlayerTurn();   // ★정지 중 플레이어 직접 마주봄(슬롯 공전 ❌).
            // ★타깃 인지 = 즉시 재접근(첫 교전이든 슬램 1사이클 후 복귀든 동일). 딜레이는 Idle 멀뚱이 아니라
            //   Approach의 슬램 쿨다운(_slamCooldownUntil)이 채운다 = 마주본 채 square-up(능동적 비트).
            if (!_approaching) SetApproaching(true);   // → Approach(Run_RM) 진입
        }
        // ── Approach(Run_RM 루트모션 접근): ★2026-06-14 근접=계속 슬램 재설계(스탠드오프·정면콘·백오프 폐기) ──
        //   유저 설계: 가까우면 굳거나 물러나지 말고 *계속 내려찍는다*. 교전 브루트 = 슬롯 공전 ❌ → 플레이어를 *마주본 채* 직선 접근.
        //   slamRange 안이면(아주 가까운 d≈0 포함) 돌아서 슬램. 밖이면 마주본 채 전진. 지나침 허용(큰 AoE가 근접 플레이어를 덮음).
        //   대기 브루트(조율자 막힘) = 플랭킹 재배치(빈 방위로 surround 슬롯, 목적지 있는 측면 이동 — 공전 아님, 이미 구현).
        else if (s == SApproach)
        {
            if (_smashFired) { /* 커밋 후 전이 프레임 — 아무것도 안 함(speed 1f 유지, 배율 누수 차단). */ }
            else
            {
                float myAz = BruteSlamCoordinator.AzimuthOf(model.position, target.position);
                bool coordinatorAllows = BruteSlamCoordinator.CanSlamNow(this, myAz, angleSpread, staggerMin);

                if (!coordinatorAllows)
                {
                    // ── ★대기 브루트(각/박자 충돌) = 플랭킹 재배치. 맴돌기 아님 = 빈 방위로 목적지 있는 측면 이동. ──
                    //   여기선 surround 슬롯 스티어를 쓴다(Steer = SlotTargetPoint 추적). 마주보기 ❌(플랭킹은 자리 잡으러 가는 중).
                    RepositionToFlank(myAz);
                    modelAnimator.speed = approachSpeed / RunNativeSpeed;
                    Steer();   // 열린 슬롯으로 미끄러짐(굼뜬 거구).
                }
                else
                {
                    // ── ★슬램 차례 브루트 = 마주보고 직선 접근(공전 ❌). ──
                    FacePlayerTurn();   // 항상 플레이어 직접 마주봄(제자리 회전+재정렬). 측면 슬롯 공전 안 함 = 뱅뱅 금지.
                                        //   ★옆에 붙은 빠른 플레이어도 여기서 돌아서 마주본다 = 돌면서라도 찍는다.

                    float d = PlanarDistanceToTarget();
                    bool cooledDown = Time.time >= _slamCooldownUntil;   // ★큰 슬램 후 딜레이 — 쿨다운 전엔 마주보기만(커밋 ❌, 페이스).

                    if (d <= slamRange && cooledDown)
                    {
                        // ★slamRange 안(아주 가까운 d≈0 포함) = 슬램 커밋. 굳지/물러나지 말고 *계속 내려찍는다*.
                        //   슬램 전진(~3.5m)+AoE(r8)가 근접 플레이어를 덮으니 본체가 좀 지나쳐도 적중. 돌아서 또 찍는 연타.
                        //   ★d≈0 굳음 차단: 위 FacePlayerTurn으로 이미 플레이어 향해 돌고 있고, 슬램 궤적/장판은
                        //     커밋 시점의 model.forward에 깔린다(SpawnSmashTelegraph). 방향이 degenerate(플레이어가 정확히 위)여도
                        //     PlanarToTarget가 model.forward 폴백 → forward는 항상 유효 → 절대 멈추지 않고 현재 facing으로 슬램 강행.
                        _smashFired = true;
                        SetApproaching(false);
                        modelAnimator.speed = 1f;          // 스매시 클립은 상태 speed가 구동(배율 누수 차단).
                        modelAnimator.SetTrigger(PSmash);  // → SmashWindup CUT 진입
                        // ★커밋 *즉시* 등록(Windup 진입 1프레임 지연 ❌ — race 차단).
                        _slamRegistered = true;
                        BruteSlamCoordinator.RegisterSlam(this, myAz);
                    }
                    else if (d > slamRange)
                    {
                        // ★사거리 밖 = 마주본 채 직선 접근(전방 루트모션). 측면 공전 아님 — seek가 플레이어 직선 위.
                        modelAnimator.speed = approachSpeed / RunNativeSpeed;
                    }
                    else
                    {
                        // ★사거리 안인데 쿨다운 중 = 이동 정지 + 마주보기 유지(FacePlayerTurn이 회전 중).
                        //   멀뚱 아님 = 슬램 사거리에서 플레이어를 노려보는 *코일된 준비 자세*. 쿨다운 풀리면 다음 프레임 커밋(연타 페이스).
                        modelAnimator.speed = 0f;
                    }
                }
            }
        }
        // ── SmashWindup(frame 0~15 = 양팔 들어올림): ★회전 O(cocking 조준) + ★장판 스폰·차오름 개시. ──
        else if (s == SWindup)
        {
            FacePlayerTurn();   // ★cocking 중 플레이어 직접 추적(슬롯 공전 ❌ — 스탠드오프 정렬과 일관). 아직 내려찍기 전 = 헌법 허용.
                                //   turnSpeed 제한 회전(거구라 스냅 ❌ 묵직하게 — 빠른 플레이어는 윈드업 중 측면을 잡을 수 있게 = 공정성 §북극성6).
                                //   회전만(위치=루트모션, speed=정적).

            if (!_windupSpawned)
            {
                _windupSpawned = true;
                SpawnSmashTelegraph();   // ★진입 1회: 전방 ●r3 원 장판 스폰 → 윈드업 동안 채움(_Progress 0→1).
                // (슬램 등록은 Approach 커밋 시점에 이미 완료 — race 차단 위해 Windup 지연 안 함.)
            }
        }
        // ── SmashStrike(frame 15~30 = 내려찍기 commit): ★회전 0 엄수(전방 슬램 궤적 보존). ──
        //   SmashHit AnimationEvent(임팩트 frame 20)가 여기서 발화 → 장판 ForceFull(채움 완료=발동) + 향후 광역 히트 훅.
        else if (s == SStrike)
        {
            // 회전·이동·trigger 없음 — 내려찍기가 루트모션으로 전진하며 재생될 뿐. (_windupSpawned 리셋은 상단 엣지 가드가 담당.)
        }
        // ── SmashRecovery(frame 30~50 = 중립 복귀, ★느린 회수 RecoverySpeed 0.65 = 천천히 손 올림): ★회전 0 엄수. ──
        else if (s == SRecovery)
        {
            // 회전 0. 회수 끝나면 컨트롤러가 Idle로(드라이버가 거기서 즉시 재접근 → 백오프+마주보기로 딜레이를 채움).
            // ★슬램 끝(내려찍기 착탄 후 회수) → 조율자에서 등록 해제 + 슬램 쿨다운 무장(큰 딜레이): 둘 다 _slamRegistered 일회 가드 경유.
            //   Strike까지는 잡고 있어야(각 충돌 유효) 공정 — Recovery 진입 = 슬램의 위협 종료 시점.
            if (_slamRegistered)
                _slamCooldownUntil = Time.time + restBeforeApproach;   // 회수 시작부터 큰 딜레이 카운트(이 시간 동안 백오프+square-up, 커밋 ❌).
            UnregisterSlam();   // 내부 _slamRegistered 가드 — 위 cooldown 무장도 같은 일회성(첫 Recovery 프레임만).
        }
    }

    // ════════ ★장판 스폰 — SmashWindup 진입 1회. 전방 ●r3 원을 윈드업→임팩트 동안 채운다. ════════
    //   ★채움 시간(fillDuration) = 윈드업 시작 → 임팩트(frame 20)까지의 *실시간*. 윈드업 구간 speed가 늦추므로 그만큼 계산.
    //     윈드업 구간(frame 0~15) 실시간 = (15/30fps) / WindupSpeed = 0.5/0.5 = 1.0s.
    //     스트라이크 구간 임팩트까지(frame 15~20) 실시간 = (5/30fps) / StrikeSpeed = 0.1667/1.25 = 0.133s.
    //     → fillDuration ≈ 1.133s (LV4 텔레그래프 1.0~1.4s 윈도 정중앙). ★SmashHit 이벤트가 ForceFull로 정확히 동기 보정.
    //   ★장판 위치 = 스폰 *시점*의 전방 telegraphForwardOffset 지점에 고정(차오르는 동안 안 옮김 = 공정한 약속).
    //     스매시가 전방 3.5m 전진하므로 착탄 중심을 앞에 둔다. 회전해도(Windup FaceTarget) 장판은 안 따라 돈다(이미 깐 약속 보존).
    void SpawnSmashTelegraph()
    {
        if (telegraphPool == null) return;   // 풀 없으면 장판 생략(시스템 미주입 — 모션만으로도 가독은 윈드업이 보완).
        var pad = telegraphPool.Acquire();
        if (pad == null) return;

        Vector3 fwd = model.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();
        Vector3 origin = model.position + fwd * telegraphForwardOffset; origin.y = 0f;

        float fillDuration = SmashWindupToImpactSeconds();
        pad.SpawnCircle(origin, telegraphRadius, fillDuration, telegraphHold,
                        telegraphColor, telegraphAlpha, telegraphFillAlpha, telegraphEdgeAlpha, telegraphEdgeWorld);
        _activePad = pad;
        _activeGen = pad.Gen;

        // ★임팩트 원점 캐시 — SmashHit가 *이 약속 지점*에 충격파/먼지/그을림을 떨군다(공정성: 텔레그래프 == 착탄).
        _telegraphOrigin = origin;
        _hasTelegraphOrigin = true;
    }

    // 윈드업 시작 → 임팩트(frame 20)까지의 실시간(초). 구간 speed로 native 시간을 나눈다(state.speed는 시간 스케일).
    //   ★빌드스크립트의 frame 경계(WindupFrame 15 / 임팩트 frame 20 / SrcFps 30)와 일치해야 함 → const로 묶어 desync 차단.
    public const int   SrcFps       = 30;
    public const int   WindupFrame  = 15;   // ★윈드업/스트라이크 경계(팔 정점 직후 = 내려찍기 시작). 0~15=Windup, 15~30=Strike.
    public const int   ImpactFrame  = 20;   // ★임팩트(실측 — 손 최저점, 내려찍기 닿는 순간). Strike 구간 내.
    static float SmashWindupToImpactSeconds()
    {
        float windupReal = (WindupFrame / (float)SrcFps) / WindupSpeed;                 // frame 0~15 @ WindupSpeed
        float strikeToImpactReal = ((ImpactFrame - WindupFrame) / (float)SrcFps) / StrikeSpeed; // frame 15~20 @ StrikeSpeed
        return windupReal + strikeToImpactReal;
    }

    // ════════ ★AnimationEvent 콜백 — SmashStrike 클립의 임팩트 프레임(20)에서 호출(SendMessage, Animator와 같은 GameObject=루트) ════════

    /// <summary>내려찍기가 바닥에 닿는 순간(임팩트 frame 20). ★장판을 ForceFull로 채움 완료 = 발동(채움 클립 미세 어긋남 보정).
    /// 향후 광역 데미지/히트박스는 게임플레이 단계(범위 밖) — 이 이벤트에 훅(애니=타이밍의 진실).</summary>
    public void SmashHit(AnimationEvent ev)
    {
        // ★장판 발동 동기: 채움을 가득(=발동)으로 강제. gen 가드로 회수된 패드 오발 차단.
        if (_activePad != null)
        {
            _activePad.ForceFull(_activeGen);
            // 임팩트 후 패드 수명은 Pad.Update가 holdAfterFull 뒤 자동 반납 → 참조만 놓는다(이중 반납 방지).
            _activePad = null;
            _activeGen = -1;
        }

        // ════════ ★임팩트 주스 — 닿는 *순간의 충격*(유저 2026-06-14 "위협 안 됨, 압박감 없음") ════════
        //   장판 동기(위)와 *별개* 추가. 텔레그래프 원점(_telegraphOrigin = 공정한 약속 지점)에 떨군다.
        FireSmashImpact();
        // 향후 광역 데미지/넉백 히트박스는 게임플레이 단계에서 이 이벤트에 훅(애니=타이밍의 진실).
    }

    // ★임팩트 VFX + 카메라 쉐이크 + 히트스탑 — SmashHit 닿는 순간. 텔레그래프 약속 지점에 충격.
    void FireSmashImpact()
    {
        if (!impactEnabled) return;

        // 임팩트 위치 = 텔레그래프 약속 지점(현재 forward 재계산 ❌ — 슬램 도중 전진·회전 무시, 깐 약속 = 착탄).
        //   텔레그래프 미스폰(풀 미주입 등)이면 폴백으로 현재 전방을 쓴다(임팩트가 무위치로 사라지지 않게).
        Vector3 impactOrigin;
        if (_hasTelegraphOrigin) impactOrigin = _telegraphOrigin;
        else
        {
            Vector3 fwd = model != null ? model.forward : Vector3.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();
            impactOrigin = (model != null ? model.position : transform.position) + fwd * telegraphForwardOffset;
            impactOrigin.y = 0f;
        }
        _hasTelegraphOrigin = false;   // 이번 슬램 소비 — 다음 Windup이 다시 깐다(stale 원점 재사용 차단).

        // 1) 임팩트 VFX(충격파+먼지+그을림) — 텔레그래프 반경과 일치(약속 == 피해 영역).
        if (impactPool != null)
        {
            var fx = impactPool.Acquire();
            if (fx != null)
                fx.Play(impactOrigin, telegraphRadius, impactShockColor, impactIntensity,
                        impactCoreFlash, impactRingWidth, impactShockDuration, impactScorchDuration,
                        impactDustSizeScale, impactScorchColor);
        }

        // 2) 카메라 쉐이크 — 쾅의 물리적 무게(이벤트 브로드캐스트, JudgeCam 리스너가 받음).
        if (shakeAmplitude > 0f && shakeDuration > 0f)
            SmashFeel.Shake(shakeDuration, shakeAmplitude, shakeFrequency);

        // 3) 히트스탑 — 임팩트 강조 시간 정지(★프로젝트 HitStop.Do로 위임 — 단일 시간 소유자, timeScale 복원 안전).
        SmashFeel.HitStop(hitStopDuration);
    }
}
