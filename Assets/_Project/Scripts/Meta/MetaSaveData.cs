using System.Collections.Generic;

namespace Meta
{
    /// <summary>
    /// JSON 직렬화 대상. Wallet 잔액 + Upgrade 레벨만 저장(헌장 부록 A).
    /// StrainDef/UpgradeDef는 에셋 이름을 키로 쓴다(런타임 참조를 직렬화하지 않음).
    /// JsonUtility는 Dictionary를 못 다루므로 병렬 리스트(key/value)로 보관한다.
    /// </summary>
    [System.Serializable]
    public class MetaSaveData
    {
        public List<string> walletKeys = new List<string>();
        public List<int> walletValues = new List<int>();

        public List<string> upgradeKeys = new List<string>();
        public List<int> upgradeLevels = new List<int>();

        /// <summary>현금 잔고(E-002: 메모리는 납품 품목 — 정산은 현금으로).</summary>
        public int cash;
    }
}
