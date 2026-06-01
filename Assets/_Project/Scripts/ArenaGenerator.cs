using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경계 아레나 도심을 런 시작 시 1회 유기적으로 생성한다 — 격자 타일 폐기 버전.
///
/// 격자를 죽이는 3원리:
///  1) 균일 타일 대신 가변 크기 "건물 클러스터"를 최소간격 거부표집(dart-throw)으로 흩뿌림 → 클러스터 사이 빈 공간이 곧 도로(베이크 안 함).
///  2) 건물 자유 회전(0~360°) + 미세 스케일 지터 → 축 정렬 제거.
///  3) 프리팹 풋프린트를 런타임 측정해 Mini~Main을 섞음 → 균일 크기 반복 제거.
///
/// 구조 축(권위 문서 목표 보존):
///  - 원점=집, homeSafeRadius 안은 비움(스폰 없음).
///  - gateDirection 한 방향으로 완만히 굽은 스파인 도로(±spineHalfWidth 비움) → 읽히는 통행로 + 노출 위험.
///  - 스파인 위 gateRadii(200/450/700m)에 광장(빈 공터) + 게이트 트리거 플레이스홀더. 돌파 로직은 범위 밖.
///  - 평지 y=0, 바닥 Ground(6), 건물 Obstacle(8) → 기존 스폰/지면스냅/OverlapsObstacle 호환.
///
/// 1회 생성이므로 ChunkManager식 coord 결정론은 불필요 — 단일 seed가 전체를 순차 구동(같은 seed=같은 도시).
/// </summary>
[DisallowMultipleComponent]
public class ArenaGenerator : MonoBehaviour
{
    [Header("Building pools (가변 풋프린트 — Mini~Main 섞어 넣기)")]
    [SerializeField] GameObject[] buildingPrefabs;
    [SerializeField] Material groundMaterial;

    [Header("Prop pools (빈 공간·도로변 엄폐물 — 기존 curated 재사용)")]
    [SerializeField] GameObject[] heavyProps;    // 차량·대형 잔해
    [SerializeField] GameObject[] debrisProps;   // 드럼통·잔해
    [SerializeField] GameObject[] vegetation;    // 덤불·나무
    [SerializeField] int heavyCount = 60;
    [SerializeField] int debrisCount = 220;
    [SerializeField] int vegetationCount = 180;

    [Header("Arena")]
    [SerializeField] float arenaRadius = 800f;    // 게이트 ③(700m) + 여유
    [SerializeField] float homeSafeRadius = 40f;  // 집 안전지대 — 건물·프롭 제외
    [SerializeField] int seed = 12345;            // 그레이박스 고정. 런마다 무작위화하려면 randomizeSeed
    [SerializeField] bool randomizeSeed = false;

    [Header("Spine road (곡선 통행로)")]
    [SerializeField] Vector3 gateDirection = new Vector3(0f, 0f, 1f);
    [SerializeField] float spineHalfWidth = 14f;   // 건물 비우는 회랑 반폭
    [SerializeField] float spineAmplitude = 70f;   // 좌우로 굽는 진폭(격자 직선감 제거)
    [SerializeField] float spineWavelength = 520f; // 한 굽이 길이
    [SerializeField] float[] gateRadii = { 200f, 450f, 700f };
    [SerializeField] float gatePlazaRadius = 32f;  // 게이트 광장(건물 비움)

    [Header("Clusters")]
    [SerializeField] int maxClusters = 160;
    [SerializeField] float clusterMinSpacing = 48f; // 클러스터 중심 최소 간격 → 도로 폭 확보
    [SerializeField] float clusterSpread = 11f;      // 클러스터 내 건물 퍼짐 반경
    [SerializeField] int clusterMinBuildings = 2;
    [SerializeField] int clusterMaxBuildings = 5;
    [SerializeField] float buildingGap = 2.5f;       // 건물 풋프린트 사이 최소 틈
    [SerializeField] float scaleJitter = 0.12f;      // ±12% 미세 스케일

    const int ObstacleLayer = 8;
    const int GroundLayer = 6;

    readonly List<Vector3> _spine = new List<Vector3>();           // 스파인 폴리라인 표본(거리 계산용)
    readonly List<Vector2> _gateXZ = new List<Vector2>();          // 게이트 광장 중심 xz
    readonly List<(Vector2 pos, float radius)> _placed = new List<(Vector2, float)>(); // 배치된 건물 풋프린트
    readonly Dictionary<GameObject, float> _footprint = new Dictionary<GameObject, float>();

    void Start()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        ClearExisting();
        var root = new GameObject("Arena");
        root.transform.SetParent(transform, false);

        int s = randomizeSeed ? (int)System.DateTime.Now.Ticks : seed;
        var rng = new System.Random(s);

        BuildSpine();
        BuildGates();
        _placed.Clear();

        BuildGround(root.transform);
        ScatterClusters(root.transform, rng);
        ScatterProps(root.transform, heavyProps,  heavyCount,      rng, 0.9f, 1.15f);
        ScatterProps(root.transform, debrisProps, debrisCount,     rng, 0.8f, 1.3f);
        ScatterProps(root.transform, vegetation,  vegetationCount, rng, 0.85f, 1.4f);
        PlaceGateMarkers(root.transform);
    }

    void ClearExisting()
    {
        var existing = transform.Find("Arena");
        if (existing == null) return;
        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    // ── 스파인/게이트 기하 ─────────────────────────────────────────────
    void BuildSpine()
    {
        _spine.Clear();
        Vector3 dir = gateDirection.sqrMagnitude > 1e-4f ? gateDirection.normalized : Vector3.forward;
        Vector3 perp = new Vector3(dir.z, 0f, -dir.x);
        float step = 10f;
        for (float arc = 0f; arc <= arenaRadius; arc += step)
        {
            float lateral = spineAmplitude * Mathf.Sin(arc / spineWavelength * 2f * Mathf.PI);
            Vector3 p = dir * arc + perp * lateral;
            _spine.Add(new Vector3(p.x, 0f, p.z));
        }
    }

    void BuildGates()
    {
        _gateXZ.Clear();
        Vector3 dir = gateDirection.sqrMagnitude > 1e-4f ? gateDirection.normalized : Vector3.forward;
        Vector3 perp = new Vector3(dir.z, 0f, -dir.x);
        foreach (float r in gateRadii)
        {
            float lateral = spineAmplitude * Mathf.Sin(r / spineWavelength * 2f * Mathf.PI);
            Vector3 p = dir * r + perp * lateral;
            _gateXZ.Add(new Vector2(p.x, p.z));
        }
    }

    float DistanceToSpine(Vector2 xz)
    {
        float best = float.MaxValue;
        for (int i = 0; i < _spine.Count; i++)
        {
            float dx = xz.x - _spine[i].x, dz = xz.y - _spine[i].z;
            float d = dx * dx + dz * dz;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    float DistanceToNearestGate(Vector2 xz)
    {
        float best = float.MaxValue;
        for (int i = 0; i < _gateXZ.Count; i++)
        {
            float d = (xz - _gateXZ[i]).magnitude;
            if (d < best) best = d;
        }
        return best;
    }

    // ── 바닥 ──────────────────────────────────────────────────────────
    void BuildGround(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Ground";
        go.transform.SetParent(parent, false);
        go.transform.position = Vector3.zero;
        // Plane = 10×10 units at scale 1 → 아레나 지름을 덮도록 스케일.
        float s = (arenaRadius * 2.2f) / 10f;
        go.transform.localScale = new Vector3(s, 1f, s);
        go.layer = GroundLayer;
        if (groundMaterial != null)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = groundMaterial;
        }
    }

    // ── 클러스터(건물) ────────────────────────────────────────────────
    void ScatterClusters(Transform parent, System.Random rng)
    {
        if (buildingPrefabs == null || buildingPrefabs.Length == 0)
        {
            Debug.LogWarning("[ArenaGenerator] buildingPrefabs가 비어 있음 — 건물 없이 생성됩니다. Inspector에 빌딩 프리팹을 넣어주세요.", this);
            return;
        }
        var centers = new List<Vector2>();
        int attempts = maxClusters * 30;
        for (int a = 0; a < attempts && centers.Count < maxClusters; a++)
        {
            Vector2 c = RandomInDisk(rng, arenaRadius * 0.97f);
            if (c.magnitude < homeSafeRadius + clusterSpread) continue;
            if (DistanceToSpine(c) < spineHalfWidth + clusterSpread) continue;
            if (DistanceToNearestGate(c) < gatePlazaRadius + clusterSpread) continue;

            bool tooClose = false;
            for (int i = 0; i < centers.Count; i++)
                if ((centers[i] - c).sqrMagnitude < clusterMinSpacing * clusterMinSpacing) { tooClose = true; break; }
            if (tooClose) continue;

            centers.Add(c);
        }

        foreach (var center in centers)
        {
            int n = rng.Next(clusterMinBuildings, clusterMaxBuildings + 1);
            for (int b = 0; b < n; b++)
                TryPlaceBuilding(parent, center, rng);
        }
    }

    void TryPlaceBuilding(Transform parent, Vector2 center, System.Random rng)
    {
        if (buildingPrefabs == null || buildingPrefabs.Length == 0) return;
        for (int t = 0; t < 12; t++)
        {
            var prefab = buildingPrefabs[rng.Next(buildingPrefabs.Length)];
            if (prefab == null) continue;
            // 스케일을 검사 전에 뽑아 실제 풋프린트(fr*sc)로 거리·겹침을 판정 → 통행로/광장/집 침범·건물 겹침 방지.
            float sc = 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleJitter;
            float fr = Footprint(prefab) * sc;

            Vector2 offset = RandomInDisk(rng, clusterSpread);
            Vector2 xz = center + offset;

            float dOrigin = xz.magnitude;
            if (dOrigin < homeSafeRadius + fr) continue;
            if (dOrigin > arenaRadius - fr) continue;
            if (DistanceToSpine(xz) < spineHalfWidth + fr) continue;
            if (DistanceToNearestGate(xz) < gatePlazaRadius + fr) continue;

            bool overlap = false;
            for (int i = 0; i < _placed.Count; i++)
            {
                float minD = _placed[i].radius + fr + buildingGap;
                if ((_placed[i].pos - xz).sqrMagnitude < minD * minD) { overlap = true; break; }
            }
            if (overlap) continue;

            float yaw = (float)(rng.NextDouble() * 360.0);
            var go = Instantiate(prefab, new Vector3(xz.x, 0f, xz.y), Quaternion.Euler(0f, yaw, 0f), parent);
            go.transform.localScale *= sc;
            SetLayerRecursive(go.transform, ObstacleLayer);
            _placed.Add((xz, fr));
            return;
        }
    }

    // ── 프롭(엄폐물) ──────────────────────────────────────────────────
    void ScatterProps(Transform parent, GameObject[] pool, int count, System.Random rng, float sMin, float sMax)
    {
        if (pool == null || count <= 0) return;
        var valid = new List<GameObject>();
        foreach (var p in pool) if (p != null) valid.Add(p);
        if (valid.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var prefab = valid[rng.Next(valid.Count)];
            float yaw = (float)(rng.NextDouble() * 360.0);
            float scale = Mathf.Lerp(sMin, sMax, (float)rng.NextDouble());

            bool placed = false;
            Vector2 chosen = default;
            for (int t = 0; t < 14; t++)
            {
                Vector2 xz = RandomInDisk(rng, arenaRadius * 0.97f);
                if (placed) continue;                                   // RNG 고정 예산 — 성공해도 남은 추첨 소비
                if (xz.magnitude < homeSafeRadius) continue;
                if (DistanceToSpine(xz) < spineHalfWidth * 0.45f) continue;  // 도로 한복판만 비움(도로변 엄폐 허용)
                if (InsideAnyBuilding(xz, 1.2f)) continue;
                chosen = xz; placed = true;
            }
            if (!placed) continue;

            var go = Instantiate(prefab, new Vector3(chosen.x, 0f, chosen.y), Quaternion.Euler(0f, yaw, 0f), parent);
            go.transform.localScale *= scale;
        }
    }

    bool InsideAnyBuilding(Vector2 xz, float clear)
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            float minD = _placed[i].radius + clear;
            if ((_placed[i].pos - xz).sqrMagnitude < minD * minD) return true;
        }
        return false;
    }

    // ── 게이트 플레이스홀더 ───────────────────────────────────────────
    void PlaceGateMarkers(Transform parent)
    {
        for (int i = 0; i < _gateXZ.Count; i++)
        {
            var go = new GameObject($"GateSlot_{i + 1}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(_gateXZ[i].x, 0f, _gateXZ[i].y);
        }
        var home = new GameObject("HomeSlot");
        home.transform.SetParent(parent, false);
        home.transform.position = Vector3.zero;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────
    float Footprint(GameObject prefab)
    {
        if (_footprint.TryGetValue(prefab, out float r)) return r;
        var temp = Instantiate(prefab, new Vector3(0f, -5000f, 0f), Quaternion.identity);
        var rends = temp.GetComponentsInChildren<Renderer>(true);
        float radius = 4f;
        if (rends.Length > 0)
        {
            Bounds bb = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bb.Encapsulate(rends[i].bounds);
            radius = Mathf.Max(bb.extents.x, bb.extents.z);
        }
        else Debug.LogWarning($"[ArenaGenerator] '{prefab.name}'에 Renderer 없음 → 풋프린트 fallback {radius}m 사용. 간격이 부정확할 수 있음.", this);
        if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
        _footprint[prefab] = radius;
        return radius;
    }

    static Vector2 RandomInDisk(System.Random rng, float radius)
    {
        float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
        float a = (float)(rng.NextDouble() * 2.0 * System.Math.PI);
        return new Vector2(r * Mathf.Cos(a), r * Mathf.Sin(a));
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        if (_spine.Count > 1)
            for (int i = 1; i < _spine.Count; i++) Gizmos.DrawLine(_spine[i - 1], _spine[i]);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
        for (int i = 0; i < _gateXZ.Count; i++)
            Gizmos.DrawWireSphere(new Vector3(_gateXZ[i].x, 0f, _gateXZ[i].y), gatePlazaRadius);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
        Gizmos.DrawWireSphere(Vector3.zero, homeSafeRadius);
    }
#endif
}
