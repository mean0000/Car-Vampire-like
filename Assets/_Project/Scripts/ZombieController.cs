using DG.Tweening;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public enum SpawnAnimation { None, RiseFromGround, RushFromSide }
public enum ZombieBehavior  { Chase, Flank, Block, Group }
public enum ZombieType { Ranged, Charger, Laser }

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ZombieController : MonoBehaviour
{
    [SerializeField] float minMoveSpeed = 2f;
    [SerializeField] float maxMoveSpeed = 4.5f;
    float moveSpeed;
    [SerializeField] float speedPenalty = 2f;
    [SerializeField] float stopDistance = 1.5f;

    [Header("Ground")]
    [SerializeField] LayerMask groundLayer = -1;

    [Header("Separation")]
    [SerializeField] float separationRadius = 1.5f;
    [SerializeField] float separationStrength = 1.5f;
    [SerializeField] LayerMask zombieLayer;

    [Header("Car Avoidance")]
    [SerializeField] float carAvoidRadius = 3f;
    [SerializeField] float carAvoidStrength = 8f;

    [Header("Smoothing")]
    [SerializeField] float velocitySmoothing = 8f;

    [Header("Behavior")]
    [SerializeField] float flankOffset = 4f;
    [SerializeField] float blockDistance = 12f;

    [Header("Kill Feedback")]
    [SerializeField] ParticleSystem killParticlePrefab;
    [SerializeField] AudioClip killSound;

    [Header("XP")]
    [SerializeField] GameObject xpOrbPrefab;
    [SerializeField] int orbCountMin = 3;
    [SerializeField] int orbCountMax = 5;

    [Header("Fuel")]
    [SerializeField] float killFuelAmount = 15f;

    [Header("Spread Fire")]
    [SerializeField] int spreadCount = 3;
    [SerializeField] float spreadAngle = 30f;
    [SerializeField] float spreadDifficultyThreshold = 0.5f;

    [Header("Ranged Attack")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] GameObject indicatorPrefab;
    [SerializeField] float fireRange = 25f;
    [SerializeField] float fireInterval = 2.5f;
    [SerializeField] float searchTime = 0.6f;
    [SerializeField] float lockTime = 0.4f;
    [SerializeField] float flightDuration = 0.8f;
    [SerializeField] float projectileDamage = 4f;
    [SerializeField] float blastRadius = 2f;

    [Header("Zombie Type")]
    [SerializeField] ZombieType zombieType = ZombieType.Ranged;

    [Header("Charger")]
    [SerializeField] float chargeSpeedMultiplier = 1.8f;
    [SerializeField] float chargeTriggerRange = 12f;
    [SerializeField] float chargeDamage = 8f;
    [SerializeField] float chargePositionTime = 1.2f;
    [SerializeField] float chargeRecoverTime = 2.0f;
    [SerializeField] float turnDetectThreshold = 1.5f;
    [SerializeField] float innerArcOffset = 6f;
    [SerializeField] float chargePredictAhead = 8f;
    bool _charging;
    ChargerState _chargerState;
    float _chargerStateTimer;

    [Header("Laser")]
    [SerializeField] float laserRange = 30f;
    [SerializeField] float laserAimTime = 2.0f;
    [SerializeField] float laserFireDuration = 0.3f;
    [SerializeField] float laserDamage = 6f;
    [SerializeField] float laserCooldown = 2.5f;
    [SerializeField] Color laserAimColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] Color laserFireColor = new Color(1f, 0.2f, 0f, 1f);
    LineRenderer _laserLine;

    enum ChargerState { Stalk, Position, Charge, Recover }
    enum LaserState { Idle, Aiming, Firing, Cooldown }
    LaserState _laserState;
    float _laserTimer;

    [Header("Drift Launch")]
    [SerializeField] int maxChainKills = 3;
    [SerializeField] float chainKillRadius = 1.8f;
    [SerializeField] float homingRadius = 8f;
    [SerializeField] float homingStrength = 3f;

    MMSpringScale _springScale;
    Animator _animator;
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    Transform _target;
    Rigidbody _rb;
    Collider _carCollider;
    CarController _car;
    Rigidbody _carRb;
    float _groundOffset;
    bool _dead;
    bool _launched;
    bool _spawning;
    int _chainKillCount;
    Vector3 _velocity;
    ZombieBehavior _behavior;
    float _flankSign;
    float _difficulty;
    float _fireTimer = -1f;
    bool _firstShot = true;
    bool _searching;
    bool _locked;
    float _phaseTimer;
    Vector3 _aimTarget;
    Vector2 _aimRandomOffset;
    GameObject _indicator;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        // 콜라이더 바닥~피벗 간 오프셋 (피벗이 중심에 있으면 extents.y만큼)
        var col = GetComponent<Collider>();
        _groundOffset = col.bounds.center.y - col.bounds.min.y;

        _springScale = GetComponent<MMSpringScale>();
        if (_springScale == null)
            Debug.LogWarning($"[ZombieController] MMSpringScale missing on {gameObject.name} — kill bump disabled", this);

        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (_target == null)
        {
            CarController car = FindFirstObjectByType<CarController>();
            if (car != null) _target = car.transform;
        }
        if (_target != null)
        {
            _carCollider = _target.GetComponent<Collider>();
            if (_car == null) _car = _target.GetComponent<CarController>();
            if (_carRb == null) _carRb = _target.GetComponent<Rigidbody>();
        }
    }

    void OnDestroy()
    {
        transform.DOKill();
        DestroyIndicator();
        if (_laserLine != null)
        {
            if (_laserLine.material != null) Destroy(_laserLine.material);
            _laserLine.enabled = false;
        }
    }

    public void SetZombieType(ZombieType type) { zombieType = type; }

    public void Init(Transform target, float difficulty = 0f, float speedBonus = 0f)
    {
        _charging = false;
        _chargerState = ChargerState.Stalk;
        _chargerStateTimer = 0f;
        _laserState = LaserState.Idle;
        _laserTimer = 0f;

        _target = target;
        _carCollider = target != null ? target.GetComponent<Collider>() : null;
        _car = target != null ? target.GetComponent<CarController>() : null;
        _carRb = target != null ? target.GetComponent<Rigidbody>() : null;

        moveSpeed = Mathf.Lerp(minMoveSpeed, maxMoveSpeed, difficulty) + speedBonus;
        _difficulty = difficulty;
        _flankSign = Random.value > 0.5f ? 1f : -1f;
        _behavior = RollBehavior(difficulty);
    }

    ZombieBehavior RollBehavior(float difficulty)
    {
        float r = Random.value;
        if (difficulty < 0.3f)
        {
            // Chase 80%, Flank 15%, Block 5%
            if (r < 0.80f) return ZombieBehavior.Chase;
            if (r < 0.95f) return ZombieBehavior.Flank;
            return ZombieBehavior.Block;
        }
        if (difficulty < 0.6f)
        {
            // Chase 50%, Flank 25%, Block 15%, Group 10%
            if (r < 0.50f) return ZombieBehavior.Chase;
            if (r < 0.75f) return ZombieBehavior.Flank;
            if (r < 0.90f) return ZombieBehavior.Block;
            return ZombieBehavior.Group;
        }
        // Chase 30%, Flank 25%, Block 25%, Group 20%
        if (r < 0.30f) return ZombieBehavior.Chase;
        if (r < 0.55f) return ZombieBehavior.Flank;
        if (r < 0.80f) return ZombieBehavior.Block;
        return ZombieBehavior.Group;
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

    void FixedUpdate()
    {
        if (_dead || _spawning || _target == null) return;

        Vector3 toTarget = GetTargetDirection();
        float dist = toTarget.magnitude;

        Vector3 separation = CalcSeparation();

        // Charger 돌진 중에는 차량 회피 무시
        Vector3 carAvoid = (zombieType == ZombieType.Charger && _charging)
            ? Vector3.zero
            : CalcCarAvoidance();

        float effectiveStopDist;
        switch (zombieType)
        {
            case ZombieType.Ranged: effectiveStopDist = fireRange; break;
            case ZombieType.Laser:  effectiveStopDist = laserRange; break;
            default:                effectiveStopDist = stopDistance; break;
        }

        Vector3 desiredDir = (dist > effectiveStopDist ? toTarget.normalized : Vector3.zero)
                           + separation * separationStrength
                           + carAvoid * carAvoidStrength;
        desiredDir.y = 0f;

        float currentSpeed = moveSpeed;
        if (zombieType == ZombieType.Charger && _charging)
            currentSpeed *= chargeSpeedMultiplier;

        Vector3 desiredVel = desiredDir.sqrMagnitude > 0.001f
            ? desiredDir.normalized * currentSpeed
            : Vector3.zero;

        _velocity = Vector3.Lerp(_velocity, desiredVel,
            1f - Mathf.Exp(-velocitySmoothing * Time.fixedDeltaTime));

        Vector3 toCar = _target.position - transform.position;
        toCar.y = 0f;
        float carDist = toCar.magnitude;

        switch (zombieType)
        {
            case ZombieType.Ranged:
                UpdateRangedAttack(carDist);
                break;
            case ZombieType.Charger:
                UpdateCharger(carDist);
                break;
            case ZombieType.Laser:
                UpdateLaser(carDist);
                break;
        }

        if (_velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 nextPos = transform.position + _velocity * Time.fixedDeltaTime;
            nextPos.y = SampleTerrainHeight(nextPos) + _groundOffset;
            _rb.MovePosition(nextPos);

            // 이동 방향으로 회전
            Quaternion targetRot = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
            _rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime));
        }

        // Animator Speed 파라미터
        if (_animator != null)
            _animator.SetFloat(SpeedHash, _velocity.magnitude);
    }

    void UpdateRangedAttack(float carDist)
    {
        // 원거리 공격 — 사거리 진입 시 즉시 반응
        if (carDist <= fireRange)
        {
            if (_firstShot)
            {
                _firstShot = false;
                _fireTimer = fireInterval - Random.Range(0.2f, 0.5f);
            }
            _fireTimer += Time.fixedDeltaTime;

            // 1단계: 탐색 — 예측만 계산, indicator 없음
            if (_fireTimer >= fireInterval && !_searching && !_locked)
            {
                _searching = true;
                _phaseTimer = 0f;
                _aimRandomOffset = Random.insideUnitCircle * blastRadius * 0.3f;
            }
            if (_searching)
            {
                UpdateAimPrediction();
                _phaseTimer += Time.fixedDeltaTime;
                if (_phaseTimer >= searchTime)
                {
                    // 2단계: 잠금 — 탄착점 확정 + indicator 표시
                    _searching = false;
                    _locked = true;
                    _phaseTimer = 0f;
                    UpdateIndicator();
                }
            }
            // 2단계: 잠금 — indicator 표시 유지, 타이머 후 발사
            if (_locked)
            {
                // lock 중에도 느린 보간으로 예측 추적 유지
                Vector3 prevAim = _aimTarget;
                UpdateAimPrediction();
                _aimTarget = Vector3.Lerp(prevAim, _aimTarget, 0.3f);
                UpdateIndicator(); // indicator도 따라 이동

                _phaseTimer += Time.fixedDeltaTime;
                if (_phaseTimer >= lockTime)
                {
                    Fire();
                    _locked = false;
                    _fireTimer = 0f;
                }
            }
        }
        else
        {
            _fireTimer = 0f;
            if (_searching || _locked) DestroyIndicator();
            _searching = false;
            _locked = false;
        }
    }

    void UpdateCharger(float carDist)
    {
        float angularY = _carRb != null ? _carRb.angularVelocity.y : 0f;
        bool carTurning = Mathf.Abs(angularY) > turnDetectThreshold;

        switch (_chargerState)
        {
            case ChargerState.Stalk:
                _charging = false;
                if (carDist <= chargeTriggerRange && carTurning)
                {
                    _chargerState = ChargerState.Position;
                    _chargerStateTimer = 0f;
                }
                break;

            case ChargerState.Position:
                _charging = false;
                _chargerStateTimer += Time.fixedDeltaTime;
                if (_chargerStateTimer >= chargePositionTime)
                {
                    _chargerState = ChargerState.Charge;
                    _charging = true;
                    _chargerStateTimer = 0f;
                }
                break;

            case ChargerState.Charge:
                _charging = true;
                _chargerStateTimer += Time.fixedDeltaTime;
                if (carDist > chargeTriggerRange * 2.5f || _chargerStateTimer > 4f)
                {
                    _chargerState = ChargerState.Recover;
                    _charging = false;
                    _chargerStateTimer = 0f;
                }
                break;

            case ChargerState.Recover:
                _charging = false;
                _chargerStateTimer += Time.fixedDeltaTime;
                if (_chargerStateTimer >= chargeRecoverTime)
                {
                    _chargerState = ChargerState.Stalk;
                    _chargerStateTimer = 0f;
                }
                break;
        }
    }

    void UpdateLaser(float carDist)
    {
        if (_target == null) return;

        switch (_laserState)
        {
            case LaserState.Idle:
                if (carDist <= laserRange)
                {
                    _laserState = LaserState.Aiming;
                    _laserTimer = 0f;
                    EnsureLaserLine();
                    _laserLine.enabled = true;
                }
                break;

            case LaserState.Aiming:
                _laserTimer += Time.fixedDeltaTime;
                UpdateLaserLine(laserAimColor, 0.05f);
                if (_laserTimer >= laserAimTime)
                {
                    _laserState = LaserState.Firing;
                    _laserTimer = 0f;
                    FireLaser();
                }
                break;

            case LaserState.Firing:
                _laserTimer += Time.fixedDeltaTime;
                UpdateLaserLine(laserFireColor, Mathf.Lerp(0.3f, 0.05f, _laserTimer / laserFireDuration));
                if (_laserTimer >= laserFireDuration)
                {
                    _laserState = LaserState.Cooldown;
                    _laserTimer = 0f;
                    if (_laserLine != null) _laserLine.enabled = false;
                }
                break;

            case LaserState.Cooldown:
                _laserTimer += Time.fixedDeltaTime;
                if (_laserTimer >= laserCooldown)
                    _laserState = LaserState.Idle;
                break;
        }
    }

    void EnsureLaserLine()
    {
        if (_laserLine != null) return;
        _laserLine = gameObject.AddComponent<LineRenderer>();
        _laserLine.positionCount = 2;
        var shader = Shader.Find("Unlit/Color");
        if (shader != null)
            _laserLine.material = new Material(shader);
        _laserLine.startWidth = 0.05f;
        _laserLine.endWidth = 0.05f;
        _laserLine.enabled = false;
    }

    void UpdateLaserLine(Color color, float width)
    {
        if (_laserLine == null || _target == null) return;
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        _laserLine.SetPosition(0, origin);
        _laserLine.SetPosition(1, _target.position);
        _laserLine.startColor = color;
        _laserLine.endColor = color;
        _laserLine.startWidth = width;
        _laserLine.endWidth = width;
    }

    void FireLaser()
    {
        if (_target == null) return;
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 dir = (_target.position - origin).normalized;

        // 좀비/터레인 무시 — 차량 레이어만 대상
        int carLayer = _target.gameObject.layer;
        int mask = 1 << carLayer;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, laserRange, mask, QueryTriggerInteraction.Collide))
        {
            CarController car = hit.collider.GetComponentInParent<CarController>();
            if (car != null)
            {
                CarZone zone = CarZoneHelper.Resolve(car.transform, hit.point);
                HullManager.Instance?.TakeDamage(laserDamage, zone);
            }
        }
    }

    Vector3 GetTargetDirection()
    {
        if (zombieType == ZombieType.Charger && _carRb != null)
        {
            float angularY = _carRb.angularVelocity.y;

            if (_chargerState == ChargerState.Position)
            {
                float turnSign = Mathf.Sign(angularY);
                Vector3 carFwd = _car != null ? _car.FlatVelocity : Vector3.zero;
                if (carFwd.sqrMagnitude < 1f) carFwd = _target.forward;
                else carFwd.Normalize();

                Vector3 carRight = _target.right;
                carRight.y = 0f;
                carRight.Normalize();

                Vector3 innerPos = _target.position
                    + carFwd * chargePredictAhead
                    + carRight * turnSign * innerArcOffset;

                Vector3 toInner = innerPos - transform.position;
                toInner.y = 0f;
                return toInner;
            }

            if (_chargerState == ChargerState.Charge)
            {
                Vector3 carVel = _car != null ? _car.FlatVelocity : Vector3.zero;
                Vector3 predictedPos = _target.position + carVel * 0.3f;
                Vector3 toTarget = predictedPos - transform.position;
                toTarget.y = 0f;
                return toTarget;
            }
        }

        Vector3 carPos = _target.position;

        switch (_behavior)
        {
            case ZombieBehavior.Flank:
            {
                // 차 전진 방향의 수직으로 flankOffset만큼 오프셋된 지점을 향해 이동
                Vector3 carFwd = _target.forward;
                carFwd.y = 0f;
                Vector3 lateral = Vector3.Cross(Vector3.up, carFwd).normalized;
                Vector3 flankPos = carPos + lateral * (_flankSign * flankOffset);
                return flankPos - transform.position;
            }

            case ZombieBehavior.Block:
            {
                Vector3 carVel = _car != null ? _car.FlatVelocity : Vector3.zero;
                // 차가 느리면 Chase로 폴백
                if (carVel.sqrMagnitude < 1f)
                    break;
                Vector3 blockPos = carPos + carVel.normalized * blockDistance;
                return blockPos - transform.position;
            }

            case ZombieBehavior.Group:
            {
                // 근처 Group 좀비들의 평균 위치와 목표 방향을 혼합
                Vector3 groupCenter = Vector3.zero;
                int groupCount = 0;
                Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius * 2f, zombieLayer);
                foreach (var col in nearby)
                {
                    if (col.gameObject == gameObject) continue;
                    var z = col.GetComponent<ZombieController>();
                    if (z != null && !z._dead && z._behavior == ZombieBehavior.Group)
                    {
                        groupCenter += col.transform.position;
                        groupCount++;
                    }
                }

                if (groupCount == 0)
                    break; // Chase 폴백

                groupCenter /= groupCount;
                Vector3 toGroup  = (groupCenter - transform.position);
                Vector3 toTarget = (carPos - transform.position);
                toGroup.y = 0f;
                toTarget.y = 0f;
                return (toGroup + toTarget).normalized * toTarget.magnitude;
            }
        }

        // Chase (default / fallback)
        Vector3 result = carPos - transform.position;
        result.y = 0f;
        return result;
    }

    float SampleTerrainHeight(Vector3 pos)
    {
        Vector3 origin = new Vector3(pos.x, 200f, pos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f,
                            groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return pos.y;
    }

    Vector3 CalcCarAvoidance()
    {
        if (_carCollider == null) return Vector3.zero;

        Vector3 closest = _carCollider.ClosestPoint(transform.position);
        Vector3 away = transform.position - closest;
        away.y = 0f;
        float d = away.magnitude;

        if (d >= carAvoidRadius) return Vector3.zero;
        if (d > 0.001f)
            return away.normalized * (carAvoidRadius / d - 1f);
        else
            return (transform.position - _target.position).normalized * 10f;
    }

    Vector3 CalcSeparation()
    {
        Vector3 sep = Vector3.zero;
        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius, zombieLayer);
        foreach (var col in nearby)
        {
            if (col.gameObject == gameObject) continue;
            var z = col.GetComponent<ZombieController>();
            if (z != null && z._launched) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.001f)
                sep += away.normalized * (separationRadius / d - 1f);
        }
        return sep;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_dead) return;

        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) return;

        // Charger: 돌진 중 충돌 — 정면에서 속도가 있으면 kill, 아니면 차에 데미지
        if (zombieType == ZombieType.Charger && _chargerState == ChargerState.Charge)
        {
            CarZone zone = CarZoneHelper.Resolve(car.transform, transform.position);
            if (zone != CarZone.Front || car.CurrentSpeed < 3f)
            {
                // Charger 돌진 성공 — 차에 데미지, 좀비 사망
                HullManager.Instance?.TakeDamage(chargeDamage, zone);
                _dead = true;
                if (_laserLine != null) _laserLine.enabled = false;
                if (killParticlePrefab != null)
                    Instantiate(killParticlePrefab, transform.position, Quaternion.identity);
                Destroy(gameObject);
                return;
            }
            // Front hit at speed — fall through to normal kill
        }

        if (car.CurrentSpeed < 3f) return;

        Vector3 toZombie = transform.position - car.transform.position;
        toZombie.y = 0f;
        Vector3 carDir = car.FlatVelocity.sqrMagnitude > 0.01f
            ? car.FlatVelocity.normalized
            : car.transform.forward;
        if (toZombie.sqrMagnitude > 0.001f &&
            Vector3.Dot(carDir, toZombie.normalized) < -0.5f)
            return;

        _dead = true;
        if (_laserLine != null) _laserLine.enabled = false;
        _searching = false;
        _locked = false;
        DestroyIndicator();

        bool isDrifting = car.IsDrifting;
        if (isDrifting)
            _springScale?.Bump(new Vector3(0.5f, -0.8f, 0.5f));
        else
            _springScale?.Bump(new Vector3(0.3f, -0.5f, 0.3f));
        MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0.03f, isDrifting ? 0.07f : 0.05f, false, 0f, false);

        car.AddKillFuel(killFuelAmount);
        car.ApplySpeedPenalty(speedPenalty);

        Vector3 pos = transform.position;

        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, pos, Quaternion.identity);

        if (killSound != null)
            AudioSource.PlayClipAtPoint(killSound, pos);

        SpawnXPOrbs(pos, car.transform);

        if (isDrifting)
        {
            Vector3 lateralDir = car.transform.right * Mathf.Sign(car.LateralSpeed);
            if (Mathf.Abs(car.LateralSpeed) < 0.1f) lateralDir = car.transform.right;
            float lateralSpeed = Mathf.Clamp(Mathf.Abs(car.LateralSpeed), 12f, 28f);
            Launch(lateralDir * lateralSpeed + Vector3.up * 8f);
            DriftComboUI.Instance?.RegisterDriftKill();
        }
        else if (car.IsBoostActive)
        {
            float boostSpeed = Mathf.Clamp(car.CurrentSpeed, 10f, 20f);
            Launch(car.FlatVelocity.normalized * boostSpeed + Vector3.up * 4f);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void UpdateAimPrediction()
    {
        if (_target == null) return;

        Vector3 carVel = _car != null ? _car.FlatVelocity : Vector3.zero;

        // 탄착까지 남은 총 시간: 잔여 탐색 + 락 + 비행
        float remainSearch = Mathf.Max(searchTime - _phaseTimer, 0f);
        float totalLead = remainSearch + lockTime + flightDuration;

        // 원호 예측: 차가 원형 주행 중일 때 회전 반경으로 착탄 위치 보정
        float omega = _carRb != null ? _carRb.angularVelocity.y : 0f;
        if (Mathf.Abs(omega) > 0.3f && carVel.magnitude > 0.1f)
        {
            float speed = carVel.magnitude;
            float R = speed / Mathf.Abs(omega);

            // 회전 중심 방향: omega > 0(좌회전)이면 중심은 오른쪽, omega < 0이면 왼쪽
            float perpAngle = omega > 0f ? 90f : -90f;
            Vector3 perp = Quaternion.Euler(0f, perpAngle, 0f) * carVel.normalized;
            Vector3 center = _target.position + perp * R;

            // 회전 각도만큼 오프셋 벡터를 회전시켜 예측 위치 산출
            // Quaternion.Euler Y 양수 = CW, angularVelocity.y 양수 = CCW → 부호 반전
            float angleDeg = -omega * totalLead * Mathf.Rad2Deg;
            Vector3 offset = _target.position - center;
            _aimTarget = center + Quaternion.Euler(0f, angleDeg, 0f) * offset;
        }
        else
        {
            _aimTarget = _target.position + carVel * totalLead;
        }

        // 산포 (랜덤 오프셋)
        _aimTarget.x += _aimRandomOffset.x;
        _aimTarget.z += _aimRandomOffset.y;
        _aimTarget.y = SampleTerrainHeight(_aimTarget);
    }

    void UpdateIndicator()
    {
        if (indicatorPrefab == null) return;

        if (_indicator == null)
        {
            _indicator = Instantiate(indicatorPrefab, _aimTarget, Quaternion.identity);
            float diameter = blastRadius * 2f;
            _indicator.transform.localScale = new Vector3(diameter, diameter, diameter);
        }

        _indicator.transform.position = _aimTarget + Vector3.up * 0.05f;
    }

    void DestroyIndicator()
    {
        if (_indicator != null)
        {
            Destroy(_indicator);
            _indicator = null;
        }
    }

    void Fire()
    {
        if (projectilePrefab == null || _target == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 1f;

        // 호밍 레이트: 40도/초
        float homingRate = 40f;

        // spreadCount는 홀수여야 중앙탄이 정확히 aimed 방향에 위치
        int effectiveSpreadCount = spreadCount % 2 == 0 ? spreadCount + 1 : spreadCount;
        bool useSpread = _difficulty >= spreadDifficultyThreshold && effectiveSpreadCount > 1;

        if (useSpread)
        {
            // 부채꼴 발사: 중앙(aimed+호밍) + 양쪽(static, 호밍 없음)
            Vector3 dir = (_aimTarget - spawnPos);
            dir.y = 0f;

            float halfAngle = spreadAngle * 0.5f;
            float step = spreadAngle / (effectiveSpreadCount - 1);
            int centerIndex = effectiveSpreadCount / 2; // 홀수 보장 후 계산하므로 항상 정확

            for (int i = 0; i < effectiveSpreadCount; i++)
            {
                float angle = -halfAngle + step * i;
                Vector3 rotatedDir = Quaternion.Euler(0f, angle, 0f) * dir;
                Vector3 targetPos = spawnPos + rotatedDir;
                targetPos.y = SampleTerrainHeight(targetPos);

                GameObject obj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
                var proj = obj.GetComponent<ZombieProjectile>();
                if (proj != null)
                {
                    bool isCenterShot = (i == centerIndex);
                    if (isCenterShot)
                    {
                        // 중앙탄: aimed + 소프트 호밍
                        proj.Init(spawnPos, _aimTarget, projectileDamage * 0.6f, flightDuration, blastRadius, _target, homingRate);
                    }
                    else
                    {
                        // 양쪽탄: static (호밍 없음), 데미지 감소
                        proj.Init(spawnPos, targetPos, projectileDamage * 0.4f, flightDuration, blastRadius * 0.7f);
                    }

                    // indicator는 중앙탄에만
                    if (isCenterShot && _indicator != null)
                    {
                        proj.SetIndicator(_indicator);
                        _indicator = null;
                    }
                }
            }
        }
        else
        {
            // 단발 + 소프트 호밍
            GameObject obj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            var proj = obj.GetComponent<ZombieProjectile>();
            if (proj != null)
            {
                proj.Init(spawnPos, _aimTarget, projectileDamage, flightDuration, blastRadius, _target, homingRate);
                if (_indicator != null)
                {
                    proj.SetIndicator(_indicator);
                    _indicator = null;
                }
            }
        }
    }

    public void LaunchKill()
    {
        if (_dead) return;
        _dead = true;
        if (_laserLine != null) _laserLine.enabled = false;
        _springScale?.Bump(new Vector3(0.3f, -0.5f, 0.3f));
        MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0.03f, 0.05f, false, 0f, false);
        _target?.GetComponent<CarController>()?.AddKillFuel(killFuelAmount);

        Vector3 pos = transform.position;

        if (killParticlePrefab != null)
            Instantiate(killParticlePrefab, pos, Quaternion.identity);

        if (killSound != null)
            AudioSource.PlayClipAtPoint(killSound, pos);

        SpawnXPOrbs(pos, _target);

        Destroy(gameObject);
    }

    void SpawnXPOrbs(Vector3 pos, Transform orbTarget)
    {
        if (xpOrbPrefab == null) return;
        int count = Random.Range(orbCountMin, orbCountMax + 1);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-30f, 30f);
            Vector3 burstDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var obj = Instantiate(xpOrbPrefab, pos, Quaternion.identity);
            var orb = obj.GetComponent<XPOrb>();
            if (orb != null) orb.Init(burstDir, orbTarget);
        }
    }

    public void Launch(Vector3 launchVelocity)
    {
        _dead = true;
        _launched = true;
        _rb.isKinematic = false;
        GetComponent<Collider>().isTrigger = false;
        _rb.linearVelocity = launchVelocity;
        Destroy(gameObject, 3f);
        StartCoroutine(ChainKillRoutine());
    }

    System.Collections.IEnumerator ChainKillRoutine()
    {
        while (_chainKillCount < maxChainKills)
        {
            ZombieController nearest = FindNearestLivingZombie(homingRadius);
            if (nearest != null)
            {
                Vector3 vel = _rb.linearVelocity;
                Vector3 toTarget = (nearest.transform.position - transform.position).normalized;
                Vector3 newDir = Vector3.Lerp(vel.normalized, toTarget, homingStrength * Time.fixedDeltaTime);
                _rb.linearVelocity = newDir * vel.magnitude;
            }

            Collider[] hits = Physics.OverlapSphere(transform.position, chainKillRadius, zombieLayer);
            foreach (var col in hits)
            {
                if (_chainKillCount >= maxChainKills) break;
                if (col == null || col.gameObject == gameObject) continue;
                var other = col.GetComponent<ZombieController>();
                if (other != null && !other._dead && !other._launched)
                {
                    _chainKillCount++;
                    other.LaunchKill();
                }
            }
            yield return new WaitForFixedUpdate();
        }
    }

    ZombieController FindNearestLivingZombie(float radius)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, zombieLayer);
        ZombieController nearest = null;
        float minDist = float.MaxValue;
        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;
            var z = col.GetComponent<ZombieController>();
            if (z == null || z._dead) continue;
            float d = (col.transform.position - transform.position).sqrMagnitude;
            if (d < minDist) { minDist = d; nearest = z; }
        }
        return nearest;
    }
}
