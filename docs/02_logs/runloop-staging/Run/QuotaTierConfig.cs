using UnityEngine;

namespace Run
{
    /// <summary>
    /// 티어별 할당량 N · 제한시간 · 성장 계승(XP 배수) 설정. 모든 수치는 이 SO에 산다(헌장 §1-3).
    /// 티어는 1부터 — 인덱스 = tier-1, 배열 끝을 넘으면 마지막 값으로 클램프(외삽).
    /// ※모든 배열 값은 자리표시 — 맵 실측 후 튜닝([[project_2026_06_03_spawn_model]] "수치는 맵 의존").
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/QuotaTierConfig", fileName = "QuotaTierConfig")]
    public class QuotaTierConfig : ScriptableObject
    {
        [Tooltip("티어별 할당량 N(처리 목표). 인덱스 0=티어1. 배열 끝 넘으면 마지막 값 외삽.")]
        public int[] quotaPerTier = { 12, 18, 26, 36 };

        [Tooltip("티어별 제한시간(초). 0 도달=사망 아님, 위협 시간초과 램프 시작.")]
        public float[] secondsPerTier = { 180f, 165f, 150f, 135f };

        [Tooltip("티어별 XP 획득 배수(룰렛 한 탕 더의 성장 보상 — '티어3의 나 ≫ 티어1').")]
        public float[] tierXpMult = { 1.0f, 1.3f, 1.7f, 2.2f };

        /// <summary>해당 티어의 할당량 N.</summary>
        public int QuotaForTier(int tier) => Sample(quotaPerTier, tier);

        /// <summary>해당 티어의 제한시간(초).</summary>
        public float SecondsForTier(int tier) => Sample(secondsPerTier, tier);

        /// <summary>해당 티어의 XP 획득 배수.</summary>
        public float XpMultForTier(int tier) => Sample(tierXpMult, tier);

        // tier는 1부터 → 인덱스 tier-1, 배열 끝 넘으면 마지막 값 클램프. 빈 배열이면 폴백 반환.
        static int Sample(int[] arr, int tier)
            => (arr == null || arr.Length == 0) ? 0 : arr[Mathf.Clamp(tier - 1, 0, arr.Length - 1)];

        static float Sample(float[] arr, int tier)
            => (arr == null || arr.Length == 0) ? 0f : arr[Mathf.Clamp(tier - 1, 0, arr.Length - 1)];
    }
}
