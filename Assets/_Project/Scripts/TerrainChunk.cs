using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
    [SerializeField, Min(1)] private int resolution = 16;
    [SerializeField] private MapNoiseSettings noiseSettings;
    [SerializeField] private PropScatter propScatter;

    private float _chunkSize;
    private Mesh _mesh;

    public void Init(Vector2Int coord, float chunkSize)
    {
        if (noiseSettings == null) Debug.LogWarning("[TerrainChunk] noiseSettings is not assigned — terrain will be flat.", this);
        _chunkSize = chunkSize;
        transform.position = new Vector3(coord.x * _chunkSize, 0f, coord.y * _chunkSize);

        BuildMesh(coord);
        DetermineZone(coord);
        if (propScatter != null)
            propScatter.Scatter(coord, _chunkSize, _mesh);
    }

    private void BuildMesh(Vector2Int coord)
    {
        // 진짜 플랫 쉐이딩: 삼각형마다 버텍스 6개 (공유 없음)
        int triCount = resolution * resolution * 2;
        int vertCount = triCount * 3;

        Vector3[] verts = new Vector3[vertCount];
        int[] tris = new int[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        int safeResolution = Mathf.Max(1, resolution);
        float step = _chunkSize / safeResolution;
        float originX = coord.x * _chunkSize - _chunkSize * 0.5f;
        float originZ = coord.y * _chunkSize - _chunkSize * 0.5f;

        int v = 0;
        for (int z = 0; z < safeResolution; z++)
        {
            for (int x = 0; x < safeResolution; x++)
            {
                float wx0 = originX + x * step;
                float wz0 = originZ + z * step;
                float wx1 = wx0 + step;
                float wz1 = wz0 + step;

                float h00 = SampleHeight(wx0, wz0);
                float h10 = SampleHeight(wx1, wz0);
                float h01 = SampleHeight(wx0, wz1);
                float h11 = SampleHeight(wx1, wz1);

                float lx0 = wx0 - coord.x * _chunkSize;
                float lx1 = wx1 - coord.x * _chunkSize;
                float lz0 = wz0 - coord.y * _chunkSize;
                float lz1 = wz1 - coord.y * _chunkSize;

                // 삼각형 A: (0,0) (0,1) (1,0)
                verts[v]     = new Vector3(lx0, h00, lz0);
                verts[v + 1] = new Vector3(lx0, h01, lz1);
                verts[v + 2] = new Vector3(lx1, h10, lz0);
                uvs[v]     = new Vector2((float)x / resolution,       (float)z / resolution);
                uvs[v + 1] = new Vector2((float)x / resolution,       (float)(z + 1) / resolution);
                uvs[v + 2] = new Vector2((float)(x + 1) / resolution, (float)z / resolution);
                tris[v] = v; tris[v + 1] = v + 1; tris[v + 2] = v + 2;
                v += 3;

                // 삼각형 B: (1,0) (0,1) (1,1)
                verts[v]     = new Vector3(lx1, h10, lz0);
                verts[v + 1] = new Vector3(lx0, h01, lz1);
                verts[v + 2] = new Vector3(lx1, h11, lz1);
                uvs[v]     = new Vector2((float)(x + 1) / resolution, (float)z / resolution);
                uvs[v + 1] = new Vector2((float)x / resolution,       (float)(z + 1) / resolution);
                uvs[v + 2] = new Vector2((float)(x + 1) / resolution, (float)(z + 1) / resolution);
                tris[v] = v; tris[v + 1] = v + 1; tris[v + 2] = v + 2;
                v += 3;
            }
        }

        if (_mesh != null) Destroy(_mesh);
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var mesh = _mesh;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void OnDestroy()
    {
        if (propScatter != null) propScatter.ClearProps();
        if (_mesh != null)
        {
            var col = GetComponent<MeshCollider>();
            if (col != null) col.sharedMesh = null;
            var flt = GetComponent<MeshFilter>();
            if (flt != null) flt.sharedMesh = null;
            Destroy(_mesh);
        }
    }

    private float SampleHeight(float wx, float wz)
    {
        if (noiseSettings == null || noiseSettings.octaves == null || noiseSettings.octaves.Length == 0)
            return 0f;

        // 1. 도메인 워핑: 샘플 좌표를 노이즈로 비틀기
        float sampleX = wx;
        float sampleZ = wz;
        if (noiseSettings.enableDomainWarp)
        {
            float warpX = Mathf.PerlinNoise(wx * noiseSettings.warpScale + noiseSettings.warpOffset.x,
                                            wz * noiseSettings.warpScale + noiseSettings.warpOffset.y);
            float warpZ = Mathf.PerlinNoise(wx * noiseSettings.warpScale + noiseSettings.warpOffset.x + 3.7f,
                                            wz * noiseSettings.warpScale + noiseSettings.warpOffset.y + 3.7f);
            sampleX += (warpX - 0.5f) * noiseSettings.warpStrength;
            sampleZ += (warpZ - 0.5f) * noiseSettings.warpStrength;
        }

        // 2. 옥타브 합산 (워핑된 좌표 사용)
        float rawNoise = 0f;
        float amplitudeSum = 0f;
        foreach (var oct in noiseSettings.octaves)
        {
            if (oct.scale <= 0f)
            {
                Debug.LogWarning("[TerrainChunk] Octave with scale <= 0 skipped — set amplitude=0 to disable.", this);
                continue;
            }
            if (oct.amplitude <= 0f) continue;
            float n = Mathf.PerlinNoise(sampleX * oct.scale + oct.offset.x,
                                         sampleZ * oct.scale + oct.offset.y);
            n = Mathf.Clamp01(n);
            switch (oct.mode)
            {
                case MapNoiseSettings.NoiseOctave.Mode.Ridged: n = Mathf.Clamp01(1f - Mathf.Abs(n * 2f - 1f)); break;
                case MapNoiseSettings.NoiseOctave.Mode.Valley: n = Mathf.Clamp01(Mathf.Abs(n * 2f - 1f));       break;
                default: break;
            }
            rawNoise += n * oct.amplitude;
            amplitudeSum += oct.amplitude;
        }

        // 3. 리매핑: 0~1로 정규화 후 커브 적용
        float normalizedNoise = amplitudeSum > 0f ? rawNoise / amplitudeSum : 0f;
        float normalizedClamped = Mathf.Clamp01(normalizedNoise);
        float remapValue = (noiseSettings.heightRemap != null && noiseSettings.heightRemap.length > 0)
            ? noiseSettings.heightRemap.Evaluate(normalizedClamped)
            : normalizedClamped;
        remapValue = Mathf.Clamp01(remapValue);
        float remappedHeight = remapValue * noiseSettings.remapMaxHeight;

        // 4. 거점 평탄화
        float dist = Mathf.Sqrt(wx * wx + wz * wz); // 거점 경계는 world 좌표 기준 (워핑과 무관하게 물리적 위치로 판정)
        float radius = Mathf.Max(noiseSettings.cityRadius, 0f);
        if (radius <= 0f)
            return remappedHeight;

        float transition = Mathf.Clamp(noiseSettings.cityFlatTransition, 0.001f, radius);
        float flatStart = radius - transition;

        if (dist < flatStart)
            return 0f;
        if (dist < radius)
        {
            float t = (dist - flatStart) / transition;
            return Mathf.Lerp(0f, remappedHeight, t);
        }
        return remappedHeight;
    }

    private void DetermineZone(Vector2Int coord)
    {
        if (noiseSettings == null)
            return;

        float cx = coord.x * _chunkSize;
        float cz = coord.y * _chunkSize;
        float dist = Mathf.Sqrt(cx * cx + cz * cz);

        if (dist <= noiseSettings.cityRadius)
            return;

        int seed = coord.x * 1000003 + coord.y * 999983;
        var rng = new System.Random(seed);
        if (rng.NextDouble() < noiseSettings.smallTownProbability)
            Debug.Log($"SmallTown spawned at {coord}");
    }
}
