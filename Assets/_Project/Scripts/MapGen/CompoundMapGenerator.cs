#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZombieCrush.MapGen
{
    /// <summary>
    /// 컴파운드 맵 빌더 — MapSpec 데이터 한 장으로 전체 맵을 재현(같은 seed=같은 결과).
    /// 던지던 RunCommand 손배치를 *재사용 가능·시드 결정론·치수 정합* 코드로 승격.
    ///
    /// 빌드 순서(루트 하나 밑): 지면 → 도로 → 컴파운드 → 비콘 → 경계 → 폐허 → 스폰마커.
    /// 에디터/RunCommand 양쪽 호출 가능(플레이모드 의존 없음). 씬/프리팹 저장은 하지 않음(호출측 책임).
    /// </summary>
    public static class CompoundMapGenerator
    {
        public const string RootName = "GeneratedMap";

        // ── 메뉴: 코드 스펙(폐허 도심 1차)으로 생성 ──
        [MenuItem("Tools/MapGen/Generate Map")]
        public static GameObject GenerateDefault()
        {
            ClearGenerated();
            var spec = RuinCitySpec.Build();
            try
            {
                var go = Generate(spec);
                Selection.activeGameObject = go;
                Debug.Log($"[MapGen] '{spec.mapName}' 생성 완료 (seed={spec.seed}). 루트='{RootName}'.");
                return go;
            }
            finally
            {
                // 임시 스펙 SO는 생성에만 쓰고 보존 안 함(생성물은 값 복사라 참조 안 함) → 메뉴 반복 시 누수 방지(Codex LOW).
                if (spec != null) Object.DestroyImmediate(spec);
            }
        }

        [MenuItem("Tools/MapGen/Clear Generated")]
        public static void ClearGenerated()
        {
            // ★HIGH: 이름(GameObject.Find)으로 삭제하면 동명 오브젝트 오삭제 위험 → 소유 마커(MapGenRoot)로만 식별.
            var roots = Object.FindObjectsOfType<MapGenRoot>(true);
            foreach (var r in roots)
                if (r != null) Object.DestroyImmediate(r.gameObject);
        }

        /// <summary>
        /// MapSpec 으로 맵을 빌드한다. 루트 GameObject('GeneratedMap') 하나 밑에 전부 들어간다.
        /// RunCommand 에서도 직접 호출 가능: CompoundMapGenerator.Generate(RuinCitySpec.Build()).
        /// </summary>
        public static GameObject Generate(MapSpec spec)
        {
            if (spec == null) { Debug.LogWarning("[MapGen] spec null — 생성 중단."); return null; }
            MapGenKit.ClearCaches(); // stale 프리팹/풋프린트 방지

            // ★HIGH: 새 루트 생성 전, 기존 생성물(마커 기준)을 먼저 제거 → 누적/중복 방지.
            ClearGenerated();

            var root = new GameObject(RootName);
            root.AddComponent<MapGenRoot>(); // 소유 마커(Clear 식별 + 런타임 머티리얼 정리)

            // 1) 완전 바닥 — 전면 타일 바닥(회전0·피벗보정·이음새 backstop) + E-1 구역 패치(풀/흙).
            MapGenKit.TiledFloor(spec.mapHalf, spec.baseFloorTile, spec.groundColor, root.transform);
            if (spec.groundPatches != null && spec.groundPatches.Count > 0)
            {
                var patchParent = new GameObject("GroundPatches");
                patchParent.transform.SetParent(root.transform, false);
                foreach (var gp in spec.groundPatches)
                    MapGenKit.GroundPatch(gp, patchParent.transform);
            }

            // 2) 도로 — ★E-2 그래프(위계·교차 피스)가 있으면 그게 메인, 없으면 폴리라인 폴백.
            var roadParent = new GameObject("Roads");
            roadParent.transform.SetParent(root.transform, false);
            if (spec.roadGraph != null && spec.roadGraph.HasGraph)
            {
                MapGenKit.RoadGraph(spec.roadGraph, roadParent.transform);
            }
            else
            {
                var roadPolylines = spec.roadPolylines;
                if (roadPolylines != null)
                    foreach (var poly in roadPolylines)
                        MapGenKit.RoadPolyline(poly?.pts, roadParent.transform, spec.roadTilePrefab);
            }

            // 3~4.5) 히어로(랜드마크·핵심 건물·회랑) = 손배치 → heroByHand 면 절차 생성에서 제외(캔버스만).
            //   청사진(spec.compounds/corridors/coreBeacon 좌표)은 유저 손배치 가이드로 유지.
            if (!spec.heroByHand)
            {
                // 3) 컴파운드(비대칭 거점). null 가드 + AABB 겹침 경고(자동 회피 X — 데이터 검수 보조).
                var compParent = new GameObject("Compounds");
                compParent.transform.SetParent(root.transform, false);
                var compounds = spec.compounds;
                if (compounds != null)
                {
                    WarnCompoundOverlaps(compounds);
                    foreach (var c in compounds)
                        MapGenKit.Compound(c, compParent.transform);
                }

                // 4) 코어 비콘(끌림 — 키 큰 크레인 등).
                if (!string.IsNullOrEmpty(spec.coreBeaconPrefab))
                {
                    Vector2 core = spec.CoreCenter();
                    MapGenKit.Spawn(spec.coreBeaconPrefab,
                        new Vector3(core.x, 0f, core.y), spec.coreBeaconRot, root.transform);
                }

                // 4.5) ★E-3 컨테이너 회랑(어깨맞대기 벽).
                if (spec.corridors != null && spec.corridors.Count > 0)
                {
                    var corrParent = new GameObject("Corridors");
                    corrParent.transform.SetParent(root.transform, false);
                    foreach (var c in spec.corridors)
                        MapGenKit.CorridorRow(c, corrParent.transform);
                }
            }

            // 5) 경계(봉쇄벽 둘레).
            MapGenKit.Boundary(spec.mapHalf, root.transform, spec.wallPrefab, spec.cornerPrefab);

            // 5.5) E-4 식생 군집(결정론 — spec.seed 분기).
            if (spec.vegetation != null && spec.vegetation.Count > 0)
            {
                var vegParent = new GameObject("Vegetation");
                vegParent.transform.SetParent(root.transform, false);
                foreach (var v in spec.vegetation)
                    MapGenKit.VegetationCluster(v, spec.seed, vegParent.transform);
            }

            // 6) 폐허 산포(결정론).
            float h = spec.mapHalf * 0.92f; // 벽 안쪽
            MapGenKit.ScatterRuins(
                new Rect(-h, -h, h * 2f, h * 2f),
                spec.ruinPrefabs, spec.ruinCount, spec.seed, root.transform);

            // 7) 스폰 마커(데이터만 — 디렉터 후속 연동).
            var markerParent = new GameObject("SpawnMarkers");
            markerParent.transform.SetParent(root.transform, false);
            var spawnMarkers = spec.spawnMarkers;
            if (spawnMarkers != null)
                foreach (var m in spawnMarkers)
                {
                    if (m == null) continue;
                    var mk = new GameObject($"Spawn_L{m.lvBand}_{m.role}");
                    mk.transform.SetParent(markerParent.transform, false);
                    mk.transform.position = new Vector3(m.pos.x, 0f, m.pos.y);
                    var comp = mk.AddComponent<MapGenSpawnMarker>();
                    comp.lvBand = m.lvBand;
                    comp.role = m.role;
                }

            return root;
        }

        /// <summary>
        /// 컴파운드 쌍별 월드 XZ AABB 겹침을 검사해 경고만 남긴다(자동 회피·재배치 X — 데이터 검수 보조).
        /// 컴파운드 배치는 데이터(스펙) 책임이므로 폭발이 아니라 로그로 검수자에게 알린다.
        /// </summary>
        static void WarnCompoundOverlaps(System.Collections.Generic.List<CompoundSpec> compounds)
        {
            int n = compounds.Count;
            for (int i = 0; i < n; i++)
            {
                if (compounds[i] == null) continue;
                var ri = MapGenKit.CompoundFootprint(compounds[i]);
                for (int j = i + 1; j < n; j++)
                {
                    if (compounds[j] == null) continue;
                    var rj = MapGenKit.CompoundFootprint(compounds[j]);
                    if (ri.Overlaps(rj))
                        Debug.LogWarning($"[MapGen] 컴파운드 AABB 겹침: '{compounds[i].name}' ↔ '{compounds[j].name}' — 데이터 검수 필요(자동 회피 안 함).");
                }
            }
        }
    }

    /// <summary>
    /// MapGen 생성물 루트 소유 마커 — Clear 가 이름이 아니라 이 컴포넌트로 식별/삭제한다(동명 오삭제 방지).
    /// 생성 중 만든 런타임 머티리얼(Ground 등)을 등록해 두고, 루트 파괴 시 함께 파기(누수 방지).
    /// </summary>
    public class MapGenRoot : MonoBehaviour
    {
        // 인스펙터 노출 불요(런타임 정리용) — 직렬화 안 함.
        [System.NonSerialized] readonly System.Collections.Generic.List<Material> _runtimeMaterials =
            new System.Collections.Generic.List<Material>();

        /// <summary>생성 중 new 한 머티리얼을 등록 → OnDestroy 에서 함께 파기.</summary>
        public void RegisterRuntimeMaterial(Material mat)
        {
            if (mat != null) _runtimeMaterials.Add(mat);
        }

        void OnDestroy()
        {
            foreach (var m in _runtimeMaterials)
                if (m != null) Object.DestroyImmediate(m);
            _runtimeMaterials.Clear();
        }
    }
}
#endif
