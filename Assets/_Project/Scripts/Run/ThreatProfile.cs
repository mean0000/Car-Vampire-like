using UnityEngine;

namespace Run
{
    /// <summary>
    /// 위협 다이얼 T → 곱셈자(스탯/밀도) + 변이 단계 곡선. 위협 시스템의 데이터 모델.
    /// 상승원 가중(깊이/시간초과)과 곡선/임계가 모두 여기 산다 → ThreatLevel은 로직만.
    /// ※모든 곡선/임계는 형태만 잡은 자리표시 — 맵 실측 후 튜닝.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/ThreatProfile", fileName = "ThreatProfile")]
    public class ThreatProfile : ScriptableObject
    {
        [Header("상승원 가중")]
        [Tooltip("티어 1단계 깊어질 때 위협 가산(룰렛 한 탕 더의 무게).")]
        public float depthPerTier = 0.25f;

        [Tooltip("할당량 시간 초과 후 경과 1초당 위협 가산(지연 페널티 램프).")]
        public float overtimeRamp = 0.02f;

        [Header("곱셈자 곡선 (X=위협 T, Y=배수)")]
        [Tooltip("몬스터 능력치(HP·공격·속도) 배수. 1 미만은 1로 바닥 고정.")]
        public AnimationCurve statByThreat = AnimationCurve.Linear(0f, 1f, 1.5f, 2.2f);

        [Tooltip("스폰 밀도 배수. 1 미만은 1로 바닥 고정.")]
        public AnimationCurve densityByThreat = AnimationCurve.Linear(0f, 1f, 1.5f, 2.8f);

        [Header("변이 임계 (T가 이 값 넘으면 단계 ↑, 오름차순)")]
        public float[] mutationThresholds = { 0.5f, 1.0f, 1.6f };

        /// <summary>위협 T에서의 능력치 배수(≥1).</summary>
        public float StatScaleAt(float t) => Mathf.Max(1f, statByThreat.Evaluate(t));

        /// <summary>위협 T에서의 밀도 배수(≥1).</summary>
        public float DensityMultAt(float t) => Mathf.Max(1f, densityByThreat.Evaluate(t));

        /// <summary>위협 T가 넘은 변이 단계(0=기본). 임계는 오름차순 가정.</summary>
        public int MutationStageAt(float t)
        {
            if (mutationThresholds == null) return 0;
            int s = 0;
            for (int i = 0; i < mutationThresholds.Length; i++)
            {
                if (t >= mutationThresholds[i]) s = i + 1;
                else break;
            }
            return s;
        }
    }
}
