# Synty 키트 모듈 문법 (BR + Construction) — 전수 파악

> **작성**: 2026-06-14 · 출처: `Assets/Synty/PolygonBattleRoyale/Prefabs/` + Construction 프리팹 전수(Glob) + 실측 치수(이전 측정). Codex 권한 대기로 오케 직접 수행.
> **목적**: 우리가 "직선 타일만 줄로" 깔다 망한 도로를, 키트의 *실제 연결 피스*로 제대로 물리게.

## 0. 우리 최대 실수
**도로를 `Road_Straight`만 줄로 깔았다.** 키트엔 코너·T·교차로·끝막이가 다 있는데 안 썼다 → 도로망이 아니라 막대기.

## 1. 도로/환경 피스 (BR Environments)
- **도로 시스템(★전부 써야)**: `SM_Env_Road_Straight` · `_Corner` · `_T` · `_Cross` · `_Straight_End`(끝막이) · `_Straight_Damaged`(폐허). + `SM_Env_DirtRoad_Straight/End`(흙길).
  - → 굽힘=Corner, 합류=T, 교차=Cross, 막다른=End. 직선만 ❌.
- **그리드 모듈(실측)**: 도로 Straight 20폭×**5깊이** → 진행축 5m 적층. 수평 그리드 = **2.5m(Build)/5m(City·도로·지면)**, 수직 **3m** 배수(외부 리서치 정합).
- **경계**: `SM_Env_Port_Wall` · `_Corner` · `Dock` · `Beach` · `Concrete_Base`.
- **지면**: `Concrete_Base` · `Grass_Circle/Square`(+Construction 콘크리트 바닥·Sand_Ground). 단색 쿼드 ❌.
- **폐허**: `SM_Env_Rubble_Pebbles/Pile/Plank/Stone` · `Bridge_Broken`.

## 2. 건물 (BR — 적음 → Construction으로 보강)
- **BR**: `House` · `House_Glass` · `SmallBuilding` · `Warehouse` · `WoodenShack` · `Tent` · `Window_Bars`(부착). = 종류 적음.
- **보강(Construction)**: 콘크리트 벽/바닥/기둥/프레임(모듈러 산업 셸), 크레인, 중장비 → *산업 야드·폐허 구조물*. (실측: House_01 11×10.4, House_02 13.4×12.5, House_03 5.6×10.6, SmallBuilding_03 10.7×7.1, Warehouse_01 21.7×22.6, 전부 pivot 중심·base 0)
- ★의미: House/Shack=주거, SmallBuilding=상가/잡, Warehouse+Construction=산업. 군집 정체성에 맞게.

## 3. ★밀도 소품 (BR Props — 내가 0개 쓴 곳)
방대함 = 밀도의 재료: `Barrel` · `Crate(_Large/Small/Medical)` · `Pallet(_Stack/Loaded)` · `Barricade` · `Barrier(_Dirt)` · `Cinderblock(_Wall)` · `Container(_Small)` · `Generator` · `GuardTower` · `AmmoBox` · `CardboardBox` · `Iron_Sheet` · `BaseWall(_Broken/Post)` · `EmergencyDrop(_Crate)` 등 + Vehicles 폴더(승용/트럭/버스 파손 다수).
- → 건물 사이·도로변·야드를 *이걸로 꽉* 채워야 진짜감. + 식생=LMHPOLY 나무/덤불 + Grass/Rock.

## 4. 한 맵에 필요한 최소 세트 (체크)
- 도로: Straight + **Corner + T + Cross + End**(필수, 내가 빠뜨림) + Damaged(폐허)
- 지면: 도로 타일 + 사이드워크/콘크리트/모래 패치(복합)
- 건물: 주거(House/Shack ×색다양) + 상가(SmallBuilding) + 산업(Warehouse+Construction)
- 경계: Port_Wall/Corner (+ 자연=Beach/Dock 선택)
- 밀도: Barrel/Crate/Pallet/Barricade/Container/Generator + 파손차량 + 식생 **다수**
- 폐허: Rubble ×4 + Bridge_Broken + Destroyed 차량
- 코어: Crane(Construction) = 비콘

## 5. 다음
이 문서 + `synty-demo-layout-analysis.md`(데모 추론) + `real-game-map-grammar.md`(실제·게임 문법) 셋을 LevelDesign이 **맵 구축 바이블 + 한 칸 한 뼘 블루프린트**로 종합 → 치수/특성 검증 → MapGen 엔진으로 배치.
