# AI/절차적 맵 툴링 전수 조사 — 탑뷰 바운디드 존 깔기 (2026-06-14)

> **목적**: Unity 탑뷰 게임 맵(덕코프식 유기적 바운디드 존, 오픈월드 리얼리즘 아님)을 *쉽게 깔기/구성*하는 AI/절차 도구를 전수 조사.
> **방법**: WebSearch/WebFetch 현재 정보(2026-06). 추정은 "(추정)" 표기, 나머지는 출처 확인된 사실.
> **연결 권위**: 우리 맵 앵커는 이미 동결됨 — [[2026-06-13-topdown-map-reference-research]](Duckov 공간/추출 골격 + DRG:S/Megabonk 호드 + Hades II 방 craft). **이 문서는 "그 앵커를 어떤 툴로 시공하나"에 답한다.**

---

## 0. 결론 먼저 (TL;DR)

- **우리 게임에는 "AI 터레인/도시 생성기"가 최선이 아니다.** Gaea·World Creator·CityEngine·Marble은 전부 **리얼리즘 오픈월드/하이트맵/도시 스카이라인**용 — 우리는 이미 [[feedback_use_existing_assets]] + 손배치 Synty 모듈([[project_map_synty_handauthored]])로 동결돼 있고, 탑뷰 바운디드 존은 *높이 없는 평면 가독성*이 생명이라 하이트맵 터레인 자체가 부적합.
- **맞는 방법론 = 모듈러 키트 조립 + 그래프 기반 POI 배치**(WFC는 보조). 즉 *우리가 가진 Synty/Toon City 프리팹을 룰로 깔아주는* 도구가 정답이지, 지형을 통째로 생성하는 도구가 아니다.
- **추천 top 3**: ① **Edgar Pro**(그래프+룸템플릿, 비대칭 거점 배치 정확 일치, $) ② **Dungeon Architect**(그래프 그래머, 런타임/디자인타임, $) ③ **WFC 비주얼 툴/Tessera**(유기적 길·이음새 절차화, 보조 레이어). 터레인이 정말 필요하면 ④ **Gaea 2/Community(무료)** 한 장만.
- **AI 텍스트→씬(Holodeck/SceneCraft/Marble/Unity AI)은 전부 우리 케이스에 미성숙/부적합** — 실내 임베디드AI 연구물이거나(Holodeck), 게임 export가 splat이라 탑뷰 라이팅·게임플레이 메시로 못 씀(Marble), 또는 2D 에셋 생성만(Unity AI). 씬 *배치*를 LLM이 해주는 프로덕션급 제품은 **Promethean AI** 하나뿐인데 이건 "AI 생성"보다 "내 라이브러리로 배치 가속"에 가깝다.

---

## 1. 도구 표

### A. 터레인 생성 (하이트맵/지형)

| 도구 | 무엇 생성 | API/플러그인 | 가격 (2026) | Unity 임포트 | 탑뷰 바운디드 적합도 | 자동화 |
|---|---|---|---|---|---|---|
| **Gaea 2 / 2.2** | 하이트맵·메시·에로전 지형 | Unity 플러그인 *예정*(現 Houdini/UE만), 하이트맵/메시 export는 지금도 됨 | Community 무료(1K·비상업), Indie $99, Pro $199, Ent $299 | 하이트맵→Unity Terrain, 또는 OBJ/메시 | ▲ 낮음 — 리얼 산악/에로전용. 평면 존엔 과함 | 노드그래프(반자동) |
| **Gaea 3.0** | +벡터 도로/강, 모래·강·눈 시뮬, 2.7D 변위 | Unity 플러그인 *예정* | 동일 ($99/$199/$299), **출시 2026 중반·예약** | 하이트맵/메시 | ▲ 낮음(동일 이유). 벡터 도로는 흥미롭지만 탑뷰 평면엔 오버킬 | 노드그래프 |
| **World Creator 2026** | GPU 실시간 지형, 카메라 주변 포커스 | Unity Bridge 플러그인 있음(Blender/C4D/Godot/Houdini/UE/Unity) | Indie $99, Pro $199, 렌탈 $49/yr, **Community 무료(export 잠김)** | Unity Bridge(실시간 동기) | ▲ 낮음 — 오픈월드 지형 특화 | 실시간 슬라이더(반자동) |
| **Gaia Pro VS (Unity)** | Unity 네이티브 지형+나무/풀/물 | Unity 에셋(엔진 내장) | €183 정가(50% 세일 $99.5 빈번), Gaia 무료판 존재 | 네이티브 — Unity 안에서 직접 | ◯ 중간 — 엔진 내장이라 통합 최강, 단 여전히 지형 패러다임 | 스탬프+스폰(반자동) |
| **Unity Terrain Tools + Sample** | Unity 지형 브러시·에로전·스컬프트 | Unity 패키지(공식) | 무료 | 네이티브 | ◯ 중간 — 손스컬프트 보조. 생성기 아님 | 수동(브러시) |
| **Instant Terra** | 노드 기반 지형(Gaea류) | Unity 플러그인 있음(추정 — 공식 페이지 확인 필요) | 유료(추정) | 하이트맵/메시 | ▲ 낮음 | 노드그래프 |
| **EarthSculptor** | 구형 실시간 하이트맵 에디터 | 직접 export | 저가/구형 | 하이트맵 | ▲ 낮음·노후 | 수동 |

> **터레인 카테고리 총평**: 전부 *리얼 지형* 도구다. 우리 탑뷰 바운디드 존은 [[2026-06-13-topdown-map-reference-research]] §4 "비회전/부감이면 높이를 캐릭터 키 수준 제한, 큰 랜드마크는 가장자리만" — 즉 **높이 자체가 적**이다. 터레인 생성기를 쓰면 게임플레이가 못 읽는 굴곡을 깎아내는 데 시간을 쓰게 됨. 자연 장벽(바위·물·절벽)이 *경계*로만 필요하면 Gaea Community(무료)로 한 장 굽거나 Synty 자연물 프리팹으로 충분.

### B. 도시/레벨 레이아웃 생성

| 도구 | 무엇 생성 | API/플러그인 | 가격 (2026) | Unity 임포트 | 탑뷰 바운디드 적합도 | 자동화 |
|---|---|---|---|---|---|---|
| **Esri CityEngine 2025.1** | 룰 기반 절차 도시(Street Designer, CGA) | FBX/USD export, ArcGIS | **렌탈만** $2,200/yr(Pro) / $4,200/yr(Pro+), 영구 라이선스 폐지 | FBX→Unity | ▲ 낮음 — 리얼 GIS 도시 스카이라인. 가격·복잡도 솔로 부적합 | 룰 작성(반자동) |
| **Houdini PDG 시티** | 절차 도시 폴리곤(.prefab까지) | **Houdini Engine for Unity 무료**(엔진 라이선스 별도) | Houdini Indie ~$269/yr, Apprentice 무료(비상업) | Engine 플러그인 — Unity 안에서 파라미터 쿡 | ◯ 중간 — 정확히 *키트 깔기* 가능, 단 러닝커브 가파름 | HDA 파라미터(반자동, 강력) |
| **Blender SceneCity** | 절차 도시(도로망+빌딩) | **Unity 로더 윈도우** 제공(전용 export 노드→txt→Unity 재구축) | $ 유료(cgchan, 가격 미확정 — 공식 확인 필요) | 전용 로더(에셋 매핑 후 1클릭 재구축) | ◯ 중간 — Unity 임포트 경로가 도구 중 가장 깔끔 | 노드+1클릭(반자동) |
| **Blender Buildify** | 모듈러 빌딩(지오노드, OSM 연동) | Geometry Nodes(.blend) | **무료** | FBX export | ◯ 중간 — *건물 1동* 생성기. 맵 배치는 별도 | 지오노드(반자동) |
| **Blender BagaPie** | 스캐터/어레이/불리언 지오노드 프리셋 | 애드온 | 저가/무료티어 | FBX | ◯ 보조 — 프리팹 스캐터(소품 흩기)에 유용 | 모디파이어(반자동) |
| **Edgar Pro (Unity)** | **2D/탑다운 레벨**(그래프 연결+룸 템플릿) | 네이티브 Unity 에셋(itch/AssetStore) | $ 저가(개인 개발자) | 네이티브 — 타일맵/프리팹 룸 | **★ 높음** — "방 N개·연결구조 지정→정확히 그대로" = 비대칭 거점 배치 직결 | 그래프 정의→자동생성 |
| **Dungeon Architect (Unity)** | 절차 레벨(그래프 그래머, 런/디자인타임) | 네이티브 Unity 에셋 | $ 저가 | 네이티브 — 프리팹 테마 | **★ 높음** — 그래프 그래머로 굽은 길·거점 룰 표현 | 그래프 룰(반자동~자동) |
| **WFC Unity (Tessera / 2025 비주얼툴 / Boris the Brave)** | 타일 인접 규칙 기반 레벨(2D/3D) | 네이티브 Unity 에셋/오픈소스 | Tessera $, 비주얼툴 $, mxgmn 원본 무료 | 네이티브 — 타일/프리팹 | ◯ 보조 높음 — *유기적 길·이음새*를 절차화. 단독 거점배치는 약함 | 인접규칙(자동) |
| **Unity 절차 도시 에셋(다수)** | 빌딩/블록 스폰 키트 | 네이티브 | 다양 | 네이티브 | ◯ 중간 | 다양 |

### C. ★AI 텍스트→씬 / LLM 씬 배치

| 도구 | 무엇 | 형태 | 가격 | Unity 경로 | 탑뷰 바운디드 적합도 | 비고 |
|---|---|---|---|---|---|---|
| **Holodeck** (AI2) | 텍스트→3D 임베디드AI 환경(방·가구) | **연구물**(GPT-4가 평면도·자산배치) | 오픈(연구) | 직접 경로 없음(AI2-THOR 대상) | ✕ 부적합 — *실내 로봇 학습용*, 게임 메시 아님 | arXiv 2312.09067 |
| **SceneCraft** | 텍스트→Blender 파이썬 코드로 씬 | **연구물**(LLM 에이전트) | 오픈(연구) | Blender 경유(간접) | ✕ 부적합 — 단일 씬 합성, 게임 레벨 아님 | arXiv 2403.01248 |
| **3D-GPT** | 텍스트→Infinigen(Blender 래퍼) 호출 | **연구물** | 오픈(연구) | Blender 경유 | ✕ 부적합 — 자연 환경 합성 | — |
| **Infinigen / Infinigen Indoors** | 절차 자연/실내(제약 기반 배치) | 오픈소스(Blender 위) | 무료(BSD) | Blender→FBX(간접) | ▲ 낮음 — 포토리얼 자연/실내, 탑뷰·스타일라이즈드 아님 | 제약기반 배치는 개념 참고 가치 |
| **AnyHome** | 텍스트→집 평면 | 연구물(자료 희소) | 오픈(추정) | 직접 경로 없음(추정) | ✕ 부적합 | 정보 부족 |
| **Promethean AI** | 자연어→**내 라이브러리로** 씬 배치/팝 | **제품**(에디터 내, Unity/UE) | 유료(구독, 가격 비공개) | 에디터 통합 | ◯ 가장 현실적 — "AI 생성"보다 *내 에셋 배치 가속*. 좌표 배치 노가다 절감 | 솔로엔 가격 검토 필요 |
| **Layer.ai** | AI 2D 에셋(텍스처/스프라이트) 생성 | 제품 + **Unity 6.2 Generators 백엔드 통합** | 유료/구독 | Unity Generators로 직결 | ✕ 씬배치 아님 — 2D 에셋용 | — |
| **Hexagen.World** | 브라우저 협업 AI 헥사타일 캔버스 | 웹 게임/실험 | 무료(브라우저) | 없음 | ✕ 부적합 — 게임 자산 아님 | (Hexa3D=별개 이미지→3D 제품) |
| **World Labs — Marble 1.1** | 텍스트/이미지/영상/거친3D→영속 3D 월드 | **제품**(웹), splat/메시 export | 유료(무료 티어 있음) | **splat 또는 메시→Unity** export 가능 | ▲ 낮음 — splat은 탑뷰 라이팅·콜라이더·게임플레이 메시로 부적합. 메시는 클린업 필요. 실내/장면용 | TechCrunch 2025-11 출시. 1.1 = 2026 |
| **Unity AI (6.2, 2026)** | 에디터 내 sprite/texture/material/sound/anim 생성 + Assistant | **네이티브**(Muse/Sentis 대체) | Unity 구독 연동 | 네이티브 | ✕ 레벨 배치 아님 — **2D 에셋·코드 보조만**. 씬 생성/배치 없음 | Scenario·Layer AI 모델 + Unity 자체(타일 텍스처) |

---

## 2. 방법론 비교 — ★탑뷰 맵을 AI/절차로 까는 최선

네 가지 접근법을, *우리 게임(탑뷰·바운디드·스타일라이즈드·Synty 보유)* 기준으로 비교:

| 접근법 | 원리 | 강점 | 약점 | 맞는 도구 | 우리 적합도 |
|---|---|---|---|---|---|
| **① 하이트맵/터레인** | 노이즈·에로전으로 지형면 생성 | 광대한 자연 지형 | 평면 가독성 파괴, 높이=탑뷰의 적, 콜라이더 복잡 | Gaea/World Creator/Gaia | **하** — 경계 자연장벽 한정 |
| **② 모듈러 키트 조립** | 손제작 프리팹을 룰/노드로 배치 | 우리 보유 에셋 직활용, 아트 일관성, 게임플레이 메시 통제 | 키트 품질이 천장, 룰 작성 노동 | Houdini PDG, SceneCity, Buildify, BagaPie | **상** — [[project_map_synty_handauthored]]와 정합 |
| **③ 그래프 기반 POI 배치** | 거점=노드/연결=엣지로 위상 정의→공간화 | **난이도 띠·비대칭 거점·골든패스 직접 표현**, 페이싱 통제 | 미적 디테일은 키트에 의존 | **Edgar Pro, Dungeon Architect** | **★최상** — Duckov 비대칭 추출구·동심원 보상과 1:1 |
| **④ 타일맵/WFC** | 인접 규칙으로 타일 채움 | 유기적 길·자연스러운 이음새, 무한 변주 | 전역 구조(거점 위치) 통제 약함, 도달성 보장 추가 작업 | Tessera, WFC 비주얼툴, mxgmn | **중(보조)** — ③의 거점 사이 *길·필러* 채우기 |

### ★핵심 통찰: 단일 접근법이 아니라 **계층 조합**이다

탑뷰 바운디드 맵의 정석 파이프라인은 **③ 그래프로 골격 → ② 키트로 거점 살 붙이기 → ④ WFC로 사이 길/디테일 → ①은 경계 자연장벽만**.

- **거점/페이싱(=재미의 뼈대)** 은 절대 풀 AI에 맡기지 않는다 — 그래프(③)로 *설계자가 의도*를 박는다. [[2026-06-13-topdown-map-reference-research]] §4 "보상을 자연 초크 근처 배치→수렴 유도", §6 "난이도 띠+비대칭 추출구+동심원" 이 전부 **그래프 위상 결정**이다.
- **사이 공간/길(=노동)** 은 WFC(④)나 키트 스캐터(②)로 절차화해 노가다를 던다.
- **AI 텍스트→씬은 현재 ③④를 못 대체한다** — 게임플레이 의도(난이도 곡선, 도주선, 수렴 초크)를 LLM이 이해 못 하고, export가 splat/연구물이라 통합도 막힘.

---

## 3. ★덕코프식 유기적 컴파운드 맵 — 최적 도구+파이프라인

**요구**: 비대칭 거점(추출구 ≥3, 비용 차등) + 굽은 길 + 자연 장벽 + 난이도 띠 + 동심원 보상([[2026-06-13-topdown-map-reference-research]] §2~3).

### 추천 파이프라인 (솔로·Unity·Synty 보유 전제)

```
1. [골격] Edgar Pro 또는 Dungeon Architect
   → 거점을 노드로, 연결을 엣지로 그래프 정의
   → 비대칭 추출구 3개 = 종단 노드 3개(개방/은밀/고위험)
   → 난이도 띠 = 중앙→외곽 동심 레이어로 노드 배치
   verify: 생성 결과가 "중앙 고밀도·외곽 저위험" 위상을 지키나

2. [살] 룸 템플릿 = 손제작 Synty/Toon City 모듈 프리팹
   → Edgar 룸템플릿/DA 테마에 우리 프리팹 매핑
   → 굽은 길 = 직선 금지, L/곡선 모듈 우선(굽음=Duckov 정체성)
   verify: NavMesh 연결성(모듈 이음새 틈), 탑뷰 가독성

3. [사이] WFC(Tessera) — 선택
   → 거점 사이 통로/잔해/필러를 인접규칙으로 채워 변주
   verify: 도달성(Dijkstra로 고립 영역 제거)

4. [경계] 자연 장벽만 Gaea Community(무료) 또는 Synty 자연물
   → 절벽/물/바위로 맵 가두기(걷는 면은 평면 유지)
   verify: 높이가 게임플레이 면에 안 들어옴
```

### 왜 이 조합인가

- **Edgar Pro가 1순위인 이유**: "방 몇 개, 어떻게 연결" 을 *정확히 그대로* 생성 — 비대칭 거점·골든패스가 곧 그래프다. 손제작 룸템플릿 = 우리 보유 Synty 모듈을 그대로 쓰는 구조라 [[feedback_use_existing_assets]] 위반 없음. 2D/탑다운 특화.
- **Dungeon Architect 병기 이유**: 그래프 *그래머*(생성 규칙)라 "굽은 길·거점 룰"을 더 유기적으로. 런타임 생성도 지원(런 단위 변주에 유리).
- **Houdini PDG는 강력하나 2순위**: .prefab까지 뽑고 Unity 내 파라미터 쿡이 되지만 러닝커브가 가팔라 솔로 ROI가 낮음. 도시 *블록* 양산이 정말 필요해지면 그때.
- **풀 AI 생성(Marble/Holodeck 등)은 제외**: 게임플레이 의도 통제 불가 + export 통합 미성숙. 우리 동결 앵커(의도된 비대칭·페이싱)와 충돌.

---

## 4. 추천 랭킹 (비용·품질·통합 종합)

| 순위 | 도구 | 비용 | 왜 |
|---|---|---|---|
| **1** | **Edgar Pro** | $ (저가) | 그래프+룸템플릿 = 탑뷰 비대칭 거점 배치에 정확 일치. 우리 Synty 모듈 그대로 룸으로. Unity 네이티브. 솔로 ROI 최상. |
| **2** | **Dungeon Architect** | $ (저가) | 그래프 그래머로 굽은 길·거점 룰 표현, 런타임 변주. Edgar와 보완 관계. |
| **3** | **WFC (Tessera / 2025 비주얼툴)** | $ / 무료 | 거점 사이 통로·디테일 절차화. 단독 아닌 ①②의 보조 레이어로. |
| **4** | **Houdini PDG + Engine for Unity** | 무료 플러그인(엔진 별도) | 도시 블록 양산이 정말 필요할 때. 강력하나 러닝커브로 후순위. |
| **5** | **Gaea 2 Community(무료)** | 무료 | 경계 자연장벽 한 장 굽기 한정. 본체 아님. |

**탈락(우리 케이스 부적합)**: CityEngine(가격·리얼GIS), World Creator/Gaia(오픈월드 지형 패러다임), Marble/Holodeck/SceneCraft/3D-GPT/AnyHome(AI 씬생성 미성숙·통합 막힘), Unity AI(2D 에셋만), Promethean AI(배치 가속 제품이나 구독가·"생성" 아님 — 여력 생기면 재검토).

---

## 5. 사실 vs 추정 구분

**확인된 사실(출처 有)**: Gaea 2/3 가격·티어·Unity 플러그인 예정 상태 / World Creator 2026 가격·Unity Bridge·Community판 / CityEngine 렌탈 전환·가격·FBX→Unity / Unity Muse 은퇴→Unity AI(6.2, 2026-01)·Generators 범위·Layer/Scenario 통합 / Marble export(splat·메시·Unity/UE)·2025-11 출시 / Edgar Pro·Dungeon Architect 그래프 방식 / Buildify 무료·OSM / Holodeck·SceneCraft·3D-GPT 연구물 성격.

**추정/미확인(별도 확인 요)**: SceneCity 정확 가격(cgchan 공식 페이지 미확인) / Instant Terra Unity 플러그인 유무 / Promethean AI 구독가(비공개) / AnyHome 세부.

---

## 출처

**터레인**: [Gaea 3.0(CGChannel)](https://www.cgchannel.com/2025/12/quadspinner-unveils-gaea-3-0/) · [Gaea 2.0(80.lv)](https://80.lv/articles/rewritten-engine-new-terrain-tools-ue5-bridge-gaea-2-0-unveiled) · [Gaea 2.2(CGChannel)](https://www.cgchannel.com/2025/07/quadspinner-releases-gaea-2-2/) · [Gaea 가격(QuadSpinner Compare)](https://quadspinner.com/compare) · [World Creator 2026.4 Community(CGChannel)](https://www.cgchannel.com/2026/04/world-creator-2026-4-is-out-with-a-new-free-community-edition/) · [World Creator Unity Bridge](https://docs.world-creator.com/reference/export/bridge-tools/unity-bridge) · [Gaia Pro VS(Unity Asset Store)](https://assetstore.unity.com/packages/tools/terrain/gaia-pro-vs-terrain-trees-grass-water-for-unity-6-263149) · [Unity Terrain Tools(docs)](https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.0/manual/installing-terrain-tools.html)

**도시/레벨**: [CityEngine 2025.0(CGChannel)](https://www.cgchannel.com/2025/06/esri-releases-cityengine-2025-0/) · [CityEngine 가격(DigitalProduction)](https://digitalproduction.com/2025/07/01/cityengine-2025-0-street-designer-arrives-perpetual-licensing-leaves/) · [Houdini Engine for Unity(GitHub)](https://github.com/sideeffects/HoudiniEngineForUnity) · [Houdini Engine 무료(SideFX)](https://www.sidefx.com/community/houdini-engine-for-unreal-and-unity/) · [PCG City(GitHub)](https://github.com/Davecodingking/PCG-city-generator) · [SceneCity(cgchan)](http://www.cgchan.com/) · [SceneCity→Unity(docs)](https://www.cgchan.com/static/doc/scenecity/1.5/export_to_unity.html) · [Buildify(CGChannel)](https://www.cgchannel.com/2022/07/download-free-blender-3d-building-generator-buildify/) · [Edgar(docs)](https://ondrejnepozitek.github.io/Edgar-Unity/docs/next/introduction/) · [Edgar(GitHub)](https://github.com/OndrejNepozitek/Edgar-Unity) · [Dungeon Architect](https://dungeonarchitect.dev/unity) · [Tessera(Unity Discussions)](https://discussions.unity.com/t/released-tessera-generate-3d-tile-based-levels-with-wave-function-collapse/767170) · [WFC 원본(mxgmn)](https://github.com/mxgmn/WaveFunctionCollapse)

**AI 씬/배치**: [Holodeck(arXiv)](https://arxiv.org/pdf/2312.09067) · [SceneCraft(arXiv)](https://arxiv.org/html/2403.01248v1) · [Promethean AI(SwitchTools)](https://www.switchtools.io/tool/prometheanai) · [Marble(TechCrunch)](https://techcrunch.com/2025/11/12/fei-fei-lis-world-labs-speeds-up-the-world-model-race-with-marble-its-first-commercial-product/) · [Marble export(Skywork)](https://skywork.ai/blog/ai-image/marble-export-mesh/) · [Unity AI 6.2(80.lv)](https://80.lv/articles/unity-goes-all-in-on-generative-ai-introducing-a-bunch-of-ai-features-in-6-2-update) · [Unity Generators(docs)](https://docs.unity3d.com/6000.2/Documentation/Manual/com.unity.ai.generators.html) · [Unity Muse 은퇴(GameFromScratch)](https://gamefromscratch.com/unity-ai-muse-tools-review/) · [Hexagen.World](https://hexagen.world/)

**방법론**: [Roguelike 절차 레이아웃(Grid Sage)](https://www.gridsagegames.com/blog/2019/03/roguelike-level-design-addendum-procedural-layouts/) · [WFC tips(Boris the Brave)](https://www.boristhebrave.com/2020/02/08/wave-function-collapse-tips-and-tricks/) · [PCG GenAI 2025(ThinkGamerZ)](https://www.thinkgamerz.com/procedural-content-generation-genai-ai-level-design/) · [Brotato vs VS 아레나(GamesAsylum)](https://www.gamesasylum.com/2024/02/06/brotato-review/)
