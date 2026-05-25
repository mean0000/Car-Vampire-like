using System.Collections.Generic;

/// <summary>
/// 천장 시스템. 일정 라운드 동안 Rare/Legendary가 나오지 않으면 보장.
/// Legendary 12라운드 미등장 → 다음 라운드 첫 슬롯 Legendary 보장.
/// Rare    4라운드 미등장 → 다음 라운드 첫 슬롯 Rare 이상 보장.
/// </summary>
public class PityTracker
{
    int _roundsSinceRare = 0;
    int _roundsSinceLegendary = 0;

    const int LegendaryPity = 12;
    const int RarePity = 4;

    /// <summary>이번 라운드 첫 슬롯에 보장할 희귀도. 없으면 null.</summary>
    public WeaponRarity? GetGuaranteed()
    {
        if (_roundsSinceLegendary >= LegendaryPity) return WeaponRarity.Legendary;
        if (_roundsSinceRare >= RarePity)           return WeaponRarity.Rare;
        return null;
    }

    /// <summary>실제 선택한 카드를 기준으로 카운터 업데이트.</summary>
    public void OnCardPicked(UpgradeCard picked)
    {
        if (picked == null) return;

        bool pickedLegendary = picked.TargetRarity == WeaponRarity.Legendary;
        bool pickedRare      = picked.TargetRarity >= WeaponRarity.Rare;

        _roundsSinceLegendary = pickedLegendary ? 0 : _roundsSinceLegendary + 1;
        _roundsSinceRare      = pickedRare      ? 0 : _roundsSinceRare + 1;
    }
}
