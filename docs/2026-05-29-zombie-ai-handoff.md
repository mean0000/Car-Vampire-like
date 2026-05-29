# 좀비 AI 구현 핸드오프

> **작성일**: 2026-05-29
> **목적**: GDD v2.7 도보 스텔스 방향에 맞는 좀비 AI 전면 교체. 집에서 바로 코딩 가능한 수준의 구현 명세.
> **선행**: `2026-05-27-new-direction-gdd.md` §9/§17, `2026-05-29-infection-noise-design.md`, `2026-05-29-greybox-setup.md`

---

## 0. 요약 — 왜 교체하는가

기존 코드는 **차량 뱀서라이크** 용:
- `ZombieController.cs` (959줄): CarController 추적, Ranged/Charger/Laser 3타입, 드리프트 킬 체인, 투사체 시스템
- `ZombieSpawner.cs` (420줄): 차량 속도 기반 스폰, Cozy 밤낮 연동
- `ZombieProjectile.cs` (115줄): 포물선 투사체 + AoE

새 방향은 **도보 스텔스 생존** — 타겟이 PlayerController, 소음/스텔스 기반 탐지, 근접 전투. 기존 코드와 겹치는 부분이 거의 없어 전면 교체.

---

## 1. 파일 변경 목록

| 파일 | 액션 | 비고 |
|---|---|---|
| `ZombieConfig.cs` | **신규** | ScriptableObject, 타입별 수치 |
| `NoiseManager.cs` | **신규** | 소음 싱글톤 |
| `ZombieController.cs` | **전체 교체** | 959줄 → ~350줄 |
| `ZombieSpawner.cs` | **전체 교체** | 420줄 → ~180줄 |
| `ZombieProjectile.cs` | **삭제** | 원거리 좀비 없음 |
| `Editor/ZombieSpawnerEditor.cs` | **전체 교체** | 프리팹 분배 → 스폰 테스트 버튼 |
| `XPOrb.cs` | **1줄 수정** | CarController → PlayerController |
| `PlayerController.cs` | **신규 스텁** | 좀비 시스템이 필요한 API만 |

### 삭제 대상
- `ZombieProjectile.cs` — 새 방향에 원거리 좀비 없음
- 관련 프리팹이 있다면 보존 (에셋이므로), 코드만 삭제

---

## 2. 아키텍처

```
ZombieConfig (SO)          ← 타입별 수치 (General / Signal)
    │
NoiseManager (싱글톤)       ← 소음 0~100, 가청반경 R=N×0.25
    │
PlayerController (스텁)     ← IsStealth, TakeDamage
    │
ZombieController           ← 상태머신 + 탐지 + 이동 + 전투
    │
ZombieSpawner              ← 인구 관리, 15~25 유지
    │
XPOrb (기존, 1줄 수정)     ← 킬 보상
```

### 설계 결정 근거

| 결정 | 선택 | 이유 |
|---|---|---|
| 타입별 분기 | 단일 스크립트 + ZombieConfig SO | MVP 2타입뿐. SO로 수치만 바꾸면 5타입 확장 가능 |
| 상태머신 | enum + switch | 상태 5개, 각 15~30줄. 기존 코드 패턴과 동일 |
| 소음 알림 | 폴링 (FixedUpdate마다 거리 체크) | 이벤트보다 단순. 25체 × 2레이캐스트/프레임 = 충분히 저렴 |
| 이동 | 키네마틱 MovePosition + 분리 스티어링 | 기존 코드에서 검증됨. NavMesh는 MVP 불필요 |

---

## 3. 상태머신

```
States: Idle, Investigate, Chase, Attack, Dead

            ┌──────────────────────────────────────────────┐
            │                   HP ≤ 0                      │
            │ ┌──────────┐ ┌──────────────┐ ┌──────────┐   │
            │ │          │ │              │ │          │   │
            ▼ ▼          │ ▼              │ ▼          │   │
         ┌──────┐    ┌───────────┐   ┌────────┐   ┌────────┐
    ┌───►│ Idle │───►│Investigate│──►│ Chase  │──►│ Attack │
    │    └──────┘    └───────────┘   └────────┘   └────────┘
    │       ▲             │               │            │
    │       │    타임아웃  │     스텔스    │   사거리   │
    │       └─────────────┘     └────┬────┘   밖으로   │
    │                                │        └────────┘
    │                                │
    │         스텔스 (1초 후)          │
    └────────────────────────────────┘
```

### 전이 조건 상세

```
Idle → Investigate:   소음 가청범위 내 AND noise < 50
Idle → Chase:         시야 원뿔 내 감지 OR (가청범위 내 AND noise ≥ 50)
Idle → Dead:          HP ≤ 0

Investigate → Idle:   조사 지점 도착 후 lookTime(3초) 경과 OR investigateTimeout(8초)
Investigate → Chase:  시야 내 감지 OR noise ≥ 50
Investigate → Dead:   HP ≤ 0

Chase → Attack:       거리 ≤ attackRange (1.5m)
Chase → Idle:         스텔스 발동 → 1초 후 타겟 상실 → 3초간 재탐지 불가
Chase → Dead:         HP ≤ 0

Attack → Chase:       거리 > attackRange × 1.2 (히스테리시스)
Attack → Idle:        스텔스 (Chase→Idle과 동일)
Attack → Dead:        HP ≤ 0

Dead:                 터미널. XP 드롭, 피드백, Destroy.
```

---

## 4. 탐지 시스템

### 4.1 시야 (Sight)

```
조건 (모두 충족 시 true):
1. player.IsStealth == false
2. 거리 ≤ config.sightRange (General: 12m)
3. 각도: zombie.forward → toPlayer ≤ config.sightHalfAngle (60°)
4. LOS 레이캐스트: obstacleMask에 안 막힘
```

```csharp
bool CanSeePlayer()
{
    if (_playerController != null && _playerController.IsStealth) return false;

    Vector3 toPlayer = _player.position - transform.position;
    toPlayer.y = 0f;
    float dist = toPlayer.magnitude;

    if (dist > _config.sightRange) return false;
    if (Vector3.Angle(transform.forward, toPlayer) > _config.sightHalfAngle) return false;

    Vector3 eyePos = transform.position + Vector3.up * 1f;
    if (Physics.Raycast(eyePos, toPlayer.normalized, dist, obstacleMask))
        return false;

    return true;
}
```

### 4.2 청각 (Hearing)

```
조건 (모두 충족 시 true):
1. player.IsStealth == false
2. _stealthCooldownTimer ≤ 0 (스텔스 후 3초 재탐지 불가)
3. 거리 ≤ NoiseManager.HearingRadius × config.hearingMultiplier
4. (선택) 장애물 사이에 있으면 유효 반경 50% 감소
```

```csharp
bool CanHearPlayer()
{
    if (_playerController != null && _playerController.IsStealth) return false;
    if (_stealthCooldownTimer > 0f) return false;

    float radius = NoiseManager.Instance.HearingRadius * _config.hearingMultiplier;
    if (radius <= 0f) return false;

    Vector3 toPlayer = _player.position - transform.position;
    toPlayer.y = 0f;
    float dist = toPlayer.magnitude;

    // 장애물 감쇄: 사이에 건물이 있으면 유효 반경 절반
    if (Physics.Raycast(transform.position + Vector3.up, toPlayer.normalized, dist, obstacleMask))
        radius *= 0.5f;

    return dist <= radius;
}
```

### 4.3 스텔스 처리 (per-zombie, FixedUpdate)

```
프레임 단위:
1. 이번 프레임 player.IsStealth == true && 이전 프레임 false
   → _stealthLostTimer = 1.0f  (1초 후 타겟 상실)
2. _stealthLostTimer 카운트다운
   → 0 도달: state → Idle, _stealthCooldownTimer = 3.0f
3. _stealthCooldownTimer 카운트다운
   → 0 도달: 탐지 정상 재개
4. 스텔스 중: CanSeePlayer() / CanHearPlayer() 모두 false 반환
```

---

## 5. 신호 좀비 (Signal Zombie)

**기존 좀비를 소환하지, 새 좀비를 스폰하지 않음** → 인구 캡(15~25) 유지.

```
1. 신호 좀비가 Chase 진입 (플레이어 감지)
2. 3초 대기 (config.signalDelay)
   - 시각 피드백: 마젠타 파티클/사운드
3. Physics.OverlapSphere(position, 15m, zombieLayer)
4. 반경 내 Idle/Investigate 좀비 최대 4마리에게 AlertTo() 호출
5. AlertTo() → 즉시 Chase 진입 (Investigate 건너뜀)
6. 신호 쿨다운 15초 (재발동 방지)
```

**왜 암살 대상 1순위인가**: 신호 좀비를 먼저 안 잡으면 4마리가 즉시 추적 상태로 전환. 스텔스 접근 → 뒤에서 암살(소음 0, 데미지 2x)이 정답. GDD의 핵심 루프.

---

## 6. 파일별 구현 명세

---

### 6.1 `ZombieConfig.cs` (신규)

```csharp
public enum ZombieType { General, Signal }

[CreateAssetMenu(menuName = "ZombieCrush/ZombieConfig")]
public class ZombieConfig : ScriptableObject
{
    [Header("Identity")]
    public ZombieType zombieType;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float acceleration = 6f;

    [Header("Health")]
    public int maxHP = 3;

    [Header("Detection - Sight")]
    public float sightRange = 12f;
    public float sightHalfAngle = 60f;

    [Header("Detection - Hearing")]
    public float hearingMultiplier = 1f;

    [Header("Combat")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1.5f;
    public float attackDamage = 10f;

    [Header("Investigation")]
    public float investigateLookTime = 3f;
    public float investigateTimeout = 8f;

    [Header("Signal")]
    public bool isSignalZombie;
    public float signalRadius = 15f;
    public float signalDelay = 3f;
    public int signalSummonCount = 4;

    [Header("XP Drop")]
    public int xpOrbCountMin = 3;
    public int xpOrbCountMax = 5;
}
```

**SO 에셋 생성 (Unity 에디터)**:
- `Assets/_Project/Data/ZombieConfig_General.asset` — 기본값 그대로
- `Assets/_Project/Data/ZombieConfig_Signal.asset` — `isSignalZombie = true`

### General vs Signal 수치 차이 (시작값)

| 항목 | General | Signal |
|---|---|---|
| moveSpeed | 3 | 2.5 |
| maxHP | 3 (3타) | 3 (3타) |
| sightRange | 12 | 15 (넓음 — 발견 확률 높여 암살 가치 상승) |
| hearingMultiplier | 1.0 | 1.3 (소음에 민감) |
| isSignalZombie | false | true |
| signalRadius | — | 15 |
| signalDelay | — | 3 |
| signalSummonCount | — | 4 |

---

### 6.2 `NoiseManager.cs` (신규)

```csharp
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance { get; private set; }

    [SerializeField] float decayRate = 3f;
    [SerializeField] float radiusPerNoise = 0.25f;
    [SerializeField] float maxRadius = 25f;

    float _noise;

    public float CurrentNoise => _noise;
    public float HearingRadius => Mathf.Clamp(_noise * radiusPerNoise, 0f, maxRadius);
    public bool IsChaseThreshold => _noise >= 50f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_noise > 0f)
            _noise = Mathf.Max(0f, _noise - decayRate * Time.deltaTime);
    }

    public void AddNoise(float amount)
    {
        _noise = Mathf.Clamp(_noise + amount, 0f, 100f);
    }
}
```

**호출처** (PlayerController 쪽에서):
- 근접 타격: `NoiseManager.Instance.AddNoise(10f);`
- 크래프팅 중: `NoiseManager.Instance.AddNoise(20f * Time.deltaTime);` (매 프레임)
- 암살: AddNoise 호출 안 함

---

### 6.3 `PlayerController.cs` (최소 스텁)

좀비 AI가 필요로 하는 API만. 이동/입력/전투는 별도 작업에서 구현.

```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("HP")]
    [SerializeField] float maxHP = 100f;
    float _currentHP;

    [Header("Stealth")]
    [SerializeField] float stealthDuration = 3f;
    [SerializeField] float stealthCooldown = 8f;

    bool _isStealth;
    float _stealthTimer;
    float _cooldownTimer;

    public bool IsStealth => _isStealth;
    public float CurrentHP => _currentHP;
    public float MaxHP => maxHP;

    public event System.Action OnPlayerDied;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _currentHP = maxHP;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // 스텔스 타이머
        if (_isStealth)
        {
            _stealthTimer -= Time.deltaTime;
            if (_stealthTimer <= 0f)
            {
                _isStealth = false;
                _cooldownTimer = stealthCooldown;
            }
        }
        else if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }

        // TODO: 이동, 입력, 전투 로직 추가
        // 테스트용 스텔스 토글: Space
        if (Input.GetKeyDown(KeyCode.Space) && !_isStealth && _cooldownTimer <= 0f)
        {
            _isStealth = true;
            _stealthTimer = stealthDuration;
            // 감염도 +1 (SyncRateManager 0~1f이므로 0.1f = 감염 1칸)
            SyncRateManager.Instance?.AddSync(0.1f);
        }
    }

    public void TakeDamage(float amount)
    {
        _currentHP -= amount;
        if (_currentHP <= 0f)
        {
            _currentHP = 0f;
            OnPlayerDied?.Invoke();
        }
    }
}
```

---

### 6.4 `ZombieController.cs` (전체 교체)

**재사용하는 로직** (기존 코드에서):
- `CalcSeparation()` — OverlapSphere 분리 스티어링 (635~651줄)
- `SampleTerrainHeight()` — 지면 레이캐스트 (610~617줄)
- `PlaySpawnAnimation()` — DOTween RiseFromGround/RushFromSide (220~242줄)
- `SpawnXPOrbs()` — 방사형 버스트 (890~902줄)
- `_groundOffset` 계산 (Awake, 137~139줄)
- 이동 스무딩 패턴 (279~280줄, 지수감쇠 Lerp)
- MMSpringScale.Bump, MMTimeScaleEvent 히트스탑

**완전 삭제하는 로직**:
- Ranged/Charger/Laser 전체 (315~866줄)
- CarController/CarZone/HullManager 참조 전체
- OnTriggerEnter 차량 충돌 (653~732줄)
- Launch/ChainKillRoutine 드리프트 킬 (904~958줄)
- GetTargetDirection의 Flank/Block/Group 행동 (515~608줄)

```csharp
using DG.Tweening;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public enum SpawnAnimation { None, RiseFromGround, RushFromSide }

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ZombieController : MonoBehaviour
{
    enum ZombieState { Idle, Investigate, Chase, Attack, Dead }

    [Header("Config")]
    [SerializeField] ZombieConfig _config;

    [Header("Ground")]
    [SerializeField] LayerMask groundLayer = -1;

    [Header("Separation")]
    [SerializeField] float separationRadius = 1.5f;
    [SerializeField] float separationStrength = 1.5f;
    [SerializeField] LayerMask zombieLayer;

    [Header("Detection")]
    [SerializeField] LayerMask obstacleMask;

    [Header("Kill Feedback")]
    [SerializeField] ParticleSystem killParticlePrefab;
    [SerializeField] AudioClip killSound;

    [Header("XP")]
    [SerializeField] GameObject xpOrbPrefab;

    // --- Runtime ---
    ZombieState _state = ZombieState.Idle;
    int _currentHP;
    float _attackCooldownTimer;
    float _investigateTimer;
    float _investigateLookTimer;
    Vector3 _investigateTarget;
    float _stealthLostTimer;
    float _stealthCooldownTimer;
    bool _wasPlayerStealthLastFrame;
    float _signalCooldownTimer;
    bool _hasSignaled;

    Transform _player;
    PlayerController _playerController;
    Rigidbody _rb;
    float _groundOffset;
    Vector3 _velocity;
    Vector3 _homePosition;
    bool _dead;
    bool _spawning;

    MMSpringScale _springScale;
    Animator _animator;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    // ──────────── Lifecycle ────────────

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        var col = GetComponent<Collider>();
        _groundOffset = col.bounds.center.y - col.bounds.min.y;

        _springScale = GetComponent<MMSpringScale>();
        _animator = GetComponent<Animator>();
    }

    void OnDestroy()
    {
        transform.DOKill();
    }

    /// <summary>스포너에서 Instantiate 직후 호출</summary>
    public void Init(Transform player, Vector3 homePosition)
    {
        _player = player;
        _playerController = player != null ? player.GetComponent<PlayerController>() : null;
        _homePosition = homePosition;
        _currentHP = _config != null ? _config.maxHP : 3;
        _state = ZombieState.Idle;
        _dead = false;
        _hasSignaled = false;
    }

    public void PlaySpawnAnimation(SpawnAnimation type, Vector3 rushTarget = default, bool hasRushTarget = false)
    {
        transform.localScale = Vector3.one;
        switch (type)
        {
            case SpawnAnimation.RiseFromGround:
                _spawning = true;
                transform.localScale = new Vector3(1f, 0f, 1f);
                transform.DOScaleY(1f, 0.4f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => _spawning = false);
                break;
            case SpawnAnimation.RushFromSide:
                _spawning = true;
                Vector3 dest = hasRushTarget ? rushTarget : transform.position;
                transform.DOMove(dest, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => _spawning = false);
                break;
        }
    }

    // ──────────── Main Loop ────────────

    void FixedUpdate()
    {
        if (_dead || _spawning || _player == null) return;

        UpdateStealthTracking();
        UpdateStateMachine();
        UpdateMovement();
        UpdateAnimation();
    }

    // ──────────── Stealth Tracking ────────────

    void UpdateStealthTracking()
    {
        bool stealthNow = _playerController != null && _playerController.IsStealth;

        // 스텔스 진입 감지 (엣지)
        if (stealthNow && !_wasPlayerStealthLastFrame)
        {
            if (_state == ZombieState.Chase || _state == ZombieState.Attack)
                _stealthLostTimer = 1.0f;
        }

        // 1초 후 타겟 상실
        if (_stealthLostTimer > 0f)
        {
            _stealthLostTimer -= Time.fixedDeltaTime;
            if (_stealthLostTimer <= 0f)
            {
                _state = ZombieState.Idle;
                _stealthCooldownTimer = 3.0f;
            }
        }

        // 쿨다운 감소
        if (_stealthCooldownTimer > 0f)
            _stealthCooldownTimer -= Time.fixedDeltaTime;

        _wasPlayerStealthLastFrame = stealthNow;
    }

    // ──────────── Detection ────────────

    bool CanSeePlayer()
    {
        if (_playerController != null && _playerController.IsStealth) return false;
        if (_config == null || _player == null) return false;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        if (dist > _config.sightRange) return false;
        if (dist < 0.01f) return true;
        if (Vector3.Angle(transform.forward, toPlayer) > _config.sightHalfAngle) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(eyePos, toPlayer.normalized, dist, obstacleMask))
            return false;

        return true;
    }

    bool CanHearPlayer()
    {
        if (_playerController != null && _playerController.IsStealth) return false;
        if (_stealthCooldownTimer > 0f) return false;
        if (NoiseManager.Instance == null || _config == null || _player == null) return false;

        float radius = NoiseManager.Instance.HearingRadius * _config.hearingMultiplier;
        if (radius <= 0f) return false;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        // 장애물 감쇄
        if (Physics.Raycast(transform.position + Vector3.up, toPlayer.normalized, dist, obstacleMask))
            radius *= 0.5f;

        return dist <= radius;
    }

    // ──────────── State Machine ────────────

    void UpdateStateMachine()
    {
        bool canSee = CanSeePlayer();
        bool canHear = CanHearPlayer();
        bool chaseThreshold = NoiseManager.Instance != null && NoiseManager.Instance.IsChaseThreshold;

        switch (_state)
        {
            case ZombieState.Idle:
                if (canSee || (canHear && chaseThreshold))
                    EnterChase();
                else if (canHear)
                    EnterInvestigate(_player.position);
                break;

            case ZombieState.Investigate:
                _investigateTimer += Time.fixedDeltaTime;

                if (canSee || (canHear && chaseThreshold))
                {
                    EnterChase();
                    break;
                }

                Vector3 toTarget = _investigateTarget - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < 1f)
                {
                    _investigateLookTimer += Time.fixedDeltaTime;
                    if (_investigateLookTimer >= _config.investigateLookTime)
                    {
                        _state = ZombieState.Idle;
                        break;
                    }
                }

                if (_investigateTimer >= _config.investigateTimeout)
                    _state = ZombieState.Idle;
                break;

            case ZombieState.Chase:
                float distToPlayer = FlatDistance(transform.position, _player.position);

                if (distToPlayer <= _config.attackRange)
                {
                    _state = ZombieState.Attack;
                    _attackCooldownTimer = 0f;
                    break;
                }

                // 신호 좀비: Chase 진입 시 한 번 시그널
                if (_config.isSignalZombie && !_hasSignaled && _signalCooldownTimer <= 0f)
                {
                    _hasSignaled = true;
                    StartCoroutine(SignalCoroutine());
                }
                break;

            case ZombieState.Attack:
                float dist = FlatDistance(transform.position, _player.position);
                if (dist > _config.attackRange * 1.2f)
                {
                    _state = ZombieState.Chase;
                    break;
                }

                _attackCooldownTimer -= Time.fixedDeltaTime;
                if (_attackCooldownTimer <= 0f)
                {
                    PerformAttack();
                    _attackCooldownTimer = _config.attackCooldown;
                }
                break;
        }
    }

    void EnterChase()
    {
        _state = ZombieState.Chase;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;
    }

    void EnterInvestigate(Vector3 target)
    {
        _state = ZombieState.Investigate;
        _investigateTarget = target;
        _investigateTimer = 0f;
        _investigateLookTimer = 0f;
    }

    /// <summary>신호 좀비가 AlertTo()로 호출. 즉시 Chase.</summary>
    public void AlertTo(Vector3 targetPosition)
    {
        if (_dead || _state == ZombieState.Dead) return;
        if (_state == ZombieState.Chase || _state == ZombieState.Attack) return;
        EnterChase();
    }

    // ──────────── Movement ────────────

    void UpdateMovement()
    {
        Vector3 moveTarget;
        float stopDist;

        switch (_state)
        {
            case ZombieState.Investigate:
                moveTarget = _investigateTarget;
                stopDist = 0.5f;
                break;
            case ZombieState.Chase:
                moveTarget = _player.position;
                stopDist = _config.attackRange * 0.8f;
                break;
            case ZombieState.Attack:
                moveTarget = _player.position;
                stopDist = _config.attackRange * 0.8f;
                break;
            default: // Idle
                moveTarget = _homePosition;
                stopDist = 1f;
                break;
        }

        Vector3 toTarget = moveTarget - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        Vector3 desiredDir = dist > stopDist ? toTarget.normalized : Vector3.zero;
        desiredDir += CalcSeparation() * separationStrength;
        desiredDir.y = 0f;

        float speed = _config != null ? _config.moveSpeed : 3f;
        float accel = _config != null ? _config.acceleration : 6f;

        Vector3 desiredVel = desiredDir.sqrMagnitude > 0.001f
            ? desiredDir.normalized * speed
            : Vector3.zero;

        _velocity = Vector3.Lerp(_velocity, desiredVel,
            1f - Mathf.Exp(-accel * Time.fixedDeltaTime));

        if (_velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 nextPos = transform.position + _velocity * Time.fixedDeltaTime;
            nextPos.y = SampleTerrainHeight(nextPos) + _groundOffset;
            _rb.MovePosition(nextPos);

            // Chase/Attack에서는 플레이어를 바라봄, 그 외는 이동 방향
            Vector3 lookDir = (_state == ZombieState.Chase || _state == ZombieState.Attack)
                ? (_player.position - transform.position) : _velocity;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                _rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime));
            }
        }
    }

    void UpdateAnimation()
    {
        if (_animator != null)
            _animator.SetFloat(SpeedHash, _velocity.magnitude);
    }

    // ──────────── Combat ────────────

    void PerformAttack()
    {
        if (_playerController != null)
            _playerController.TakeDamage(_config.attackDamage);
        // TODO: 공격 애니메이션 트리거
    }

    /// <summary>
    /// 플레이어 공격 코드에서 호출.
    /// isAssassination = true면 소음 0 + 데미지 2배 (호출자가 처리).
    /// </summary>
    public void TakeDamage(int amount, bool isAssassination = false)
    {
        if (_dead) return;

        _currentHP -= amount;

        if (_currentHP <= 0)
        {
            Die();
            return;
        }

        // 맞으면 작은 범프
        _springScale?.Bump(new Vector3(0.2f, -0.3f, 0.2f));

        // Idle/Investigate 상태에서 맞으면 (암살 아닌 경우) → Chase
        if (!isAssassination && (_state == ZombieState.Idle || _state == ZombieState.Investigate))
            EnterChase();
    }

    void Die()
    {
        _dead = true;
        _state = ZombieState.Dead;

        _springScale?.Bump(new Vector3(0.3f, -0.5f, 0.3f));
        MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0.03f, 0.05f, false, 0f, false);

        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, transform.position, Quaternion.identity);
        if (killSound != null)
            AudioSource.PlayClipAtPoint(killSound, transform.position);

        SpawnXPOrbs();
        Destroy(gameObject);
    }

    void SpawnXPOrbs()
    {
        if (xpOrbPrefab == null || _player == null || _config == null) return;
        int count = Random.Range(_config.xpOrbCountMin, _config.xpOrbCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-30f, 30f);
            Vector3 burstDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var obj = Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
            var orb = obj.GetComponent<XPOrb>();
            if (orb != null) orb.Init(burstDir, _player);
        }
    }

    // ──────────── Signal Zombie ────────────

    System.Collections.IEnumerator SignalCoroutine()
    {
        yield return new WaitForSeconds(_config.signalDelay);

        if (_dead) yield break;

        // TODO: 시그널 파티클/사운드

        Collider[] nearby = Physics.OverlapSphere(transform.position, _config.signalRadius, zombieLayer);
        int summoned = 0;
        foreach (var col in nearby)
        {
            if (summoned >= _config.signalSummonCount) break;
            if (col.gameObject == gameObject) continue;

            var zombie = col.GetComponent<ZombieController>();
            if (zombie != null && !zombie._dead
                && (zombie._state == ZombieState.Idle || zombie._state == ZombieState.Investigate))
            {
                zombie.AlertTo(_player.position);
                summoned++;
            }
        }

        _signalCooldownTimer = 15f;
    }

    // ──────────── Utility (기존 코드 재사용) ────────────

    Vector3 CalcSeparation()
    {
        Vector3 sep = Vector3.zero;
        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius, zombieLayer);
        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f)
                sep += away.normalized * (separationRadius / d - 1f);
        }
        return sep;
    }

    float SampleTerrainHeight(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f,
                            groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return pos.y;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
```

---

### 6.5 `ZombieSpawner.cs` (전체 교체)

```csharp
using System.Collections.Generic;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject generalZombiePrefab;
    [SerializeField] GameObject signalZombiePrefab;

    [Header("Player")]
    [SerializeField] Transform playerTransform;

    [Header("Population")]
    [SerializeField] int minZombies = 15;
    [SerializeField] int maxZombies = 25;
    [SerializeField] float spawnInterval = 2f;

    [Header("Spawn Radius")]
    [SerializeField] float minSpawnRadius = 20f;
    [SerializeField] float maxSpawnRadius = 35f;
    [SerializeField] float despawnDistance = 60f;

    [Header("Signal Zombie")]
    [SerializeField, Range(0f, 1f)] float signalSpawnChance = 0.08f;

    [Header("Ground")]
    [SerializeField] LayerMask groundLayer = -1;

    List<ZombieController> _activeZombies = new List<ZombieController>();
    float _timer;

    void Start()
    {
        if (playerTransform == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) playerTransform = pc.transform;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        CleanupDead();
        DespawnFar();

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval && _activeZombies.Count < maxZombies)
        {
            _timer = 0f;
            SpawnOne();
        }

        // 최소 인구 유지
        if (_activeZombies.Count < minZombies)
            SpawnOne();
    }

    void SpawnOne()
    {
        Vector3 pos = FindSpawnPosition();
        if (pos == Vector3.zero) return;

        bool isSignal = Random.value < signalSpawnChance;
        GameObject prefab = isSignal && signalZombiePrefab != null
            ? signalZombiePrefab
            : generalZombiePrefab;

        if (prefab == null) return;

        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);
        ZombieController zombie = obj.GetComponent<ZombieController>();
        if (zombie != null)
        {
            zombie.Init(playerTransform, pos);
            zombie.PlaySpawnAnimation(SpawnAnimation.RiseFromGround);
            _activeZombies.Add(zombie);
        }
    }

    Vector3 FindSpawnPosition()
    {
        Camera cam = Camera.main;
        for (int i = 0; i < 10; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 candidate = playerTransform.position + new Vector3(dir.x * dist, 0f, dir.y * dist);
            candidate.y = SampleTerrainHeight(candidate);

            if (cam == null || IsOutsideViewport(cam, candidate))
                return candidate;
        }
        return Vector3.zero;
    }

    void CleanupDead()
    {
        for (int i = _activeZombies.Count - 1; i >= 0; i--)
        {
            if (_activeZombies[i] == null)
                _activeZombies.RemoveAt(i);
        }
    }

    void DespawnFar()
    {
        Vector3 playerPos = playerTransform.position;
        for (int i = _activeZombies.Count - 1; i >= 0; i--)
        {
            if (_activeZombies[i] == null) { _activeZombies.RemoveAt(i); continue; }
            if (Vector3.Distance(_activeZombies[i].transform.position, playerPos) > despawnDistance)
            {
                Destroy(_activeZombies[i].gameObject);
                _activeZombies.RemoveAt(i);
            }
        }
    }

    float SampleTerrainHeight(Vector3 worldPos)
    {
        Vector3 origin = new Vector3(worldPos.x, 200f, worldPos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f,
                            groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return playerTransform != null ? playerTransform.position.y : 0f;
    }

    static bool IsOutsideViewport(Camera cam, Vector3 worldPos)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return true;
        return vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;
    }
}
```

---

### 6.6 `XPOrb.cs` 수정 (1줄)

56번 줄:
```csharp
// 기존
CarController car = FindFirstObjectByType<CarController>();
if (car != null) _carTransform = car.transform;

// 변경
PlayerController player = FindFirstObjectByType<PlayerController>();
if (player != null) _carTransform = player.transform;
```

필드명 `_carTransform`은 일단 유지 (전체 리네임은 불필요 — 동작에 영향 없음).

---

### 6.7 `Editor/ZombieSpawnerEditor.cs` (교체)

```csharp
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ZombieSpawner))]
public class ZombieSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        if (Application.isPlaying && GUILayout.Button("Force Spawn 5"))
        {
            // 테스트용: 즉시 5마리 스폰
            var spawner = (ZombieSpawner)target;
            for (int i = 0; i < 5; i++)
            {
                // SpawnOne은 private이므로 리플렉션 또는 public 테스트 메서드 필요
                // 대안: ZombieSpawner에 public void DebugSpawn(int count) 추가
            }
        }
    }
}
```

> **참고**: 에디터는 선택사항. 기존 에디터 삭제만 해도 됨 (프리팹 분배 기능은 더 이상 불필요).

---

## 7. 구현 순서 & 체크리스트

순서대로 구현. 각 단계 완료 후 Unity 에디터에서 에러 없는지 확인.

```
1. [ ] ZombieConfig.cs 생성
       → 컴파일 확인
       → Assets/_Project/Data/ 폴더에 SO 에셋 2개 생성 (General, Signal)

2. [ ] NoiseManager.cs 생성
       → 씬에 빈 GO "NoiseManager" 배치 + 스크립트 부착
       → 컴파일 확인

3. [ ] PlayerController.cs 스텁 생성
       → 씬에 캡슐 GO "Player" 배치 + 스크립트 부착
       → Space 키로 스텔스 토글 확인

4. [ ] ZombieProjectile.cs 삭제
       → 컴파일 에러 없는지 확인 (ZombieController만 참조했고, 교체할 것이므로 OK)
       → 만약 다른 곳에서 참조하면 그것도 정리

5. [ ] ZombieController.cs 전체 교체
       → 컴파일 확인
       → 프리팹에 ZombieConfig SO 연결 (Inspector)
       → obstacleMask에 Obstacle 레이어 할당
       → zombieLayer에 Zombie 레이어 할당

6. [ ] ZombieSpawner.cs 전체 교체
       → 컴파일 확인
       → generalZombiePrefab / signalZombiePrefab 연결

7. [ ] XPOrb.cs 수정 (1줄)
       → CarController → PlayerController

8. [ ] Editor/ZombieSpawnerEditor.cs 교체 (또는 삭제)

9. [ ] 플레이 테스트
       → 좀비가 Idle 상태로 스폰되는가
       → 가까이 가면 시야 감지 → Chase → Attack
       → 소음 발생 시 가청반경 내 좀비 반응
       → 스텔스 발동 시 1초 후 추적 중단
       → 신호 좀비가 주변 좀비 호출
       → 죽으면 XP 오브 드롭
```

---

## 8. 프리팹 셋업 가이드

### 그레이박스 좀비 프리팹 (최소)

**General Zombie Prefab**:
```
GeneralZombie (GO)
├── Capsule (MeshRenderer — 회색/탁한 녹색)
├── Collider: CapsuleCollider (isTrigger = true)
├── Rigidbody (isKinematic = true, Interpolate)
├── ZombieController
│   ├── _config → ZombieConfig_General (SO 연결)
│   ├── groundLayer → Ground
│   ├── zombieLayer → Zombie
│   ├── obstacleMask → Obstacle
│   └── xpOrbPrefab → XPOrb 프리팹
└── (선택) MMSpringScale, Animator
Layer: Zombie
Tag: Zombie
```

**Signal Zombie Prefab**:
```
SignalZombie (GO)  — General과 동일 구조
├── Capsule (MeshRenderer — 마젠타/노랑)
├── 상단 기둥/마커 (시각 구분용)
├── ZombieController
│   └── _config → ZombieConfig_Signal (SO 연결)
└── ...
Layer: Zombie
Tag: SignalZombie
```

---

## 9. 암살 판정 (PlayerController 쪽 구현)

좀비 AI 자체는 암살을 판정하지 않음. **공격하는 쪽(PlayerController)이 판정**:

```csharp
// PlayerController의 공격 로직에서:
void AttackZombie(ZombieController target)
{
    Vector3 attackDir = (target.transform.position - transform.position).normalized;
    float dot = Vector3.Dot(attackDir, target.transform.forward);

    if (dot < -0.5f)
    {
        // 암살: 뒤에서 공격
        target.TakeDamage(2, isAssassination: true);   // 2배 데미지
        // 소음 추가 안 함
    }
    else
    {
        // 일반 공격
        target.TakeDamage(1, isAssassination: false);
        NoiseManager.Instance?.AddNoise(10f);
    }
}
```

`dot < -0.5f` = 공격 방향과 좀비 정면이 반대 = 뒤에서 공격.

---

## 10. 기존 코드와의 충돌 위험

| 항목 | 위험 | 대응 |
|---|---|---|
| `ZombieType` enum 이름 충돌 | 기존: `{Ranged, Charger, Laser}`, 신규: `{General, Signal}` | ZombieController.cs 교체 시 함께 교체됨. 다른 파일에서 참조 확인 |
| `SpawnAnimation` enum | 기존 ZombieController에 정의. 신규에서도 동일하게 정의 | 교체 시 자연스럽게 해결 |
| `ZombieBehavior` enum | 기존 전용. 삭제 대상 | 다른 곳 참조 없으면 그냥 사라짐 |
| 씬의 기존 좀비 GO 참조 | Inspector에서 기존 ZombieController 필드가 깨질 수 있음 | 프리팹 재설정 필요 |
| `CarController` 참조 제거 | `XPOrb.cs`만 수정하면 됨. 나머지는 교체되는 파일에 포함 | — |

---

## 11. 후속 작업 (이 문서 범위 밖)

1. **PlayerController 전체 구현** — 이동(WASD), 마우스 방향 바라보기, 클릭 공격, 스텔스
2. **소음 가독성** — 행동 펄스 셰이더/파티클 (infection-noise-design §1.4)
3. **감염도 연동** — SyncRateManager 범위를 0~10 정수로 변경 (현재 0~1f)
4. **크래프팅 시스템** — 후순위
5. **밤낮 사이클 연동** — COZY, 후순위
6. **5타입 확장** — ZombieConfig SO 추가 + ZombieController에 타입별 분기 (대형: 느림+넓은시야, 소형: 빠름+소음민감, 과부하: 사망시 폭발+소음)

---

## 변경 이력

| 날짜 | 변경 |
|---|---|
| 2026-05-29 | 초안 작성. 차량→도보 좀비 AI 전면 교체 설계. |
