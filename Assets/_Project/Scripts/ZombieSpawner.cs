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
    [SerializeField] float backfillInterval = 0.3f;   // ★ 최소인구 미달 시 빠른 보충 간격

    [Header("Spawn Radius")]
    [SerializeField] float minSpawnRadius = 20f;
    [SerializeField] float maxSpawnRadius = 35f;
    [SerializeField] float despawnDistance = 60f;

    [Header("Signal Zombie")]
    [SerializeField, Range(0f, 1f)] float signalSpawnChance = 0.08f;

    [Header("Ground")]
    [SerializeField] LayerMask groundLayer = -1;

    [Header("Obstacle Avoidance")]
    [SerializeField] LayerMask obstacleMask = 1 << 8;   // Obstacle(8) — 건물 안에 스폰 금지
    [SerializeField] float obstacleClearance = 1.2f;    // 후보 지점에서 이 반경 안에 건물이 있으면 기각

    List<ZombieController> _activeZombies = new List<ZombieController>();
    float _timer;
    float _backfillTimer;   // ★ 최소인구 보충용 별도 타이머

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

        // ★ 최소 인구 보충 — 프레임당 무제한이 아니라 backfillInterval로 레이트 제한
        //   (기존엔 0→15까지 매 프레임 SpawnOne 호출되어 시작 시 버스트)
        if (_activeZombies.Count < minZombies && _activeZombies.Count < maxZombies)
        {
            _backfillTimer += Time.deltaTime;
            if (_backfillTimer >= backfillInterval)
            {
                _backfillTimer = 0f;
                SpawnOne();
            }
        }
        else
        {
            _backfillTimer = 0f;
        }
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

            // 화면 밖 + 건물 안이 아닐 때만 채택. 둘 중 하나라도 실패하면 다음 시도로.
            bool offscreen = cam == null || IsOutsideViewport(cam, candidate);
            if (offscreen && !OverlapsObstacle(candidate))
                return candidate;
        }
        return Vector3.zero;
    }

    // 도심 그리드 도입 후: 후보 지점이 건물 footprint 안이면 좀비가 벽에 끼므로 기각한다.
    bool OverlapsObstacle(Vector3 pos)
    {
        return Physics.CheckSphere(pos + Vector3.up * 0.5f, obstacleClearance,
                                   obstacleMask, QueryTriggerInteraction.Ignore);
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
