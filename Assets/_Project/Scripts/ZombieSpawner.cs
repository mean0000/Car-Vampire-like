using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [SerializeField] GameObject zombiePrefab;
    [SerializeField] Transform carTransform;
    [SerializeField] float spawnInterval = 1.5f;
    [SerializeField] float minSpawnInterval = 0.3f;
    [SerializeField] float minSpawnRadius = 15f;
    [SerializeField] float maxSpawnRadius = 25f;
    [SerializeField] int maxZombies = 50;
    [SerializeField] float despawnDistance = 100f;
    [SerializeField, Range(0f, 1f)] float forwardBias = 0.6f;
    [SerializeField, Range(0f, 1f)] float frontSpawnChance = 0.45f;

    [Header("Horde")]
    [SerializeField] float hordeInterval = 15f;
    [SerializeField] int hordeSize = 12;
    [SerializeField] float hordeSpread = 4f;

    [Header("Front Density")]
    [SerializeField] float frontCheckInterval = 3f;   // 전방 밀도 체크 주기 (초)
    [SerializeField] float frontCheckDistance = 80f;  // 전방으로 체크할 거리
    [SerializeField] float frontCheckAngle = 50f;     // 전방 판정 반각 (°) — 좌우 각각
    [SerializeField] int frontHordeThreshold = 5;     // 이 수 미만이면 무리 소환

    float _timer;
    float _hordeTimer;
    float _frontCheckTimer;
    CarController _car;

    void Start()
    {
        if (carTransform == null)
        {
            var found = FindFirstObjectByType<CarController>();
            if (found != null) carTransform = found.transform;
        }
        if (carTransform != null)
            _car = carTransform.GetComponent<CarController>();
    }

    void Update()
    {
        if (carTransform == null) return;

        // 거리 초과 좀비 삭제 + 전체/전방 활성 수 카운트 (루프 1회)
        ZombieController[] zombies = FindObjectsByType<ZombieController>(FindObjectsSortMode.None);
        int activeCount = 0;
        int frontCount = 0;
        Vector3 carFwd = GetCarForward();

        foreach (var z in zombies)
        {
            float dist = Vector3.Distance(z.transform.position, carTransform.position);
            if (dist > despawnDistance)
            {
                Destroy(z.gameObject);
                continue;
            }
            activeCount++;

            // 전방 범위 내 좀비 카운트
            if (dist <= frontCheckDistance)
            {
                Vector3 toZombie = z.transform.position - carTransform.position;
                toZombie.y = 0f;
                if (toZombie.sqrMagnitude > 0.001f &&
                    Vector3.Angle(carFwd, toZombie.normalized) <= frontCheckAngle)
                    frontCount++;
            }
        }

        // 개별 스폰 (속도에 비례해 간격 단축)
        float speedRatio = (_car != null && _car.MaxSpeed > 0f)
            ? Mathf.Clamp01(_car.CurrentSpeed / _car.MaxSpeed) : 0f;
        float dynamicInterval = Mathf.Lerp(spawnInterval, minSpawnInterval, speedRatio);

        _timer += Time.deltaTime;
        if (_timer >= dynamicInterval && activeCount < maxZombies)
        {
            _timer = 0f;
            SpawnSingle();
        }

        // 주기적 군중 스폰
        _hordeTimer += Time.deltaTime;
        if (_hordeTimer >= hordeInterval)
        {
            _hordeTimer = 0f;
            SpawnHorde(activeCount);
        }

        // 전방 밀도 부족 시 군중 소환
        _frontCheckTimer += Time.deltaTime;
        if (_frontCheckTimer >= frontCheckInterval)
        {
            _frontCheckTimer = 0f;
            if (frontCount < frontHordeThreshold)
                SpawnHorde(activeCount);
        }
    }

    void SpawnSingle()
    {
        if (zombiePrefab == null)
        {
            Debug.LogWarning("[ZombieSpawner] zombiePrefab이 할당되지 않았습니다.");
            return;
        }

        Vector3 center = FindSpawnCenter();
        if (center == Vector3.zero) return;

        GameObject obj = Instantiate(zombiePrefab, center, Quaternion.identity);
        ZombieController zombie = obj.GetComponent<ZombieController>();
        if (zombie != null) zombie.Init(carTransform);
    }

    void SpawnHorde(int currentActive)
    {
        if (zombiePrefab == null || carTransform == null) return;

        Vector3 center = GetFrontEdgePosition(); // 군중은 항상 전방에 소환
        if (center == Vector3.zero) return;

        int spawnCount = Mathf.Min(hordeSize, maxZombies - currentActive);
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * hordeSpread;
            Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);
            pos.y = carTransform.position.y;

            GameObject obj = Instantiate(zombiePrefab, pos, Quaternion.identity);
            ZombieController zombie = obj.GetComponent<ZombieController>();
            if (zombie != null) zombie.Init(carTransform);
        }
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
            candidate.y = carTransform.position.y;

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
        predicted.y = carTransform.position.y;

        return predicted;
    }

    // 차량 이동 방향 (정지 시 transform.forward 사용)
    Vector3 GetCarForward()
    {
        if (_car != null && _car.FlatVelocity.sqrMagnitude > 0.1f)
            return _car.FlatVelocity.normalized;
        Vector3 fwd = carTransform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
    }

    bool IsOutsideViewport(Camera cam, Vector3 worldPos)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z < 0f) return true;
        return vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;
    }
}
