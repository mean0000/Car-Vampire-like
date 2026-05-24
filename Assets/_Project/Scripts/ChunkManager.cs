using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField, Min(0)] private int viewRadius = 3;
    [SerializeField, Min(0.01f)] private float chunkSize = 80f;
    [SerializeField] private GameObject chunkPrefab;

    private Dictionary<Vector2Int, GameObject> _activeChunks = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int _lastPlayerChunk = new Vector2Int(int.MaxValue, int.MaxValue);
    private readonly HashSet<Vector2Int> _neededChunks = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> _toRemove = new List<Vector2Int>();

    private void Update()
    {
        if (playerTransform == null || chunkPrefab == null)
            return;

        Vector2Int currentChunk = WorldToChunkCoord(playerTransform.position);
        if (currentChunk == _lastPlayerChunk)
            return;

        _lastPlayerChunk = currentChunk;
        UpdateChunks(currentChunk);
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

            GameObject go = Instantiate(chunkPrefab, transform);
            var chunk = go.GetComponent<TerrainChunk>();
            if (chunk != null)
                chunk.Init(coord, chunkSize);
            else
            {
                Debug.LogError($"[ChunkManager] chunkPrefab '{chunkPrefab.name}' has no TerrainChunk component.", go);
                Destroy(go);
                continue;
            }

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
