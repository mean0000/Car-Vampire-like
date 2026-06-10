using System;
using System.Collections.Generic;
using UnityEngine;
using Run;

namespace Meta
{
    /// <summary>
    /// 영구 strain 잔액(저장). RunHarvest(휘발)와 분리 — 탈출 성공만 입금된다.
    /// MetaProgress가 소유하며 저장/로드를 위임한다. UI는 OnBalanceChanged를 구독만 한다.
    /// </summary>
    public class StrainWallet
    {
        // StrainDef 참조로 잔액 보관(런타임). 저장 시 에셋 이름으로 직렬화.
        readonly Dictionary<StrainDef, int> _balances = new Dictionary<StrainDef, int>();

        /// <summary>잔액이 바뀔 때마다 발화(어떤 strain이든). UI가 구독.</summary>
        public event Action OnBalanceChanged;

        /// <summary>특정 strain의 영구 잔액.</summary>
        public int Balance(StrainDef def)
        {
            if (def == null) return 0;
            return _balances.TryGetValue(def, out int n) ? n : 0;
        }

        /// <summary>구매 등으로 차감. 잔액 부족이면 false(차감 안 함).</summary>
        public bool TrySpend(StrainDef def, int amount)
        {
            if (def == null || amount <= 0) return false;
            int cur = Balance(def);
            if (cur < amount) return false;
            _balances[def] = cur - amount;
            OnBalanceChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 런 수확을 retentionRate(탈출=1.0 / 사망·sweep=0.0 / ★Phase2 보험=0.3) 비율로 입금.
        /// 보험 전체가 이 인자 하나로 진입한다(헌장 부록 A).
        /// </summary>
        public void Deposit(IReadOnlyDictionary<StrainDef, int> harvest, float retentionRate)
        {
            if (harvest == null) return;
            float rate = Mathf.Clamp01(retentionRate);
            bool changed = false;
            foreach (var kv in harvest)
            {
                if (kv.Key == null) continue;
                int gain = Mathf.FloorToInt(kv.Value * rate);
                if (gain <= 0) continue;
                _balances.TryGetValue(kv.Key, out int cur);
                _balances[kv.Key] = cur + gain;
                changed = true;
            }
            if (changed) OnBalanceChanged?.Invoke();
        }

        // ───────── 저장/로드 (MetaProgress가 호출) ─────────

        /// <summary>에셋 이름 → 잔액으로 직렬화 데이터를 채운다.</summary>
        public void WriteTo(MetaSaveData data)
        {
            data.walletKeys.Clear();
            data.walletValues.Clear();
            foreach (var kv in _balances)
            {
                if (kv.Key == null || kv.Value <= 0) continue;
                data.walletKeys.Add(kv.Key.name);
                data.walletValues.Add(kv.Value);
            }
        }

        /// <summary>에셋 이름 → StrainDef 해석기로 잔액을 복원.</summary>
        public void ReadFrom(MetaSaveData data, Func<string, StrainDef> resolve)
        {
            _balances.Clear();
            if (data == null) return;
            int n = Mathf.Min(data.walletKeys.Count, data.walletValues.Count);
            for (int i = 0; i < n; i++)
            {
                var def = resolve(data.walletKeys[i]);
                if (def != null) _balances[def] = data.walletValues[i];
            }
        }
    }
}
