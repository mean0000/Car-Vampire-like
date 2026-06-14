# 맵 구축 바이블 + 첫 맵 블루프린트 — 폐허 도심 (집하장)

> **작성**: 2026-06-14 · **에이전트**: LevelDesign(lv) · **상태**: 설계 제안(유저 판정 대기) — 설계 문서만, 에디터/MCP/씬 ❌(빌드·캡처는 오케스트레이터 전담)
> **트리거**: "무지성 배치"로 맵을 망쳐 유저 질책 → *제대로 된 설계도*. 임의설계 ❌ — 아래 3 분석 + 기존 권위에만 근거.
> **종합 입력(반드시 동시 참조)**:
> 1. `docs/02_logs/2026-06-14-synty-demo-layout-analysis.md` — Synty 데모 추론(밀도·클러스터 정체성·도로 시스템·세트드레싱·식생)
> 2. `docs/02_logs/2026-06-14-real-game-map-grammar.md` — 실제/게임 문법(도로 위계·블록 ≤150m·건물 의미·Hades 패턴·그리드 2.5/5m·★§6.6 안티패턴 체크리스트)
> 3. `docs/02_logs/2026-06-14-synty-kit-grammar.md` — 키트 피스(Corner/T/Cross/End 다 써야·실측 치수·밀도 소품)
> **상위 권위**: `docs/00_authority/2026-06-14-natural-pull-doctrine.md`(끌림·단일평면·동심원·스폰밴드) · `docs/02_logs/2026-06-14-asset-driven-map-composition.md`(POLYGON BR+CN 단일 가족·폐허 어휘·Toon/PBR 배제)
> **엔진**: `Assets/_Project/Scripts/MapGen/`(MapSpec·CompoundMapGenerator·MapGenKit). **본 블루프린트는 이 엔진의 데이터 구조(CompoundSpec/roadPolylines/spawnMarkers)에 매핑된다.**
> **계승/교체**: `2026-06-14-first-stage-build-spec.md`의 **집하장(맵 A)** 정체성(회랑 코너 왈칵·크레인 비콘·보급 잭팟·디렉터)을 **계승**. 단 그 스펙엔 **도로망·클러스터 정체성·밀도/식생·블록 서브분할이 없었다**(=무지성의 잔재) → 본 문서가 그 빈칸을 채워 *교체*한다. 분수광장(맵 B)은 본 문서 검증 후 같은 방법론으로 후속.

---

# 0. 한 장 요약 (TL;DR)

**우리가 망친 6가지를 이 바이블이 하나씩 잠근다:**

| 실패 | 바이블 처방(§) | 블루프린트 적용 |
|---|---|---|
| ① 도로 = Straight만 줄로 | §2 도로 시스템(Corner/T/Cross/End 의무) | §B 도로망(굽은 메인 + 분기 + 교차 노드 명시) |
| ② 밀도0·소품0·식생0 | §5 밀도 의무(빈칸 금지) | 각 클러스터 밀도 레시피 + §B.7 식생/폐허 산포 |
| ③ 건물 띄엄띄엄·정체성0 | §4 클러스터 정체성 카탈로그 | §B 6개 클러스터 각자 정체성·건물·소품 |
| ④ 단색 쿼드 지면 | §6 복합 지면 | §B.8 + ★엔진 확장 E-1(복합 타일) |
| ⑤ 단조 색·종류 | §4 색/형태 다양 레시피 | 클러스터별 건물·색 다양 명시 |
| ⑥ 블록 스케일 안 맞음 | §3 블록 서브분할(30~50m) | §B 블록 격자 30~40m + 카이팅 레인 |

**핵심 한 줄**: *밀도가 "진짜감"의 본체다. 도로는 위계로, 건물은 의미로, 빈칸은 소품·식생·폐허로 채운다. 모든 치수는 Synty 그리드(수평 2.5/5m, 수직 3m)의 배수.*

---

# 파트 1: 맵 구축 바이블 (폐허 도심 전용 원칙)

> 우리 게임 = 탑다운 45°/15m · 서바이버즈 농성 · 카이팅 · 단일 전투 평면 · POLYGON(BR+CN) 단일 가족. 현실 도시 문법을 *우리 카메라/루프에 환산*해 쓴다(real-game-map-grammar §6).

## §1. 불변식 (위반 금지 — 권위 계승)

- **단일 전투 평면**: 모든 적·플레이어·조준 Y=0 한 평면(natural-pull §3). 키 큰 물체 = 비콘 실루엣/오클루전 전용, **윗면 워크어블 차단**. 등반·옥상 사격 ❌.
- **바운디드 존 ≈150×150m**(mapHalf=75). 동심원 LV: 외곽 r55~74 LV1 / 중간 r22~50 LV2~3 / 코어 r0~22 LV4~5.
- **데스트랩 0**: 모든 포켓·회랑·골목 ≥2 출구, 각 출구 폭 ≥3m. 막다른 길 0.
- **한 셰이더 가족만**: POLYGON BattleRoyale + Construction(GUID `0730dae3…`). Toon City(다른 셰이더+폐허 0개)·PBR Post-Apoc(톤충돌) **한 조각도 금지**(asset-driven §1·6).
- **비균일 스케일 ❌**: 회전만, scale 1(NavMesh 오베이크 + 비주얼 왜곡 방지). 지면 Y=0. 스폰 마커 ≥1.5m 분리.
- **임의설계 ❌**: 모든 주요 선택은 위 3 분석 + 권위에 출처.

## §2. 도로 시스템 사용법 (★최대 실패 지점 — 피스별 언제 쓰나)

> real-game-map-grammar §1.1 도로 위계 + synty-kit-grammar §1. **도로를 깔 때 먼저 묻는다: "이건 무슨 위계? 이 노드서 위계가 어떻게 바뀌나?"** 다 같은 굵기 = 죽은 격자(안티패턴).

### 2.1 위계 → 우리 끌림 척추 (real-game §6.2)

| 위계 | 역할(우리 게임) | 폭 | 피스 | 어디에 |
|---|---|---|---|---|
| **간선 (Arterial)** | 코어로 향하는 **끌림 척추**. 시선·발을 비콘으로 꺾는 수렴선. | 넓음(아스팔트 1줄=20m 타일) | `Road_Straight` 본선 + 굽이=`Road_Corner` | 외곽 진입(림) → 코어. 1줄. |
| **집산 (Collector)** | 동심원 띠 사이를 잇는 환상/방사. **카이팅 루프의 뼈대.** | 중간 | `Road_Straight` + `Road_T`(간선서 분기) | 코어 ↔ 각 클러스터. 2~3줄. |
| **국지 (Local)** | 블록 사이 골목. "건물 가렸다 골목 끝 왈칵"의 무대. | 좁음 | `DirtRoad_Straight`(흙 갓길) + 잔해 | 클러스터 내부·블록 사이. 다수. |

### 2.2 교차 노드 = *위계가 바뀌는 지점* (synty-kit §1)

- **`Road_Cross_01`** (십자) — 두 동급 도로 직교 교차 = **동심원 교차 노드(카이팅 분기점)**. 격자의 기본 노드. 광장 중앙에도.
- **`Road_T_01`** (T자) — 위계가 *바뀌는* 곳. 간선에서 집산이 갈라지거나 집산에서 국지가 붙는 분기. **자연스러운 동선 결정 지점.**
- **`Road_Corner_01/02`** — 도로를 *굽힌다*. 간선이 직선이면 단조 → 굽혀서 "사선 공개"(natural-pull §2.4) 드라마. 굽이 안쪽이 코너 건물 자리.
- **`Road_Straight_End_01`** (끝막이) — 막다른 도로의 *시각적 마감*. ⚠️단, 게임플레이상 막다른 골목은 데스트랩 → End는 **벽에 붙은 짧은 잔도(殘道)·잔해로 끊긴 도로** 연출에만(워크어블은 항상 ≥2 출구로 트임).
- **`Road_Straight_Damaged_01~03`** — 폐허 어휘. 간선/집산 일부 구간을 교체 = "버려진 대로"(asset-driven §3.1).
- **`DirtRoad_Straight_01` + `DirtRoad_End_01`** — 모래/흙 갓길(데모의 "좌우 베이지 흙길=외곽"). 아스팔트 본선 양옆에 깔아 "길이 살아있게". ⚠️흙길 Corner/T/Cross는 BR에 없음 → 굽이가 필요하면 Construction `SM_Env_DirtRoad_Corner_01` 사용 or 아스팔트로.

> **규칙**: 한 맵에 **Straight·Corner·T·Cross·End·Damaged 6종 + DirtRoad 갓길**이 *전부* 나와야 한다. Straight만 쓰면 그게 무지성(synty-kit §0).
>
> **★엔진 현실(E-2 참조)**: 현재 `MapGenKit.RoadPolyline`은 *단일 타일 1종*(`roadTilePrefab`)을 세그먼트에 적층할 뿐, 폴리라인 굴절점에 Corner를, 교차점에 Cross/T를 자동 배치하지 **않는다.** → 본 바이블의 도로 위계는 **엔진 확장 E-2가 선행돼야 데이터로 완전 실현**. 그 전까지는 폴리라인 = 직선 적층(굽이는 폴리라인 점으로 근사), 교차 피스는 별도 컴파운드/수동 배치.

## §3. 블록 서브분할 (카이팅 30~50m — ⑥ 스케일 교정)

> real-game §1.3 + §6.1: 우리 존 150×150m = 현실 블록 약 1~2개. → "도시"가 아니라 **"한 구획(a few blocks)"**. 현실 보행 블록 60~180m이지만 **우리는 카이팅 레인·교차로 밀도가 필요** → 블록을 **한 변 30~40m**로 잘게 썬다.

- **블록 격자 = 30~40m 한 변**(natural-pull 카이팅 링 r13/r36/r62 사이에 블록이 들어가게). 5m 그리드의 6~8칸.
- **블록 사이 = 카이팅 레인(국지 도로 + 갓길)**, 폭 **5~8m**(대시 카빙 1회로 관통, 호드를 한 줄로 압축). 회랑형은 5m, 개활 진입은 8m.
- **블록 내부 = 클러스터 1기**(§4). 블록 가장자리에 건물이 정면을 레인으로 향해 앉고, 블록 안쪽(rearyard)은 서비스·매복 포켓·캐시.
- **검증**: 카메라 가시 Ø30m → 한 화면에 **블록 1~2개 + 레인 1~2개 + 교차 노드 1개**가 들어와야 "읽힌다". 블록이 50m 넘으면 한 화면에 한 블록만 = 카이팅 레인 죽음(안티패턴).

## §4. 클러스터 정체성 카탈로그 (③·⑤ 교정 — 각자 건물·소품·밀도 레시피)

> Synty 데모 교훈: 건물을 *군집*으로 묶고 각 군집이 정체성. real-game §2·§3 건물 의미 + asset-driven §3 owned 매핑. **각 클러스터 = `CompoundSpec` 1기**(centerXZ·rot·buildingPrefabs·props).

> ⚠️ **frontage 규약**: 건물 정면이 인접 도로(레인)를 향하도록 `CompoundSpec.rot`을 잡는다. 엔진은 컴파운드 단위로만 회전(건물 개별 랜덤 회전 ❌, 축정렬) — 한 줄 패킹된 건물 정면이 *연속 가로벽*을 이루게.

### 카탈로그 (6 정체성 — POLYGON BR+CN 한 가족, 전부 owned ✓ 검증)

| 정체성 | 의미(real-game §3) | 건물(buildingPrefabs) | 밀도 소품(props) | 식생 | 셋백·배치 |
|---|---|---|---|---|---|
| **A. 주거 줄 (ResidentialRuin)** | 작은 풋프린트, 앞마당 셋백, 동네 군집, 조용·저밀도 | `SM_Bld_House_01`·`_02`·`_03`·`SM_Bld_WoodenShack_01`(색·형태 다양) | `SM_Veh_Car_Destroyed_01`·`SM_Env_Rubble_Pile_01/02`·`SM_Prop_WireFence_01/02`(마당 펜스)·`SM_Prop_TrashBag_02`(CN) | `SM_Generic_Tree_01~04`·`TreeDead_01`(CN) 마당 군집 | 셋백 中(앞마당). 레인 향 정렬, 일정 셋백 = 가로벽. **외곽 LV1.** |
| **B. 산업 야드 (CoreYard)** | 큰 단순 매스(비콘 실루엣), 적재 독·컨테이너 마당·트럭 court가 *같이* 와야 읽힘 | `SM_Bld_Warehouse_01` + 비콘 `SM_Bld_Crane_01`(CN) | `SM_Prop_Shipping_Container_01`(CN)·`SM_Prop_Container_01`(BR) 색 스택 사각 야드 + `SM_Prop_Floodlights_01`(CN)·`SM_Prop_Pallet_Loaded_01/02`·`SM_Prop_BarrelStack_02`(CN)·`SM_Prop_Generator_01` | (산업=식생 적음, 잡초 `SM_Env_Grass_Square_01` 균열만) | 셋백 0(독에 바짝). 컨테이너=어깨맞대기 격자(synty-placement). **코어 LV4~5 잭팟.** |
| **C. 창고 (Warehouse)** | 매우 깊은 매스, 적재 독, 트럭 마당, 서비스 도로 | `SM_Bld_Warehouse_01` + `SM_Bld_Concrete_Floor_01~04`(CN 바닥독) | `SM_Veh_Truck_Destroyed_01`·`SM_Prop_Pallet_Loaded_01`·`SM_Prop_Crate_Large_01`·`SM_Prop_BarrelStack_02`(CN)·`SM_Prop_Container_Small_01` | `TreeDead_01` 1~2 | 셋백 0, 뒤=서비스 레인. 트럭 court가 매복 포켓. **중간~코어.** |
| **D. 상가 묶음 (Outpost/상업)** | 좁은 정면+깊은 안쪽, 셋백 0, 어깨 맞댄 연속 가로벽, 코너 강조 | `SM_Bld_SmallBuilding_01`·`_02`·`_03`(어깨맞댐) + `SM_Bld_House_Glass_01`(코너 강조) | `SM_Prop_Barricade_01`·`SM_Prop_Roadblock_02`(CN)·`SM_Prop_CardboardBox_01`·`SM_Prop_Crate_01`·`SM_Prop_Rubbish_*` | 가로수 `SM_Generic_Tree_02` 1~2 | 셋백 0=연속 가로벽. 코너 건물=교차로 강조. **중간 LV2~3.** |
| **E. 검문소/전초 (Outpost)** | 응급 봉쇄·바리케이드, 작은 가설물 | `SM_Bld_Portable_Office_01/02`(CN 컨테이너 사무실) + `SM_Prop_GuardTower_01` | `SM_Prop_Barrier_01~04`·`SM_Prop_TankTrap_01`·`SM_Prop_Cinderblock_Wall_01`·`SM_Prop_Sandbag`류·`SM_Veh_Light_Armored_Car_01` | (없음) | 도로 가로질러 = 검문 게이트. **외곽~중간 진입 통제 연출.** |
| **F. 개활 잔해 공터 (OpenLot)** | 건물 없음, 잔해만. 카빙 개활 무대 | (없음) | `SM_Veh_Car_Destroyed_01`·`Buggy_Destroyed_01`·`SM_Env_Rubble_Pile_01/02`·`Rubble_Stone_01/02`·`SM_Prop_Concrete_Slab_Pile_02`(CN) | `TreeStump_01`·`TreeDead_01` 산발 | 셋백 없음. 360° 카빙 레인. **카이팅 떨치기 공간.** |

> **색·형태 다양(⑤)**: 주거는 House_01/02/03 + Shack를 *섞어* 지붕·매스 다양(데모 "지붕색 빨강/노랑/회색"). 컨테이너는 BR+CN 한 가족이라 *색만* 섞어 적층(asset-driven §6.3). 같은 프리팹 1종 반복 = 단조 = 무지성.

## §5. 밀도 의무 (② 교정 — 빈칸 금지)

> Synty 데모 최대 교훈: **건물 사이 모든 틈을 소품 더미로 메운다. 화면에 빈 공간이 거의 없다.** 밀도가 진짜감의 본체.

- **빈 베이지 금지**: 클러스터 *둘레*(props 링) + 클러스터 *사이*(ScatterRuins) + 도로변(갓길 잔해)을 채운다.
- **밀도 역구배**(natural-pull §1 동심원): 외곽 폐허 *성김* → 중간 컨테이너/파사드 *조밀* → 코어 잭팟 *집중*. (밀도 자체가 끌림 그라디언트.)
- **3겹 채우기 도구**:
  1. **클러스터 props**(엔진: `CompoundSpec.props` 링 배치) — 정황(드럼·팔레트·바리케이드·잔해).
  2. **전역 폐허 산포**(엔진: `ScatterRuins`, 결정론 seed) — rubble·damaged road·destroyed car를 맵 전역에. `ruinCount` = **블록 빈칸 메울 만큼**(첫 맵 권고 ~55, §B.7).
  3. **식생 군집**(나무/덤불/잡초) — 모래 바닥을 깨고 생기·차폐. 주거·공터에 군집, 산업엔 균열 잡초만.
- **★walkable 밀도 가드(NavMesh)**: 소품으로 꽉 채우되 **통과 간격 ≥3m**(agent 회피). ~1 agent/15㎡ 규칙 — 소품이 walkable을 너무 먹으면 카이팅 데드락. 밀도는 *시각*, 통로는 *항상 ≥3m 트임*.

## §6. 지면 (④ 교정 — 복합 타일)

> Synty 데모: 지면 = 텍스처(모래/흙) + 도로/사이드워크/풀 패치 복합. 평평 단색 쿼드 ❌.

- **베이스**: 폐허 베이지/회갈 — 현재 엔진은 단색 quad(`groundColor`). **이것만으론 ④ 미해결** → ★엔진 확장 E-1(복합 타일) 필요.
- **복합 레이어(E-1 후 or 수동)**:
  - 코어 야드/창고 = `SM_Bld_Concrete_Floor_01~04`(CN) 콘크리트 바닥 격자 + `SM_Env_Concrete_Base_01`(BR) 가장자리.
  - 도로 = 아스팔트 타일(§2) + `DirtRoad` 갓길.
  - 풀 패치 = `SM_Env_Grass_Circle_01/02`·`Grass_Square_01`(BR) 산발(공터·주거).
  - 균열/먼지 = `SM_Env_Road_Straight_Damaged_*` 끼움 + `SM_Env_Dirt_Pile_02/03`(CN).
- **전부 Y=0**(단차 0). 타일 간 0.5m 오버랩(0.01u 갭 = NavMesh #1 버그).

## §7. ★우리판 "무지성 배치" 안티패턴 체크리스트 (깔 때마다 확인)

> real-game §6.6을 우리 폐허 도심에 특화. **하나라도 체크되면 개판 — 다시 깐다.**

- [ ] 도로가 전부 `Road_Straight` (Corner/T/Cross/End/Damaged 0종) → §2 위반
- [ ] 블록 한 변 > 50m (카이팅 레인 죽음, 한 화면에 한 블록) → §3 위반
- [ ] 건물이 도로/레인 안 보고 아무 방향 (`CompoundSpec.rot`이 frontage 무시) → §4 위반
- [ ] 같은 건물 1종만 반복 (색·매스 단조) → §4·§5 위반
- [ ] 창고/야드를 컨테이너·독·트럭 court 없이 단독 박스로 → §4 위반
- [ ] 클러스터 props 0 / ScatterRuins 0 / 식생 0 (빈 베이지) → §5 위반
- [ ] 키 큰 물체(크레인·타워)가 플레이 중앙 평면에 (시야 차단) → §1 위반
- [ ] 소품을 흩뿌림(의미 군집 아님) or 통과 간격 < 3m (NavMesh 데드락) → §5 위반
- [ ] 비콘이 안 보이거나 간선이 코어로 안 꺾임 → natural-pull §2.3 위반
- [ ] 막다른 포켓·회랑(출구 1개) → 데스트랩 위반
- [ ] Toon City / PBR 프리팹이 씬에 1개라도 (셰이더 클래시) → asset-driven §1 위반
- [ ] 비균일 스케일 or Y≠0 프리팹 (NavMesh 오베이크) → §1 위반

---

# 파트 2: 첫 맵 블루프린트 — 집하장 (RuinCity_Compound_01)

> **한 줄**: 굽은 간선(흙 갓길)을 따라 폐허 주거 줄·검문소·상가를 지나 컨테이너 회랑 미로로 빨려들고, 마지막 코너를 도는 순간 본사 보급 야드(크레인 비콘+컨테이너 잭팟)가 **왈칵** 터진다. 공개 드라마 = 왈칵(최강).
> **계승**: first-stage-build-spec 집하장 정체성(회랑·크레인·잭팟·디렉터). **추가(빈칸 채움)**: 도로 위계망·6 클러스터 정체성·밀도/식생·블록 30~40m 서브분할.
> **모든 좌표 = (X,Z) 미터, Y=0. 원점=맵 중심. mapHalf=75 → X,Z ∈ [−75,+75].**
> **모든 프리팹 = owned 검증 완료**(Glob 디스크 확인, 2026-06-14). 엔진 `FindPrefab`이 BR+CN 가족 폴더서 해소.

## §A. 좌표·동심원·블록 격자 규약

- **동심 밴드**: 코어 LV4~5 **r0~22** / 중간 LV2~3 **r22~50** / 외곽 LV1 **r55~74** / 봉쇄벽 **r74**. (r50~55 완충)
- **카이팅 링**: 림 r62 / 중간 r36 / 코어 r13.
- **블록 격자**: 한 변 **30~40m**(§3). 카이팅 레인 폭 **회랑 5m / 진입 8m**.
- **그리드 스냅**: 도로/바닥 5m, 비-타일 프롭 자유(통과 ≥3m).
- **진입 동선**: 외곽 **(0,+70)** → 코어 야드 **(8,14)**. 추출 LZ = 코어 반대편 **(0,−68)**.

## §B. 도로망 (★① 교정 — 위계 + 굽은 메인 + 분기 + 교차 노드)

> 엔진 매핑 = `MapSpec.roadPolylines` (List<RoadPolyline>, 각 `pts`=Vector2[]). + 교차 피스(Cross/T/Corner)는 **E-2 확장 후 자동** or 그 전까지 별도 배치(아래 "교차 노드" 표).

### B.1 간선 (Arterial) — 굽은 S자 코어 척추 (1줄)

남쪽 LZ(0,−68) → 굽이 → 코어 야드(8,14) → 북 진입(0,68). **굽이 = 사선 공개 드라마**(회랑이 가렸다 코너서 왈칵).

```
roadPolyline #1 (간선, 폭 20m 아스팔트 + 양옆 흙 갓길):
  (0, -68) → (-12, -42) → (6, -16) → (8, 14)[코어 경유] → (-6, 42) → (2, 68)
```
- 굴절점 (-12,-42)·(6,-16)·(-6,42) = **`Road_Corner_01` 자리**(E-2 후). 간선이 직선이면 단조 → 굽혀서 다음 구간을 가린다.
- 코어 직전 구간 (6,-16)→(8,14)이 **컨테이너 회랑 사이를 통과** → 마지막 코너(±8,+18 부근)가 왈칵 지점.

### B.2 집산 (Collector) — 코어 ↔ 클러스터 환상/방사 (3줄)

```
roadPolyline #2 (집산, 코어 → 주거 줄 SW): (6,-16) → (-26,-28) → (-44,-36)   [간선서 T 분기]
roadPolyline #3 (집산, 코어 → 창고 E):     (8,14)  → (32,6)   → (52,-8)       [코어서 T 분기]
roadPolyline #4 (집산, 메인 → 검문소 NW):  (-6,42) → (-26,42) → (-42,40)       [간선서 T 분기]
```
- 분기 시작점 (6,-16)·(8,14)·(-6,42) = **`Road_T_01` 자리**(위계 변화: 간선→집산).
- 집산이 카이팅 중간 링 r36과 교차하는 곳 = 카이팅 분기 노드.

### B.3 국지 (Local) — 블록 사이 골목 + 갓길 (다수, 흙길)

- 각 클러스터 블록 사이 5~8m 레인 = `DirtRoad_Straight_01`(BR) 갓길 + 잔해.
- 회랑 미로(§C.2) 자체가 국지 동선(컨테이너 벽 사이).

### B.4 교차 노드 (위계가 바뀌는 곳 — 피스 명시)

| 노드 | 좌표 | 피스 | 위계 변화 |
|---|---|---|---|
| 간선↔집산#2 | (6,−16) | `Road_T_01` | 간선→집산(SW 주거) |
| 코어↔집산#3 | (8,14) | `Road_T_01` (코어 야드 진입구) | 간선→집산(E 창고) |
| 간선↔집산#4 | (−6,42) | `Road_T_01` | 간선→집산(NW 검문소) |
| 중간 링 교차 | (±36,0),(0,±36) 부근 집산 위 | `Road_Cross_01` | 동급 카이팅 분기 |
| 회랑 진입 코너 | (±8,+18) | `Road_Corner_01` + 컨테이너 코너 | 왈칵 지점 |
| LZ 진입 | (0,−62) | `Road_Cross_01`(출하 게이트 앞) | 간선 종단 |

> ★도로 일부 구간(외곽·버려진 대로)은 `Road_Straight_Damaged_01~03`으로 교체 = 폐허 어휘(asset-driven §3.1).

## §C. 클러스터 6기 (★③ 교정 — 위치·회전·정체성·건물·밀도)

> 엔진 매핑 = `MapSpec.compounds` (List<CompoundSpec>). 각 = name·type·centerXZ·rot·buildingPrefabs·buildingGap·props. **rot = frontage(정면을 인접 레인으로).** 겹침은 엔진이 AABB 경고만(자동회피 ❌) → 아래 좌표는 겹침 검수 통과 의도.

### C.1 코어 야드 (B. 산업 — 잭팟·비콘 수렴점) — `CoreYard`
```
centerXZ=(8,14)  rot=18°  buildingGap=3
buildings: [SM_Bld_Warehouse_01]
props(링): [SM_Prop_Shipping_Container_01, SM_Prop_Container_01,
            SM_Prop_Shipping_Container_01, SM_Prop_Container_01,
            SM_Prop_Floodlights_01, SM_Prop_Pallet_Loaded_01,
            SM_Prop_BarrelStack_02, SM_Prop_Generator_01]
```
- 비콘 `SM_Bld_Crane_01`(CN, ~15m) = 코어 중심(엔진 `coreBeaconPrefab`, CoreCenter()=이 컴파운드). 붐이 −Z(LZ) 가리킴(`coreBeaconRot≈200°`). **윗면 워크어블 차단.**
- 보급 잭팟 더미 = 컨테이너 1단(적층 금지, 발밑 평면). 코어 정예(Fulgurodonte 램) 런웨이 = 야드 가장자리.
- 밀도 = **집중**(코어 = 끌림 정점). 식생 0(산업).

### C.2 컨테이너 회랑 미로 (B의 연장 — 중간 띠 벽) — ★데스트랩 0 최우선
> 회랑은 *클러스터*가 아니라 **컨테이너 행 벽**(엔진상 별도 컴파운드들 or E-3 회랑 빌더). 코어 야드(r0~22)를 4방위 회랑(r22~50)이 감싼다.

| 행 | 중심(X,Z) | 길이축 | 비고 |
|---|---|---|---|
| 진입 회랑 외벽(+Z) | (−10~+10, +30) | X축 | 외곽→코어 양벽, 폭 5m |
| 진입 회랑 외벽(+Z) | (−10~+10, +40) | X축 | 바깥 행(폭 5m 유지) |
| 분기 벽(+X) | (+30, −10~+10) | Z축 | 창고 분기 |
| 코어 진입 코너 | (±8, +18) | — | ③ 왈칵 지점 |

- 컨테이너 = `SM_Prop_Container_01`(BR)·`SM_Prop_Shipping_Container_01`(CN) 색 스택, 어깨맞대기. NavMesh obstacle, 윗면 차단.
- **데스트랩 검증**: 모든 회랑 교차 ≥2 출구, 막다른 골목 0, 입구 ≥5m. 회랑당 동시 alive ≤6(보수적).

### C.3 주거 줄 (A. 주거 — 외곽 LV1, SW) — `ResidentialRuin`
```
centerXZ=(-44,-36)  rot=-35°(정면을 집산#2 레인으로)  buildingGap=2.5
buildings: [SM_Bld_House_01, SM_Bld_House_03, SM_Bld_House_02, SM_Bld_WoodenShack_01]  ← 색·매스 다양
props(링): [SM_Veh_Car_Destroyed_01, SM_Env_Rubble_Pile_01,
            SM_Prop_WireFence_01, SM_Prop_TrashBag_02, SM_Env_Rubble_Pile_02]
식생: SM_Generic_Tree_01, Tree_03, TreeDead_01 (마당 군집 — ScatterRuins 별도 or props 확장)
```
- 셋백 中(앞마당), 레인 향 정렬 = 연속 가로벽. 밀도 **성김**(외곽). 마당 펜스로 블록 구획.

### C.4 검문소/전초 (E. 검문소 — 외곽→중간 진입 통제, NW) — `Outpost`
```
centerXZ=(-42,40)  rot=25°  buildingGap=2.5
buildings: [SM_Bld_Portable_Office_01, SM_Bld_SmallBuilding_03]
props(링): [SM_Prop_Barrier_01, SM_Prop_TankTrap_01, SM_Prop_Cinderblock_Wall_01,
            SM_Prop_Roadblock_02, SM_Veh_Light_Armored_Car_01, SM_Prop_GuardTower_01]
```
- 집산#4 레인을 가로질러 = 응급 검문 게이트(서사). GuardTower = 보조 수직 랜드마크(윗면 차단). 밀도 中.

### C.5 창고 (C. 창고 — 중간~코어, E) — `Warehouse`
```
centerXZ=(52,-8)  rot=90°(정면을 집산#3 레인으로)  buildingGap=3
buildings: [SM_Bld_Warehouse_01]
props(링): [SM_Veh_Truck_Destroyed_01, SM_Prop_Pallet_Loaded_02,
            SM_Prop_Crate_Large_01, SM_Prop_BarrelStack_02, SM_Prop_Container_Small_01]
```
- 셋백 0, 뒤=서비스 레인(트럭 court=매복 포켓). 적재 독 정황으로 "창고"로 읽힘. 밀도 조밀.

### C.6 개활 잔해 공터 (F. 공터 — 카이팅 떨치기, S) — `OpenLot`
```
centerXZ=(4,-52)  rot=0°  buildings:[]  (건물 없음)
props(링): [SM_Veh_Car_Destroyed_01, SM_Veh_Buggy_Destroyed_01, SM_Env_Rubble_Pile_01,
            SM_Env_Rubble_Stone_01, SM_Prop_Concrete_Slab_Pile_02, SM_Env_Rubble_Pile_02]
식생: SM_Generic_TreeStump_01, TreeDead_01 산발
```
- 360° 카빙 개활(호드 떨치기). LZ(0,−68) 진입 완충. 밀도 = 잔해만 성김.

> **상가 묶음(D)**은 첫 맵에선 검문소(C.4)와 창고(C.5) 사이 중간 띠에 *옵션*(E. 검증 후 추가). 6 정체성 중 5기를 첫 맵에 배치(주거·산업코어·검문소·창고·공터) + 회랑 = 충분한 다양성. D 추가 시 centerXZ≈(28,28) 권고.

## §D. 코어 비콘 허브 + 동심원 밀도 그라디언트 + 끌림

- **비콘**: `SM_Bld_Crane_01`(CN) @ CoreCenter (8,14), rot≈200°(붐=−Z LZ 리딩). 15m+ 실루엣이 45° 틸트로 회랑 벽(~2.6m) 압도 = 상시 중력(natural-pull §2.3-1).
- **리딩 라인**: 간선 굽이 + 크레인 붐 + 컨테이너 열이 시선·발을 코어로 수렴.
- **밀도 그라디언트**: 외곽(주거 성김·폐허 산포) → 중간(회랑·검문소·창고 조밀) → 코어(잭팟 집중). 밀도 자체가 끌림.
- **콘 릴레이 공개(지각만)**: ① 외곽 글린트 캐시 → ② 회랑 틈 엘리트 윤곽 → ③ 코너 왈칵(야드 전모). *스폰 카운트 아님*(디렉터).

## §E. 스폰 밴드 (동심원 LV — 마커 = 디제틱 출처, 카운트 ❌)

> 엔진 매핑 = `MapSpec.spawnMarkers` (List<SpawnMarker>: pos·lvBand·role). role = 출처 종류(Ambient/WaveSpawn/EliteSpawn/FlyerSpawn). **얼마나·언제 = Gameplay 디렉터**(natural-pull §2.5).

| 마커 | 좌표(X,Z) | lvBand | role | 디제틱 출처 |
|---|---|---|---|---|
| 외곽 진입 | (0,+62) | 1 | Ambient | 진입 회랑 입구 |
| 외곽 림 ×4 | (±44,±44) | 1 | Ambient | 림 컨테이너/폐허 틈 |
| 외곽 남 | (0,−62) | 1 | Ambient | LZ 측 림 |
| 중간 분기 NE | (26,26) | 2 | WaveSpawn | 회랑 틈 |
| 중간 분기 NW | (−28,22) | 2 | WaveSpawn | 검문소 그늘 |
| 중간 분기 SW | (−22,−28) | 3 | WaveSpawn | 주거 줄 뒤 |
| 중간 분기 E | (30,−18) | 3 | WaveSpawn | 창고 트럭 court |
| 상공 진입(비행) | (0,36) | 2 | FlyerSpawn | 회랑 위 상공(평면 다이브 목표) |
| 코어 엘리트 런웨이 | (−10,14) | 5 | EliteSpawn | 야드 가장자리(Fulgurodonte 램 정원 1) |
| 코어 호위 | (8,2) | 4 | WaveSpawn | 잭팟 더미 틈(Venosaur) |

- 밴드 LV 풀(natural-pull §4): 외곽 Lacercharias·Venodonte / 중간 Caniathrox·Dimaxillosaurus·Kupolojuve / 코어 Fulgurodonte·Venosaur.
- **전부 평면 Y=0, ≥1.5m 분리, 정면 팝인 0**(콘 밖·차폐 뒤).

## §F. 경계 + 추출 LZ + NavMesh 안전

- **봉쇄벽 r74**: `SM_Env_Port_Wall_01`(BR) 변 + `SM_Env_Port_Wall_Corner_01`(BR) 모서리(엔진 `Boundary`) + `SM_Prop_WireFence_01/02`(BR)·`SM_Bld_ConcreteRebar_Wall_01~05`(CN 응급 봉쇄) 보강. Y=0.
- **추출 LZ (0,−68)**: `SM_Env_Port_Concrete_Slab_01`(BR) 패드 + `SM_Prop_Floodlights_01`(CN)×2 (±4,−68) + 출하차량 `SM_Veh_Truck_01_DumpTray_02`·`SM_Veh_Pickup_01_Canopy`(CN). 할당량 N → 점등(Gameplay). ≥2 진입로(데스트랩 0).
- **≥2 출구 검증**: 코어 야드(간선 −Z + 집산 E + 회랑 ×3), 모든 회랑 교차, LZ. 막다른 0.
- **NavMesh 체크리스트**: ①타일 0.5m 오버랩(0.01u 갭=#1) ②컨테이너/크레인 윗면 차단 ③비균일 스케일 ❌·Y=0 ④소품 통과 ≥3m ⑤회랑 walkable 밀도 보수적 캡 ⑥베이크 후 `SpawnPointMarker.isNavMeshValid` 확인(Gameplay 툴).

## §G. ASCII 톱다운 (150×150)
```
        X−75 ─────────────────── 0 ─────────────────── X+75
  Z+75  W═══════════════════════════════════════════════W  봉쇄벽 r74
        │ ·     [검문소E]   ┊간선┊   ①진입    ▦▦▦      · │  Z+62 ① 외곽(LV1)
        │  ⌂⌂주거줄        ═T═(-6,42)         ▦▦│∗      │  외곽 r55~74
        │   ⌂⌂ SW    ∗   ┊  ▦▦회랑▦▦  ┊  ∗  [창고C]    │  Z+40 ② 중간(LV2~3)
        │ ════집산#2══T═(6,-16)══┊  ┊══T═(8,14)═집산#3═══│  중간 r22~50
        │       ∗   ┌──③코너왈칵③──┐    ✦상공     ★    │  Z+18 ③ 왈칵
   Z 0  │  ════     │ ⌁크레인 ◫잭팟 │  ★Fulguro런웨이    │  ⌁=크레인(8,14)15m+
        │           │  코어 야드     │   r0~22            │  ◫=잭팟(발밑평면)
        │     ∗     └──────────────┘        ∗           │
        │  [공터F개활]    ┊간선┊    잔해산포              │  Z−52 공터(카이팅떨치기)
        │ · 카빙떨치기   ═Cross═(0,-62)═               · │
        │        출하게이트   ⊕LZ(0,-68)                 │  ⊕ 추출 LZ
  Z−75  W═══════════════════════════════════════════════W
범례: ⌁크레인비콘 ▦컨테이너회랑(윗면❌) ◫보급잭팟 ⌂주거 [클러스터] ═간선/집산 ┊갓길
      T=Road_T Cross=Road_Cross ①②③지각비트 ✦상공진입 ★코어정예 ∗LV2~3 ·LV1 ⊕LZ
```

---

# 파트 3: MapGen 스펙 매핑 (엔진이 데이터로 먹는 형태)

> 본 블루프린트 → `MapSpec` 필드 1:1. **현재 엔진으로 즉시 가능 vs 엔진 확장 필요**를 정직하게 분리.

## §H. 즉시 매핑 (현재 엔진으로 데이터만 바꾸면 됨)

| 블루프린트 | MapSpec 필드 | 값 |
|---|---|---|
| 맵 식별 | `mapName` / `seed` / `mapHalf` | "RuinCity_Compound_01" / 7741 / 75 |
| 베이스 지면색 | `groundColor` | (0.55, 0.50, 0.43) 폐허 회갈 |
| 봉쇄벽 | `wallPrefab` / `cornerPrefab` | SM_Env_Port_Wall_01 / SM_Env_Port_Wall_Corner_01 |
| 코어 비콘 | `coreBeaconPrefab` / `coreBeaconRot` | SM_Bld_Crane_01 / ~200° |
| 클러스터 6기(§C) | `compounds` (List<CompoundSpec>) | C.1·C.3·C.4·C.5·C.6 (+회랑은 E-3 or 컴파운드 다수) |
| 도로(§B) | `roadPolylines` (List<RoadPolyline>) | 간선 #1 + 집산 #2/#3/#4 (점 배열) |
| 폐허 산포(§5) | `ruinPrefabs` / `ruinCount` | Rubble×9·Damaged 도로·Destroyed car / **~55**(빈칸 메움 상향) |
| 스폰 밴드(§E) | `spawnMarkers` (List<SpawnMarker>) | 11 마커(pos·lvBand·role) |

> ★기존 `RuinCitySpec.cs`가 이미 이 구조의 *초안*이다. 본 블루프린트는 그것을 **밀도·정체성·도로 위계로 보강**한 버전(ruinCount 44→55, 클러스터 props/식생 확충, 도로 굴절점 = 교차 피스 의도 명시, 검문소·공터 정체성 추가). → Gameplay는 `RuinCitySpec.Build()`의 값을 본 블루프린트로 업데이트하면 됨.

## §I. ★엔진 확장 필요 (현재 엔진의 한계 — Gameplay 구현 대상)

> 본 바이블의 핵심 처방 중 3개는 **현재 엔진이 데이터로 표현 못 한다.** 정직하게 분리 — 이걸 안 짚으면 또 "도로=Straight만, 지면=단색 쿼드"로 회귀(무지성 재발).

| 확장 | 현재 한계 | 필요 작업(Gameplay) | 우선순위 |
|---|---|---|---|
| **E-1 복합 지면** | `MapGenKit.Ground` = 단색 quad 1장. ④ 미해결. | 코어/창고에 `Concrete_Floor` 타일 격자, 풀 패치, Damaged 도로 끼움 = 복합 지면 빌더. `MapSpec`에 groundTiles 영역 필드 추가. | 中(시각 — 캡처 판정 후) |
| **E-2 도로 교차 피스** | `RoadPolyline` = 단일 타일 직선 적층. 굴절점에 Corner, 분기점에 T, 교차에 Cross 자동 배치 ❌. ① 부분 미해결. | 폴리라인 굴절각 감지 → Corner 삽입; 폴리라인 교차/끝점 → T/Cross/End 삽입. `RoadPolyline`에 width/위계 enum 필드 추가. | **高(①이 최대 실패)** |
| **E-3 회랑 빌더(컨테이너 격자)** | 회랑을 컴파운드로 표현하면 한 줄 패킹뿐(어깨맞대기 격자 ❌). | 컨테이너 행 격자 빌더(중심선·길이축·폭·간격) + 데스트랩 검증(≥2 출구). `MapSpec`에 corridorRows 필드. | **高(집하장 정체성)** |
| **E-4 식생 군집** | `ScatterRuins`만 있음(폐허 전역 산포). 식생을 클러스터별 군집으로 ❌. | 클러스터 props에 식생 포함 or 별도 vegetationClusters 필드(군집 중심·반경·종). | 中(②밀도) |

> **E-2·E-3가 高** — ①(도로망)과 집하장 회랑 정체성이 여기 달림. 이 둘이 없으면 본 블루프린트의 도로는 여전히 "Straight 줄"로, 회랑은 "컨테이너 한 줄"로 떨어진다 = 무지성 재발. **Gameplay에 E-2·E-3 우선 구현 요청.**

---

# 파트 4: Owner 경계 + 판정 + 리스크

## §J. Owner 경계 (lv 명세 ↔ Gameplay 구현)

| 작업 | Owner |
|---|---|
| 도로 위계·굴절점·교차 노드 위치, 클러스터 정체성·좌표·rot·건물·props, 비콘/잭팟/봉쇄벽/LZ 위치, 동심원 LV 배치 | **LevelDesign(본 블루프린트)** |
| 밀도 레시피·식생 군집 의도·폐허 산포 어휘·ruinCount 방향 | **LevelDesign** 의도 → Gameplay 수치 |
| 디제틱 스폰 *출처* 마커(pos·role·lvBand) — *카운트 ❌* | **LevelDesign** → Gameplay 젠율 |
| seed *값*(7741) | **LevelDesign 설정** → Gameplay 소비(한쪽만 만짐) |
| **E-1~E-4 엔진 확장**(복합지면·도로교차피스·회랑빌더·식생군집) | **Gameplay**(lv가 스펙 입력) |
| `RuinCitySpec.Build()` 값 업데이트, MapSpec .asset 굳히기 | **Gameplay**(에디터 전담 세션) |
| NavMesh 베이크 + 이음새/Y=0/윗면차단/데스트랩/walkable밀도 검증 | **Gameplay**(lv가 검증 포인트 명세) |
| 스폰 디렉터 런타임(밴드 젠·표효·구심/번짐 곡선·동시 alive 캡·스태거) | **Gameplay**(lv = §A.10/§B.10 규칙·곡선 모양, first-stage-build-spec 계승) |
| 비콘 발광·보급등·안개 셰이더/VFX | **artist** |
| 검문소/공터 서사 정체성 | **Story** ↔ 유저 |

## §K. 판정 포인트

| P | 질문 | 권고 |
|---|---|---|
| **P1** | 첫 맵 = **집하장**을 도로위계·6클러스터·밀도/식생으로 보강한 본 블루프린트로 (분수광장은 검증 후 후속) — 동의? | 권고=예. 기존 빌드스펙 집하장 정체성 계승 + 빈칸 채움 |
| **P2** | **E-2(도로 교차 피스)·E-3(회랑 빌더) 高 우선 — Gameplay 선행 구현** 동의? 없으면 도로=Straight·회랑=한 줄로 무지성 재발 | **권고=예(핵심).** 이게 ①최대 실패의 진짜 수정 |
| **P3** | E-1(복합지면)·E-4(식생군집) 中 — 캡처 판정 후 단계 구현 | 권고=예 |
| **P4** | 클러스터 6 정체성 중 **5기(주거·코어야드·검문소·창고·공터)+회랑** 첫 맵, 상가(D)는 옵션 후속 | 권고=예. 5기로 충분한 다양성, D는 (28,28) 후보 |
| **P5** | 좌표 = 데이터 빌드 시작값 — 엔진 생성 후 캡처 보고 미세조정 OK? | 권고=예. 본 블루프린트=배치 의도+시작 좌표 |
| **P6** | `ruinCount` 44→**55** 상향(빈칸 메움) 동의? walkable ≥3m 가드 전제 | 권고=예. 밀도 의무(§5) |

## §L. 리스크

- **R1(전제·최대)**: 쫄깃함은 이 맵으로 판정 불가 — 측정 = 공간 가독성/끌림까지. "쫄깃한가"는 게이트0 전투감 선행(natural-pull §3·R1). **맵=무대, 전투감=배우.**
- **R2(무지성 재발 #1)**: **E-2·E-3 미구현 시 도로=Straight·회랑=한 줄로 회귀** = 같은 실패 반복. 엔진 확장이 진짜 수정(P2).
- **R3(밀도↔NavMesh)**: 빈칸을 꽉 채우되 통과 ≥3m·회랑 walkable 보수적 캡. 밀도 욕심이 카이팅 데드락 유발(§5 가드).
- **R4(겹침)**: 클러스터 좌표는 AABB 겹침 검수 의도 통과 — 엔진은 경고만(자동회피 ❌). 캡처서 겹침 보이면 좌표 조정.
- **R5(셰이더 가족)**: 모든 프리팹 BR+CN owned 검증 완료. Toon/PBR 1개라도 섞이면 클래시(체크리스트 §7).
- **R6(높이)**: 전 프리팹 Y=0. 컨테이너/크레인/GuardTower 윗면 워크어블 생기면 버그(NavMesh 차단 확인).
