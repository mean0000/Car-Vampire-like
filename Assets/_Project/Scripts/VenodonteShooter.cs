// Venodonte 사수 드라이버 — 원거리 조준 사격 군중 AI. ★Caniathrox 추격과 정반대 철학(예측 안 함).
//
// ════════ 헌법 (불가침 — 애니 에이전트 3원칙) ════════
//   제0원칙: 정체성 동작(산성샷 3연) 재생 중엔 그 클립만 돈다. crossfade로 뭉개지 않는다.
//            → 상태머신(VenodonteAttack.controller)이 강제. 이 드라이버는 컨트롤러를 건드리지 않는다.
//   제1원칙: 공격은 상태 시퀀스다. Idle → Reposition(CrawlForward_RM 루트모션으로 사거리 진입)
//            → Aim(Taunt 머리 들어올림=조준 윈드업) → Fire(3AcidShotCombo, 제자리·이벤트3발)
//            → (Idle 쿨다운) → ... 각 단계 = 1클립 = 1상태, 순차·완결.
//   제2원칙: 애니메이션이 진실. ★발사 타이밍은 클립의 AnimationEvent 3개(FireAcidGlob)가 만든다 —
//            코드 타이머가 아니다. 머리 전방 스러스트 정점(norm 0.225/0.425/0.625, Animator 실측)에 박힘.
//            위치(전진)는 Crawl 루트모션이 만든다. 이 드라이버는 모션을 만들지 않는다(상태 전이·회전·이벤트만).
//
//   ★조준탄 철학 (Caniathrox 예측 요격과 정반대):
//     글롭은 발사되는 *순간*의 플레이어 위치로 직사 — pos+vel 리드 없음. 날아가는 동안 유도 0(AcidGlob).
//     ┗ 플레이어가 멈춰 쏘면 정통, 움직이면 빗나감 = "정지 사격의 대가". LV1이라 거리 유지는 약하게.
//       순수 카이터(끝없이 도망)가 아니라 군체(몸으로 밀려옴)에 섞여 사선을 쏘는 사수.
//
//   ★회전 경계(헌법):
//     - Reposition(로코모션)·Aim(조준) 중: 플레이어를 향해 yaw 회전 O (방향=AI 의도, 위치는 루트모션).
//     - Fire(발사) 중: 회전 절대 0. 몸이 조준에 고정됐고, 각 글롭은 이벤트 순간 방향으로 이미 발사됨.
//       경계 판정질문 = "발사된 글롭의 궤적을 코드가 휘나" → Fire 진입 후 No(글롭은 고정 직사).
using System.Collections.Generic;
using UnityEngine;

public class VenodonteShooter : MonoBehaviour
{
    // 분리(separation)용 전역 로스터 — Caniathrox와 같은 패턴(자기 종 안에서만 회피).
    static readonly List<VenodonteShooter> Roster = new List<VenodonteShooter>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStaticState() => Roster.Clear();

    [Header("참조 (스포너가 와이어링)")]
    public Transform model;
    public Animator modelAnimator;
    public Transform target;
    public RuntimeAnimatorController attackController;
    [Tooltip("공유 공격 토큰 풀 — 동시 사격 수 제한(탄막 밀도 규율: 직사 사수 ≤4).")]
    public AttackTokenPool tokenPool;
    [Tooltip("공유 투사체 풀 — 글롭 발사·재활용. 스포너가 모든 사수에 같은 참조 주입.")]
    public ProjectilePool projectilePool;

    [Header("사거리 (조준탄 — 거리 유지 노브)")]
    [Tooltip("선호 사격 거리(m). 이보다 멀면 다가가고(Reposition), 너무 가까우면 물러난다. 군체에 섞이는 중거리.")]
    [SerializeField] float preferredRange = 9.0f;
    [Tooltip("선호 거리 허용 폭(m). [preferred±band] 안에 들면 사격. LV1이라 넓게(느슨한 카이팅).")]
    [SerializeField] float rangeBand = 2.5f;
    [Tooltip("Reposition(CrawlForward_RM) 이동 속도(m/s). 낮게 기는 크리처라 느리게(시작값 3.0). 군체에 섞일 정도.")]
    [SerializeField] float moveSpeed = 3.0f;
    [Tooltip("사격 1사이클 후 다음 조준까지 쿨다운(초). 조준 사격 리듬. 짧으면 압박↑(정지사격 처벌↑). 시작값 1.0.")]
    [SerializeField] float cooldown = 1.0f;

    [Header("조준/발사 (정지사격 처벌 — 공정성 노브)")]
    [Tooltip("Reposition/Aim 중 플레이어를 향해 도는 회전 속도(도/초). Fire 중엔 0(궤적 보존).")]
    [SerializeField] float turnSpeed = 300f;
    [Tooltip("글롭 탄속(m/s). ★정지사격 처벌의 공정성을 만드는 핵심. 느릴수록 위빙 쉬움(공정), 빠를수록 회피 어려움. 시작값 7(공격 문법 §6).")]
    [SerializeField] float globSpeed = 7.0f;
    [Tooltip("글롭 최대 사거리(m). 이 거리 넘으면 소멸. preferredRange+여유.")]
    [SerializeField] float globRange = 16f;
    [Tooltip("글롭 수명(초). 사거리 도달 전에라도 이 시간이 지나면 소멸(안전망).")]
    [SerializeField] float globLifetime = 3f;
    [Tooltip("총구 높이 오프셋(m) — 모델 발밑에서 머리/입 높이로. 글롭이 머리 높이에서 나가게. 시작값 0.9.")]
    [SerializeField] float muzzleHeight = 0.9f;
    [Tooltip("총구 전방 오프셋(m) — 모델 중심에서 입 앞으로. 글롭이 몸 안에서 안 생기게. 시작값 0.5.")]
    [SerializeField] float muzzleForward = 0.5f;

    [Header("조준 압축 (윈드업 속도)")]
    [Tooltip("Aim(Taunt) 재생 배속. Taunt 3.33s를 압축 → 머리 들어올림 텔레그래프가 ExitTime(0.45)에 ~0.45s로 읽히게. 시작값 3.3.")]
    [SerializeField] float aimSpeed = 3.3f;

    [Header("Separation — 동료 회피 (군중 AI)")]
    [Tooltip("이 반경(m) 안 동료를 회피. 사수끼리 안 겹치고 사선이 분산되게.")]
    [SerializeField] float separationRadius = 2.0f;
    [Range(0f, 3f)] [SerializeField] float separationWeight = 1.0f;

    // 애니 파라미터 — VenodonteAttack.controller와 공유.
    static readonly int PMoving = Animator.StringToHash("isMoving");   // Idle↔Reposition
    static readonly int PAim    = Animator.StringToHash("aim");        // Idle→Aim (사격 개시)
    static readonly int SIdle   = Animator.StringToHash("Idle");
    static readonly int SRepos  = Animator.StringToHash("Reposition");
    static readonly int SAim    = Animator.StringToHash("Aim");
    static readonly int SFire   = Animator.StringToHash("Fire");

    // CrawlForward_RM 네이티브 속도(1.567m / 0.533s). Animator 스텝 실측(2026-06-13).
    const float CrawlNativeSpeed = 2.940f;

    bool _moving;
    bool _holdsToken;
    float _cdTimer;        // Idle 진입 후 쿨다운 카운트다운

    void Awake()
    {
        if (modelAnimator == null) { Debug.LogError("[VenodonteShooter] modelAnimator 미할당"); enabled = false; return; }
        if (attackController != null) modelAnimator.runtimeAnimatorController = attackController;
        else Debug.LogError("[VenodonteShooter] attackController 미할당");
        modelAnimator.applyRootMotion = true;   // 루트모션이 캐릭터를 움직인다(제2원칙)
        _cdTimer = 0f;
    }

    void OnEnable()  { if (!Roster.Contains(this)) Roster.Add(this); }
    void OnDisable() { Roster.Remove(this); ReleaseToken(); }

    void SetMoving(bool v)
    {
        _moving = v;
        if (modelAnimator != null) modelAnimator.SetBool(PMoving, v);
    }

    void ReleaseToken()
    {
        if (_holdsToken && tokenPool != null) tokenPool.Release();
        _holdsToken = false;
    }

    float PlanarDistanceToTarget()
    {
        Vector3 a = model.position, b = target.position; a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // 동료 회피 가중(반경 내 사수에서 멀어짐). 사수는 능동 추적 대신 거리 유지 + 사선 분산.
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

    // 플레이어를 향해 yaw 회전(Reposition/Aim 전용 — Fire 중엔 호출 안 함). 분리 가중을 살짝 섞어 사선 분산.
    void FaceTarget()
    {
        Vector3 toT = target.position - model.position; toT.y = 0f;
        if (toT.sqrMagnitude < 0.0001f) return;
        Vector3 dir = (toT.normalized + SeparationDir() * 0.5f); dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = toT.normalized;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        model.rotation = Quaternion.RotateTowards(model.rotation, want, turnSpeed * Time.deltaTime);
    }

    void Update()
    {
        if (modelAnimator == null) return;
        // ★속도 단일 진실원: 매 프레임 1f로 리셋, Reposition·Aim 브랜치에서만 배율로 올림(누수 방지 — Caniathrox 패턴).
        modelAnimator.speed = 1f;
        // ★토큰 누수 방어: 토큰을 쥔 채 타깃(플레이어)이 파괴되면 Idle 해제 경로(아래)에 영영 못 닿는다 →
        //   여기서 반납해야 다른 사수가 굶지 않는다(실게임 플레이어 사망 시 발화. 랩 캡슐은 안 죽어 미발화).
        if (target == null || model == null) { ReleaseToken(); return; }

        var info = modelAnimator.GetCurrentAnimatorStateInfo(0);
        float dist = PlanarDistanceToTarget();

        // ── Idle(쿨다운/대기): 사거리 평가 → 밴드 밖이면 Reposition, 밴드 안+토큰이면 Aim 개시 ──
        if (info.shortNameHash == SIdle)
        {
            ReleaseToken();      // 사이클 완결(Fire→Idle) → 토큰 반납(다음 사수가 획득)

            if (_cdTimer > 0f) { _cdTimer -= Time.deltaTime; SetMoving(false); return; }

            bool inBand = Mathf.Abs(dist - preferredRange) <= rangeBand;
            if (!inBand)
            {
                SetMoving(true);                    // 사거리 밖 → Reposition(CrawlForward_RM)
            }
            else
            {
                SetMoving(false);
                // 사거리 안 — 토큰 얻으면 조준 개시(Aim→자동 Fire). 못 얻으면 침착하게 대기.
                if (_holdsToken || (tokenPool != null && tokenPool.TryAcquire()))
                {
                    if (tokenPool != null) _holdsToken = true;
                    FaceTarget();                   // 조준 진입 직전 1회 정렬
                    modelAnimator.SetTrigger(PAim); // → Aim(Taunt 머리 들어올림). Aim→Fire는 ExitTime 자동.
                }
                // 토큰 없음 → 제자리 대기(사수는 서성이지 않고 침착). 다음 프레임 재시도.
            }
        }
        // ── Reposition(CrawlForward_RM): 루트모션 전진 + 회전(가까우면 등져 후퇴), 밴드 들면 정지 ──
        else if (info.shortNameHash == SRepos)
        {
            // 이동 속도: Crawl 네이티브에 배율(발도 비례 가속, 미끄러짐 최소). Reposition에서만.
            modelAnimator.speed = moveSpeed / CrawlNativeSpeed;

            // 너무 가까우면 등을 돌려 후퇴(전진 루트모션이 멀어지는 방향이 되게), 멀면 정면 접근.
            bool tooClose = dist < preferredRange - rangeBand;
            Vector3 toT = target.position - model.position; toT.y = 0f;
            Vector3 face = tooClose ? -toT : toT;
            face = (face.normalized + SeparationDir() * 0.5f); face.y = 0f;
            if (face.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(face.normalized, Vector3.up);
                model.rotation = Quaternion.RotateTowards(model.rotation, want, turnSpeed * Time.deltaTime);
            }

            // 밴드 안쪽(히스테리시스 0.6×band)으로 들면 이동 종료 → Idle 복귀(다음 프레임 사격 평가). 떨림 방지.
            if (Mathf.Abs(dist - preferredRange) <= rangeBand * 0.6f)
                SetMoving(false);
        }
        // ── Aim(Taunt 머리 들어올림=조준 윈드업): 플레이어 향해 회전 O. ExitTime에 자동 Fire(트리거 불요). ──
        else if (info.shortNameHash == SAim)
        {
            // Taunt(3.33s)를 압축 재생 → 머리 들어올림(조준 텔레그래프)이 ~0.45s에 읽힘(LV1 0.4~0.5s 약속).
            modelAnimator.speed = aimSpeed;
            FaceTarget();                           // 발사 직전까지 플레이어 추적(분산 섞음)
        }
        // ── Fire(3AcidShotCombo): ★회전 0 엄수. 발사는 AnimationEvent(FireAcidGlob)가. 드라이버는 무동작. ──
        //   각 글롭은 이벤트 순간의 플레이어 위치로 직사(예측 없음). 몸은 조준에 고정.
        //   상태머신이 Fire→Idle을 ExitTime으로 완결 → Idle 진입 시 OnFireCycleEnd가 쿨다운 재무장.
        else if (info.shortNameHash == SFire)
        {
            // Fire 진입을 감지해 쿨다운을 예약(이 사이클이 끝나고 Idle로 돌아가면 cooldown만큼 쉼).
            if (_cdTimer <= 0f) _cdTimer = cooldown;
        }
    }

    // ════════ ★AnimationEvent 콜백 — 3AcidShotCombo 클립의 머리 스러스트 정점 3곳에서 호출 ════════
    //   클립의 events:가 SendMessage("FireAcidGlob", shotIndex)로 이 메서드를 부른다(Animator와 같은 GameObject).
    //   각 호출 = 글롭 1발 발사. ★발사 순간의 플레이어 위치로 직사(예측 없음 — 정지사격 처벌).
    public void FireAcidGlob(int shotIndex)
    {
        if (projectilePool == null || target == null || model == null) return;

        // 총구 = 모델 위치 + 전방·높이 오프셋(입 앞·머리 높이). 본 의존 안 함(견고).
        Vector3 muzzle = model.position + model.forward * muzzleForward + Vector3.up * muzzleHeight;

        // ★조준 = 발사 *이 순간* 플레이어 위치(예측 없음). 플레이어가 멈췄으면 정통, 움직였으면 빗나감.
        Vector3 aimPoint = target.position + Vector3.up * muzzleHeight * 0.5f;  // 플레이어 몸통 높이 겨냥
        Vector3 dir = aimPoint - muzzle; dir.y = 0f;   // 탑뷰 평면 직사
        // 플레이어가 총구 XZ에 정확히 겹치면 dir≈0 → 글롭이 풀의 stale forward로 오발. 조준했던 정면으로 폴백.
        if (dir.sqrMagnitude < 0.0001f) { dir = model.forward; dir.y = 0f; }

        projectilePool.Fire(muzzle, dir, globSpeed, globRange, globLifetime);
    }
}
