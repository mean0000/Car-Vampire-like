using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 라운드의 업그레이드 카드 4장을 구성.
/// 어피니티 가중치, PityTracker 보장, 중복 방지를 적용.
/// </summary>
public class CardPoolBuilder
{
    readonly PlayerWeaponInventory _inv;
    readonly TreeAffinityTracker _affinity;
    readonly WeaponData[] _allWeapons;

    public CardPoolBuilder(PlayerWeaponInventory inv, TreeAffinityTracker affinity, WeaponData[] allWeapons)
    {
        _inv = inv;
        _affinity = affinity;
        _allWeapons = allWeapons ?? new WeaponData[0];
    }

    /// <summary>
    /// 카드 4장 빌드.
    /// isFirstPitStop: 첫 피트스톱이면 4트리 각 1장 Normal 해금 고정 반환.
    /// </summary>
    public List<UpgradeCard> Build(float progression, int pendingLevels, PityTracker pity, bool isFirstPitStop)
    {
        if (isFirstPitStop)
            return BuildFirstPitStop();

        var result = new List<UpgradeCard>(4);

        // 보유 수에 따라 해금/강화 슬롯 비율 결정
        int owned = _inv.OwnedCount;
        int unlockSlots = owned < 4 ? 3 : owned < 8 ? 2 : 1;
        int upgradeSlots = 4 - unlockSlots;

        // 해금/강화 풀 구성
        var unlockPool = BuildUnlockPool();
        var upgradePool = BuildUpgradePool();

        var usedWeapons = new HashSet<WeaponData>();
        var usedTrees   = new Dictionary<WeaponTree, int>();

        // pity 보장은 첫 번째 upgrade 슬롯에 적용 (unlock 슬롯은 항상 Normal이라 의미 없음)
        WeaponRarity? guaranteed = pity.GetGuaranteed();
        bool guaranteedApplied = false;

        for (int i = 0; i < 4; i++)
        {
            bool isUnlockSlot = i < unlockSlots;
            var pool = isUnlockSlot ? unlockPool : upgradePool;

            // 풀이 비어있으면 반대 타입으로 폴백
            if (pool.Count == 0) pool = isUnlockSlot ? upgradePool : unlockPool;
            if (pool.Count == 0) break;

            // 중복 및 트리 제한 필터링
            var filtered = FilterPool(pool, usedWeapons, usedTrees);
            if (filtered.Count == 0)
            {
                // 트리 제한만 완화, 중복 무기는 끝까지 금지
                filtered = new List<WeaponData>();
                foreach (var w in pool)
                    if (!usedWeapons.Contains(w))
                        filtered.Add(w);
            }

            WeaponData picked = WeightedRandom(filtered);
            if (picked == null) break;

            // 희귀도 결정: pity 보장은 첫 번째 upgrade 슬롯에만 적용
            WeaponRarity rarity;
            bool applyGuarantee = !isUnlockSlot && !guaranteedApplied && guaranteed.HasValue;
            if (applyGuarantee)
            {
                rarity = guaranteed.Value;
                guaranteedApplied = true;
            }
            else
            {
                rarity = DetermineRarity(picked, isUnlockSlot, progression, pendingLevels);
            }

            var card = new UpgradeCard
            {
                Type         = isUnlockSlot ? CardType.Unlock : CardType.Upgrade,
                Weapon       = picked,
                TargetRarity = isUnlockSlot ? WeaponRarity.Normal : rarity
            };

            result.Add(card);
            usedWeapons.Add(picked);
            usedTrees.TryGetValue(picked.tree, out int treeCount);
            usedTrees[picked.tree] = treeCount + 1;
        }

        return result;
    }

    // ── 첫 피트스톱: 4트리 각 1장 ────────────────────────────────────────

    List<UpgradeCard> BuildFirstPitStop()
    {
        var result = new List<UpgradeCard>(4);

        foreach (WeaponTree tree in System.Enum.GetValues(typeof(WeaponTree)))
        {
            WeaponData first = FindFirstUnlockable(tree);
            if (first == null) continue;

            result.Add(new UpgradeCard
            {
                Type         = CardType.Unlock,
                Weapon       = first,
                TargetRarity = WeaponRarity.Normal
            });
        }

        return result;
    }

    WeaponData FindFirstUnlockable(WeaponTree tree)
    {
        foreach (var w in _allWeapons)
        {
            if (w != null && w.tree == tree && !_inv.IsOwned(w))
                return w;
        }
        return null;
    }

    // ── 풀 구성 ────────────────────────────────────────────────────────────

    List<WeaponData> BuildUnlockPool()
    {
        var pool = new List<WeaponData>();
        foreach (var w in _allWeapons)
        {
            if (w != null && !_inv.IsOwned(w))
                pool.Add(w);
        }
        return pool;
    }

    List<WeaponData> BuildUpgradePool()
    {
        var pool = new List<WeaponData>();
        foreach (var w in _inv.OwnedWeapons)
        {
            if (_inv.CanUpgrade(w))
                pool.Add(w);
        }
        return pool;
    }

    // ── 필터: 중복 방지, 트리 최대 2장 ────────────────────────────────────

    List<WeaponData> FilterPool(List<WeaponData> pool, HashSet<WeaponData> used, Dictionary<WeaponTree, int> treeCounts)
    {
        var filtered = new List<WeaponData>();
        foreach (var w in pool)
        {
            if (used.Contains(w)) continue;
            treeCounts.TryGetValue(w.tree, out int cnt);
            if (cnt >= 2) continue;
            filtered.Add(w);
        }
        return filtered;
    }

    // ── 어피니티 가중치 기반 랜덤 선택 ────────────────────────────────────

    WeaponData WeightedRandom(List<WeaponData> pool)
    {
        if (pool == null || pool.Count == 0) return null;

        float totalWeight = 0f;
        foreach (var w in pool)
            totalWeight += Mathf.Max(0f, _affinity.GetWeight(w.tree));

        if (totalWeight <= 0f)
            return pool[Random.Range(0, pool.Count)];

        float r = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var w in pool)
        {
            cumulative += Mathf.Max(0f, _affinity.GetWeight(w.tree));
            if (r <= cumulative) return w;
        }

        return pool[pool.Count - 1];
    }

    // ── 희귀도 결정 ────────────────────────────────────────────────────────

    WeaponRarity DetermineRarity(WeaponData w, bool isUnlock, float progression, int pendingLevels)
    {
        if (isUnlock) return WeaponRarity.Normal;

        // 강화 카드: RarityRoller 결과와 실제 다음 등급 중 낮은 쪽
        WeaponRarity rolled  = RarityRoller.Roll(progression, pendingLevels);
        WeaponRarity nextRar = _inv.NextRarity(w);
        return (WeaponRarity)Mathf.Min((int)rolled, (int)nextRar);
    }
}
