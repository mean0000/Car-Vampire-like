using UnityEngine;

/// <summary>
/// XP 및 레벨 관리 싱글톤.
/// </summary>
[DefaultExecutionOrder(-100)]
public class XPManager : MonoBehaviour
{
    public static XPManager Instance { get; private set; }

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXP { get; private set; } = 0;

    /// <summary>피트스톱에서 아직 처리되지 않은 레벨업 횟수.</summary>
    public int PendingLevels { get; private set; } = 0;

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
        // 효율 흡수 카드: 획득량 배수 적용.
        CurrentXP += Mathf.Max(0, Mathf.RoundToInt((amount + bonusXP) * PlayerStats.XPGainMult));

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
        PendingLevels++;
        OnLevelChanged?.Invoke(CurrentLevel);

        // 업그레이드는 피트스톱 거점 진입 시 트리거 (PitStopZone.cs)
    }

    /// <summary>
    /// 현재 레벨에서 다음 레벨까지 필요한 XP.
    /// 공식: 10 + (level * 8) + (level * level * 2), 40레벨까지 스케일.
    /// </summary>
    public int GetCurrentThreshold()
    {
        int lv = Mathf.Clamp(CurrentLevel, 1, 40);
        return 10 + (lv * 8) + (lv * lv * 2);
    }

    /// <summary>PendingLevels가 남아있으면 1 소비하고 true 반환. 없으면 false.</summary>
    public bool ConsumePendingLevel()
    {
        if (PendingLevels <= 0) return false;
        PendingLevels--;
        return true;
    }
}
