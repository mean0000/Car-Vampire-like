using System.Collections.Generic;
using UnityEngine;

public class PropScatter : MonoBehaviour
{
    [SerializeField] private BiomeSettings[] biomeSettings;  // 인덱스 = BiomeType 순서

    private const int MaxSamplesPerProp = 200;  // 포아송 샘플 최대 개수
    private const int MaxActiveList = 500;

    private readonly List<GameObject> _spawnedProps = new List<GameObject>();

    public void Scatter(Vector2Int coord, float chunkSz, Mesh terrainMesh)
    {
        ClearProps();
        if (chunkSz <= 0f) return;
        var biome = BiomeResolver.GetBiome(coord.x * chunkSz, coord.y * chunkSz);
        int biomeIdx = (int)biome;
        if (biomeSettings == null || biomeIdx < 0 || biomeIdx >= biomeSettings.Length || biomeSettings[biomeIdx] == null) return;

        var settings = biomeSettings[biomeIdx];
        if (settings.props == null || settings.props.Length == 0) return;

        Vector3[] cachedVerts   = terrainMesh != null ? terrainMesh.vertices : null;
        Vector3[] cachedNormals = terrainMesh != null ? terrainMesh.normals  : null;

        float originX = coord.x * chunkSz - chunkSz * 0.5f;
        float originZ = coord.y * chunkSz - chunkSz * 0.5f;

        // 청크 시드 (항상 동일한 결과)
        int seed = HashCoord(coord.x, coord.y);
        var rng = new System.Random(seed);

        foreach (var prop in settings.props)
        {
            if (prop.prefab == null) continue;

            float minSpacing = Mathf.Max(prop.minSpacing, 0.5f);
            var positions = PoissonSample(originX, originZ, chunkSz, minSpacing, HashCoord(coord.x + prop.prefab.GetInstanceID(), coord.y));

            foreach (var pos in positions)
            {
                // 노이즈 밀도 체크
                float densityNoise = Mathf.PerlinNoise(
                    pos.x * settings.densityNoiseScale + settings.densityNoiseOffset.x,
                    pos.y * settings.densityNoiseScale + settings.densityNoiseOffset.y);
                if (densityNoise > prop.density) continue;

                // 높이 샘플링
                float height = SampleTerrainHeight(pos.x, pos.y, cachedVerts, coord, chunkSz);

                // 높이 범위 체크
                if (height < prop.minHeight || height > prop.maxHeight) continue;

                // 경사도 체크 (법선 벡터의 Y성분으로 근사)
                float slope = SampleSlope(pos.x, pos.y, cachedVerts, cachedNormals, coord, chunkSz);
                if (slope < prop.minSlope || slope > prop.maxSlope) continue;

                // 배치
                float scale = Mathf.Lerp(prop.minScale, prop.maxScale, (float)rng.NextDouble());
                var go = Instantiate(prop.prefab, transform);
                _spawnedProps.Add(go);
                go.transform.position = new Vector3(pos.x, height, pos.y);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one * scale;
            }
        }
    }

    public void ClearProps()
    {
        for (int i = _spawnedProps.Count - 1; i >= 0; i--)
            if (_spawnedProps[i] != null) Destroy(_spawnedProps[i]);
        _spawnedProps.Clear();
    }

    private void OnDestroy() => ClearProps();

    private static int HashCoord(int x, int y)
    {
        unchecked { int h = 17; h = h * 31 + x; h = h * 31 + y; return h; }
    }

    // 포아송 디스크 샘플링 (간소화 버전, 최대 200개 제한)
    private List<Vector2> PoissonSample(float originX, float originZ, float size, float minDist, int seed)
    {
        var result = new List<Vector2>();
        if (size <= 0f || minDist <= 0f) return result;
        var active = new List<Vector2>();
        var rng = new System.Random(seed);
        int attempts = 30;

        var first = new Vector2(originX + (float)rng.NextDouble() * size, originZ + (float)rng.NextDouble() * size);
        result.Add(first);
        active.Add(first);

        while (active.Count > 0 && result.Count < MaxSamplesPerProp)
        {
            if (active.Count > MaxActiveList)
                active.RemoveRange(MaxActiveList, active.Count - MaxActiveList);

            int idx = rng.Next(active.Count);
            var point = active[idx];
            bool found = false;

            for (int i = 0; i < attempts; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = minDist * (1f + (float)rng.NextDouble());
                var candidate = new Vector2(point.x + Mathf.Cos(angle) * radius, point.y + Mathf.Sin(angle) * radius);

                if (candidate.x < originX || candidate.x > originX + size ||
                    candidate.y < originZ || candidate.y > originZ + size) continue;

                bool tooClose = false;
                foreach (var existing in result)
                    if (Vector2.Distance(candidate, existing) < minDist) { tooClose = true; break; }

                if (!tooClose) { result.Add(candidate); active.Add(candidate); found = true; break; }
            }

            if (!found) active.RemoveAt(idx);
        }
        return result;
    }

    private float SampleTerrainHeight(float wx, float wz, Vector3[] verts, Vector2Int coord, float chunkSz)
    {
        if (verts == null || verts.Length == 0) return 0f;
        // 메시 버텍스에서 가장 가까운 점의 Y 반환 (근사)
        // BuildMesh 로컬 좌표계: lx0 = wx0 - coord.x * _chunkSize ([-chunkSize/2, +chunkSize/2])
        float lx = wx - coord.x * chunkSz;
        float lz = wz - coord.y * chunkSz;
        float bestDist = float.MaxValue;
        float bestY = 0f;
        foreach (var v in verts)
        {
            float d = (v.x - lx) * (v.x - lx) + (v.z - lz) * (v.z - lz);
            if (d < bestDist) { bestDist = d; bestY = v.y; }
        }
        return bestY;
    }

    private float SampleSlope(float wx, float wz, Vector3[] verts, Vector3[] normals, Vector2Int coord, float chunkSz)
    {
        if (verts == null || verts.Length == 0) return 0f;
        if (normals == null || normals.Length == 0) return 0f;
        float lx = wx - coord.x * chunkSz;
        float lz = wz - coord.y * chunkSz;
        float bestDist = float.MaxValue;
        Vector3 bestNormal = Vector3.up;
        int count = Mathf.Min(verts.Length, normals.Length);
        for (int i = 0; i < count; i++)
        {
            float d = (verts[i].x - lx) * (verts[i].x - lx) + (verts[i].z - lz) * (verts[i].z - lz);
            if (d < bestDist) { bestDist = d; bestNormal = normals[i]; }
        }
        return Vector3.Angle(Vector3.up, bestNormal);
    }
}
