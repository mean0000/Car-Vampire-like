---
name: mapgen-engine-pattern
description: 데이터-드리븐 맵젠 엔진(MapGen/) 리뷰 — Phase1 풋프린트축전제·flush순서·결정론. Phase2(E-1/E-3/E-4+RoadGraph)에서 ★식생 GetHashCode 결정론깨짐·GroundPatch overlap 폭주 신규. 이전 누수/Find 지적은 교정됨
metadata:
  type: project
---

`Assets/_Project/Scripts/MapGen/` = 이 프로젝트 첫 데이터-드리븐 맵 빌더(RunCommand 손배치를 재사용 코드로 승격). 5파일: MapGenKit(static DSL)·MapSpec(SO+직렬화 데이터)·MapGenSpawnMarker(MonoBehaviour)·CompoundMapGenerator(메뉴 빌더)·RuinCitySpec(코드 빌드 스펙). 메뉴=`Tools/MapGen/Generate Map`.

**Why:** 손던지던 배치를 시드 결정론·치수정합·재사용 코드로 만든 폐허 도심 맵젠. 헌법=natural-pull-doctrine §3(단일 전투평면, bounds.min.y→y=0 flush), asset-driven-map-composition §1(POLYGON BR+CN 가족만, 동명충돌은 폴더로 해소).

## Phase2 (E-1/E-3/E-4 + RoadGraph, 06-14) — 신규 위험
- **★H-1 식생 결정론 깨짐 = `string.GetHashCode()`** — `VegetationCluster`(MapGenKit.cs:496)가 `seed ^ v.name.GetHashCode()`로 시드 분기. 최신 .NET/Mono는 string.GetHashCode가 **프로세스마다 무작위화** → 에디터 재시작 시 식생 산포 패턴 변함. "같은 seed=같은 결과" 헌법 위반. **ScatterRuins는 `new System.Random(seed)`만 써서 안전** — 식생만 비대칭으로 깨짐. 수정=안정 해시(FNV/문자합) 또는 군집 인덱스를 시드로.
- **★M-1 GroundPatch overlap 무상한 폭주** — `overlap`이 타일 폭 이상이면 `stepX=Max(0.1f, tw-ov)`가 0.1로 바닥 클램프 → `nx=CeilToInt(size/0.1)` 폭증(28m→280칸, nx*nz≈78k 타일 같은자리 겹침). 음수만 막고 상한 없음. 현 RuinCitySpec 데이터는 안전(overlap default 0.5). 손배치 patch 추가 시 발화. 수정=overlap Clamp(0,tw-0.1) 또는 nx*nz 상한 가드.
- **M-2 짧은 구간 마지막타일 clamp가 구간 밖 배치** — Road/CorridorRow에서 `dist<step*0.5`면 count=1인데 보정식 `Max(unitLen*0.5, dist-unitLen*0.5)`가 구간 밖 along을 낳음. 견고성 결함. `count==1→along=dist*0.5` 선처리로 해소. 현 데이터(14~20m 회랑)는 안전.
- **M-3 Compound 깨진 건물이름 = 유령 간격** — FindPrefab null이면 Footprint fallback 4m로 cursor 전진하나 Spawn은 null → 4m 빈칸. `depths[]` 배열은 측정만 하고 미사용(dead store).

## Phase1 위험 (여전히 유효)
- **★도로/벽 타일 길이축 전제** — `Road`는 `fp.size.z`를 진행길이로, `yaw=Atan2(dir.x,dir.z)`(+Z기준) 정렬. 타일 length축이 로컬 +Z 아니면 누워서 N배 겹침. **정적 리뷰로 메시 bounds 실측 불가 → 에디터 1회 Generate 육안 확인이 동결 게이트.** (현 1차 스펙은 RoadGraph 비활성·폴리라인 사용 — RoadGraph 교차피스 회전은 보류 경로라 회전정확도 리뷰 스킵.)
- **Spawn 부모 회전 무효(데드 코드)** — `SetParent(parent,false)` 후 월드 `.position`/`.rotation` 절대 덮어씀 → 부모 회전 자식 무영향. Compound는 `TransformPoint`로 위치 수동회전+일괄 c.rot라 *우연히* 정합. local 배치 리팩터 시 이중변환 무음 폭발.
- **seed 경계** — `new System.Random(int.MinValue)` 예외(Abs 오버플로). 현 스펙 양수라 무해.

**검증된 안전 전제(재플래그 금지):**
- 에디터/런타임 타입 분리 정확 — MapSpec(SO)·MapGenSpawnMarker는 #if UNITY_EDITOR *밖*, 나머지 3개는 안. MapSpec.asset 만들어도 빌드 안 깨짐. 빌드 무결성 통과.
- **★교정됨(이전 노트 stale): Footprint 임시 인스턴스 누수 = try/finally로 파기 보장(108-111). Clear = `GameObject.Find` 아니라 `MapGenRoot` 마커로 식별 삭제(동명 오삭제 방지). new Material = MapGenRoot.RegisterRuntimeMaterial 등록 후 OnDestroy 파기.** 이 3건은 v1 리뷰 지적 후 수정 완료 — 재플래그 금지.
- `Generate` 진입 시 ClearCaches+ClearGenerated 선행 → 누적/중복/stale 차단, 멱등(두번 호출=맵1개).
- RoadGraph 가드 촘촘 — 중복id·self-loop·미해소노드·0거리엣지 전부 경고후 skip, NRE 없음. nodeInset 전 노드 채운 뒤 엣지참조라 KeyNotFound 없음.
- 0거리/0벡터 나눗셈 가드 완비 — Road·CorridorRow·RoadGraph엣지·BuildWallRun 전부 dist<0.01 / sqrMagnitude<0.01 선검사.
- ScatterRuins 결정론 정확(System.Random(seed) 단일 순차소비). WiderTier 정합(Arterial0<Collector1<Local2, 작은인덱스=넓은도로 채택).
- FindPrefab 가족폴더 한정+`GetFileNameWithoutExtension==name` 정확일치 dedup.

가족 루트 실존: Assets/Synty/PolygonBattleRoyale, PolygonConstruction (+ Generic/Nightclubs/Western/Helper 배제).
