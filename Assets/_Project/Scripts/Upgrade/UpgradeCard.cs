/// <summary>업그레이드 카드 한 장의 데이터. UI와 로직 모두 이 객체를 기준으로 동작.</summary>
public enum CardType { Unlock, Upgrade }

public class UpgradeCard
{
    public CardType Type;
    public WeaponData Weapon;

    /// <summary>해금이면 Normal, 강화이면 현재 등급의 다음 등급.</summary>
    public WeaponRarity TargetRarity;

    public string GetTitle() => Type == CardType.Unlock
        ? $"[신규] {Weapon.weaponName}"
        : $"{Weapon.weaponName} 강화";

    public string GetDesc() => Weapon.description;
}
