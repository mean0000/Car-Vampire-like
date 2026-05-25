using System.Collections.Generic;
using DistantLands.Cozy;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [SerializeField] GameObject zombiePrefab;
    [SerializeField] GameObject chargerPrefab;
    [SerializeField] GameObject laserPrefab;
    [SerializeField] Transform carTransform;
    [SerializeField] float spawnInterval = 1.5f;
    [SerializeField] float minSpawnInterval = 0.3f;
    [SerializeField] float minSpawnRadius = 15f;
    [SerializeField] float maxSpawnRadius = 25f;
    [SerializeField] float despawnDistance = 100f;
    [SerializeField] LayerMask groundLayer = -1;
    [SerializeField, Range(0f, 1f)] float forwardBias = 0.6f;
    [SerializeField, Range(0f, 1f)] float frontSpawnChance = 0.45f;

    [Header("Horde")]
    [SerializeField] float hordeInterval = 15f;
    [SerializeField] float hordeSpread = 4f;

    [Header("Front Density")]
    [SerializeField] float frontCheckInterval = 3f;
    [SerializeField] float frontCheckDistance = 80f;
    [SerializeField] float frontCheckAngle = 50f;
    [SerializeField] int frontHordeThreshold = 5;

    [Header("Day/Night")]
    [SerializeField] float nightSpawnRateMultiplier = 0.7f;
    [SerializeField] float nightSpeedBonus = 1.5f;
    [SerializeField] float cozyTimeSpeed = 1f;

    [Header("Difficulty")]
    [SerializeField] float maxDifficultyTime = 600f;
    [SerializeField] float maxDifficultyDist = 5000f;
    [SerializeField] int minMaxZombies = 50;
    [SerializeField] int maxMaxZombies = 120;
    [SerializeField] int minHordeSize = 12;
    [SerializeField] int maxHordeSize = 25;
    [SerializeField] float surgeDistInterval = 500f;
    [SerializeField] float surgeTimeInterval = 60f;
    [SerializeField] float surgeCooldown = 3f;

    float _timer;
    float _hordeTimer;
    float _frontCheckTimer;
    float _elapsedTime;
    float _totalDistance;
    float _nextSurgeDist;
    float _nextSurgeTime;
    float _surgeCooldownTimer;
    Vector3 _prevCarPos;
    CarController _car;
    List<ZombieController> _activeZombies = new List<ZombieController>();
    CozyWeather _cozy;
    CozyTimeModule _timeModule;
    bool _isNight;
    float _prevCozyTimeSpeed;

    void Start()
    {
        if (carTransform == null)
        {
            var found = FindFirstObjectByType<CarController>();
            if (found != null) carTransform = found.transform;
        }
        if (carTransform != null)
        {
            _car = carTransform.GetComponent<CarController>();
            _prevCarPos = carTransform.position;
        }

        _nextSurgeDist = surgeDistInterval;
        _nextSurgeTime = surgeTimeInterval;
        _cozy = CozyWeather.instance;
        if (_cozy != null)
        {
            _timeModule = _cozy.timeModule;
            if (_timeModule?.perennialProfile != null)
            {
                _timeModule.perennialProfile.timeMovementSpeed = cozyTimeSpeed;
                _prevCozyTimeSpeed = cozyTimeSpeed;
            }
        }
        else
        {
            Debug.LogWarning("[ZombieSpawner] CozyWeather not found — day/night disabled.");
        }
    }

    void Update()
    {
        if (carTransform == null) return;

        if (_timeModule != null)
        {
            // Inspector에서 값 변경 시에만 적용
            if (!Mathf.Approximately(_prevCozyTimeSpeed, cozyTimeSpeed) && _timeModule.perennialProfile != null)
            {
                _timeModule.perennialProfile.timeMovementSpeed = cozyTimeSpeed;
                _prevCozyTimeSpeed = cozyTimeSpeed;
            }
            float t = _timeModule.currentTime;
            _isNight = t < new MeridiemTime(5, 30) || t >= new MeridiemTime(21, 0);
        }

        _elapsedTime += Time.deltaTime;

        Vector3 carPos = carTransform.position;
        _totalDistance += Vector3.Distance(carPos, _prevCarPos);
        _prevCarPos = carPos;

        float timeFactor = Mathf.Clamp01(_elapsedTime / maxDifficultyTime);
        float distFactor = Mathf.Clamp01(_totalDistance / maxDifficultyDist);
        float difficulty = Mathf.Max(timeFactor, distFactor);

        int maxZombies = Mathf.RoundToInt(Mathf.Lerp(minMaxZombies, maxMaxZombies, difficulty));
        int hordeSize  = Mathf.RoundToInt(Mathf.Lerp(minHordeSize,  maxHordeSize,  difficulty));

        // 거리 초과 좀비 삭제 + 전방 활성 수 카운트
        int frontCount = 0;
        Vector3 carFwd = GetCarForward();

        for (int i = _activeZombies.Count - 1; i >= 0; i--)
        {
            if (_activeZombies[i] == null) { _activeZombies.RemoveAt(i); continue; }

            ZombieController z = _activeZombies[i];
            float dist = Vector3.Distance(z.transform.position, carPos);
            if (dist > despawnDistance)
            {
                Destroy(z.gameObject);
                _activeZombies.RemoveAt(i);
                continue;
            }

            if (dist <= frontCheckDistance)
            {
                Vector3 toZombie = z.transform.position - carPos;
                toZombie.y = 0f;
                if (toZombie.sqrMagnitude > 0.001f &&
                    Vector3.Angle(carFwd, toZombie.normalized) <= frontCheckAngle)
                    frontCount++;
            }
        }

        int activeCount = _activeZombies.Count;

        float dynamicInterval = Mathf.Lerp(spawnInterval, minSpawnInterval, difficulty);
        if (_isNight) dynamicInterval *= nightSpawnRateMultiplier;

        float speedBonus = _isNight ? nightSpeedBonus : 0f;

        _timer += Time.deltaTime;
        if (_timer >= dynamicInterval && activeCount < maxZombies)
        {
            _timer = 0f;
            SpawnSingle(difficulty, speedBonus);
        }

        bool hordeSpawnedThisFrame = false;

        _hordeTimer += Time.deltaTime;
        if (_hordeTimer >= hordeInterval)
        {
            _hordeTimer = 0f;
            hordeSpawnedThisFrame = SpawnHorde(activeCount, hordeSize, difficulty, SpawnAnimation.RiseFromGround, speedBonus);
        }

        _frontCheckTimer += Time.deltaTime;
        if (_frontCheckTimer >= frontCheckInterval)
        {
            _frontCheckTimer = 0f;
            if (frontCount < frontHordeThreshold && !hordeSpawnedThisFrame)
                hordeSpawnedThisFrame = SpawnHorde(activeCount, hordeSize, difficulty, SpawnAnimation.RiseFromGround, speedBonus);
        }

        // 서지 이벤트 (거리 또는 시간 트리거)
        _surgeCooldownTimer -= Time.deltaTime;
        if (_surgeCooldownTimer <= 0f && !hordeSpawnedThisFrame)
        {
            bool surgeTriggered = _totalDistance >= _nextSurgeDist || _elapsedTime >= _nextSurgeTime;
            if (surgeTriggered)
            {
                int surgeSize = Mathf.RoundToInt(hordeSize * 1.5f);
                if (SpawnHorde(_activeZombies.Count, surgeSize, difficulty, SpawnAnimation.RushFromSide, speedBonus))
                {
                    _nextSurgeDist = _totalDistance + surgeDistInterval;
                    _nextSurgeTime = _elapsedTime + surgeTimeInterval;
                    _surgeCooldownTimer = surgeCooldown;
                }
            }
        }
    }

    ZombieType RollZombieType(float difficulty)
    {
        if (difficulty < 0.2f) return ZombieType.Ranged;

        float r = Random.value;
        if (difficulty < 0.5f)
        {
            return r < 0.70f ? ZombieType.Ranged : ZombieType.Charger;
        }
        if (r < 0.50f) return ZombieType.Ranged;
        if (r < 0.80f) return ZombieType.Charger;
        return ZombieType.Laser;
    }

    GameObject GetPrefabForType(ZombieType type)
    {
        switch (type)
        {
            case ZombieType.Charger: return chargerPrefab != null ? chargerPrefab : zombiePrefab;
            case ZombieType.Laser:   return laserPrefab != null ? laserPrefab : zombiePrefab;
            default:                 return zombiePrefab;
        }
    }

    void SpawnSingle(float difficulty, float speedBonus = 0f)
    {
        if (zombiePrefab == null)
        {
            Debug.LogWarning("[ZombieSpawner] zombiePrefab이 할당되지 않았습니다.");
            return;
        }

        Vector3 center = FindSpawnCenter();
        if (center == Vector3.zero) return;

        ZombieType type = RollZombieType(difficulty);
        GameObject prefab = GetPrefabForType(type);
        GameObject obj = Instantiate(prefab, center, Quaternion.identity);
        ZombieController zombie = obj.GetComponent<ZombieController>();
        if (zombie != null)
        {
            zombie.Init(carTransform, difficulty, speedBonus);
            zombie.SetZombieType(type);
            zombie.PlaySpawnAnimation(SpawnAnimation.RiseFromGround);
            _activeZombies.Add(zombie);
        }
    }

    bool SpawnHorde(int currentActive, int hordeSize, float difficulty, SpawnAnimation anim, float speedBonus = 0f)
    {
        if (zombiePrefab == null || carTransform == null) return false;

        Vector3 center = GetFrontEdgePosition();
        if (center == Vector3.zero) return false;

        int maxZombies = Mathf.RoundToInt(Mathf.Lerp(minMaxZombies, maxMaxZombies, difficulty));
        int spawnCount = Mathf.Min(hordeSize, maxZombies - currentActive);
        if (spawnCount <= 0) return false;

        float hordeSpreadVal = hordeSpread;

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * hordeSpreadVal;
            Vector3 targetPos = center + new Vector3(offset.x, 0f, offset.y);
            targetPos.y = SampleTerrainHeight(targetPos);

            Vector3 spawnPos = targetPos;
            if (anim == SpawnAnimation.RushFromSide)
            {
                // 측면 8-12m 오프셋에서 시작
                Vector3 lateral = carTransform.right * (Random.value > 0.5f ? 1f : -1f);
                float sideDist = Random.Range(8f, 12f);
                spawnPos = targetPos + lateral * sideDist;
                spawnPos.y = SampleTerrainHeight(spawnPos);
            }

            ZombieType type = RollZombieType(difficulty);
            GameObject prefab = GetPrefabForType(type);
            GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
            ZombieController zombie = obj.GetComponent<ZombieController>();
            if (zombie != null)
            {
                zombie.Init(carTransform, difficulty, speedBonus);
                zombie.SetZombieType(type);
                zombie.PlaySpawnAnimation(anim, targetPos, anim == SpawnAnimation.RushFromSide);
                _activeZombies.Add(zombie);
            }
        }
        return true;
    }

    Vector3 FindSpawnCenter()
    {
        if (Random.value < frontSpawnChance)
        {
            Vector3 frontPos = GetFrontEdgePosition();
            if (frontPos != Vector3.zero) return frontPos;
        }

        Vector3 flatVel = _car != null ? _car.FlatVelocity : Vector3.zero;
        Vector2 biasedDir;
        if (flatVel.sqrMagnitude > 0.5f)
        {
            Vector2 forward2D = new Vector2(flatVel.x, flatVel.z).normalized;
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            biasedDir = Vector2.Lerp(randomDir, forward2D, forwardBias).normalized;
        }
        else
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            biasedDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        Camera cam = Camera.main;
        float dist = Random.Range(minSpawnRadius, maxSpawnRadius);

        for (int i = 0; i < 10; i++)
        {
            Vector2 dir = i == 0 ? biasedDir : Random.insideUnitCircle.normalized;
            Vector3 candidate = carTransform.position + new Vector3(dir.x * dist, 0f, dir.y * dist);
            candidate.y = SampleTerrainHeight(candidate);

            if (cam == null || IsOutsideViewport(cam, candidate))
                return candidate;
        }

        return Vector3.zero;
    }

    Vector3 GetFrontEdgePosition()
    {
        if (_car == null) return Vector3.zero;

        float speedRatio = _car.MaxSpeed > 0f
            ? Mathf.Clamp01(_car.CurrentSpeed / _car.MaxSpeed) : 0f;

        Vector3 flatVel = _car.FlatVelocity;
        Vector3 dir;
        if (flatVel.sqrMagnitude > 0.1f)
            dir = new Vector3(flatVel.x, 0f, flatVel.z).normalized;
        else
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        float lookAheadTime = Mathf.Lerp(2f, 6f, speedRatio);
        float predictDist = Mathf.Max(flatVel.magnitude * lookAheadTime, minSpawnRadius);

        Vector3 predicted = carTransform.position + dir * predictDist;
        Vector2 spread = Random.insideUnitCircle * 6f;
        predicted += new Vector3(spread.x, 0f, spread.y);
        predicted.y = SampleTerrainHeight(predicted);

        return predicted;
    }

    Vector3 GetCarForward()
    {
        if (_car != null && _car.FlatVelocity.sqrMagnitude > 0.1f)
            return _car.FlatVelocity.normalized;
        Vector3 fwd = carTransform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
    }

    /// <summary>월드 좌표에서 지형 높이를 Raycast로 샘플링. 실패 시 차량 높이 폴백.</summary>
    float SampleTerrainHeight(Vector3 worldPos)
    {
        Vector3 origin = new Vector3(worldPos.x, 200f, worldPos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f,
                            groundLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return carTransform != null ? carTransform.position.y : 0f;
    }

    bool IsOutsideViewport(Camera cam, Vector3 worldPos)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return true;
        return vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;
    }
}
