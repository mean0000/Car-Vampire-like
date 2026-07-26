using UnityEngine;

/// <summary>
/// 비주얼 캐릭터(Humanoid Animator)에 붙는다. 두 역할:
///  ① 구동: 매 프레임 Motor/Aim 상태 → 로코모션 파라미터(Speed/MoveX/MoveY/Dash).
///     facing(조준)과 movement(이동)를 분리 — 마우스로 앞을 보며 S로 뒷걸음, D로 우측 스트레이프.
///  ② 릴레이: 공격 클립의 AnimationEvent(<see cref="OnAttackHit"/>)를 받아 <see cref="AttackHit"/>로
///     무기에 전달한다(애니가 진실 — 타격 타이밍은 클립이 소유).
///
/// 파라미터 규약은 Animation 에이전트의 컨트롤러와 고정 합의: Speed/MoveX/MoveY(float), Attack(trigger), Dash(bool).
/// 루트는 안 돌린다(자식 카메라 보호) — 회전은 이 비주얼 transform에만 적용한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorDriver : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("이동을 추적할 대상. 비우면 부모(플레이어 루트). Aim/Motor는 이 대상의 부모 체인에서 찾는다.")]
    [SerializeField] Transform moveSource;

    [Header("Speed Mapping (m/s → blend)")]
    [SerializeField] float walkSpeedRef = 5f;
    [SerializeField] float runSpeedRef = 8.5f;
    [Tooltip("측정 속도 상한 — 스폰/텔레포트 점프로 인한 블렌드 오버슈트 방지.")]
    [SerializeField] float maxSpeed = 12f;
    [Tooltip("이 속도(m/s) 미만이면 정지로 간주 — 방향(MoveX/Y)을 0으로(회전은 계속).")]
    [SerializeField] float moveThreshold = 0.3f;

    [Header("★발속도 종속 (2026-07-11 조작감 동기화 — 재생율이 실측 속도를 따라가 발 미끄러짐 제거)")]
    [Tooltip("걷기 트리(blend 1) 클립의 1.0× 자연 이동속도(m/s) — S2_Run 루트모션 실측 4.31(06-19). 플레이에서 발이 여전히 밀리면 이 값을 보정.")]
    [SerializeField] float walkAnimSpeed = 4.31f;
    [Tooltip("런 트리(blend 2, m_TimeScale 1.35) 자연 이동속도(m/s) ≈ walkAnimSpeed×1.35.")]
    [SerializeField] float runAnimSpeed = 5.82f;
    [Tooltip("재생율 상한 — 초과 속도(고티어 스프린트 15/24)는 연출(잔상·기울기)이 판다. 너무 크면 발이 우스꽝스럽게 동동거림.")]
    [SerializeField] float locoRateMax = 2f;
    [Tooltip("재생율 댐핑(초) — 티어 버스트 순간의 재생율 점프 완충(짧게).")]
    [SerializeField] float locoRateDamp = 0.05f;

    [Header("★달리기 기울기 (2026-07-11 — 속도를 실루엣으로: 진행 방향으로 몸 전체를 기울임)")]
    [Tooltip("최대 기울기(도) — 속도가 leanFullSpeed일 때. 0=끔(A/B 노브). 하이포니 레퍼 프레임 실측: '뛴다'는 발싱크보다 기울기·실루엣이 판다.")]
    [SerializeField, Range(0f, 25f)] float leanMaxAngle = 10f;
    [Tooltip("이 속도(m/s)에서 최대 기울기 도달 — 기본 15(스프린트 2단). 걷기(4.31)는 ~29%만 기울어 은은하고, 티어가 오를수록 실루엣이 눕는다.")]
    [SerializeField] float leanFullSpeed = 15f;
    [Tooltip("기울기 이즈 시간(초) — 0→최대 도달 시간. 짧을수록 즉각.")]
    [SerializeField] float leanDamp = 0.12f;

    [Header("Damping")]
    [SerializeField] float speedDamp = 0.035f;
    [Tooltip("★방향 댐핑 — 8방향 스냅 버킷 사이 전환을 뭉개주는 완충. 너무 낮으면 45° 점프가 그대로 보여 각짐(07-11 0.03 과조임 → 0.07 복원). 속도 반응은 speedDamp가 별도.")]
    [SerializeField] float dirDamp = 0.07f;
    [Tooltip("★8방향 스냅 A/B(07-11) — ON=이동 애니 방향 45° 양자화(한 클립 100% 활성, 기존 캐넌: 대각 F+R+FR 3클립 혼합 뭉갬 방지). " +
             "OFF=연속 방향 블렌드(부드러움↑, 각짐↓ — freeform 2D 혼합 품질은 플레이 판정). 플레이 중 토글 키 있음.")]
    [SerializeField] bool snap8Way = true;
    [Tooltip("8방향 스냅 A/B 토글 키(비교 하니스 — 판정 후 제거).")]
    [SerializeField] KeyCode toggleSnap8Key = KeyCode.G;
    [Tooltip("★곡선 스냅 완화(07-14) — 속도 방향이 이 각속도(도/초) 이상으로 지속 회전 중(원호 카이팅)이면 8방향 스냅을 일시 해제해 연속 블렌드. " +
             "직선·정지·미세 조정은 스냅 유지(R12 '스냅 ON' 판정은 직선 문법 — 곡선 한정 완화는 신규 판정 대상). 0=끔(항상 스냅=R12 그대로).")]
    [SerializeField, Min(0f)] float snapRelaxTurnRate = 60f;
    [Tooltip("★조향(궤적 원호) A/B 토글 키(07-11 R5) — ON=하데스식 원호 궤적 / OFF=트윈스틱식 즉각 이동(시각만 스무딩). 판정 후 제거.")]
    [SerializeField] KeyCode toggleSteerKey = KeyCode.H;
    [Tooltip("★대시 시각-궤적 정합(R10, Codex 발견) — ON: 대시 순간 몸이 실제 대시 방향을 보고 전진 스텝 재생(하데스식 커밋, " +
             "자유 방향 전부 발=궤적 일치). OFF: 구 방식(조준 프레임 4방향 스냅 — 몸은 대각선인데 발은 정방향 문제).")]
    [SerializeField] bool dashFaceMotion = true;
    [Tooltip("★이동 시 몸 회전 속도(도/초) — 낮을수록 부드럽게 돌고(각짐↓·둔함↑), 높을수록 즉각 스냅. WASD 8방향의 '딱딱한 방향 전환'을 이걸로 완화한다. 공격/대시 중엔 무시(즉시 잠금 — 런지/회피 크리스프 유지).")]
    [SerializeField] float faceTurnRate = 600f;

    [Header("★제자리 회전 발 셔플 (2026-07-11 — '동상 회전' 제거: 정지 중 몸 yaw가 빠르게 돌면 발이 따라 구름)")]
    // 회전 각도/속도는 계속 코드(_facingRot)가 소유한다 — 이 셔플은 루트모션 턴이 아니라, 코드 회전 중 다리가 죽지 않게 보이는
    //   '표시용' 하체 모션이다. Base 로코모션 Speed=0 노드에 중첩된 TurnShuffle 1D 트리(TurnRate 파라미터)가 기존 8way
    //   스트레이프(walk와 동일 스탠스·루프 클립)를 부분 웨이트로 켠다 → 스탠스 모프 위험 0. 이동/공격/대시/액션 중엔 0(요구사항 #3).
    //   클립 루트 변위는 로코모션 프레임에서 OnAnimatorMove가 폐기하므로 발은 제자리에서 구른다(코드가 위치/회전 단일 소유).
    [Tooltip("이 yaw 각속도(도/초) 미만이면 셔플 없음(데드존) — 미세 조준 흔들림에 발이 떨리지 않게. 요구사항의 '임계'.")]
    [SerializeField] float turnShuffleThreshold = 55f;
    [Tooltip("이 yaw 각속도(도/초)에서 셔플이 최대치(turnShuffleMax) 도달. faceTurnRate가 실제 도달 각속도의 상한이므로 이 값은 그보다 낮게 둔다.")]
    [SerializeField] float turnRateRef = 200f;
    [Tooltip("셔플 강도 상한(0~1) — 스트레이프 클립을 최대 몇 %까지 섞을지. 낮게=은은한 무게이동, 높게=성큼 스텝. '달리는 다리처럼' 보이면 낮춰라.")]
    [SerializeField, Range(0f, 1f)] float turnShuffleMax = 0.55f;
    [Tooltip("셔플 파라미터 스무딩(초) — 회전 시작/정지 시 발이 부드럽게 들어오고 빠지게.")]
    [SerializeField] float turnShuffleDamp = 0.09f;
    [Tooltip("셔플 좌우 방향 뒤집기(플레이 판정 A/B) — 오른쪽으로 도는데 발이 왼쪽으로 스텝하면 체크.")]
    [SerializeField] bool turnShuffleInvert = false;

    [Header("★대시 리타임 3단 강약 (2026-07-13 R14 — '뭘 하는지 모르겠다' 수정: 커밋 빠름·회피실루엣 느림·회수 중간)")]
    // Dash 상태 클립(Evade)을 DashRate 배속 멀티플라이어로 구동한다 — 클립=진실, 코드는 배속만(LocoRate 선례, 위치/포즈 창작 아님).
    //   위상은 클립 자신의 normalizedTime으로 판단(자기완결). 컨트롤러 계약: Dash 상태 m_Speed=1 + SpeedParameter=DashRate.
    //   상태 exit=exitTime 0.95(Dash==false 조기컷 삭제)라 회수까지 재생된다.
    // ★R13(2단)이 실패한 이유 = 경계가 잘못됐다. Evade_F 프레임 실측(60fps/48f, 위치는 모터 소유·클립은 포즈만):
    //     · f0-8   (n0~0.17) = 발구름/커밋: 속도 1.8→11.4 m/s 가속(코일→발사).
    //     · f8-19  (n0.17~0.40) = 회피 실루엣: 피크 14.3 m/s(f10) → 3 m/s 감속. ★몸이 회피방향으로 뻗은 '내가 지금 피한다' 포즈 = 정체성.
    //     · f19-46 (n0.40~0.95) = 착지/회수: 3→0 m/s 무게 회복 후 정지. 정보량 낮음(캔슬 대상).
    //   R13은 '이젝션'(n0~0.4)에 위 발구름+실루엣을 통째로 넣고 2.2×로 밀어 0.145s에 뭉갰다(정체성 실종=유저 "뭘 하는지 모르겠다").
    //   그리고 1.2× '착지'는 정보 없는 회수 꼬리에 0.37s를 썼다 — 강약이 거꾸로였다. R14는 이걸 3구간으로 분리해 뒤집는다.
    [Tooltip("① 발구름/커밋(클립 앞부분, 기본 n0~0.15) 재생 배속 — 회피 시작의 '결단' 스냅. 높을수록 즉각 튀어나감(입력 반응성은 상태 진입이 보장, 이 값은 발구름 스냅감만). 낮추면 커밋이 물러진다.")]
    [SerializeField, Range(0.8f, 2.4f)] float dashLaunchRate = 1.5f;
    [Tooltip("② 회피 실루엣/도약(클립 중간, 기본 n0.15~0.42) 재생 배속 — ★핵심 가독 구간. 몸이 회피방향으로 뻗은 정체성 포즈를 여기서 '읽히게' 잡아둔다. 낮을수록 더 또렷이 보임(1.0=자연속도, 0.9=살짝 붙잡음). 이게 '뭘 하는지 모르겠다'를 고치는 값.")]
    [SerializeField, Range(0.5f, 1.5f)] float dashFlightRate = 0.9f;
    [Tooltip("③ 착지/회수(클립 뒷부분, 기본 n0.42~0.95) 재생 배속 — 무게 회복(캔슬 가능). 정보량 낮아 너무 느리면 늘어진다. 1.1=살짝 빠르게 정리, 낮추면 착지가 묵직해지지만 늘어질 위험.")]
    [SerializeField, Range(0.5f, 1.8f)] float dashRecoverRate = 1.1f;
    [Tooltip("발구름→실루엣 경계(클립 정규화 진행도). 이 지점까지 dashLaunchRate, 이후 dashFlightRate. F 피크(n0.208) 앞에 둬 발구름만 빠르게 스냅.")]
    [SerializeField, Range(0.05f, 0.35f)] float dashLaunchEnd = 0.15f;
    [Tooltip("실루엣→회수 경계(클립 정규화 진행도). 이 지점까지 dashFlightRate(가독), 이후 dashRecoverRate. 4방향 공통 80%-이동 지점(n0.375~0.42) 근처.")]
    [SerializeField, Range(0.3f, 0.7f)] float dashFlightEnd = 0.42f;
    [Tooltip("배속 전환 스무딩(초) — 구간 경계 배속 급변 스터터 방지. 짧게.")]
    [SerializeField, Min(0f)] float dashRateDamp = 0.04f;
    [Tooltip("★R14c 이동 캔슬 지점(클립 정규화 진행도) — 이동키를 누른(홀드/신규 무관) 채 클립이 이 지점을 지나면 회수를 즉시 끊고 로코모션 복귀 " +
             "(\"회피 후 딜레이\" 제거). 무입력이면 이 값과 무관하게 착지 풀재생(exitTime 0.95, R13 '착지 읽힘' 유지). " +
             "★변위 구간(발구름+비행, ~n0.42 이전)은 못 끊게 dashFlightEnd 이상으로 둔다 — 낮추면 회피 거리 일관성이 깨진다. " +
             "낮을수록 이동 즉복귀(잔여 변위 더 드랍)·높을수록 거리 보존(복귀 늦음). 0.5=회수 초입(변위 ~88% 완료).")]
    [SerializeField, Range(0.42f, 0.9f)] float dashMoveCancelPoint = 0.5f;

    [Header("★상하체 완전 분리 (07-04 — SoD/RUINER식, 상체 공격이 하체에 무영향)")]
    // ★상체 레이어 웨이트 구동(07-04 위상 디싱크 확정 수정, Fix B): 두 로코모션 클럭(상체 UB_Loco·하체 Base Locomotion)이
    //   동시에 weight>0이 되는 상황을 아예 없앤다 — 걷기 정상상태에선 상체를 Base가 구동(weight 0)해 다리와 같은 클럭 = 위상 자동 정합.
    //     · Base가 상체 소유(액션/대시)      → 0 즉시 (Base 전신이 상체까지 보임, 위상 정합)
    //     · 콤보 진행                        → 1 즉시 (크리스프 진입)
    //     · 그 외(콤보 직후 복귀 + 걷기)      → 0으로 이즈아웃(comboLayerBlendTime) — 정적 콤보-끝-포즈 → Base 라이브 로코모션 블렌드
    //   콤보 종료 시 컨트롤러는 Combo 상태가 마지막 프레임을 홀드(Combo→UB_Loco 전이 제거)하고, 웨이트 이즈아웃이 그 *정적* 포즈를
    //   Base 로코모션으로 블렌드한다. 정적 포즈라 클럭이 안 끼어 위상 무관 = 디싱크 원천 소멸. 단일 블렌드라 복귀 튐도 없다.
    [Tooltip("★콤보→걷기 복귀 웨이트 이즈아웃 시간(초). 콤보 종료 후 상체 레이어를 이 시간에 걸쳐 1→0으로 내려 " +
             "정적 콤보-끝-포즈를 Base 라이브 로코모션으로 블렌드한다(단일 블렌드=튐 없음). 짧을수록 크리스프, 길수록 부드럽게 안착.")]
    [SerializeField] float comboLayerBlendTime = 0.12f;
    [Tooltip("★콤보 스텝인(Day2 '밀어넣고 멈춤' 전진 루트모션) 억제 — 완전 분리에선 기본 ON(true). " +
             "이유: 정지 콤보에서 다리가 idle 스탠스인데 루트모션이 전진시키면 발이 미끄러진다(브리프 §2). " +
             "★현 아키텍처(콤보=UpperBody 마스크 레이어, Root 제외)에선 콤보가 애초에 전진 루트모션을 delta에 싣지 않아 이 값과 무관하게 스텝인이 없다. " +
             "이 노브는 억제 경로를 명시적으로 남겨둔 것이며, Day2 스텝인의 진짜 부활은 콤보를 Root 포함 레이어로 되돌리는 컨트롤러 변경이 필요하다(그럴 경우 다리가 다시 콤보 골반을 따라간다).")]
    [SerializeField] bool suppressComboStepIn = true;

    public enum FacingMode { FaceMovement, FaceMouse, Hybrid, MoveAimTwist }
    [Header("Facing Mode (비교용 — 플레이 중 F키 순환)")]
    [Tooltip("몸이 뭘 보나: FaceMovement=이동 방향(하데스) / FaceMouse=마우스(트윈스틱) / Hybrid=질주는 이동·그 외는 조준 / " +
             "MoveAimTwist(07-14)=몸은 조향된 속도 방향(전방런 문법 — 원형 이동 자연화), 가슴만 조준으로 비틀기(아래 트위스트 노브). 직접 비교용.")]
    [SerializeField] FacingMode facingMode = FacingMode.Hybrid;
    [Tooltip("facingMode 순환 키.")]
    [SerializeField] KeyCode cycleFacingKey = KeyCode.F;

    [Header("★상체 조준 트위스트 (07-14 MoveAimTwist 전용 — 하체=이동 전방런, 가슴/척추만 조준을 가져간다)")]
    [Tooltip("가슴이 조준 쪽으로 비틀 수 있는 최대 각(도). 초과분은 포기 — 공격은 어차피 트리거 순간 facing이 조준으로 스냅(_lockedFace)이라 조준 정체성 유지. 0=트위스트 끔(FaceMovement와 동일해짐).")]
    [SerializeField, Range(0f, 90f)] float aimTwistMaxAngle = 60f;
    [Tooltip("트위스트 이즈 시간(초) — 0→최대각 도달. 짧을수록 가슴이 조준을 즉각 따라잡는다.")]
    [SerializeField] float aimTwistDamp = 0.1f;
    [Tooltip("트위스트의 Spine 배분 비율 — 잔여는 Chest/UpperChest 균등(한 관절 몰빵=허리 꺾임 방지, 체인 분산).")]
    [SerializeField, Range(0f, 1f)] float aimTwistSpineShare = 0.35f;

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int LocoRateHash = Animator.StringToHash("LocoRate");   // ★발속도 종속 — Locomotion 상태 Speed Multiplier(컨트롤러 파라미터 없으면 무해 no-op)
    static readonly int TurnRateHash = Animator.StringToHash("TurnRate");   // ★제자리 회전 발 셔플 — Locomotion Speed=0 노드의 중첩 TurnShuffle 1D 트리 구동(컨트롤러 파라미터 없으면 무해 no-op)
    static readonly int DashRateHash = Animator.StringToHash("DashRate");   // ★대시 리타임 — Dash 상태 Speed Multiplier(시작 빠름/착지 느림. 컨트롤러 파라미터 없으면 무해 no-op)
    static readonly int DashCancelHash = Animator.StringToHash("DashCancel");   // ★R14c 이동 캔슬 트리거 — Dash→Locomotion 하드컷(회수 구간, 이동 입력 시). 컨트롤러 파라미터 없으면 무해 no-op
    static readonly int ComboStepHash = Animator.StringToHash("ComboStep");
    static readonly int AttackHash = Animator.StringToHash("Attack");   // ★콤보 1단 진입 트리거(ANY→Combo1, CanTransitionToSelf) — 홀드된 Combo에서 재시작 가능
    static readonly int DashHash = Animator.StringToHash("Dash");
    static readonly int DashXHash = Animator.StringToHash("DashX");
    static readonly int DashYHash = Animator.StringToHash("DashY");
    // ★액션 트리거 해시(Counter/Skill01/SkillCharge/SkillCancel/DashAttack)는 더 이상 여기 하드코딩하지 않는다 —
    //   WeaponActionSet.animator(SO)가 이름을 소유하고 WeaponAnimatorData.Resolve()가 해시를 캐시(2026-07-05 슬롯화).

    Animator _animator;
    PlayerAim _aim;
    PlayerMotor _motor;
    Vector3 _lastPos;
    bool _wasDashing;
    bool _attacking;   // 공격 커밋 중 = 루트모션을 위치로 적용(PlayerBrain이 매 프레임 갱신)
    Vector3 _lockedFace;   // 콤보 단 시작 시 잠근 facing — 공격 중 몸/런지 방향 고정(단 사이엔 재캡처)
    int _comboLayer = -1;       // ★상체 콤보 오버라이드 레이어("UpperBodyCombo") 인덱스(컨트롤러에 없으면 -1 → 웨이트 구동 스킵, 무해)
    Quaternion _facingRot = Quaternion.identity;   // ★기울기 분리(07-11) — lean 미적용 순수 facing 상태. 스무딩은 이 값 기준(기울기가 회전 보간을 오염 방지)
    float _prevFacingYaw;       // ★제자리 회전 셔플 — 프레임 간 _facingRot yaw(도) 변화로 각속도 산출(회전 로직은 무변경, 관측만).
    Vector3 _leanVec;           // ★기울기 벡터 = 축×각도(도) — 각도/축을 분리 이즈하면 스트레이프 좌↔우 전환 시 축이 순간 반전해
                                //   몸이 탁 꺾인다(각짐). 벡터 공간에서 이즈하면 전환이 0을 지나며 부드럽게 미러링된다.
    bool _comboActive;          // ★콤보(평타) 진행 중인가 — 스텝인 억제 게이트의 소스(SetCombo가 설정). 반격/스킬/대시베기는 false(Base 전신).
                                //   ★웨이트 게이트는 더 이상 이 값이 아니라 "Base가 상체를 소유하는가"(LayerHasActionTag(0)‖IsDashing)로 구동(07-04 튐 수정).
    bool _suppressStepIn;       // ★스텝인 억제 스냅샷(Stab H-1) — Tick 시점에 확정. OnAnimatorMove가 라이브 _comboActive 대신 이걸 읽어,
                                //   같은 프레임 후반 애니 이벤트(OnComboEnd→SetCombo(0))가 게이트를 뒤집는 레이스 차단.
    float _comboWeight;         // ★상체 레이어 웨이트 상태(Fix B) — MoveTowards 이즈아웃이 프레임 간 값을 이어가려면 필요. SetLayerWeight의 최근값과 동치.
    Transform _spine, _chest, _upperChest;   // ★상체 조준 트위스트(07-14) — 휴머노이드 척추 체인 캐시(Awake). 없으면 null=그 본 스킵(있는 본이 몫 승계)
    float _aimTwistCur;         // 현재 상체 트위스트 각(도, +우/−좌) — LateUpdate가 이즈·적용(모드 밖/액션 중엔 0 복귀)
    float _prevVelYaw;          // ★곡선 스냅 완화(07-14) — 속도 방향 yaw(도) 프레임 추적(모터 조향의 연속 회전 관측)
    bool _velYawValid;          // 이동 재개 첫 프레임의 가짜 각속도 스파이크 방지(stale yaw 가드)
    float _velTurnRateSm;       // 속도 방향 각속도(도/초) 지수 스무딩 — 한 프레임 스파이크가 아니라 '지속 회전'만 곡선 판정

    /// <summary>공격 클립 타격 정점(AnimationEvent OnAttackHit)이 발화 → 무기가 구독해 판정.</summary>
    public event System.Action<int> AttackHit;
    /// <summary>캔슬 윈도우 시작(AnimationEvent OnComboWindow) — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public event System.Action ComboWindow;
    /// <summary>공격 클립 끝(AnimationEvent OnComboEnd) — 다음 단 안 갔으면 콤보 종료.</summary>
    public event System.Action ComboEnd;
    /// <summary>스윙 가속 시작(AnimationEvent OnSwishWhoosh) — 휘두름 소리를 칼날 스윕에 정렬(무기가 구독).</summary>
    public event System.Action SwishWhoosh;
    /// <summary>로코모션 클립 발 디딤(AnimationEvent OnFootstep) — PlayerFootsteps가 구독해 발소리.</summary>
    public event System.Action Footstep;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        // 공격 클립은 루트모션(클립에 박힌 거리만큼 전진) — deltaPosition을 받으려면 true.
        // 단 자동적용(자식만 이동)은 OnAnimatorMove 정의로 차단되고, 우리가 공격 중에만 루트(부모)에 수동 적용한다.
        // 로코모션 클립은 In_Place(루트≈0)라 비공격 프레임의 delta는 무시한다.
        _animator.applyRootMotion = true;
        // ★상하체 완전 분리: 상체 콤보 오버라이드 레이어 인덱스 캐시(이름으로 — 레이어 재정렬에 견고). 없으면 -1.
        _comboLayer = _animator.GetLayerIndex("UpperBodyCombo");
        // 배선 실패를 무음 강등으로 두지 않는다(_motor/_aim 정책과 일관, Stab M-1) — 레이어 못 찾으면 상체 콤보 오버라이드가 조용히 죽는다.
        if (_comboLayer < 0)
            Debug.LogError("[PlayerAnimatorDriver] UpperBodyCombo 레이어 못 찾음 — 상체 콤보 오버라이드 비활성.", this);
        // ★상체 조준 트위스트 본 캐시(07-14) — 휴머노이드 아바타 척추 체인. 리깅에 UpperChest가 없으면 null(있는 본이 몫 승계).
        //   isHuman 가드: 아바타 미설정/제네릭 리그에서 GetBoneTransform이 InvalidOperationException을 던져 Awake가
        //   통째로 중단(아래 _aim/_motor 배선까지 죽음) — 프로브로 실측된 엔진 현실 방어.
        if (_animator.isHuman)
        {
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
        }
        if (_spine == null && _chest == null && _upperChest == null)
            Debug.LogWarning("[PlayerAnimatorDriver] 척추 본을 못 찾음(휴머노이드 아바타 아님?) — MoveAimTwist 상체 조준 트위스트 비활성.", this);
        if (moveSource == null) moveSource = transform.parent;
        if (moveSource != null)
        {
            _aim = moveSource.GetComponentInParent<PlayerAim>();
            _motor = moveSource.GetComponentInParent<PlayerMotor>();
        }
        // 배선 실패를 무음으로 두지 않는다 — _motor 없으면 공격 루트모션이 조용히 죽으므로 즉시 노출(Codex).
        if (_motor == null)
            Debug.LogError("[PlayerAnimatorDriver] PlayerMotor를 부모 체인에서 못 찾음 — 공격 루트모션이 적용되지 않는다. 프리팹 계층(비주얼 자식이 PlayerMotor 하위인지) 확인.", this);
        // _aim도 대칭으로 노출(Stab 게이트) — 없으면 Hybrid facing이 조준을 못 써 transform.forward로 무음 폴백된다.
        if (_aim == null)
            Debug.LogError("[PlayerAnimatorDriver] PlayerAim을 부모 체인에서 못 찾음 — Hybrid facing이 조준 방향 대신 transform.forward로 폴백. 계층 확인.", this);
    }

    void OnEnable()
    {
        if (moveSource != null) _lastPos = moveSource.position;
        // ★기울기 분리 — 활성 시점 회전에서 *순수 yaw만* 재구성해 facing 기준으로(Stab M-3, 07-11):
        //   transform.rotation은 lean이 섞인 값일 수 있어(이동 중 재활성 경로) 그대로 캡처하면 기울기가 facing에 고착된다.
        Vector3 f0 = transform.forward; f0.y = 0f;
        _facingRot = f0.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f0.normalized, Vector3.up) : transform.rotation;
        _prevFacingYaw = _facingRot.eulerAngles.y;   // ★제자리 회전 셔플 각속도 기준 — 재활성 프레임에 가짜 각속도 스파이크 방지
    }

#if UNITY_EDITOR
    // 비교용 하니스 HUD — 현재 facing 모드 표시(고른 뒤 OnGUI/enum/순환 전부 제거). 에디터 전용(빌드 노출 차단).
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.cyan;
        string steer = _motor == null ? "?" : (_motor.SteerEnabled ? "ON(원호)" : "OFF(즉각)");
        GUI.Label(new Rect(14f, 10f, 1100f, 40f),
            $"[{cycleFacingKey}] Facing: {facingMode}   [{toggleSnap8Key}] 8방향스냅: {(snap8Way ? (snapRelaxTurnRate > 0f ? "ON·곡선완화" : "ON") : "OFF(연속)")}   [{toggleSteerKey}] 조향: {steer}", style);
    }
#endif

    /// <summary>PlayerBrain이 매 프레임 마지막에 호출. moveIntent = 이번 프레임 이동 입력(★R10 의도 채널 —
    /// Codex P0 최소 도입: 발/기울기/facing의 '방향'은 위치 결과 관측이 아니라 입력 의도를 즉시 표현한다.
    /// "발이 내가 누른 방향이 아니라 이미 밀려난 결과를 따라간다" = agency 손실의 코드 근거. 크기(Speed)는 실측 유지).</summary>
    public void Tick(Vector2 moveIntent = default)
    {
        if (moveSource == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 비교용 하니스: 플레이 중 facing 모드 순환 + 8방향 스냅 토글(고른 뒤 떼어낼 임시 코드)
#if UNITY_EDITOR
        if (Input.GetKeyDown(cycleFacingKey))
            facingMode = (FacingMode)(((int)facingMode + 1) % 4);   // ★07-14: MoveAimTwist 추가로 3→4 (기본 Hybrid에서 F 1번 = 신규 모드)
        if (Input.GetKeyDown(toggleSnap8Key))
            snap8Way = !snap8Way;
        if (Input.GetKeyDown(toggleSteerKey) && _motor != null)
            _motor.DebugToggleSteer();
#endif

        // 평면(XZ) 변위만 — 지면 추종 y 변화는 로코모션과 무관.
        Vector3 cur = moveSource.position;
        Vector3 delta = cur - _lastPos; _lastPos = cur; delta.y = 0f;

        // 스폰/텔레포트로 루트가 순간이동하면 가짜 변위로 방향이 튄다 — 기준만 리셋하고 스킵.
        float rawSpeed = delta.magnitude / dt;
        if (rawSpeed > maxSpeed * 3f) return;
        float speed = Mathf.Min(rawSpeed, maxSpeed);
        bool moving = speed > moveThreshold;

        // facing 3안 비교(F키 순환). 공격 중엔 항상 단 시작 잠근 조준(_lockedFace). 비주얼(this)만 회전 — 루트는 안 돈다.
        // ★폴백은 transform.forward가 아니라 순수 facing(_facingRot) 기준(Stab M-4, 07-11) — transform엔 lean이 섞여 있어
        //   face==0 극단 프레임에 기울어진 forward가 새어 들어가는 경로 차단(순수 facing 소스 단일화).
        // ★의도 채널(R10): 방향의 1차 소스 = 입력 의도(즉시), 폴백 = 위치 델타(외력 이동 — 넉백/글라이드).
        Vector3 intent3 = new Vector3(moveIntent.x, 0f, moveIntent.y);
        bool hasIntent = intent3.sqrMagnitude > 0.01f;
        if (hasIntent) intent3.Normalize();

        Vector3 pureFwd = _facingRot * Vector3.forward;
        Vector3 aimFace = (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f) ? _aim.Direction : pureFwd;
        Vector3 moveFace = hasIntent ? intent3 : (moving ? delta.normalized : pureFwd);
        Vector3 motorVel = _motor != null ? _motor.Velocity : Vector3.zero;   // ★07-14 — MoveAimTwist facing(궤적 접선)·곡선 스냅 완화가 공유
        Vector3 face;
        if (_attacking && _lockedFace.sqrMagnitude > 0.0001f)
            face = _lockedFace;
        else if (dashFaceMotion && _motor != null && _motor.IsDashing && _motor.DashDir.sqrMagnitude > 0.0001f)
            face = _motor.DashDir;   // ★R10 대시 커밋 — 몸=실제 대시 방향(자유 벡터). 시각=궤적 정합(하데스식)
        else if (facingMode == FacingMode.FaceMovement)
            face = moveFace;                                                     // 이동 방향(하데스식)
        else if (facingMode == FacingMode.FaceMouse)
            face = aimFace;                                                      // 마우스 조준(트윈스틱)
        else if (facingMode == FacingMode.MoveAimTwist)
        {
            // ★07-14 원형 이동 자연화: 몸=조향된 '속도' 방향(궤적 접선) — 원호에 몸이 정렬돼 '커브를 도는 전방런'이 된다.
            //   intent(8방향 생입력)를 그대로 보면 45° 계단이 몸 회전에 실린다 — 모터 조향(SteerVelocity)이 이미 둥글린
            //   방향을 쓴다(위치-몸 정렬 = 스트레이프 문법 탈출). 출발 첫 프레임(저속)은 intent 폴백(즉각성),
            //   정지 시엔 조준(기존 idle 계약 — 턴셔플이 회전을 소화). 가슴 조준은 LateUpdate 트위스트가 가져간다.
            Vector3 velDir = motorVel; velDir.y = 0f;
            face = velDir.sqrMagnitude > 0.25f ? velDir.normalized
                 : (hasIntent ? intent3 : aimFace);
        }
        else
            face = (_motor != null && _motor.IsSprinting) ? moveFace : aimFace; // 하이브리드: 질주=이동, 그 외=조준
        face.y = 0f;
        if (face.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(face.normalized, Vector3.up);
            // ★몸 회전 스무딩 — 이동 facing은 turnRate로 스르륵 돈다(WASD 8방향이라도 각진 스냅 제거).
            //   공격 잠금(_attacking)·대시 중엔 즉시 스냅(런지/회피 방향 크리스프 유지). faceTurnRate 크게=옛 즉시동작 복원.
            bool snap = _attacking || (_motor != null && _motor.IsDashing);
            // ★기울기 분리(07-11): 보간은 순수 facing(_facingRot) 기준 — lean이 회전 상태에 섞이면 매 프레임
            //   "기울기 되돌리기"에 회전 속도를 소모해 facing이 느려진다. lean은 Tick 끝에서 합성(아래).
            _facingRot = snap
                ? targetRot
                : Quaternion.RotateTowards(_facingRot, targetRot, faceTurnRate * dt);
            transform.rotation = _facingRot;
        }

        // movement = facing 프레임 투영 → MoveX(우측 스트레이프) / MoveY(전진 +, 뒷걸음 −)
        // ★투영 기준도 순수 facing(_facingRot) — lean 잔여치가 섞인 transform.forward 의존 제거(Stab M-4와 동일 소스 단일화).
        Vector3 fwd = _facingRot * Vector3.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);   // 좌수계: up×fwd = right
        // ★의도 채널(R10): 발 방향 = 입력 의도 즉시(위치 결과 관측 폐기). 입력 없을 때만 델타 폴백.
        Vector3 moveDir = hasIntent ? intent3 : (moving ? delta.normalized : Vector3.zero);
        float moveX = Vector3.Dot(moveDir, right);
        float moveY = Vector3.Dot(moveDir, fwd);

        // 8방향 스냅(딱딱) — 이동 방향을 가장 가까운 45도로 양자화해 블렌드트리에서 한 클립만 100% 활성한다.
        // 대각선 입력 시 F+R+FR 가중 블렌드로 애매해지던 것을 제거(이동 자체는 연속, 애니 방향만 8단계).
        // ★07-11 A/B: snap8Way OFF면 연속 방향 그대로(각짐 완화 실험 — 혼합 뭉갬 vs 부드러움은 유저 판정).
        // ★곡선 스냅 완화(07-14): 모터 조향이 속도 방향을 '지속 회전' 중(원호 카이팅)일 땐 45° 양자화가
        //   "위치는 둥근데 포즈는 계단"을 만든다(나+Codex 07-14 수렴 지적) — 그 동안만 연속 블렌드로 완화.
        if (moving && motorVel.sqrMagnitude > 0.25f)
        {
            float velYaw = Mathf.Atan2(motorVel.x, motorVel.z) * Mathf.Rad2Deg;
            if (!_velYawValid) { _prevVelYaw = velYaw; _velYawValid = true; }   // 이동 재개 첫 프레임 — 가짜 스파이크 방지
            float turn = Mathf.DeltaAngle(_prevVelYaw, velYaw) / dt;
            _prevVelYaw = velYaw;
            _velTurnRateSm = Mathf.Lerp(_velTurnRateSm, turn, 1f - Mathf.Exp(-dt / 0.08f));   // 지수 스무딩(τ=0.08s) — 지속 회전만 곡선 판정
        }
        else { _velYawValid = false; _velTurnRateSm = 0f; }
        bool curving = snapRelaxTurnRate > 0f && Mathf.Abs(_velTurnRateSm) >= snapRelaxTurnRate;
        if (moving && snap8Way && !curving)
        {
            float ang = Mathf.Atan2(moveX, moveY);                       // 이동 방향각(rad)
            ang = Mathf.Round(ang / (Mathf.PI / 4f)) * (Mathf.PI / 4f);  // 45도 단위 반올림
            moveX = Mathf.Sin(ang);
            moveY = Mathf.Cos(ang);
        }

        // speed 블렌드 0(idle)→1(walk)→2(run)
        float blend = speed <= walkSpeedRef
            ? (walkSpeedRef > 0f ? speed / walkSpeedRef : 0f)
            : 1f + (speed - walkSpeedRef) / Mathf.Max(0.01f, runSpeedRef - walkSpeedRef);
        blend = Mathf.Clamp(blend, 0f, 2f);

        _animator.SetFloat(SpeedHash, blend, speedDamp, dt);
        _animator.SetFloat(MoveXHash, moveX, dirDamp, dt);
        _animator.SetFloat(MoveYHash, moveY, dirDamp, dt);

        // ★발속도 종속(2026-07-11): 블렌드가 포화(스프린트 9/15/24 > runSpeedRef)해도 발이 지면 속도를 따라가게
        //   로코모션 재생율 = 실측속도 / 현재 블렌드의 애니 커버 속도. 상한 초과분(고티어)은 연출이 판다.
        //   기준 속도는 애니 진실(클립 루트모션 실측 4.31/×1.35) — walkSpeedRef/runSpeedRef도 씬에서 이 값에 정렬(발-참).
        float expected = blend <= 1f
            ? walkAnimSpeed * Mathf.Max(blend, 0.05f)
            : Mathf.Lerp(walkAnimSpeed, runAnimSpeed, Mathf.Clamp01(blend - 1f));
        float locoRate = moving ? Mathf.Clamp(speed / Mathf.Max(0.1f, expected), 1f, locoRateMax) : 1f;
        _animator.SetFloat(LocoRateHash, locoRate, locoRateDamp, dt);

        // ★제자리 회전 발 셔플(2026-07-11) — 정지 중 몸 yaw가 임계 이상으로 돌면 TurnShuffle 트리에 스트레이프를 부분 섞어
        //   '동상 회전'을 없앤다. yaw 각속도는 _facingRot(코드 소유 순수 facing)의 프레임 변화로 *관측만* 한다(회전 로직 무변경).
        //   이동/공격/대시/액션 중엔 0 — 로코모션·런지가 이미 방향을 소화(요구사항 #3). 이동 시엔 Speed>0라 트리 자체도 페이드아웃(이중 안전).
        float curFacingYaw = _facingRot.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(_prevFacingYaw, curFacingYaw) / dt;   // 도/초, 부호=회전 방향(+우/−좌)
        _prevFacingYaw = curFacingYaw;
        float turnTarget = 0f;
        bool turnShuffleAllowed = !moving && !_attacking && !(_motor != null && _motor.IsDashing) && !IsActionPlaying;
        if (turnShuffleAllowed)
        {
            float mag = Mathf.Abs(yawRate);
            if (mag > turnShuffleThreshold)   // 데드존 — 미세 조준 흔들림엔 발이 안 떨림(요구사항의 '임계')
            {
                float t = Mathf.Clamp01((mag - turnShuffleThreshold) / Mathf.Max(1f, turnRateRef - turnShuffleThreshold));
                turnTarget = Mathf.Sign(yawRate) * t * turnShuffleMax;
                if (turnShuffleInvert) turnTarget = -turnTarget;
            }
        }
        _animator.SetFloat(TurnRateHash, turnTarget, turnShuffleDamp, dt);

        // ★상하체 완전 분리 + 위상 디싱크 확정 수정(07-04 Fix B) — 상체 콤보 레이어(UpperBodyCombo, UpperBody 마스크)의 웨이트를
        //   세 갈래로 구동해, 두 로코모션 클럭(상체 UB_Loco·하체 Base Locomotion)이 동시에 weight>0이 되는 상황을 아예 없앤다.
        //     ① Base가 상체 소유(액션 태그 or 대시)  → 0 즉시. Counter/Skill01*/DashAttack/Dash가 상체까지 보이고, 위상은 Base 소유라 정합.
        //     ② 콤보 진행(_comboActive)             → 1 즉시. 베기 상체가 크리스프하게 켜진다.
        //     ③ 그 외(콤보 직후 복귀 + 걷기 정상상태) → 0으로 이즈아웃(comboLayerBlendTime). 컨트롤러가 Combo 상태를 홀드(Combo→UB_Loco
        //        전이 제거)하므로 이 이즈아웃은 *정적* 콤보-끝-포즈를 Base 라이브 로코모션으로 블렌드한다. 정적 포즈=클럭 없음=위상 무관.
        //   왜 위상 정합인가: 걷기 정상상태에서 weight가 0에 도달하면 상체를 Base가 구동(다리와 동일 클럭) → 팔-다리 위상 자동 일치.
        //   구버전(v3)은 걷기 중 weight를 상수 1로 둬 UB_Loco가 Base와 독립 클럭 → 콤보 직후 팔-다리 위상이 영구 어긋났다(디싱크). 이게 그 수정.
        //   P2b(Base 액션 종료 후 스냅): 액션 중 weight=0이었고 종료 후에도 ③이 MoveTowards(0→0)라 0 유지 → 0→1 스냅 없음 = UB_Loco 드리프트 위상 팝 없음.
        //   ⚠️Base는 콤보 중 Locomotion 상태에 머문다(컨트롤러가 Combo 상태를 상체 레이어로 이관 — Base엔 ComboStep 전이 없음).
        _suppressStepIn = _comboActive && suppressComboStepIn;   // ★프레임-초 스냅샷(Stab H-1) — 콤보 전체 스텝인 억제. 애니 이벤트가 _comboActive 뒤집어도 이번 프레임 게이트 불변.
        if (_comboLayer >= 0)
        {
            // LayerHasActionTag(0)은 이미 IsActionPlaying(busy/이동잠금)이 쓰는 검증된 술어 — Counter/Skill01*/DashAttack 전부 "Action" 태그.
            bool baseOwnsUpperBody = LayerHasActionTag(0) || (_motor != null && _motor.IsDashing);
            if (baseOwnsUpperBody)      _comboWeight = 0f;                 // ① Base 전신 소유 → 즉시 0(위상 정합)
            else if (_comboActive)      _comboWeight = 1f;                 // ② 콤보 진행 → 즉시 1(크리스프)
            else                        _comboWeight = Mathf.MoveTowards(  // ③ 복귀/걷기 → 0으로 이즈아웃(정적 콤보-끝-포즈 → Base 로코모션)
                                            _comboWeight, 0f, dt / Mathf.Max(0.0001f, comboLayerBlendTime));
            _animator.SetLayerWeight(_comboLayer, _comboWeight);
        }

        // 대시: 시작 엣지에서 방향(어느 Step 클립)을 잠그고, bool 엣지로 상태 진입/복귀를 구동한다.
        // DashX/DashY = facing 프레임의 카디널 방향(우=+X, 전진=+Y) → 블렌드트리가 한 Step 클립을 100% 고른다.
        // 한 동작=한 클립: 대시 진행 중 이 값은 고정(매 프레임 갱신 안 함) — 모션 정체성 보존.
        if (_motor != null)
        {
            bool dashStartedNow = _motor.DashStartedThisFrame;
            if (dashStartedNow)
            {
                if (dashFaceMotion)
                {
                    // ★R10(Codex 발견 — "몸은 대각선인데 발은 정방향 스텝"): 몸이 실제 대시 방향을 보고(위 facing 분기)
                    //   전진 스텝만 재생 → 자유 방향 전부 발=궤적 일치. L/R/B 스텝 클립은 이 모드에선 미사용.
                    _animator.SetFloat(DashXHash, 0f);
                    _animator.SetFloat(DashYHash, 1f);
                }
                else
                {
                    _animator.SetFloat(DashXHash, _motor.DashLocalX);
                    _animator.SetFloat(DashYHash, _motor.DashLocalY);
                }
                // ★대시=즉각 캐넌(Stab M-1): 대시 시작 프레임엔 위 웨이트 게이트가 이미 _motor.IsDashing=true를 읽어 상체 레이어를
                //   0으로 스냅한다(StartDash가 _dashActive 세팅→IsDashing(window-L) 즉시 true, 드라이버 Tick은 모터 뒤에 돈다). 별도 스냅 불필요.
                // ★대시 리타임 3단(2026-07-13 R14): 발구름 배속을 즉시 스냅(커밋 크리스프) — 실루엣 느림/회수 중간은 아래 nt 구동이 이어받는다.
                _animator.SetFloat(DashRateHash, dashLaunchRate);
                // ★R14c 이동 캔슬 트리거 위생: 직전 대시가 남긴 미소비 DashCancel이 있으면 새 대시가 프레임0에 즉시 캔슬될 위험 → 시작 시 소거.
                _animator.ResetTrigger(DashCancelHash);
                // ★재대시 리스타트: 직전 대시의 착지가 아직 Dash 상태로 재생 중이면(연속 대시), AnyState→Dash가 self-transition을 안 하므로
                //   (Dash bool이 0.15s 유지돼 CanTransitionToSelf=1이면 프레임0 동결 위험 → 컨트롤러는 0으로 둔다), 코드가 클립을 0에서
                //   재생해 새 회피 이젝션을 보여준다. 이는 상태 재진입(전환 트리거)이지 위치/포즈 창작이 아니다(헌법 부합). 최초 진입(로코모션→Dash)은
                //   IsName("Dash")가 false라 Play 안 하고 AnyState→Dash가 담당한다.
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Dash"))
                    _animator.Play("Dash", 0, 0f);
            }
            // ★Dash bool = 커밋 창(window-S=DashCommitted), NOT IsDashing(window-L)(R14 재진입 버그 수정): IsDashing은 클립 전체(~0.7s+grace)
            //   동안 true라, 그걸로 Dash bool을 구동하면 상태가 exitTime 0.95로 Locomotion에 빠진 뒤에도 bool이 true로 남아 AnyState→Dash가
            //   Locomotion에서 재발화 → 무한 재진입. 커밋 창(0.15s)은 진입 직후 꺼지므로(상태는 exitTime까지 재생) 옛 동작과 동일하게 1회 진입만 트리거.
            bool dashAnim = _motor.DashCommitted;
            if (dashAnim != _wasDashing) { _animator.SetBool(DashHash, dashAnim); _wasDashing = dashAnim; }

            // ★대시 리타임 3단 배속 구동(2026-07-13 R14) — Dash 상태에 있는 동안 클립 진행도(normalizedTime)로 세 구간 배속을 전환한다.
            //   nt < dashLaunchEnd = 발구름(빠름) / < dashFlightEnd = 회피 실루엣(느림, 가독) / 이후 = 착지·회수(중간). 시작 프레임은 위에서
            //   이미 발구름 배속으로 스냅했으므로 제외(Play 직후 stale nt로 뒤 구간 배속이 1프레임 새는 것 방지). Dash 상태 밖에선 DashRate 미변경.
            if (!dashStartedNow)
            {
                var dashState = _animator.GetCurrentAnimatorStateInfo(0);
                if (dashState.IsName("Dash"))
                {
                    float dnt = dashState.normalizedTime;
                    float rateTarget = dnt < dashLaunchEnd ? dashLaunchRate
                                     : dnt < dashFlightEnd ? dashFlightRate
                                     : dashRecoverRate;
                    _animator.SetFloat(DashRateHash, Mathf.Max(0.3f, rateTarget), dashRateDamp, dt);
                    // ★R14c 이동 캔슬(2026-07-13, 유저 "회피 후 딜레이 정리") — 변위 완료(회수 구간, dnt≥dashMoveCancelPoint) 후 이동 입력이 있으면
                    //   Dash→Locomotion 하드컷(DashCancel 트리거) + 변위 창(window-L) 즉시 종료(EndDashRoot)로 다음 프레임 이동 즉복귀.
                    //   무입력이면 발화 안 함 → 착지 풀재생(exitTime 0.95)=R13 '착지 읽힘' 유지. 변위 구간(<dashFlightEnd)은 cancelPoint 하한(0.42)이 막는다.
                    //   홀드/신규 무관: hasIntent를 매 프레임 보므로 회수 중 새로 눌러도 즉시 컷(자연스러움). 포즈 팝은 플레이어 하드컷 캐넌(DMC식).
                    if (dnt >= dashMoveCancelPoint && hasIntent && _motor != null)
                    {
                        _animator.SetTrigger(DashCancelHash);
                        _motor.EndDashRoot();
                    }
                }
            }
        }

        // ★달리기 기울기(07-11) — 몸 전체를 이동 방향으로 살짝 눕혀 속도를 실루엣으로 판다(하이포니 레퍼 문법).
        //   포즈(관절)는 안 건드린다 — 비주얼 루트 전체 틸트 한 번(애니가 진실 유지). 공격/대시 중엔 0(크리스프).
        //   축 = Cross(up, 이동방향): 8방향 스트레이프 전부 "가는 쪽으로 눕기"가 된다. 합성은 facing 위에 곱(분리 상태라 보간 무오염).
        bool leanBlocked = _attacking || (_motor != null && _motor.IsDashing);
        if (leanBlocked)
            _leanVec = Vector3.zero;   // 하드컷 — 런지/회피 방향은 1프레임도 기울어지지 않는다(크리스프 캐넌)
        else
        {
            // ★벡터 이즈(07-11 각짐 픽스): 목표 = 축(이동방향 수평직교)×각도. 각도/축 분리 이즈는 스트레이프
            //   좌↔우 전환에서 축이 순간 반전해 몸이 탁 꺾였다 — 벡터로 이즈하면 0을 지나며 부드럽게 미러링.
            Vector3 leanTarget = Vector3.zero;
            if (moving && leanMaxAngle > 0f && moveDir.sqrMagnitude > 0.0001f)
            {
                Vector3 ax = Vector3.Cross(Vector3.up, moveDir);
                if (ax.sqrMagnitude > 0.0001f)
                    leanTarget = ax.normalized * (leanMaxAngle * Mathf.Clamp01(speed / Mathf.Max(1f, leanFullSpeed)));
            }
            // 이동률 = 최대각/이즈시간 — 축 반전(최대 2×leanMaxAngle 거리)도 같은 속도로 통과.
            _leanVec = Vector3.MoveTowards(_leanVec, leanTarget, (leanMaxAngle / Mathf.Max(0.01f, leanDamp)) * dt);
        }
        float leanMag = _leanVec.magnitude;
        if (leanMag > 0.01f)
            transform.rotation = Quaternion.AngleAxis(leanMag, _leanVec / leanMag) * _facingRot;
        // ★달리기 무기 처리: 별도 코드 없음 — run 티어(Speed=2)는 walk 티어와 동일한 S2_Run 8way(무기 OUT) 세트를
        //   m_TimeScale 1.35로 더 빠르게 돌린다(스프린트=빠른 런 + 발슬라이드 완화). 무기 상태가 walk와 통일되어 임계서
        //   칼 깜빡임 없음. (구 Run_Stance3 단일 클립은 loop=0이라 스프린트 중 1회 후 freeze → 폐기.)
    }

    /// <summary>★상체 조준 트위스트(07-14, MoveAimTwist 모드) — Animator 포즈 평가 *후*(LateUpdate) 척추 체인에 yaw를 가산.
    /// 하체/궤적은 이동 방향(전방런 문법 = 원형 이동 자연화)을 보고, 가슴만 조준을 가져가 조준형 정체성을 유지한다
    /// (07-14 나+Codex 독립 진단 수렴 — '조준형 스트레이프 문법'이 원형 부자연의 1원인). 포즈 창작이 아니라 기존 포즈 위
    /// 오프셋(lean과 같은 절차 후처리 계열 — 애니가 진실 유지). 액션/대시/콤보 중엔 0 복귀(facing 잠금이 조준을 이미 소유 — 크리스프 캐넌).</summary>
    void LateUpdate()
    {
        float target = 0f;
        if (facingMode == FacingMode.MoveAimTwist && aimTwistMaxAngle > 0f
            && !_attacking && !(_motor != null && _motor.IsDashing) && !_comboActive && !IsActionPlaying
            && _aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
        {
            Vector3 fwd = _facingRot * Vector3.forward;   // 순수 facing 기준(lean 오염 없는 소스 — M-4와 동일 단일화)
            target = Mathf.Clamp(Vector3.SignedAngle(fwd, _aim.Direction, Vector3.up),
                                 -aimTwistMaxAngle, aimTwistMaxAngle);
        }
        // 이동률 = 최대각/이즈시간(lean과 동형) — 모드 전환/액션 진입 시에도 같은 속도로 부드럽게 0 복귀(스냅 없음).
        _aimTwistCur = Mathf.MoveTowards(_aimTwistCur, target,
            (Mathf.Max(30f, aimTwistMaxAngle) / Mathf.Max(0.01f, aimTwistDamp)) * Time.deltaTime);
        if (Mathf.Abs(_aimTwistCur) < 0.01f) return;

        // 배분: Spine=aimTwistSpineShare, 잔여는 Chest/UpperChest 균등. 없는 본의 몫은 있는 본이 승계(합계 항상 100%).
        int chestCount = (_chest != null ? 1 : 0) + (_upperChest != null ? 1 : 0);
        if (_spine == null && chestCount == 0) return;   // 척추 체인 전무(Awake에서 경고) — 무동작
        float spineShare = _spine != null ? (chestCount > 0 ? aimTwistSpineShare : 1f) : 0f;
        float chestShare = chestCount > 0 ? (1f - spineShare) / chestCount : 0f;
        if (_spine != null)      _spine.Rotate(Vector3.up, _aimTwistCur * spineShare, Space.World);
        if (_chest != null)      _chest.Rotate(Vector3.up, _aimTwistCur * chestShare, Space.World);
        if (_upperChest != null) _upperChest.Rotate(Vector3.up, _aimTwistCur * chestShare, Space.World);
    }

    /// <summary>콤보 단 설정(0=idle, 1/2/3) — AnimatorController가 ComboStep으로 Combo 상태를 전환한다.
    /// 단 시작(step≥1) = 이 단의 facing 잠금 지점(이후 마우스를 돌려도 몸/런지 방향은 이 순간 조준에 고정).</summary>
    public void SetCombo(int step)
    {
        _comboActive = step >= 1;   // ★상하체 분리 게이트의 단일 소스 — 콤보만 하체 오버라이드/스텝인-억제 대상(반격/스킬/대시베기 제외)
        if (step >= 1)
        {
            if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
                _lockedFace = _aim.Direction;
        }
        else _lockedFace = Vector3.zero;   // 콤보 종료 — 잔존 잠금 제거(self-cancel 경로에서 옛 방향 재사용 방지)
        _animator.SetInteger(ComboStepHash, step);
        // ★콤보 1단 진입 = Attack 트리거로 ANY→Combo1 발화(컨트롤러 CanTransitionToSelf=1). Combo→UB_Loco 전이를 제거해
        //   콤보 종료 시 상체 레이어가 직전 Combo 상태의 마지막 프레임을 홀드하므로, 다음 콤보는 그 홀드된 상태에서 자기 자신으로
        //   재진입해야 한다(단발-후-단발이 흔한 경로). ComboStep==1 int 조건은 재생 내내 참이라 CanTransitionToSelf와 함께 쓰면
        //   매 프레임 재발화(프레임0 동결)하므로, 소비형 트리거를 쓴다(1회 발화 후 자동 소진). 1단에서만 발화 — 연계 2/3단은
        //   ComboStep==2/3 전이가 담당하고, BeginCombo가 SetCombo(1)을 콤보 시작에만 호출하므로 재생 중 재발화가 없다.
        if (step == 1) _animator.SetTrigger(AttackHash);
    }

    /// <summary>공격 커밋 여부 — true인 프레임에만 루트모션 변위를 루트로 적용(PlayerBrain이 busy로 갱신).</summary>
    public void SetAttacking(bool attacking) => _attacking = attacking;

    /// <summary>★레일: 지금 '액션' 클립(공격/반격)이 실제로 재생 중인가 — busy의 단일 진실 소스(애니가 진실).
    /// 컨트롤러에서 Combo1/2/3·Counter 상태에 "Action" 태그를 단다. 새 무기/액션은 상태에 같은 태그만 달면
    /// 코드 수정 없이 busy/잠금에 자동 편입(OCP 확장점). 전이 중이면 다음 상태도 봐서 진입 1프레임 갭을 메운다.
    /// ★REQUIRED: 모든 '공격/반격' 액션 상태에 Animator State Tag "Action" 필수. 누락 시 유예(0.12s) 뒤 busy가
    ///   풀려 이동 누수 — 단 KatanaWeapon의 진입 실패 자가치유가 에디터 경고로 잡아준다(런타임 안전망).</summary>
    public bool IsActionPlaying
    {
        get
        {
            if (_animator == null) return false;
            // Base 레이어(0)=Counter/Skill01*/DashAttack(전신 액션, 끝나면 Locomotion으로 전이 → 태그가 정확히 풀린다).
            if (LayerHasActionTag(0)) return true;
            // 상체 콤보 레이어=Combo1/2/3. ★Fix B로 Combo→UB_Loco 전이를 제거해 콤보 종료 후 Combo 상태를 마지막 프레임에
            //   홀드하므로, 태그만 보면 busy가 영구 true가 된다 → '실제 재생 중'일 때만 busy로 친다(홀드=끝=busy 아님).
            if (_comboLayer >= 0 && ComboLayerActivelyPlaying()) return true;
            return false;
        }
    }

    /// <summary>한 레이어의 현재(또는 전이 중 도착) 상태가 "Action" 태그인가 — 요청→진입 갭의 busy 누수 방지.</summary>
    bool LayerHasActionTag(int layer)
    {
        if (_animator.GetCurrentAnimatorStateInfo(layer).IsTag("Action")) return true;
        if (_animator.IsInTransition(layer) && _animator.GetNextAnimatorStateInfo(layer).IsTag("Action")) return true;
        return false;
    }

    /// <summary>★상체 콤보 레이어가 지금 '재생 중'인가 — 홀드된(끝난)·중단된(캔슬된) Combo는 제외(Fix B).
    /// Combo→UB_Loco 전이를 제거해 콤보 종료/캔슬 후 Combo 상태가 강제 종료 없이 남으므로, Action 태그만으로는
    /// busy가 영구/장시간 true가 된다. 구 설계는 Combo→UB_Loco 전이가 태그를 풀어 busy를 해제했고, 이 메서드가 그 역할을 대체한다.
    /// 두 겹으로 판정한다:
    ///  ① _comboActive 게이트(Stab P0) — 대시캔슬/자가캔슬이 스윙 도중 Cancel()→SetCombo(0)으로 _comboActive를 끈다.
    ///     이 게이트가 없으면 중단된 클립이 자연 종료(normalizedTime≥1)될 때까지 busy가 붙어 대시 직후 이동이 얼어붙는다
    ///     (대시캔슬=이 무기의 코어 캐넌 경로). _comboActive가 콤보 논리 종료(정상 종료·캔슬 공통)를 정확히 반영한다.
    ///  ② normalizedTime 안전망 — _comboActive가 (이벤트 누락 등으로) 고착돼도 Animator가 끝났으면(≥1) busy 해제 → KatanaWeapon 자가치유 발화.</summary>
    bool ComboLayerActivelyPlaying()
    {
        if (!_comboActive) return false;   // ① 논리 종료(정상/캔슬 공통) — 대시캔슬 busy-freeze 방지
        var cur = _animator.GetCurrentAnimatorStateInfo(_comboLayer);
        if (cur.IsTag("Action") && cur.normalizedTime < 1f) return true;   // ② 진행 중(마지막 프레임 홀드 전) — 고착 시 안전망
        if (_animator.IsInTransition(_comboLayer) && _animator.GetNextAnimatorStateInfo(_comboLayer).IsTag("Action")) return true;  // 진입 전이 중
        return false;
    }

    /// <summary>★액션 진입(반격/스킬/대시베기 공통) — 데이터 주도 트리거(2026-07-05 슬롯화). 트리거 이름은
    /// <see cref="WeaponActionSet"/>.animator(SO)가 소유하고 해시는 Resolve 캐시 — 구 TriggerCounter/TriggerSkill/
    /// TriggerDashAttack의 일반화(동작 보존). 액션은 공격이라 IsBusy(=_attacking)로 루트모션이 적용된다(별도 게이트 불필요).
    /// 몸 facing을 현재 조준에 잠가 히트박스 _aimDir과 통일(콤보 SetCombo의 잠금과 동형). 종료 시 SetCombo(0)이 잠금 해제.
    /// ★AnyState 경쟁 해소: 직전 대시의 Dash bool이 남아 있으면 Any→Dash(우선순위 0)가 액션 전이를 이긴다 —
    /// 요청 시 Dash bool을 즉시 꺼 확실히 진입(엣지 추적도 동기화). hash 0(이름 미설정)이면 무동작(안전).</summary>
    public void TriggerAction(int triggerHash)
    {
        if (_animator == null || triggerHash == 0) return;
        if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            _lockedFace = _aim.Direction;
        _animator.SetBool(DashHash, false);
        _wasDashing = false;
        _animator.SetTrigger(triggerHash);
    }

    /// <summary>★차징 윈드업(RMB 누름) — 구 TriggerSkillCharge의 일반화. 윈드업(프레임 0→70)을 재생한 뒤 프레임 70
    /// (차징완료 포즈)에서 홀드한다. 발동(베기)은 릴리스 때 <see cref="TriggerAction"/>이 별도로 낸다(윈드업 재재생 없음).
    /// facing 잠금 + Dash bool 정리(AnyState 경쟁 해소)는 TriggerAction과 동형.
    /// cancelHash 잔류 가드: 대시-취소가 쏜 취소 트리거가 AnyState→Dash에 밀려 미소비로 남으면 새 차징을 즉시
    /// 취소시킴 → 새 차징 전 클리어. chargeHash 0이면 무동작(안전).</summary>
    public void TriggerCharge(int chargeHash, int cancelHash)
    {
        if (_animator == null || chargeHash == 0) return;
        if (_aim != null && _aim.Direction.sqrMagnitude > 0.0001f)
            _lockedFace = _aim.Direction;
        _animator.SetBool(DashHash, false);
        _wasDashing = false;
        if (cancelHash != 0) _animator.ResetTrigger(cancelHash);
        _animator.SetTrigger(chargeHash);
    }

    /// <summary>★차징 취소(최소차징 전 릴리스 = 불발 / 하드컷 복귀) — 구 TriggerSkillCancel의 일반화.
    /// 컨트롤러 Charge/Hold→Locomotion 트리거로 윈드업/홀드만 idle로 되돌린다(베기 미발동). 차징 시작이 윈드업
    /// 트리거를 쐈는데 불발 시 홀드에 고착되는 소프트락을 막는다. hash 0이면 무동작(안전).</summary>
    public void TriggerActionCancel(int cancelHash)
    {
        if (_animator == null || cancelHash == 0) return;
        _animator.SetTrigger(cancelHash);
    }

    /// <summary>★트리거 파라미터 실존 검증(Stab H-1, 07-05) — 이름은 있는데 컨트롤러에 파라미터가 없으면
    /// SetTrigger가 경고만 내고 무시되는데 쿨다운은 이미 소진(무음 자원 소각). 무기 초기화가 슬롯별로 검증해
    /// 즉시 에러 노출(무음 강등 금지 정책). 판정 불가 상황(Animator/컨트롤러 미준비)은 true(오탐 에러 방지).</summary>
    public bool HasTrigger(int hash)
    {
        if (_animator == null) _animator = GetComponent<Animator>();   // Awake 순서 경합 대비 지연 해석
        if (_animator == null || _animator.runtimeAnimatorController == null) return true;
        foreach (var p in _animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.nameHash == hash) return true;
        return false;
    }

    /// <summary>★차징 윈드업(Skill01Charge) 재생 중인가 — ChargePhantomEmitter가 *윈드업에만* 팬텀 방출하도록 읽음(홀드/베기 제외).
    /// Base Layer(0) 현재 상태명으로 판정. 액션 진입 전이가 전부 CUT(0)이라 윈드업 0→70 동안만 true, 프레임70 홀드 진입 시 false.</summary>
    public bool IsInSkillChargeWindup => _animator != null && _animator.GetCurrentAnimatorStateInfo(0).IsName("Skill01Charge");

    /// <summary>공격·대시 클립의 루트모션을 루트(PlayerMotor)로 넘긴다 — 애니가 진실(전진/회피 거리는 클립이 소유).
    /// 공격 커밋 중이거나 대시 창 동안만 적용한다. 그 외(In_Place 로코모션, delta≈0)는 무시해
    /// PlayerMotor 이동과 충돌하지 않는다. 자동 루트모션은 비주얼 자식만 옮기므로 쓰지 않고, 이 수동 경로로
    /// 부모 루트를 옮긴다. 위치 단일 소유는 PlayerMotor.ApplyRootStep(같은 프레임 이중 적용 가드 내장).</summary>
    void OnAnimatorMove()
    {
        if (_motor == null) return;
        bool dashing = _motor.IsDashing;
        if (!_attacking && !dashing) return;   // 공격 커밋 or 대시 변위 창(window-L) 동안만 루트모션을 위치로 적용
        var st = _animator.GetCurrentAnimatorStateInfo(0);
        // ★차징 윈드업/게이더링 홀드는 제자리 — 루트모션 억제. 이유 둘: ①게이더링 홀드(Skill01Hold)는 매 루프
        //   사이클마다 클립을 전진 재생해 루트모션이 누적(실측 ~0.26m/s, 홀드 3s=0.78m 슬라이드)되므로 차단해야
        //   "기 모으며 제자리"가 된다. ②윈드업(Skill01Charge) 제자리화로 팬텀 출발점이 고정된다.
        //   클립은 윈드업/홀드/베기 공유라 bake로 분리 불가 → 상태별 억제(코드가 위치를 *만들지* 않고 클립 변위를 *억제만* — 헌법 부합).
        //   베기(Skill01Strike) 런지는 정상 적용(아래 경로). 이름 판정은 기존 IsInSkillChargeWindup과 동형 결속.
        if (st.IsName("Skill01Charge") || st.IsName("Skill01Hold")) return;
        // ★R14 대시 루트모션 피드(2026-07-13, 이동-시각 분열 수정) — Dash 클립 재생 동안 그 변위(F=3.27m)를 위치로 넘긴다(옛 코드 버스트 폐기).
        //   변위 창(window-L)은 애니가 소유: 여기서 KeepDashActive로 매 프레임 갱신하고, 상태가 Dash를 벗어나면 ping이 끊겨 모터 grace가 만료.
        //   deltaPosition은 DashRate(3단 배속)로 스케일되므로 '배속이 변위 속도 프로파일을 함께 만든다'(포즈=변위 동기 → 디싱크 불가). 공격보다 먼저 판정.
        bool inDash = st.IsName("Dash") ||
                      (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName("Dash"));
        if (inDash)
        {
            _motor.KeepDashActive();                        // 애니 진실이 변위 창을 소유(코드는 따라감)
            _motor.ApplyRootStep(_animator.deltaPosition);  // 위치=클립 델타(벽가드+지면은 공격과 동일 ApplyRootStep 파이프)
            return;
        }
        // ★상하체 완전 분리(07-04 v2): 콤보 스텝인(전진 루트모션) 억제 — 완전 분리에선 정지 콤보 다리가 idle 스탠스라
        //   전진 루트모션이 발을 미끄러뜨린다(브리프 §2). 그래서 콤보 전체 스텝인 억제(_suppressStepIn = _comboActive && suppressComboStepIn).
        //   ★단 현 아키텍처(콤보=UpperBody 마스크 레이어, Root 제외)에선 콤보가 애초에 delta에 전진을 싣지 않아 억제 여부와 무관하게 delta≈0.
        //   반격/스킬/대시베기(_comboActive=false)는 Base 전신(Root 포함)이라 그 커밋 런지가 정상 적용된다.
        //   ★스냅샷 판독(Stab H-1) — 라이브 _comboActive를 읽으면 종료 프레임 애니 이벤트(OnComboEnd)가 게이트를 풀어
        //   마지막 프레임 루트 delta가 새어 나간다(ClearActionMove와 거울상 레이스).
        if (_attacking)
        {
            if (_suppressStepIn) return;
            _motor.ApplyRootStep(_animator.deltaPosition);
        }
    }

    // ── AnimationEvent 수신(함수명 고정 — Animation 에이전트가 이 이름으로 클립에 심는다) ──
    /// <summary>타격 정점 — 무기 판정으로 릴레이.</summary>
    public void OnAttackHit(int hitFrameIndex) => AttackHit?.Invoke(hitFrameIndex);
    /// <summary>캔슬 윈도우 시작 — 다음 콤보 단 입력이 먹히기 시작.</summary>
    public void OnComboWindow() => ComboWindow?.Invoke();
    /// <summary>공격 클립 끝 — 다음 단 안 갔으면 콤보 종료(idle 복귀).</summary>
    public void OnComboEnd() => ComboEnd?.Invoke();
    /// <summary>스윙 가속 시작 프레임 — 휘두름 사운드로 릴레이(소리가 칼날과 함께 정점).</summary>
    public void OnSwishWhoosh() => SwishWhoosh?.Invoke();
    /// <summary>로코모션 클립 발 디딤 프레임 — 발소리로 릴레이(디바운스는 구독자가).</summary>
    public void OnFootstep() => Footstep?.Invoke();
}
