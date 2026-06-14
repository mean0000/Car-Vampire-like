using System;
using UnityEngine;

namespace Run
{
    /// <summary>
    /// 위협 레벨 = 압박 단일 다이얼. 상승원(티어 깊이 + 시간 초과)을 합산해
    /// 곱셈자(스탯/밀도) + 변이 단계를 노출한다. ★소비자는 읽기 전용 프로퍼티만 본다.
    /// 싱크 게이지(SyncRateManager)와 무관 — 압박은 절대 SyncRate를 안 읽는다(06-13 피벗, P6).
    /// </summary>
    [DefaultExecutionOrder(-90)]   // QuotaRun·소비자보다 먼저 갱신(읽기 전 값 확정)
    public class ThreatLevel : MonoBehaviour
    {
        public static ThreatLevel Instance { get; private set; }

        [SerializeField] ThreatProfile profile;

        // ── 소비자용 읽기 전용 API (스폰 디렉터·몬스터·HUD가 이것만 본다) ──
        /// <summary>합산 위협 T(0~1+, 상한 없음 — 곡선이 포화).</summary>
        public float Current { get; private set; }

        /// <summary>몬스터 능력치 배수(≥1). ★소비: ZombieController가 스폰 시 1회 스냅샷(P4).</summary>
        public float StatScale { get; private set; } = 1f;

        /// <summary>스폰 밀도 배수(≥1). ★소비: ZombieSpawner가 라이브로 읽음.</summary>
        public float SpawnDensityMult { get; private set; } = 1f;

        /// <summary>변이 단계(0=기본). Phase 2 스폰 디렉터가 단계별 패턴 풀 활성.</summary>
        public int MutationStage { get; private set; }

        /// <summary>변이 단계 상승 시 발화(새 패턴 활성 트리거 — Phase 2 디렉터 구독).</summary>
        public event Action<int> OnMutationStageChanged;

        float _overtimeSec;   // 할당량 타이머 만료 후 누적 경과(위협 시간초과 상승원)

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (profile == null) return;   // 미배선 시 NRE 방지(스테이징 안전)

            float tDepth = ThreatFromDepth();
            float tOver = ThreatFromOvertime();   // QuotaRun.IsOvertime + 경과 누적
            Current = tDepth + tOver;

            StatScale = profile.StatScaleAt(Current);
            SpawnDensityMult = profile.DensityMultAt(Current);

            int stage = profile.MutationStageAt(Current);
            if (stage != MutationStage)
            {
                MutationStage = stage;
                OnMutationStageChanged?.Invoke(stage);
            }
        }

        // 위협 = 티어 깊이(룰렛 한 탕 더의 누적). 티어1=0, 티어2=+depthPerTier, …
        float ThreatFromDepth()
        {
            var q = QuotaRunController.Instance;
            return q == null ? 0f : (q.CurrentTier - 1) * profile.depthPerTier;
        }

        // 위협 = 할당량 시간 초과 누적. 만료 전(또는 미가동)이면 누적 리셋·0.
        float ThreatFromOvertime()
        {
            var q = QuotaRunController.Instance;
            if (q == null || !q.IsOvertime) { _overtimeSec = 0f; return 0f; }
            _overtimeSec += Time.deltaTime;   // 게임 시간 기준 — timeScale 0이면 멈춤
            return profile.overtimeRamp * _overtimeSec;
        }
    }
}
