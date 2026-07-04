using UnityEngine;

/// <summary>
/// ★처리효율 게이지 스텁 — 상시형(2026-07-02 동결: "본사 실시간 모니터링"). 수치만, 정식 HUD ❌(디버그 라벨만 —
/// 서식 연출은 표현 트랙/렉시콘 경유 별도). 랩 스펙 §3-2.
///
/// 상승 = 처리(킬)·읽고-처리(크리). 감쇠 = 시간(후퇴/비전투가 마르는 유일한 채찍 — 이동 페널티 ❌).
/// 피격 = 즉시 감쇠(생존층 1-4: 피격 비용을 목숨 대신 실적으로 일부 지불 → 커밋 근접의 실패 비용 인하).
///
/// ★저점 안전망(Codex 반증 조건 3): 게이지는 '가속기'지 생존권이 아니다 — HealMult/GenerosityMult는
/// 1.0 밑으로 절대 안 내려간다(기본 회복·기본 배려는 실적 무관 유지, 나선 하강 방지).
/// Stage 2 훅: HealMult/GenerosityMult는 지금 노출만 — 소비자 연결은 Stage 2(랩 스펙 §4).
/// </summary>
public class PerformanceGauge : MonoBehaviour
{
    [Header("상승 (처리 행동만 — Doom push-forward)")]
    [Tooltip("킬 1건당 가산(0..100 스케일).")]
    [SerializeField, Min(0f)] float gainPerKill = 5f;
    [Tooltip("읽고-처리(크리티컬 피격) 1건당 가산 — 킬과 별도(크리 후 킬이면 둘 다).")]
    [SerializeField, Min(0f)] float gainPerCrit = 10f;

    [Header("감쇠 (유일한 채찍)")]
    [Tooltip("초당 감쇠. ULTRAKILL 15/s의 완화판 — 뱀서 숨돌리기(줍기/재정렬)를 죄책감으로 만들지 않게 낮게 시작(Codex 반증 §2).")]
    [SerializeField, Min(0f)] float decayPerSecond = 4f;
    [Tooltip("피격 시 즉시 감소 — 피격 비용=실적(생존층 1-4).")]
    [SerializeField, Min(0f)] float hitPenalty = 12f;

    [Header("참조/디버그")]
    [Tooltip("플레이어 체력(피격 감쇠 구독). 비우면 씬에서 탐색.")]
    [SerializeField] PlayerHealth playerHealth;
    [Tooltip("좌상단 디버그 라벨(값+랭크) 표시 — 랩 전용(정식 HUD 아님).")]
    [SerializeField] bool debugLabel = true;

    float _value;   // 0..100

    public float Value01 => _value / 100f;
    /// <summary>랭크 0..4 = D/C/B/A/S (20 단위).</summary>
    public int Rank => Mathf.Clamp((int)(_value / 20f), 0, 4);
    /// <summary>Stage 2 훅 — 회복 배율. ★저점 안전망: 최저 1.0(실적은 가속기, 생존권 아님).</summary>
    public float HealMult => 1f + Value01;
    /// <summary>Stage 2 훅 — 배려(버퍼/마그네티즘) 확대 배율. 최대 +30%(카탈로그 1-5). ★최저 1.0.</summary>
    public float GenerosityMult => 1f + Value01 * 0.3f;

    void OnEnable()
    {
        EnemyDamageReceiver.AnyDied -= OnAnyDied;      // 정적 이벤트 — 중복 구독 방지 후 구독
        EnemyDamageReceiver.AnyDied += OnAnyDied;
        EnemyDamageReceiver.AnyCritHit -= OnAnyCrit;
        EnemyDamageReceiver.AnyCritHit += OnAnyCrit;
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.Damaged -= OnPlayerDamaged;
            playerHealth.Damaged += OnPlayerDamaged;
        }
        else Debug.LogWarning("[PerformanceGauge] PlayerHealth 미발견 — 피격 감쇠 비활성(무음 소실 방지 경고, Stab L-4).", this);
    }

    void OnDisable()
    {
        // ★정적 이벤트 해제 필수 — 씬 전환/파괴 후 죽은 구독 잔존(누수·NRE) 방지.
        EnemyDamageReceiver.AnyDied -= OnAnyDied;
        EnemyDamageReceiver.AnyCritHit -= OnAnyCrit;
        if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
    }

    void OnAnyDied(EnemyDamageReceiver r) => _value = Mathf.Min(100f, _value + gainPerKill);
    void OnAnyCrit(EnemyDamageReceiver r) => _value = Mathf.Min(100f, _value + gainPerCrit);
    void OnPlayerDamaged(int cur, int max) => _value = Mathf.Max(0f, _value - hitPenalty);

    void Update()
    {
        // 스케일 시간 감쇠 — 히트스탑/슬로모 중엔 세상이 멈춘 만큼 게이지도 천천히(연출이 실적을 갉지 않게).
        _value = Mathf.Max(0f, _value - decayPerSecond * Time.deltaTime);
    }

    void OnGUI()
    {
        if (!debugLabel) return;
        GUI.Label(new Rect(10, 10, 320, 22), $"EFFICIENCY {_value:F0}  RANK {"DCBAS"[Rank]}   (debug)");
    }
}
