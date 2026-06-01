using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도심 블록(80×80) 생성 직후 폐허 프롭(차량·잔해·식생)을 시드로 흩뿌려 "손배치한 듯한" 밀도를 만든다.
/// 같은 coord는 항상 같은 배치 → ChunkManager가 청크를 파괴/재생성해도 잔해가 그대로(결정론).
///
/// 배치 규칙:
///  - 중앙 십자 도로(|x|<roadHalfWidth 또는 |z|<roadHalfWidth)는 비워 통행 보장 → 프롭은 4분면에만.
///  - 건물(Obstacle 레이어 8) 바운드와 겹치면 거부 → 건물 안/밑에 안 박힘.
///  - 프롭은 4분면에만 생기므로 중앙 통행로 길막 없음(좀비/플레이어 경로 안전).
///
/// 런타임은 Start에서, 에디터 프리뷰는 Decorate()를 직접 호출해 동일 경로로 검증한다.
/// </summary>
[DisallowMultipleComponent]
public class BlockDecorator : MonoBehaviour
{
    [Header("Prop pools (시드 흩뿌림)")]
    [SerializeField] GameObject[] heavyProps;    // 차량·대형 잔해 — 희소
    [SerializeField] GameObject[] debrisProps;   // 잔해·드럼통·쓰레기 — 중간
    [SerializeField] GameObject[] vegetation;    // 덤불·나무(자연 회복) — 중간

    [Header("Counts (블록 1개당)")]
    [SerializeField] int heavyCount = 3;
    [SerializeField] int debrisCount = 12;
    [SerializeField] int vegetationCount = 10;

    [Header("Layout")]
    [SerializeField] float blockSize = 80f;
    [SerializeField] float roadHalfWidth = 12f;  // 중앙 십자 도로(±10) + 여유 → 통행로 보존
    [SerializeField] float heavyRoadExtra = 4f;   // 대형 프롭(차량/스킵)은 풋프린트가 커 도로변 침범 → 추가 여유
    [SerializeField] float edgeMargin = 3f;       // 외곽 이음새에서 떨어뜨림
    [SerializeField] float clearRadius = 1.8f;    // 건물 바운드 회피 여유
    [SerializeField] int maxTriesPerProp = 10;

    void Start()
    {
        Decorate();
    }

    /// <summary>시드 기반으로 프롭을 흩뿌린다. 재호출 시 이전 결과를 지워 멱등.</summary>
    public void Decorate()
    {
        var existing = transform.Find("Decoration");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        var root = new GameObject("Decoration");
        root.transform.SetParent(transform, false);

        // coord 시드 — ChunkManager 블록 선택 시드와 분리하려고 +31 오프셋.
        int cx = Mathf.RoundToInt(transform.position.x / blockSize);
        int cz = Mathf.RoundToInt(transform.position.z / blockSize);
        int seed;
        unchecked { seed = cx * 1000003 + cz * 999983 + 31; }
        var rng = new System.Random(seed);

        var blockers = CollectBlockers();

        // 대형 프롭은 풋프린트가 커 도로변까지 침범 → 전용 여유(roadHalfWidth+heavyRoadExtra)로 더 밀어낸다.
        Scatter(root.transform, heavyProps,  heavyCount,      rng, blockers, 0.9f, 1.1f, roadHalfWidth + heavyRoadExtra);
        Scatter(root.transform, debrisProps, debrisCount,     rng, blockers, 0.8f, 1.3f, roadHalfWidth);
        Scatter(root.transform, vegetation,  vegetationCount, rng, blockers, 0.8f, 1.4f, roadHalfWidth);
    }

    /// <summary>이 블록의 건물(Obstacle 레이어 8) 렌더러 월드 바운드 수집 — 물리 등록 타이밍 무관하게 안전.</summary>
    List<Bounds> CollectBlockers()
    {
        var list = new List<Bounds>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.gameObject.layer == 8) list.Add(r.bounds);
        return list;
    }

    static readonly List<GameObject> _validPool = new List<GameObject>();

    void Scatter(Transform parent, GameObject[] pool, int count, System.Random rng, List<Bounds> blockers, float sMin, float sMax, float roadClear)
    {
        if (pool == null || count <= 0) return;

        // null 슬롯은 미리 거른다 → RNG가 항상 유효 프리팹만 뽑아 결정론을 깨지 않음(빈 슬롯이 위치 시도를 삼키던 버그 제거).
        _validPool.Clear();
        for (int p = 0; p < pool.Length; p++)
            if (pool[p] != null) _validPool.Add(pool[p]);
        if (_validPool.Count == 0) return;

        float half = blockSize * 0.5f - edgeMargin;
        for (int i = 0; i < count; i++)
        {
            // 슬롯마다 prefab/yaw/scale를 먼저 고정 예산으로 뽑는다 → 배치 성공/실패가 RNG 흐름을 바꾸지 않음(결정론을 구조로 보장).
            var prefab = _validPool[rng.Next(_validPool.Count)];
            float yaw = (float)(rng.NextDouble() * 360.0);
            float scale = Mathf.Lerp(sMin, sMax, (float)rng.NextDouble());

            // 위치 후보 maxTries개를 "항상" 끝까지 소진하며 첫 유효점을 채택 → RNG 소비량이 슬롯마다 일정.
            bool placed = false;
            Vector3 chosen = default;
            for (int t = 0; t < maxTriesPerProp; t++)
            {
                float lx = (float)(rng.NextDouble() * 2.0 - 1.0) * half;
                float lz = (float)(rng.NextDouble() * 2.0 - 1.0) * half;
                if (placed) continue;                                        // 이미 채택했어도 남은 추첨은 소비만
                if (Mathf.Abs(lx) < roadClear || Mathf.Abs(lz) < roadClear) continue;  // 중앙 십자 도로 회피
                Vector3 w = transform.TransformPoint(new Vector3(lx, 0f, lz));
                if (IsBlocked(w, blockers)) continue;                        // 건물 바운드 회피
                chosen = w; placed = true;
            }

            if (!placed) continue;
            var go = Instantiate(prefab, chosen, Quaternion.Euler(0f, yaw, 0f), parent);
            go.transform.localScale *= scale;
        }
    }

    bool IsBlocked(Vector3 world, List<Bounds> blockers)
    {
        for (int i = 0; i < blockers.Count; i++)
        {
            var b = blockers[i];
            if (world.x >= b.min.x - clearRadius && world.x <= b.max.x + clearRadius &&
                world.z >= b.min.z - clearRadius && world.z <= b.max.z + clearRadius)
                return true;
        }
        return false;
    }
}
