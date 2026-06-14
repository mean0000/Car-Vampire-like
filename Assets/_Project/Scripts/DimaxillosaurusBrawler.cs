// Dimaxillosaurus 근접 격투 드라이버 — ★"벽처럼 오는 클로월"(유저 디렉팅 2026-06-14): 멀리서 발견 → 그 자리에서 포효(오프너 1회)
//   → 좌/우 단발 클로 L→R→L→R 무한 교대가 곧 이동수단(별도 달리기/걷기 접근 없음). 클로 클립 루트모션(각 2.22m 전진)이 몬스터를
//   플레이어 쪽으로 데려간다. 디스인게이지 없음 — 항상 플레이어 쪽으로 클로질하며 따라온다(끊임없는 벽). 장판 텔레그래프 없음(Dimax 전용 제거,
//   가독성 = 보이는 클로 윈드업 + 포효가 담당). 공정 탈출 = 클로 전진이 느려 플레이어가 발로 앞섬(벽은 계속 네 위치로 클로질).
//
// ★Caniathrox 추격(돌진/도약)·Venodonte 사수(원거리)와 다른 세 번째 틀 = "포효 후 좌우 단발 교대 클로월(전진=공격)".
//
// ════════ 헌법 (불가침 — 애니 에이전트 3원칙) ════════
//   제0원칙: 정체성 동작(단발 클로) 재생 중엔 그 클립만 돈다. crossfade로 뭉개지 않는다.
//            → 상태머신(DimaxillosaurusBrawler.controller)이 강제(정체성 전이=CUT dur0). 단발 1회 = 완결 후 다음 손.
//            ★단발은 Windup(0~9)+Strike(9~16)+FollowOut(16~22)+Recovery(22~35) 네 구간으로 분할 — *같은 동작의 분할*이라
//             구간 경계 포즈가 비트-동일(연속). CUT여도 포즈 점프 0 = crossfade 아님(헌법 준수).
//   제1원칙: 공격은 상태 시퀀스다.
//            오프너:  Idle →(타깃 인지 즉시)→ Roar(앵티시페이션·1회) → L_Windup(첫 단발 진입).
//            ★★단발 시퀀스(2026-06-14 "끊임없는 좌우" 개정): L_Windup →[CUT 연속]→ L_Strike →[CUT]→ L_FollowOut →[CUT]→ L_Recovery
//                    →[CUT, Idle 우회]→ R_Windup →…→ L_Windup (Roar·Idle 둘 다 생략, *무봉제* 무한 교대).
//                    ★(구)Recovery→Idle→(chainGap 재조준 비트)→다음손에서 Idle 클립이 깔리던 "잠시 쉼"을 제거 —
//                     Recovery가 끝나면 *반대 손 Windup으로 직행*(Idle 미경유)해 좌우좌우 쉼 없이 연타(끈질긴 벽).
//                    각 단발 = 4구간(이즈 램프), 순차·완결. 디스인게이지·접근 상태 없음 — 클로질이 전진.
//   제2원칙: 애니메이션이 진실. 전진은 ★단발 클립 루트모션(4구간 합 = 풀클립 2.218m)이 만든다
//            (applyRootMotion=true, 코드 포물선/위치이동 금지).
//            ★★스윙 속도는 구간별 정적 speed의 *이즈 램프*로 만든다(per-frame 코드 speed 곡선 ❌ — 헌법 위반).
//             Windup 1.9(앞 빠르게) → Strike 1.35(읽히는 히트) → FollowOut 2.3(뒤 빠르게) → Recovery 2.5(중립 복귀).
//             "휘릭" = 빠른 cocking + 크리스피 히트 + 휙 빠지는 팔로스루 + 탁 다음 손. 루트모션 전부 *재생*(트림/손실 아님).
//            ★(방향 정정 2026-06-14) 유저가 "이즈 곡선으로 빠르게"로 마음 바꿈 — (구)거부된 건 *flat 균일 빨리감기*였고
//             *가속 셰이핑*이면 OK. 그래서 균일 ❌·이즈 램프 ✓로 스윙 전체를 빠르게.
//            ★히트 모먼트는 *Strike* 구간의 AnimationEvent(ClawHit)가 만든다(컨택 frame 12, 코드 타이머가 클립과 따로 놀지 않게).
//
//   ★★회전 경계(헌법 — 2026-06-14 미세 개정, 유저 승인): 추적(재조준)을 *각 클로의 Windup*에 접는다.
//            회전 O = Roar(오프너 조준) · Idle(미교전 대기) · ★Windup(cocking 구간 frame 0~9).
//              └ Windup은 *아직 내지르기 전*(팔 드는 중)이라, 여기서 몸을 틀어 다음 타격을 조준해도 *내지르는 런지 궤적은 안 휜다*
//                → 헌법 정신("앞으로 내지르는 런지가 휘지 않게") 부합. position/speed 스크럽 아님(회전만 — Roar/Idle서 이미 허용된 동작의 확장).
//            회전 0 = Strike · FollowOut · Recovery (commit~회수 frame 9~35). 일단 내지르기 시작하면 방향 고정 — 궤적 보존.
//            경계 판정질문 = "발동된(commit된) 스윙의 궤적을 코드가 휘나" → Strike 진입 후부터 No(아직 cocking인 Windup은 조준 허용).
//            ★(구) "Windup 포함 4구간 전부 회전 0"에서 개정 — Idle 재조준 비트 제거에 따라 추적을 Windup으로 이관(쉼 없는 연타 + 매 클로 추적).
using System.Collections.Generic;
using UnityEngine;

public class DimaxillosaurusBrawler : MonoBehaviour
{
    static readonly List<DimaxillosaurusBrawler> Roster = new List<DimaxillosaurusBrawler>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStaticState() => Roster.Clear();

    [Header("참조 (스포너가 와이어링)")]
    public Transform model;
    public Animator modelAnimator;
    public Transform target;
    public RuntimeAnimatorController attackController;
    [Tooltip("공유 공격 토큰 풀 — 동시 교전 수 제한용 시스템(보존). ★클로월에선 비게이팅(획득 실패해도 전진은 멈추지 않음 — 모두가 벽).")]
    public AttackTokenPool tokenPool;

    [Header("조향/회전 (★추적 노브 — 유저 ▶ 판정)")]
    [Tooltip("★추적(재조준) 회전 속도(도/초). 회전 O = Roar·Idle(미교전)·★Windup(각 클로 cocking 구간 frame 0~9). Strike/FollowOut/Recovery 발동 중엔 0(궤적 보존).\n★이게 *추적 강도* 노브다(2026-06-14 끊임없는 좌우 개정): Idle 재조준 비트를 없애고 추적을 Windup에 접었으므로, 플레이어를 따라 도는 능력은 전적으로 Windup 길이(~0.158s = 9f/30fps÷1.9배속) × turnSpeed로 결정된다. 360°/s × 0.158s ≈ 클로당 최대 ~57° 회전(충분한 추적창). 사이드스텝을 더 빡세게 따라오게 하려면 ↑, 너무 잘 따라와 회피 불가하면 ↓.")]
    [SerializeField] float turnSpeed = 360f;

    [Header("Separation — 동료 회피 (군중 AI, 벽 간격 유지)")]
    [SerializeField] float separationRadius = 2.2f;
    [Range(0f, 3f)][SerializeField] float separationWeight = 1.0f;

    // 애니 파라미터 — DimaxillosaurusBrawler.controller와 공유. ★로코모션 파라미터(isMoving/isRunning)는 제거(접근 상태 없음).
    static readonly int PAttack = Animator.StringToHash("attack");     // 오프너: Idle→Roar (타깃 인지 1회)
    static readonly int PChainL = Animator.StringToHash("chainL");     // ★좌 단발 체인 — R_Recovery→L_Windup 직행(주) / Idle→L_Windup(폴백). 드라이버가 Recovery 진입에 _nextRight로 쏨.
    static readonly int PChainR = Animator.StringToHash("chainR");     // ★우 단발 체인 — L_Recovery→R_Windup 직행(주) / Idle→R_Windup(폴백).
    static readonly int SIdle   = Animator.StringToHash("Idle");
    static readonly int SRoar   = Animator.StringToHash("Roar");
    // ★★스윙 = 이즈 4구간 분할(2026-06-14 유저 디렉팅: "휘두르는 거 더 빠르게 + 앞부분 빠르게·뒷부분 빠르게·자연스럽게 연결, 휘릭휘릭휘릭").
    //   각 단발 = Windup→Strike→FollowOut→Recovery 네 상태. 같은 take를 frame 범위만 다르게 4분할(Swing/Recovery 분할과 동일 메커니즘).
    //   ★왜 4분할인가(헌법 준수): 한 state.speed는 클립 전체 균일 → "앞은 빠르게·중간은 자연·뒤는 빠르게"의 이즈 곡선을
    //    *per-frame 코드 speed 곡선*으로 만들면 헌법 위반(코드 매프레임 스크럽 금지). 대신 구간별 *정적 state.speed*의 계단형 근사로
    //    이즈를 실현 — 같은 take 분할이라 경계 포즈 비트-동일 → 포즈 점프 0, 루트모션 손실 0(트림 아님, 전부 재생).
    static readonly int SLWindup   = Animator.StringToHash("LeftClaw_Windup");
    static readonly int SLStrike   = Animator.StringToHash("LeftClaw_Strike");
    static readonly int SLFollow   = Animator.StringToHash("LeftClaw_FollowOut");
    static readonly int SLRecov    = Animator.StringToHash("LeftClaw_Recovery");
    static readonly int SRWindup   = Animator.StringToHash("RightClaw_Windup");
    static readonly int SRStrike   = Animator.StringToHash("RightClaw_Strike");
    static readonly int SRFollow   = Animator.StringToHash("RightClaw_FollowOut");
    static readonly int SRRecov    = Animator.StringToHash("RightClaw_Recovery");

    // ════════ ★★구간별 정적 speed = 이즈 곡선의 계단형 근사 (SSOT — 빌드스크립트가 state.speed로 참조) ════════
    //   ★(구) ClawSpeed 1.0 자연/RecoverySpeed 3.0의 하드 1.0→3.0 점프 = "휙 채서 어색" 1순위 우려였다.
    //   ★(방향 정정 2026-06-14) 유저가 "이즈 곡선으로 빠르게"로 마음 바꿈 — 어제 거부된 건 *flat 균일 2.5× 빨리감기*였고,
    //    *가속 셰이핑*이면 OK. → 스윙 전체를 1.0보다 빠르게 올리되 구간별로 램프(균일 ❌, 이즈 ✓).
    //   램프: Windup 1.9(앞 빠르게/cocking은 죽은 시간) → Strike 1.35(타격 frame12 — 읽히는 히트, 약간만 스내피=이즈-인 임팩트 강조)
    //         → FollowOut 2.3(뒤 빠르게/팔로스루 휙) → Recovery 2.5(중립 복귀). ★1.0→3.0 절벽 소멸: 2.3→2.5는 거의 이음매 없음.
    //   "휘릭" = 빠른 cocking + 읽히는 hit + 휙 빠지는 팔로스루 + 탁 다음 손. Strike만 상대적 느림(=히트 앵커, 유저의 "앞·뒤만 빠르게").
    public const float WindupSpeed    = 1.9f;   // frame 0~9   — 윈드업/cocking. 앞부분 빠르게(이즈-인 진입).
    public const float StrikeSpeed    = 1.35f;  // frame 9~16  — 컨택(f12)+초기 팔로스루. ★ClawHit 여기. 읽히는 히트(상대적 느림=임팩트).
    public const float FollowSpeed    = 2.3f;   // frame 16~22 — 후기 팔로스루. 뒷부분 빠르게(휙 빠짐).
    public const float RecoverySpeed  = 2.5f;   // frame 22~35 — 중립 복귀. ★3.0→2.5로 낮춰 FollowOut(2.3)과 이음매 매끄럽게(절벽 제거).

    // ════════ ★전진 증폭 게인 (AdvanceGain) — 유저 승인 헌법 미세 확장 (2026-06-14) ════════
    //   문제: 클로월 전진 ≈ 3.75 m/s(클로당 루트모션 2.218m / 0.59s) < 걷기 5.5 → 솔로 플레이어가 걸어서 빠져나감(§5 긴장).
    //   유저 확정(AskUserQuestion) = "전진거리 ↑ (루트모션 증폭)" — 속도(state.speed=4구간 이즈 램프)는 안 건드린다("휘릭" 타이밍 100% 보존),
    //     *거리만* 늘린다. 클로당 2.218m × 1.3 ≈ 2.88m → 새 전진 ≈ 4.9 m/s(걷기 근접 → 걸어선 못 빠짐, 대시로만 떼냄).
    //   ★헌법(제2원칙 미세 확장 — 유저 승인): 루트모션 = *방향·궤적·타이밍*의 진실. AdvanceGain은 클립 자신의 전진 델타(animator.deltaPosition)에
    //     게인만 곱하는 *증폭*이다 — 코드가 자체 속도/벡터로 위치를 *발명*하는 게 아니다(과거 코드 포물선 점프 사고와 다름).
    //     방향·궤적·타이밍은 100% 클립이 결정, 코드는 전진 *크기*만 스케일 → "애니가 진실" 정신 보존.
    //   ★튜닝: 유저 ▶ 4.5 vs 5 m/s 체감 조정(1.2~1.4 범위). 런타임 드라이버 로직이라 SetupData 재빌드 불필요(컨트롤러 불변 — 컴파일만).
    public const float AdvanceGain = 1.3f;

    bool _holdsToken;       // ★토큰 보유(비게이팅 — 슬롯 못 잡아도 전진은 계속). 수명 = 교전 수명(오프너~OnDisable).
    bool _engaged;          // ★공격 체인 활성(Roar 오프너 이미 마침). true면 Idle에서 Roar 생략하고 단발 직행.
    bool _windupSetup;      // ★현재 Windup 진입에 대해 1회 셋업 끝났나(엣지 가드). Windup이 아닌 상태에선 false로 리셋 → 매 Windup 진입마다 1회.
    bool _recovChained;     // ★현재 Recovery 진입에 대해 다음손 chain trigger 이미 쐈나(엣지 가드). Recovery가 아닌 상태에선 false로 리셋.
    bool _firedThisIdle;    // ★이번 Idle 체류 중 trigger 이미 쐈나 — 전이 1프레임 지연 동안 중복/오발 trigger 방지.
    bool _nextRight;        // ★좌우 교대 — 다음 단발이 우손인가. 오프너(Roar→L_Windup) 후 true(다음은 R). Windup 진입마다 토글.

    void Awake()
    {
        if (modelAnimator == null) { Debug.LogError("[DimaxBrawler] modelAnimator 미할당"); enabled = false; return; }
        if (attackController != null) modelAnimator.runtimeAnimatorController = attackController;
        else Debug.LogError("[DimaxBrawler] attackController 미할당");
        // ★루트모션이 전진을 만든다(제2원칙). applyRootMotion=true는 *deltaPosition/deltaRotation을 채워둔다* —
        //   OnAnimatorMove를 구현했으므로 Unity의 *자동* 적용은 꺼지고(이중적용 차단) 그 콜백에서 *수동* 적용한다(전진만 AdvanceGain 증폭).
        //   ★false로 바꾸면 deltaPosition이 0이 될 수 있어 전진이 죽는다 → true 유지가 정석(델타는 채워지되 적용 주체만 콜백으로 이관).
        modelAnimator.applyRootMotion = true;
        _engaged = false;
    }

    void OnEnable()  { if (!Roster.Contains(this)) Roster.Add(this); }
    void OnDisable()
    {
        Roster.Remove(this);
        ReleaseToken();   // ★토큰 반납 + ResetCombatState(체인 플래그·trigger 일괄 — _firedThisIdle 포함, disable/re-enable 소프트락 차단)
    }

    void ReleaseToken()
    {
        if (_holdsToken && tokenPool != null) tokenPool.Release();
        _holdsToken = false;
        ResetCombatState();   // ★토큰 해제 = 교전 종료 → 상태 일괄 리셋.
    }

    // ★교전 상태 일괄 리셋 — 모든 해제/비활성 경로가 경유(플래그 누락 비대칭 차단: trigger 스턱·stale gap 근원).
    //   trigger 리셋은 modelAnimator 가드(소실 경로서 호출되므로).
    void ResetCombatState()
    {
        _engaged = false;
        _windupSetup = false;
        _recovChained = false;
        _firedThisIdle = false;
        _nextRight = false;   // 다음 교전은 오프너(Roar)→L_Windup부터, 그 다음이 R.
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

    // 플레이어를 향해 yaw 회전(Roar/Idle/★Windup 재조준 전용 — Strike/FollowOut/Recovery 발동 중엔 호출 안 함). 분리 가중 살짝 섞음(벽 간격).
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

        // ★엣지 가드 리셋: 해당 상태가 *아닐* 때 false로 → 그 상태에 진입하는 첫 프레임에만 1회 셋업이 돈다.
        bool inWindup = (s == SLWindup || s == SRWindup);
        bool inRecov  = (s == SLRecov  || s == SRRecov);
        if (!inWindup) _windupSetup = false;
        if (!inRecov)  _recovChained = false;

        // ── Idle: 미교전 결정 허브(오프너). ★★끊임없는 좌우 개정 후 Idle은 *사실상 교전 중엔 안 들름*(Recovery→반대 Windup 직행). ──
        //   교전 중 Idle 도착 = 디제너릿(타깃 소실로 Recovery→Idle 폴백 전이가 걸림) → 타깃 재인지 시 다음 손으로 즉시 재개(쉼 최소).
        if (s == SIdle)
        {
            FaceTarget();            // ★Idle 재조준 = 회전 O(발동 중이 아님 — 회전 경계 준수).

            if (!_engaged)
            {
                // ★오프너: 타깃 인지 즉시 포효(거리 무관 — "멀리서 발견 → 그 자리에서 포효"). 토큰은 비게이팅 best-effort 획득.
                if (!_firedThisIdle)
                {
                    if (!_holdsToken && tokenPool != null && tokenPool.TryAcquire()) _holdsToken = true;
                    modelAnimator.SetTrigger(PAttack);
                    _firedThisIdle = true;
                }
            }
            else if (!_firedThisIdle)
            {
                // ★디제너릿 재개: 교전 중인데 Idle로 떨어졌다(폴백) → 재조준 비트 없이 즉시 다음 손으로 벽 재개.
                modelAnimator.SetTrigger(_nextRight ? PChainR : PChainL);
                _firedThisIdle = true;
            }
        }
        // ── Roar(앵티시페이션·위협 텔레그래프): 플레이어 향해 회전 O. ExitTime에 자동 L_Windup(오프너 첫 손). ──
        else if (s == SRoar)
        {
            _firedThisIdle = false;                        // Idle 떠남(attack 적중) — 다음 Idle에서 다시 쏠 수 있게 리셋
            modelAnimator.ResetTrigger(PChainL);           // 스테일 chain 재무장 제거
            modelAnimator.ResetTrigger(PChainR);
            FaceTarget();                                  // 발동 직전까지 조준(첫 클로가 플레이어를 향하게)
        }
        // ── LeftClaw_Windup / RightClaw_Windup(단발 첫 구간, frame 0~9 = cocking): ★★회전 O(추적을 여기 접음 — 개정). ──
        //   ★진입 1회 셋업(엣지): 스테일 trigger 청소 + 체인 활성 + 손 교대. ★매 프레임 FaceTarget(아직 내지르기 전이라 추적 가능 = 헌법 정신).
        else if (inWindup)
        {
            if (!_windupSetup)
            {
                _windupSetup = true;
                _firedThisIdle = false;
                modelAnimator.ResetTrigger(PAttack);   // ★전이 지연 동안 재무장됐을 스테일 trigger 청소(오발 방지).
                modelAnimator.ResetTrigger(PChainL);   // ★Recovery에서 쏜 chain trigger도 여기서 소비 후 청소(다음 Recovery 전까지 재무장 차단).
                modelAnimator.ResetTrigger(PChainR);
                _engaged = true;        // ★이제 체인 중 — 다음(폴백) Idle에서 Roar 생략(단발 직행).
                // ★좌우 교대: 방금 진입한 손의 *반대*를 다음 손으로 예약(Recovery에서 이 _nextRight로 chain). 오프너 Roar→L_Windup이면 다음 R.
                _nextRight = (s == SLWindup);
            }
            FaceTarget();   // ★cocking 중 추적 = 회전만(위치는 루트모션, speed는 정적). 내지르는 Strike~Recovery 궤적은 안 휨.
        }
        // ── Strike / FollowOut(단발 컨택~후기 팔로스루, frame 9~22): ★회전 0 엄수(commit됨 — 궤적 보존). ──
        else if (s == SLStrike || s == SRStrike || s == SLFollow || s == SRFollow)
        {
            // 회전·이동·trigger 없음 — 내지른 타격이 이즈 램프로 재생될 뿐. Strike에만 ClawHit(컨택) 이벤트.
        }
        // ── Recovery(단발 마지막 구간, frame 22~35 = 중립 복귀): ★회전 0 엄수. ★진입 1회 다음손 chain trigger(Idle 우회 직행). ──
        //   회수가 끝나는 ExitTime(0.99)에 *반대 손 Windup으로 직행* — Idle 미경유 = "잠시 쉼" 소멸. 폴백(타깃소실)이면 trigger 미소비→Idle.
        else if (inRecov)
        {
            if (!_recovChained)
            {
                _recovChained = true;
                modelAnimator.SetTrigger(_nextRight ? PChainR : PChainL);   // ★다음 손(반대) 예약 → Recovery ExitTime에 그 Windup으로 직행.
            }
            // 회전 0 — 이미 내지른 회수 궤적 보존.
        }
    }

    // ════════ ★루트모션 수동 적용 — 전진 증폭(OnAnimatorMove) ════════
    //   applyRootMotion=true이면 Unity가 매 평가 후 *자동*으로 transform에 deltaPosition/deltaRotation을 더한다.
    //   OnAnimatorMove를 구현하면 그 자동 적용이 *콜백으로 위임*된다 → 여기서 수동 적용(전진만 AdvanceGain배).
    //   ★발화 보장: 이 콜백은 *Animator가 붙은 GameObject의 MonoBehaviour*에만 불린다. 이 드라이버는 프리팹 *루트*(Animator도 루트, 검증됨)에
    //     AddComponent되므로(스포너) Animator와 같은 GameObject → 발화 보장. model == 그 루트 transform이라 model을 직접 움직인다.
    //   ★헌법(제2원칙, 유저 승인 확장): deltaPosition은 *클립 자신의* 전진 델타 — 게인은 그 *크기*만 키운다(방향·궤적·타이밍은 클립이 진실).
    //     deltaRotation은 1×(증폭 안 함): 클로 클립의 회전 루트모션은 0(측면·상승 0 실측)이라 ≈identity, 조향은 FaceTarget이 담당.
    //   ★★게인은 *클로 구간(8상태)에만* 적용한다(Stab/Codex 수렴 지적). Idle/Roar는 *제자리 상태*인데 root에 미세 변위가 구워져
    //     있으면 그것까지 ×1.3로 증폭돼 평시 슬금슬금 밀릴 수 있다(Idle/Roar 루트모션 미측정). 클로 외 상태는 게인 1×(=변경 전
    //     자동적용과 동일 동작 → 새 드리프트 위험 0). 실측된 건 클로 클립뿐(전진 2.218m·측면0·상승0)이라 게인도 거기만.
    void OnAnimatorMove()
    {
        if (modelAnimator == null || model == null) return;
        int s = modelAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        bool inClaw = s == SLWindup || s == SLStrike || s == SLFollow || s == SLRecov
                   || s == SRWindup || s == SRStrike || s == SRFollow || s == SRRecov;
        float gain = inClaw ? AdvanceGain : 1f;             // ★클로 구간만 증폭 / Idle·Roar·전이는 1×(제자리 드리프트 비증폭).
        model.position += modelAnimator.deltaPosition * gain;   // ★전진 증폭(클립 전진 델타 × 게인) — 크기만 스케일.
        model.rotation *= modelAnimator.deltaRotation;          // 회전은 그대로(클립 회전 0 → ≈identity). 조향=FaceTarget.
    }

    // ════════ ★AnimationEvent 콜백 — 단발 Left/RightClawsAttackForward_RM 클립의 컨택 프레임에서 호출 ════════
    //   클립 events:가 SendMessage("ClawHit", hitIndex)로 호출(Animator와 같은 GameObject). ★장판 텔레그래프 제거(Dimax 미사용).

    /// <summary>각 클로 컨택 정점 프레임 → 히트 모먼트(향후 데미지 훅). 실 전투 히트박스/데미지는 게임플레이 단계(범위 밖).</summary>
    public void ClawHit(AnimationEvent ev)
    {
        // ★닿는 순간(클립 컨택 정점). 데미지/히트박스는 게임플레이 단계에서 이 이벤트에 훅(애니=타이밍의 진실).
    }
}
