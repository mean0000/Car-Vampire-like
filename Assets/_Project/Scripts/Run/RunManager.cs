using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Meta;

namespace Run
{
    /// <summary>
    /// 런 생명주기의 단일 권위(헌장 부록 A). 페이즈 머신 + 3개 Settle 경로 수렴.
    /// 게임 시작 = Office(timeScale 0). StartMission → InMission. 탈출/사망/만료 → Settled.
    /// 씬 전환 없음 — 사무실/정산은 풀스크린 UI 패널 + timeScale 0. "사무실로" = 씬 리로드(메타는 DDOL 생존).
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        public enum RunPhase { Office, InMission, Extracting, Settled }
        public enum RunOutcome { Extracted, Died, Swept }

        public static RunManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] RunConfig config;

        [Header("Refs (RunLoopSetup이 연결)")]
        [SerializeField] OperationTimer timer;
        [SerializeField] RunHarvest harvest;
        [SerializeField] ZombieSpawner spawner;
        [SerializeField] ExtractionPoint extractionPoint;

        [Header("Sweep 연출")]
        [Tooltip("타이머 만료 시 풀스크린 백색 플래시 이미지(페이드). 비어도 동작.")]
        [SerializeField] CanvasGroup sweepFlash;
        [SerializeField] float sweepFlashFadeIn = 0.08f;

        public RunPhase Phase { get; private set; } = RunPhase.Office;

        /// <summary>페이즈가 바뀔 때 발화. UI가 패널 토글에 구독.</summary>
        public event Action<RunPhase> OnPhaseChanged;

        /// <summary>런 종료 + 정산 완료 시 발화(입금 후). 정산 패널이 구독.</summary>
        public event Action<SettlementReport> OnRunSettled;

        PlayerController _player;
        bool _playerDiedHooked;
        float _sweepFlashTarget;   // sweep 플래시 알파 목표(0→1)

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnhookPlayer();
            if (timer != null) timer.OnExpired -= HandleTimerExpired;
            if (extractionPoint != null) extractionPoint.OnExtractionComplete -= HandleExtractionComplete;
        }

        void Start()
        {
            // 이벤트 배선(설정 스크립트가 ref를 꽂아둠).
            if (timer != null) timer.OnExpired += HandleTimerExpired;
            if (extractionPoint != null) extractionPoint.OnExtractionComplete += HandleExtractionComplete;
            HookPlayer();

            // 시작 = 사무실. timeScale 0으로 전투 정지.
            EnterOffice();
        }

        void Update()
        {
            // sweep 플래시 페이드(unscaled — timeScale 0에서도 페이드되어야 함).
            if (sweepFlash != null && !Mathf.Approximately(sweepFlash.alpha, _sweepFlashTarget))
            {
                float step = (sweepFlashFadeIn > 0f ? Time.unscaledDeltaTime / sweepFlashFadeIn : 1f);
                sweepFlash.alpha = Mathf.MoveTowards(sweepFlash.alpha, _sweepFlashTarget, step);
            }
        }

        // ───────── Player 사망 훅 ─────────

        void HookPlayer()
        {
            _player = PlayerController.Instance;
            if (_player != null && !_playerDiedHooked)
            {
                _player.OnPlayerDied += HandlePlayerDied;
                _playerDiedHooked = true;
            }
        }

        void UnhookPlayer()
        {
            if (_player != null && _playerDiedHooked)
                _player.OnPlayerDied -= HandlePlayerDied;
            _playerDiedHooked = false;
        }

        // ───────── 페이즈 전이 ─────────

        void SetPhase(RunPhase p)
        {
            Phase = p;
            OnPhaseChanged?.Invoke(p);
        }

        void EnterOffice()
        {
            Time.timeScale = 0f;
            SetPhase(RunPhase.Office);
        }

        /// <summary>사무실 "출격" 버튼 → 미션 시작. timeScale 1, 타이머·스포너 가동.</summary>
        public void StartMission()
        {
            if (Phase != RunPhase.Office) return;

            harvest?.Clear();
            Time.timeScale = 1f;

            // 플레이어가 늦게 생성됐을 수 있으니 한 번 더 훅 시도.
            if (!_playerDiedHooked) HookPlayer();

            if (timer != null && config != null) timer.Begin(config.missionSeconds);

            SetPhase(RunPhase.InMission);   // 스포너는 Phase 게이트로 InMission/Extracting에만 스폰
        }

        /// <summary>탈출 지점 진입 → 헬기 호출 시작(Extracting). 취소 가능.</summary>
        public void RequestExtraction()
        {
            if (Phase != RunPhase.InMission) return;
            SetPhase(RunPhase.Extracting);
        }

        /// <summary>탈출 반경 이탈 → 호출 취소. InMission으로 복귀.</summary>
        public void CancelExtraction()
        {
            if (Phase != RunPhase.Extracting) return;
            SetPhase(RunPhase.InMission);
        }

        // ───────── Settle 경로 3개 ─────────

        void HandleExtractionComplete() => Settle(RunOutcome.Extracted, 1.0f);
        void HandlePlayerDied()         => Settle(RunOutcome.Died, 0.0f);

        void HandleTimerExpired()
        {
            // sweep 연출: 백색 플래시 페이드인 후 정산.
            _sweepFlashTarget = 1f;
            Settle(RunOutcome.Swept, 0.0f);
        }

        void Settle(RunOutcome outcome, float retentionRate)
        {
            if (Phase == RunPhase.Settled) return;   // 중복 수렴 방지(같은 프레임 다중 경로)

            timer?.Stop();

            // 정산 스냅샷 작성.
            var snapshot = harvest != null
                ? new Dictionary<StrainDef, int>(harvest.Snapshot())
                : new Dictionary<StrainDef, int>();

            int total = 0;
            foreach (var kv in snapshot) total += kv.Value;

            // Wallet 입금(영구). 보존율 적용.
            var wallet = MetaProgress.Instance != null ? MetaProgress.Instance.Wallet : null;
            int deposited = 0;
            if (wallet != null)
            {
                int before = WalletTotal(wallet, snapshot.Keys);
                wallet.Deposit(snapshot, retentionRate);   // 잔액 변경 시 OnBalanceChanged → MetaProgress.Save 자동 영속화
                int after = WalletTotal(wallet, snapshot.Keys);
                deposited = after - before;
            }

            var report = new SettlementReport
            {
                outcome = outcome,
                harvest = snapshot,
                retentionRate = retentionRate,
                totalHarvested = total,
                totalDeposited = deposited,
            };

            Time.timeScale = 0f;
            SetPhase(RunPhase.Settled);
            OnRunSettled?.Invoke(report);
        }

        static int WalletTotal(StrainWallet wallet, IEnumerable<StrainDef> defs)
        {
            int sum = 0;
            foreach (var d in defs) sum += wallet.Balance(d);
            return sum;
        }

        // ───────── 재출동 ─────────

        /// <summary>정산 패널 "사무실로(재출동 승인)" → 씬 리로드로 런 리셋. 메타는 DDOL 생존.</summary>
        public void ReturnToOffice()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                // 씬이 Build Settings에 없으면 LoadScene이 조용히 실패해 재출동이 먹통이 된다.
                Debug.LogError($"[RunManager] 씬 '{scene.name}'이 Build Settings에 없어 재출동 불가 — File > Build Settings에 씬을 추가하라.");
                return;
            }
            Time.timeScale = 1f;   // 리로드 전 복구(다음 씬 Awake가 정상 진행되도록)
            SceneManager.LoadScene(scene.name);
        }
    }
}
