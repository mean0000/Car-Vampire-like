using UnityEngine;

namespace Run
{
    /// <summary>
    /// 사무실에서 strain으로 구매하는 무기 강화 1줄의 정의(스켈레톤=데미지·연사 2개).
    /// 비용은 레벨별 배열 또는 base+증가량으로 계산. 효과는 레벨당 선형 누적(GetMultiplier).
    /// 저장 키 = 에셋 이름(UpgradeDef.name). id는 표시/디버그용.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieCrush/Run/UpgradeDef", fileName = "UpgradeDef")]
    public class UpgradeDef : ScriptableObject
    {
        [Tooltip("내부 식별자(디버그/로그용). 저장 키는 에셋 이름을 사용한다.")]
        public string id = "damage";

        [Tooltip("사무실 버튼에 표시할 이름(예: '사격 피해').")]
        public string displayName = "사격 피해";

        [Tooltip("최대 강화 레벨.")]
        [Min(1)] public int maxLevel = 5;

        [Header("비용 (strain)")]
        [Tooltip("레벨별 비용 배열. 비어 있지 않으면 이 배열이 우선(인덱스=구매하려는 레벨-1).")]
        public int[] costPerLevel;

        [Tooltip("costPerLevel가 비었을 때 폴백: 첫 레벨 비용.")]
        [Min(0)] public int baseCost = 3;

        [Tooltip("costPerLevel가 비었을 때 폴백: 레벨당 비용 증가량.")]
        [Min(0)] public int costIncreasePerLevel = 2;

        [Header("효과")]
        [Tooltip("레벨당 효과 비율(0.15 = 레벨당 +15%). 최종 배율 = 1 + effectPerLevel × level.")]
        public float effectPerLevel = 0.15f;

        [Tooltip("UI 효과 설명(예: '데미지 +15%/레벨').")]
        public string effectDescription = "데미지 +15%/레벨";

        /// <summary>currentLevel(0-based 보유)에서 다음 1레벨을 살 때의 비용. 만렙이면 -1.</summary>
        public int CostForNextLevel(int currentLevel)
        {
            if (currentLevel >= maxLevel) return -1;
            if (costPerLevel != null && currentLevel < costPerLevel.Length)
                return Mathf.Max(0, costPerLevel[currentLevel]);
            return Mathf.Max(0, baseCost + costIncreasePerLevel * currentLevel);
        }

        /// <summary>보유 레벨에서의 최종 배율(1 + effectPerLevel × level).</summary>
        public float MultiplierAtLevel(int level) => 1f + effectPerLevel * level;
    }
}
