using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieCrush.MapGen
{
    /// <summary>
    /// 컴파운드(건물 클러스터) 유형 — natural-pull-doctrine §2.1 끌림 위계 + 빌드스펙 §A 컴파운드 종류.
    /// </summary>
    public enum CompoundType
    {
        CoreYard,        // 코어 거점(크레인 야드) — 잭팟·끌림 수렴점
        ResidentialRuin, // 주거 폐허
        Warehouse,       // 창고
        Outpost,         // 전초
        OpenLot,         // 개활 잔해 공터
    }

    /// <summary>스폰 마커 역할 — 디제틱 젠 *출처* 종류(빌드스펙 §디렉터, 카운트 ❌).</summary>
    public enum SpawnRole
    {
        Ambient,    // 외곽 출처(LV1 배경)
        WaveSpawn,  // 중간 출처
        EliteSpawn, // 코어 엘리트 정원 1
        FlyerSpawn, // 상공 진입점(평면 다이브 목표)
    }

    /// <summary>
    /// 도로 위계(real-game-map-grammar §1.1 / bible §2.1). 폭·타일 선택 기준.
    /// </summary>
    public enum RoadTier
    {
        Arterial,   // 간선 — 넓음(20m 아스팔트 1줄), 코어로 향하는 끌림 척추
        Collector,  // 집산 — 중간, 동심원 띠 사이 잇기(카이팅 뼈대)
        Local,      // 국지 — 좁음(흙 갓길), 블록 사이 골목
    }

    /// <summary>도로 노드 종류 — 분류는 degree(연결 엣지 수)로 *자동* 결정(bible §2.2). 데이터는 hint만.</summary>
    public enum RoadNodeType
    {
        Auto,    // degree 로 자동 분류(권장)
        End,     // degree 1
        Corner,  // degree 2 (굴절)
        T,       // degree 3
        Cross,   // degree 4
    }

    /// <summary>
    /// 건물 클러스터 1기 — *비대칭 위치*. 축정렬(랜덤 회전 ❌), 컴파운드 단위로만 rot 회전.
    /// </summary>
    [Serializable]
    public class CompoundSpec
    {
        public string name = "Compound";
        public CompoundType type = CompoundType.OpenLot;
        public Vector2 centerXZ;             // 맵 좌표(X,Z)
        public float rot;                    // 컴파운드 yaw(deg)
        public string[] buildingPrefabs;     // 한 줄 패킹할 건물(POLYGON BR/CN 이름)
        public float buildingGap = 2.5f;     // 건물 풋프린트 사이 틈(m)
        public string[] props;               // 둘레 정황 프롭(선택)
    }

    /// <summary>스폰 마커 데이터 — 동심원 LV 밴드 + 출처 종류(디렉터 후속 연동, 지금은 마커만).</summary>
    [Serializable]
    public class SpawnMarker
    {
        public Vector2 pos;     // 맵 좌표(X,Z)
        public int lvBand;      // 1=외곽 / 2~3=중간 / 4~5=코어
        public SpawnRole role = SpawnRole.WaveSpawn;
    }

    /// <summary>
    /// 한 스테이지의 전체 배치 명세 — *데이터*. ScriptableObject(에셋 저장 가능) + [Serializable](코드 인라인).
    /// CompoundMapGenerator.Generate(spec) 가 이 데이터만으로 맵을 재현(같은 seed=같은 결과).
    /// </summary>
    [CreateAssetMenu(fileName = "MapSpec", menuName = "ZombieCrush/MapGen/MapSpec")]
    public class MapSpec : ScriptableObject
    {
        [Header("Identity")]
        public string mapName = "RuinCity";

        [Header("Determinism / Extent")]
        [Tooltip("폐허 산포 등 확률 배치의 시드. 같은 시드=같은 결과.")]
        public int seed = 12345;
        [Tooltip("맵 반폭(m). 봉쇄벽이 ±mapHalf 둘레. 빌드스펙 = 75(150×150).")]
        public float mapHalf = 75f;
        [Tooltip("이음새 backstop quad 색(타일 미세 틈 보강용, 어둡게).")]
        public Color groundColor = new Color(0.62f, 0.58f, 0.5f);

        [Header("Base floor — 전면 타일 바닥(완전 바닥)")]
        [Tooltip("맵 전체를 빈틈없이 덮는 베이스 지면 타일(예: SM_Env_Concrete_Base_01=탄 포장). 빈 문자열이면 backstop quad만.")]
        public string baseFloorTile = "SM_Env_Concrete_Base_01";
        [Tooltip("히어로(랜드마크·핵심 건물·회랑)는 손배치 → 절차 생성에서 제외하고 캔버스(바닥/벽/산포/식생)만 생성.")]
        public bool heroByHand = true;

        [Header("Boundary kit (POLYGON BR/CN)")]
        public string wallPrefab = "SM_Env_Port_Wall_01";
        public string cornerPrefab = "SM_Env_Port_Wall_Corner_01";

        [Header("Road kit")]
        public string roadTilePrefab = "SM_Env_Road_Straight_01";

        [Header("Beacon — 코어 끌림(크레인 등 키 큰 비콘)")]
        [Tooltip("코어 거점에 세울 비콘 프리팹(끌림). 빈 문자열이면 생략.")]
        public string coreBeaconPrefab = "SM_Bld_Crane_01";
        [Tooltip("비콘 yaw(deg) — 붐/면이 LZ(또는 진입로)를 가리키게.")]
        public float coreBeaconRot = 180f;

        [Header("Compounds — 비대칭 거점")]
        public List<CompoundSpec> compounds = new List<CompoundSpec>();

        [Header("Roads — 굽은 메인 + 분기 폴리라인 (레거시 폴백)")]
        public List<RoadPolyline> roadPolylines = new List<RoadPolyline>();

        [Header("Road graph — ★E-2 위계 도로망(노드 자동분류 + 교차 피스). 비면 폴리라인 폴백.")]
        public RoadGraphSpec roadGraph = new RoadGraphSpec();

        [Header("Corridors — ★E-3 컨테이너 회랑(어깨맞대기 벽)")]
        public List<CorridorRowSpec> corridors = new List<CorridorRowSpec>();

        [Header("Ground patches — E-1 복합 지면(콘크리트/풀/모래 타일 격자)")]
        public List<GroundPatchSpec> groundPatches = new List<GroundPatchSpec>();

        [Header("Vegetation — E-4 식생 군집(나무/덤불 산포)")]
        public List<VegetationClusterSpec> vegetation = new List<VegetationClusterSpec>();

        [Header("Ruin scatter — 폐허 어휘(rubble·damaged road·destroyed car)")]
        public string[] ruinPrefabs =
        {
            "SM_Env_Rubble_Pile_01", "SM_Env_Rubble_Pile_02",
            "SM_Env_Road_Straight_Damaged_01", "SM_Veh_Car_Destroyed_01",
        };
        [Tooltip("폐허 산포 개수(맵 전역).")]
        public int ruinCount = 40;

        [Header("Spawn markers — 동심원 LV 밴드(디렉터 후속 연동, 지금은 마커만)")]
        public List<SpawnMarker> spawnMarkers = new List<SpawnMarker>();

        /// <summary>코어 거점(CoreYard) 중심 — 비콘/끌림 수렴점. 없으면 원점.</summary>
        public Vector2 CoreCenter()
        {
            if (compounds == null) return Vector2.zero;
            foreach (var c in compounds)
                if (c != null && c.type == CompoundType.CoreYard) return c.centerXZ;
            return Vector2.zero;
        }
    }

    /// <summary>
    /// 굽은 도로 폴리라인 — List 안에 Vector2[] 를 직접 넣을 수 없어(Unity 직렬화 한계)
    /// 1단 래퍼로 감싼다. pts = 연속 세그먼트로 깔리는 점 배열.
    /// </summary>
    [Serializable]
    public class RoadPolyline
    {
        public Vector2[] pts;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ★E-2 도로 그래프 — 노드(교차점) + 엣지(직선 구간). 위계·교차 피스의 데이터 모델.
    //   degree(노드의 연결 엣지 수)로 End/Corner/T/Cross 를 *자동* 분류하고
    //   엣지를 5m Straight 로 채운다. junction 피스는 회전·그리드 스냅·폭불일치 inset 처리.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>도로 노드 1개 — 교차점/굴절점/끝점. degree(연결 수)로 피스가 자동 결정된다.</summary>
    [Serializable]
    public class RoadNodeSpec
    {
        public string id = "n";              // 엣지가 참조할 식별자(맵 내 유일)
        public Vector2 pos;                  // 맵 좌표(X,Z). 빌더가 5m 그리드로 스냅.
        public RoadTier tier = RoadTier.Collector;
        public RoadNodeType typeHint = RoadNodeType.Auto; // Auto=degree로 분류
        [Tooltip("교차 피스 yaw 수동 보정(deg). 0=자동. 피스 로컬 방향 오프셋 교정용.")]
        public float rotOverride = 0f;       // 0이면 자동, else 강제
    }

    /// <summary>도로 엣지 1개 — 두 노드 사이 직선 구간. 위계=두 노드 중 낮은(넓은) 위계.</summary>
    [Serializable]
    public class RoadEdgeSpec
    {
        public string nodeA = "a";
        public string nodeB = "b";
    }

    /// <summary>도로 그래프 전체 — 노드 + 엣지. 비면(노드 0) RoadPolyline 폴백.</summary>
    [Serializable]
    public class RoadGraphSpec
    {
        public List<RoadNodeSpec> nodes = new List<RoadNodeSpec>();
        public List<RoadEdgeSpec> edges = new List<RoadEdgeSpec>();

        [Header("위계별 도로 타일(직선)")]
        public string arterialTile = "SM_Env_Road_Straight_01";   // 넓은 아스팔트
        public string collectorTile = "SM_Env_Road_Straight_01";
        public string localTile = "SM_Env_DirtRoad_Straight_01";  // 흙 갓길

        [Header("교차/굴절/끝 피스 (BR 도로 키트)")]
        public string cornerPiece = "SM_Env_Road_Corner_01";
        public string tPiece = "SM_Env_Road_T_01";
        public string crossPiece = "SM_Env_Road_Cross_01";
        public string endPiece = "SM_Env_Road_Straight_End_01";

        public bool HasGraph => nodes != null && nodes.Count > 0 && edges != null && edges.Count > 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ★E-3 회랑 — 컨테이너 줄 1행(중심선 a→b, 길이축 따라 어깨맞대기 적층).
    //   한 회랑 = 두 평행 행(±offset)이 통과 회랑을 형성. 데스트랩 0 은 데이터(양끝 트임) 책임.
    // ─────────────────────────────────────────────────────────────────────

    [Serializable]
    public class CorridorRowSpec
    {
        public string name = "Corridor";
        public Vector2 a;                    // 행 시작(X,Z)
        public Vector2 b;                    // 행 끝(X,Z)
        public string containerPrefab = "SM_Prop_Container_01";
        public string altContainerPrefab = "SM_Prop_Shipping_Container_01"; // 색 다양(번갈아). 비면 단일.
        [Tooltip("컨테이너 사이 틈(m). 0=어깨맞대기.")]
        public float gap = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────
    // E-1 복합 지면 — 사각 영역을 타일 프리팹 격자로 채움(콘크리트 바닥·풀 패치).
    // ─────────────────────────────────────────────────────────────────────

    [Serializable]
    public class GroundPatchSpec
    {
        public string name = "Patch";
        public Vector2 centerXZ;
        public Vector2 sizeXZ = new Vector2(20f, 20f);
        public string tilePrefab = "SM_Bld_Concrete_Floor_01";
        [Tooltip("타일 간 오버랩(m) — 0.01u 갭(NavMesh #1 버그) 방지.")]
        public float overlap = 0.5f;
    }

    // ─────────────────────────────────────────────────────────────────────
    // E-4 식생 군집 — 중심·반경 안에 종(species) 중에서 결정론 산포.
    // ─────────────────────────────────────────────────────────────────────

    [Serializable]
    public class VegetationClusterSpec
    {
        public string name = "Veg";
        public Vector2 centerXZ;
        public float radius = 8f;
        public int count = 6;
        public string[] species = { "SM_Generic_Tree_01", "SM_Generic_Tree_02" };
    }
}
