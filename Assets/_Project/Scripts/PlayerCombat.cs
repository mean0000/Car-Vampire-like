using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 탑다운 마우스 조준 전투. 몸통은 회전시키지 않고(카메라가 플레이어 자식이라
/// 회전 시 화면이 같이 돌아 어지러움), 마우스 지면 투영으로 조준 방향만 계산한다.
///
/// 무기 종류(WeaponLoadout.Kind)에 따라 분기한다:
/// - Ranged(총): 좌클릭 홀드 = 쿨마다 히트스캔(스피어캐스트). 벽에 막히고 좀비를 맞히면 데미지.
///   총성은 NoiseManager로 큰 순간 소음 → 호드 유발(핵심 긴장).
/// - Melee(근접): MeleeAttacker가 스윙/판정/넉백/연출을 전담. 좌클릭 홀드 = 쿨마다 자동 스윙.
///   이동이 스윙을 막지 않아 무빙하며 패기(run-and-bash). 소음은 낮다(근접의 정체성).
///
/// 좌클릭(주발사)은 두 무기 모두 「홀드 = 쿨마다 반복」으로 통일. 우클릭은 원거리 무기별 보조 발사(alt-fire):
/// 리볼버=패닝(난사), 라이플=관통 차지샷(홀드 차징), 샷건=개머리판 밀치기(즉발 넉백). WeaponLoadout.AltFire로 분기.
/// </summary>
// PlayerController(기본 0)보다 먼저 실행 — _aimDir을 같은 프레임에 확정해 로코모션 facing이 1프레임 늦지 않게.
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] int damage = 2;              // General 좀비 HP 3 → 2발
    [SerializeField] float fireCooldown = 0.35f;
    [SerializeField] float range = 18f;
    [SerializeField] float hitRadius = 0.4f;      // 스피어캐스트 조준 관용
    [SerializeField] float muzzleHeight = 1f;
    [Tooltip("샷건 펠릿이 좀비를 미는 힘(m/s) — 강한 푸시.")]
    [SerializeField, Min(0f)] float bulletKnockback = 5f;
    [Tooltip("샷건 외 총(권총·라이플)의 피탄 넉백(m/s). 전진 의지와 충돌 → 연사 받으면 '으그극' 버티며 기어오고, 멈추면 전진. 크면 뒤로 밀리고 작으면 그냥 전진.")]
    [SerializeField, Min(0f)] float weakKnockback = 3.4f;
    [SerializeField] int magazineSize = 6;        // 미선택 폴백(원거리) 탄창 크기
    [SerializeField] float reloadTime = 1.1f;     // 미선택 폴백(원거리) 재장전 시간

    [Header("Combat Feel (게이트0 — 3계층 탄 판정·히트스탑. 비우면 기본값으로 런타임 인스턴스 생성)")]
    [Tooltip("combat-texture-foundation §6.2 수치의 거처. 랩 튜닝은 이 SO 에셋만 만진다.")]
    [SerializeField] CombatFeelConfig feel;

    [Header("Layers")]
    [SerializeField] LayerMask zombieMask = 1 << 7;
    [SerializeField] LayerMask obstacleMask = 1 << 8;   // 총알이 벽에 막힘

    [Header("Noise")]
    [SerializeField] float gunshotNoise = 90f;    // 추격 임계(50) 훨씬 위 — 쏘면 들킨다

    [Header("Charge Brackets (라이플 차지 — 레이저 좌우에서 수렴하는 브라켓)")]
    [Tooltip("차지 0%일 때 브라켓 길이(m). 차징할수록 관통 사거리 끝까지 뻗는다.")]
    [SerializeField] float chargeLength = 4f;
    [Tooltip("차지 0%일 때 레이저 좌우로 벌어진 거리(m). 차징할수록 0으로 수렴해 한 줄기가 된다.")]
    [SerializeField] float chargeSideMax = 1.1f;
    [Tooltip("저차지 브라켓 두께(m).")]
    [SerializeField] float chargeWidthMin = 0.03f;
    [Tooltip("풀차지 브라켓 두께(m) — 굵어진 상태로 충전 완료를 표현.")]
    [SerializeField] float chargeWidthMax = 0.16f;
    [Tooltip("저차지 색(흰 반투명) → 풀차지 색으로 보간.")]
    [SerializeField] Color chargeColorLow = new Color(1f, 1f, 1f, 0.3f);
    [Tooltip("풀차지 색 — 빨갛게 달아오른 한 줄기.")]
    [SerializeField] Color chargeColorHigh = new Color(1f, 0.1f, 0.08f, 0.95f);

    [Header("Laser Sight (조준 레이저 — 총 들면 상시 표시)")]
    [Tooltip("레이저 선 두께(m). 얇고 어둡게 — 조준 보조용.")]
    [SerializeField] float laserWidth = 0.025f;
    [Tooltip("레이저 선 색(어둡게).")]
    [SerializeField] Color laserColor = new Color(1f, 0.15f, 0.12f, 0.5f);
    [Tooltip("레이저가 또렷하게 보이는 최대 길이(m). 이 너머는 그려지지 않고 끝부분이 점점 사라진다.")]
    [SerializeField, Min(0.5f)] float laserLength = 6f;
    [Tooltip("또렷한 비율(0~1). 이 지점부터 선 끝까지 알파가 0으로 페이드 — 레이저처럼 트레일 오프.")]
    [SerializeField, Range(0f, 1f)] float laserFadeStart = 0.45f;
    [Tooltip("착탄점 도트 크기(m).")]
    [SerializeField] float laserDotSize = 0.13f;
    [Tooltip("착탄점 도트 색(레이저보다 밝게).")]
    [SerializeField] Color laserDotColor = new Color(1f, 0.35f, 0.28f, 0.9f);

    [Header("Bullet Tracer (날아가는 탄 — 트레일)")]
    [Tooltip("탄 비주얼 속도(m/s). 판정은 즉발이고 이건 연출 전용 — 클수록 즉발에 가깝게 보인다.")]
    [SerializeField, Min(0.001f)] float bulletSpeed = 90f;   // 0이면 머리가 안 나가 트레일이 영구 정지하는 footgun 차단
    [Tooltip("트레일 잔상 지속(초). 길수록 꼬리가 길게 늘어진다.")]
    [SerializeField, Min(0.01f)] float trailTime = 0.06f;
    [Tooltip("트레일 머리 두께(m). 꼬리로 갈수록 0으로 가늘어진다.")]
    [SerializeField, Min(0.001f)] float trailWidth = 0.07f;
    [Tooltip("트레일 HDR 색 — 채널>1이면 블룸으로 빛난다(메인 씬 블룸 ON). 따뜻한 예광탄.")]
    [ColorUsage(true, true)][SerializeField] Color tracerColor = new Color(4f, 2.2f, 0.8f, 1f);

    [Header("Muzzle Flash (총구 화염 VFX — 코드 생성 빌보드 가산 HDR)")]
    [Tooltip("(선택) 씬에 수동 배치한 머즐 라이트. 비우면 아래 값으로 코드 라이트를 생성·구동한다.")]
    [SerializeField] GunFlashLight gunFlashLight;
    [Tooltip("총구 화염 HDR 색 — 뜨거운 화이트-앰버. 채널>1이면 블룸으로 빛난다.")]
    [ColorUsage(true, true)][SerializeField] Color muzzleFlashColor = new Color(6f, 3.5f, 1.2f, 1f);
    [Tooltip("총구 화염 최대 크기(m).")]
    [SerializeField, Min(0.01f)] float muzzleFlashSize = 0.55f;
    [Tooltip("총구 화염 지속(초) — 임팩트보다 짧게 번쩍.")]
    [SerializeField, Min(0.01f)] float muzzleFlashTime = 0.05f;
    [Tooltip("총구 위치를 조준 방향으로 앞당기는 거리(m) — 화염·라이트가 총신 끝에 오도록.")]
    [SerializeField] float muzzleForward = 0.45f;

    [Header("Muzzle Light (주위 밝기 — gunFlashLight 미배치 시 코드 생성)")]
    [Tooltip("발사 순간 피크 광량.")]
    [SerializeField] float muzzleLightIntensity = 12f;
    [Tooltip("머즐 라이트 사거리(m) — 주위를 밝히는 범위.")]
    [SerializeField] float muzzleLightRange = 7f;
    [Tooltip("머즐 라이트 지속(초) — 짧고 강한 스파이크.")]
    [SerializeField, Min(0.01f)] float muzzleLightDuration = 0.06f;
    [Tooltip("머즐 라이트 색 — 따뜻한 앰버.")]
    [SerializeField] Color muzzleLightColor = new Color(1f, 0.72f, 0.34f, 1f);

    [Header("Audio (발사·재장전 사운드 — Resources/SFX/Guns)")]
    [Tooltip("발사 사운드 볼륨.")]
    [SerializeField, Range(0f, 1f)] float shotVolume = 0.185f;
    [Tooltip("재장전 사운드 볼륨.")]
    [SerializeField, Range(0f, 1f)] float reloadVolume = 0.25f;

    [Header("Impact Flash (명중 플래시 — 코드 생성 · 블룸용 가산 HDR)")]
    [Tooltip("명중 플래시 지속(초). 짧게 팝 — 탄착 순간만 번쩍.")]
    [SerializeField, Min(0.02f)] float impactFlashTime = 0.12f;
    [Tooltip("좀비(살점) 명중 플래시 최대 크기(m).")]
    [SerializeField, Min(0.01f)] float zombieFlashSize = 0.7f;
    [Tooltip("벽(스파크) 명중 플래시 최대 크기(m).")]
    [SerializeField, Min(0.01f)] float wallFlashSize = 0.5f;
    [Tooltip("좀비 명중 HDR 색 — 따뜻한 살점 히트. 채널>1이면 블룸으로 빛난다.")]
    [ColorUsage(true, true)][SerializeField] Color zombieFlashColor = new Color(5f, 1.3f, 0.6f, 1f);
    [Tooltip("벽 명중 HDR 색 — 차가운 스파크. 채널>1이면 블룸으로 빛난다.")]
    [ColorUsage(true, true)][SerializeField] Color wallFlashColor = new Color(3.5f, 4.5f, 6f, 1f);

    [Header("Spark Pop (탄 튀김 — 명중 시 사방으로 튀는 밝은 스파크 줄기)")]
    [Tooltip("스파크 HDR 색 — 뜨거운 화이트-옐로(채널>1 블룸).")]
    [ColorUsage(true, true)][SerializeField] Color sparkColor = new Color(9f, 7.5f, 3.5f, 1f);
    [Tooltip("명중 1회당 튀는 스파크 줄기 개수.")]
    [SerializeField, Min(1)] int sparkBurstCount = 16;
    [Tooltip("발사 시 카메라 쉐이크 세기(m). 작게 — 연사 시 잔잔한 럼블. 샷건은 자동 2배.")]
    [SerializeField, Min(0f)] float fireShake = 0f;

    [Header("Impact Override (선택 — 채우면 코드 플래시 대신 이 프리팹 사용)")]
    [Tooltip("좀비 명중 이펙트 프리팹. 비우면 위 코드 플래시 사용. ⚠️URP 호환 셰이더만(BIRP 프리팹은 핑크).")]
    [SerializeField] GameObject zombieHitOverride;
    [Tooltip("벽 명중 이펙트 프리팹. 비우면 위 코드 플래시 사용. ⚠️URP 호환 셰이더만(BIRP 프리팹은 핑크).")]
    [SerializeField] GameObject wallHitOverride;
    [Tooltip("피격 이펙트 프리팹 자동 소멸 시간(초). 짧게 — 빠른 사격에 오브젝트 누적/잔류 최소화(blood≈1.1s는 온전, spark 긴 연기 꼬리는 컷).")]
    [SerializeField, Min(0.1f)] float overrideLifetime = 1.2f;

    [Header("Aim Trailing (마우스 추종 지연)")]
    [Tooltip("조준 추종 반응 속도(1/초). 낮을수록 사격 방향(조준선·탄도)이 마우스보다 더 천천히 따라온다. " +
             "높을수록 즉각. 빠른 스윙 중엔 탄착이 뒤처지므로 정확도에 직접 영향.")]
    [SerializeField, Range(1f, 25f)] float aimResponsiveness = 10f;

    [Header("Spread (미선택 폴백)")]
    [Tooltip("무기 미선택 시 좌클릭 탄이 좌우(yaw)로 벌어지는 최대 각(도). 무기 선택 시엔 무기별 spread 사용.")]
    [SerializeField, Range(0f, 30f)] float hipfireSpread = 7f;

    [Header("Alt-Fire · 리볼버 패닝(Fan Fire)")]
    [Tooltip("난사 발수.")]
    [SerializeField] int fanShots = 3;
    [Tooltip("난사 연사 간격(초).")]
    [SerializeField] float fanInterval = 0.07f;
    [Tooltip("난사 산포 반각(도) — 좌우로 크게 튄다(정확도를 버리고 화력).")]
    [SerializeField] float fanSpread = 13f;
    [Tooltip("난사 후 장전 공백(초) — 이 동안 주발사/패닝 모두 잠금.")]
    [SerializeField] float fanReload = 0.9f;

    [Header("Alt-Fire · 라이플 관통 차지샷(Charge Pierce)")]
    [Tooltip("풀차지까지 걸리는 시간(초).")]
    [SerializeField] float chargeTimeMax = 0.6f;
    [Tooltip("발사 성립 최소 차지(초) — 이보다 짧게 떼면 오발 없이 취소.")]
    [SerializeField] float chargeMinTime = 0.12f;
    [Tooltip("풀차지 데미지 배수(저차지=1배에서 보간).")]
    [SerializeField] float pierceDamageMult = 3f;
    [Tooltip("차지샷 사거리 배수(관통은 멀리 뻗는다).")]
    [SerializeField] float pierceRangeMult = 1.3f;
    [Tooltip("차지샷 발사 후 우클릭 쿨다운(초).")]
    [SerializeField] float pierceCooldown = 0.5f;

    [Header("Alt-Fire · 샷건 개머리판 밀치기(Stock Bash)")]
    [Tooltip("밀치기 사거리(m).")]
    [SerializeField] float bashRange = 3f;
    [Tooltip("전방 부채꼴 반각(도) — 초근접(0.9m 내)은 각도 무시.")]
    [SerializeField, Range(10f, 90f)] float bashArc = 60f;
    [Tooltip("넉백 초기 속도(m/s).")]
    [SerializeField] float bashKnockback = 9f;
    [Tooltip("경직 시간(초) — 좀비 이동 일시정지.")]
    [SerializeField] float bashStagger = 0.5f;
    [Tooltip("밀치기 데미지(주목적은 넉백, 데미지는 양념).")]
    [SerializeField] int bashDamage = 1;
    [Tooltip("밀치기 후 우클릭 쿨다운(초).")]
    [SerializeField] float bashCooldown = 0.7f;
    [Tooltip("밀치기 둔기 소음(총성보다 낮음).")]
    [SerializeField] float bashNoise = 30f;

    [Header("Debug")]
    [SerializeField] KeyCode evolveKey = KeyCode.T;   // 데모용: 방망이 → 쇠지렛대 라이브 진화
    [Tooltip("켜면 숫자키 1/2/3으로 원거리 무기를 라이브 스왑(1=리볼버 2=라이플 3=샷건). 데모 테스트용.")]
    [SerializeField] bool allowWeaponHotswap = true;

    Camera _cam;
    float _cooldownTimer;
    // 메타 업그레이드 배율 — 시작 시 1회 캐시 + MetaUpgrades.OnChanged로 갱신(매 발사 조회 회피).
    float _metaDamageMult = 1f;
    float _metaFireRateMult = 1f;
    Vector3 _aimDir = Vector3.forward;
    bool _aimInitialized;   // 첫 유효 샘플은 스냅(시작 시 forward→커서 스윙 방지)

    // 탄약/재장전 런타임 상태(원거리 전용. _magazine=0이면 무탄약 = 근접).
    int _ammo;
    int _magazine;
    float _reloadTime;
    bool _reloading;
    float _reloadTimer;

    /// <summary>마우스 지면 투영으로 계산된 조준 방향(수평, 정규화). 로코모션 facing 등에서 공유.</summary>
    public Vector3 AimDirection => _aimDir;

    // HUD가 읽는 읽기 전용 탄약 API.
    public bool UsesAmmo => _kind == WeaponLoadout.Kind.Ranged && _magazine > 0;
    public int CurrentAmmo => _ammo;
    public int MagazineSize => _magazine;
    public bool IsReloading => _reloading;
    public float ReloadProgress01 => (_reloading && _reloadTime > 0f) ? 1f - Mathf.Clamp01(_reloadTimer / _reloadTime) : 1f;

    WeaponLoadout.Kind _kind = WeaponLoadout.Kind.Ranged;
    MeleeAttacker _melee;   // 근접일 때만 생성(원거리는 null)

    // 원거리 런타임 스탯(무기에서 채워짐). 미선택 시 인스펙터 기본 + hipfireSpread.
    int _pelletCount = 1;   // 1발당 펠릿 수(샷건은 다수)
    float _spread;          // 좌클릭 산포 반각(도)
    WeaponLoadout.AltFire _altFire;   // 우클릭 보조 발사 종류(무기별)

    // 우클릭 alt-fire 런타임 상태(모든 알트 공용 쿨다운 + 종류별 진행 상태).
    float _altCooldownTimer;
    int _fanShotsLeft;      // 패닝: 남은 난사 발수
    float _fanTimer;        // 패닝: 다음 발까지
    bool _charging;         // 차지샷: 우클릭 홀드 중 차징
    float _chargeTime;      // 차지샷: 누적 차지(초)
    readonly HashSet<ZombieController> _altHits = new HashSet<ZombieController>();   // 개머리판 밀치기 중복타 방지(관통은 발사체별 _bulletHits)

    LineRenderer _chargeL, _chargeR;   // 라이플 차지: 레이저 좌우에서 수렴하는 브라켓(차징 중에만)
    LineRenderer _laserLine, _laserDot;   // 조준 레이저(상시) + 착탄 도트
    Gradient _laserGradient;              // 레이저 페이드(또렷→투명) — 1회 생성 캐시
    TrailRenderer[] _tracers;   // 발사체별 날아가는 탄 트레일(이동시키면 streak 자동 생성). BulletPoolSize만큼 풀.
    Material _tracerMat;         // 트레일 공유 가산 HDR 머티리얼(블룸용 — 모든 트레일이 한 머티리얼 공유).

    // 발사체 1발의 비행 상태. ★판정은 즉발이 아니라 비행 중(UpdateBullets segment 캐스트) — 탄이 닿아야 명중.
    struct Bullet { public bool active; public Vector3 pos; public Vector3 dir; public float remaining; public int damage; public bool pierce; public float knockback; }
    Bullet[] _bullets;
    HashSet<ZombieController>[] _bulletHits;   // 슬롯별 관통 중복타 방지(pierce 탄만 사용).
    int _bulletEvict;                          // 풀 고갈 시 라운드로빈 강제 재사용 인덱스(데미지 유실 방지).
    const int BulletPoolSize = 48;             // 동시 비행 총알 풀(샷건 8펠릿 연발 + 라이플 연사 여유, 고갈 사실상 차단).

    // 명중 플래시 풀 — 코드 생성 빌보드 쿼드(가산 HDR + 라디얼 글로우). 좀비/벽 색만 다르게 재생.
    Transform[] _flashTr;            // 풀 오브젝트 트랜스폼(매 프레임 카메라 빌보드).
    MeshRenderer[] _flashMR;
    MaterialPropertyBlock _flashMPB; // 인스턴스별 HDR 색을 머티리얼 복제 없이 주입.
    float[] _flashTimer;             // 남은 수명(초). <=0 = 유휴.
    float[] _flashLife;              // 이 재생의 총 수명(초) — 임팩트/총구화염이 서로 다른 지속을 갖도록 슬롯별 보관.
    Color[] _flashColor;             // 이 재생의 HDR 색.
    float[] _flashSize;              // 이 재생의 최대 크기(m).
    int _flashEvict;
    Material _impactMat;             // 가산 HDR + 라디얼 텍스처(전 플래시 공유).
    Texture2D _impactTex;            // 코드 생성 라디얼 글로우(부드러운 원형 — 사각 티 제거).
    Mesh _quadMesh;                  // 빌보드 쿼드(인스턴스 소유 — static 공유 시 멀티 OnDestroy 파탄 방지).
    const int FlashPoolSize = 24;   // 머즐 화염 + 임팩트(좀비/벽)가 한 풀 공유 → 산탄+연사 동시에도 살아있는 플래시 퇴거 안 되게 여유

    // 머즐 라이트(주위 밝기) — gunFlashLight 미배치 시 코드로 생성·구동.
    Light _muzzleLight;
    float _muzzleLightTimer;
    bool _muzzleLightActive;

    // 발사/재장전 사운드 — 코드 생성 2D AudioSource + Resources 클립(GunSfx).
    // 발사는 오프셋 재생(clip.time)을 위해 Play()를 쓰므로 per-call 볼륨이 안 됨 → 재장전과 소스를 분리(볼륨 독립 + 발사가 재장전음을 안 끊음).
    AudioSource _gunAudio;
    AudioSource _reloadAudio;
    GunSfx.GunClass _gunClass = GunSfx.GunClass.Pistol;   // 미선택 폴백은 권총류

    // 피 튀김 프리팹(Resources 코드 로드 — 무와이어링, 모든 씬 동작). 좀비 명중에 검은 피.
    GameObject _bloodPrefab;

    // 탄 스파크 — 월드 공간 PS 1개를 명중 지점으로 옮겨 Emit(Instantiate 없이 성능). 스트레치 줄기가 사방으로.
    ParticleSystem _sparkPS;
    Material _sparkMat;

    void Awake()
    {
        _cam = Camera.main;

        // 3계층 판정/히트스탑 수치 — SO 미와이어링이어도 기본값으로 동작(랩에선 에셋을 꽂아 튜닝).
        if (feel == null) feel = ScriptableObject.CreateInstance<CombatFeelConfig>();
        ZombieController.SetKillFeedbackWindow(feel.killFeedbackWindow);

        // 메타 업그레이드 배율 캐시 + 변경 구독(사무실 구매 시 갱신). Instance가 없으면 1배 폴백.
        RefreshMetaMultipliers();
        if (Meta.MetaProgress.Instance != null)
            Meta.MetaProgress.Instance.Upgrades.OnChanged += RefreshMetaMultipliers;

        // 미선택 기본값: 인스펙터 원거리값 + 단발, 산포는 hipfireSpread.
        // 우클릭은 기본 권총류 → 패닝(FanFire)을 줘 미선택으로 바로 플레이해도 alt-fire가 작동한다.
        _pelletCount = 1;
        _spread = hipfireSpread;
        _altFire = WeaponLoadout.AltFire.FanFire;
        _magazine = magazineSize; _reloadTime = reloadTime; _ammo = _magazine; _reloading = false; _reloadTimer = 0f;

        // 시작 무기 선택(WeaponSelect 화면)이 있으면 그 스탯으로 덮어쓴다.
        if (WeaponLoadout.HasSelection)
        {
            var w = WeaponLoadout.Selected;
            if (w.kind == WeaponLoadout.Kind.Melee)
            {
                _kind = WeaponLoadout.Kind.Melee;
                _magazine = 0;   // 근접: 무탄약(UsesAmmo=false)
                _melee = new MeleeAttacker(transform, w, muzzleHeight, zombieMask, obstacleMask);
            }
            else ApplyRanged(w);
        }

        // 차지 브라켓 2줄 — 평소 숨김, 라이플 차징 중에만 좌우에서 수렴.
        _chargeL = CreateLine("ChargeBracketL", chargeWidthMin); _chargeL.enabled = false;
        _chargeR = CreateLine("ChargeBracketR", chargeWidthMin); _chargeR.enabled = false;

        // 조준 레이저 + 착탄 도트 — 총 들면 상시(매 프레임 _aimDir 따라 갱신). 도트는 둥글게 보이도록 캡 정점↑.
        _laserLine = CreateLine("LaserSight", laserWidth); _laserLine.enabled = false;
        _laserDot = CreateLine("LaserDot", laserDotSize); _laserDot.numCapVertices = 8; _laserDot.enabled = false;

        // 레이저 페이드: 시작~laserFadeStart 까지 또렷, 그 뒤 끝까지 알파 0 → 레이저처럼 트레일 오프.
        _laserGradient = new Gradient();
        _laserGradient.SetKeys(
            new[] { new GradientColorKey(laserColor, 0f), new GradientColorKey(laserColor, 1f) },
            new[] { new GradientAlphaKey(laserColor.a, 0f), new GradientAlphaKey(laserColor.a, laserFadeStart), new GradientAlphaKey(0f, 1f) });
        _laserLine.colorGradient = _laserGradient;

        // 발사체 풀은 원거리 전용. 펠릿 수가 아니라 동시 비행분을 넉넉히(연사·산탄 동시 비행) 확보한다.
        if (_kind == WeaponLoadout.Kind.Ranged)
        {
            _tracerMat = CreateAdditiveMaterial(tracerColor);
            _tracers = new TrailRenderer[BulletPoolSize];
            _bullets = new Bullet[BulletPoolSize];
            _bulletHits = new HashSet<ZombieController>[BulletPoolSize];
            for (int i = 0; i < BulletPoolSize; i++)
            {
                _tracers[i] = CreateTracer("Tracer" + i);
                _bulletHits[i] = new HashSet<ZombieController>();
            }

            // 명중 플래시 풀(코드 생성 빌보드 글로우). 좀비/벽 색은 재생 시 MPB로 주입.
            _quadMesh = BuildQuadMesh();
            _impactTex = BuildRadialTexture(64);
            _impactMat = CreateAdditiveMaterial(Color.white);
            if (_impactMat.HasProperty("_BaseMap")) _impactMat.SetTexture("_BaseMap", _impactTex);
            if (_impactMat.HasProperty("_MainTex")) _impactMat.SetTexture("_MainTex", _impactTex);
            if (_impactMat.HasProperty("_Cull")) _impactMat.SetInt("_Cull", 0);   // 빌보드라 양면 — 백페이스 컬링 리스크 제거
            _flashTr = new Transform[FlashPoolSize];
            _flashMR = new MeshRenderer[FlashPoolSize];
            _flashTimer = new float[FlashPoolSize];
            _flashLife = new float[FlashPoolSize];
            _flashColor = new Color[FlashPoolSize];
            _flashSize = new float[FlashPoolSize];
            _flashMPB = new MaterialPropertyBlock();
            for (int i = 0; i < FlashPoolSize; i++)
            {
                var go = new GameObject("ImpactFlash" + i);
                var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = _quadMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _impactMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.SetActive(false);
                _flashTr[i] = go.transform;
                _flashMR[i] = mr;
            }

            // 머즐 라이트(주위 밝기) — 씬에 gunFlashLight를 안 꽂았으면 코드로 생성해 항상 작동.
            if (gunFlashLight == null)
            {
                var lgo = new GameObject("MuzzleLight");
                lgo.transform.SetParent(transform, false);
                _muzzleLight = lgo.AddComponent<Light>();
                _muzzleLight.type = LightType.Point;
                _muzzleLight.color = muzzleLightColor;
                _muzzleLight.range = muzzleLightRange;
                _muzzleLight.intensity = 0f;
                _muzzleLight.shadows = LightShadows.None;   // 머즐플래시는 그림자 불필요 — 성능
                _muzzleLight.enabled = false;
            }

            // 발사/재장전 사운드용 2D 오디오 소스(코드 생성 — 무와이어링). 발사·재장전 분리.
            _gunAudio = gameObject.AddComponent<AudioSource>();
            _gunAudio.playOnAwake = false;
            _gunAudio.spatialBlend = 0f;
            _reloadAudio = gameObject.AddComponent<AudioSource>();
            _reloadAudio.playOnAwake = false;
            _reloadAudio.spatialBlend = 0f;

            // 피 튀김 프리팹 코드 로드(좀비 명중). 실패 시 코드 플래시 폴백.
            _bloodPrefab = Resources.Load<GameObject>("FX/blood_hit");
            if (_bloodPrefab == null)
                Debug.LogWarning("[PlayerCombat] Resources/FX/blood_hit 로드 실패 — 좀비 명중 코드 플래시로 폴백.");

            _sparkPS = CreateSparkPS();
        }
    }

    /// <summary>원거리 무기 스탯을 적용(시작 선택·데모 핫스왑 공용).</summary>
    void ApplyRanged(WeaponLoadout.Weapon w)
    {
        _kind = WeaponLoadout.Kind.Ranged;
        damage = w.damage;
        fireCooldown = w.fireCooldown;
        range = w.range;
        gunshotNoise = w.gunshotNoise;
        _pelletCount = Mathf.Max(1, w.pelletCount);
        _spread = w.spread;
        _altFire = w.altFire;
        _cooldownTimer = 0f;   // 스왑 즉시 발사 가능

        // 탄약: 무기값(없으면 폴백). 스왑 시 가득 찬 새 탄창으로 리셋.
        _magazine = w.magazine > 0 ? w.magazine : magazineSize;
        _reloadTime = w.reloadTime > 0f ? w.reloadTime : reloadTime;
        _ammo = _magazine; _reloading = false; _reloadTimer = 0f;

        // 스왑 시 진행 중이던 우클릭 상태 초기화(난사 잔류·차징 게이지 누수 방지).
        _altCooldownTimer = 0f;
        _fanShotsLeft = 0; _fanTimer = 0f;
        _charging = false; _chargeTime = 0f;
        HideChargeBrackets();

        _gunClass = ClassifyGun(w);   // 발사음 분류(권총/라이플/샷건)
    }

    /// <summary>무기 특성으로 발사음 분류: 산탄=샷건, 빠른 연사(쿨≤0.2)=라이플, 그 외=권총류.</summary>
    static GunSfx.GunClass ClassifyGun(WeaponLoadout.Weapon w)
    {
        if (w.pelletCount > 1) return GunSfx.GunClass.Shotgun;
        if (w.fireCooldown <= 0.2f) return GunSfx.GunClass.Rifle;
        return GunSfx.GunClass.Pistol;
    }

    /// <summary>메타 업그레이드 배율을 캐시(Awake 1회 + OnChanged마다). 매 발사 조회를 피한다.</summary>
    void RefreshMetaMultipliers()
    {
        var meta = Meta.MetaProgress.Instance;
        _metaDamageMult = meta != null ? meta.Upgrades.GetDamageMultiplier() : 1f;
        _metaFireRateMult = meta != null ? meta.Upgrades.GetFireRateMultiplier() : 1f;
    }

    void OnDestroy()
    {
        if (Meta.MetaProgress.Instance != null)
            Meta.MetaProgress.Instance.Upgrades.OnChanged -= RefreshMetaMultipliers;
        if (_chargeL != null) Destroy(_chargeL.material);
        if (_chargeR != null) Destroy(_chargeR.material);
        if (_laserLine != null) Destroy(_laserLine.material);
        if (_laserDot != null) Destroy(_laserDot.material);
        if (_tracers != null)
            foreach (var t in _tracers) if (t != null) Destroy(t.gameObject);
        if (_tracerMat != null) Destroy(_tracerMat);
        if (_flashTr != null)
            foreach (var f in _flashTr) if (f != null) Destroy(f.gameObject);
        if (_impactMat != null) Destroy(_impactMat);
        if (_impactTex != null) Destroy(_impactTex);
        if (_quadMesh != null) Destroy(_quadMesh);
        if (_muzzleLight != null) Destroy(_muzzleLight.gameObject);
        if (_sparkPS != null) Destroy(_sparkPS.gameObject);
        if (_sparkMat != null) Destroy(_sparkMat);
        _melee?.Cleanup();
    }

    LineRenderer CreateLine(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        lr.material = new Material(sh);
        return lr;
    }

    /// <summary>
    /// 날아가는 탄 1발용 트레일 오브젝트. 트랜스폼을 이동시키면 TrailRenderer가 꼬리를 자동으로 그린다.
    /// 플레이어 자식이 아닌 월드 공간(부모 null) — 발사 후 플레이어가 움직여도 탄 궤적이 끌려가지 않는다.
    /// </summary>
    TrailRenderer CreateTracer(string name)
    {
        var go = new GameObject(name);
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = trailTime;
        tr.widthMultiplier = trailWidth;
        tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);   // 머리 두껍게 → 꼬리 0으로 테이퍼
        tr.numCapVertices = 2;
        tr.minVertexDistance = 0.02f;
        tr.autodestruct = false;
        tr.emitting = false;
        tr.alignment = LineAlignment.View;   // 탑다운 카메라를 향한 빌보드
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.sharedMaterial = _tracerMat;
        tr.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },   // 머리 불투명 → 꼬리 투명
        };
        return tr;
    }

    /// <summary>
    /// 탄 스파크 PS 생성(월드 공간, emission off). 명중 시 transform을 명중점으로 옮겨 Emit하면
    /// 스트레치 줄기들이 사방으로 튀어 "탄에 맞았다"는 임팩트를 준다. 가산 HDR이라 블룸으로 번쩍.
    /// </summary>
    ParticleSystem CreateSparkPS()
    {
        var go = new GameObject("BulletSparkPS");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);   // 엄청 얇게
        main.startColor = sparkColor;
        main.gravityModifier = 0.6f;
        main.maxParticles = 256;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // 명중점에 남고 플레이어를 안 따라옴
        var em = ps.emission; em.enabled = false;                     // 코드 버스트(Emit)로만 방출
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.18f; sh.radiusThickness = 0f;   // 셸에서만 → 중앙에 틈(선들이 서로 떨어짐)

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;            // 속도 방향으로 늘려 줄기(streak)로
        psr.velocityScale = 0.1f;
        psr.lengthScale = 2f;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;
        _sparkMat = CreateAdditiveMaterial(Color.white);             // 색은 파티클 startColor가 결정(머티 white와 곱)
        psr.sharedMaterial = _sparkMat;
        return ps;
    }

    /// <summary>가산(additive) HDR 머티리얼 — 채널>1 색이 블룸으로 빛난다. URP 파티클 언릿 기반, Src=SrcAlpha·Dst=One.</summary>
    Material CreateAdditiveMaterial(Color hdr)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hdr);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", hdr);
        // 가산 블렌드(실제 GPU 블렌드 상태를 직접 지정 — 셰이더 프로퍼티명 차이에 안전).
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);       // additive
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    void Update()
    {
        UpdateAim();

        // 제작 채널링·좀비에게 잡힘(grapple) 중에는 무방비 — 사격/스윙 모두 잠금(움직임도 잠겨 있음).
        bool crafting = CraftingSystem.Instance != null && CraftingSystem.Instance.IsCrafting;
        bool grappled = PlayerController.Instance != null && PlayerController.Instance.IsGrappled;
        bool locked = crafting || grappled;
        bool attackHeld = Input.GetMouseButton(0);

        if (_kind == WeaponLoadout.Kind.Melee)
        {
            // 근접: 입력/조준/잠금만 넘기고 쿨·판정·연출은 MeleeAttacker가 전담.
            _melee.Tick(attackHeld, _aimDir, locked);

            // 데모: 디버그키로 방망이 → 쇠지렛대 라이브 진화.
            if (Input.GetKeyDown(evolveKey) && _melee != null)
                _melee.Evolve(WeaponLoadout.EvolvedCrowbar);
        }
        else
        {
            // 데모: 숫자키로 원거리 무기 라이브 스왑(1=리볼버 2=라이플 3=샷건).
            if (allowWeaponHotswap)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyRanged(WeaponLoadout.Revolver);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyRanged(WeaponLoadout.Rifle);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyRanged(WeaponLoadout.Shotgun);
            }

            // 수동 재장전(R) — 재장전 중이 아니고 탄약 무기이며 탄창이 가득 차지 않았을 때만.
            if (!_reloading && _magazine > 0 && _ammo < _magazine && Input.GetKeyDown(KeyCode.R)) StartReload();

            if (_altCooldownTimer > 0f) _altCooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
            // 재장전 진행(주발사 쿨과 별도 — alt-fire는 재장전 중에도 사용 가능).
            if (_reloading) { _reloadTimer -= Time.deltaTime; if (_reloadTimer <= 0f) { _reloading = false; _ammo = _magazine; } }

            // 우클릭 보조 발사(무기별). 진행 중이던 패닝 난사는 입력과 무관하게 계속 쏜다.
            HandleAltFire(locked);
            if (_fanShotsLeft > 0) TickFan();

            // 좌클릭 주발사 — 패닝/차징 중에는 잠금(같은 무기 좌·우 동시 발사 방지).
            bool altBusy = _fanShotsLeft > 0 || _charging;
            if (!locked && !altBusy && attackHeld && _cooldownTimer <= 0f && !_reloading && _ammo > 0)
                Fire();
            // 빈 탄창인데 계속 쏘려 하면 자동 재장전.
            else if (!locked && !altBusy && attackHeld && !_reloading && _ammo <= 0 && _magazine > 0)
                StartReload();

            UpdateBullets();
            UpdateImpactFlashes();   // 명중 플래시 팝→페이드 + 카메라 빌보드
            UpdateMuzzleLight();     // 머즐 라이트 펀치 폴오프
            UpdateLaser(locked);     // 조준 레이저는 차징 중에도 유지 — 브라켓이 그 좌우로 수렴한다.
        }
    }

    /// <summary>우클릭 보조 발사 입력 처리(무기별 분기). locked = 제작 중 또는 grapple로 잡힘.</summary>
    void HandleAltFire(bool locked)
    {
        switch (_altFire)
        {
            case WeaponLoadout.AltFire.FanFire:
                // 즉발 트리거: 쿨·장전·난사 모두 비어 있을 때만 난사 시작(첫 발은 TickFan이 같은 프레임에).
                if (!locked && Input.GetMouseButtonDown(1)
                    && _altCooldownTimer <= 0f && _cooldownTimer <= 0f && _fanShotsLeft <= 0)
                {
                    _fanShotsLeft = Mathf.Max(1, fanShots);
                    _fanTimer = 0f;
                }
                break;

            case WeaponLoadout.AltFire.ChargePierce:
                // 잠금 진입 시 차징 강제 취소 — 안 그러면 _charging이 잔류해 좌클릭(altBusy)이 영구 잠긴다.
                if (locked)
                {
                    if (_charging) { _charging = false; _chargeTime = 0f; }
                    HideChargeBrackets();
                    break;
                }

                // 홀드 차징 → 떼면 발사. 쿨 중이면 차징 안 함.
                bool altReady = _altCooldownTimer <= 0f;
                if (altReady && Input.GetMouseButton(1))
                {
                    _charging = true;
                    _chargeTime += Time.deltaTime;
                    ShowChargeBrackets(Mathf.Clamp01(_chargeTime / Mathf.Max(0.01f, chargeTimeMax)));
                }
                if (Input.GetMouseButtonUp(1))
                {
                    if (_charging) ReleaseCharge();
                    else { _charging = false; _chargeTime = 0f; }
                }
                if (!_charging) HideChargeBrackets();
                break;

            case WeaponLoadout.AltFire.StockBash:
                if (!locked && Input.GetMouseButtonDown(1) && _altCooldownTimer <= 0f)
                    StockBash();
                break;
        }
    }

    /// <summary>패닝: 예약된 난사를 간격마다 한 발씩. 끝나면 장전 공백을 건다.</summary>
    void TickFan()
    {
        _fanTimer -= Time.deltaTime;
        if (_fanTimer > 0f) return;

        FireShot(fanSpread, damage, range, 1, false, gunshotNoise);
        _fanShotsLeft--;
        if (_fanShotsLeft > 0) _fanTimer = fanInterval;
        else { _cooldownTimer = fanReload; _altCooldownTimer = fanReload; }   // 난사 후 장전 — 좌·우 모두 잠금
    }

    /// <summary>차지샷 발사: 차지량에 비례한 데미지로 경로상 모든 좀비를 관통.</summary>
    void ReleaseCharge()
    {
        float charge01 = Mathf.Clamp01(_chargeTime / Mathf.Max(0.01f, chargeTimeMax));
        bool valid = _chargeTime >= chargeMinTime;
        _charging = false;
        _chargeTime = 0f;
        HideChargeBrackets();
        if (!valid) return;   // 최소 차지 미달 → 오발 없이 취소(쿨다운도 안 걸림)

        int dmg = Mathf.RoundToInt(damage * Mathf.Lerp(1f, pierceDamageMult, charge01));
        FireShot(0f, dmg, range * pierceRangeMult, 1, true, gunshotNoise);
        _altCooldownTimer = pierceCooldown;
    }

    /// <summary>개머리판 밀치기: 전방 부채꼴 좀비에 넉백+경직(데미지는 양념). 탄약 0, 즉발.</summary>
    void StockBash()
    {
        _altCooldownTimer = bashCooldown;

        Vector3 origin = transform.position;
        Vector3 eye = origin + Vector3.up * muzzleHeight;
        int dmg = bashDamage + PlayerStats.DamageBonus;

        _altHits.Clear();
        Collider[] cols = Physics.OverlapSphere(origin, bashRange, zombieMask, QueryTriggerInteraction.Collide);
        foreach (var c in cols)
        {
            var z = c.GetComponentInParent<ZombieController>();
            if (z == null || _altHits.Contains(z)) continue;

            Vector3 to = z.transform.position - origin; to.y = 0f;
            float dist = to.magnitude;
            if (dist > bashRange) continue;
            if (dist > 0.9f && Vector3.Angle(_aimDir, to) > bashArc) continue;   // 전방 부채꼴(초근접은 각도 무시)

            // 벽 너머 좀비 제외(총·근접과 동일한 시야 체크).
            Vector3 d = (z.transform.position + Vector3.up * muzzleHeight) - eye;
            float dl = d.magnitude;
            if (dl > 0.001f && Physics.Raycast(eye, d / dl, dl, obstacleMask, QueryTriggerInteraction.Ignore)) continue;

            _altHits.Add(z);
            z.TakeMeleeHit(dmg, origin, bashKnockback, bashStagger, WeaponLoadout.DeathStyle.None, 0.03f);
        }

        NoiseManager.Instance?.EmitImpulse(bashNoise);
        TriggerMuzzle(eye + _aimDir * muzzleForward);   // 밀치기 순간 라이트 펀치(총성 아님 — VFX·사운드 없음)
        GetComponent<MoreMountains.Feedbacks.MMSpringScale>()?.Bump(new Vector3(-0.12f, 0.08f, -0.12f));
    }

    /// <summary>
    /// 차지 브라켓: 레이저 좌우로 벌어진 두 선이 차지가 오를수록 가운데(레이저)로 수렴하고,
    /// 색이 흰 반투명→빨강으로 달아오르며 두꺼워진다. 풀차지 시 한 줄기로 합쳐진 굵은 빔.
    /// </summary>
    void ShowChargeBrackets(float charge01)
    {
        if (_chargeL == null || _chargeR == null) return;
        Vector3 origin = transform.position + Vector3.up * muzzleHeight;
        Vector3 side = Vector3.Cross(Vector3.up, _aimDir).normalized;   // 조준선 수평 직교(좌우)

        float len = Mathf.Lerp(chargeLength, range * pierceRangeMult, charge01);
        float off = Mathf.Lerp(chargeSideMax, 0.02f, charge01);   // 수렴(완전히 0이면 두 선이 겹쳐 깜빡 → 살짝 남김)
        float w = Mathf.Lerp(chargeWidthMin, chargeWidthMax, charge01);
        Color c = Color.Lerp(chargeColorLow, chargeColorHigh, charge01);

        SetBracket(_chargeL, origin + side * off, len, w, c);
        SetBracket(_chargeR, origin - side * off, len, w, c);
    }

    void SetBracket(LineRenderer lr, Vector3 from, float len, float w, Color c)
    {
        lr.enabled = true;
        lr.widthMultiplier = w;
        lr.SetPosition(0, from);
        lr.SetPosition(1, from + _aimDir * len);
        lr.startColor = c;
        lr.endColor = c;
    }

    void HideChargeBrackets()
    {
        if (_chargeL != null) _chargeL.enabled = false;
        if (_chargeR != null) _chargeR.enabled = false;
    }

    /// <summary>
    /// 조준 레이저: 총구에서 _aimDir(실제 탄도 방향 — 마우스보다 한 박자 늦음)을 따라
    /// 첫 벽/좀비까지 얇고 어두운 선 + 착탄 도트. 디제틱한 조준 보조. 제작 중엔 숨김.
    /// </summary>
    void UpdateLaser(bool locked)
    {
        if (_laserLine == null || _laserDot == null) return;
        if (locked)
        {
            _laserLine.enabled = false;
            _laserDot.enabled = false;
            return;
        }

        Vector3 origin = transform.position + Vector3.up * muzzleHeight;
        int mask = zombieMask | obstacleMask;
        Vector3 endPoint = Physics.Raycast(origin, _aimDir, out RaycastHit hit, range, mask, QueryTriggerInteraction.Collide)
            ? hit.point
            : origin + _aimDir * range;

        // 선은 또렷한 최대 길이까지만 그린다(착탄점이 더 멀어도). 끝부분은 그라디언트가 0으로 페이드.
        float beamLen = Mathf.Min(Vector3.Distance(origin, endPoint), laserLength);
        Vector3 lineEnd = origin + _aimDir * beamLen;
        _laserLine.enabled = true;
        _laserLine.widthMultiplier = laserWidth;
        _laserLine.SetPosition(0, origin);
        _laserLine.SetPosition(1, lineEnd);

        // 도트: 착탄점을 중심으로 _aimDir 방향 아주 짧은 선 + 둥근 캡 → 작은 점처럼 보인다.
        Vector3 half = _aimDir * (laserDotSize * 0.5f);
        _laserDot.enabled = true;
        _laserDot.SetPosition(0, endPoint - half);
        _laserDot.SetPosition(1, endPoint + half);
        _laserDot.startColor = laserDotColor;
        _laserDot.endColor = laserDotColor;
    }

    void UpdateAim()
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        // 마우스 → 플레이어 높이의 수평면 교차점. 카메라 각도와 무관하게 동작.
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (ground.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            Vector3 dir = hit - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Vector3 targetDir = dir.normalized;
                // 커서에 즉시 스냅하지 않고 지수 감쇠로 추종 → 마우스를 홱 돌려도 사격 방향이
                // 한 박자 늦게 따라붙는다(조준선·탄도·근접 스윙 모두 _aimDir 공유 → 일관). 프레임율 독립.
                _aimDir = _aimInitialized
                    ? Vector3.Slerp(_aimDir, targetDir, 1f - Mathf.Exp(-aimResponsiveness * Time.deltaTime)).normalized
                    : targetDir;
                _aimInitialized = true;
            }
        }
    }

    /// <summary>좌클릭 주발사 — 무기 스탯대로 한 번 발사하고 쿨다운을 건다.</summary>
    void Fire()
    {
        // 속사 카드 + 메타 연사 업그레이드: 두 배수만큼 쿨다운 단축.
        _cooldownTimer = fireCooldown / Mathf.Max(0.01f, PlayerStats.FireRateMult * _metaFireRateMult);
        FireShot(_spread, damage, range, _pelletCount, false, gunshotNoise);

        // 탄약 소모 — 발사 후 차감. 빈 탄창이면 자동 재장전 시작.
        _ammo--;
        if (_ammo <= 0) StartReload();
    }

    /// <summary>재장전 시작(탄약 무기 전용). alt-fire와 무관한 자체 타이머를 건다.</summary>
    void StartReload()
    {
        if (_magazine <= 0) return;
        // reloadTime이 0(데이터 실수 등)이면 재장전 상태로 들어가지 않고 즉시 풀충전 — 무한탄약 footgun 차단.
        if (_reloadTime <= 0f) { _ammo = _magazine; _reloading = false; _reloadTimer = 0f; return; }
        _reloading = true; _reloadTimer = _reloadTime;

        if (_reloadAudio != null)
        {
            var clip = GunSfx.Reload(_gunClass);
            if (clip != null) _reloadAudio.PlayOneShot(clip, reloadVolume);
        }
    }

    /// <summary>
    /// 발사체 발사(주발사·패닝·차지샷 공용). spread로 펠릿마다 방향을 정해 날아가는 탄을 만든다.
    /// ★판정은 즉발이 아니라 비행 중(UpdateBullets segment 캐스트) — 탄이 실제로 닿아야 명중.
    /// 데미지·사거리·관통 여부는 발사 순간 스냅해 발사체에 싣는다(강선 카드 보너스도 여기서 가산).
    /// </summary>
    void FireShot(float spread, int baseDmg, float rng, int pellets, bool pierce, float noise)
    {
        Vector3 origin = transform.position + Vector3.up * muzzleHeight;
        pellets = Mathf.Max(1, pellets);
        // 카드 가산 후 메타 데미지 업그레이드 배수 적용(반올림). 배수≥1이라 최소 카드값은 보장.
        int dmg = Mathf.RoundToInt((baseDmg + PlayerStats.DamageBonus) * _metaDamageMult);
        // 넉백: 샷건=강한 푸시, 그 외=아주 약한 잼킹(타격감용 흔들림, 호드는 안 흩어짐).
        float kb = _gunClass == GunSfx.GunClass.Shotgun ? bulletKnockback : weakKnockback;

        for (int p = 0; p < pellets; p++)
        {
            // 펠릿마다 좌우(yaw) 랜덤 산포. spread=0이면 정확히 조준 방향.
            Vector3 dir = spread > 0f
                ? Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * _aimDir
                : _aimDir;
            SpawnBullet(origin, dir, rng, dmg, pierce, kb);
        }

        // 소음·머즐 연출·사운드는 발사 1회당 한 번(펠릿 수와 무관).
        NoiseManager.Instance?.EmitImpulse(noise);

        // 총구 끝(조준 방향으로 앞당김) — 화염 VFX·라이트가 총신 끝에 오도록.
        Vector3 muzzleTip = origin + _aimDir * muzzleForward;
        PlayFlash(muzzleTip, muzzleFlashColor, muzzleFlashSize, muzzleFlashTime);   // 총구 화염(임팩트 풀 재사용, 더 짧게)
        TriggerMuzzle(muzzleTip);                                                   // 주위 밝기(라이트)
        PlayShotSound();
        PlayerCameraRig.Instance?.TriggerShake(_gunClass == GunSfx.GunClass.Shotgun ? fireShake * 2f : fireShake);   // 발사 화면 펀치(샷건 강하게)
    }

    /// <summary>머즐 라이트 점멸: 씬에 꽂힌 gunFlashLight가 있으면 그걸, 없으면 코드 라이트를 muzzleTip에서 번쩍.</summary>
    void TriggerMuzzle(Vector3 muzzleTip)
    {
        if (gunFlashLight != null) { gunFlashLight.Trigger(); return; }
        if (_muzzleLight == null) return;
        _muzzleLight.transform.position = muzzleTip;
        _muzzleLight.intensity = muzzleLightIntensity;
        _muzzleLight.enabled = true;
        _muzzleLightActive = true;
        _muzzleLightTimer = muzzleLightDuration;
    }

    /// <summary>발사음 1회 재생(무기 분류별 변형 랜덤 + 살짝 피치 흔들기로 반복 피로 완화).</summary>
    void PlayShotSound()
    {
        if (_gunAudio == null) return;
        var clip = GunSfx.Shot(_gunClass);
        if (clip == null) return;
        // 오프셋 재생: 완만한 어택 앞부분을 건너뛰어 트리거 즉시 타격. Play()는 재생 중이면 자동 재시작 → 연사 누적도 차단.
        _gunAudio.volume = shotVolume;
        _gunAudio.clip = clip;
        _gunAudio.time = Mathf.Clamp(GunSfx.ShotSkip(_gunClass), 0f, Mathf.Max(0f, clip.length - 0.02f));
        _gunAudio.Play();
    }

    /// <summary>빈 발사체 슬롯 인덱스. 다 차 있으면 라운드로빈으로 가장 오래된 슬롯을 강제 재사용(데미지 유실 방지).</summary>
    int AcquireBulletSlot()
    {
        for (int i = 0; i < _bullets.Length; i++)
            if (!_bullets[i].active) return i;
        int e = _bulletEvict;
        _bulletEvict = (_bulletEvict + 1) % _bullets.Length;
        return e;
    }

    /// <summary>날아가는 탄 1발을 풀에 생성. 판정은 비행 중(UpdateBullets)에서 처리한다.</summary>
    void SpawnBullet(Vector3 origin, Vector3 dir, float range, int damage, bool pierce, float knockback)
    {
        if (_bullets == null) return;
        int i = AcquireBulletSlot();
        _bullets[i] = new Bullet { active = true, pos = origin, dir = dir, remaining = Mathf.Max(0.15f, range), damage = damage, pierce = pierce, knockback = knockback };
        _bulletHits[i].Clear();   // 3계층 판정은 그레이즈(비정지)가 있어 비관통 탄도 중복타 방지 셋이 필요

        var tr = i < _tracers.Length ? _tracers[i] : null;
        if (tr != null)
        {
            tr.emitting = false;          // 먼저 끔 — 텔레포트 전 잔여 꼬리 방출 차단(일부 URP 버전 안전)
            tr.transform.position = origin;
            tr.Clear();                   // 직전 비행/주차 위치에서 늘어진 꼬리 제거(텔레포트 streak 방지)
            tr.emitting = true;
        }
    }

    /// <summary>
    /// 발사체 갱신: 매 프레임 bulletSpeed만큼 전진시키며 그 구간(이전→현재)을 캐스트해 명중을 판정한다.
    /// segment 캐스트라 빠른 탄도 좀비를 건너뛰지(터널링) 않는다. 벽/사거리/비관통 명중 시 비행 종료.
    /// 트레일 오브젝트를 현재 위치로 옮기면 TrailRenderer가 꼬리를 자동으로 그린다.
    /// </summary>
    void UpdateBullets()
    {
        if (_bullets == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < _bullets.Length; i++)
        {
            if (!_bullets[i].active) continue;

            Vector3 from = _bullets[i].pos;
            Vector3 dir = _bullets[i].dir;
            float step = bulletSpeed * dt;
            bool finish = false;
            bool hitZombie = false;   // 비관통이 좀비에 멈췄는지 — 종료 시 벽 임팩트와 구분

            // 남은 사거리에서 멈춤.
            if (step >= _bullets[i].remaining) { step = _bullets[i].remaining; finish = true; }

            // 벽: 구간 내 첫 장애물에서 종료(스피어 반경만큼 앞에서 멈춰 벽 뚫림 방지).
            float travel = step;
            float wallDist = float.PositiveInfinity;
            Vector3 wallPoint = Vector3.zero;   // 벽 스파크는 벽면(point)에 — to는 hitRadius만큼 앞이라 부적합
            if (Physics.Raycast(from, dir, out RaycastHit wallHit, step, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                wallDist = wallHit.distance;
                wallPoint = wallHit.point;
                travel = Mathf.Max(0f, wallHit.distance - hitRadius);
                finish = true;
            }

            // 좀비: 3계층 탄 판정(combat-texture-foundation §6.2) — "맞췄나"(디지털)가 아니라 "얼마나 잘 맞췄나"(아날로그).
            // 탄도선↔좀비 축 최단 수평거리 d로 분류: 풀히트(정지) / 스침(50%+flinch, 정지) / 그레이즈(소량+비틀 확률, ★탄 계속).
            // 그레이즈가 탄을 안 멈추므로 비관통도 CastAll로 경로상 전원을 평가한다. 시작점을 반경만큼 뒤로 빼 초근접 누락 방지,
            // 벽 밀착 사격 시 cast가 벽면 너머로 새지 않도록 길이를 벽 거리로 캡.
            float judgeR = feel.nearMissRadius;
            Vector3 castFrom = from - dir * judgeR;
            float castLen = Mathf.Min(travel + judgeR, wallDist);
            var hits = Physics.SphereCastAll(castFrom, judgeR, dir, castLen, zombieMask, QueryTriggerInteraction.Collide);
            if (hits.Length > 1) System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var z = h.collider.GetComponentInParent<ZombieController>();
                if (z == null || z.IsDead || _bulletHits[i].Contains(z)) continue;

                // 탄도 "세그먼트"와 좀비 축(수직 캡슐)의 최단 수평거리 — 무한 직선이 아니라 이번 스텝 구간에
                // 투영을 클램프(리뷰 반영: 구간 끝 너머 좀비가 직선 거리로 과대 판정되는 것 방지).
                Vector3 rel = z.transform.position - from; rel.y = 0f;
                Vector3 flat = dir; flat.y = 0f;
                float flatLen = flat.magnitude;
                float perp;
                if (flatLen > 0.0001f)
                {
                    Vector3 axis = flat / flatLen;
                    float along = Mathf.Clamp(Vector3.Dot(rel, axis), 0f, travel);
                    perp = (rel - axis * along).magnitude;
                }
                else perp = rel.magnitude;
                if (perp > judgeR) continue;   // 넓은 캐스트가 주워온 콜라이더 가장자리 — 판정 밖

                _bulletHits[i].Add(z);
                Vector3 hitPoint = h.distance > 0f ? h.point : from;

                if (perp <= feel.fullHitRadius)
                {
                    // 풀히트: 풀데미지 + 넉백 + 피격 사다리 + 히트스탑. 비관통은 여기서 정지.
                    z.TakeBulletHit(_bullets[i].damage, dir, _bullets[i].knockback, feel.hitStopNormal, BulletHitTier.Full, false);
                    PlayImpact(hitPoint, dir, true);
                }
                else if (perp <= feel.grazeRadius)
                {
                    // 스침: 데미지 절반 + flinch만(넉백·사다리 없음). 맞긴 맞았으니 탄은 정지.
                    int gdmg = Mathf.Max(1, Mathf.RoundToInt(_bullets[i].damage * feel.grazeDamageMult));
                    z.TakeBulletHit(gdmg, dir, 0f, feel.hitStopNormal, BulletHitTier.Graze, false);
                    PlayImpact(hitPoint, dir, true);
                }
                else
                {
                    // 그레이즈: 미스에 가깝지만 소량 데미지 + 비틀 확률. 탄은 계속 — 모든 발사가 세계에 흔적을 남긴다.
                    // 피 없이 스파크만(절반) — 풀히트와의 질감 차이를 시각으로도 가른다.
                    int ndmg = Mathf.Max(1, Mathf.RoundToInt(_bullets[i].damage * feel.nearMissDamageMult));
                    bool flinch = Random.value < feel.nearMissFlinchChance;
                    z.TakeBulletHit(ndmg, dir, 0f, 0f, BulletHitTier.NearMiss, flinch);
                    if (_sparkPS != null) { _sparkPS.transform.position = hitPoint; _sparkPS.Emit(Mathf.Max(1, sparkBurstCount / 2)); }
                    continue;   // 탄 비행 유지
                }

                // 풀히트/스침 — 관통탄은 뚫고 계속, 비관통은 명중점에서 정지.
                if (!_bullets[i].pierce)
                {
                    hitZombie = true;
                    travel = Mathf.Clamp(h.distance - judgeR, 0f, travel);   // castFrom 기준 → from 기준 환산
                    finish = true;
                    break;
                }
            }

            Vector3 to = from + dir * travel;
            _bullets[i].pos = to;
            _bullets[i].remaining -= travel;

            var tr = i < _tracers.Length ? _tracers[i] : null;
            if (tr != null) tr.transform.position = to;

            if (finish)
            {
                _bullets[i].active = false;
                if (tr != null) tr.emitting = false;
                // 좀비에 멈춘 게 아니라 벽에 멈췄으면 벽 스파크(사거리 소진=허공이면 무생성).
                if (!hitZombie && !float.IsPositiveInfinity(wallDist)) PlayImpact(wallPoint, dir, false);
            }
        }
    }

    /// <summary>
    /// 명중 이펙트 1회 재생. 탄 스파크 팝(밝은 흰 코드 빌보드)은 모든 명중에, 검은 피 분사(프리팹)는 좀비 명중에만.
    /// zombieHitOverride를 채우면 코드 로드 블러드를 대체. 블러드 미로드 시 좀비색 코드 플래시로 폴백.
    /// </summary>
    void PlayImpact(Vector3 pos, Vector3 dir, bool zombie)
    {
        // 탄 튀김 — 모든 명중에 사방으로 튀는 밝은 스파크 줄기(월드 PS를 명중점으로 옮겨 Emit).
        if (_sparkPS != null) { _sparkPS.transform.position = pos; _sparkPS.Emit(sparkBurstCount); }

        // 피 튀김 — 좀비 명중에 검은 피 분사(프리팹). 없으면 좀비색 플래시 폴백.
        if (zombie)
        {
            var blood = zombieHitOverride != null ? zombieHitOverride : _bloodPrefab;
            if (blood != null) SpawnOverride(blood, pos, dir);
            else PlayFlash(pos, zombieFlashColor, zombieFlashSize, impactFlashTime);
        }
    }

    /// <summary>오버라이드 프리팹을 명중점에 스폰하고 수명 후 자동 소멸(표면에서 튀어나오도록 -dir 정렬).</summary>
    void SpawnOverride(GameObject prefab, Vector3 pos, Vector3 dir)
    {
        Quaternion rot = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(-dir) : Quaternion.identity;
        Destroy(Instantiate(prefab, pos, rot), overrideLifetime);
    }

    /// <summary>명중점에 풀에서 플래시 1발을 재생(팝→페이드는 UpdateImpactFlashes가 처리). 색·크기는 좀비/벽으로 분기해 넘긴다.</summary>
    void PlayFlash(Vector3 pos, Color hdr, float size, float life)
    {
        if (_flashTr == null) return;
        int i = AcquireFlashSlot();
        _flashLife[i] = Mathf.Max(0.02f, life);
        _flashTimer[i] = _flashLife[i];
        _flashColor[i] = hdr;
        _flashSize[i] = size;
        _flashTr[i].position = pos;
        _flashTr[i].localScale = Vector3.zero;   // 첫 프레임 팝 전 0에서 시작(터짐 연출)
        _flashMR[i].gameObject.SetActive(true);
    }

    /// <summary>머즐 라이트를 매 프레임 t² 폴오프로 감쇠(짧고 강한 펀치). 코드 생성 라이트일 때만 동작.</summary>
    void UpdateMuzzleLight()
    {
        if (!_muzzleLightActive || _muzzleLight == null) return;
        _muzzleLightTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(_muzzleLightTimer / Mathf.Max(0.0001f, muzzleLightDuration));
        _muzzleLight.intensity = muzzleLightIntensity * t * t;   // 빠른 폴오프 = 펀치감
        if (_muzzleLightTimer <= 0f)
        {
            _muzzleLightActive = false;
            _muzzleLight.intensity = 0f;
            _muzzleLight.enabled = false;
        }
    }

    /// <summary>유휴(타이머≤0) 슬롯을 먼저, 없으면 라운드로빈으로 재사용.</summary>
    int AcquireFlashSlot()
    {
        for (int i = 0; i < _flashTimer.Length; i++)
            if (_flashTimer[i] <= 0f) return i;
        int e = _flashEvict;
        _flashEvict = (_flashEvict + 1) % _flashTimer.Length;
        return e;
    }

    /// <summary>활성 플래시를 매 프레임 팝(크기↑)·페이드(알파↓)시키고 카메라를 향해 빌보드. 끝나면 비활성.</summary>
    void UpdateImpactFlashes()
    {
        if (_flashTr == null) return;
        float dt = Time.deltaTime;
        if (_cam == null) _cam = Camera.main;   // 씬 전환 등으로 빠졌으면 재취득(빌보드 방향용)
        Vector3 camPos = _cam != null ? _cam.transform.position : transform.position - Vector3.forward;
        for (int i = 0; i < _flashTimer.Length; i++)
        {
            if (_flashTimer[i] <= 0f) continue;
            _flashTimer[i] -= dt;
            if (_flashTimer[i] <= 0f) { _flashMR[i].gameObject.SetActive(false); continue; }
            if (_flashLife[i] <= 0f) { _flashTimer[i] = 0f; _flashMR[i].gameObject.SetActive(false); continue; }   // 방어: 수명 0 슬롯 NaN scale 차단

            float life01 = 1f - _flashTimer[i] / _flashLife[i];     // 0(탄착)→1(소멸) — 슬롯별 수명 기준
            float pop = 1f - (1f - life01) * (1f - life01);         // ease-out: 빠르게 퍼지고 둔화
            float fade = 1f - life01;                               // 선형 페이드(SrcAlpha 가산 → 알파가 기여도)

            var tr = _flashTr[i];
            tr.localScale = Vector3.one * (_flashSize[i] * pop);
            Vector3 toFlash = tr.position - camPos;   // 점블랭크로 카메라와 겹치면 zero → LookRotation 에러 방지
            if (toFlash.sqrMagnitude > 1e-6f) tr.rotation = Quaternion.LookRotation(toFlash);   // 쿼드 +Z면을 카메라로

            Color c = _flashColor[i]; c.a = fade;
            _flashMPB.SetColor("_BaseColor", c);
            _flashMPB.SetColor("_Color", c);
            _flashMR[i].SetPropertyBlock(_flashMPB);
        }
    }

    /// <summary>중심 원점, XY평면, +Z 노멀의 1x1 빌보드 쿼드를 코드로 생성.</summary>
    Mesh BuildQuadMesh()
    {
        var m = new Mesh { name = "ImpactQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),   new Vector3(-0.5f, 0.5f, 0f),
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>중심이 밝고 가장자리로 갈수록 투명해지는 부드러운 라디얼 글로우 텍스처(사각 티 제거 · 블룸 친화).</summary>
    Texture2D BuildRadialTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half, dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0(중심)~1(가장자리)
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                         // 부드러운 폴오프(가운데 집중)
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
