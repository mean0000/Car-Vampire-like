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

        /// <summary>입금 보존율(탈출=1.0 / 사망·sweep=0.0).</summary>
        public float retentionRate;

        /// <summary>총 수확 개수(모든 strain 합).</summary>
        public int totalHarvested;

        /// <summary>실제 입금 개수(수확 × 보존율의 합).</summary>
        public int totalDeposited;
    }
}
