#if UNITY_EDITOR
using UnityEngine;

namespace ZombieCrush.MapGen
{
    /// <summary>
    /// 첫 MapSpec — 폐허 도심 "집하장"(map-construction-bible 파트2 블루프린트). 손배치를 데이터화.
    ///
    /// 끌림(natural-pull §2): 굽은 간선 → 컨테이너 회랑 미로 → 코어 크레인 야드(잭팟·비콘) 왈칵.
    /// 동심원 LV: 외곽 r55~74 LV1 / 중간 r22~50 LV2~3 / 코어 r0~22 LV4~5.
    ///
    /// ★바이블 처방 반영(무지성 6대 실패 교정):
    ///  ① 도로 = 폴리라인 아스팔트 유도선(겹침=연속 도로, 자연 크로스) — RoadGraph 교차 피스는 에셋 폭불일치로 보류.
    ///  ② 밀도 = props 링 확충 + ScatterRuins 55 + 식생 군집.
    ///  ③ 6 클러스터 정체성(주거·코어야드·검문소·창고·공터 5기 + 회랑).
    ///  ④ 복합 지면 = GroundPatch(코어 콘크리트 바닥 + 공터 풀 패치).
    ///  ⑤ 색·매스 다양(House 01/02/03+Shack, 컨테이너 색 번갈아).
    ///  ⑥ 블록 30~40m(컴파운드 좌표 간격).
    ///
    /// ※ ScriptableObject .asset 미저장 → 재현성을 코드에 고정(같은 함수=같은 맵).
    /// </summary>
    public static class RuinCitySpec
    {
        public static MapSpec Build()
        {
            var s = ScriptableObject.CreateInstance<MapSpec>();
            s.mapName = "RuinCity_Compound_01";
            s.seed = 7741;
            s.mapHalf = 75f;
            s.groundColor = new Color(0.30f, 0.27f, 0.22f); // 이음새 backstop(어두운 회갈)
            s.baseFloorTile = "SM_Env_Concrete_Base_01";    // 탄 포장 전면(완전 바닥)
            s.heroByHand = true;                            // 히어로(건물/크레인/회랑)=손배치, 캔버스만 절차 생성

            s.wallPrefab = "SM_Env_Port_Wall_01";
            s.cornerPrefab = "SM_Env_Port_Wall_Corner_01";
            s.roadTilePrefab = "SM_Env_Road_Straight_01"; // 폴리라인 폴백용(그래프가 메인)

            // ── 코어 비콘: 크레인(코어 야드). 붐이 -Z(LZ) 가리킴 ──
            s.coreBeaconPrefab = "SM_Bld_Crane_01";
            s.coreBeaconRot = 200f;

            BuildCompounds(s);
            BuildRoads(s);
            BuildCorridors(s);
            BuildGroundPatches(s);
            BuildVegetation(s);
            BuildRuinScatter(s);
            BuildSpawnMarkers(s);

            return s;
        }

        // ─────────────────────────────────────────────────────────────────
        // §C 클러스터 6기 — 정체성·좌표·rot(frontage)·건물·밀도 props
        // ─────────────────────────────────────────────────────────────────
        static void BuildCompounds(MapSpec s)
        {
            // C.1 코어 야드(산업 — 잭팟·비콘 수렴점). ★건물 없음: 크레인(비콘 45m)이 구조물,
            //     게이트릿 컨테이너 + 콘크리트 패드 + 야적 프롭이 야드 정체성. (Warehouse는 별도 클러스터 — 중복 제거,
            //     크레인 타워와 풋프린트 겹침 방지.)
            s.compounds.Add(new CompoundSpec
            {
                name = "CoreYard",
                type = CompoundType.CoreYard,
                centerXZ = new Vector2(8f, 14f),
                rot = 18f,
                buildingPrefabs = new string[0],
                props = new[]
                {
                    "SM_Prop_Floodlights_01", "SM_Prop_Pallet_Loaded_01",
                    "SM_Prop_BarrelStack_02", "SM_Prop_Generator_01",
                    "SM_Prop_Pallet_Loaded_02", "SM_Prop_Crate_Large_01",
                },
            });

            // C.3 주거 줄(외곽 LV1, SW) — 색·매스 다양.
            s.compounds.Add(new CompoundSpec
            {
                name = "ResidentialRuin",
                type = CompoundType.ResidentialRuin,
                centerXZ = new Vector2(-44f, -36f),
                rot = -35f, // 정면을 집산 레인으로
                buildingPrefabs = new[] { "SM_Bld_House_01", "SM_Bld_House_03", "SM_Bld_House_02", "SM_Bld_WoodenShack_01" },
                buildingGap = 2.5f,
                props = new[]
                {
                    "SM_Veh_Car_Destroyed_01", "SM_Env_Rubble_Pile_01",
                    "SM_Prop_WireFence_01", "SM_Prop_TrashBag_02", "SM_Env_Rubble_Pile_02",
                },
            });

            // C.4 검문소/전초(외곽→중간 진입 통제, NW).
            s.compounds.Add(new CompoundSpec
            {
                name = "Checkpoint",
                type = CompoundType.Outpost,
                centerXZ = new Vector2(-42f, 40f),
                rot = 25f,
                buildingPrefabs = new[] { "SM_Bld_Portable_Office_01", "SM_Bld_SmallBuilding_03" },
                buildingGap = 2.5f,
                props = new[]
                {
                    "SM_Prop_Barrier_01", "SM_Prop_TankTrap_01", "SM_Prop_Cinderblock_Wall_01",
                    "SM_Prop_Roadblock_02", "SM_Veh_Light_Armored_Car_01", "SM_Prop_GuardTower_01",
                },
            });

            // C.5 창고(중간~코어, E).
            s.compounds.Add(new CompoundSpec
            {
                name = "Warehouse",
                type = CompoundType.Warehouse,
                centerXZ = new Vector2(52f, -8f),
                rot = 90f, // 정면을 집산 레인으로
                buildingPrefabs = new[] { "SM_Bld_Warehouse_01" },
                buildingGap = 3f,
                props = new[]
                {
                    "SM_Veh_Truck_Destroyed_01", "SM_Prop_Pallet_Loaded_02",
                    "SM_Prop_Crate_Large_01", "SM_Prop_BarrelStack_02", "SM_Prop_Container_Small_01",
                },
            });

            // C.6 개활 잔해 공터(카이팅 떨치기, S).
            s.compounds.Add(new CompoundSpec
            {
                name = "OpenLot",
                type = CompoundType.OpenLot,
                centerXZ = new Vector2(4f, -52f),
                rot = 0f,
                buildingPrefabs = new string[0], // 개활 — 건물 없음
                props = new[]
                {
                    "SM_Veh_Car_Destroyed_01", "SM_Veh_Buggy_Destroyed_01", "SM_Env_Rubble_Pile_01",
                    "SM_Env_Rubble_Stone_01", "SM_Prop_Concrete_Slab_Pile_02", "SM_Env_Rubble_Pile_02",
                },
            });

            // ── 필러 클러스터(중간대/외곽 빈 공간 깸 — 구조물 + 잔해 더미) ──
            s.compounds.Add(new CompoundSpec
            {
                name = "FillerNE", type = CompoundType.OpenLot, centerXZ = new Vector2(40f, 34f), rot = -20f,
                buildingPrefabs = new[] { "SM_Bld_SmallBuilding_03", "SM_Bld_WoodenShack_01" }, buildingGap = 2f,
                props = new[] { "SM_Veh_Truck_Destroyed_01", "SM_Prop_Crate_Large_01", "SM_Prop_BarrelStack_02", "SM_Env_Rubble_Pile_01", "SM_Prop_Pallet_Loaded_02", "SM_Prop_Container_Small_01" },
            });
            s.compounds.Add(new CompoundSpec
            {
                name = "FillerW", type = CompoundType.OpenLot, centerXZ = new Vector2(-58f, 4f), rot = 60f,
                buildingPrefabs = new[] { "SM_Bld_WoodenShack_01" }, buildingGap = 2f,
                props = new[] { "SM_Veh_Car_Destroyed_01", "SM_Env_Rubble_Pile_02", "SM_Prop_WireFence_01", "SM_Prop_TrashBag_02", "SM_Env_Rubble_Stone_01" },
            });
            s.compounds.Add(new CompoundSpec
            {
                name = "FillerSE", type = CompoundType.OpenLot, centerXZ = new Vector2(46f, -42f), rot = 15f,
                buildingPrefabs = new string[0],
                props = new[] { "SM_Veh_Buggy_Destroyed_01", "SM_Prop_Concrete_Slab_Pile_02", "SM_Env_Rubble_Pile_01", "SM_Prop_BarrelStack_02", "SM_Prop_CardboardBox_01" },
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // §B 도로 — 폴리라인 아스팔트 유도선(끌림). roadGraph는 비워 둠.
        //   ★RoadGraph 교차 피스(Corner/T/Cross)는 에셋이 싸운다(직선 20m폭 ↔ 교차 10m폭,
        //     피벗 오프셋 cz≈10, 로컬 정면 미상) → 깨진 교차로. 견고한 폴리라인 경로로 우회:
        //     직선 타일만 겹쳐 깔아 *연속 아스팔트*, 폴리라인 교차점은 겹침으로 자연 크로스.
        //     클러스터는 *정면*에서 끝(건물 풋프린트 안으로 찌르지 않음 — 측정 AABB로 종단점 산정).
        // ─────────────────────────────────────────────────────────────────
        static void BuildRoads(MapSpec s)
        {
            // 척추(메인 유도선): LZ → 코어 야드 진입(z≈-3). 크레인(8,14)이 너머에서 looming = 끌림 종착.
            s.roadPolylines.Add(new RoadPolyline
            {
                pts = new[] { new Vector2(0f, -66f), new Vector2(3f, -44f), new Vector2(5f, -20f), new Vector2(7f, -3f) },
            });
            // 북 스퍼(코어 관통 전진축 → 북문). 야드 북쪽(z≈30)에서 시작(크레인 타워 z[10,18] 회피).
            s.roadPolylines.Add(new RoadPolyline
            {
                pts = new[] { new Vector2(8f, 30f), new Vector2(5f, 50f), new Vector2(2f, 64f) },
            });
            // 교차 집산(E-W): 창고 정면(x40) ↔ 척추 크로스(5,-12) ↔ 주거 정면(x-22). 클러스터 AABB 밖에서 종단.
            s.roadPolylines.Add(new RoadPolyline
            {
                pts = new[] { new Vector2(40f, -10f), new Vector2(14f, -11f), new Vector2(5f, -12f), new Vector2(-22f, -22f) },
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // §C.2 ★E-3 컨테이너 회랑 — 코어 야드를 감싸는 어깨맞대기 벽(데스트랩 0).
        //   두 평행 행 = 폭 5m 통과 회랑. 양끝 트임(막다른 0).
        // ─────────────────────────────────────────────────────────────────
        static void BuildCorridors(MapSpec s)
        {
            // 코어 진입 게이트릿(컨테이너 양벽) — 척추 끝(z≈-2)에서 크레인(z14)으로 빨아들이는 통로.
            //   양끝 트임(막다른 0). 내부 폭 ≈19m(x-5.75~13.75) = 카이팅 가능. Duckov식 깔때기.
            s.corridors.Add(new CorridorRowSpec
            {
                name = "GauntletWest", a = new Vector2(-7f, -2f), b = new Vector2(-7f, 12f),
                containerPrefab = "SM_Prop_Container_01", altContainerPrefab = "SM_Prop_Shipping_Container_01",
            });
            s.corridors.Add(new CorridorRowSpec
            {
                name = "GauntletEast", a = new Vector2(15f, -2f), b = new Vector2(15f, 12f),
                containerPrefab = "SM_Prop_Shipping_Container_01", altContainerPrefab = "SM_Prop_Container_01",
            });
            // 야드 뒤 차폐벽(북, z24) — 코어를 막다른 잭팟으로(전진축은 북스퍼가 우회).
            s.corridors.Add(new CorridorRowSpec
            {
                name = "YardBack", a = new Vector2(-2f, 24f), b = new Vector2(18f, 24f),
                containerPrefab = "SM_Prop_Container_01", altContainerPrefab = "SM_Prop_Shipping_Container_01",
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // §6 E-1 복합 지면 — 코어 콘크리트 바닥 + 공터 풀 패치(단색 quad 위 덮음).
        // ─────────────────────────────────────────────────────────────────
        static void BuildGroundPatches(MapSpec s)
        {
            // 베이스(Concrete_Base_01 탄 포장)가 전면을 덮으므로, 여기선 *색 변화 구역*만 — 잔디 침식 + 깨진 흙.
            s.groundPatches.Add(new GroundPatchSpec { name = "OpenLotGrass", centerXZ = new Vector2(4f, -52f), sizeXZ = new Vector2(30f, 30f), tilePrefab = "SM_Generic_Ground_02" });
            s.groundPatches.Add(new GroundPatchSpec { name = "ResidentialGrass", centerXZ = new Vector2(-44f, -36f), sizeXZ = new Vector2(34f, 28f), tilePrefab = "SM_Generic_Ground_02" });
            s.groundPatches.Add(new GroundPatchSpec { name = "DirtNW", centerXZ = new Vector2(-40f, 32f), sizeXZ = new Vector2(30f, 30f), tilePrefab = "SM_Env_Dirt_Square_01", overlap = 1.2f });
            s.groundPatches.Add(new GroundPatchSpec { name = "DirtNE", centerXZ = new Vector2(48f, 34f), sizeXZ = new Vector2(28f, 28f), tilePrefab = "SM_Env_Dirt_Square_01", overlap = 1.2f });
            s.groundPatches.Add(new GroundPatchSpec { name = "DirtSE", centerXZ = new Vector2(52f, -46f), sizeXZ = new Vector2(28f, 28f), tilePrefab = "SM_Env_Dirt_Square_01", overlap = 1.2f });
        }

        // ─────────────────────────────────────────────────────────────────
        // §E-4 식생 군집 — 주거·공터에 나무 군집, 산업엔 없음(바이블 §4).
        // ─────────────────────────────────────────────────────────────────
        static void BuildVegetation(MapSpec s)
        {
            s.vegetation.Add(new VegetationClusterSpec
            {
                name = "ResidentialYard", centerXZ = new Vector2(-44f, -36f), radius = 18f, count = 16,
                species = new[] { "SM_Generic_Tree_01", "SM_Generic_Tree_03", "SM_Generic_TreeDead_01", "SM_Generic_Tree_02" },
            });
            s.vegetation.Add(new VegetationClusterSpec
            {
                name = "OpenLotSnags", centerXZ = new Vector2(4f, -52f), radius = 18f, count = 12,
                species = new[] { "SM_Generic_TreeStump_01", "SM_Generic_TreeDead_01", "SM_Generic_Tree_04" },
            });
            s.vegetation.Add(new VegetationClusterSpec
            {
                name = "CheckpointTrees", centerXZ = new Vector2(-42f, 40f), radius = 12f, count = 8,
                species = new[] { "SM_Generic_Tree_02", "SM_Generic_Tree_04", "SM_Generic_Tree_01" },
            });
            // 외곽 차폐 식생 — 가장자리 휑함 깸.
            s.vegetation.Add(new VegetationClusterSpec
            {
                name = "NEThicket", centerXZ = new Vector2(52f, 50f), radius = 22f, count = 16,
                species = new[] { "SM_Generic_Tree_01", "SM_Generic_Tree_03", "SM_Generic_TreeDead_01" },
            });
            s.vegetation.Add(new VegetationClusterSpec
            {
                name = "SWThicket", centerXZ = new Vector2(-54f, -60f), radius = 22f, count = 16,
                species = new[] { "SM_Generic_Tree_02", "SM_Generic_TreeDead_01", "SM_Generic_TreeStump_01" },
            });
            s.vegetation.Add(new VegetationClusterSpec
            {
                name = "SEThicket", centerXZ = new Vector2(58f, -52f), radius = 18f, count = 12,
                species = new[] { "SM_Generic_Tree_01", "SM_Generic_Tree_04" },
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // §5 폐허 산포 어휘 — ruinCount 44→55(빈칸 메움).
        // ─────────────────────────────────────────────────────────────────
        static void BuildRuinScatter(MapSpec s)
        {
            s.ruinPrefabs = new[]
            {
                "SM_Env_Rubble_Pile_01", "SM_Env_Rubble_Pile_02", "SM_Env_Rubble_Stone_01",
                "SM_Env_Road_Straight_Damaged_01", "SM_Env_Road_Straight_Damaged_02",
                "SM_Veh_Car_Destroyed_01", "SM_Veh_Truck_Destroyed_01", "SM_Veh_Buggy_Destroyed_01",
                "SM_Prop_BarrelStack_02", "SM_Prop_Pallet_Loaded_01", "SM_Prop_Pallet_Loaded_02",
                "SM_Prop_CardboardBox_01", "SM_Prop_Crate_Large_01", "SM_Prop_Concrete_Slab_Pile_02",
                "SM_Prop_TrashBag_02", "SM_Prop_Container_Small_01", "SM_Prop_WireFence_01",
                "SM_Prop_Cinderblock_Wall_01", "SM_Prop_Barrier_01", "SM_Prop_TankTrap_01",
            };
            s.ruinCount = 200;
        }

        // ─────────────────────────────────────────────────────────────────
        // §E 스폰 밴드 — 동심원 LV(디제틱 출처, 카운트 ❌).
        // ─────────────────────────────────────────────────────────────────
        static void BuildSpawnMarkers(MapSpec s)
        {
            // 외곽 LV1.
            AddMarker(s, 0f, 62f, 1, SpawnRole.Ambient);
            AddMarker(s, 44f, 44f, 1, SpawnRole.Ambient);
            AddMarker(s, -44f, 44f, 1, SpawnRole.Ambient);
            AddMarker(s, -44f, -44f, 1, SpawnRole.Ambient);
            AddMarker(s, 44f, -44f, 1, SpawnRole.Ambient);
            AddMarker(s, 0f, -62f, 1, SpawnRole.Ambient);
            // 중간 LV2~3.
            AddMarker(s, 26f, 26f, 2, SpawnRole.WaveSpawn);
            AddMarker(s, -28f, 22f, 2, SpawnRole.WaveSpawn);
            AddMarker(s, -22f, -28f, 3, SpawnRole.WaveSpawn);
            AddMarker(s, 30f, -18f, 3, SpawnRole.WaveSpawn);
            AddMarker(s, 0f, 36f, 2, SpawnRole.FlyerSpawn);   // 상공 진입점
            // 코어 LV4~5.
            AddMarker(s, -10f, 14f, 5, SpawnRole.EliteSpawn); // Fulgurodonte 램 런웨이
            AddMarker(s, 8f, 2f, 4, SpawnRole.WaveSpawn);     // Venosaur 호위
        }

        static void AddMarker(MapSpec s, float x, float z, int band, SpawnRole role)
        {
            s.spawnMarkers.Add(new SpawnMarker { pos = new Vector2(x, z), lvBand = band, role = role });
        }
    }
}
#endif
