using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField, Min(0)] private int viewRadius = 3;
    [SerializeField, Min(0.01f)] private float chunkSize = 80f;
    [SerializeField] private GameObject[] blockPalette;

    private Dictionary<Vector2Int, GameObject> _activeChunks = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int _lastPlayerChunk = new Vector2Int(int.MaxValue, int.MaxValue);
    private readonly HashSet<Vector2Int> _neededChunks = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> _toRemove = new List<Vector2Int>();

    /// <summary>초기 청크 생성이 완료되었는지 여부. 다른 시스템이 참조.</summary>
    public bool InitialChunksReady { get; private set; }

    private void Update()
    {
        if (playerTransform == null || blockPalette == null || blockPalette.Length == 0)
            return;

        Vector2Int currentChunk = WorldToChunkCoord(playerTransform.position);
        if (currentChunk == _lastPlayerChunk)
            return;

        _lastPlayerChunk = currentChunk;
        UpdateChunks(currentChunk);

        if (!InitialChunksReady)
            InitialChunksReady = true;
    }

    private void UpdateChunks(Vector2Int center)
    {
        _neededChunks.Clear();
        for (int z = -viewRadius; z <= viewRadius; z++)
        {
            for (int x = -viewRadius; x <= viewRadius; x++)
            {
                _neededChunks.Add(new Vector2Int(center.x + x, center.y + z));
            }
        }

        // 범위 밖 청크 제거
        _toRemove.Clear();
        foreach (var coord in _activeChunks.Keys)
        {
            if (!_neededChunks.Contains(coord))
                _toRemove.Add(coord);
        }
        foreach (var coord in _toRemove)
        {
            Destroy(_activeChunks[coord]);
            _activeChunks.Remove(coord);
        }

        // 없는 청크 생성
        foreach (var coord in _neededChunks)
        {
            if (_activeChunks.ContainsKey(coord))
                continue;

            // coord로 시드된 결정론적 선택 — 같은 coord는 항상 같은 블록+회전 → 되돌아가도 도시가 그대로.
            // unchecked: 좌표가 극단으로 커져도 오버플로가 플랫폼 무관하게 wrap되어 결정론 유지.
            int seed;
            unchecked { seed = coord.x * 1000003 + coord.y * 999983; }
            var rng = new System.Random(seed);
            int idx = rng.Next(blockPalette.Length);
            int rot = rng.Next(4) * 90;

            GameObject prefab = blockPalette[idx];
            if (prefab == null)
            {
                Debug.LogError($"[ChunkManager] blockPalette[{idx}] is null — Inspector 슬롯을 확인하세요.", this);
                continue;
            }

            GameObject go = Instantiate(prefab, transform);
            // 블록 로컬 원점=타일 중심. 기존 TerrainChunk와 동일하게 coord*chunkSize에 배치 → 경계 무이음.
            go.transform.position = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);
            go.transform.rotation = Quaternion.Euler(0f, rot, 0f);

            _activeChunks[coord] = go;
        }
    }

    private Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkSize),
            Mathf.FloorToInt(worldPos.z / chunkSize)
        );
    }
}
