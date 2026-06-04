using UnityEngine;

/// <summary>
/// 시작 무기 선택을 씬 너머로 전달하는 정적 보관소이자 무기 정의의 단일 출처.
/// WeaponSelectUI가 SelectedIndex를 정하고, 게임 씬의 PlayerCombat이 Awake에서 읽어 적용한다.
/// 미선택(SelectedIndex<0)이면 PlayerCombat은 인스펙터 기본값(원거리)을 그대로 쓴다.
///
/// 데모 무기 라인업(docs/00_authority/2026-06-03-demo-weapon-lineup.md):
/// - 리볼버(원거리): 대가 = 소음. 묵직한 한 발, 낮은 연사.
/// - 야구방망이(근접): 대가 = 체력(밀착). 넓은 클리브, 약한 넉백, 무빙하며 패기.
/// - 쇠지렛대(방망이 진화형): 좁고 강한 단타. 데모 테스트용으로 선택지에도 노출.
/// </summary>
public static class WeaponLoadout
{
    public enum Kind { Ranged = 0, Melee = 1 }     // Ranged=0 → struct 기본값이 원거리(안전)

    /// <summary>무기별 죽음 연출 형태. None=비무기 사망(좀비 자체 폴백 연출).</summary>
    public enum DeathStyle { None = 0, Splat = 1, Crunch = 2 }

    public struct Weapon
    {
        public string name;
        public string desc;
        public Kind kind;

        // 공유/원거리 필드 (근접은 의미를 재사용: fireCooldown=스윙쿨, range=리치, gunshotNoise=스윙소음)
        public int damage;
        public float fireCooldown;
        public float range;
        public float gunshotNoise;

        // 근접 전용 (원거리는 0/None)
        public float arcHalfAngle;   // 전방 부채꼴 반각(도)
        public float knockback;      // 피격 시 초기 넉백 속도(m/s)
        public float stagger;        // 경직 시간(초) — 좀비 AI 이동 일시정지
        public float hitstop;        // 타격 히트스탑(초)
        public DeathStyle deathStyle;
    }

    public static readonly Weapon[] Weapons =
    {
        new Weapon {
            name = "리볼버", desc = "묵직한 한 발 · 긴 사거리 · 큰 소음", kind = Kind.Ranged,
            damage = 3, fireCooldown = 0.5f, range = 20f, gunshotNoise = 95f,
            arcHalfAngle = 0f, knockback = 0f, stagger = 0f, hitstop = 0f, deathStyle = DeathStyle.None,
        },
        new Weapon {
            name = "야구방망이", desc = "넓게 후려치기 · 약한 넉백 · 조용함 (밀착 위험)", kind = Kind.Melee,
            damage = 2, fireCooldown = 0.5f, range = 2.2f, gunshotNoise = 25f,
            arcHalfAngle = 50f, knockback = 6f, stagger = 0.15f, hitstop = 0.04f, deathStyle = DeathStyle.Splat,
        },
        new Weapon {
            name = "쇠지렛대", desc = "좁고 강한 단타 · 강한 넉백 (방망이 진화형)", kind = Kind.Melee,
            damage = 6, fireCooldown = 0.45f, range = 2.5f, gunshotNoise = 32f,
            arcHalfAngle = 32f, knockback = 11f, stagger = 0.28f, hitstop = 0.08f, deathStyle = DeathStyle.Crunch,
        },
    };

    public static int SelectedIndex = -1;   // -1 = 미선택 → 인스펙터 기본값 유지

    public static bool HasSelection => SelectedIndex >= 0 && SelectedIndex < Weapons.Length;
    public static Weapon Selected => Weapons[SelectedIndex];

    /// <summary>야구방망이(인덱스 1)의 진화형 = 쇠지렛대(인덱스 2). 데모 라이브 진화용.</summary>
    public static Weapon BaseBat => Weapons[1];
    public static Weapon EvolvedCrowbar => Weapons[2];
}
