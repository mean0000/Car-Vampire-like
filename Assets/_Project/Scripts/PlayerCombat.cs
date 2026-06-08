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
    [SerializeField] int magazineSize = 6;        // 미선택 폴백(원거리) 탄창 크기
    [SerializeField] float reloadTime = 1.1f;     // 미선택 폴백(원거리) 재장전 시간

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
    [SerializeField, Min(0.001f)] float trailWidth = 0.16f;
    [Tooltip("트레일 HDR 색 — 채널>1이면 블룸으로 빛난다(메인 씬 블룸 ON). 따뜻한 예광탄.")]
    [ColorUsage(true, true)][SerializeField] Color tracerColor = new Color(4f, 2.2f, 0.8f, 1f);

    [Header("Muzzle Flash")]
    [SerializeField] GunFlashLight gunFlashLight;

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

    [Header("Impact Override (선택 — 채우면 코드 플래시 대신 이 프리팹 사용)")]
    [Tooltip("좀비 명중 이펙트 프리팹. 비우면 위 코드 플래시 사용. ⚠️URP 호환 셰이더만(BIRP 프리팹은 핑크).")]
    [SerializeField] GameObject zombieHitOverride;
    [Tooltip("벽 명중 이펙트 프리팹. 비우면 위 코드 플래시 사용. ⚠️URP 호환 셰이더만(BIRP 프리팹은 핑크).")]
    [SerializeField] GameObject wallHitOverride;
    [Tooltip("오버라이드 프리팹 자동 소멸 시간(초).")]
    [SerializeField, Min(0.1f)] float overrideLifetime = 2f;

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
    TrailRenderer[] _tracers;   // 발사체별 날아가는 탄 트레일(이동시키면 streak 자동 생성). BulletPoolSize만큼 풀.
    Material _tracerMat;         // 트레일 공유 가산 HDR 머티리얼(블룸용 — 모든 트레일이 한 머티리얼 공유).

    // 발사체 1발의 비행 상태. ★판정은 즉발이 아니라 비행 중(UpdateBullets segment 캐스트) — 탄이 닿아야 명중.
    struct Bullet { public bool active; public Vector3 pos; public Vector3 dir; public float remaining; public int damage; public bool pierce; }
    Bullet[] _bullets;
    HashSet<ZombieController>[] _bulletHits;   // 슬롯별 관통 중복타 방지(pierce 탄만 사용).
    int _bulletEvict;                          // 풀 고갈 시 라운드로빈 강제 재사용 인덱스(데미지 유실 방지).
    const int BulletPoolSize = 48;             // 동시 비행 총알 풀(샷건 8펠릿 연발 + 라이플 연사 여유, 고갈 사실상 차단).

    // 명중 플래시 풀 — 코드 생성 빌보드 쿼드(가산 HDR + 라디얼 글로우). 좀비/벽 색만 다르게 재생.
    Transform[] _flashTr;            // 풀 오브젝트 트랜스폼(매 프레임 카메라 빌보드).
    MeshRenderer[] _flashMR;
    MaterialPropertyBlock _flashMPB; // 인스턴스별 HDR 색을 머티리얼 복제 없이 주입.
    float[] _flashTimer;             // 남은 수명(초). <=0 = 유휴.
    Color[] _flashColor;             // 이 재생의 HDR 색.
    float[] _flashSize;              // 이 재생의 최대 크기(m).
    int _flashEvict;
    Material _impactMat;             // 가산 HDR + 라디얼 텍스처(전 플래시 공유).
    Texture2D _impactTex;            // 코드 생성 라디얼 글로우(부드러운 원형 — 사각 티 제거).
    Mesh _quadMesh;                  // 빌보드 쿼드(인스턴스 소유 — static 공유 시 멀티 OnDestroy 파탄 방지).
    const int FlashPoolSize = 16;

    void Awake()
    {
        _cam = Camera.main;

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
    }

    void OnDestroy()
    {
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

        // 제작 채널링 중에는 무방비 — 사격/스윙 모두 잠금(움직임도 잠겨 있음).
        bool crafting = CraftingSystem.Instance != null && CraftingSystem.Instance.IsCrafting;
        bool attackHeld = Input.GetMouseButton(0);

        if (_kind == WeaponLoadout.Kind.Melee)
        {
            // 근접: 입력/조준/잠금만 넘기고 쿨·판정·연출은 MeleeAttacker가 전담.
            _melee.Tick(attackHeld, _aimDir, crafting);

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
            HandleAltFire(crafting);
            if (_fanShotsLeft > 0) TickFan();

            // 좌클릭 주발사 — 패닝/차징 중에는 잠금(같은 무기 좌·우 동시 발사 방지).
            bool altBusy = _fanShotsLeft > 0 || _charging;
            if (!crafting && !altBusy && attackHeld && _cooldownTimer <= 0f && !_reloading && _ammo > 0)
                Fire();
            // 빈 탄창인데 계속 쏘려 하면 자동 재장전.
            else if (!crafting && !altBusy && attackHeld && !_reloading && _ammo <= 0 && _magazine > 0)
                StartReload();

            UpdateBullets();
            UpdateImpactFlashes();   // 명중 플래시 팝→페이드 + 카메라 빌보드
            UpdateLaser(crafting);   // 조준 레이저는 차징 중에도 유지 — 브라켓이 그 좌우로 수렴한다.
        }
    }

    /// <summary>우클릭 보조 발사 입력 처리(무기별 분기).</summary>
    void HandleAltFire(bool crafting)
    {
        switch (_altFire)
        {
            case WeaponLoadout.AltFire.FanFire:
                // 즉발 트리거: 쿨·장전·난사 모두 비어 있을 때만 난사 시작(첫 발은 TickFan이 같은 프레임에).
                if (!crafting && Input.GetMouseButtonDown(1)
                    && _altCooldownTimer <= 0f && _cooldownTimer <= 0f && _fanShotsLeft <= 0)
                {
                    _fanShotsLeft = Mathf.Max(1, fanShots);
                    _fanTimer = 0f;
                }
                break;

            case WeaponLoadout.AltFire.ChargePierce:
                // 제작 진입 시 차징 강제 취소 — 안 그러면 _charging이 잔류해 좌클릭(altBusy)이 영구 잠긴다.
                if (crafting)
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
                if (!crafting && Input.GetMouseButtonDown(1) && _altCooldownTimer <= 0f)
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
            z.TakeMeleeHit(dmg, origin, bashKnockback, bashStagger, WeaponLoadout.DeathStyle.None);
        }

        NoiseManager.Instance?.EmitImpulse(bashNoise);
        gunFlashLight?.Trigger();
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
    void UpdateLaser(bool crafting)
    {
        if (_laserLine == null || _laserDot == null) return;
        if (crafting)
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

        _laserLine.enabled = true;
        _laserLine.widthMultiplier = laserWidth;
        _laserLine.SetPosition(0, origin);
        _laserLine.SetPosition(1, endPoint);
        _laserLine.startColor = laserColor;
        _laserLine.endColor = laserColor;

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
        // 속사 카드: 연사 속도 배수만큼 쿨다운 단축.
        _cooldownTimer = fireCooldown / Mathf.Max(0.01f, PlayerStats.FireRateMult);
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
        int dmg = baseDmg + PlayerStats.DamageBonus;

        for (int p = 0; p < pellets; p++)
        {
            // 펠릿마다 좌우(yaw) 랜덤 산포. spread=0이면 정확히 조준 방향.
            Vector3 dir = spread > 0f
                ? Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * _aimDir
                : _aimDir;
            SpawnBullet(origin, dir, rng, dmg, pierce);
        }

        // 소음·머즐플래시는 발사 1회당 한 번(펠릿 수와 무관).
        NoiseManager.Instance?.EmitImpulse(noise);
        gunFlashLight?.Trigger();
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
    void SpawnBullet(Vector3 origin, Vector3 dir, float range, int damage, bool pierce)
    {
        if (_bullets == null) return;
        int i = AcquireBulletSlot();
        _bullets[i] = new Bullet { active = true, pos = origin, dir = dir, remaining = Mathf.Max(0.15f, range), damage = damage, pierce = pierce };
        if (pierce) _bulletHits[i].Clear();

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

            // 좀비: 구간 스피어캐스트. 시작점을 hitRadius만큼 뒤로 빼 초근접(시작점 겹침) 누락을 막는다.
            // 벽에 hitRadius 이내로 밀착해 쏠 때 cast가 벽면 너머로 새지 않도록 길이를 벽 거리로 캡한다.
            Vector3 castFrom = from - dir * hitRadius;
            float castLen = Mathf.Min(travel + hitRadius, wallDist);
            if (_bullets[i].pierce)
            {
                var hits = Physics.SphereCastAll(castFrom, hitRadius, dir, castLen, zombieMask, QueryTriggerInteraction.Collide);
                foreach (var h in hits)
                {
                    var z = h.collider.GetComponentInParent<ZombieController>();
                    if (z == null || _bulletHits[i].Contains(z)) continue;
                    _bulletHits[i].Add(z);
                    z.TakeDamage(_bullets[i].damage);
                    PlayImpact(h.distance > 0f ? h.point : from, dir, true);   // 관통: 뚫는 좀비마다 살점 임팩트(초근접 point=0 폴백)
                }
            }
            else if (Physics.SphereCast(castFrom, hitRadius, dir, out RaycastHit zHit, castLen, zombieMask, QueryTriggerInteraction.Collide))
            {
                var z = zHit.collider.GetComponentInParent<ZombieController>();
                if (z != null) z.TakeDamage(_bullets[i].damage);
                PlayImpact(zHit.distance > 0f ? zHit.point : from, dir, true);   // 비관통: 첫 좀비에 살점 임팩트(초근접 point=0 폴백)
                hitZombie = true;
                travel = Mathf.Clamp(zHit.distance - hitRadius, 0f, travel);   // castFrom 기준 → from 기준 환산 후 명중점에서 멈춤
                finish = true;
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
    /// 명중 이펙트 1회 재생. 해당 오버라이드 프리팹이 꽂혀 있으면 그걸 스폰(교체), 없으면 코드 플래시로 폴백.
    /// zombie=true면 좀비(살점), false면 벽(스파크) 계열 색·크기·프리팹을 고른다.
    /// </summary>
    void PlayImpact(Vector3 pos, Vector3 dir, bool zombie)
    {
        var ov = zombie ? zombieHitOverride : wallHitOverride;
        if (ov != null) { SpawnOverride(ov, pos, dir); return; }
        PlayFlash(pos,
            zombie ? zombieFlashColor : wallFlashColor,
            zombie ? zombieFlashSize : wallFlashSize);
    }

    /// <summary>오버라이드 프리팹을 명중점에 스폰하고 수명 후 자동 소멸(표면에서 튀어나오도록 -dir 정렬).</summary>
    void SpawnOverride(GameObject prefab, Vector3 pos, Vector3 dir)
    {
        Quaternion rot = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(-dir) : Quaternion.identity;
        Destroy(Instantiate(prefab, pos, rot), overrideLifetime);
    }

    /// <summary>명중점에 풀에서 플래시 1발을 재생(팝→페이드는 UpdateImpactFlashes가 처리). 색·크기는 좀비/벽으로 분기해 넘긴다.</summary>
    void PlayFlash(Vector3 pos, Color hdr, float size)
    {
        if (_flashTr == null) return;
        int i = AcquireFlashSlot();
        _flashTimer[i] = impactFlashTime;
        _flashColor[i] = hdr;
        _flashSize[i] = size;
        _flashTr[i].position = pos;
        _flashTr[i].localScale = Vector3.zero;   // 첫 프레임 팝 전 0에서 시작(터짐 연출)
        _flashMR[i].gameObject.SetActive(true);
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

            float life01 = 1f - _flashTimer[i] / impactFlashTime;   // 0(탄착)→1(소멸)
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
