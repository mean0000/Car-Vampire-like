using UnityEngine;

/// <summary>
/// ★읽기 슬로모 트리거 — 고가치 커밋 *시작* 엣지(<see cref="EnemyDamageReceiver.CommitStarted"/>)를 받아
/// 근접(액션 가능 거리)일 때만 <see cref="ParrySlowMotion.ReadSlow"/>로 짧은 시간 팽창을 요청한다.
/// "세상이 기회를 보여준다" — 입력 요구 없음(타이밍-듀얼 아님, 지각 배려. 카탈로그 1-3).
///
/// 필터(Codex 반증 조건 4 — 호드 전체가 후보면 소음): ①고가치 개체만(HighValueCommit)
/// ②거리(나에게 실제로 액션 가능한 커밋만) ③시간(ParrySlowMotion 내부 readSlowCooldown).
/// 플레이어 계층(ParrySlowMotion 있는 곳)에 부착. timeScale은 ParrySlowMotion 단일 소유 유지.
/// </summary>
public class ReadSlowmoTrigger : MonoBehaviour
{
    [Tooltip("★랩 토글 — 끄면 읽기 슬로모 없음(A/B).")]
    [SerializeField] bool readSlowmoEnabled = true;
    [Tooltip("이 거리(m) 안의 커밋만 '나에게 액션 가능한' 것으로 본다 — 화면 밖/먼 커밋은 무시.")]
    [SerializeField, Min(0f)] float maxDistance = 6f;
    [Tooltip("timeScale 소유자(ParrySlowMotion). 비우면 부모/자식에서 탐색.")]
    [SerializeField] ParrySlowMotion slowMotion;

    void Awake()
    {
        if (slowMotion == null)
            slowMotion = GetComponentInParent<ParrySlowMotion>() ?? GetComponentInChildren<ParrySlowMotion>(true);
        if (slowMotion == null)
            Debug.LogWarning("[ReadSlowmoTrigger] ParrySlowMotion 미발견 — 읽기 슬로모 비활성.", this);
    }

    void OnEnable()
    {
        EnemyDamageReceiver.CommitStarted -= OnCommit;   // 정적 이벤트 — 중복 구독 방지
        EnemyDamageReceiver.CommitStarted += OnCommit;
    }

    void OnDisable()
    {
        EnemyDamageReceiver.CommitStarted -= OnCommit;   // ★정적 이벤트 해제 필수(누수 방지)
    }

    void OnCommit(EnemyDamageReceiver r)
    {
        if (!readSlowmoEnabled || slowMotion == null || r == null) return;
        if (!r.HighValueCommit) return;                       // 고가치 커밋만(엘리트/시그니처)
        Vector3 to = r.transform.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > maxDistance * maxDistance) return;   // 액션 가능 거리만
        slowMotion.ReadSlow();                                // 쿨다운/양보는 소유자가 판단
    }
}
