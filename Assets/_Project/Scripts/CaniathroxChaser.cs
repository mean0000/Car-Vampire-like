// Caniathrox 추격 드라이버 — 군중 근접 AI(steering + separation + surround + attack token)로 재설계(v2).
//
// ════════ 헌법 (불가침 — CaniathroxAttackDemo와 동일 원칙) + ★재해석 1건 ════════
//   제0원칙: 정체성 동작(도약·물기·스핏) 재생 중엔 그 클립만 돈다. crossfade로 두 동작을 뭉개지 않는다.
//            → 상태머신(CaniathroxAttack.controller)이 강제한다. 이 드라이버는 컨트롤러를 건드리지 않는다.
//   제1원칙: 공격은 상태 시퀀스다. IdleAngry → Approach(Run_RM 루트모션 접근) → [도착·토큰]
//            → ★거리 분기: 가까우면 Bite(BiteForward_RM, 전진 1.33m) / 멀면 Coil(응축) →[ExitTime 자동발사]→
//              Lunge(JumpLunge_RM, 전진 4.67m·Y억제, 낮게 깔린 돌진) → [Exit] → IdleAngry.
//            각 단계 = 1클립 = 1상태, 순차·완결. "모았다가(Coil) 팍(Lunge)" 타이밍 대비는 state speed(0.6/1.3)가 만든다.
//            (Spit은 이번 분기에서 제외 — 원거리용 보존)
//   제2원칙: 애니메이션이 진실. 위치·궤적·포즈는 전부 클립 루트모션(applyRootMotion=true)이 만든다.
//            이 드라이버는 모션(위치/포즈)을 만들지 않는다.
//
//   ★헌법 재해석(오케스트레이터 판단): Approach(로코모션) + Coil(응축=발사 전 조준) 중 회전은 허용한다.
//     회전은 위치/포즈가 아니라 *방향*(AI 의도)이다. 접근하려면 방향 조정이 당연하고(루트모션=전진, 코드 회전=방향),
//     Coil은 발사 직전의 *조준* 단계라 플레이어 미래 위치(예측)로 겨냥하는 게 자연스럽다(Approach steering의 연장).
//     ┗ 단 Lunge/Bite/Spit 진행 중엔 회전 절대 0 — 예측 조준된 방향으로 직선 발사, 궤적 보존(제2원칙).
//       예측은 Coil까지, 발사(Lunge)는 고정. "이 프레임에 두 클립이 섞이나"가 아니라 "발사 궤적을 코드가 휘나"가 경계.
//
// ════════ 군중 AI 4기법 (웹 리서치 — 표준) ════════
//   1) Steering(seek)  : Approach 중 매 프레임 타겟 방향으로 turnSpeed(도/초)만큼 부드럽게 회전 → 곡선 추적.
//   2) Separation      : 근처 동료(separationRadius 내)를 회피하는 가중을 seek 방향에 더함 → 호드가 안 겹치고 퍼짐.
//   3) Surround        : 플레이어 직타격이 아니라 인스턴스별 각도 슬롯(slotAngleDeg)으로 분산된 지점을 노림 → 포위.
//   4) Attack Token    : 공유 토큰 풀 점유한 적만 Lunge 발동, 나머진 접근만 → 6마리가 동시에 안 덤빔.
using System.Collections.Generic;
using UnityEngine;

public class CaniathroxChaser : MonoBehaviour
{
    // ── 분리(separation) 계산용 전역 로스터 — 활성 추격자 전부를 가볍게 순회 ──
    static readonly List<CaniathroxChaser> Roster = new List<CaniathroxChaser>();

    // Enter Play Mode Options(도메인 리로드 off)에서 static이 세션 간 잔존 → Play→Stop→Play 시
    // 파괴된 추격자 참조가 Roster에 누적된다(Stab+Codex 합의). 플레이 진입마다 1회 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStaticState() => Roster.Clear();

    [Header("참조 (스포너가 와이어링)")]
    [Tooltip("Caniathrox 인스턴스(Animator 보유, 루트모션이 이걸 움직인다).")]
    public Transform model;
    [Tooltip("상태머신 재생 주체(applyRootMotion=true 강제).")]
    public Animator modelAnimator;
    [Tooltip("추적 대상 = 플레이어 Transform.")]
    public Transform target;
    [Tooltip("CaniathroxAttack.controller (스포너가 할당).")]
    public RuntimeAnimatorController attackController;
    [Tooltip("공유 공격 토큰 풀 — 스포너가 모든 Chaser에 같은 참조 주입(이게 동시 공격 수를 제한).")]
    public AttackTokenPool tokenPool;
    [Tooltip("★장판 텔레그래프 공유 풀(스포너 주입) — Coil(응축=조준) 진입 시 전방 부채꼴 예고(돌진 방향·도달거리). null이면 장판 생략(모션만).")]
    public TelegraphPool telegraphPool;

    [Header("접근/공격 판정 (피하기 난이도 노브)")]
    [Tooltip("도착 판정 거리(m). 이 거리 안에 들고 토큰을 획득하면 공격을 발동한다. 발동 시 아래 biteRange로 물기/도약을 가른다.")]
    [SerializeField] float lungeRange = 5.0f;
    [Tooltip("★거리 분기 임계(m). 도착 시 플레이어가 이 거리보다 가까우면 물기(Bite, 전진 1.33m), 멀면 도약(Lunge, 전진 4.67m). 점프가 가까운 플레이어를 지나쳐 뒤로 착지하던 오버슈트를 푼다.")]
    [SerializeField] float biteRange = 2.5f;
    [Tooltip("Approach(Run_RM) 접근 이동 속도(m/s). 플레이어 걷기(5.5)보다 빠르고 질주(9.0)보다 느리게(시작값 7.0). Run_RM 네이티브 4.094m/s에 배율을 걸어 맞춘다(발도 비례 가속 → 미끄러짐 최소).")]
    [SerializeField] float approachSpeed = 7.0f;
    [Tooltip("공격 1사이클 끝나고 다시 접근하기까지의 정지 시간(초). 윈드업 호흡 + 공격 쿨다운.")]
    [SerializeField] float restBeforeApproach = 0.6f;
    [Tooltip("IdleAngry 정지 중 플레이어가 이 거리 안이면 다시 Approach 발동(추격 재개 트리거). 0 이하면 항상 추격.")]
    [SerializeField] float chaseRange = 0f;

    [Header("Steering — 곡선 추적 회전 (군중 AI 1)")]
    [Tooltip("Approach 중 타겟 방향으로 회전하는 최대 속도(도/초). 크면 빠르게 휘어 쫓고(피하기 어려움), 작으면 굼떠 옆으로 빠지기 쉽다.")]
    [SerializeField] float turnSpeed = 360f;

    [Header("예측 조준 — 움직이는 플레이어 요격 (Coil 단계 한정)")]
    [Tooltip("Coil(응축) 중 플레이어의 이동 방향·속도를 읽어 '도착할 위치'를 선행 조준하는 시간(초). predicted = 플레이어위치 + 평활속도 × leadTime. " +
             "크면 더 앞을 노려 빠른 플레이어를 요격(과하면 헛다리), 작으면 현재 위치에 가까움. 시작값 0.5(Coil 0.42s + Lunge 전진 일부). " +
             "★예측은 Coil(조준)에서만 — Lunge(발사) 진입 후엔 회전 0, 조준된 방향으로 직선 발사(궤적 보존, 제2원칙).")]
    [SerializeField] float leadTime = 0.5f;
    [Tooltip("플레이어 속도 평활 계수(초) — SmoothDamp 프레임 노이즈를 깎아 예측이 떨지 않게. 작을수록 즉각(흔들림↑), 클수록 안정(반응 지연).")]
    [SerializeField, Min(0.01f)] float velocitySmoothTime = 0.12f;

    [Header("Separation — 동료 회피 (군중 AI 2)")]
    [Tooltip("이 반경(m) 안의 동료를 회피 가중에 넣는다. 크면 더 넓게 퍼진다.")]
    [SerializeField] float separationRadius = 2.5f;
    [Tooltip("분리 회피가 seek 방향에 섞이는 강도(0=무시, 1=동등). 크면 서로 안 겹치되 추적이 산만해진다.")]
    [Range(0f, 3f)] [SerializeField] float separationWeight = 1.0f;

    [Header("Surround — 포위 슬롯 (군중 AI 3)")]
    [Tooltip("플레이어 둘레에서 이 적이 파고들 슬롯 거리(m). 플레이어 중심에서 이만큼 떨어진 링 위 한 점을 노린다.")]
    [SerializeField] float surroundRadius = 1.6f;
    [Tooltip("스포너가 인스턴스별로 분배하는 슬롯 각도(도). 0이면 정면, 분산되면 측면·후방 포위. 스포너가 주입.")]
    [SerializeField] float slotAngleDeg = 0f;

    [Header("★텔레그래프 (읽기 — Coil 진입 시 돌진 예고 부채꼴. 유저 ▶ 가독성/톤 판정)")]
    [Tooltip("부채꼴 사거리(m) = 돌진 도달거리 예고. Lunge 전진(4.67m)+근접을 덮게 시작값 5.5(lungeRange 근처).")]
    [SerializeField] float telegraphRadius = 5.5f;
    [Tooltip("부채꼴 전각(도, 1~180). 돌진은 직선이라 좁게 — 방향이 또렷이 읽히되 약간의 조준 오차 폭. 시작값 50.")]
    [SerializeField, Range(1f, 180f)] float telegraphAngleDeg = 50f;
    [Tooltip("★채움 시간(초) = Coil(응축) 실시간 길이. JumpCoil 0.167s native ÷ Coil state speed 0.4 ≈ 0.42s(가득=Lunge 발사 근사). Coil→Lunge가 ExitTime 자동이라 AnimationEvent 없음 → 추정 채움. 유저가 튜닝.")]
    [SerializeField, Min(0.05f)] float telegraphFillDuration = 0.42f;
    [Tooltip("가득 찬 뒤(=발사) 잔상 유지(초). 발사 순간을 눈으로 잡게 짧게.")]
    [SerializeField, Min(0f)] float telegraphHold = 0.12f;
    [Tooltip("적 장판 색 = 레드-오렌지 단색(색 캐넌 §5). Crassorrid와 동일 톤.")]
    [SerializeField] Color telegraphColor = new Color(1f, 0.30f, 0.08f, 1f);
    [Tooltip("장판 마스터 알파(전체 진하기). Crassorrid 차용 0.85.")]
    [SerializeField, Range(0f, 1f)] float telegraphAlpha = 0.85f;
    [Tooltip("채움(안쪽) 알파. Crassorrid 차용 0.55.")]
    [SerializeField, Range(0f, 1f)] float telegraphFillAlpha = 0.55f;
    [Tooltip("외곽선(예고선) 알파. Crassorrid 차용 1.0.")]
    [SerializeField, Range(0f, 1f)] float telegraphEdgeAlpha = 1.0f;
    [Tooltip("외곽선 두께(월드 m). Crassorrid 차용 0.18.")]
    [SerializeField, Min(0f)] float telegraphEdgeWorld = 0.18f;

    [Header("★히트리액트 (스태거/플린치 — 카타나에 맞으면 GetHit 상태. 빈도/무게 = 유저 ▶ 판정)")]
    [Tooltip("켜면 피격 시 GetHit(플린치) 상태로 끊고 들어간다(끊고-베기). 끄면 피격해도 플래시만(애니 반응 없음).")]
    [SerializeField] bool enableHitReact = true;
    [Tooltip("★포이즈(경직치) 임계 — 누적 피해가 이 값 이상이면 스태거 발동 후 0으로 리셋. " +
             "작게=가벼운 타격에도 자주 휘청(연속 스턴락 위험 ↑·무게감 ↑), 크게=한두 방은 버티고 큰/누적 타격에만 휘청(공격할 틈 ↑). " +
             "enemyHp=4·카타나 ~1dmg 기준 시작값 2(약 2타마다 1회). 1로 낮추면 매 타격, 99로 올리면 사실상 무경직.")]
    [SerializeField, Min(1)] int poiseThreshold = 2;
    [Tooltip("스태거 1회 후 다음 스태거까지 최소 간격(초) — 연타로 영구 경직락 되는 것을 막는 안전판. " +
             "0이면 임계만으로 제어(쿨다운 없음). 시작값 0.6(플린치 길이 ~0.625s 근처라 끝나자마자 또 휘청 방지).")]
    [SerializeField, Min(0f)] float staggerCooldown = 0.6f;
    [Tooltip("★발동(Lunge)·물기(Bite) *실행 중*에는 경직 면역(하이퍼아머)인가. " +
             "false(기본)=발사 중에도 끊김 → 윈드업(Coil)뿐 아니라 돌진 자체도 베어 멈출 수 있음(플레이어 강함·끊고베기 극대). " +
             "true=일단 발사하면 끝까지 간다(공격이 위협으로 완결, 플레이어가 윈드업에 반응해야 함). 손맛 판정은 유저.")]
    [SerializeField] bool staggerImmuneDuringStrike = false;

    // ── ★장판 텔레그래프 활성 추적 (gen 세대 가드 — Crassorrid 패턴. 풀 회수로 주인 바뀌면 stale 조작 차단) ──
    TelegraphPad _activePad;
    int _activeGen = -1;
    bool _coilSpawned;   // ★이 Coil 진입에 장판 이미 스폰했나(엣지 가드 — Coil 아니면 false 자가치유, 매 Coil 1회).

    // 애니 파라미터 — 컨트롤러(CaniathroxAttack.controller)와 공유.
    static readonly int PApproach = Animator.StringToHash("isApproaching");
    static readonly int PAttack   = Animator.StringToHash("attack");   // → Coil(응축) →[ExitTime]→ Lunge(JumpLunge_RM, Y억제)
    static readonly int PBite     = Animator.StringToHash("bite");     // → Bite(BiteForward_RM)
    static readonly int PGetHit   = Animator.StringToHash("getHit");   // → GetHit(GetHitBack 플린치, 제자리·하드컷). AnyState에서 끊고 진입.
    static readonly int SIdle    = Animator.StringToHash("IdleAngry");
    static readonly int SApp     = Animator.StringToHash("Approach");
    static readonly int SCoil    = Animator.StringToHash("Coil");      // 응축(조준) — 예측 yaw 허용 구간. Lunge(발사)는 회전 0.
    static readonly int SGetHit  = Animator.StringToHash("GetHit");    // 플린치 — 이 상태 동안 드라이버는 아무것도 안 한다(애니만).
    static readonly int SLunge   = Animator.StringToHash("Lunge");     // 발사(JumpLunge_RM) — 하이퍼아머 판정용.
    static readonly int SBite    = Animator.StringToHash("Bite");      // 물기(BiteForward_RM) — 하이퍼아머 판정용.

    // Run_RM 루트모션 네이티브 속도(2.4565m / 0.600s). Animator 스텝 실측값(2026-06-13).
    // approachSpeed를 이 값으로 나눈 배율을 Approach 상태에서만 modelAnimator.speed에 적용.
    const float RunNativeSpeed = 4.0942f;

    // 사이클 1회성 가드
    bool _attackFired;     // 이 사이클에 attack 트리거를 쐈나(도착→도약)
    float _restTimer;      // 휴지 카운트다운(IdleAngry 진입 후)
    bool _approaching;     // isApproaching 상태 미러
    bool _holdsToken;      // 이 적이 현재 공격 토큰을 점유 중인가(Lunge 사이클 동안 true)

    // ── 히트리액트(스태거) 상태 ──
    EnemyDamageReceiver _receiver;   // 같은 GO의 피격 수신기(OnDamaged 구독 → 끊고-베기 플린치). 풀 재활용 대비 OnEnable에서 (재)구독.
    bool _subscribed;                // 중복 구독/누수 가드(OnEnable이 여러 번 불려도 1회만).
    int _poise;                      // 누적 피해(경직치). poiseThreshold 넘으면 스태거 발동 후 0.
    float _staggerCdTimer;           // 다음 스태거까지 남은 쿨다운(초). >0이면 임계 넘어도 발동 보류.

    // ── 예측 조준용 플레이어 속도 추적 ──
    LabPlayerController _targetPlayer;   // target에 붙은 컨트롤러(있으면 PlanarVelocity 직접 사용). 없으면 위치델타로 추정.
    Vector3 _smoothedTargetVel;          // 평활된 플레이어 속도(예측에 쓰는 값) — 프레임 노이즈 제거
    Vector3 _velSmoothRef;               // SmoothDamp 내부 상태
    Vector3 _lastTargetPos;              // 폴백 추정용 직전 타겟 위치
    bool _hasLastTargetPos;              // 첫 프레임 가드(델타 폭발 방지)

    void Awake()
    {
        if (modelAnimator == null)
        {
            Debug.LogError("[CaniathroxChaser] modelAnimator 미할당 — 상태머신 재생 불가");
            enabled = false; return;
        }
        if (attackController != null) modelAnimator.runtimeAnimatorController = attackController;
        else Debug.LogError("[CaniathroxChaser] attackController 미할당 — CaniathroxAttack.controller 로드 실패");
        modelAnimator.applyRootMotion = true;   // ★루트모션이 캐릭터를 움직인다(제2원칙)

        ResetCycle();
    }

    void OnEnable()
    {
        if (!Roster.Contains(this)) Roster.Add(this);
        SubscribeReceiver();   // ★풀 재활용 대비: 매 활성마다 (재)구독. 내부 가드로 중복 방지.
        // 재활용 시 경직 상태 리셋(이전 생애의 포이즈/쿨다운이 새 적에 새지 않게).
        _poise = 0;
        _staggerCdTimer = 0f;
        // ★풀 재활용 대비: 직전 생애에 큐됐다가 비활성으로 미소비된 getHit 트리거를 비운다(재활성 즉시 엉뚱한 플린치 방지).
        if (modelAnimator != null) modelAnimator.ResetTrigger(PGetHit);
    }
    void OnDisable()
    {
        Roster.Remove(this);
        UnsubscribeReceiver(); // ★구독 대칭(누수 방지) — 비활성/파괴/사망 모두 여기로.
        ReleaseToken();      // 비활성/파괴 시 점유 토큰 누수 방지
        CancelTelegraph();   // ★시전 중 비활성/파괴(특히 사망) → 차오르던 장판 즉시 취소 + _coilSpawned 리셋(Crassorrid 동형).
    }

    // ════════ 히트리액트 구독 — 같은 GO의 EnemyDamageReceiver.OnDamaged ════════
    //   코드는 *상태 전환만* 한다(애니가 진실, 제2원칙). 위치/포즈/넉백을 코드가 만들지 않는다.
    void SubscribeReceiver()
    {
        if (_subscribed) return;
        if (_receiver == null && model != null) _receiver = model.GetComponent<EnemyDamageReceiver>();
        if (_receiver == null) _receiver = GetComponent<EnemyDamageReceiver>();
        if (_receiver != null)
        {
            _receiver.OnDamaged += OnDamaged;
            _subscribed = true;
        }
    }
    void UnsubscribeReceiver()
    {
        if (_receiver != null) _receiver.OnDamaged -= OnDamaged;
        _subscribed = false;
        // _receiver 참조는 유지(재활성 시 재구독에 재사용) — 컴포넌트가 같은 GO라 안정.
    }

    // ════════ 피격 콜백 — 포이즈 누적 → 임계+쿨다운 통과 시 스태거 발동(끊고-베기) ════════
    //   ★사망 타격 가드(중요): 수신기는 OnDamaged를 *Die() 전에* 쏜다(이때 _dead는 아직 false). 따라서 IsDead가
    //     아니라 *이미 차감된* Hp<=0으로 "이 타격이 치명타인가"를 본다. 치명타면 GetHit 안 함(곧 SetActive(false)될
    //     시체에 플린치 트리거 = 다음 재활용에 stale 트리거 잔존 위험). IsDead는 이전 프레임 사망 잔여 콜백 방어.
    void OnDamaged(int damage, Vector3 from)
    {
        if (!enableHitReact || modelAnimator == null) return;
        if (_receiver != null && (_receiver.IsDead || _receiver.Hp <= 0)) return;   // 치명타·시체는 GetHit 금지.
        // ★이미 GetHit(플린치) 중이면 무시 — 쿨다운값과 무관하게 연쇄 스태거·큐된 트리거 퍼마락 차단(Stab+Codex 수렴).
        if (modelAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == SGetHit) return;

        _poise += Mathf.Max(1, damage);   // 0데미지 히트도 최소 1 누적(영원히 안 쌓이는 것 방지).
        if (_poise < poiseThreshold) return;          // 아직 경직 한계 미달 — 플래시만(수신기가 이미 처리).
        if (_staggerCdTimer > 0f) return;             // 쿨다운 중 — 누적은 유지, 발동만 보류.

        // 하이퍼아머: 발사(Lunge)/물기(Bite) *실행 중* 면역 옵션(켜졌을 때만). 윈드업(Coil)·접근은 항상 끊긴다.
        if (staggerImmuneDuringStrike)
        {
            var st = modelAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (st == SLunge || st == SBite) return;   // 발사 중엔 안 끊고 통과(공격 완결 — 누적은 유지).
        }

        TriggerStagger();
    }

    // 스태거 발동 = 진행 중 동작을 *하드 컷*으로 끊고 GetHit 진입(AnyState 전환·duration 0).
    //   ★제0원칙: crossfade로 두 동작을 뭉개지 않는다 — 컨트롤러의 AnyState→GetHit가 m_TransitionDuration=0(컷).
    //   코드는 트리거만 세우고, 현재 동작의 부산물(토큰/장판/접근/사이클)을 정리한다(모션은 안 만듦).
    void TriggerStagger()
    {
        _poise = 0;
        _staggerCdTimer = staggerCooldown;

        // 진행 중 공격의 잔재 정리 — 안 하면 GetHit 후 IdleAngry로 돌아와도 _attackFired/_holdsToken이 stale.
        SetApproaching(false);
        CancelTelegraph();      // 차오르던 Coil 장판 즉시 취소(이미 깐 약속을 끊고-베기로 무효화 = 공정: 베면 예고도 사라짐).
        ReleaseToken();         // 점유 토큰 반납 — 끊겼으니 다른 적이 공격할 차례.
        ResetCycle();           // _attackFired=false·휴지 타이머 재무장. 위치는 안 건드림(루트모션 0 클립이라 제자리 플린치).
        modelAnimator.speed = 1f;  // 혹시 Approach 배율이 남아있던 프레임이면 즉시 정상화(공격/플린치는 네이티브).

        modelAnimator.ResetTrigger(PAttack);   // 큐된 공격 트리거가 GetHit 직후 곧장 재발동하는 것 방지.
        modelAnimator.ResetTrigger(PBite);
        modelAnimator.ResetTrigger(PGetHit);   // ★동일 프레임 2히트 시 트리거 2개 스택→이중 GetHit 방지(Stab) — 멱등.
        modelAnimator.SetTrigger(PGetHit);      // → AnyState→GetHit 하드컷 진입.
    }

    // 차오르던 장판을 즉시 풀로 반납(gen 가드 — 이미 회수돼 남의 것이면 무시). Crassorrid.CancelTelegraph 동형.
    void CancelTelegraph()
    {
        if (_activePad != null) _activePad.CancelImmediate(_activeGen);
        _activePad = null;
        _activeGen = -1;
        _coilSpawned = false;   // ★Stab M-1: 독립 호출(향후 인터럽트 경로)에서도 다음 Coil 장판이 무음 스킵되지 않게 함께 리셋.
    }

    // ════════ ★텔레그래프 스폰 — Coil(응축) 진입 1회. 전방 부채꼴(돌진 방향·도달거리)을 Coil 동안 채운다. ════════
    //   ★헌법: 텔레그래프는 가독성 VFX다 — 위치/포즈/회전 안 건드린다(Coil 예측 조준 로직은 그대로). 부채꼴은 *스폰 시점*
    //     model.forward(=현재 조준)에 깔린다. Coil 중 예측 yaw로 몸이 더 돌아도 장판은 안 따라 돈다(이미 깐 약속 = 공정).
    //   채움(fillDuration) = Coil 실시간 — 가득 차는 순간이 Lunge 발사 근사(Coil→Lunge ExitTime 자동, 이벤트 없음 → 추정).
    void SpawnCoilTelegraph()
    {
        if (telegraphPool == null) return;   // 풀 미주입 → 장판 생략(Coil 모션만으로도 "응축" 가독 보완).
        var pad = telegraphPool.Acquire();
        if (pad == null) return;

        Vector3 fwd = model.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();
        Vector3 origin = model.position; origin.y = 0f;

        pad.SpawnFan(origin, fwd, telegraphRadius, telegraphAngleDeg,
                     telegraphFillDuration, telegraphHold,
                     telegraphColor, telegraphAlpha, telegraphFillAlpha, telegraphEdgeAlpha, telegraphEdgeWorld);
        _activePad = pad;
        _activeGen = pad.Gen;
    }

    // 사이클 시작 — ★위치 리셋 없음(추격은 루트모션이 데려간 자리 유지). 휴지 타이머만 재무장.
    void ResetCycle()
    {
        SetApproaching(false);
        _attackFired = false;
        _restTimer = restBeforeApproach;
    }

    void SetApproaching(bool v)
    {
        _approaching = v;
        if (modelAnimator != null) modelAnimator.SetBool(PApproach, v);
    }

    // 스포너가 슬롯 각도를 주입(인스턴스별 포위 분배). 인스펙터 노출값을 코드로 덮어쓰는 진입점.
    public void SetSurroundSlot(float angleDeg) => slotAngleDeg = angleDeg;

    void ReleaseToken()
    {
        if (_holdsToken && tokenPool != null) tokenPool.Release();
        _holdsToken = false;
    }

    // ════════ 포위 슬롯 목표점 — 플레이어 직타격이 아니라 둘레 링 위 한 점 ════════
    //   슬롯 각도는 "현재 이 적이 플레이어를 보는 방위"를 기준으로 회전 → 적마다 다른 측면/후방으로 파고든다.
    Vector3 SlotTargetPoint()
    {
        Vector3 p = target.position; p.y = 0f;
        if (surroundRadius <= 0.001f) return p;
        Vector3 toMe = model.position - target.position; toMe.y = 0f;
        // 이 적이 플레이어를 보는 방위에서 slotAngleDeg만큼 돌린 지점 = 이 적의 포위 슬롯.
        Vector3 baseDir = toMe.sqrMagnitude > 0.0001f ? toMe.normalized : model.forward;
        Vector3 slotDir = Quaternion.AngleAxis(slotAngleDeg, Vector3.up) * baseDir;
        return p + slotDir * surroundRadius;
    }

    // ════════ Steering 합성 방향 — seek(슬롯 향) + separation(동료 회피) ════════
    Vector3 SteerDirection()
    {
        Vector3 me = model.position; me.y = 0f;

        // seek: 포위 슬롯을 향하는 방향
        Vector3 seek = SlotTargetPoint() - me; seek.y = 0f;
        Vector3 seekDir = seek.sqrMagnitude > 0.0001f ? seek.normalized : model.forward;

        // separation: 반경 내 동료에서 멀어지는 방향(거리 가까울수록 강하게)
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
                    sep += away.normalized * (1f - Mathf.Sqrt(d2) / separationRadius);   // 가까울수록 ↑
            }
        }

        Vector3 composite = seekDir + sep * separationWeight;
        return composite.sqrMagnitude > 0.0001f ? composite.normalized : seekDir;
    }

    // ════════ 플레이어 속도 추적 — 매 프레임 평활(예측 조준의 입력) ════════
    //   우선순위: target에 LabPlayerController가 있으면 그 PlanarVelocity(이미 SmoothDamp된 값)를 한 번 더 평활.
    //   없으면(임의 Transform 타겟) 위치 델타/dt로 추정 후 평활. 어느 쪽이든 _smoothedTargetVel에 수렴.
    void TrackTargetVelocity()
    {
        Vector3 raw;
        // 컨트롤러 캐시(타겟은 스포너가 Awake 이후 주입할 수 있어 지연 해석).
        if (_targetPlayer == null) _targetPlayer = target.GetComponent<LabPlayerController>();

        if (_targetPlayer != null)
        {
            raw = _targetPlayer.PlanarVelocity;   // 이미 한 겹 평활된 평면 속도
        }
        else
        {
            // 폴백: 위치 델타/dt. 첫 프레임은 0(스폰 순간 위치점프가 가짜 속도가 되는 것 방지).
            Vector3 pos = target.position; pos.y = 0f;
            raw = (_hasLastTargetPos && Time.deltaTime > 0.0001f)
                ? (pos - _lastTargetPos) / Time.deltaTime
                : Vector3.zero;
            _lastTargetPos = pos;
            _hasLastTargetPos = true;
        }
        raw.y = 0f;
        // 한 겹 더 평활 — SmoothDamp 잔떨림/방향전환 스파이크를 깎아 예측 방향이 펄럭이지 않게.
        _smoothedTargetVel = Vector3.SmoothDamp(_smoothedTargetVel, raw, ref _velSmoothRef,
                                                velocitySmoothTime, Mathf.Infinity, Time.deltaTime);
    }

    // 예측(선행) 조준 지점 = 플레이어 현재 위치 + 평활속도 × leadTime. 선형 lead(게임 표준 — 2차 intercept는 과함).
    Vector3 PredictedTargetPoint()
    {
        Vector3 p = target.position; p.y = 0f;
        return p + _smoothedTargetVel * leadTime;
    }

    // ════════ Update — 상태머신이 모션을 돌리고, 이 드라이버는 조건/추적/조향만 ════════
    void Update()
    {
        if (modelAnimator == null) return;
        // ★속도 단일 진실원: 매 프레임 1f(공격 클립 네이티브)로 리셋하고, Approach 브랜치에서만 배율로 올린다.
        //   이로써 Approach 이탈 경로(target/model null, Lunge/Bite/Idle 진입 등) 전부에서 배율이 새지 않는다(Codex 지적).
        modelAnimator.speed = 1f;
        if (_staggerCdTimer > 0f) _staggerCdTimer -= Time.deltaTime;   // 스태거 쿨다운 카운트다운(상태 무관 상시).
        if (target == null || model == null) return;
        TrackTargetVelocity();   // 매 프레임 플레이어 속도 평활 — Coil 예측 조준의 입력(상태 무관 상시 추적).
        var info = modelAnimator.GetCurrentAnimatorStateInfo(0);

        // ── GetHit(플린치) 중: ★제0원칙 — 이 동작만 돈다. 드라이버는 조향·조준·공격 무엇도 하지 않는다(애니가 완결). ──
        //   루트모션 0 클립이라 위치도 안 움직인다(제자리 휘청). GetHit→IdleAngry는 컨트롤러 ExitTime이 자동 처리.
        if (info.shortNameHash == SGetHit) return;

        // ★텔레그래프 엣지 가드(Crassorrid 패턴): Coil이 *아닐* 때 _coilSpawned=false → Coil 진입 첫 프레임에만 1회 스폰.
        //   Coil을 건너뛰는 비정상 경로(Bite 분기 등)에서 stale true 잔존으로 다음 장판이 무음 스킵되는 것까지 자가치유.
        if (info.shortNameHash != SCoil) _coilSpawned = false;

        // ── 휴지(IdleAngry) → 접근 시작 / 추격 재개 ──
        if (info.shortNameHash == SIdle)
        {
            ReleaseToken();   // ★공격 사이클 완결(Lunge/Bite→IdleAngry) → 토큰 반납. 다음 적이 획득.

            if (_attackFired)            // 직전 사이클이 공격까지 끝났다 → 새 사이클 리셋(위치는 안 건드림)
            {
                ResetCycle();
            }
            else
            {
                _restTimer -= Time.deltaTime;
                if (_restTimer <= 0f && !_approaching && PlayerInChaseRange())
                {
                    FaceSteer();          // 접근 직전 1회 정렬(정지 상태) — 이후 Approach에서 매 프레임 조향
                    SetApproaching(true); // → Approach(Run_RM) 진입
                }
            }
        }
        // ── 접근 중(Approach): ★매 프레임 steering 회전(헌법 재해석 허용 지점) + 도착·토큰 시 거리분기 공격 ──
        else if (info.shortNameHash == SApp)
        {
            // ★접근 속도: Run_RM 네이티브(4.094m/s)에 배율을 걸어 approachSpeed(m/s)로. 발도 비례 가속(미끄러짐 최소).
            //   Approach 상태에서만 올린다 — 다른 상태/이탈 경로는 Update 맨 위 단일 리셋이 1f로 되돌린다.
            modelAnimator.speed = approachSpeed / RunNativeSpeed;

            // steering: seek+separation 합성 방향으로 turnSpeed만큼 부드럽게 회전 → 루트모션 전진이 곡선이 됨.
            Vector3 dir = SteerDirection();
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
                model.rotation = Quaternion.RotateTowards(model.rotation, want, turnSpeed * Time.deltaTime);
            }

            // 도착 + 토큰 획득 시에만 공격. 토큰 없으면 접근 유지(서성거림) — 동시 공격 제한.
            if (!_attackFired && ArrivedAtTarget())
            {
                if (_holdsToken || (tokenPool != null && tokenPool.TryAcquire()))
                {
                    if (tokenPool != null) _holdsToken = true;   // 풀 없으면(폴백) 토큰 개념 무시하고 그냥 공격
                    _attackFired = true;
                    SetApproaching(false);             // 다음 사이클까지 접근 끔
                    modelAnimator.speed = 1f;          // 공격 클립은 네이티브 속도(배율 누수 방지)

                    // ★거리 분기: 가까우면 물기(Bite), 멀면 도약(Lunge). 둘 다 토큰 필요·컷 진입·상태머신이 완결.
                    if (PlanarDistanceToTarget() < biteRange)
                        modelAnimator.SetTrigger(PBite);   // → Bite(BiteForward_RM) 컷 진입
                    else
                        modelAnimator.SetTrigger(PAttack); // → Coil(응축) →[ExitTime]→ Lunge(JumpLunge_RM) 컷 진입
                }
                // 토큰 못 얻음 → 아무것도 안 함. Approach 유지하며 슬롯 주위를 계속 조향·서성.
            }
        }
        // ── Coil(응축) 중: ★예측 조준 허용 구간 — 발사 전 *조준* 단계라 회전 OK(헌법 경계). ──
        //   플레이어의 미래 위치(predicted)를 향해 yaw를 부드럽게 돌린다 = "기 모으며 도착할 자리를 겨냥".
        //   ExitTime CUT으로 Coil이 끝나는 순간, 이 조준된 방향 그대로 Lunge가 직선 발사된다(궤적 보존, 제2원칙).
        //   ┗ 회전만(modelAnimator.speed/위치/포즈는 안 건드림). Coil은 제자리(루트모션 0)라 회전이 위치를 안 만듦.
        else if (info.shortNameHash == SCoil)
        {
            // ★Coil 진입 1회: 전방 부채꼴 장판 스폰(돌진 방향·도달거리 예고). 스폰은 *현재* forward에 깔리고,
            //   아래 예측 yaw로 몸이 더 돌아도 장판은 안 따라 돈다(이미 깐 약속 = 공정성). 회전/위치/포즈는 안 건드림.
            if (!_coilSpawned) { _coilSpawned = true; SpawnCoilTelegraph(); }

            Vector3 toPredicted = PredictedTargetPoint() - model.position; toPredicted.y = 0f;
            if (toPredicted.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(toPredicted.normalized, Vector3.up);
                model.rotation = Quaternion.RotateTowards(model.rotation, want, turnSpeed * Time.deltaTime);
            }
        }
        // ── Lunge/Bite 중: ★회전 0 엄수(궤적 보존). 속도는 위 단일 리셋이 1f 유지(공격 클립 네이티브). 상태머신이 완결. ──
        //   이 드라이버는 정체성 동작 중 아무것도 하지 않는다. 예측 조준은 Coil에서 끝났고, 발사는 고정 방향.
    }

    // 정지 상태(IdleAngry)에서 접근 직전 머리 방향을 steering 합성 방향으로 1회 정렬.
    void FaceSteer()
    {
        Vector3 dir = SteerDirection();
        if (dir.sqrMagnitude > 0.0001f)
            model.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    // 도착 판정: 플레이어와의 평면 거리 ≤ lungeRange. (위치를 읽는 것 = 조건, 위치를 만드는 것 아님)
    bool ArrivedAtTarget()
    {
        return PlanarDistanceToTarget() <= lungeRange;
    }

    // 플레이어와의 평면(XZ) 거리(m) — 도착 판정·거리 분기(bite vs lunge)에 공용.
    float PlanarDistanceToTarget()
    {
        Vector3 a = model.position, b = target.position; a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 추격 재개 거리 판정 — chaseRange 0 이하면 항상 추격.
    bool PlayerInChaseRange()
    {
        if (chaseRange <= 0f) return true;
        Vector3 a = model.position, b = target.position; a.y = b.y = 0f;
        return Vector3.Distance(a, b) <= chaseRange;
    }
}
