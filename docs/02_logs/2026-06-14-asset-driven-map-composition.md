# 에셋 주도 맵 구성 감사 — 폐허 도심 1차 (집하장 + 분수광장)

> **작성**: 2026-06-14 · **에이전트**: LevelDesign(lv) · **레이어**: 에셋 가족 판정 + 요소별 owned 프리팹 매핑
> **상태**: 설계 감사/제안(유저 판정 대기) · **씬/프리팹/코드·MCP 미사용 — 설계 문서만**
> **트리거**: 유저 품질 경고 "조잡한 초등학생 게임 수준 금지 → 조잡함 #1 뿌리 = 아트 스타일 클래시"
> **상위 권위**: `docs/00_authority/2026-06-14-natural-pull-doctrine.md`(끌림·높이규칙·150척도·디렉터) · 빌드스펙 `docs/02_logs/2026-06-14-first-stage-build-spec.md`(본 감사가 *가족/폐허 커버리지*를 교정)
> **계승**: `docs/00_authority/2026-06-03-synty-demo-placement-rules.md`(도로 타일링·어깨맞대기·10m 그리드)
> **감사 근거**: 실제 프리팹/머티리얼/셰이더 전수(Glob+Grep), 추정 아님

---

## 0. TL;DR (한 화면)

| 항목 | 결론 |
|---|---|
| **가족 판정** | **POLYGON (BattleRoyale + Construction) 단일 가족으로 통일.** 두 팩은 *같은 셰이더 GUID*를 공유 = 한 가족(섞어도 클래시 0). |
| **Toon City 거취** | **도심 골격에서 배제.** 별도 셰이더 가족 + **폐허/파손 프리팹 0개** = 폐허 도심의 핵심 어휘가 없음. POLYGON과 한 씬에 섞으면 그게 바로 유저가 경고한 클래시. |
| **폐허 커버리지** | POLYGON BR = `Road_Straight_Damaged_01~03`, `Rubble_*` 9종, `Bridge_Broken_01`, `Veh_*_Destroyed` 5종 보유 → **폐허 도심 어휘 충분.** 빈칸 거의 없음. |
| **빌드스펙 변경** | 기존 스펙 = 이미 POLYGON(BR+CN) — **가족 골격은 안 바꿔도 됨.** 단 ①폐허 어휘 미사용(깨끗한 신축 야드처럼 읽힘) 보강 ②분수광장 비콘 = **Fountain owned 존재**(스펙의 "없음" 전제 정정, 단 Toon 가족이라 채굴 불가 → POLYGON 대체 유지). |
| **임포트/구매** | **불필요.** POLYGON BR+CN만으로 두 맵 품질 빌드 가능. POLYBOX City Pack 임포트·신규 구매 권고 안 함. |
| **조잡 방지 #1** | **한 셰이더 가족 강제 + 폐허 어휘 적극 사용 + Top_Down_Post-Apoc(PBR)는 골격 금지(채굴도 톤충돌로 비권장).** |

---

## 1. ★가족 판정 — 셰이더 레벨 증거 (추정 아님)

조잡함의 뿌리는 "팩 이름"이 아니라 **셰이더/텍스처 모델이 다른가**다. 실측:

| 가족 | 셰이더 GUID | 텍스처 모델 | 머티리얼 수 |
|---|---|---|---|
| **POLYGON BattleRoyale** | `0730dae39bc73f34796280af9875ce14` | 단일 아틀라스(`PolygonBattleRoyale_01_A`~`04_A` + Road/Glass/Veh) | **9개 공유** |
| **POLYGON Construction** | `0730dae39bc73f34796280af9875ce14` ← **BR과 동일** | 단일 아틀라스(`PolygonConstruction_01_A`~) | 소수 공유 |
| **Toon City** | `25e085ecbe5fe224db065ec60b95b24b` ← **다름** | 에셋별 디퓨즈(`Building_1A_D`·`Brownstone_1A_D`·`Road_1A_D`…) | **~70개 개별** |

**판정 근거 3겹:**
1. **셰이더 GUID** — BR과 Construction은 *바이트 단위로 같은 셰이더*(같은 fileID·guid). Toon City는 다른 셰이더(평탄 라이팅 응답 vs 툰 응답이 한 화면에서 충돌).
2. **텍스처 철학** — POLYGON = 1 아틀라스로 전 팩이 같은 색온도/그라데이션. Toon City = 에셋마다 개별 디퓨즈 = POLYGON 옆에 두면 *질감 밀도·채도가 따로 놂*(클래시의 전형).
3. **폐허 어휘** — 아래 §2. Toon City는 **깨끗한 신축 도시**라 폐허 도심을 *못 짠다*(스킨만 깨끗).

> **결론**: 도심 골격 = **POLYGON BR + Construction 한 가족**. Toon City는 한 조각도 섞지 않는다(15m 톱뷰에서도 셰이더 차이가 림/채도로 드러남 = 조잡).

> **⚠️ 기존 메모리 정정**: "Toon City = 톤정합 메인 outdoor(06-13 정정)"는 *셰이더/폐허 전수 감사 이전* 판단이었다. 폐허 도심(ruined) 요구 + 셰이더 가족 충돌 + 폐허 어휘 0개 = **Toon City는 1차 폐허 도심엔 부적합**. (깨끗한 현대 도시 후반 바이옴엔 재검토 가치 있음 — 단 그땐 *맵 전체를 Toon City 단독*으로, 혼용 금지.)

---

## 2. 폐허(RUINED) 커버리지 — 가족별 전수

★폐허 도심 = **부서진·파손·잔해** 어휘가 있어야 "초등학생 게임"을 벗어난다. 깨끗한 도시를 어둡게만 깔면 조잡.

### 2.1 POLYGON (BR+CN) — 폐허 어휘 충분 ✓

| 폐허 요소 | owned 프리팹 |
|---|---|
| **파손 도로** | `SM_Env_Road_Straight_Damaged_01/02/03`(BR) — 갈라진 아스팔트 |
| **잔해 더미** | `SM_Env_Rubble_Pile_01/02`·`Rubble_Stone_01/02`·`Rubble_Plank_01/02/03`·`Rubble_Pebbles_01/02/03`(BR) — 9종 |
| **부서진 구조** | `SM_Env_Bridge_Broken_01`(BR), `SM_Bld_Retainer_Wall_02_*`(CN 옹벽), `SM_Prop_Concrete_Slab_Pile_01~03`(CN 깨진 슬래브 더미) |
| **방치/파괴 차량** | `SM_Veh_Car_Destroyed_01`·`Light_Car_Destroyed_01`·`Buggy_Destroyed_01`·`Plane_Destroyed_01`·`Tank_Russia_Destroyed_01`(BR) — 5종 |
| **건설 잔해 텍스처** | `SM_Prop_Junk_Stack_01/04/06`·`SM_Prop_Iron_Sheet_02`·`SM_Env_Dirt_Pile_02/03`·`Dirt_Rocks_04`(CN) |
| **노출 철근 벽**(붕괴감) | `SM_Bld_ConcreteRebar_Wall_01~05`(CN) — 봉쇄벽·폐허 벽 양용 |

→ **빈칸 없음.** POLYGON 한 가족으로 폐허 도심의 "부서진" 정체성을 충분히 짠다.

### 2.2 Toon City — 폐허 어휘 0개 ✗ (배제 결정의 핵심)

- Grep `(ruin|destroyed|damaged|broken|rubble|wreck|debris|burnt|collaps|crack)` over Toon City Prefabs = **0 matches.**
- 차량 25종 전부 **멀쩡한 차**(Destroyed 변형 0). 건물 90종 전부 **깨끗한 신축**. 도로 풀세트지만 **파손 도로 0**.
- Toon City가 가진 것(폐허엔 무용): Fountain, Statue, Helipad, Water_Tank, Umbrella, Bench, 깨끗한 Brownstone/Skyscraper.

→ Toon City로 폐허 도심을 짜려면 **"깨끗한 도시를 어둡게 라이팅"** 외엔 방법이 없고, 그게 정확히 유저가 경고한 *조잡함*(폐허인 척하는 멀쩡한 맵).

### 2.3 Top_Down_Post-Apocalyptic_Pack (PBR) — 채굴도 비권장

- 폐허 어휘는 풍부하나 **풀 PBR = POLYGON 평탄 셰이딩과 톤충돌**(괴수 톤게이트와 같은 문제). 한 조각만 섞여도 라이팅 응답이 튀어 클래시.
- **결론**: 골격 금지는 물론, *소품 채굴도 비권장*. POLYGON 폐허 어휘(§2.1)가 충분하므로 PBR을 섞을 이유가 없다. (정 필요하면 셰이더를 POLYGON 평탄으로 강제 리스킨해야 하는데, 그 비용 > POLYGON 그대로 쓰기.)

---

## 3. 요소별 정밀 구성안 — 한 가족(POLYGON) 통일

> 표기: (BR)=PolygonBattleRoyale, (CN)=PolygonConstruction. **둘은 한 셰이더 가족이라 자유 혼용 OK.** 전부 Y=0 단일 평면(높이규칙). 좌표·회전은 빌드스펙 §A/§B 계승, 본 표는 *프리팹 선별·폐허 보강*에 집중.

### 3.1 맵 공통 요소

| 맵 요소 | owned 프리팹(POLYGON 한 가족) | 폐허 보강 포인트 |
|---|---|---|
| **① 동심원 바닥** | `SM_Bld_Concrete_Floor_01~04`·`SM_Bld_Concrete_Slab_04`(CN) + `SM_Env_Concrete_Base_01`(BR) 가장자리 | 바닥 사이사이 `SM_Env_Road_Straight_Damaged_01~03`(BR) 끼워 **갈라진 노면** 산포(깨끗 바닥 단조 방지) |
| **① 도로(분수광장 방사)** | `SM_Env_Road_Straight_01/02`·`Road_Corner_01/02`·`Road_T_01`·`Road_Cross`(BR) | 방사선 일부를 `Road_Straight_Damaged_*`로 교체 = **버려진 대로** |
| **⑥ 봉쇄벽 (r74)** | `SM_Env_Port_Wall_01/02`+`Port_Wall_Corner_01`(BR) + `SM_Prop_WireFence_01/02`(BR) 보강 | 군데군데 `SM_Bld_ConcreteRebar_Wall_01~05`(CN 노출 철근) 섞어 **응급 봉쇄** 느낌 |
| **⑦ 매복 페그 — 차량** | `SM_Veh_Car_Destroyed_01`·`Light_Car_Destroyed_01`·`Buggy_Destroyed_01`(BR) | ★폐허 정체성 핵심. 멀쩡한 차 금지(Toon Car 배제 이유) |
| **⑦ 매복 페그 — 바리케이드/잔해** | `SM_Prop_Barrier_01`(BR), `SM_Prop_Roadblock_02`·`SM_Prop_Barrier_Long_02_Tarp`(CN), `SM_Env_Rubble_Pile_01/02`·`Rubble_Stone_01/02`(BR) | 잔해 더미 = 콘 밖 측면 매복 차폐(데스트랩 0 = ≥2 출구 유지) |
| **⑧ 디제틱 스폰 출처** | 컨테이너 *뒤*(집하장), `SM_Veh_Car_Destroyed`·`Rubble_Pile` *뒤*·`SM_Bld_House_Wall_*`(CN 벽 균열), Port_Wall 틈 | 마커는 *출처 표시*만(카운트 ❌, 빌드스펙 §디렉터) |
| **추출 LZ 패드** | `SM_Env_Port_Concrete_Slab_01`(BR) + `SM_Prop_Floodlights_01`(CN)×2 + 출하차량 `SM_Veh_Truck_01_DumpTray_02`·`SM_Veh_Pickup_01_Canopy`(CN) | 점등 = artist/Gameplay |
| **드레싱(정황)** | `SM_Prop_BarrelStack_02`·`SM_Prop_Pallet_01`·`SM_Prop_TireStack_01`·`SM_Prop_Junk_Stack_01/04/06`·`SM_Prop_Concrete_Slab_Pile_02`(CN) | 통과 간격 ≥3m 유지 |

### 3.2 집하장 (맵 A) 전용

| 맵 요소 | owned 프리팹 | 비고 |
|---|---|---|
| **③ 코어 비콘(크레인)** | `SM_Bld_Crane_01`(CN) — 타워 크레인 15m+ | 붐이 −Z(LZ) 가리킴. **윗면 워크어블 차단** |
| 크레인 보조 | `SM_Prop_Crane_Section_01`(CN), `SM_Veh_Crane_01_WreckingBall_01/02`(CN 철거 크레인) | WreckingBall 크레인 = **철거 현장=폐허** 정체성 강화(추가 비콘/리딩) |
| **④ 컨테이너 회랑 벽** | `SM_Prop_Shipping_Container_01`·`Shipping_Container_Small_01`(CN) + `SM_Prop_Container_01`·`Container_Small_01`(BR) | BR+CN 컨테이너 **한 가족이라 색 스택 자유**. 회랑 폭 5m, 어깨맞대기 격자 |
| **⑤ 보급 잭팟 더미** | 위 컨테이너 1단(적층 금지=발밑 잭팟) + `SM_Prop_Crate_01`(BR) 글린트 캐시 | 코어 정예 발밑 평면 |
| 야드 폐허 보강 | `SM_Env_Rubble_Pile_01/02`·`Concrete_Slab_Pile_02/03`(CN) 야드 가장자리 | 신축 야드 아닌 **버려진 보급기지** |

### 3.3 분수광장 (맵 B) 전용 — ★비콘 정정

| 맵 요소 | owned 프리팹 | 비고 |
|---|---|---|
| **③ 코어 비콘(수직 랜드마크)** | `SM_Prop_GuardTower_01`(BR) **또는** `SM_Bld_WaterTank_01`·`SM_Bld_SmokeStack_01`(CN) | ★빌드스펙 유지. **POLYGON 가족 내 치환** |
| 단상 | `SM_Bld_Concrete_Slab_04`(CN) ×3~4 적층, ~2m. 윗면 워크어블 차단 | 둘레 평면이 전투 |
| **⑥ 광장(시민) 정황** | `SM_Bld_SmallBuilding_01~03`·`SM_Bld_House_02/03`(BR) 파사드 | 시선 깔때기·스폰 출처(사격 발판 ❌, 높이규칙) |
| 매복 페그 | `SM_Veh_Car_Destroyed_01`(BR) 군집(Venodonte 엄폐) + `SM_Prop_Roadblock_02`(CN) | |

> **★Fountain 정정 (빌드스펙 P2 갱신)**: 빌드스펙은 "Fountain/Monument owned 미존재"라 적었으나, **`Fountain_1A`·`Statue_1B/3A/3B`·`Helipad_1A/1B`가 Toon City에 실재**한다. 그러나 **Toon 가족(다른 셰이더) → POLYGON 도심에 섞으면 클래시** → **채굴 불가, 사용 안 함.** "분수" 정체성은 빌드스펙대로 **GuardTower/WaterTank + artist 발광 풀**로 치환 유지(가족 통일 우선). "owned 없음"이 아니라 **"owned지만 가족이 달라 못 씀"**이 정확한 사유 — Story 정체성 합의(P2)는 그대로 필요.

---

## 4. 배치 독트린 준수 (빌드스펙·독트린 계승, 가족 관점 보강)

| 독트린 | 본 구성안 준수 |
|---|---|
| **3단 높이 위계** | 크레인 15m+(CN) → 컨테이너/단상 ~2.6m → 바닥 잭팟 발밑. 전부 윗면 워크어블 차단(높이규칙). |
| **밀도 역구배** | 외곽 폐허 산포(rubble/destroyed car) 성김 → 중간 컨테이너/파사드 조밀 → 코어 잭팟 집중. |
| **리딩 라인** | 크레인 붐·WreckingBall·방사 대로(Road)·컨테이너 열이 코어로 수렴. |
| **데스트랩 0** | 모든 회랑/포켓 ≥2 출구·폭 ≥3m. 잔해 매복 포켓도 관통 출구 보장. |
| **단일 평면(150)** | 전 프리팹 Y=0. r0~22/22~50/55~74 밴드, 봉쇄벽 r74. |
| **스폰 디렉터** | 마커=디제틱 출처(컨테이너·파괴차량·벽균열 뒤)만, 카운트 ❌. |

---

## 5. 임포트/구매 권고

| 후보 | 권고 | 사유 |
|---|---|---|
| **POLYGON BR+CN (설치됨)** | ✅ **이걸로 빌드** | 한 셰이더 가족 + 폐허 어휘 충분 + 즉시 사용 |
| **POLYBOX City Pack 5.7GB (보유·미설치)** | ❌ 임포트 불요 | POLYGON으로 충분. 미지의 가족 추가 = 클래시 리스크↑. 5.7GB 임포트 비용 > 이득 |
| **Toon City (설치됨)** | ⛔ 1차 폐허 도심엔 미사용 | 다른 셰이더 + 폐허 0개. 후반 *깨끗한 현대 도시* 바이옴에서 *단독*으로 재검토 가치 |
| **Top_Down_Post-Apoc (PBR)** | ⛔ 골격·채굴 모두 비권장 | PBR 톤충돌. POLYGON 폐허 어휘가 대체 |
| **신규 구매** | ❌ 불필요 | 빈칸 없음 |

---

## 6. 조잡 방지 체크리스트 (유저 경고 대응)

1. **한 셰이더 가족만** — POLYGON(BR+CN) GUID `0730dae3…` 외 셰이더 프리팹이 씬에 1개라도 들어오면 즉시 빼라(Toon `25e085ec…`·PBR 금지).
2. **폐허 어휘 적극 사용** — 파손 도로·rubble·destroyed car를 *깔아라*. 깨끗한 바닥+어두운 라이팅만으론 폐허로 안 읽힌다(=초등학생 게임).
3. **컨테이너 색 스택** — BR+CN 컨테이너 한 가족이라 색만 섞어 적층(형태 단조 방지).
4. **비균일 스케일 ❌** — 회전만, 스케일 1(NavMesh 오베이크 + 비주얼 왜곡).
5. **Toon Fountain 유혹 차단** — owned지만 가족 달라 못 씀. POLYGON GuardTower/WaterTank + artist 발광으로.
6. **PBR 채굴 유혹 차단** — 폐허 소품이 탐나도 POLYGON rubble로 충분. 톤충돌 비용 > 이득.

---

## 7. 빌드스펙 변경 요약 (Gameplay/유저용)

| 항목 | 기존 빌드스펙 | 본 감사 후 |
|---|---|---|
| 도심 골격 가족 | POLYGON(BR+CN) | **유지** (가족 정합 검증 완료 — 안 바꿈) |
| 폐허 어휘 | 거의 미사용(깨끗) | **보강 필수**: Damaged 도로·Rubble·Destroyed car를 동심 밴드에 산포 |
| 분수광장 비콘 사유 | "Fountain owned 없음" | **"owned지만 Toon 가족이라 클래시 → 미사용"**으로 정정. 치환(GuardTower/WaterTank)은 유지 |
| Toon City | 메모리상 "메인 outdoor 후보" | **1차 폐허 도심 배제** (셰이더 가족 + 폐허 0개) |
| 임포트/구매 | 미정 | **불요** |

---

## 판정 포인트
| P | 질문 | 권고 |
|---|---|---|
| **PA** | 도심 골격 = **POLYGON(BR+CN) 단일 가족**, Toon City 배제 동의? | 권고 = 예(셰이더 GUID 동일 검증 + Toon 폐허 0개) |
| **PB** | 폐허 어휘(Damaged 도로·Rubble·Destroyed car) **적극 산포** — 깨끗 바닥 단조 교정 | 권고 = 예(조잡 방지 #2) |
| **PC** | Top_Down_Post-Apoc(PBR) **골격·채굴 모두 미사용** — POLYGON 폐허로 대체 | 권고 = 예(톤충돌) |
| **PD** | 임포트/구매 불요(POLYGON만으로 빌드) | 권고 = 예 |
| **PE** | 분수광장 Fountain = owned지만 가족 달라 미사용, GuardTower/WaterTank 치환 유지(Story P2) | 권고 = 예 |
