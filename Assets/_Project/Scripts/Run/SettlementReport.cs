using System.Collections.Generic;
using Run;

namespace Run
{
    /// <summary>
    /// 런 종료 정산 스냅샷(불변). 정산 패널이 표시하고, Wallet 입금 계산의 근거가 된다.
    /// </summary>
    public struct SettlementReport
    {
        public RunManager.RunOutcome outcome;

        /// <summary>이번 런 수확(strain → 개수) 스냅샷.</summary>
        public Dictionary<StrainDef, int> harvest;

        /// <summary>산재 보전 비율(탈출=1.0 / 사망=특약 0.3 / sweep=0.0). 표기용(특약 % 행).</summary>
        public float retentionRate;

        /// <summary>총 수확 개수(모든 strain 합).</summary>
        public int totalHarvested;

        // ───── E-002: 현금 정산 ─────

        /// <summary>이번 런 처치 수(이상개체 처리).</summary>
        public int kills;

        /// <summary>금일 정산액 = 납품 성사분만. 탈출=N×단가 / 사망·소독=0.</summary>
        public int gross;

        /// <summary>공제: 장비 보급료(양수 보관, 표기 시 음수).</summary>
        public int equipmentFee;

        /// <summary>공제: 산재보험료(양수 보관, 표기 시 음수).</summary>
        public int insuranceFee;

        /// <summary>산재 보전(특약): 사망 시 RoundToInt(N×단가×보전율). 그 외 0. 금액 보전.</summary>
        public int insuranceCredit;

        /// <summary>실수령액 = 정산액 − 공제 + 산재 보전. 음수 가능(표기는 0 바닥, 음수 노출 금지).</summary>
        public int net;

        /// <summary>실제 입금 현금 = max(0, net).</summary>
        public int deposited;

        /// <summary>작업코드(예: "OP-0382"). StartMission 시 발급.</summary>
        public string opCode;

        /// <summary>산재 미인정(Swept=무단 잔류).</summary>
        public bool insuranceDenied;
    }
}
