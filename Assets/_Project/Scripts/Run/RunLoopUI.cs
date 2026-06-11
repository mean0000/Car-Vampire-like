using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Meta;
using Run;

namespace Run
{
    /// <summary>
    /// 익스트랙션 루프 UI(기존 HudV2와 분리). 이벤트 구독만 — 게임 로직 0(헌장 부록 A).
    /// ① HUD 띠: 타이머/수확 카운터/탈출 진행
    /// ② 사무실 패널: 지갑·업그레이드 2개 구매·출격
    /// ③ 정산 패널: 결과·수확→입금·누적·재출동
    /// 톤: 관료제 블랙코미디. 참조는 RunLoopSetup이 연결한다.
    /// </summary>
    public class RunLoopUI : MonoBehaviour
    {
        [Header("HUD 띠")]
        [SerializeField] GameObject hudRoot;
        [SerializeField] TextMeshProUGUI timerLabel;
        [SerializeField] TextMeshProUGUI harvestLabel;
        [SerializeField] TextMeshProUGUI extractionLabel;

        [Header("사무실 패널")]
        [SerializeField] GameObject officePanel;
        [SerializeField] TextMeshProUGUI walletLabel;
        [SerializeField] Button deployButton;
        [SerializeField] Button damageBuyButton;
        [SerializeField] TextMeshProUGUI damageBuyLabel;
        [SerializeField] Button fireRateBuyButton;
        [SerializeField] TextMeshProUGUI fireRateBuyLabel;

        [Header("정산 패널")]
        [SerializeField] GameObject settlementPanel;
        [SerializeField] TextMeshProUGUI settlementTitle;
        [SerializeField] TextMeshProUGUI settlementBody;
        [SerializeField] Button returnButton;

        [Header("색")]
        [SerializeField] Color timerNormal = new Color(0.88f, 0.96f, 0.97f);
        [SerializeField] Color timerDanger = new Color(1f, 0.26f, 0.26f);
        [Tooltip("이 초 이하부터 타이머가 적색.")]
        [SerializeField] float timerDangerSeconds = 60f;

        RunManager _run;
        OperationTimer _timer;
        RunHarvest _harvest;
        ExtractionPoint _extraction;

        int _lastTimerSecs = -1;

        // Start(=모든 Awake 이후)에서 배선 — RunManager/Timer 싱글톤 Instance가 확정된 뒤 구독해
        // OnEnable 실행 순서 레이스(Instance 미설정 시 구독 누락)를 피한다.
        void Start()
        {
            _run = RunManager.Instance;
            _timer = OperationTimer.Instance;
            _harvest = RunHarvest.Instance;
            _extraction = FindFirstObjectByType<ExtractionPoint>();

            if (_run != null)
            {
                _run.OnPhaseChanged += HandlePhaseChanged;
                _run.OnRunSettled += HandleRunSettled;
            }
            if (_timer != null) _timer.OnTick += HandleTimerTick;
            if (_harvest != null) _harvest.OnHarvested += HandleHarvested;

            var meta = MetaProgress.Instance;
            if (meta != null)
            {
                meta.Wallet.OnBalanceChanged += RefreshOffice;
                meta.Upgrades.OnChanged += RefreshOffice;
            }

            // 버튼 바인딩(이벤트 발행이 아니라 RunManager/Upgrades 호출 — UI는 로직을 갖지 않고 위임만).
            if (deployButton != null) deployButton.onClick.AddListener(OnDeployClicked);
            if (damageBuyButton != null) damageBuyButton.onClick.AddListener(OnBuyDamageClicked);
            if (fireRateBuyButton != null) fireRateBuyButton.onClick.AddListener(OnBuyFireRateClicked);
            if (returnButton != null) returnButton.onClick.AddListener(OnReturnClicked);

            // 초기 상태 동기화(이벤트 구독 전 이미 Office에 진입했을 수 있음).
            if (_run != null) HandlePhaseChanged(_run.Phase);
            RefreshOffice();
            RefreshHarvest();
        }

        void OnDestroy()
        {
            if (_run != null)
            {
                _run.OnPhaseChanged -= HandlePhaseChanged;
                _run.OnRunSettled -= HandleRunSettled;
            }
            if (_timer != null) _timer.OnTick -= HandleTimerTick;
            if (_harvest != null) _harvest.OnHarvested -= HandleHarvested;

            var meta = MetaProgress.Instance;
            if (meta != null)
            {
                meta.Wallet.OnBalanceChanged -= RefreshOffice;
                meta.Upgrades.OnChanged -= RefreshOffice;
            }

            if (deployButton != null) deployButton.onClick.RemoveListener(OnDeployClicked);
            if (damageBuyButton != null) damageBuyButton.onClick.RemoveListener(OnBuyDamageClicked);
            if (fireRateBuyButton != null) fireRateBuyButton.onClick.RemoveListener(OnBuyFireRateClicked);
            if (returnButton != null) returnButton.onClick.RemoveListener(OnReturnClicked);
        }

        void Update()
        {
            // 탈출 진행 표시는 매 프레임 폴링(ExtractionPoint는 이벤트가 아닌 진행 상태).
            if (_extraction != null && extractionLabel != null)
            {
                if (_extraction.IsExtracting)
                {
                    extractionLabel.gameObject.SetActive(true);
                    int secs = Mathf.CeilToInt(_extraction.Remaining);
                    extractionLabel.text = $"회수 헬기 도착까지 {secs}초";
                }
                else
                {
                    extractionLabel.gameObject.SetActive(false);
                }
            }
        }

        // ───────── 이벤트 핸들러 ─────────

        void HandlePhaseChanged(RunManager.RunPhase phase)
        {
            bool office = phase == RunManager.RunPhase.Office;
            bool settled = phase == RunManager.RunPhase.Settled;
            bool inMission = phase == RunManager.RunPhase.InMission || phase == RunManager.RunPhase.Extracting;

            if (officePanel != null) officePanel.SetActive(office);
            if (settlementPanel != null) settlementPanel.SetActive(settled);
            if (hudRoot != null) hudRoot.SetActive(inMission);

            if (office) RefreshOffice();
        }

        void HandleTimerTick(float remaining)
        {
            if (timerLabel == null) return;
            int secs = Mathf.CeilToInt(remaining);
            if (secs == _lastTimerSecs) return;   // 초 단위 변할 때만 재할당(GC 방지)
            _lastTimerSecs = secs;

            int m = secs / 60;
            int s = secs % 60;
            timerLabel.text = $"{m:00}:{s:00}";
            timerLabel.color = remaining <= timerDangerSeconds ? timerDanger : timerNormal;
        }

        void HandleHarvested(StrainDef def, int total) => RefreshHarvest();

        void RefreshHarvest()
        {
            if (harvestLabel == null) return;
            var meta = MetaProgress.Instance;
            var strain = meta != null ? meta.BioStrain : null;
            int n = (_harvest != null && strain != null) ? _harvest.Count(strain) : 0;
            // 렉시콘 v1.4: 인게임 표기 = "메모리" (개발 용어 strain은 코드에만).
            harvestLabel.text = $"메모리: {n}";
        }

        void HandleRunSettled(SettlementReport report)
        {
            if (settlementTitle != null) settlementTitle.text = OutcomeTitle(report.outcome);

            var meta = MetaProgress.Instance;
            var strain = meta != null ? meta.BioStrain : null;
            int balance = (meta != null && strain != null) ? meta.Wallet.Balance(strain) : 0;

            if (settlementBody != null)
            {
                string flavor = OutcomeFlavor(report.outcome);
                // E-001: 사망 시 소실 사실 1줄 — "잃었음이 보여야 판돈이다"(스펙 §요소2).
                string lossLine = report.outcome == RunManager.RunOutcome.Died
                    ? $"메모리 {report.totalHarvested}개 소실\n\n"
                    : "";
                settlementBody.text =
                    $"{flavor}\n\n" +
                    lossLine +
                    $"금일 회수 실적: {report.totalHarvested}\n" +
                    $"본부 입금 처리: {report.totalDeposited}\n" +
                    $"누적 보유 strain: {balance}";
            }
        }

        // ───────── 버튼 핸들러(위임만) ─────────

        void OnDeployClicked() => _run?.StartMission();

        void OnBuyDamageClicked()
        {
            var meta = MetaProgress.Instance;
            if (meta != null) meta.Upgrades.TryPurchase(meta.DamageUpgrade);
        }

        void OnBuyFireRateClicked()
        {
            var meta = MetaProgress.Instance;
            if (meta != null) meta.Upgrades.TryPurchase(meta.FireRateUpgrade);
        }

        void OnReturnClicked() => _run?.ReturnToOffice();

        // ───────── 사무실 갱신 ─────────

        void RefreshOffice()
        {
            var meta = MetaProgress.Instance;
            if (meta == null) return;
            var strain = meta.BioStrain;
            int balance = strain != null ? meta.Wallet.Balance(strain) : 0;

            if (walletLabel != null)
            {
                string label = strain != null ? strain.DisplayName : "생체";
                walletLabel.text = $"보유 strain — {label}: {balance}";
            }

            UpdateUpgradeButton(meta, meta.DamageUpgrade, damageBuyButton, damageBuyLabel);
            UpdateUpgradeButton(meta, meta.FireRateUpgrade, fireRateBuyButton, fireRateBuyLabel);
        }

        void UpdateUpgradeButton(MetaProgress meta, UpgradeDef def, Button btn, TextMeshProUGUI label)
        {
            if (def == null) return;
            int lv = meta.Upgrades.GetLevel(def);
            int cost = def.CostForNextLevel(lv);
            bool maxed = cost < 0;
            bool affordable = meta.Upgrades.CanPurchase(def);

            if (label != null)
            {
                label.text = maxed
                    ? $"{def.displayName}  Lv.{lv}  (최대)"
                    : $"{def.displayName}  Lv.{lv} → {lv + 1}   비용 {cost}";
            }
            if (btn != null) btn.interactable = !maxed && affordable;
        }

        // ───────── 문구(관료제 블랙코미디) ─────────

        static string OutcomeTitle(RunManager.RunOutcome o)
        {
            switch (o)
            {
                case RunManager.RunOutcome.Extracted: return "정산 보고서 — 탈출 성공";
                case RunManager.RunOutcome.Died:      return "정산 보고서 — 현장 사망";
                case RunManager.RunOutcome.Swept:     return "정산 보고서 — 구역 소독 집행";
                default: return "정산 보고서";
            }
        }

        static string OutcomeFlavor(RunManager.RunOutcome o)
        {
            switch (o)
            {
                case RunManager.RunOutcome.Extracted:
                    return "회수 헬기 탑승이 확인되었습니다. 금일 실적이 본부에 입금됩니다.";
                case RunManager.RunOutcome.Died:
                    return "현장 사망으로 회수물은 전량 유실 처리되었습니다.";
                case RunManager.RunOutcome.Swept:
                    return "구역 소독이 일정대로 집행되었습니다. 부서원 없음 처리.";
                default: return "";
            }
        }
    }
}
