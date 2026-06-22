# ZombieCrush 문서 인덱스

> 이 파일이 진입점이다. "지금 믿을 문서가 뭔지" 여기서 먼저 확인하고 들어간다.
> 폴더는 **상태(현행/이력)**와 **종류**로 나뉜다. 숫자 접두사가 낮을수록 "지금 진실"에 가깝다.

| 폴더 | 의미 | 손대는 빈도 |
|---|---|---|
| `00_authority/` | **현행 권위 문서.** 설계 진실은 여기서만 확인 | 자주 |
| `01_handoffs/` | 세션간 전달용 핸드오프 (작업 인계 시점 스냅샷) | 가끔 |
| `02_logs/` | 날짜별 회의록·탐구·진단 기록 | 읽기 전용 |
| `03_reference/` | 외부 레퍼런스·시각자료(HTML)·이미지·에셋 매핑 | 참고용 |
| `04_archive/` | **폐기·구버전.** 재제안 금지. 흔적 보존만 | 거의 안 봄 |

---

## 🟢 현행 권위 빠른 지도 (코어 · 스토리 · 전투)

> **주제별로 딱 한 문서만 읽는다 — 나머지는 읽지 말 것.**

| 주제 | 지금 읽을 단 하나 | 폐기되어 읽지 말 것 |
|---|---|---|
| **코어 시스템 (런 구조·경제·진행)** | [[2026-06-09-postprocessing-core-design]]<br>— 뱀서+익스트랙션+로그라이트, 사무실 허브+이산 런+3축(HP/작전 타이머/싱크) | 감염 시계·3게이트·설계도/부품 파밍·strain 크래프팅(06-11 폐기 → 본사 보급+일당 공제) |
| **스토리·배경·캐릭터** | [[2026-06-06-worldbuilding-pitch]] (+`.html`)<br>— 게임명 "사후처리부", 흑막 = 행정 관성의 괴물, 세 인물(플레이어·팀장·엘). 어휘는 [[2026-06-11-story-core-lexicon]] | GDD §4 세계관·§6-4 AI 센터 추적, 옛 HUD 목업, 2026-05-24 핸드오프, 04_archive 일체 (*감염 기술자·SYNC RATE=정신오염·차량 서사 전부 무효*) |
| **전투 방식·질감** | [[2026-06-09-postprocessing-core-design]] §4 + [[2026-06-10-combat-texture-foundation]]<br>— "순간의 무게", 카메라 45°/15m | 리볼버/방망이 2종 택1([[2026-06-03-demo-weapon-lineup]]), GDD §무기체계, 04_archive/weapon-system, 옛 차량 전투 |
| **감염 시계·둠클락** | 🟥 **개념 자체 폐기 (06-09).** 작전 타이머+이벤트 풀로 대체 → [[2026-06-09-postprocessing-core-design]] | [[2026-05-31-level-design-authority]], [[2026-05-29-infection-noise-design]] §2 |

※ 2026-06-06 대개정으로 스토리 교체, **2026-06-09 대전환으로 시스템 레이어 교체**(익스트랙션). 옛 GDD는 역사 참고용으로만 남음.

---

## 🔗 링크 컨벤션 (옵시디언 Vault)

> 이 `docs/` 폴더는 옵시디언 Vault로 열어 본다 (쓰기는 Claude가 터미널에서, 탐색은 옵시디언 그래프/백링크로). 문서를 쓰거나 폐기할 때 아래 규칙을 따른다.

1. **문서 간 참조는 `[[파일명]]` 위키링크** (경로·확장자 없이). 예: `[[2026-06-09-postprocessing-core-design]]`
2. **폐기/대체 시 제목 바로 아래 배너 한 줄**: `> 🟥 **폐기 (날짜).** 사유. 현행: [[현행문서]]` — 부분 구식이면 `> ⚠️ **부분 구식 (날짜).**` + 무효 범위 명시
3. **새 권위 문서·핸드오프는 선행 문서를 위키링크로 인용**한다 (백틱 파일명 ❌) — 그래프에 계보가 남는 게 목적
4. 새 권위 문서가 생기면 이 README의 00_authority 표에 행 추가, 옛 문서엔 배너 추가

---

## 00_authority — 지금 믿을 문서

### 현행

| 문서 | 무엇 | 비고 |
|---|---|---|
| [[2026-06-09-postprocessing-core-design]] | ★ **코어 디자인 권위.** 익스트랙션 전환 — 사무실 허브+이산 런+3축(HP/타이머/싱크) | 06-11 개정: strain 크래프팅 폐기 → 본사 보급+일당 공제 |
| [[2026-06-10-design-compass]] | 디자인 나침반 (v1.1) — 판단 기준 | |
| [[2026-06-10-production-charter]] | 생산 헌장 — 스펙동결→구현→리뷰→게이트→잠금 | |
| [[2026-06-10-combat-texture-foundation]] | 전투 질감 기반 — "순간의 무게", 죽음 3역할 | |
| [[2026-06-10-camera-system]] | 카메라 권위 — 45°/15m 확정 | 줌 기각 |
| [[2026-06-10_그래픽_베이스라인_v1]] | 그래픽 베이스라인 v1 (라이팅 바이블 p.1) | COZY = Sky Authority 복귀 |
| [[2026-06-08-ingame-hud]] | **인게임 HUD 권위.** HUD_V2(Greybox_ScanLit) 레이아웃·REAL/PLACEHOLDER·탄약/재장전 | 옛 `04_archive/2026-05-24-uiux-design`(차량 HUD) 대체 |
| [[2026-06-11-story-core-lexicon]] | 스토리 코어 렉시콘 — 동결 어휘·레지스터 | 신규 네이밍은 여기 경유 |
| [[2026-06-11-feel-bolt-process]] | Feel 볼트온 프로세스 | |
| [[2026-06-11-corner-lab-spec]] | B-008 모퉁이 랩 스펙 | |
| [[2026-06-11-e001-stakes-bolton-spec]] | E-001 판돈 볼트온 스펙 | 게이트 판정 대기 |
| [[2026-06-12-b004-rapidfire-spec]] | B-004 트랜지언트 행렬 스펙 (동결) | |
| [[2026-06-12-e002-settlement-spec]] | E-002 정산서 수지 — 납품→일당→공제→실수령 | 게이트 대기 |
| [[2026-06-03-synty-demo-placement-rules]] | Synty 도시 컴포지션·배치 규칙 | 손제작 모듈 권위 |
| [[2026-06-02-city-scale-decisions]] | 도시 스케일/맵 결정 | ⚠️ 게이트 거리(300/650/1000) 부분은 06-08 게이트 폐기로 무효 |
| [[2026-06-21-engine-decision-art-camera-northstar]] | ★ **엔진 결정 + 아트/카메라 북극성.** Unity(URP) 유지(언리얼 기각)·Ruiner=룩/카메라 겨냥점·노을 무드 URP 레시피·카메라 기법 스펙·순서대로 작업 계획 | 방향 동결 |

### 🟥 무효·부분 구식 (역사 참고용 — 각 문서 머리 배너 참조)

| 문서 | 무엇이었나 | 현행 |
|---|---|---|
| [[2026-05-27-new-direction-gdd]] (+`.html`) | 옛 GDD v3.0 | 시스템 → [[2026-06-09-postprocessing-core-design]], 서사 → [[2026-06-06-worldbuilding-pitch]] |
| [[2026-05-29-progression-system]] | 설계도/부품 파밍 진행 | 🟥 06-09/11 폐기 → 본사 보급+일당 공제 |
| [[2026-05-31-level-design-authority]] | 감염 시계 + 3게이트 | 🟥 06-09 전면 무효 → 작전 타이머+이벤트 풀 |
| [[2026-05-29-infection-noise-design]] | 감염도·소음 수치 곡선 | ⚠️ §2 감염 무효 / §1 소음 = 빌드 실측 기록 |
| [[2026-05-31-levelup-cards-catalog]] | 레벨업 카드 ~23장 명세 | ⚠️ 시계축 카드 전제 무효 — 적용 전 코어 디자인 교차 확인 |
| [[2026-06-03-demo-weapon-lineup]] | 리볼버/방망이 2종 택1 | 🟥 06-09 대체 |
| [[2026-06-03-page1-outskirts-blueprint]] | 옛 시작지(외곽 주택가) | ⚠️ 시작지 피벗으로 강등 — 후반존 재활용 후보 |

## 01_handoffs — 핸드오프

zombie-ai / map-implementation / city-map / crafting / levelup-cards / greybox-setup / asset-system-mapping 등 작업 인계 스냅샷. 인계 시점 기준이라 현행과 다를 수 있음.

## 02_logs — 기록

brainstorming / session·session2 / upgrade-system-log / game-direction-pivot / funness-diagnosis / **levelup-verbs**(동사 후보 카탈로그·탐구, 카드 명세는 cards-catalog가 권위).

## 03_reference — 레퍼런스/시각자료

- **`2026-06-06-worldbuilding-pitch.md` (+`.html`) — 스토리·배경·캐릭터 단일 권위 (위 🟢 빠른 지도 참조)**
- `hell-express-reference.md`(광원분석)
- HTML 뷰: game-overview / levelup-cards-view / city-blueprint / city-vision / session / **city-asset-catalog · prefab-contact-index · zone-reference-board** (※ 옛 차량 `hud-mockup`은 04_archive로 내림)
- `images/`(무드보드), `references/`(외부 게임 스크린샷 레퍼런스)
- ※ city-asset-catalog·prefab-contact-index·zone-reference-board 는 루트 `city_catalog/`를 `../city_catalog/`로 참조한다 (city_catalog는 도로 세션 공유라 루트 유지).

## 04_archive — 폐기·구버전 (재제안 금지)

| 문서 | 폐기 사유 |
|---|---|
| [[2026-05-19-gdd-foundations]] | GDD v0.1 → [[2026-05-27-new-direction-gdd]]로 대체 (그것도 06-09 이후 역사 참고용) |
| [[2026-05-21-gdd-update]] (+`.html`) | GDD v0.2 (피벗 이전) → 대체됨 |
| [[2026-05-21-mvp-tiers]] (+`.html`) | 폐기된 GDD v0.2 기준 → 무효. 현행 생산 흐름은 [[2026-06-10-production-charter]] |
| [[2026-05-25-upgrade-mechanism]] | 피벗 이전 업그레이드 설계 |
| [[2026-05-25-weapon-system]] | 피벗 이전 무기 설계 |
| [[2026-05-29-crafting-design]] | 소비형 레시피 구조 폐기 → [[2026-05-29-progression-system]]이 대체 (그것도 06-09/11 폐기) |
| `2026-05-25-design-doc.html` | 피벗 이전 종합 설계 HTML |
| [[2026-05-24-uiux-design]] | 차량 HUD(HULL·SUPPRESS) 전제 — **[[2026-06-08-ingame-hud]]로 대체됨.** 재참조 금지 |
| `2026-05-25-hud-mockup-car.html` | 옛 차량 HUD 목업(SYNC RATE=정신오염·HULL·SUPPRESS) — 사후처리부 피벗으로 무효 |
| [[2026-05-24-handoff-car]] | 피벗 이전 차량/SYNC RATE/드리프트 핸드오프 — 스토리·전투 전부 구버전 |

---

## docs 루트에 남겨둔 항목 (이동 안 함)

- **`city_catalog/`** — 도시 에셋 이미지/매니페스트. `03_reference/`의 카탈로그 HTML들과 루트의 `road-catalog.html`이 함께 참조하는 **공유 폴더**라 루트 유지.

### ⚠️ 도로 세션 영역 — 건드리지 말 것
- **`road_catalog/`, `2026-06-02-road-catalog.html`** — 별도 세션이 도로 데이터 정렬 작업 중. `road-catalog.html`은 `city_catalog/`를 참조하므로 `city_catalog/`도 이동 불가.
