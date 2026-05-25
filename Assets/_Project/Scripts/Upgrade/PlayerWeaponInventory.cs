using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 플레이어가 보유한 무기와 등급을 관리하는 싱글톤.
/// 다른 시스템보다 먼저 초기화되어야 하므로 DefaultExecutionOrder(-90).
/// </summary>
[DefaultExecutionOrder(-90)]
public class PlayerWeaponInventory : MonoBehaviour
{
    public static PlayerWeaponInventory Instance { get; private set; }

    readonly Dictionary<WeaponData, WeaponRarity> _owned = new Dictionary<WeaponData, WeaponRarity>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 조회 ──────────────────────────────────────────────────────────────

    public bool IsOwned(WeaponData w) => w != null && _owned.ContainsKey(w);

    public WeaponRarity GetRarity(WeaponData w) => _owned.TryGetValue(w, out WeaponRarity r) ? r : WeaponRarity.Normal;

    public int OwnedCount => _owned.Count;

    /// <summary>보유 중인 무기 목록 (읽기 전용).</summary>
    public IReadOnlyList<WeaponData> OwnedWeapons
    {
        get
        {
            var list = new List<WeaponData>(_owned.Count);
            foreach (var key in _owned.Keys) list.Add(key);
            return list;
        }
    }

    // ── 조작 ──────────────────────────────────────────────────────────────

    /// <summary>무기를 Normal 등급으로 해금.</summary>
    public void Unlock(WeaponData w)
    {
        if (w == null || _owned.ContainsKey(w)) return;
        _owned[w] = WeaponRarity.Normal;
    }

    /// <summary>보유 중이고 Legendary 미만이면 다음 등급으로 강화.</summary>
    public void Upgrade(WeaponData w)
    {
        if (!CanUpgrade(w)) return;
        _owned[w] = NextRarity(w);
    }

    public bool CanUpgrade(WeaponData w) => IsOwned(w) && _owned[w] < WeaponRarity.Legendary;

    /// <summary>현재 등급의 다음 등급. Legendary면 Legendary 그대로.</summary>
    public WeaponRarity NextRarity(WeaponData w)
    {
        if (!_owned.TryGetValue(w, out WeaponRarity cur)) return WeaponRarity.Normal;
        return cur < WeaponRarity.Legendary ? cur + 1 : WeaponRarity.Legendary;
    }
}
