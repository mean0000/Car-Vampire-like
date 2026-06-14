# 런 루프 시스템 아키텍처 — Phase 1 설계 (2026-06-13)

> **Phase 1 = 설계만.** 코드는 `Assets/`에 들어가지 않는다(병렬 세션이 전투/무기/애니로 에디터 점유 중 — 공유 컴파일 보호). 이 문서의 의사코드/클래스 스켈레톤은 Phase 2 구현이 기계적이 되도록 *연동 훅과 시그니처*를 못 박는 것이 목적.
>
> **권위 = 잠긴 결정.** [[project_2026_06_13_run_structure_pressure_engine]] (★최종 개정 = 러시안 룰렛 할당량 / 위협 레벨 한 다이얼 / 인-런 성장). 본 문서는 그 결정을 *그대로* 시스템으로 옮긴다. 임의 기능 추가 0 — 미정은 **[판정 필요]**로 표시.

---

## 0. 설계 대상 범위 (이번 3개만)

| # | 시스템 | 한 줄 책임 |
|---|--------|-----------|
| 1 | **할당량 런 상태기계** | 3상태(Processing / ExtractionAvailable / Committed) + 처리 카운터 + 티어 커밋(잠김·새 할당량·위협 계승) |
| 2 | **위협 레벨 시스템** | 한 다이얼. 상승원 2개(티어 깊이 + 할당량 시간 초과) → 곱셈자 3개(스탯/밀도/변이) 노출 |
| 3 | **인-런 레벨업 스캐폴드** | 처리=XP→레벨업→업그레이드 선택. 임시(퇴근/사망 리셋). 시스템 골격 + 데이터 훅만 |

**범위 밖 (Phase 2):** 스폰 디렉터, 모듈 조립, 추출구 비주얼/씬, HUD, 카드 *내용*(수치·아트). 이 문서는 그것들이 **소비할 API**만 정의한다.

---

## 1. 기존 코드와의 관계 — 재사용 / 대체 / 신규 판단

먼저 읽은 실측 결과(현재 코드 상태):

| 기존 자산 | 현재 역할 | 06-13 피벗 후 처분 | 근거 |
|-----------|-----------|---------------------|------|
| `Run.RunManager` | 4페이즈(Office/InMission/Extracting/Settled) + 3-Settle 수렴 + 정산 경제 | **유지·확장** — Office/Settled/정산 생명주기는 그대로 가치 있음. InMission 내부에 할당량 상태기계를 **얹는다**(RunManager가 새 매니저를 *질의*, 페이즈 enum은 안 늘림) | RunManager.cs:14~233. 정산·DDOL·씬리로드가 이미 견고 |
| `Run.OperationTimer` | 작전 카운트다운 → 만료=sweep 사망 | **의미 전환** — "만료=사망벽" 폐기(06-13: 하드 타임아웃 사망 ❌). 동일 카운트다운 코드를 **티어 할당량 타이머**로 재사용(0 도달 = 사망 아니라 *위협 시간초과 누적 시작*) | OperationTimer.cs:11~68. Begin/Stop/OnTick/OnExpired API 그대로 쓰되 OnExpired 핸들러를 RunManager에서 sweep→threat로 바꿈 |
| `SyncRateManager` | 싱크 게이지(좀비화 시계) = 마스터 시계 후보 | **압박 역할 폐기·삭제 금지** — 06-13에서 "싱크=마스터 시계" 폐기, 위협 레벨이 대체. 단 `GlitchFX`가 마젠타 신호로 SyncRate를 읽으므로(아래 grep) **플레이버/사망 연출 신호로만 잔류**. 새 압박은 절대 SyncRate를 안 본다 | SyncRateManager.cs 전체. 메모리 ★개정 §"능력 비용은 탄약/재장전이 커버, 좀비화 테마는 플레이버/사망연출로 잔류" |
| `XPManager` | XP·레벨·PendingLevels | **재사용(그대로)** — 인-런 성장의 XP 엔진은 이미 존재·동작. 스캐폴드는 이 위에 *업그레이드 카탈로그 데이터 훅*만 얹는다 | XPManager.cs:1~80. AddXP/OnLevelChanged/ConsumePendingLevel 충분 |
| `LevelUpChoiceUI` | 레벨업 시 기본 6카드 선택 패널 | **재사용·확장점** — 카드를 하드코딩 배열에서 SO 카탈로그로 바꿀 *데이터 훅*만 추가(Phase 2). UI 흐름은 이미 PendingLevels 루프로 동작 | LevelUpChoiceUI.cs:17~59 |
| `RunStats` | 런 경과시간 + 킬 카운트(폴링) | **재사용** — 할당량 "처리 카운터"는 RunStats.Kills와 **별개 개념**(처리=할당량 대상 처치, 킬=전체). 신규 카운터 필요. 단 같은 폴링 패턴 답습 | RunStats.cs:8~39 |
| `EscalationProfile` | 경과분→초당스폰 곡선(스폰 디렉터 seam) | **유지·입력 전환** — 시간축 대신 **위협 레벨**을 입력으로 받게 Phase 2에서 전환. 곡선 SO 패턴은 위협 곡선에도 재사용 | EscalationProfile.cs:9~26 |
| `ZombieController.Die()/DieByWeapon()` | 킬 시 RunStats.AddKill + HarvestStrain + SpawnXPOrbs | **연동 지점(병렬 세션 소유)** — 처리 카운터/위협 기여 훅이 여기 한 줄로 붙는다(§4 상세). 절대 재작성 안 함 | ZombieController.cs:1076·1124 |

**핵심 원칙:** RunManager를 *포크하지 않는다*. RunManager는 런 생명주기(사무실↔미션↔정산)의 단일 권위로 남고, **할당량 상태기계는 InMission 동안만 사는 하위 머신**으로 신규 매니저에 둔다. RunManager는 그 매니저를 질의하고, 커밋/퇴근해금/사망을 그쪽에 위임한다.

---

## 2. 클래스 아키텍처

### 2.1 신규 클래스 (이번 Phase 2 구현 대상)

```
Run/
├── QuotaRunController.cs   [신규]  할당량 3상태 머신. 처리 카운터·티어·커밋·퇴근해금
├── ThreatLevel.cs          [신규]  위협 다이얼 단일 권위. 상승원 합산 → 곱셈자 노출
├── ThreatProfile.cs        [신규 SO]  위협→곱셈자 곡선(스탯/밀도) + 변이 임계 배열
├── QuotaTierConfig.cs      [신규 SO]  티어별 할당량 N·제한시간·위협 계승 규칙
CombatEvents.cs             [신규]  전투→런 시스템 단방향 이벤트 허브(처리/사망). ★연동 계약
Upgrade/
├── RunUpgradeCatalog.cs    [신규 SO]  인-런 업그레이드 카드 풀(levelup_catalog 익스트랙션 적응)
└── RunUpgradeDef.cs        [신규 SO]  카드 1장 정의(자원축·효과 훅·진화 게이트)
```

### 2.2 클래스 책임·관계도

```
                         ┌─────────────────────────────────────────┐
                         │            RunManager (기존, 확장)        │
                         │  Office/InMission/Extracting/Settled      │
                         │  ── StartMission() 시 QuotaRun.Begin() ──┐│
                         │  ── 퇴근해금 질의 ──────────────────────┐││
                         └───────────────┬───────────────────────┘││
                                         │ owns / queries           ││
                    ┌────────────────────┼──────────────────────────┘│
                    ▼                    ▼                            ▼
        ┌───────────────────┐  ┌──────────────────┐      ┌──────────────────┐
        │ QuotaRunController │  │   ThreatLevel    │      │ OperationTimer    │
        │  3-state machine   │─▶│  단일 다이얼      │◀─────│ (기존, 할당량     │
        │  처리 카운터        │  │  상승원 2 → 곱3  │ 시간 │  타이머로 재사용) │
        │  티어 N            │  └────────┬─────────┘ 초과 └──────────────────┘
        └─────────▲─────────┘           │ reads (곱셈자)
                  │ counts              ├──────────────┬──────────────┐
                  │ (처리 1건)          ▼              ▼              ▼
        ┌─────────┴─────────┐  ┌──────────────┐ ┌────────────┐ ┌──────────────┐
        │   CombatEvents    │  │ ZombieSpawner│ │ Zombie     │ │ (변이 훅:     │
        │  OnAnomalyProcessed│ │ (밀도 곱)    │ │Controller  │ │  Phase 2     │
        │  OnPlayerDowned   │  │              │ │ (스탯 곱)  │ │  스폰 디렉터) │
        └─────────▲─────────┘  └──────────────┘ └────────────┘ └──────────────┘
                  │ raised by (한 줄)
        ┌─────────┴──────────────────────────┐
        │ ZombieController.Die()/DieByWeapon()│  ← 병렬 세션 소유. 훅 1줄만 추가
        │ PlayerController (사망)              │
        └────────────────────────────────────┘

  XP/성장 측 (기존 재사용 + 데이터 훅):
        CombatEvents.OnAnomalyProcessed ─(또는 기존 XPOrb 경로)→ XPManager.AddXP
        XPManager.OnLevelChanged → LevelUpChoiceUI ─reads→ RunUpgradeCatalog (Phase 2 데이터 훅)
```

**관계 요약 (의존 방향 = 단방향, 순환 없음):**
- 전투(`ZombieController`/`PlayerController`)는 **`CombatEvents`에만** 의존(런 시스템 내부를 모름). → 병렬 세션과 충돌 0.
- `QuotaRunController`는 `CombatEvents.OnAnomalyProcessed`를 구독해 카운터를 올리고, `ThreatLevel`에 "처리됨" 통보 안 함(처리는 위협 *감소*가 아님 — 위협은 깊이+시간만 올림).
- `ThreatLevel`은 `QuotaRunController`(현재 티어)와 `OperationTimer`(시간초과)만 읽어 다이얼을 계산. 소비자(`ZombieSpawner`/`ZombieController`)는 `ThreatLevel`의 **읽기 전용 곱셈자 프로퍼티**만 본다.
- `RunManager`가 오케스트레이터: 미션 시작 시 `QuotaRunController.Begin()`, 퇴근 가능 여부는 `QuotaRunController.CanExtract` 질의, 커밋 버튼은 `QuotaRunController.CommitNextTier()` 호출.

---

## 3. 시스템 1 — 할당량 런 상태기계

### 3.1 상태 정의

```
enum QuotaState
{
    Processing,            // 할당량 미충족. 이상개체 처리 중. 퇴근 불가
    ExtractionAvailable,   // 할당량 충족. "퇴근 가능" 점등. 퇴근 OR 한 탕 더 선택
    Committed              // 한 탕 더 선택 → 다음 할당량까지 잠김. 중도 탈출 불가
}
```

> **상태 3개 = 06-13 명시("3상태기계: 진행중/퇴근가능/잠김").** Committed는 "다음 할당량을 채우면 다시 ExtractionAvailable로" 돌아오는 잠금 구간이다. 즉 Committed는 *Processing의 잠긴 변종* — 카운터/타이머는 동일하게 굴러가되 `CanExtract`가 false로 강제된다.

### 3.2 상태 전이도

```
        StartMission (RunManager)
              │
              ▼
      ┌──────────────┐  처리수 ≥ 할당량N        ┌────────────────────┐
      │  Processing  │ ───────────────────────▶ │ ExtractionAvailable │
      │ CanExtract=F │                          │   CanExtract = T    │
      └──────┬───────┘                          └──────┬──────┬───────┘
             │                                         │      │
             │ 사망(CombatEvents.OnPlayerDowned)        │ 퇴근  │ 한 탕 더
             │   또는 어느 상태든 사망                   │ 버튼  │ (CommitNextTier)
             ▼                                         ▼      ▼
      ┌──────────────┐                    ┌────────────┐  ┌──────────────┐
      │  (RunManager  │◀───── 퇴근 ────────│  추출 진행  │  │  Committed   │
      │   Settle:Died │                    │ (Extracting │  │ CanExtract=F │
      │   수익 전부    │                    │  RunManager)│  │ 새 할당량 발급│
      │   상실 ★철컥) │                    └─────┬──────┘  │ 위협 계승(↑) │
      └──────────────┘                          │         │ 티어 += 1    │
             ▲                                   ▼         └──────┬───────┘
             │                            ┌────────────┐         │ 처리수 ≥ 새N
             │  사망(Committed 중)         │ Settle:    │         ▼
             └─────────────────────────── │ Extracted  │   (다시 ExtractionAvailable로)
                                          │ 일당 입금   │   ※카운터는 티어별 리셋
                                          └────────────┘

  ★시간초과(할당량 타이머 0 도달): 사망 아님. ThreatLevel에 "초과중" 신호만.
     상태 전이 없음 — Processing/Committed 그대로, 위협만 계속 상승.
```

### 3.3 클래스 스켈레톤 (시그니처 + 의사코드)

```csharp
namespace Run
{
    /// <summary>
    /// 할당량 런의 3상태 머신. RunManager가 InMission 동안 소유·질의한다.
    /// 처리 카운터(이상개체 N) + 티어 깊이 + 커밋(잠김·새 할당량·위협 계승).
    /// ★시간초과는 사망이 아니라 ThreatLevel 상승원 — 여기서 타임아웃 사망 분기 없음.
    /// </summary>
    public class QuotaRunController : MonoBehaviour
    {
        public static QuotaRunController Instance { get; private set; }

        [SerializeField] QuotaTierConfig config;          // 티어별 N·제한시간·위협 계승
        [SerializeField] OperationTimer  quotaTimer;      // 기존 타이머 재사용(할당량 시계)

        // ── 읽기 전용 상태(HUD·RunManager가 폴링/구독) ──
        public QuotaState State { get; private set; } = QuotaState.Processing;
        public int   CurrentTier   { get; private set; } = 1;     // 1부터. 커밋마다 +1
        public int   Processed     { get; private set; }          // 이번 티어 처리 수
        public int   QuotaTarget   { get; private set; }          // 이번 티어 목표 N
        public bool  CanExtract => State == QuotaState.ExtractionAvailable;
        public bool  IsLocked   => State == QuotaState.Committed;  // 중도 탈출 불가 구간
        public bool  IsOvertime { get; private set; }             // 할당량 타이머 만료됨(위협 가속)

        public event Action<QuotaState> OnStateChanged;
        public event Action<int,int>    OnProgress;   // (Processed, QuotaTarget) — HUD 갱신
        public event Action<int>        OnTierCommitted;  // (새 CurrentTier) — 연출/위협 트리거

        // ── RunManager가 미션 시작 시 호출 ──
        public void Begin()
        {
            CurrentTier = 1;
            StartTier(CurrentTier);
            // CombatEvents 구독(처리/사망). ★단방향 — 전투는 우릴 모름.
            CombatEvents.OnAnomalyProcessed += HandleAnomalyProcessed;
        }

        void StartTier(int tier)
        {
            Processed   = 0;
            QuotaTarget = config.QuotaForTier(tier);
            IsOvertime  = false;
            SetState(QuotaState.Processing);
            quotaTimer.Begin(config.SecondsForTier(tier));   // 0 도달 = HandleQuotaTimerExpired
            OnProgress?.Invoke(Processed, QuotaTarget);
        }

        // ── 전투 측 콜백: 이상개체 1건 처리 ──
        void HandleAnomalyProcessed(int threatTier)   // 인자=처치된 개체 위협분류(Phase 2 변이용, 지금은 카운트만)
        {
            if (State == QuotaState.ExtractionAvailable) return;  // 점등 후 잉여 처리는 카운트 불요(잠김 전)
            Processed++;
            OnProgress?.Invoke(Processed, QuotaTarget);
            if (Processed >= QuotaTarget)
                SetState(QuotaState.ExtractionAvailable);   // 퇴근 점등
        }

        // ── 할당량 타이머 만료: 사망 아님. 위협 가속 신호만 ──
        void HandleQuotaTimerExpired()
        {
            IsOvertime = true;   // ThreatLevel이 이 플래그를 시간초과 상승원으로 읽는다
            // 상태 전이 없음. 계속 처리 가능. 위협만 램프.
        }

        // ── ExtractionAvailable에서 "한 탕 더" 선택(UI 버튼 → RunManager → 여기) ──
        public void CommitNextTier()
        {
            if (State != QuotaState.ExtractionAvailable) return;
            CurrentTier++;
            SetState(QuotaState.Committed);          // 즉시 잠금(이 프레임부터 CanExtract=false)
            OnTierCommitted?.Invoke(CurrentTier);    // ThreatLevel·연출이 계승분 반영
            StartTier(CurrentTier);                  // 새 N·새 타이머 — 단 상태는 곧 Processing로
            // ※StartTier가 SetState(Processing) 호출 → Committed는 1프레임 신호용.
            //   [판정 필요] Committed를 "잠긴 Processing"으로 합칠지(IsLocked 플래그만),
            //   아니면 별도 체류 상태로 둘지 — 잠금=중도탈출 불가만 보장하면 되므로
            //   IsLocked 플래그 + Processing 재진입이 가장 단순(권장). 상태도는 명료성 위해 3개 표기.
        }

        // ── RunManager Settle 시 정리 ──
        public void End()
        {
            CombatEvents.OnAnomalyProcessed -= HandleAnomalyProcessed;
            quotaTimer?.Stop();
        }

        void SetState(QuotaState s) { State = s; OnStateChanged?.Invoke(s); }
    }
}
```

> **★Committed 구현 단순화 권장 [판정 필요]:** Committed를 독립 체류 상태로 두면 "언제 Processing으로 가나"가 모호하다(커밋 즉시 새 할당량이 시작되므로 사실상 Processing). 가장 깔끔한 구현 = **상태는 Processing/ExtractionAvailable 둘로 굴리고, `IsLocked` 불린으로 "이번 티어는 커밋된 티어라 중도탈출 불가"를 표현**. 커밋 = `IsLocked=true` + 티어++ + 새 할당량. 채우면 ExtractionAvailable 복귀(이때 `IsLocked`는 유지 — 이미 커밋했으니 퇴근만 가능, 또 한 탕 더 가능). 상태도 3개는 *개념* 표기, 구현은 2상태+플래그가 헌장 "단순 C#"에 부합. Phase 2 착수 시 유저 1줄 확인.

### 3.4 RunManager 연동 (기존 파일 최소 수정 — Phase 2)

```
StartMission():       기존 끝에 QuotaRunController.Instance.Begin();   1줄
                      OperationTimer.Begin(missionSeconds) 호출 제거(할당량 타이머가 대체)
                      timer.OnExpired += HandleTimerExpired 배선 → QuotaRun이 타이머 소유로 이전
퇴근 트리거:          ExtractionPoint가 헬기 카운트 시작 전 QuotaRun.CanExtract 게이트 추가
                      (CanExtract=false면 추출구 진입해도 헬기 호출 안 됨 = "퇴근 불가" 디제틱)
"한 탕 더" 버튼:      신규 UI(Phase 2) → QuotaRunController.CommitNextTier()
Settle():             기존 그대로(Extracted/Died) + QuotaRunController.End() 1줄
```

> **OperationTimer 소유권 이전 [판정 필요]:** 현재 RunManager가 OnExpired→sweep. 피벗 후 만료=위협가속이므로 **OperationTimer를 QuotaRunController가 소유**(StartTier마다 Begin)하고 RunManager의 sweep 경로(RunOutcome.Swept)는 *제거 또는 후반 폭격 이벤트로 격하*. 메모리 §"폭격은 후반 에스컬레이션 이벤트로 흡수, 별도 HUD 시계 ❌"와 정합. Swept enum/정산분기는 남겨두되(보스/폭격 이벤트 재활용 여지) 타이머 만료로는 안 탄다.

---

## 4. 시스템 2 — 위협 레벨 시스템 (한 다이얼)

### 4.1 수식 모델

위협 레벨 `T` = 정규화 0~1+ 단일 스칼라. **상승원 2개의 합** (06-13: ①티어 깊이 + ②할당량 시간 초과):

```
T_depth    = (CurrentTier - 1) * depthPerTier        // 티어 1=0, 티어2=+0.25, …  (선형 계승)
T_overtime = IsOvertime ? overtimeRamp * overtimeSec : 0   // 만료 후 경과초당 누적
T          = T_depth + T_overtime                    // 상한 없음(곱셈자는 곡선이 포화)
```

`T`에서 **곱셈자 3개**를 곡선(`ThreatProfile`)으로 도출 (06-13 "곱셈자+변이"):

```
StatScale         = statByThreat.Evaluate(T)     // 몬스터 HP·공격·속도 배수 (≥1)
SpawnDensityMult  = densityByThreat.Evaluate(T)  // 스폰 밀도 배수 (≥1)
MutationStage     = 임계 배열에서 T가 넘은 단계   // 0,1,2… (변이 신규 패턴 트리거 훅)
```

> **다이얼 하나가 시간·룰렛·강화를 다 굴린다**(메모리 명시). 시간(overtime)과 룰렛(tier depth)이 같은 `T`로 합류 → 소비자는 출처를 모르고 `T`의 곱셈자만 본다. 이것이 "한 다이얼"의 핵심.

### 4.2 공개 API (다른 시스템이 소비)

```csharp
namespace Run
{
    /// <summary>
    /// 위협 레벨 = 압박 단일 다이얼. 상승원(티어 깊이 + 시간 초과)을 합산해
    /// 곱셈자(스탯/밀도) + 변이 단계를 노출한다. ★소비자는 읽기 전용 프로퍼티만 본다.
    /// 싱크 게이지(SyncRateManager)와 무관 — 압박은 절대 SyncRate를 안 읽는다(06-13 피벗).
    /// </summary>
    [DefaultExecutionOrder(-90)]   // QuotaRun(-?)·소비자보다 먼저 갱신
    public class ThreatLevel : MonoBehaviour
    {
        public static ThreatLevel Instance { get; private set; }

        [SerializeField] ThreatProfile profile;

        // ── 소비자용 읽기 전용 API (스폰 디렉터·몬스터·HUD가 이것만 본다) ──
        public float Current          { get; private set; }   // 합산 위협 T
        public float StatScale        { get; private set; }   // 몬스터 능력치 배수 (≥1)
        public float SpawnDensityMult { get; private set; }   // 스폰 밀도 배수 (≥1)
        public int   MutationStage    { get; private set; }   // 변이 단계(0=기본)

        public event Action<int> OnMutationStageChanged;   // 새 패턴 활성 트리거(Phase 2 디렉터 구독)

        void Update()
        {
            float tDepth = ThreatFromDepth();
            float tOver  = ThreatFromOvertime();   // QuotaRun.IsOvertime + 경과 누적
            Current = tDepth + tOver;

            StatScale        = profile.StatScaleAt(Current);
            SpawnDensityMult = profile.DensityMultAt(Current);

            int stage = profile.MutationStageAt(Current);
            if (stage != MutationStage)
            {
                MutationStage = stage;
                OnMutationStageChanged?.Invoke(stage);
            }
        }

        float ThreatFromDepth()
        {
            var q = QuotaRunController.Instance;
            return q == null ? 0f : (q.CurrentTier - 1) * profile.depthPerTier;
        }

        float ThreatFromOvertime()
        {
            var q = QuotaRunController.Instance;
            if (q == null || !q.IsOvertime) { _overtimeSec = 0f; return 0f; }
            _overtimeSec += Time.deltaTime;   // 만료 후 누적(timeScale 0이면 멈춤)
            return profile.overtimeRamp * _overtimeSec;
        }
        float _overtimeSec;
    }
}
```

### 4.3 소비 측 연동 훅 (스폰·몬스터 — Phase 2, 병렬 세션 영역과 경계)

| 소비자 | 읽는 API | 적용 방식 | 현재 코드 접점 |
|--------|----------|-----------|----------------|
| `ZombieSpawner` | `SpawnDensityMult` | 스폰 간격 = base / DensityMult, 또는 maxZombies *= DensityMult | EscalationProfile 평가부(ZombieSpawner.cs:79~85)를 위협 입력으로 전환 |
| `ZombieController` (스폰 시) | `StatScale` | Init 시 maxHP·damage·moveSpeed에 곱. **스폰 순간 1회 스냅샷**(살아있는 동안 고정 — 매 프레임 재평가 금지, 기존 좀비 소급 강화는 혼란) | ZombieController.Init() — 병렬 세션 소유. 훅=Init 끝에 `ApplyThreatScale(ThreatLevel.Instance.StatScale)` 1줄 |
| 스폰 디렉터(Phase 2) | `MutationStage` + `OnMutationStageChanged` | 단계별 신규 프리팹/패턴 풀 활성 | 미구현(Phase 2 신규) |

> **★스냅샷 vs 라이브 [판정 필요]:** 몬스터 스탯 스케일을 **스폰 시 1회 고정** 권장(이미 필드에 있는 좀비가 위협 상승에 소급 강화되면 "방금 약했는데 갑자기 셈" 혼란 + 매 프레임 곱 = GC/연산 낭비). 스폰 디렉터가 "새로 나오는 놈이 더 셈"으로 압박을 표현. 스폰 밀도는 라이브(다음 스폰부터 즉시 반영) OK. Phase 2 착수 시 유저 확인.

### 4.4 데이터 모델 — ThreatProfile (SO)

```csharp
[CreateAssetMenu(menuName = "ZombieCrush/Run/ThreatProfile")]
public class ThreatProfile : ScriptableObject
{
    [Header("상승원 가중")]
    [Tooltip("티어 1단계 깊어질 때 위협 가산(룰렛 한 탕 더의 무게).")]
    public float depthPerTier = 0.25f;
    [Tooltip("할당량 시간 초과 후 경과 1초당 위협 가산(지연 페널티 램프).")]
    public float overtimeRamp = 0.02f;

    [Header("곱셈자 곡선 (X=위협 T, Y=배수)")]
    public AnimationCurve statByThreat    = Linear(0,1, 1.5f,2.2f);   // 능력치
    public AnimationCurve densityByThreat = Linear(0,1, 1.5f,2.8f);   // 밀도

    [Header("변이 임계 (T가 이 값 넘으면 단계 ↑)")]
    public float[] mutationThresholds = { 0.5f, 1.0f, 1.6f };   // 3단계

    public float StatScaleAt(float t)    => Mathf.Max(1f, statByThreat.Evaluate(t));
    public float DensityMultAt(float t)  => Mathf.Max(1f, densityByThreat.Evaluate(t));
    public int   MutationStageAt(float t)
    {
        int s = 0;
        for (int i = 0; i < mutationThresholds.Length; i++)
            if (t >= mutationThresholds[i]) s = i + 1; else break;
        return s;
    }
}
```

> 모든 곡선/임계는 **맵 실측 후 튜닝**([[project_2026_06_03_spawn_model]] §"수치는 맵 의존"). 위 값은 형태만 잡은 자리표시.

---

## 5. 시스템 3 — 인-런 레벨업 스캐폴드

### 5.1 현재 상태 = 70% 존재 (재사용 중심)

이미 동작하는 것:
- `XPManager` — XP 누적·레벨·`PendingLevels`·`OnLevelChanged` ✅
- XP 획득 경로 — `ZombieController.SpawnXPOrbs()` → `XPOrb` 픽업 → `XPManager.AddXP` ✅
- `LevelUpChoiceUI` — `OnLevelChanged` 구독 → PendingLevels 루프로 카드 N장 연속 표시 ✅
- `PlayerStats` — 카드가 쌓는 전역 스탯 mod 레이어 + **매 런 Reset**(임시성 = 06-13 "퇴근/사망 리셋" 이미 충족) ✅

**즉, 스캐폴드 = XP 시스템 신규 구축이 아니라**, 하드코딩된 6카드 배열(`LevelUpChoiceUI.cs:25~46`)을 **SO 카탈로그 데이터 훅으로 승격**하는 것 + 룰렛 "한 탕 더 = 보상 더"를 거는 것.

### 5.2 데이터 훅 — RunUpgradeDef / RunUpgradeCatalog (SO)

```csharp
/// <summary>인-런 레벨업 카드 1장. levelup_catalog 구조의 익스트랙션 적응.
/// 효과는 PlayerStats mod 레이어에 적용(기존 6카드와 동일 경로) — 신규 스탯 훅은 effectKind로 분기.</summary>
[CreateAssetMenu(menuName = "ZombieCrush/Run/RunUpgradeDef")]
public class RunUpgradeDef : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea] public string[] descByLevel;      // Lv1~3 청크 설명(기존 AbilityDescByLevel 구조)
    public int maxLevel = 3;                       // catalog: 카드당 Lv3 만렙

    public enum ResourceAxis { Noise, Clock, Kill, Mobility, None }  // catalog §자원축(진화 공명용)
    public ResourceAxis axis = ResourceAxis.None;

    public enum EffectKind { FireRate, Damage, MaxHP, MoveSpeed, PickupRadius, XPGain /* …Phase2 확장 */ }
    public EffectKind effectKind;
    public float[] valueByLevel;                   // 누적 최종값(델타 아님 — 기존 *ByLevel 배열과 동일 규약)

    [Header("진화 (post-MVP 훅 — catalog §진화 게이트)")]
    public bool isSignature;                        // 시그니처 카드(동사 슬롯 1/2 가중)
    public RunUpgradeDef evolvesInto;               // Lv3 + 같은 axis 동사 2개 → 진화(Phase 2)
}

/// <summary>인-런 카드 풀 + 드로우 가중 곡선. LevelUpChoiceUI가 하드코딩 배열 대신 이걸 읽는다.</summary>
[CreateAssetMenu(menuName = "ZombieCrush/Run/RunUpgradeCatalog")]
public class RunUpgradeCatalog : ScriptableObject
{
    public RunUpgradeDef[] basics;     // 기본축(catalog §기본 8)
    public RunUpgradeDef[] verbs;      // 동사축(catalog §동사 10)
    [Tooltip("레벨대별 기본:동사 가중(catalog: Lv1~3=75:25, Lv4~6=55:45, Lv7+=40:60)")]
    public AnimationCurve verbWeightByLevel;

    /// <summary>레벨업 시 3장 드로우(만렙 카드 제외, 가중 적용). Phase 2 구현.</summary>
    public RunUpgradeDef[] Draw(int count, int playerLevel, IDictionary<string,int> ownedLevels) => /* … */;
}
```

> **카드 *내용*은 이 문서 범위 밖**(06-13 "카드 내용 전부 말고 시스템 골격 + 데이터 훅"). 내용 권위 = [[project_2026_05_31_levelup_catalog]] / `docs/2026-05-31-levelup-cards-catalog.md`. Phase 2가 그 ~23장을 SO 에셋으로 입력.

### 5.3 룰렛 "한 탕 더 = 성장도 더" 연동

06-13: *"룰렛 한 탕 더 = 보상+레벨+빌드 더 강하게(욕심 두 배). 티어3의 나 ≫ 티어1."*

```
QuotaRunController.OnTierCommitted(newTier) 구독 →
   ① XP 곡선 가속 [판정 필요]: PlayerStats.XPGainMult 또는 처리당 XP에 티어 배수
   ② 카드 희귀도 상향 [판정 필요]: 티어↑ = 시그니처/동사 가중↑ (catalog 드로우 가중에 티어 보정)
   ③ (선택) 즉시 1레벨 보너스: 커밋 보상으로 카드 1장 강제 드로우
```

> **★성장 가속 구현 [판정 필요]:** "티어3의 나 ≫ 티어1"을 어디로 표현할지 — (a) 처리당 XP에 티어 배수(레벨 빨리 오름) vs (b) 카드 희귀도만 상향 vs (c) 둘 다. (a)+(c)가 직관적이나 밸런스 위험. 가장 단순한 1차 = **티어 배수를 XP 획득에 곱**(`AddXP` 진입 시 `* tierXpMult`). XPManager.AddXP는 이미 `PlayerStats.XPGainMult`를 곱하므로(XPManager.cs:40) **신규 훅 불요 — 티어 배수를 그 곱에 합류**. Phase 2 유저 확인.

---

## 6. ★전투/스폰 연동 계약 — CombatEvents (병렬 세션과 충돌 방지)

병렬 세션이 전투/무기를 만지는 중이므로, **런 시스템이 전투에서 *필요로 하는 인터페이스*를 단방향 이벤트 허브 하나로 고정**한다. 전투 측은 이 허브에 **한 줄 발화**만 추가하면 되고, 런 시스템 내부를 전혀 모른다.

```csharp
/// <summary>
/// 전투 → 런/성장 시스템 단방향 이벤트 허브. ★연동 계약의 단일 지점.
/// 전투(ZombieController/PlayerController)는 이 정적 이벤트만 발화 — 런 시스템 내부 의존 0.
/// QuotaRunController·(필요 시 성장)이 구독한다. 정적이라 씬 재로드 시 구독 누수 주의
/// → 구독자(QuotaRunController)가 Begin/End에서 짝맞춰 +=/-= (RunManager 생명주기에 묶임).
/// </summary>
public static class CombatEvents
{
    /// <summary>이상개체 1건 처리(처치) 시 발화. 인자 = 그 개체의 위협 분류 티어(변이/가중용, 0=기본).
    /// ★할당량 카운터가 이걸 센다. 발화 위치 = ZombieController.Die()/DieByWeapon() 내부 1줄.</summary>
    public static event Action<int> OnAnomalyProcessed;
    public static void RaiseAnomalyProcessed(int threatTier = 0) => OnAnomalyProcessed?.Invoke(threatTier);

    /// <summary>플레이어 다운(사망) 시 발화. RunManager의 OnPlayerDied와 별개가 아니라
    /// ★기존 PlayerController.OnPlayerDied를 그대로 쓰면 됨 — 신규 불요(아래 표 참조).</summary>
    // (사망은 PlayerController.OnPlayerDied 기존 이벤트 재사용 — 중복 채널 만들지 않음)
}
```

### 6.1 전투 측이 추가할 것 = **딱 한 줄** (병렬 세션 작업 최소화)

| 기존 호출 위치 | 현재 코드 | 추가할 한 줄 | 비고 |
|----------------|-----------|--------------|------|
| `ZombieController.Die()` | `RunStats.AddKill(); HarvestStrain();` (cs:1076~1077) | `CombatEvents.RaiseAnomalyProcessed(threatTier);` | threatTier = 그 좀비의 분류(없으면 0) |
| `ZombieController.DieByWeapon()` | `RunStats.AddKill(); HarvestStrain();` (cs:1124~1125) | 동일 한 줄 | 두 사망 경로 모두 커버 |

> **왜 이 설계인가:** 처리 카운터를 RunHarvest.Add나 RunStats.AddKill에 *몰래 끼우지 않는다*. 이유 = (1) RunHarvest는 strain 입금 경로라 "처리=할당량"과 의미가 다르고(픽업 시점 입금 vs 처치 시점 카운트), (2) 병렬 세션이 그 메서드를 만질 때 충돌. **별도 명시 이벤트 1개**가 의미·소유권을 깨끗이 가른다. 전투 세션은 "킬 확정 지점에서 `RaiseAnomalyProcessed` 한 줄"만 합의하면 끝.

### 6.2 사망 채널 = 기존 재사용 (신규 금지)

플레이어 사망은 `PlayerController.OnPlayerDied`(cs:96, 401)가 이미 있고 RunManager가 구독 중(RunManager.cs:90). **QuotaRunController는 사망을 직접 처리하지 않는다** — RunManager가 `HandlePlayerDied → Settle(Died)`로 수렴할 때 `QuotaRunController.End()`를 호출하면 충분(어느 상태든 사망=수익 상실=Settle:Died, 06-13 "실패=사망=수익 전부 상실"). Committed 잠금 중 사망도 동일 경로 — 별도 분기 불요.

### 6.3 스폰 측 연동 (읽기 전용 — 병렬 세션 영역 존중)

스폰 디렉터는 Phase 2 신규지만, **현재 `ZombieSpawner`가 이미 `EscalationProfile`을 읽는 자리**(cs:24, 79~85)가 그대로 위협 레벨 소비점이 된다. Phase 2에서 그 평가부를 `ThreatLevel.Instance.SpawnDensityMult` 읽기로 전환. 전투 세션이 `ZombieSpawner`를 안 만지면 충돌 0; 만진다면 "스폰율 입력을 시간→위협으로 바꾼다"만 합의.

---

## 7. 데이터 모델 종합 (SO/구조체 일람)

| 타입 | 종류 | 핵심 필드 | 튜닝 책임 |
|------|------|-----------|-----------|
| `QuotaTierConfig` | SO | `quotaPerTier[]`(티어별 N), `secondsPerTier[]`(제한시간), `tierXpMult[]`(성장 계승) | 맵 실측 후 |
| `ThreatProfile` | SO | `depthPerTier`, `overtimeRamp`, `statByThreat`/`densityByThreat`(곡선), `mutationThresholds[]` | 맵 실측 후 |
| `RunUpgradeDef` | SO | `effectKind`, `valueByLevel[]`, `axis`, `evolvesInto` | catalog 문서 |
| `RunUpgradeCatalog` | SO | `basics[]`, `verbs[]`, `verbWeightByLevel`(곡선) | catalog 문서 |
| `QuotaState` | enum | Processing/ExtractionAvailable/Committed | — |
| `CombatEvents` | static | `OnAnomalyProcessed(int)` | — |

```csharp
[CreateAssetMenu(menuName = "ZombieCrush/Run/QuotaTierConfig")]
public class QuotaTierConfig : ScriptableObject
{
    [Tooltip("티어별 할당량 N(처리 목표). 인덱스 0=티어1. 배열 끝 넘으면 마지막 값 외삽.")]
    public int[]   quotaPerTier   = { 12, 18, 26, 36 };
    [Tooltip("티어별 제한시간(초). 0 도달=사망 아님, 위협 시간초과 램프 시작.")]
    public float[] secondsPerTier = { 180, 165, 150, 135 };
    [Tooltip("티어별 XP 획득 배수(룰렛 한 탕 더의 성장 보상 — '티어3의 나 ≫ 티어1').")]
    public float[] tierXpMult     = { 1.0f, 1.3f, 1.7f, 2.2f };

    public int   QuotaForTier(int tier)   => Sample(quotaPerTier, tier);
    public float SecondsForTier(int tier) => Sample(secondsPerTier, tier);
    public float XpMultForTier(int tier)  => Sample(tierXpMult, tier);
    // tier는 1부터 → 인덱스 tier-1, 배열 끝 넘으면 마지막 값 클램프
    static T Sample<T>(T[] arr, int tier) => arr[Mathf.Clamp(tier - 1, 0, arr.Length - 1)];
}
```

---

## 8. 판정 필요 목록 (+ Phase 2 구현 난이도 감)

| # | 판정 항목 | 선택지 | 권장 | Phase 2 난이도 |
|---|-----------|--------|------|----------------|
| P1 | **Committed 상태 구현** | (a) 독립 체류 상태 (b) Processing + IsLocked 플래그 | **(b)** — 헌장 "단순 C#" 부합 | 낮음 (분기 1개) |
| P2 | **OperationTimer 소유권** | RunManager 유지 vs QuotaRunController 이전 | **이전** — 만료=위협가속이라 할당량 머신 소속이 자연 | 낮음 (배선 이동) |
| P3 | **sweep(Swept) 처분** | 제거 vs 후반 폭격 이벤트로 격하 | **격하 보존** — enum/정산분기 남기되 타이머 만료론 안 탐 | 낮음 (호출부만 분리) |
| P4 | **몬스터 스탯 스케일 시점** | 스폰 시 1회 스냅샷 vs 매 프레임 라이브 | **스냅샷** — 소급 강화 혼란·연산 방지 | 중간 (Init 훅, 병렬 세션 협의) |
| P5 | **성장 가속 표현** | XP 티어배수 / 카드 희귀도 / 둘 다 | **XP 티어배수 1차**(기존 XPGainMult 곱에 합류, 신규 훅 0) | 낮음 (곱 1개) |
| P6 | **싱크 게이지 잔존 범위** | 완전 삭제 vs 플레이버/글리치만 | **플레이버만** — GlitchFX가 SyncRate 의존, 삭제 시 마젠타 신호 파탄 | 낮음 (안 건드림) |
| P7 | **CombatEvents 발화 인자** | threatTier 분류 지금 vs Phase 2 | **0 기본값으로 지금 열어두고 변이는 Phase 2** | 낮음 |
| P8 | **할당량 점등 후 잉여 처리** | 카운트 계속 vs 무시 | **무시**(ExtractionAvailable에선 카운트 정지 — 위 의사코드) [유저 확인] | 낮음 |

### Phase 2 구현 스코프 요약 (이 설계 기준)
- **신규 파일 7개** (위 §2.1) — 전부 데이터+상태머신, 게임감 튜닝 없음 → **Sonnet/Gameplay 위임 가능** (스펙 동결 후).
- **기존 파일 수정 최소** — RunManager(미션시작/Settle에 2줄), ZombieController(킬 2지점 1줄씩, **병렬 세션 협의**), ZombieSpawner(스폰율 입력 전환 1곳), LevelUpChoiceUI(배열→카탈로그 읽기).
- **튜닝 잔무** — 모든 SO 곡선/배열은 맵 실측 후([[project_2026_06_03_spawn_model]] 원칙). Phase 2 구현 ≠ 밸런싱.
- **충돌 위험 지점 = ZombieController 킬 2줄 + ZombieSpawner 스폰율 1곳** — 병렬 세션이 이 파일을 만지므로 머지 시점 합의 필요(나머지는 신규 파일이라 충돌 0).

---

## 9. 한 장 요약

```
RunManager(기존, 생명주기 권위)
  └ StartMission → QuotaRunController.Begin()
       QuotaRunController: Processing →(처리 N 충족)→ ExtractionAvailable
                                                      ├ 퇴근 → RunManager.Settle(Extracted)
                                                      └ 한 탕 더 → Committed(잠김)+티어++ → 다시 채움
       위협: ThreatLevel = (티어깊이) + (시간초과) → StatScale·DensityMult·MutationStage
              소비: ZombieSpawner(밀도) · ZombieController(스탯, 스폰 시 1회) · 디렉터(변이)
       성장: 처리 → (기존)XPOrb → XPManager → LevelUpChoiceUI → RunUpgradeCatalog(데이터 훅)
              티어 커밋 = XP 배수↑(욕심 두 배)
  └ 사망(어느 상태든) → PlayerController.OnPlayerDied → RunManager.Settle(Died) → 수익 전부 상실 ★철컥

연동 계약: 전투는 CombatEvents.RaiseAnomalyProcessed(threatTier) 한 줄만 추가.
           나머지는 신규 파일 → 병렬 세션과 충돌 0.
폐기: 싱크=마스터시계(위협이 대체), 타이머만료=사망(위협가속이 대체). 싱크는 플레이버로만 잔존.
```
