using UnityEngine;

namespace Run
{
    /// <summary>
    /// 한 런(작전)의 불변 파라미터. 모든 밸런스 수치는 이 SO에 산다(헌장 §1-3: 로직/수치 분리).
    /// 스켈레톤: 미션 시간 · 헬기 호출 딜레이 · 탈출 반경. Phase 2가 값만 갈아끼운다.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/RunConfig", fileName = "RunConfig")]
    public class RunConfig : ScriptableObject
    {
        [Tooltip("작전 제한 시간(초). 0이 되면 sweep(소독 폭격)으로 런 강제 종료.")]
        public float missionSeconds = 900f;

        [Tooltip("탈출 지점 진입 후 회수 헬기 도착까지 걸리는 시간(초). 반경을 벗어나면 취소.")]
        public float helicopterDelaySeconds = 18f;

        [Tooltip("탈출 지점 트리거 반경(m). 플레이어가 이 안에 있어야 헬기 카운트가 진행.")]
        public float extractionRadius = 4f;
    }
}
