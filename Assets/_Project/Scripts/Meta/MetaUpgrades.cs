using System;
using System.Collections.Generic;
using UnityEngine;
using Run;

namespace Meta
{
    /// <summary>
    /// 무기 강화 레벨(저장) + 전투 배율 산출. PlayerCombat은 배율 훅 한 줄로만 통합(헌장 부록 A).
    /// 구매는 MetaProgress.TrySpendCash로 현금을 차감한다(E-002). UI는 OnChanged를 구독만 한다.
    /// </summary>
    public class MetaUpgrades
    {
        readonly Dictionary<UpgradeDef, int> _levels = new Dictionary<UpgradeDef, int>();
        readonly MetaProgress _owner;   // 현금 잔고의 소유자(E-002: 비용 = 현금)

        // 데미지/연사 배율 산출용 def 참조(있으면 그 레벨로 배율 계산). 설정에서 주입.
        UpgradeDef _damageDef;
        UpgradeDef _fireRateDef;

        /// <summary>레벨/잔액 변화로 배율이 바뀔 수 있을 때 발화. PlayerCombat·UI가 구독.</summary>
        public event Action OnChanged;

        public MetaUpgrades(MetaProgress owner)
        {
            _owner = owner;
        }

        /// <summary>데미지/연사 업그레이드 def를 등록(배율 산출용).</summary>
        public void RegisterEffectDefs(UpgradeDef damageDef, UpgradeDef fireRateDef)
        {
            _damageDef = damageDef;
            _fireRateDef = fireRateDef;
        }

        /// <summary>보유 레벨(미보유=0).</summary>
        public int GetLevel(UpgradeDef def)
        {
            if (def == null) return 0;
            return _levels.TryGetValue(def, out int lv) ? lv : 0;
        }

        /// <summary>다음 레벨 구매 가능 여부(만렙 아님 + 현금 잔고 충분).</summary>
        public bool CanPurchase(UpgradeDef def)
        {
            if (def == null || _owner == null) return false;
            int cur = GetLevel(def);
            int cost = def.CostForNextLevel(cur);
            if (cost < 0) return false;   // 만렙
            return _owner.Cash >= cost;
        }

        /// <summary>다음 레벨 구매. TrySpendCash로 현금 차감 후 레벨 +1. 성공 시 OnChanged.</summary>
        public bool TryPurchase(UpgradeDef def)
        {
            if (def == null || _owner == null) return false;
            int cur = GetLevel(def);
            int cost = def.CostForNextLevel(cur);
            if (cost < 0) return false;
            if (!_owner.TrySpendCash(cost)) return false;
            _levels[def] = cur + 1;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>사격 피해 배율(데미지 def 보유 레벨 기준). def 없으면 1.</summary>
        public float GetDamageMultiplier() =>
            _damageDef != null ? _damageDef.MultiplierAtLevel(GetLevel(_damageDef)) : 1f;

        /// <summary>연사 배율(연사 def 보유 레벨 기준). def 없으면 1.</summary>
        public float GetFireRateMultiplier() =>
            _fireRateDef != null ? _fireRateDef.MultiplierAtLevel(GetLevel(_fireRateDef)) : 1f;

        // ───────── 저장/로드 ─────────

        public void WriteTo(MetaSaveData data)
        {
            data.upgradeKeys.Clear();
            data.upgradeLevels.Clear();
            foreach (var kv in _levels)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                data.upgradeKeys.Add(kv.Key.name);
                data.upgradeLevels.Add(kv.Value);
            }
        }

        public void ReadFrom(MetaSaveData data, Func<string, UpgradeDef> resolve)
        {
            _levels.Clear();
            if (data == null) return;
            int n = Mathf.Min(data.upgradeKeys.Count, data.upgradeLevels.Count);
            for (int i = 0; i < n; i++)
            {
                var def = resolve(data.upgradeKeys[i]);
                if (def != null) _levels[def] = data.upgradeLevels[i];
            }
        }
    }
}
