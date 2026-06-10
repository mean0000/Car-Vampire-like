using System;
using System.Collections.Generic;
using UnityEngine;
using Run;

namespace Run
{
    /// <summary>
    /// 한 런 동안 모은 strain(휘발). 좀비 사망 시 Add로 누적되고, Settle 시 Wallet으로 입금된다.
    /// 탈출 실패(사망/sweep) 시 retentionRate=0이라 전량 소실 → press-your-luck의 '무게'.
    /// 씬 싱글톤. UI(드롭 세리머니 자리)는 OnHarvested를 구독만 한다.
    /// </summary>
    public class RunHarvest : MonoBehaviour
    {
        public static RunHarvest Instance { get; private set; }

        readonly Dictionary<StrainDef, int> _counts = new Dictionary<StrainDef, int>();

        /// <summary>strain이 수확될 때마다 발화. (def, 누적 후 총량). 드롭 세리머니/HUD가 구독.</summary>
        public event Action<StrainDef, int> OnHarvested;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>특정 strain의 이번 런 수확량.</summary>
        public int Count(StrainDef def)
        {
            if (def == null) return 0;
            return _counts.TryGetValue(def, out int n) ? n : 0;
        }

        /// <summary>strain 수확. n만큼 누적하고 OnHarvested 발화.</summary>
        public void Add(StrainDef def, int n = 1)
        {
            if (def == null || n <= 0) return;
            _counts.TryGetValue(def, out int cur);
            cur += n;
            _counts[def] = cur;
            OnHarvested?.Invoke(def, cur);
        }

        /// <summary>이번 런 수확 전체 스냅샷(입금 계산용).</summary>
        public IReadOnlyDictionary<StrainDef, int> Snapshot() => _counts;

        /// <summary>새 런 시작 시 초기화.</summary>
        public void Clear() => _counts.Clear();
    }
}
