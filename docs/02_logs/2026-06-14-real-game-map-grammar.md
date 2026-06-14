# 실제 도시계획 + 게임 맵 구축 문법 (깊은 웹조사)

> **작성**: 2026-06-14 · **목적**: "무지성 배치 개판"을 끝내고 *한 칸 한 뼘 신중하게 까는 법*의 근거를 댄다. 맵을 지을 때마다 다시 읽는 영속 레퍼런스.
> **위치**: 본 문서는 *원리·근거*다. 우리 게임 적용 결론은 §6 + 기존 권위(`00_authority/2026-06-14-natural-pull-doctrine.md`, `2026-06-03-synty-demo-placement-rules.md`)와 합쳐 읽는다.
> **추정/사실 구분**: 〔사실〕=출처 있는 도시계획/게임 사실. 〔추정〕=내가 우리 게임에 적용하며 내린 해석.
> **충돌 주의**: 실제 도시계획은 *현실 보행/차량 효율*이 목적이고, 우리 게임은 *카이팅·끌림·가독성*이 목적이다. 현실 수치를 그대로 쓰지 않는다 — **문법(왜 그렇게 생겼나)을 빌리고, 우리 카메라/루프에 맞게 환산한다.** §6이 그 환산이다.

---

## 0. 한 장 요약 (TL;DR)

도시는 **위계(hierarchy)와 의미(meaning)로** 만들어진다. 무지성 배치가 개판인 이유: 모든 도로가 같은 굵기, 모든 건물이 같은 방향, 블록 크기가 제멋대로, 건물이 "뭘 뜻하는지" 없이 흩뿌려짐.

신중한 배치의 5대 규율:
1. **도로는 위계가 있다** — 간선 1 → 집산 몇 → 국지 다수. 다 같은 폭으로 깔면 죽은 격자.
2. **블록은 크기 규약이 있다** — 보행 가능 = 한 변 60~180m. 건물은 블록 *가장자리*에 정면을 도로로 향해 앉는다(셋백·코너 규칙).
3. **건물은 의미가 있다** — 주거/상업/창고/관공서마다 풋프린트·입구·배치가 다르다. 모르고 깔면 디오라마가 아니라 잡동사니.
4. **게임 맵은 가독성·동선·인카운터로 다시 쓴다** — 랜드마크 분포, 밀도 그라디언트, 시선축. 현실 효율이 아니라 *플레이어 경험*이 기준.
5. **모듈 조립은 그리드·스냅·인접 규칙으로** — 한 칸 = 정해진 그리드 유닛, 피스는 이음새 없이 맞물리고, 인접은 의미 군집으로.

---

## 1. 도로망 문법 (Road Network Grammar)

### 1.1 도로 위계 (Functional Classification) 〔사실〕

실제 도시계획은 도로를 **이동성(mobility) vs 접근성(access)** 트레이드오프로 3~4단 위계로 나눈다. 모든 도로가 같으면 안 되는 *근본 이유*가 이것이다.

| 위계 | 역할 | 이동성/접근성 | 현실 폭(차로) | 간격 |
|---|---|---|---|---|
| **간선 (Arterial)** | 도시 주요 활동중심 연결, 최대 교통량·최장 거리, 최소 거리로 최대 통행 흡수 | 이동성 최고, 접근 최소 | 4~10차로 (노면 ~30m, ROW ~41m) | minor arterial 간격 0.2~1.6km (개발지 1.6km 이내) |
| **집산 (Collector)** | 국지로의 교통을 모아 간선으로 전달, 접근/이동 균형 | 중간 | 2~4차로 | 간선과 국지 사이 |
| **국지 (Local)** | 부지·필지에 직접 접근. 최저 속도, 통과교통 억제 | 접근 최고, 이동 최소 | 1~2차로 (노면 6m, ROW 8m) | 촘촘 |

핵심: **위계는 굵기·연결 패턴·교차 빈도로 *눈에 보인다*.** 간선은 굵고 길고 직선, 국지는 가늘고 짧고 많다. 도로를 깔 때 "이건 무슨 위계?"를 먼저 정한다.

출처: [Wikipedia: Street hierarchy](https://en.wikipedia.org/wiki/Street_hierarchy), [FHWA Functional Classification Guidelines](https://www.dot.state.mn.us/planning/program/pdf/FHWA%20Guidelines.pdf), [Arterials.co](https://www.arterials.co/road-classification-in-transportation-planning/), [Iowa DOT 5B-1 Street Classifications](https://www.intrans.iastate.edu/wp-content/uploads/sites/15/2018/07/5B-1.pdf)

### 1.2 교차로 종류와 *어떻게 연결되나* 〔사실〕

기본 교차 형태: **3지(T)·4지(십자)·다지(multi-leg)·로터리(roundabout)**.

```
   T-자 (3-leg)             십자 (4-leg)            로터리
       │                       │                    ╭───╮
       │  stem                 │                   ╱  ●  ╲     ● = 중앙섬
   ────┴────  arms        ─────┼─────             │  순환 │
                               │                   ╲     ╱
                               │                    ╰───╯
   위계 변화·막다른 분기      두 동급 도로 교차      합류 다수, 정지 없이 흐름
```

- **T-자**: stem(주도로) + arms(가지 둘). 위계가 *바뀌는* 지점(국지→집산)이나 한쪽이 막힌 분기. 자연스러운 동선 결정 지점.
- **십자(crossroads)**: 두 도로가 보통 직각으로 교차, 사각/십자 형태. 4방향 합류 → 신호/정지로 제어. **격자의 기본 노드.**
- **로터리**: 원형 교차, 한 방향 순환, 진입차가 순환차에 양보(yield-at-entry). 고속 충돌 감소. 〔게임 적용 추정〕 로터리 = 천연 *원형 아레나/랜드마크 노드* — 카이팅 루프에 이상적.

**연결 규칙(추정·사실 혼합)**: 격자는 십자 노드의 반복. 위계가 바뀌는 곳에 T가 생긴다. 간선끼리는 드물게 만나고(큰 교차), 국지는 집산에 T로 붙는다. **노드마다 "여기서 위계가 어떻게 바뀌나"를 물어야 한다** — 같은 폭 도로가 십자로만 만나면 단조롭고 죽은 격자가 된다.

출처: [TxDOT 13.3 Types of Intersections](https://www.txdot.gov/manuals/des/rdw/chapter-13--intersections/13-3-types-of-intersections.html), [Wikipedia: Intersection (road)](https://en.wikipedia.org/wiki/Intersection_(road)), [Structural Guide: Types of Road Intersections](https://www.structuralguide.com/types-of-road-intersections/), [Broward Complete Streets Ch.6 Intersection Design](https://www.browardmpo.org/images/WhatWeDo/completestreetsinitiative/broward_complete_streets_guidelines_parts/CH-6-Intersection-Design-final.pdf)

### 1.3 블록 크기·종횡비·도로 폭 규약 〔사실〕

| 도시 | 블록 치수 | 비고 |
|---|---|---|
| Manhattan | 80m × 274m | 길쭉(종횡비 ~3.4:1) — 긴 변=가로(avenue 향), 짧은 변=세로 |
| Chicago | 100m × 200m | 2:1 |
| Paris | 60m × 86m | 거의 정사각 |
| Buenos Aires | 110m × 110m | 정사각 |
| 일반 평균 | 80×60m ~ 160m | 계획도시 평균 ~80×60m |

**보행성(walkability) 등급 — 한 변 길이 기준** 〔사실, 핵심 수치〕:
- **< 150m = "매우 보행 가능"**
- 150~300m = "중간"
- **> 450m = "보행 빈약"**
- 권장 신규 블록 = **120~180m**, best practice = 250m 미만 + 헥타르당 교차로 1개 이상.

중심 보행구역은 **60~80m 그리드**, 주거구역은 80~100m가 이상적. 즉 **작은 블록 + 높은 교차로 밀도 = 보행성**(짧고 직접적인 경로 + 다중 경로 선택). 굽은 도로·막다른 길은 보행성을 죽인다.

출처: [Sivo: average city block in meters](https://hub.sivo.it.com/urban-planning-metrics/how-big-is-the-average-city-block-in-meters/), [Wikipedia: City block](https://en.wikipedia.org/wiki/City_block), [GPSC Principle 6: Human-Scale Streets and Small Blocks](https://www.thegpsc.org/sites/default/files/globalsprawl_11_principle_6_humanscalestsmallblocks_1.pdf), [NSW Movement & Place: Principle 4 Permeable Network](https://www.movementandplace.nsw.gov.au/place-and-network/guides/network-planning-precincts-guide/network-planning-and-design-principles/principle-4-create-permeable-network-grid-structure-short-block-length-and-high-intersection), [Wakefield District Design Code B3.2.2 Block size](https://districtdesigncode.wakefield.gov.uk/part-b-codes-and-guidance/b3-welcoming-places/b32-blocks-and-typologies/b322-block-size)

> 〔우리 게임 직결 추정〕 우리 존은 **~100×100m 바운디드**(natural-pull §1). 현실 블록 한 변이 60~180m이므로 **우리 존 = 현실 블록 약 1~2개 크기**다. 즉 우리 맵은 "도시"가 아니라 **"몇 블록짜리 한 구획"**으로 설계해야 스케일이 맞는다. 블록을 더 잘게(30~50m) 썰어야 카이팅 레인·교차로 밀도가 산다 → §6.

---

## 2. 블록/필지 구조 (Block & Parcel Structure)

### 2.1 건물이 블록 안에 어떻게 앉나 〔사실〕

블록은 도로로 둘러싸인 땅. 그 안을 **필지(lot/parcel)**로 나누고, 건물이 필지 안에 앉는다. 규칙:

- **가로변 정렬(street alignment)**: 신축 건물은 인접 건물과 가로선을 따라 정렬하고 확립된 셋백을 따른다. 한 줄의 건물 정면이 *연속된 가로벽(street wall)*을 이룬다 — 이게 "거리"를 만든다.
- **전면 도로향(frontage)**: 필지가 도로와 만나는 변 = frontage. 건물 정면(façade)이 이 변을 향한다.
- **셋백(setback)**: 도로선에서 건물이 물러난 최소 거리. 0이면 정면이 보도에 바짝(소매상가), 크면 앞마당(주거). 셋백 공간 = 보도와 건물 사이 전이대(노천 좌석·진입 계단·상점 진열).
- **뒷골목/뒷마당(rearyard)**: 블록 안쪽. 서비스·녹지 회랑·주차. 정면(공적)과 후면(사적)의 *낙차*가 블록의 핵심.
- **코너 필지(corner lot)**: 두 도로에 면함. 두 변 다 정면 셋백 적용. 정면 = 보통 *짧은* 가로변. 코너 건물은 교차로의 중요한 건축 요소 — 강조해서 모서리를 살린다(활성 1층 + 건축 요소).

```
   ┌──────── 도로 (frontage 방향) ────────┐
   │ [건물정면] [건물정면] [건물정면]  ← 가로벽: 정면 정렬, 셋백 일정
   │ ┌──┐셋백 ┌──┐    ┌────┐ 코너건물
   │ │필지││필지│    │  강조 │
   │ └──┘    └──┘    └────┘
   │   뒷마당/뒷골목 (rearyard, 서비스·사적)
   └──────────────────────────────────────┘
```

출처: [LegalClarity: Lot Frontage](https://legalclarity.org/what-is-lot-frontage-and-why-does-it-matter/), [RTA Studio: Setbacks/Sideyards/Rearyards](https://www.rtastudio.com/2019/07/setbacks-sideyards-easements/), [APA PAS Report 165: Illustrating the Zoning Ordinance](https://www.planning.org/pas/reports/report165.htm), [Cyburbia: corner lot which is the front](https://cyburbia.org/forums/threads/corner-lot-which-is-the-front.37947/)

### 2.2 용도지역 (Zoning Districts) 〔사실〕

도시는 용도로 구역을 나눈다 — **주거(R)·상업(C)·산업(I)**. 조닝은 건물 높이·부피·도로선 셋백·가로변 폭을 *동네 성격에 맞게* 규제한다.

- **주거**: 큰 셋백(앞마당), 낮은 건물, 넓은 필지, 조용. 평균 셋백 규칙(인접 30%+ 건물 평균에 맞춤).
- **상업**: 셋백 0~얕음, 1층 활성(상점), 연속 가로벽, 높은 밀도.
- **산업**: 큰 풋프린트, 적재 독(loading dock), 트럭 마당, 서비스 도로. 보행자 적음.

출처: [APA PAS Report 165](https://www.planning.org/pas/reports/report165.htm), [Houston Setbacks and Lot Sizes](https://nanproperties.com/blog/houston-setbacks-and-lot-sizes-a-simple-guide), [arxiv: Zoning in American Cities (AI analysis)](https://arxiv.org/pdf/2502.00008)

---

## 3. 건물의 의미·기능 (What Buildings Mean)

"건물이 뭘 뜻하는지 모르고 깔면 안 됨" — 타입별 *형태·입구·배치 규칙*. 〔사실〕

### 3.1 Frontage 타입 (건물이 보도와 만나는 방식)

- **Linear (선형)**: 셋백 0, 도로 모서리에 바짝. 소매상가의 기본. 연립/타운하우스도 적합.
- **Stoop (계단형)**: 얕은 셋백 + 진입 계단으로 1층을 보도보다 올림. 1층 주거/기관용.
- **Forecourt (안마당형)**: 입구에 개방 공간. 혼합용도 광범위.

### 3.2 타입별 형태·입구·배치

| 타입 | 풋프린트·형태 | 입구 | 배치 규칙 |
|---|---|---|---|
| **주거(집)** | 작고 깊음, 앞마당 셋백 | 정면 1, 보도 향 | 가로선 정렬, 일정 셋백, 뒷마당, 동네 군집 |
| **상가(점포)** | 좁은 정면 + 깊은 안쪽 | 정면 보도 직결, 1층 진열창 | 셋백 0, 어깨 맞댄 연속 가로벽, 코너 강조 |
| **창고/산업** | **매우 깊음 (현실 350~400ft / ~110~120m 깊이)**, 큰 단순 매스 | **적재 독**(표준 9ft×10ft 문, 12ft+ 간격), 트럭 마당(트럭 court 깊이 40~55m) | 서비스 도로·뒷면 독, 트럭 동선, 보행 적음 |
| **관공서/기관** | 큰 대칭 매스, 권위적 | 정면 중앙 강조 입구, 광장/계단 | 셋백 큼(전면 광장), 랜드마크 위치, 축선 정렬 |

핵심 〔사실+추정〕: **건물은 입구가 동선을, 풋프린트가 매스를, 셋백이 거리 성격을 결정한다.** 창고는 *뒤에 서비스, 앞에 큰 벽* — 우리 폐허도심에 깔 때 적재 독·컨테이너 마당·트럭 court가 같이 와야 "창고"로 읽힌다(단독 박스 ❌).

출처: [Iowa City 14-2G-4 Frontage Type Standards](https://codelibrary.amlegal.com/codes/iowacityia/latest/iowacity_ia/0-0-0-21299), [Lakewood 18C.400.410 Site design/frontage](https://lakewood.municipal.codes/LMC/18C.400.410), [Wikipedia: Building typology](https://en.wikipedia.org/wiki/Building_typology), [PEB Steel: Warehouse Design Guide](https://pebsteel.com/en/warehouse-design-guide/), [Steelco: Warehouse Loading Dock Design](https://www.steelcobuildings.com/warehouse-loading-dock-design-layout-door-spacing-and-truck-court-planning/), [Wikipedia: Loading dock](https://en.wikipedia.org/wiki/Loading_dock)

---

## 4. 탑다운 게임 레벨 레이아웃 (게임이 *실제로* 어떻게 짜나)

### 4.1 탑다운 슈터 일반 원칙 (War Robots/MY.GAMES 레벨디자인 분해) 〔사실〕

게임 맵은 현실 효율이 아니라 **카메라·이동속도·전투 역학·모드 목표**가 형태를 결정한다.

1. **카메라가 지오메트리 높이를 정한다** — 비회전 탑다운 카메라는 시야 유지 위해 엄폐물 높이 ≈ 캐릭터 높이. 키 큰 물체는 *맵 가장자리에만*(게임플레이 차단 방지). → **우리 45°/15m·단일 전투 평면과 직결**: 키 큰 비콘은 끌림용으로 가장자리/코어 윤곽에만.
2. **이동속도가 밀도를 정한다** — 느림=밀집 회랑/좁은 조우, 빠름=넓은 아레나 + 띄엄띄엄 엄폐. 잦은 리스폰=컴팩트 맵.
3. **전투 역학이 공간을 만든다** — AOE 중심=엄폐 최소화로 교전 강제. 능력 기반=넓은 듀얼 공간. → 우리 서바이버즈 카이팅 = **넓은 자유 공간 + 카빙 레인** 쪽.
4. **모드 목표가 아레나 구조를 만든다** — 단일 목표=중앙 충돌 구역 하나, 다중 목표=분산 아레나 + 연결 지오메트리.
5. **대비로 가독성** — 바닥 밝게/벽 어둡게, 강한 명암. 바닥 네비 마커. **키 큰 랜드마크는 가장자리에만.**
6. **환경 메카닉이 레이아웃 보완** — 부시(조우 타이밍 조절), 파괴물(동적 시야 개방), 데미지 존(막다른 탐색 억제).

출처: [MY.GAMES (War Robots): Top-down shooter level design — how map design supports mechanics](https://medium.com/my-games-company/top-down-shooter-level-design-how-map-design-supports-game-mechanics-6ae39fdd095d)

### 4.2 가독성·랜드마크·시선 (Level Design Book / 일반) 〔사실〕

- **랜드마크/POI** = 유니크하고 기억에 남는 형태·매스·위치. **기능적으로 게임플레이와 관련 있어야 함**(장식 ❌). 전투 후 목표를 상기시키는 방향 기준점.
- **시선(sightline)** = 다른 공간을 볼 수 있는 빈 공간의 궤적. **비스타**(다음 구역 보여줌→전략) + **어프로치**(비스타로 가는 경로).
- **컴포지션 = 대비로 위계**: 높이(밀집부 高/개방부 低), 밀도(개방 vs 협소), 방향(격자에 각진 물체), 형태(사각 속 원). 키 큰 건 *주변이 낮을 때만* 의미.
- **주의**: "리딩 라인은 사실 별로 효과 없다"(스크린샷≠플레이 공간). 진짜 유도는 **메트릭·인카운터·플레이테스트·wayfinding**으로. → 우리는 비콘(상시 보임)과 콘 릴레이 공개로 유도(natural-pull §2.3)하니 이 경고를 흡수함.
- **가독성** = 클러터·시각 노이즈 적게, 게임플레이 기회·경로를 명확히. 플레이어가 조우/적을 보고 반응할 수 있게.

출처: [Level Design Book: Composition](https://book.leveldesignbook.com/process/blockout/massing/composition), [80.lv: Clever Level Design](https://80.lv/articles/how-to-build-good-levels-for-games), [Mike Barclay: Level Design Guidelines](https://mikebarclay.co.uk/my-level-design-guidelines/)

### 4.3 구체 게임 사례 분해

**Escape from Tarkov** 〔사실〕 — 핸드크래프트 맵. **명명 랜드마크/콜아웃**(Old Gas Station, Dorms, Construction Site)이 척추. 추출구가 *둘레에 명시*되던 구형 → 신형은 **환경에 녹임**(둘레 표시 X). PMC/Scav 추출구 다름, 일부는 조건부(아이템 필요). Streets=수직성↑·고층/호텔/조망점. Factory=최소(작은 어두운 방 몇 개).
출처: [U7Buy: Tarkov Map Mastery](https://www.u7buy.com/blog/escape-from-tarkov-map-mastery-guide/), [Dexerto: all maps & extraction points](https://www.dexerto.com/escape-from-tarkov/all-escape-from-tarkov-maps-and-extraction-points-guide-1309294/)

**Escape from Duckov** 〔사실〕 — 탑다운 PvE 추출. 2026 업데이트 = 산업 단지, **추출구를 둘레 명시 대신 환경에 통합.** 키/키카드로 게이팅된 고티어 룸(Central Warehouse 블루키카드, Manager's Office). 보스가 Warehouse A↔Railyard 순찰(LMG 소리·연막이 단서). → **고가치=고위험 게이팅 + 청각 단서 + POI 명명** 패턴.
출처: [escapefromduckov.net](https://escapefromduckov.net/), [vgtimes interactive map](https://vgtimes.com/guides/138180-interactive-escape-from-duckov-map-all-key-locations-and-points-of-interest.html), [duckov.com](https://www.duckov.com/), [Medium: Rise of Escape from Duckov](https://klaothongchan.medium.com/when-ducks-meet-extraction-shooters-the-rise-of-escape-from-duckov-f49f49ca0a19)

**Zero Sievert** 〔사실〕 — 탑다운 추출, **절차생성 황무지**(은신처·전리품·날씨·레이아웃이 매번 다름). 6맵, 바이옴(Swamp·City 추가). 절차 시스템을 개발 내내 정제. (구체 알고리즘은 비공개.)
출처: [Steam: ZERO Sievert](https://store.steampowered.com/app/1782120/ZERO_Sievert/), [Modern Wolf](https://modernwolf.net/game-for-cabo-studio/zero-sievert)

**Deep Rock Galactic: Survivor** 〔사실〕 — 바이옴별 *성격*이 레이아웃을 정함. Magma Core=용암 분출구+폭발 식물(접근 시 발동), 갈라진 땅이 데미지. Salt Pits=채굴 쉬운 지형 + 채굴 시 종유석 붕괴(몹 압사). Azure Weald=점프패드(착지 시 지형 파괴+몹 밀침+가속). **지형 자체가 메카닉**(해저드=플레이 동사).
출처: [DRG Survivor Wiki: Biomes](https://deeprockgalactic.wiki.gg/wiki/Survivor:Biomes)

**Hades** 〔사실, 핵심 패턴〕 — **핸드크래프트 방 템플릿을 절차적으로 재조합.** 방 *레이아웃은 고정*, 적·보물 스폰만 랜덤(기술 부담↓ + 아티스트 통제↑). 바이옴마다 통일된 설계 원리: Tartarus=중간 크기, 거의 완전히 벽으로 둘러쌈(초보 친화 + 벽꽝 데미지). **작은 방=바이옴 초반, 큰 방=후반**(에스컬레이션 반영). 동적 요소=인페르날 체스트·웰·낚시·보상·열린 출구. 발견 곁가지(항아리·트로브·카오스/에레보스 게이트).
출처: [Kotaku: Hades level design is less random than it seems](https://kotaku.com/hades-level-design-is-less-random-than-it-seems-1845254545), [Wikipedia: Hades](https://en.wikipedia.org/wiki/Hades_(video_game))

**Vampire Survivors** 〔사실〕 — 스테이지가 *형태로* 전략 강제: Dairy Plant/Mad Forest=넓은 오픈 아레나(360° 무기), Inlaid Library=좁은 회랑(적 깔때기→지역거부 무기), Gallo Tower=수직 협소(초크포인트), Cappella Magna=엔드게임(전 요소 + 극고밀도). **레이아웃이 무기/전략을 규정.** 기술 구현(서바이버류 일반): 청크 기반(20×20셀, 타일 32px), 플레이어 청크 위치로 8방향 청크 스폰, 청크에 고정 기준점, 프롭 랜덤 배치, 거리 50+ 청크 비활성.
출처: [Rogue Ranker: VS Stages](https://rogueranker.com/vampire-survivors-stages/), [Terresquall: Creating a Rogue-like (VS) Part 2 Map Generation](https://blog.terresquall.com/2022/12/creating-a-rogue-like-vampire-survivors-part-2/)

> 〔우리에게 가장 중요한 사례 추정〕 **Hades 패턴**(핸드크래프트 방 + 절차 재조합, 레이아웃 고정·스폰만 랜덤)이 우리 natural-pull §1 "모듈 라이브러리 + 절차 조립, 1차=수동 시드 MVP"와 정확히 일치한다. **Duckov**(고가치=고위험 게이팅·청각 단서·POI 명명)는 우리 끌림 독트린(값어치∈위험)의 추출-장르 증거. **VS**(레이아웃이 전략 규정)는 카이팅 공간 형태가 곧 게임플레이임을 증명.

---

## 5. 블록 단위 조립 규칙 (Modular Assembly, 한 칸 한 칸)

### 5.1 모듈러 기본 〔사실〕

- 작고 재사용 가능한 메시 세트를 **그리드에 스냅**해 큰 환경 조립. 피스마다 스냅·피벗 정의 필수.
- **먼저 기본 피스(바닥·벽·천장·문틀·창·코너·전이"풀"), 변종은 나중.** 인간 스케일 메트릭 먼저 확립.
- **검증 테스트**: ①Loopback(피스가 틈 없이 자기 자신과 연결) ②Stack(다층, 바닥 두께) ③Gap(off-angle 연결 커넥터 충분) ④Collision(끼임 없음).
- 표준 네이밍 규약(디자이너+아티스트 합의). 지오메트리 검증 후 아트 패스(텍스처 변종이 리메시보다 쌈).

출처: [Level Design Book: Modular kit design](https://book.leveldesignbook.com/process/blockout/metrics/modular), [GameDesignSkills: Modular Level Design](https://gamedesignskills.com/game-design/modular-level-design/), [WorldOfLevelDesign: Modular Environment Design 101](https://www.worldofleveldesign.com/categories/game_environments_design/modular-environment-design-101.php)

### 5.2 Synty 그리드 스펙 (★우리 키트 실측) 〔사실〕

| 팩 | 수평 그리드 | 수직 | 시스템 | 비고 |
|---|---|---|---|---|
| POLYGON City | **5m** (도로/지면 타일) | 3m | Standalone | 반모듈 건물 청크 |
| POLYGON Cyber City | 2.5m | 3m | Build 2.0 | 45° 벽 도입 |
| POLYGON Office | 2.5m | 3m | Standalone | |
| POLYGON Town | 2.5m | 3m | Standalone | |
| POLYGON Sci Fi City | 2.5m & 5m(대타일) | 3m | Standalone | |
| **Build 2.0 일반** | **2.5m 수평 / 3m 수직** | | | 벽이 인접 2칸 점유 → 게임오브젝트 수↓ |

→ **우리 작업 그리드 = 2.5m 또는 5m 수평, 3m 수직.** 한 "칸"은 즉흥이 아니라 이 유닛이다. 도로/지면은 5m, 건물 모듈은 2.5m 정렬.

출처: [GameDevBits: Synty pack snap specs](https://gamedevbits.com/syntyspecs/), [GameDevBits: Build 2.0 from Synty](https://gamedevbits.com/synty-packs/build-2-0-from-synty-studios/)

### 5.3 인접 규칙(adjacency) — 의미 군집 〔사실+추정〕

우리 프로젝트 실측 권위(`2026-06-03-synty-demo-placement-rules.md`)가 이미 증명: **프롭은 용도 있는 무리로 군집, 흩뿌리지 않음.** 컨테이너=단독 금지→사각 야드 격자, BaseWall=연속 직선 런, 펜스로 빈 땅을 "구획"하면 의미 생김. 〔이것이 §3 "건물 의미"의 프롭 버전 — 같은 원리.〕

---

## 6. ★우리 폐허도심 바운디드존에 어떻게 적용하나 (환산·결론)

> 현실 수치를 그대로 쓰지 않는다. **문법을 빌리고 우리 ~100×100m / 45°·15m / 카이팅 농성 루프에 환산.** 기존 권위(natural-pull, synty-placement)와 충돌 시 *기존 권위 우선*, 본 절은 그 *근거 보강*이다.

### 6.1 스케일 환산 (현실 → 우리)
- 우리 존 100×100m = 현실 블록 1~2개. → **"도시"가 아니라 "한 구획(a few blocks)"으로 설계.**
- 현실 보행 블록 한 변 60~180m이지만, **우리는 카이팅 레인·교차로 밀도가 필요** → 블록을 더 잘게 **30~50m 한 변**으로 썰어 도로(레인) 빈도를 높인다. 〔추정: 현실 60~80m 보행 그리드를 절반으로 압축 = 카메라 15m 반경에 교차로가 자주 들어오게.〕
- 모든 치수는 Synty 그리드(수평 2.5/5m, 수직 3m)의 배수로 떨어뜨린다.

### 6.2 도로 위계 → 끌림 척추 (§1 → natural-pull §2.2 리딩라인)
- **간선 1줄 = 코어로 향하는 척추** (BR 데모의 중앙 N-S 고속도로와 동형). 넓고 직선, 시선을 비콘으로 꺾는 *수렴선*.
- **집산 2~3 = 동심원 띠 사이를 잇는 환상/방사** — 카이팅 루프의 뼈대.
- **국지 다수 = 블록 사이 골목** — "건물이 가렸다 골목 끝 왈칵"(natural-pull §2.4 공개 드라마)의 무대.
- **교차로**: 십자=동심원 교차 노드(카이팅 분기), T=위계 바뀌는 곳, **로터리=원형 카이팅 아레나/랜드마크**(있으면 좋은 천연 농성 포켓). 단 §3 제약 — 데스트랩 0, 모든 포켓 ≥2 출구.

### 6.3 블록/필지 → 의미 있는 매스 (§2 → synty-placement §1·2·3)
- 건물은 **블록 가장자리에 정면을 도로(레인)로 향해** 앉힌다(synty rotY 규약 §4 이미 확립). 셋백 0=상가형 연속 가로벽(어깨 맞댐), 셋백 큼=주거 마당.
- **블록 안쪽(rearyard) = 서비스·뒷골목** → 매복 포켓·캐시 은닉(콘 릴레이 ① 글린트 캐시 위치). 공적(거리)↔사적(뒷골목) 낙차가 인카운터 다양성.
- 코너 건물 강조 = 교차로 = 시선 종착·랜드마크(BR/Western 데모가 "거리 끝을 랜드마크로 막음"으로 이미 실천).

### 6.4 건물 의미 → 좀비/폐허 드레싱 (§3)
- **창고/산업 = 코어 잭팟 쪽** — 적재 독·컨테이너 마당·트럭 court가 *같이* 와야 읽힘(단독 박스 ❌). 큰 단순 매스 = 비콘 실루엣. 고가치·고위험(Duckov 게이팅 패턴).
- **주거 = 외곽 LV1** — 작은 풋프린트 군집, 마당 펜스, 조용·저밀도(natural-pull §4 외곽 배치와 합치).
- **관공서/기관 = 서사 랜드마크**(본사 출장소 등) — 큰 대칭 매스 + 전면 광장, 축선 정렬, 비콘급 위치.

### 6.5 조립 규율 → 한 칸 한 뼘 (§5 → Hades 패턴)
- **수동 시드 = Hades식 핸드크래프트 "블록 템플릿"**: 레이아웃 고정, 스폰만 디렉터가 동적(natural-pull §2.5). 시드 한 장 = 블록 몇 개 + 도로 위계 + 의미 매스 + 비콘이 *의도대로 박힌* 완성 구성.
- 모든 피스 Synty 그리드 스냅, 이음새 0 갭(NavMesh 함정 §3), 의미 군집(컨테이너 야드·바리케이드 런·펜스 구획).
- **검증**: 공간 가독성·끌림 합성까지 캡처로(natural-pull §3). 블록 깔 때마다 "이 도로 무슨 위계? 이 건물 무슨 의미? 셋백 맞나? 코너 살았나? 비콘 보이나?" 체크.

### 6.6 "무지성 배치"의 안티패턴 체크리스트 (이걸 어기면 개판)
- [ ] 도로가 전부 같은 굵기 (위계 없음) → §1.1 위반
- [ ] 블록 크기 제멋대로 / 한 변 > 50m (카이팅 레인 죽음) → §1.3, §6.1 위반
- [ ] 건물이 도로 안 보고 아무 방향 (frontage 무시) → §2.1 위반
- [ ] 셋백 제각각 (가로벽 안 생김) → §2.1 위반
- [ ] 코너에 의미 없는 빈 건물 → §2.1 위반
- [ ] 창고를 적재독·마당 없이 단독 박스로 → §3.2 위반
- [ ] 키 큰 물체가 플레이 중앙에 (시야 차단) → §4.1-1 위반
- [ ] 프롭을 흩뿌림 (의미 군집 아님) → §5.3 위반
- [ ] 비콘이 안 보이거나 리딩라인이 코어로 안 꺾임 → natural-pull §2.3 위반
- [ ] 막다른 포켓 (출구 1개) → natural-pull §3 데스트랩 위반

---

## 부록: 출처 전체 목록

**도시계획 — 도로/블록/교차로**
- [Wikipedia: Street hierarchy](https://en.wikipedia.org/wiki/Street_hierarchy) · [Road hierarchy](https://en.wikipedia.org/wiki/Road_hierarchy)
- [FHWA Functional Classification Guidelines](https://www.dot.state.mn.us/planning/program/pdf/FHWA%20Guidelines.pdf)
- [Iowa DOT 5B-1 Street Classifications](https://www.intrans.iastate.edu/wp-content/uploads/sites/15/2018/07/5B-1.pdf)
- [Arterials.co: Road Classification](https://www.arterials.co/road-classification-in-transportation-planning/)
- [TxDOT 13.3 Types of Intersections](https://www.txdot.gov/manuals/des/rdw/chapter-13--intersections/13-3-types-of-intersections.html)
- [Wikipedia: Intersection (road)](https://en.wikipedia.org/wiki/Intersection_(road))
- [Structural Guide: Types of Road Intersections](https://www.structuralguide.com/types-of-road-intersections/)
- [Broward Complete Streets Ch.6](https://www.browardmpo.org/images/WhatWeDo/completestreetsinitiative/broward_complete_streets_guidelines_parts/CH-6-Intersection-Design-final.pdf)

**도시계획 — 블록 크기/보행성**
- [Sivo: average city block in meters](https://hub.sivo.it.com/urban-planning-metrics/how-big-is-the-average-city-block-in-meters/)
- [Wikipedia: City block](https://en.wikipedia.org/wiki/City_block)
- [GPSC Principle 6: Human-Scale Streets and Small Blocks](https://www.thegpsc.org/sites/default/files/globalsprawl_11_principle_6_humanscalestsmallblocks_1.pdf)
- [NSW Movement & Place: Principle 4 Permeable Network](https://www.movementandplace.nsw.gov.au/place-and-network/guides/network-planning-precincts-guide/network-planning-and-design-principles/principle-4-create-permeable-network-grid-structure-short-block-length-and-high-intersection)
- [Wakefield District Design Code B3.2.2](https://districtdesigncode.wakefield.gov.uk/part-b-codes-and-guidance/b3-welcoming-places/b32-blocks-and-typologies/b322-block-size)

**도시계획 — 필지/셋백/조닝/건물 타입**
- [LegalClarity: Lot Frontage](https://legalclarity.org/what-is-lot-frontage-and-why-does-it-matter/)
- [RTA Studio: Setbacks/Sideyards/Rearyards](https://www.rtastudio.com/2019/07/setbacks-sideyards-easements/)
- [APA PAS Report 165: Illustrating the Zoning Ordinance](https://www.planning.org/pas/reports/report165.htm)
- [Cyburbia: corner lot which is the front](https://cyburbia.org/forums/threads/corner-lot-which-is-the-front.37947/)
- [Houston Setbacks and Lot Sizes](https://nanproperties.com/blog/houston-setbacks-and-lot-sizes-a-simple-guide)
- [Iowa City 14-2G-4 Frontage Type Standards](https://codelibrary.amlegal.com/codes/iowacityia/latest/iowacity_ia/0-0-0-21299)
- [Lakewood 18C.400.410](https://lakewood.municipal.codes/LMC/18C.400.410)
- [Wikipedia: Building typology](https://en.wikipedia.org/wiki/Building_typology)
- [PEB Steel: Warehouse Design Guide](https://pebsteel.com/en/warehouse-design-guide/)
- [Steelco: Warehouse Loading Dock Design](https://www.steelcobuildings.com/warehouse-loading-dock-design-layout-door-spacing-and-truck-court-planning/)
- [Wikipedia: Loading dock](https://en.wikipedia.org/wiki/Loading_dock)

**게임 — 탑다운 레벨디자인/맵 구축**
- [MY.GAMES (War Robots): Top-down shooter level design](https://medium.com/my-games-company/top-down-shooter-level-design-how-map-design-supports-game-mechanics-6ae39fdd095d)
- [Level Design Book: Composition](https://book.leveldesignbook.com/process/blockout/massing/composition)
- [Level Design Book: Modular kit design](https://book.leveldesignbook.com/process/blockout/metrics/modular)
- [80.lv: Clever Level Design](https://80.lv/articles/how-to-build-good-levels-for-games)
- [Mike Barclay: Level Design Guidelines](https://mikebarclay.co.uk/my-level-design-guidelines/)
- [GameDesignSkills: Modular Level Design](https://gamedesignskills.com/game-design/modular-level-design/)
- [WorldOfLevelDesign: Modular Environment Design 101](https://www.worldofleveldesign.com/categories/game_environments_design/modular-environment-design-101.php)
- [GameDevBits: Synty pack snap specs](https://gamedevbits.com/syntyspecs/)
- [GameDevBits: Build 2.0 from Synty](https://gamedevbits.com/synty-packs/build-2-0-from-synty-studios/)

**게임 — 구체 사례**
- [Kotaku: Hades level design is less random than it seems](https://kotaku.com/hades-level-design-is-less-random-than-it-seems-1845254545) · [Wikipedia: Hades](https://en.wikipedia.org/wiki/Hades_(video_game))
- [U7Buy: Tarkov Map Mastery](https://www.u7buy.com/blog/escape-from-tarkov-map-mastery-guide/) · [Dexerto: Tarkov maps & extractions](https://www.dexerto.com/escape-from-tarkov/all-escape-from-tarkov-maps-and-extraction-points-guide-1309294/)
- [escapefromduckov.net](https://escapefromduckov.net/) · [duckov.com](https://www.duckov.com/) · [vgtimes Duckov map](https://vgtimes.com/guides/138180-interactive-escape-from-duckov-map-all-key-locations-and-points-of-interest.html) · [Medium: Rise of Duckov](https://klaothongchan.medium.com/when-ducks-meet-extraction-shooters-the-rise-of-escape-from-duckov-f49f49ca0a19)
- [Steam: ZERO Sievert](https://store.steampowered.com/app/1782120/ZERO_Sievert/) · [Modern Wolf: ZERO Sievert](https://modernwolf.net/game-for-cabo-studio/zero-sievert)
- [DRG Survivor Wiki: Biomes](https://deeprockgalactic.wiki.gg/wiki/Survivor:Biomes)
- [Rogue Ranker: VS Stages](https://rogueranker.com/vampire-survivors-stages/) · [Terresquall: VS Map Generation](https://blog.terresquall.com/2022/12/creating-a-rogue-like-vampire-survivors-part-2/)
