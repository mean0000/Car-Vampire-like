using System.Collections.Generic;
using UnityEngine;
using Run;

namespace Meta
{
    /// <summary>
    /// 메타 진행의 단일 권위(영구): StrainWallet + MetaUpgrades + 저장소.
    /// 씬 리로드(=런 재시작)를 가로질러 살아남아야 하므로 DontDestroyOnLoad.
    /// 동시에 변경 시 디스크에 저장해 세션을 가로질러서도 유지된다.
    /// SO 참조는 에셋 이름 ↔ 인스턴스 해석에 쓰인다(저장 키 = 에셋 이름).
    /// </summary>
    [DefaultExecutionOrder(-100)]   // PlayerCombat(-10)보다 먼저 — 안 그러면 메타 배율이 1.0으로 고정됨
    public class MetaProgress : MonoBehaviour
    {
        public static MetaProgress Instance { get; private set; }

        [Header("Strain (스켈레톤 = 생체 1성 단일)")]
        [Tooltip("스켈레톤 단일 strain. 비용·입금 모두 이 종을 사용.")]
        [SerializeField] StrainDef bioStrain;

        [Header("Upgrades (배율 산출용)")]
        [SerializeField] UpgradeDef damageUpgrade;
        [SerializeField] UpgradeDef fireRateUpgrade;

        public StrainWallet Wallet { get; private set; }
        public MetaUpgrades Upgrades { get; private set; }

        /// <summary>스켈레톤 단일 strain(좀비 드롭·비용·입금 기본값).</summary>
        public StrainDef BioStrain => bioStrain;
        public UpgradeDef DamageUpgrade => damageUpgrade;
        public UpgradeDef FireRateUpgrade => fireRateUpgrade;

        ISaveStore _store;
        Dictionary<string, StrainDef> _strainByName;
        Dictionary<string, UpgradeDef> _upgradeByName;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildResolvers();

            Wallet = new StrainWallet();
            Upgrades = new MetaUpgrades(Wallet, bioStrain);
            Upgrades.RegisterEffectDefs(damageUpgrade, fireRateUpgrade);

            _store = new JsonFileStore();
            Load();

            // 변경 시 자동 저장(구매·입금). 업그레이드 레벨 변경도 저장해야 한다 —
            // Wallet 차감(TrySpend)이 레벨 증가보다 먼저라, Wallet만 구독하면 레벨이 한 스텝 누락된다.
            Wallet.OnBalanceChanged += Save;
            Upgrades.OnChanged += Save;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildResolvers()
        {
            _strainByName = new Dictionary<string, StrainDef>();
            if (bioStrain != null) _strainByName[bioStrain.name] = bioStrain;

            _upgradeByName = new Dictionary<string, UpgradeDef>();
            if (damageUpgrade != null) _upgradeByName[damageUpgrade.name] = damageUpgrade;
            if (fireRateUpgrade != null) _upgradeByName[fireRateUpgrade.name] = fireRateUpgrade;
        }

        StrainDef ResolveStrain(string n) =>
            n != null && _strainByName.TryGetValue(n, out var d) ? d : null;

        UpgradeDef ResolveUpgrade(string n) =>
            n != null && _upgradeByName.TryGetValue(n, out var d) ? d : null;

        void Load()
        {
            string json = _store.Load();
            if (string.IsNullOrEmpty(json)) return;
            MetaSaveData data;
            try { data = JsonUtility.FromJson<MetaSaveData>(json); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MetaProgress] 세이브 파싱 실패 — 무시하고 새로 시작: {e.Message}");
                return;
            }
            if (data == null) return;
            Wallet.ReadFrom(data, ResolveStrain);
            Upgrades.ReadFrom(data, ResolveUpgrade);
        }

        /// <summary>디스크에 영구 저장. 구매(Wallet 변경)·입금 후 호출.</summary>
        public void Save()
        {
            var data = new MetaSaveData();
            Wallet.WriteTo(data);
            Upgrades.WriteTo(data);
            _store.Save(JsonUtility.ToJson(data));
        }
    }
}
