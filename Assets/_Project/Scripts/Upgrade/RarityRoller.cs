using UnityEngine;

/// <summary>
/// 희귀도 랜덤 결정 유틸리티.
/// progression: 0~1 (게임 진행도), pendingLevels: 쌓인 미처리 레벨 수.
/// </summary>
public static class RarityRoller
{
    /// <summary>
    /// 희귀도를 확률 테이블에 따라 결정.
    /// pendingLevels >= 5부터 뱅크 보너스가 Legendary 확률을 최대 100% 추가.
    /// </summary>
    public static WeaponRarity Roll(float progression, int pendingLevels)
    {
        float bankBonus = Mathf.Clamp01((pendingLevels - 4) * 0.03f);
        float leg  = 5f  + 15f * progression + bankBonus * 100f;
        float rare = 25f + 15f * progression;
        // norm은 남은 확률 (음수 방지)
        float norm = Mathf.Max(0f, 100f - leg - rare);

        float total = leg + rare + norm;
        float r = Random.Range(0f, total);

        if (r < leg)           return WeaponRarity.Legendary;
        if (r < leg + rare)    return WeaponRarity.Rare;
        return WeaponRarity.Normal;
    }
}
