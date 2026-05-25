using UnityEngine;

public enum WeaponTree { Survival, Melee, Ranged, Architect }
public enum WeaponRarity { Normal, Rare, Legendary }

/// <summary>
/// 무기 하나를 정의하는 ScriptableObject.
/// Unity Editor에서 Assets/Create/ZombieCrush/WeaponData 로 생성.
/// </summary>
[CreateAssetMenu(menuName = "ZombieCrush/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public WeaponTree tree;
    public WeaponRarity baseRarity;
    [TextArea] public string description;
    public Sprite icon;
}
