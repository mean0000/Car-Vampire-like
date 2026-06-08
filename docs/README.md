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

## 🟢 현행 권위 빠른 지도 (스토리 · 전투)

> **스토리·배경·캐릭터를 알고 싶으면 딱 한 문서만 읽는다 — 나머지는 읽지 말 것.**

| 주제 | 지금 읽을 단 하나 | 폐기되어 읽지 말 것 |
|---|---|---|
| **스토리·배경·캐릭터** | `03_reference/2026-06-06-worldbuilding-pitch.md` (+`.html`)<br>— 게임명 "사후처리부", 흑막 = 행정 관성의 괴물, 세 인물(플레이어·팀장·엘) | GDD §4 세계관·§6-4 AI 센터 추적, 옛 HUD 목업, 2026-05-24 핸드오프, 04_archive 일체 (*감염 기술자·SYNC RATE=정신오염·차량 서사 전부 무효*) |
| **전투 방식** | `00_authority/2026-06-03-demo-weapon-lineup.md`<br>— 리볼버(원거리)/야구방망이(근접) 택1 + 소음기 | GDD §무기체계(근접 기본), 04_archive/weapon-system, 옛 차량 전투 |
| **감염 시계·둠클락** | `00_authority/2026-05-31-level-design-authority.md` + `2026-05-29-infection-noise-design.md` | — |

※ 2026-06-06 대개정으로 스토리가 통째로 교체됨. GDD는 **시스템/메카닉(시계·제작·진행·바이옴) 권위로만** 살아있고, **서사 레이어는 worldbuilding-pitch가 대체**한다.

---

## 00_authority — 지금 믿을 문서

| 문서 | 무엇 | 비고 |
|---|---|---|
| `2026-05-27-new-direction-gdd.md` (+`.html`) | **현행 GDD.** 시스템/메카닉(시계·제작·진행·바이옴) 권위 | ⚠️ 스토리/세계관 레이어 폐기 → worldbuilding-pitch |
| `2026-05-29-progression-system.md` | **진행/크래프팅 권위.** 줍는 것=무기강화 설계도/부품 | 구 `crafting-design`(90_archive) 대체 |
| `2026-05-31-level-design-authority.md` | 레벨 디자인 권위 (감염 시계 + 3게이트) | |
| `2026-05-31-levelup-cards-catalog.md` | 레벨업 카드 전체 명세 (~23장) | 카드 권위 |
| `2026-06-02-city-scale-decisions.md` | 도시 스케일/맵 결정 | |
| `2026-06-03-demo-weapon-lineup.md` | **전투 방식·데모 무기 권위.** 리볼버(원거리)/야구방망이(근접) 택1 + 소음기 | GDD §무기체계(근접 기본)는 구버전 |
| `2026-05-29-infection-noise-design.md` | 감염도·소음 수치 곡선 | §1 소음 = **현재 빌드 구현 확정값** |
| `2026-06-08-ingame-hud.md` | **인게임 HUD 권위.** HUD_V2(Greybox_ScanLit) 레이아웃·REAL/PLACEHOLDER·탄약/재장전 | 옛 `04_archive/2026-05-24-uiux-design`(차량 HUD) 대체 |

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
| `2026-05-19-gdd-foundations.md` | GDD v0.1 → new-direction-gdd로 대체 |
| `2026-05-21-gdd-update.md` (+`.html`) | GDD v0.2 (피벗 이전) → 대체됨 |
| `2026-05-21-mvp-tiers.md` (+`.html`) | 폐기된 GDD v0.2 기준 → 무효 |
| `2026-05-25-upgrade-mechanism.md` | 피벗 이전 업그레이드 설계 |
| `2026-05-25-weapon-system.md` | 피벗 이전 무기 설계 |
| `2026-05-29-crafting-design.md` | 소비형 레시피 구조 폐기 → progression-system이 권위 |
| `2026-05-25-design-doc.html` | 피벗 이전 종합 설계 HTML |
| `2026-05-24-uiux-design.md` | 차량 HUD(HULL·SUPPRESS) 전제 — **`00_authority/2026-06-08-ingame-hud.md`로 대체됨.** 재참조 금지 |
| `2026-05-25-hud-mockup-car.html` | 옛 차량 HUD 목업(SYNC RATE=정신오염·HULL·SUPPRESS) — 사후처리부 피벗으로 무효 |
| `2026-05-24-handoff-car.md` | 피벗 이전 차량/SYNC RATE/드리프트 핸드오프 — 스토리·전투 전부 구버전 |

---

## docs 루트에 남겨둔 항목 (이동 안 함)

- **`city_catalog/`** — 도시 에셋 이미지/매니페스트. `03_reference/`의 카탈로그 HTML들과 루트의 `road-catalog.html`이 함께 참조하는 **공유 폴더**라 루트 유지.

### ⚠️ 도로 세션 영역 — 건드리지 말 것
- **`road_catalog/`, `2026-06-02-road-catalog.html`** — 별도 세션이 도로 데이터 정렬 작업 중. `road-catalog.html`은 `city_catalog/`를 참조하므로 `city_catalog/`도 이동 불가.
