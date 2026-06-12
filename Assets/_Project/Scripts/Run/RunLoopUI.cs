using System.Collections.Generic;
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
                meta.OnCashChanged += RefreshOffice;
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
                meta.OnCashChanged -= RefreshOffice;
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
            if (settlementBody == null) return;

            var meta = MetaProgress.Instance;
            int cash = meta != null ? meta.Cash : 0;

            // E-002 정산서(2차, 유저 판정 반영). 골격 = "4번 항목 고정":
            // 번호 행 1~6은 모든 결과에서 동일 순서·동일 자리. 조건부 내용은 번호 없는 들여쓴 부기(└)로만.
            var lines = new List<string>
            {
                OutcomeFlavor(report.outcome),
                "",
                $"작업코드 {report.opCode}",
                "",
                $"1. 작전 결과: {OutcomeResult(report.outcome)}",
                $"2. 이상개체 처리: {report.kills:N0}",
                $"3. 금일 정산액: {report.gross:N0}",
                $"4. 메모리 회수량: {report.totalHarvested:N0}",
            };

            // 소실 부기: Died/Swept만(E-001 "잃었음이 보여야 판돈이다").
            if (report.outcome != RunManager.RunOutcome.Extracted)
                lines.Add($"   └ 전량 소실 -{report.totalHarvested:N0}");

            lines.Add("5. 공제 내역");
            lines.Add($"   장비 보급료: -{report.equipmentFee:N0}");
            lines.Add($"   산재보험료: -{report.insuranceFee:N0}");
            if (report.outcome == RunManager.RunOutcome.Died)
            {
                int pct = Mathf.RoundToInt(report.retentionRate * 100f);
                lines.Add($"   산재 보전(특약 {pct}%): +{report.insuranceCredit:N0}");
            }
            else if (report.outcome == RunManager.RunOutcome.Swept)
            {
                lines.Add("   산재 미인정 (구역 소독): 0");
            }

            // 실수령 표기는 0 바닥(음수 노출 금지) — 음수일 때만 보장 부기.
            lines.Add($"6. 실수령액: {report.deposited:N0}");
            if (report.net < 0)
                lines.Add("   └ 최저 지급액 보장 · 이월 없음");

            lines.Add("");
            lines.Add($"잔고: {cash:N0}");

            settlementBody.text = string.Join("\n", lines);
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

            if (walletLabel != null)
                walletLabel.text = $"잔고: {meta.Cash:N0}";

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
                    : $"{def.displayName}  Lv.{lv} → {lv + 1}   비용 {cost:N0}";
            }
            if (btn != null) btn.interactable = !maxed && affordable;
        }

        // ───────── 문구(관료제 블랙코미디) ─────────

        static string OutcomeTitle(RunManager.RunOutcome o)
        {
            switch (o)
            {
                case RunManager.RunOutcome.Extracted: return "정산 보고서 — 탈출 성공";
                case RunManager.RunOutcome.Died:      return "정산 보고서 — 현장 인원 미회수";
                case RunManager.RunOutcome.Swept:     return "정산 보고서 — 구역 소독 집행";
                default: return "정산 보고서";
            }
        }

        static string OutcomeResult(RunManager.RunOutcome o)
        {
            switch (o)
            {
                case RunManager.RunOutcome.Extracted: return "탈출 성공";
                case RunManager.RunOutcome.Died:      return "현장 인원 미회수";
                case RunManager.RunOutcome.Swept:     return "구역 소독 집행";
                default: return "";
            }
        }

        static string OutcomeFlavor(RunManager.RunOutcome o)
        {
            switch (o)
            {
                // E-002 2차: 유저 확정 카피(Story 안).
                case RunManager.RunOutcome.Extracted:
                    return "회수 헬기 탑승이 확인되었습니다. 공제 후 실수령액이 익일 지급됩니다.";
                case RunManager.RunOutcome.Died:
                    return "현장 인원 미회수 처리되었습니다. 산재 특약에 따라 일부 보전됩니다.";
                case RunManager.RunOutcome.Swept:
                    return "구역 소독이 일정대로 집행되었습니다. 부서원 없음 처리. 본 건은 산재 미인정 사유에 해당합니다.";
                default: return "";
            }
        }
    }
}
