using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그레이박스 도심 한 블록을 시드 기반으로 결정론적으로 자동 배치한다.
///
/// 설계 철학:
///  - 바닥(Floor)이 플레이 공간이고 빌딩은 "벽/경계"다 (뱀서식 열린 바닥에서 카이팅·둘러쌈).
///  - roadStride 간격의 셀 = 바닥만(도로 회랑). 광장 영역 = 바닥 + Prop 희박(열린 킬존).
///  - 나머지 슈퍼블록 셀 = 시드 기반 빌딩 변형(S/M/L). Bldg_L(히어로 타워)은 가장자리/먼쪽에 가중 배치
///    — 카메라 45°/15m에서 카메라 앞쪽 키 큰 빌딩이 시야를 가리지 않도록.
///  - 결정론적: 같은 seed = 같은 레이아웃. 좋은 레이아웃을 시드로 락 가능.
///
/// 에디트타임 전용: [ContextMenu Generate/Clear]. 생성물은 명명된 부모 Transform 아래에만 모이고
/// Clear는 그것만 지운다 (씬의 다른 오브젝트는 건드리지 않음). 런타임 의존 없음.
/// </summary>
[DisallowMultipleComponent]
public class CityBlockGenerator : MonoBehaviour
{
    [Header("모듈 프리팹 (Blockout 키트)")]
    [SerializeField] GameObject floorPrefab;   // Floor_10  10×10m
    [SerializeField] GameObject bldgSPrefab;    // Bldg_S    10×10m, h12
    [SerializeField] GameObject bldgMPrefab;    // Bldg_M    20×20m, h24
    [SerializeField] GameObject bldgLPrefab;    // Bldg_L    20×20m, h45 (히어로 타워)
    [SerializeField] GameObject propCarPrefab;  // Prop_Car  2×4.5m, h1.5

    [Header("그리드")]
    [SerializeField] Vector2Int gridCells = new Vector2Int(14, 14);
    [Min(0.1f)] [SerializeField] float cellSize = 10f;

    [Header("도로 회랑")]
    [SerializeField] int roadStride = 4;   // 도로 회랑 간격(N셀마다 도로 라인)
    [SerializeField] int roadWidth = 1;    // 도로 폭(셀)

    [Header("중앙 광장 (빌딩 0 열린 킬존)")]
    [SerializeField] Vector2Int plazaCenter = new Vector2Int(-1, -1); // (-1,-1) = 그리드 중앙 자동
    [SerializeField] Vector2Int plazaSize = new Vector2Int(4, 4);
    [Range(0f, 1f)] [SerializeField] float plazaPropChance = 0.12f;   // 광장 셀에 Prop_Car 희박 배치 확률

    [Header("빌딩 변형 가중치")]
    [SerializeField] float weightS = 0.55f;
    [SerializeField] float weightM = 0.30f;
    [SerializeField] float weightL = 0.15f;
    [Tooltip("M/L 빌딩(20×20m)이 2×2 셀을 차지하도록 배치 시도(가능할 때). 끄면 모든 빌딩 1셀.")]
    [SerializeField] bool largeBuildingsSpanTwoCells = true;
    [Range(0f, 1f)] [SerializeField] float heightJitter = 0.12f;     // ±12% 높이(Y스케일) 지터

    [Header("Bldg_L 가장자리 가중 (시야 차폐 방지)")]
    [Tooltip("0=균일, 1=가장자리에만. 그리드 중심에서 멀수록 L 빌딩 확률 가중.")]
    [Range(0f, 1f)] [SerializeField] float largeEdgeBias = 0.75f;

    [Header("시드")]
    [SerializeField] int seed = 12345;
    [SerializeField] bool randomizeSeed = false;
    [Tooltip("마지막 Generate에 실제 쓰인 시드(읽기 전용). 마음에 드는 레이아웃이면 이 값을 seed에 복사해 락.")]
    [SerializeField] int lastUsedSeed;

    const string RootName = "CityBlock";
    const int ObstacleLayerFallback = 8;  // 빌딩 = 장애물
    const int GroundLayerFallback = 6;    // 바닥 = 지면

    // 슈퍼블록 셀이 이미 (멀티셀 빌딩에) 점유됐는지 추적 — Generate 1회당 초기화.
    bool[,] _occupied;
    // Generate 1회당 해석: 레이어 인덱스 + 광장 직사각형(그리드 안으로 clamp).
    int _obstacleLayer, _groundLayer;
    Vector2Int _plazaMin, _plazaMax;

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();

        if (floorPrefab == null)
        {
            Debug.LogWarning("[CityBlockGenerator] floorPrefab이 비어 있음 — 바닥 없이 생성합니다. Inspector에 모듈 프리팹을 배선하세요.", this);
        }

        var root = new GameObject(RootName);
        root.transform.SetParent(transform, false);

        int s = randomizeSeed ? (int)System.DateTime.Now.Ticks : seed;
        lastUsedSeed = s;
        var rng = new System.Random(s);

        _obstacleLayer = ResolveLayer("Obstacle", ObstacleLayerFallback);
        _groundLayer = ResolveLayer("Ground", GroundLayerFallback);

        int gx = Mathf.Max(1, gridCells.x);
        int gy = Mathf.Max(1, gridCells.y);
        _occupied = new bool[gx, gy];

        GetPlazaRect(gx, gy, out _plazaMin, out _plazaMax);

        // 1) 바닥 전체 깔기
        var floorParent = NewChild(root.transform, "Floors");
        for (int x = 0; x < gx; x++)
            for (int y = 0; y < gy; y++)
                PlaceFloor(floorParent, x, y);

        // 2) 슈퍼블록 셀에 빌딩 배치 (도로/광장 제외)
        var bldgParent = NewChild(root.transform, "Buildings");
        for (int x = 0; x < gx; x++)
        {
            for (int y = 0; y < gy; y++)
            {
                if (_occupied[x, y]) continue;
                if (IsRoadCell(x, y)) continue;
                if (IsPlazaCell(x, y)) continue;

                PlaceBuilding(bldgParent, x, y, gx, gy, rng);
            }
        }

        // 3) 광장에 Prop_Car 희박 배치
        if (propCarPrefab != null)
        {
            var propParent = NewChild(root.transform, "Props");
            for (int x = 0; x < gx; x++)
                for (int y = 0; y < gy; y++)
                    if (IsPlazaCell(x, y) && rng.NextDouble() < plazaPropChance)
                        PlaceCar(propParent, x, y, rng);
        }

        Debug.Log($"[CityBlockGenerator] Generate 완료 (seed={s}, grid={gx}×{gy}, cell={cellSize}m).", this);
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var existing = transform.Find(RootName);
        if (existing == null) return;
        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    // ── 셀 분류 ──────────────────────────────────────────────────────
    bool IsRoadCell(int x, int y)
    {
        if (roadStride <= 0) return false;
        int w = Mathf.Max(1, roadWidth);
        // x 또는 y가 roadStride 라인의 roadWidth 밴드 안에 들면 도로.
        bool roadX = (x % roadStride) < w;
        bool roadY = (y % roadStride) < w;
        return roadX || roadY;
    }

    // 광장 직사각형은 Generate에서 _plazaMin/_plazaMax로 해석됨(그리드 안으로 clamp). 빈 광장이면 max<min.
    bool IsPlazaCell(int x, int y)
    {
        return x >= _plazaMin.x && x <= _plazaMax.x && y >= _plazaMin.y && y <= _plazaMax.y;
    }

    // 광장 직사각형을 그리드 안으로 완전히 clamp해 [min,max] 반환 (밖으로 삐져나가거나 전체를 덮지 않게).
    void GetPlazaRect(int gx, int gy, out Vector2Int min, out Vector2Int max)
    {
        int psX = Mathf.Clamp(Mathf.Max(0, plazaSize.x), 0, gx);
        int psY = Mathf.Clamp(Mathf.Max(0, plazaSize.y), 0, gy);
        int cx = plazaCenter.x < 0 ? gx / 2 : plazaCenter.x;
        int cy = plazaCenter.y < 0 ? gy / 2 : plazaCenter.y;
        int minX = Mathf.Clamp(cx - psX / 2, 0, Mathf.Max(0, gx - psX));
        int minY = Mathf.Clamp(cy - psY / 2, 0, Mathf.Max(0, gy - psY));
        min = new Vector2Int(minX, minY);
        max = new Vector2Int(minX + psX - 1, minY + psY - 1); // psX=0 → max<min = 광장 없음
    }

    // 레이어 이름 우선 해석, 없으면 fallback 인덱스 + 경고(번호 재할당 시 무음 오작동 방지).
    static int ResolveLayer(string layerName, int fallback)
    {
        int idx = LayerMask.NameToLayer(layerName);
        if (idx < 0)
        {
            Debug.LogWarning($"[CityBlockGenerator] 레이어 '{layerName}' 없음 — fallback {fallback} 사용. Project Settings 레이어 확인 권장.");
            return fallback;
        }
        return idx;
    }

    // ── 배치 ─────────────────────────────────────────────────────────
    void PlaceFloor(Transform parent, int x, int y)
    {
        if (floorPrefab == null) return;
        var go = InstantiateModule(floorPrefab, CellCenterWorld(x, y, 1, 1), Quaternion.identity, parent);
        go.name = $"Floor_{x}_{y}";
        SetLayerRecursive(go.transform, _groundLayer);
    }

    void PlaceBuilding(Transform parent, int x, int y, int gx, int gy, System.Random rng)
    {
        // 변형 추첨 — L은 가장자리 가중으로 보정.
        float edge = EdgeFactor(x, y, gx, gy);          // 0=중심, 1=가장자리
        float wL = weightL * Mathf.Lerp(1f - largeEdgeBias, 1f, edge);
        float wM = weightM;
        float wS = weightS;
        float total = wS + wM + wL;
        if (total <= 0f) { wS = 1f; total = 1f; }

        double roll = rng.NextDouble() * total;
        GameObject prefab;
        bool wantsSpan;  // 20×20m (2×2 셀) 빌딩인가
        if (roll < wS) { prefab = bldgSPrefab; wantsSpan = false; }
        else if (roll < wS + wM) { prefab = bldgMPrefab; wantsSpan = true; }
        else { prefab = bldgLPrefab; wantsSpan = true; }

        if (prefab == null)
        {
            // 폴백: S로 강등(L/M 프리팹 미배선 시).
            prefab = bldgSPrefab;
            wantsSpan = false;
            if (prefab == null) return;
        }

        int cellsX = 1, cellsY = 1;
        if (wantsSpan && largeBuildingsSpanTwoCells && CanSpan2x2(x, y, gx, gy))
        {
            cellsX = 2; cellsY = 2;
            _occupied[x, y] = true;
            _occupied[x + 1, y] = true;
            _occupied[x, y + 1] = true;
            _occupied[x + 1, y + 1] = true;
        }
        else
        {
            _occupied[x, y] = true;
        }

        var go = InstantiateModule(prefab, CellCenterWorld(x, y, cellsX, cellsY), Quaternion.identity, parent);
        go.name = $"{prefab.name}_{x}_{y}";

        // 높이 지터 (Y스케일만 ±heightJitter).
        if (heightJitter > 0f)
        {
            float jitter = 1f + ((float)rng.NextDouble() * 2f - 1f) * heightJitter;
            var sc = go.transform.localScale;
            sc.y *= jitter;
            go.transform.localScale = sc;
        }

        SetLayerRecursive(go.transform, _obstacleLayer);
    }

    void PlaceCar(Transform parent, int x, int y, System.Random rng)
    {
        var go = InstantiateModule(propCarPrefab, CellCenterWorld(x, y, 1, 1), Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), parent);
        go.name = $"Prop_Car_{x}_{y}";
        SetLayerRecursive(go.transform, _obstacleLayer);
    }

    // 멀티셀 빌딩이 차지할 셀이 모두 비어있고 도로/광장이 아니며 그리드 안인지.
    bool CanSpan2x2(int x, int y, int gx, int gy)
    {
        if (x + 1 >= gx || y + 1 >= gy) return false;
        for (int dx = 0; dx <= 1; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                int cx = x + dx, cy = y + dy;
                if (_occupied[cx, cy]) return false;
                if (IsRoadCell(cx, cy)) return false;
                if (IsPlazaCell(cx, cy)) return false;  // 2×2가 광장 킬존을 침범하지 않게.
            }
        }
        return true;
    }

    // 그리드 중심에서의 정규화 거리(0=중심, 1=모서리). L 빌딩 가장자리 가중에 사용.
    float EdgeFactor(int x, int y, int gx, int gy)
    {
        float cx = (gx - 1) * 0.5f;
        float cy = (gy - 1) * 0.5f;
        float nx = cx > 0f ? Mathf.Abs(x - cx) / cx : 0f;
        float ny = cy > 0f ? Mathf.Abs(y - cy) / cy : 0f;
        return Mathf.Max(nx, ny);
    }

    // 셀 (x,y)를 cellsX×cellsY 풋프린트로 점유할 때의 월드 중심(피벗=바닥 중심, 그리드 스냅).
    Vector3 CellCenterWorld(int x, int y, int cellsX, int cellsY)
    {
        float wx = (x + cellsX * 0.5f) * cellSize;
        float wz = (y + cellsY * 0.5f) * cellSize;
        return transform.position + new Vector3(wx, 0f, wz);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────
    static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }

    // 에디트타임엔 프리팹 링크를 보존해 인스턴스화(원본 프리팹 수정이 씬에 반영되게). 런타임엔 일반 Instantiate.
    GameObject InstantiateModule(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.SetPositionAndRotation(pos, rot);
            return inst;
        }
#endif
        return Instantiate(prefab, pos, rot, parent);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        int gx = Mathf.Max(1, gridCells.x);
        int gy = Mathf.Max(1, gridCells.y);
        Vector3 origin = transform.position;
        // 그리드 경계
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireCube(
            origin + new Vector3(gx * cellSize * 0.5f, 0f, gy * cellSize * 0.5f),
            new Vector3(gx * cellSize, 0.1f, gy * cellSize));
        // 광장 (그리드 안으로 clamp된 실제 직사각형)
        GetPlazaRect(gx, gy, out var pMin, out var pMax);
        if (pMax.x >= pMin.x && pMax.y >= pMin.y)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
            int pw = pMax.x - pMin.x + 1;
            int ph = pMax.y - pMin.y + 1;
            Vector3 plazaC = origin + new Vector3((pMin.x + pw * 0.5f) * cellSize, 0f, (pMin.y + ph * 0.5f) * cellSize);
            Gizmos.DrawWireCube(plazaC, new Vector3(pw * cellSize, 0.1f, ph * cellSize));
        }
    }
#endif
}
