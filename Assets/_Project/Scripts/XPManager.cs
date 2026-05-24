using UnityEngine;

/// <summary>
/// XP 및 레벨 관리 싱글톤.
/// </summary>
[DefaultExecutionOrder(-100)]
public class XPManager : MonoBehaviour
{
    public static XPManager Instance { get; private set; }

    [SerializeField] int[] xpThresholds = { 5, 12, 22, 35, 50 };

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXP { get; private set; } = 0;

    /// <summary>좀비 처치 XP 보너스 (업그레이드로 증가)</summary>
    public int bonusXP = 0;

    /// <summary>XP 변경 이벤트 (currentXP, maxXP)</summary>
    public System.Action<int, int> OnXPChanged;

    /// <summary>레벨 변경 이벤트 (newLevel)</summary>
    public System.Action<int> OnLevelChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>XP를 추가하고 레벨업 여부를 확인.</summary>
    public void AddXP(int amount)
    {
        CurrentXP += amount + bonusXP;

        while (true)
        {
            int maxXP = GetCurrentThreshold();
            OnXPChanged?.Invoke(CurrentXP, maxXP);
            if (maxXP > 0 && CurrentXP >= maxXP)
                OnLevelUp();
            else
                break;
        }
    }

    void OnLevelUp()
    {
        CurrentXP = 0;
        CurrentLevel++;
        OnLevelChanged?.Invoke(CurrentLevel);

        // 업그레이드는 피트스톱 거점 진입 시 트리거 (PitStopZone.cs)
    }

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 XP. 임계값 범위 초과 시 마지막 값 사용.</summary>
    public int GetCurrentThreshold()
    {
        if (xpThresholds == null || xpThresholds.Length == 0) return 10;
        int idx = Mathf.Clamp(CurrentLevel - 1, 0, xpThresholds.Length - 1);
        return xpThresholds[idx];
    }
}
