using System.Collections.Generic;

/// <summary>
/// 플레이어의 트리 선호도를 추적. 선택 이력에 따라 카드 풀 가중치에 영향.
/// </summary>
public class TreeAffinityTracker
{
    readonly Dictionary<WeaponTree, float> _scores = new Dictionary<WeaponTree, float>();

    public TreeAffinityTracker()
    {
        foreach (WeaponTree t in System.Enum.GetValues(typeof(WeaponTree)))
            _scores[t] = 0f;
    }

    /// <summary>해금 선택 시 해당 트리 점수 +2.</summary>
    public void OnUnlocked(WeaponTree t) => _scores[t] += 2f;

    /// <summary>강화 선택 시 해당 트리 점수 +1.</summary>
    public void OnUpgraded(WeaponTree t) => _scores[t] += 1f;

    /// <summary>카드 선택 완료 후 모든 점수 10% 감쇠.</summary>
    public void Decay()
    {
        foreach (WeaponTree t in System.Enum.GetValues(typeof(WeaponTree)))
            _scores[t] *= 0.9f;
    }

    /// <summary>카드풀 가중치. 기본 1, 점수가 높을수록 선택 확률 상승.</summary>
    public float GetWeight(WeaponTree t) => 1f + _scores[t] * 0.5f;
}
