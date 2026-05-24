# ZombieCrush — 세션 로그
**날짜:** 2026-05-25  
**작업 시간:** 2026-05-24 설계 → 2026-05-25 구현 시작  
**상태:** HUD 설계 확정 완료 / Unity 배치 1단계 진행 중

---

## 1. 오늘 한 일

### 1-1. 기존 UIUX 문서 검토 및 현황 파악
`docs/2026-05-24-uiux-design.md` 기반으로 코드베이스 실태 점검.

| 항목 | 상태 |
|---|---|
| SyncRateManager.cs | ✅ 완료 |
| SyncRateUI.cs | ✅ 완료 (비네팅 포함) |
| PartnerAIUI.cs | ✅ 완료 (60%/90% bark) |
| BoostGaugeUI.cs | ⚠️ 색상/라벨 변경 필요 |
| UpgradeMenuUI.cs | ⚠️ 두 타입 분리 필요 |
| Canvas 계층 구조 | ❌ 평면 나열, 레이어 없음 |
| HullManager.cs | ❌ 미구현 (신규 메카닉) |

---

### 1-2. HUD 설계 전면 재설계

기존 문서의 레이아웃(상단 게이지 + 하단 AI)을 폐기하고, 새 구조 확정.

**최종 확정 레이아웃:**

```
[NANO HARVEST ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━]
                   [🔑 12:34]

         [ 게임플레이 영역 ]
             🚗 💨 (후방 파티클)

[파트너AI │ HULL ████░ │ SYNC RATE ████░]   [SUPPRESS ▓▓▓░]
  ← 청록/홀로그램 대시보드 →              ← 검정/노란 연료계 →
```

---

### 1-3. 핵심 설계 결정 사항

#### ① 속도계 제거
- **이유:** 플레이어가 속도를 체감으로 이미 알고 있음. SUPPRESS 잔량을 보고 부스트 여부를 판단하지, km/h 숫자를 보지 않음.
- **결론:** 불필요한 정보는 HUD에서 제거. 정보 밀도 최소화.

#### ② 모든 게이지를 하단으로 집중
- **이유:** 탑다운 시점에서 하단 = 차량 계기판 감성. 차량 온보드 진단 시스템이 화면 하단에 투사되는 느낌.
- **효과:** 게임플레이 영역이 거의 전부 비어있어 시야 확보 극대화.

#### ③ 비네팅은 HUD 위에 (FX 레이어)
- **이유:** 장르 관례. 대부분의 게임에서 비네팅은 HUD를 포함한 화면 전체를 감쌈. SYNC RATE 위험 시 전체 화면이 붉게 물드는 연출 목적.
- **트레이드오프:** 코너 UI가 살짝 가려질 수 있지만, 위험할수록 HUD도 물드는 게 의도된 연출.

#### ④ SUPPRESS — 아날로그 연료계 (검정 배경 + 노란/주황)
- **이유:** SUPPRESS는 "물리적 에너지" — 나노봇 시스템이 읽는 디지털 데이터가 아니라 실제 연료. 좌측 홀로그램 패널과 의도적으로 다른 질감.
- **디자인 언어 대비:**
  - 좌측 (청록/홀로그램) = AI 진단 시스템이 읽는 데이터 (HULL, SYNC RATE)
  - 우측 (검정/노란) = 실제 물리 연료계, 아날로그, 투박함

#### ⑤ SUPPRESS 월드 피드백 — 차량 후방 파티클
- **이유:** 부스터를 쓰는 동안 플레이어 눈이 하단 게이지로 이동 → 게임플레이 놓침 (eye travel 문제). 차량 후방에 푸른 불꽃 파티클로 "부스터 준비됨/소진 중"을 차에서 직접 읽게 함.
- **역할 분리:** 파티클 = 현재 상태(충전/소진), 하단 게이지 = 정확한 잔량.

#### ⑥ 차량 내구도 (HULL) 신규 추가
- **이유:** SYNC RATE는 "부스터를 써서" 올라감 (내 선택). HULL은 "좀비에 치여서" 줄어듦 (회피 실패). 두 개의 죽음 조건이 만드는 이중 긴장감.
- **플레이어 딜레마:** 좀비를 들이받으면 SUPPRESS 충전 + HULL 감소. 피하면 HULL 안전 + SUPPRESS 연료 부족.
- **회복:** 피트스톱(기지)에서만 가능. 절대 쉽지 않게.

#### ⑦ XP 바 → NANO HARVEST
- **이유:** XP 수집 = 나노봇 흡수. 차량 시스템이 흡수한 나노봇 데이터를 축적 → 임계량에서 업그레이드 가능. 세계관과 메카닉이 일치.

#### ⑧ 플레이타임 + 키링
- **이유:** 현장 기술자가 차에 달아둔 소품. 기능은 타이머지만 캐릭터 소품으로 읽힘. 설명 없이 캐릭터성 전달.

---

### 1-4. Canvas 계층 구조 재설계

```
Canvas [CanvasScaler: Scale with Screen Size / 1920×1080 / Match 0.5]
├── HUD                    ← 항상 표시
│   ├── XP_Bar             [anchor: top-stretch, h:8px]
│   ├── Playtime           [anchor: top-center]
│   ├── DashboardLeft      [anchor: bottom-left, 680×110]
│   │   ├── PartnerAI
│   │   ├── HullGauge
│   │   └── SyncRateGauge
│   └── DashboardRight     [anchor: bottom-right, 340×110]
│       └── SuppressGauge
├── FX                     ← 연출 레이어 (HUD 위)
│   └── Vignette           [stretch-all, alpha:0 시작]
└── Panels                 ← 모달 (최상위)
    ├── UpgradePanel
    └── GameOverPanel
```

**Unity 작업 완료:** Canvas 재구성 스크립트 실행 완료. CanvasScaler 설정, 모든 그룹 생성 및 기존 오브젝트 이동 완료.

---

### 1-5. HTML 목업 제작
`docs/hud-mockup.html` — 디자이너용 레퍼런스 목업.

포함 요소:
- 스캔라인 애니메이션 (홀로그램 패널)
- 파티클 깜빡임 애니메이션 (차량 후방)
- 세그먼트 구분선 (SUPPRESS 연료계)
- 비네팅 효과
- 레전드 (각 요소 설명)

---

## 2. 내일 해야 할 일

### Phase 2 — HUD 요소 배치 (코드 연결)

| 순서 | 작업 | 핵심 내용 |
|---|---|---|
| **1** | **HullManager.cs 신규 작성** | 차량 내구도 싱글톤. AddDamage() / Heal() / OnHullDepleted 이벤트. CarController.ApplySpeedPenalty() 호출 시 연동. |
| **2** | **HullGaugeUI.cs** | HullManager 이벤트 구독 → fillImage 갱신. 손상 시 색상 청록→주황→적 변화. |
| **3** | **SyncRateUI → DashboardLeft 연결** | 기존 SyncRateUI.cs를 씬의 새 SyncRateGauge 오브젝트에 연결. Inspector에서 fillImage 재연결 필요. |
| **4** | **BoostGaugeUI 색상 변경** | colorNormal → 노란/주황 (#FF9900). 기존 Boost 오브젝트 비활성, DashboardRight의 SuppressFill에 스크립트 연결. |
| **5** | **XPBarUI 스타일 조정** | fillImage 색상 → 시안. LevelText 숨기거나 제거 (레벨 수치 표시 안 함). |
| **6** | **PlaytimeUI.cs 신규 작성** | Time.unscaledTime 기반 MM:SS 표시. Update에서 갱신. |

### Phase 3 — 연출 & 폴리시

| 순서 | 작업 | 핵심 내용 |
|---|---|---|
| **7** | **SUPPRESS 차량 후방 파티클** | ParticleSystem 컴포넌트. BoostFuelRatio에 따라 emission rate 변화. 색상: 푸른 (#0088FF ~ #00CCFF). |
| **8** | **SyncRateUI pulse 애니메이션** | 40~70% 구간 코루틴 scale bounce. 기존 SyncRateUI.cs 확장. |
| **9** | **PartnerAIUI 씬 연결** | 기존 PartnerAIUI.cs를 DashboardLeft/PartnerAI 오브젝트에 연결. barkText, canvasGroup Inspector 연결. |
| **10** | **업그레이드 카드 두 타입** | UpgradeMenuUI.cs 확장. 현장(붉은/SYNC↑) vs 피트스톱(파란/SYNC↓). |

---

## 3. 내일 작업 시 참고 사항

### 코드 연결 체크리스트
Unity 씬에서 Inspector 연결이 끊어진 것들:
- `SyncRateUI` 컴포넌트 → `SyncRateGauge/BG/Fill` Image 연결 필요
- `BoostGaugeUI` 또는 새 스크립트 → `DashboardRight/SuppressBg/SuppressFill` 연결 필요
- `PartnerAIUI` 컴포넌트 → `DashboardLeft/PartnerAI` 오브젝트에 추가 후 barkText, canvasGroup 연결
- `XPBarUI` → `XP_Bar` 하위 FillBg/XPFill Image 연결 유지 확인
- `HullManager` 신규 → `CarController.ApplySpeedPenalty()` 내부에서 호출 추가

### HullManager 설계 메모
```
- 싱글톤 패턴 (SyncRateManager와 동일 구조)
- float _hull = 1f (0~1 범위)
- AddDamage(float amount) — 좀비 충돌 시
- Heal(float amount)      — 피트스톱 진입 시
- event OnHullChanged(float)
- event OnHullDepleted    — 게임오버 트리거
- CarController.ApplySpeedPenalty() 끝에서 HullManager.Instance?.AddDamage() 호출
- PitStopZone.cs에 Heal 호출 추가
```

### SUPPRESS 파티클 설계 메모
```
- Car 오브젝트 하위에 ParticleSystem 추가
- 위치: 차량 후방 (localPosition z 마이너스)
- emission.rateOverTime = BoostFuelRatio * 20f
- 색상: Color over Lifetime → 파란 (#0088FF) → 흰 (#AADDFF)
- startSize: 0.05 ~ 0.15
- startLifetime: 0.2 ~ 0.4
- 스크립트에서 매 Update마다 emission 조정
```

### 씬 저장 확인
Canvas 재구성 후 씬이 dirty 상태 — **Unity에서 Ctrl+S로 저장 필수**.

---

## 4. 관련 파일 목록

| 파일 | 역할 | 상태 |
|---|---|---|
| `docs/2026-05-24-uiux-design.md` | UI/UX 전체 설계 문서 | ✅ 오늘 갱신 |
| `docs/hud-mockup.html` | 디자이너용 HTML 목업 | ✅ 오늘 생성 |
| `docs/2026-05-25-session-log.md` | 이 문서 | ✅ |
| `Assets/_Project/Scripts/SyncRateManager.cs` | SYNC RATE 로직 | ✅ |
| `Assets/_Project/Scripts/SyncRateUI.cs` | SYNC RATE 게이지 UI | ✅ (씬 연결 필요) |
| `Assets/_Project/Scripts/PartnerAIUI.cs` | 파트너 AI bark | ✅ (씬 연결 필요) |
| `Assets/_Project/Scripts/BoostGaugeUI.cs` | SUPPRESS 게이지 | ⚠️ 색상 변경 필요 |
| `Assets/_Project/Scripts/XPBarUI.cs` | XP 바 | ⚠️ 스타일 조정 필요 |
| `Assets/_Project/Scripts/UpgradeMenuUI.cs` | 업그레이드 메뉴 | ⚠️ 두 타입 분리 필요 |
| `Assets/_Project/Scripts/HullManager.cs` | 차량 내구도 | ❌ 내일 신규 작성 |
| `Assets/_Project/Scripts/PlaytimeUI.cs` | 플레이타임 표시 | ❌ 내일 신규 작성 |
| `Assets/_Project/Scripts/NanobotBlink.cs` | 나노봇 시각 효과 | ✅ (기존) |
