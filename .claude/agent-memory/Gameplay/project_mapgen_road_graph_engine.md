---
name: mapgen-road-graph-engine
description: MapGen E-2~E-4 엔진 확장(도로그래프·회랑·복합지면·식생) 구조 + 도로 교차 피스 회전 보정 함정
metadata:
  type: project
---

MapGen 엔진을 "직선 타일+단색 쿼드"에서 위계 도로망으로 업그레이드(2026-06-14, 집하장 블루프린트 구현).

**4대 확장 (Assets/_Project/Scripts/MapGen/):**
- **E-2 RoadGraph** = `MapSpec.roadGraph`(RoadGraphSpec: nodes id/pos/tier + edges nodeA/nodeB). `MapGenKit.RoadGraph()`가 노드 **degree(연결 엣지 수)로 자동 분류**: 1=End, 2=Corner, 3=T, 4+=Cross. 엣지를 위계별 5m Straight로 채우고 노드에 교차 피스 배치. 5m 그리드 스냅(`GridStep=5`), junction inset(피스 풋프린트 반경만큼 엣지 안쪽부터 채워 겹침 최소화). 기존 `RoadPolyline()` 직선 적층은 폴백 보존(roadGraph 비면).
- **E-3 CorridorRow** = `MapSpec.corridors`. 컨테이너 어깨맞대기 벽. 두 평행 행=통과 회랑.
- **E-1 GroundPatch** = `MapSpec.groundPatches`. 콘크리트/풀 타일 격자를 단색 quad 위에 덮음(overlap 0.5m=NavMesh 0.01u 갭 방지).
- **E-4 VegetationCluster** = `MapSpec.vegetation`. 결정론 원 산포(seed^name.GetHashCode로 군집별 분기).

**★최대 함정 — 도로 교차 피스 회전 미상:**
Synty 도로 피스(Corner/T/End)의 **로컬 "정면" 방향을 코드로 못 읽음**(Bash 막힘+YAML 역공학 회피). 회전은 인접 엣지 방향에서 계산(End=엣지 반대, Corner=bisector, T=스템방향=관통선 대칭축, Cross=90°대칭이라 무관)하되, **피스별 base 오프셋 상수**(`MapGenKit.CornerBaseRot/TBaseRot/CrossBaseRot/EndBaseRot`, 전부 0f 초기값)로 **캡처 1회 후 일괄 보정** 전제. 노드별 `rotOverride`(0=자동, else 강제)도 있음.
**Why:** 피스 정면을 모른 채 데이터로 짜야 했음. **How to apply:** Generate Map 후 캡처 보고 Corner/T가 틀어졌으면 이 4개 상수 중 하나(보통 90/180 배수)만 고치면 전 노드 교정. 개별 노드만 틀리면 rotOverride.

**검증 상태:** 컴파일 클린(구조). 라이브 빌드(메뉴 Tools/MapGen/Generate Map)는 오케스트레이터 전담. 스펙 프리팹 전부 owned 디스크 확인(BR+CN 가족). LMHPOLY 나무는 FamilyRoots 밖이라 FindPrefab 미해소 → E-4는 Synty Generic Tree 사용(가족 폴더 안). LMHPOLY 쓰려면 MapGenKit.FamilyRoots 확장 필요.

**집하장 스펙(RuinCitySpec.Build):** 5클러스터(코어야드·주거·검문소=Outpost·창고·공터)+3회랑+도로그래프(11노드: Cross 1/Corner 1/T 3/End 6, 5피스 전부 사용)+복합지면 3+식생 3군집+ruinCount 55. seed 7741, mapHalf 75.
