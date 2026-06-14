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

    /// <summary>
    /// 무기별 우클릭 보조 발사(alt-fire). 좌클릭(주발사)을 보완하는 "위기를 끊는 한 수".
    /// - None: 우클릭 없음(근접 등).
    /// - FanFire(리볼버): 실린더를 순식간에 난사 → 후 장전 공백. 낮은 연사를 패닉 클리어로 보완.
    /// - ChargePierce(라이플): 홀드 차징 → 일직선 관통탄(좀비 줄을 꿰뚫음). 단일표적 한계를 라인 삭제로 보완.
    /// - StockBash(샷건): 개머리판 전방 광역 넉백+경직(탄약 0, 즉발). 밀착 위험을 "꺼져" 버튼으로 보완.
    /// 튜닝 수치는 PlayerCombat 인스펙터(게임감 반복용). 여기선 어떤 보조기인지 선택만.
    /// </summary>
    public enum AltFire { None = 0, FanFire = 1, ChargePierce = 2, StockBash = 3 }

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

        // 원거리 산탄/정확도 (근접은 0)
        public int pelletCount;   // 1발당 발사 펠릿 수. 1=단발(리볼버/라이플), N=산탄(샷건)
        public float spread;      // 좌클릭 산포 반각(도) — 좌우로 튀는 정도

        // 탄약/재장전 (원거리 전용. magazine=0이면 탄약 없음/무한 = 근접)
        public int magazine;      // 탄창 크기(0=무탄약/무한)
        public float reloadTime;  // 재장전 시간(초)

        // 근접 전용 (원거리는 0/None)
        public float arcHalfAngle;   // 전방 부채꼴 반각(도)
        public float knockback;      // 피격 시 초기 넉백 속도(m/s)
        public float stagger;        // 경직 시간(초) — 좀비 AI 이동 일시정지
        public float hitstop;        // 타격 히트스탑(초)
        public DeathStyle deathStyle;

        // 우클릭 보조 발사 종류(원거리 전용). 근접/미선택은 None.
        public AltFire altFire;
    }

    public static readonly Weapon[] Weapons =
    {
        new Weapon {
            name = "리볼버", desc = "묵직한 한 발 · 긴 사거리 · 큰 소음", kind = Kind.Ranged,
            damage = 3, fireCooldown = 0.5f, range = 20f, gunshotNoise = 95f,
            pelletCount = 1, spread = 7f,
            magazine = 6, reloadTime = 1.1f,
            arcHalfAngle = 0f, knockback = 0f, stagger = 0f, hitstop = 0f, deathStyle = DeathStyle.None,
            altFire = AltFire.FanFire,
        },
        new Weapon {
            name = "야구방망이", desc = "넓게 후려치기 · 약한 넉백 · 조용함 (밀착 위험)", kind = Kind.Melee,
            damage = 2, fireCooldown = 0.5f, range = 2.2f, gunshotNoise = 25f,
            pelletCount = 0, spread = 0f,
            magazine = 0, reloadTime = 0f,
            arcHalfAngle = 50f, knockback = 6f, stagger = 0.15f, hitstop = 0.04f, deathStyle = DeathStyle.Splat,
        },
        new Weapon {
            name = "쇠지렛대", desc = "좁고 강한 단타 · 강한 넉백 (방망이 진화형)", kind = Kind.Melee,
            damage = 6, fireCooldown = 0.45f, range = 2.5f, gunshotNoise = 32f,
            pelletCount = 0, spread = 0f,
            magazine = 0, reloadTime = 0f,
            arcHalfAngle = 32f, knockback = 11f, stagger = 0.28f, hitstop = 0.08f, deathStyle = DeathStyle.Crunch,
        },
        new Weapon {
            name = "라이플", desc = "빠른 연사 · 정밀 · 긴 사거리 (지속 소음 큼)", kind = Kind.Ranged,
            damage = 2, fireCooldown = 0.12f, range = 26f, gunshotNoise = 60f,
            pelletCount = 1, spread = 3f,
            magazine = 24, reloadTime = 1.6f,
            arcHalfAngle = 0f, knockback = 0f, stagger = 0f, hitstop = 0f, deathStyle = DeathStyle.None,
            altFire = AltFire.ChargePierce,
        },
        new Weapon {
            name = "샷건", desc = "근접 광역 산탄 · 강한 한 방 · 느린 장전 (가장 시끄러움)", kind = Kind.Ranged,
            damage = 1, fireCooldown = 0.85f, range = 11f, gunshotNoise = 105f,
            pelletCount = 8, spread = 10f,
            magazine = 5, reloadTime = 2.0f,
            arcHalfAngle = 0f, knockback = 0f, stagger = 0f, hitstop = 0f, deathStyle = DeathStyle.None,
            altFire = AltFire.StockBash,
        },
        // 카타나(인덱스 5) — 증명 슬라이스 Phase1. KatanaController가 거합/참격 두 모드로 분기 구동한다.
        // 평타 베이스값(데미지·리치·부채꼴)은 여기서, 모드별 차등(공속·콤보·발도)은 KatanaController 노브에서.
        new Weapon {
            name = "카타나", desc = "거합/참격 두 모드 · 연쇄 가속 (증명 슬라이스)", kind = Kind.Melee,
            damage = 3, fireCooldown = 0.45f, range = 1.8f, gunshotNoise = 22f,
            pelletCount = 0, spread = 0f,
            magazine = 0, reloadTime = 0f,
            arcHalfAngle = 50f, knockback = 4f, stagger = 0.12f, hitstop = 0.05f, deathStyle = DeathStyle.Splat,
        },
    };

    public static int SelectedIndex = -1;   // -1 = 미선택 → 인스펙터 기본값 유지

    public static bool HasSelection => SelectedIndex >= 0 && SelectedIndex < Weapons.Length;
    public static Weapon Selected => Weapons[SelectedIndex];

    /// <summary>야구방망이(인덱스 1)의 진화형 = 쇠지렛대(인덱스 2). 데모 라이브 진화용.</summary>
    public static Weapon BaseBat => Weapons[1];
    public static Weapon EvolvedCrowbar => Weapons[2];

    // 원거리 3종 — 데모 숫자키 라이브 스왑용(PlayerCombat).
    public static Weapon Revolver => Weapons[0];
    public static Weapon Rifle => Weapons[3];
    public static Weapon Shotgun => Weapons[4];

    /// <summary>카타나(인덱스 5) — 증명 슬라이스 Phase1 데모 장착용.</summary>
    public static Weapon Katana => Weapons[5];
}
