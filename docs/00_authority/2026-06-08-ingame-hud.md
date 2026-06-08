# 인게임 HUD (HUD_V2) — 현행 권위

> **상태:** 구현 완료, 플레이테스트 확인(2026-06-08).
> **이 문서가 인게임 HUD의 단일 진실이다.** 옛 `04_archive/2026-05-24-uiux-design.md`(차량 HUD·HULL·SUPPRESS)는 폐기다 — 재참조 금지.

---

## 0. 한눈에

- **씬:** `Greybox_ScanLit`
- **루트:** ScreenSpaceOverlay 캔버스 `HUD_V2` (레거시 XPBarUI/SyncRateUI/HUDController/DashChargesUI를 전면 대체)
- **목업 출처:** `_mockups/ui_mockup.html`의 "02 · IN-GAME HUD"
- **차량 흔적 없음:** HULL/SUPPRESS/대시보드/NANO HARVEST 전부 제거됨. 도보 캐릭터 기준 HP/레벨/탄약 HUD.

### 파일 위치
| 역할 | 파일 |
|---|---|
| 에디터 빌더(메뉴 `Tools/ZombieCrush/Build In-Game HUD`, 멱등) | `Assets/_Project/Scripts/Editor/HudScreenBuilder.cs` |
| 런타임 드라이버(단일 MonoBehaviour) | `Assets/_Project/Scripts/HudV2Controller.cs` |
| 런 통계 소스(타이머·킬) | `Assets/_Project/Scripts/RunStats.cs` |
| 탄약 소스 | `Assets/_Project/Scripts/PlayerCombat.cs` |
| 무기 정의 | `Assets/_Project/Scripts/WeaponLoadout.cs` |

---

## 1. 레이아웃

목업 기준 980px 스테이지를 1920 캔버스로 **스케일 S=1.95**(`HudScreenBuilder`의 `V()`/`F()` 헬퍼)로 키워 배치. CanvasScaler = ScaleWithScreenSize, ref 1920×1080, match=width.

| 영역 | 위젯 |
|---|---|
| **상단 중앙** | 런 타이머 `MM:SS` · 킬 카운트 `REMOVED · NNNN` |
| **좌하단 플레이어 카드** | HP 바 + 이름(`김도현`) + 부제(`현장요원 · LV.N`) + 레벨 라벨(`Lv.N`) + XP 바 + 버프 점등(SPD/ARM/DoT) |
| **우하단 무기 박스** | 무기 이름 + 3글자 아이콘(`REV` 등) + 탄약 상세(`6 / 6`) + 대시 핍 3 |
| **중앙 추종 위젯** | 플레이어 머리 위 = 탄약 미니 / 발 밑 = 대시 미니 핍 3 (매 프레임 스크린좌표 추종) |
| **SYNC 비네트** | 화면 전역 비네트(SYNC 70%+부터 alpha 0→0.6 램프) |

### 바(bar) 렌더 규칙 (★함정)
- 빌트인 둥근 UISprite를 9-slice로 바에 쓰면 **알약/타원으로 늘어남.** 반드시 직렬화한 `white_square.png`(8×8, Single, border 0, Point, Uncompressed) + `Image.Type.Simple`(Fill은 Filled) 사용.
- 클리핑은 `RectMask2D`. 마스킹된 트랙에는 `Outline` 제거(번짐). 세그먼트 틱은 2px.

---

## 2. REAL vs PLACEHOLDER

### REAL — 라이브 데이터 바인딩 (실제 게임플레이 연동)
| 위젯 | 소스 | 비고 |
|---|---|---|
| XP 바 / 레벨 라벨 / 부제 LV | `XPManager` 이벤트 | `OnXPChanged`/`OnLevelChanged` |
| HP 바 | `PlayerController.CurrentHP/MaxHP` | Update 폴링 |
| 대시 핍(코너+미니) | `PlayerController.DashCharges/MaxDashCharges/DashRechargeProgress01` | 충전 중 슬롯 반투명 램프 |
| **탄약(코너+미니)** | `PlayerCombat` | **2026-06-08 신규 — 3절 참조** |
| 런 타이머 / 킬 | `RunStats.ElapsedTime/Kills` | 변경 시에만 텍스트 재할당 |
| SYNC RATE 둠클락 | `SyncRateManager` | 4단 상태색·흔들림·패닉펄스·비네트 |
| 부품 토스트 | `CraftingSystem.Parts` 증가 감지 | `[ PART ] +N 회수` 떠오름 |
| 버프 점등(SPD/ARM) | `PlayerStats.MoveSpeedMult/MaxHPMult` | trivial 점등 |

### PLACEHOLDER — 백엔드 없음(훅만 노출, 후일 바인딩)
- **보스바**(`ShowBoss/SetBossFill/HideBoss`) — 시작 숨김.
- **이벤트 트래커**(좌측 카운트다운 3개) — 현재는 목업처럼 코스메틱 카운트다운(`UpdateEventTrackers`). 실제 이벤트 시스템 미연결.
- **상호작용 프롬프트**(`ShowPrompt/HidePrompt`) — 시작 숨김.
- **버프 DoT 칸** — DoT 시스템 미구현, 항상 OFF.

### 사용자가 끈 것 (현재 레이아웃에서 비활성)
- **스캔라인 오버레이** — CRT 뿌연 질감 제거용. 빌더에서 `SetActive(false)`(BuildScanlines 말미), 라이브 씬도 꺼서 저장. 켜려면 GameObject 활성화.
- ※ 사용자가 직접 UI를 재배치하며 불필요 요소 일부를 비활성화함(2026-06-08). 정리된 레이아웃이 현행.

---

## 3. 탄약/재장전 시스템 (2026-06-08 신규)

가짜 코스메틱 타이머 → **실제 발사 연동**으로 교체. 사용자 요청("총알 발사와 연동").

### 설계
- **무기별 탄창**(`WeaponLoadout.Weapon`에 `magazine`/`reloadTime`):
  | 무기 | 탄창 | 재장전 |
  |---|---|---|
  | 리볼버(원거리) | 6 | 1.1s |
  | 라이플 | 24 | 1.6s |
  | 샷건 | 5 | 2.0s |
  | 야구방망이 / 쇠지렛대(근접) | 0 = 무탄약(무한) | — |
- **주발사(좌클릭)만 1발 소모.** 0이 되면 자동 재장전(그동안 주발사 잠금) + `R` 수동 재장전. 홀드 중 빈탄→자동 재장전 시작.
- **우클릭 alt-fire(난사/차지/개머리판)는 탄창과 독립** — 소모·잠금 안 됨(자체 쿨다운). 밸런스 보존+최소변경 목적. ⚠️난사가 빈 실린더로도 나가므로 플레이감 어색하면 재검토 여지.
- 게임감: 재장전 = 무방비 창(긴장).

### HUD 표시
- `UsesAmmo == false`(근접/무탄약) → 탄약 라벨/미니 숨김.
- 재장전 중 → "재장전…" 앰버.
- 평시 → `{현재}<small> / {탄창}</small>` 화이트(코너·미니 동일).

### 구현 위치
- `WeaponLoadout.cs` — Weapon 구조체 `magazine`/`reloadTime` 필드+값.
- `PlayerCombat.cs` — `_ammo/_magazine/_reloadTime/_reloading/_reloadTimer`, 폴백 `magazineSize=6`/`reloadTime=1.1`, public `UsesAmmo/CurrentAmmo/MagazineSize/IsReloading/ReloadProgress01`, `StartReload()`(reloadTime≤0이면 즉시 풀충전으로 무한탄약 footgun 차단), Fire() 끝에서 차감+0시 StartReload, hotswap(ApplyRanged)이 탄약 풀리셋.
- `HudV2Controller.cs` — `UpdateAmmo()`가 `PlayerController.Instance.GetComponent<PlayerCombat>()` 캐시(누락 1회 경고)로 실값 표시.

상세 설계 근거는 메모리 `project_ammo_system.md` 참조.

---

## 4. 작업/검증 함정 (꼭 읽을 것)

- **빌더 실행:** `EditorApplication.ExecuteMenuItem("Tools/ZombieCrush/Build In-Game HUD")`. (리플렉션으로 Build() 직접 invoke는 MCP에서 실패함.)
- **오버레이 UI 검증:** `Unity_Camera_Capture`는 오버레이를 못 잡는다(빈 화면). 반드시 play모드 `ScreenCapture.CaptureScreenshot(절대경로, 2)` → Read PNG로 눈으로 확인. play 진입/캡처/종료는 RunCommand를 분리해야 함(async). 눈으로 보기 전 "완료" 선언 금지.
- **MCP 타입참조 함정:** RunCommand에서 프로젝트 정의 타입(`HudV2Controller` 등) 컴파일타임 참조 시 잡히지 않는 "UNEXPECTED_ERROR". 에디트 모드 우회 = `GameObject.Find(...).GetComponent("타입명문자열")` + `SerializedObject`. play 중엔 무거운 리플렉션·메서드 invoke 전부 실패.
- **런타임 텍스처:** 비네트/스캔라인 텍스처는 에셋 직렬화 불가 → 매 실행 런타임 빌드(`OnDestroy`에서 명시 해제).
