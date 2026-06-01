using UnityEngine;

/// <summary>
/// 시작 무기 선택을 씬 너머로 전달하는 정적 보관소이자 무기 정의의 단일 출처.
/// WeaponSelectUI가 SelectedIndex를 정하고, 게임 씬의 PlayerCombat이 Awake에서 읽어 스탯을 적용한다.
/// 미선택(SelectedIndex<0)이면 PlayerCombat은 인스펙터 기본값을 그대로 쓴다.
///
/// 무기는 소음-스텔스 핵심 긴장과 엮인다: 산탄총은 강하지만 시끄러워 호드를 부르고,
/// 소총은 연사가 빠르고 한 발당 약간 조용하다.
/// </summary>
public static class WeaponLoadout
{
    public struct Weapon
    {
        public string name;
        public string desc;
        public int damage;
        public float fireCooldown;
        public float range;
        public float gunshotNoise;
    }

    public static readonly Weapon[] Weapons =
    {
        new Weapon { name = "권총",   desc = "균형 잡힌 기본 무기",            damage = 2, fireCooldown = 0.35f, range = 18f, gunshotNoise = 90f },
        new Weapon { name = "소총",   desc = "빠른 연사 · 긴 사거리 · 낮은 피해", damage = 1, fireCooldown = 0.18f, range = 22f, gunshotNoise = 85f },
        new Weapon { name = "산탄총", desc = "강한 피해 · 짧은 사거리 · 큰 소음", damage = 4, fireCooldown = 0.70f, range = 11f, gunshotNoise = 100f },
    };

    public static int SelectedIndex = -1;   // -1 = 미선택 → 인스펙터 기본값 유지

    public static bool HasSelection => SelectedIndex >= 0 && SelectedIndex < Weapons.Length;
    public static Weapon Selected => Weapons[SelectedIndex];
}
