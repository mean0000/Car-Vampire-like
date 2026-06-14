using System;
using UnityEngine;

namespace Run
{
    /// <summary>할당량 런 상태(헌장 06-13 "진행중/퇴근가능").
    /// ★Committed는 독립 상태가 아니라 IsLocked 플래그로 표현(P1 판정 (b)) — 머신은 이 둘만 체류한다.</summary>
    public enum QuotaState
    {
        Processing,            // 할당량 미충족. 이상개체 처리 중. 퇴근 불가
        ExtractionAvailable,   // 할당량 충족. "퇴근 가능" 점등. 퇴근 OR 한 탕 더 선택
    }

    /// <summary>
    /// 할당량 런의 상태 머신. RunManager가 InMission 동안 소유·질의한다.
    /// 처리 카운터(이상개체 N) + 티어 깊이 + 커밋(잠김·새 할당량·위협 계승).
    /// ★시간초과는 사망이 아니라 ThreatLevel 상승원 — 여기서 타임아웃 사망 분기 없음.
    /// ★커밋 = IsLocked + 티어++ + 새 할당량(P1). 독립 Committed 상태로 머무르지 않는다.
    /// </summary>
    public class QuotaRunController : MonoBehaviour
    {
        public static QuotaRunController Instance { get; private set; }

        [Header("Config")]
        [Tooltip("티어별 N·제한시간·XP 배수. RunLoopSetup이 연결.")]
        [SerializeField] QuotaTierConfig config;

        [Header("Refs")]
        [Tooltip("기존 OperationTimer 재사용(할당량 시계). 만료=사망 아니라 위협 시간초과 시작.")]
        [SerializeField] OperationTimer quotaTimer;

        // ── 읽기 전용 상태(HUD·RunManager가 폴링/구독) ──
        /// <summary>현재 체류 상태(Processing / ExtractionAvailable). Committed는 IsLocked로 표현.</summary>
        public QuotaState State { get; private set; } = QuotaState.Processing;

        /// <summary>현재 티어(1부터). 커밋마다 +1.</summary>
        public int CurrentTier { get; private set; } = 1;

        /// <summary>이번 티어 처리 수.</summary>
        public int Processed { get; private set; }

        /// <summary>이번 티어 목표 N.</summary>
        public int QuotaTarget { get; private set; }

        /// <summary>할당량 충족으로 퇴근 가능한가. 항상 "할당량을 채운 상태에서만" true.</summary>
        public bool CanExtract => State == QuotaState.ExtractionAvailable;

        /// <summary>한 번이라도 커밋했는가(중도 탈출 불가 구간 진입). 런 동안 true 유지 — 채워도 안 풀림.
        /// (의미: 잠긴 뒤로는 '할당량 미충족 중 퇴근'이 봉인됨. 충족하면 퇴근/한 탕 더는 여전히 가능.)</summary>
        public bool IsLocked { get; private set; }

        /// <summary>할당량 타이머가 만료됐는가(위협 시간초과 램프 가동). ThreatLevel이 읽는다.</summary>
        public bool IsOvertime { get; private set; }

        /// <summary>상태 전이 시 발화. HUD가 점등/패널 토글에 구독.</summary>
        public event Action<QuotaState> OnStateChanged;

        /// <summary>(Processed, QuotaTarget) — HUD 진행 게이지 갱신.</summary>
        public event Action<int, int> OnProgress;

        /// <summary>(새 CurrentTier) — 위협 계승·커밋 연출 트리거.</summary>
        public event Action<int> OnTierCommitted;

        bool _running;   // Begin~End 사이인지(이중 구독·만료 처리 가드)

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Begin이 걸려 있었다면 누수 방지로 해제(End 미호출 경로 방어).
            if (_running) Unhook();
        }

        // ── RunManager가 미션 시작 시 호출 ──
        /// <summary>미션 시작 — 티어1부터 할당량 머신 가동. CombatEvents·타이머 구독.</summary>
        public void Begin()
        {
            if (_running) return;   // 중복 Begin 가드
            _running = true;

            CurrentTier = 1;
            IsLocked = false;

            // CombatEvents 구독(처리). ★단방향 — 전투는 우릴 모름.
            CombatEvents.OnAnomalyProcessed += HandleAnomalyProcessed;
            if (quotaTimer != null) quotaTimer.OnExpired += HandleQuotaTimerExpired;

            StartTier(CurrentTier);
        }

        // ── RunManager Settle 시 정리(어느 결과든) ──
        /// <summary>미션 종료 — 구독 해제·타이머 정지. RunManager가 Settle 진입에서 호출.</summary>
        public void End()
        {
            if (!_running) return;
            _running = false;
            Unhook();
            quotaTimer?.Stop();
        }

        void Unhook()
        {
            CombatEvents.OnAnomalyProcessed -= HandleAnomalyProcessed;
            if (quotaTimer != null) quotaTimer.OnExpired -= HandleQuotaTimerExpired;
        }

        void StartTier(int tier)
        {
            Processed = 0;
            QuotaTarget = config != null ? config.QuotaForTier(tier) : 0;
            IsOvertime = false;
            SetState(QuotaState.Processing);
            // 새 티어 = 새 할당량 시계. 만료 도달 = HandleQuotaTimerExpired(사망 아님).
            if (quotaTimer != null && config != null) quotaTimer.Begin(config.SecondsForTier(tier));
            OnProgress?.Invoke(Processed, QuotaTarget);
        }

        // ── 전투 측 콜백: 이상개체 1건 처리 ──
        // threatTier = 처치된 개체의 위협 분류(Phase 2 변이/가중용, 지금은 카운트만).
        void HandleAnomalyProcessed(int threatTier)
        {
            // 점등 후(ExtractionAvailable) 잉여 처리는 카운트 정지(P8) — 다음 티어 카운터로 새지 않음.
            if (State == QuotaState.ExtractionAvailable) return;

            Processed++;
            OnProgress?.Invoke(Processed, QuotaTarget);

            // QuotaTarget 0(미설정)일 때 즉시 점등되는 사고 방지 — 목표가 양수일 때만 충족 판정.
            if (QuotaTarget > 0 && Processed >= QuotaTarget)
                SetState(QuotaState.ExtractionAvailable);   // 퇴근 점등
        }

        // ── 할당량 타이머 만료: 사망 아님. 위협 가속 신호만 ──
        void HandleQuotaTimerExpired()
        {
            IsOvertime = true;   // ThreatLevel이 이 플래그를 시간초과 상승원으로 읽는다
            // 상태 전이 없음. 계속 처리 가능. 위협만 램프.
        }

        // ── ExtractionAvailable에서 "한 탕 더"(UI 버튼 → RunManager → 여기) ──
        /// <summary>한 탕 더 — 잠그고 티어++·새 할당량 발급. ExtractionAvailable에서만 유효.</summary>
        public void CommitNextTier()
        {
            if (State != QuotaState.ExtractionAvailable) return;

            IsLocked = true;        // 이 프레임부터 중도 탈출 봉인(채울 때까진 퇴근 불가)
            CurrentTier++;
            OnTierCommitted?.Invoke(CurrentTier);   // ThreatLevel·연출·성장 가속이 계승분 반영
            StartTier(CurrentTier);                 // 새 N·새 타이머 → 상태는 Processing 재진입
        }

        void SetState(QuotaState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
