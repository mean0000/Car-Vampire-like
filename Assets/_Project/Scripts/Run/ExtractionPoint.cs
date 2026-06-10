using System;
using UnityEngine;

namespace Run
{
    /// <summary>
    /// 탈출 지점. 플레이어가 반경(RunConfig.extractionRadius) 안에 머물면 회수 헬기를 호출(딜레이 카운트),
    /// 반경을 벗어나면 취소. 딜레이 완료 → OnExtractionComplete(=cash-out, RunManager가 Extracted로 수렴).
    /// 비주얼은 RunLoopSetup이 만드는 시안 이미시브 마커(라이트 금지·이미시브만).
    /// 거리 판정은 트리거 콜라이더 대신 거리 폴링으로 단순화(반경이 RunConfig에서 변경 가능).
    /// </summary>
    public class ExtractionPoint : MonoBehaviour
    {
        [SerializeField] RunConfig config;

        /// <summary>헬기 도착(딜레이 완료) 시 1회 발화.</summary>
        public event Action OnExtractionComplete;

        /// <summary>플레이어가 반경 안에 있고 헬기 호출이 진행 중인지.</summary>
        public bool IsExtracting { get; private set; }

        /// <summary>헬기 도착까지 남은 시간(초). 호출 중이 아니면 전체 딜레이.</summary>
        public float Remaining { get; private set; }

        /// <summary>호출 진행도(0~1). HUD가 표시.</summary>
        public float Progress01
        {
            get
            {
                float delay = config != null ? config.helicopterDelaySeconds : 1f;
                return delay > 0f ? 1f - Mathf.Clamp01(Remaining / delay) : 1f;
            }
        }

        Transform _player;
        bool _completed;

        void Start()
        {
            Remaining = config != null ? config.helicopterDelaySeconds : 18f;
            var pc = PlayerController.Instance;
            if (pc != null) _player = pc.transform;
        }

        void Update()
        {
            var rm = RunManager.Instance;
            if (rm == null || _completed) return;

            // InMission/Extracting에서만 동작. 그 외(사무실/정산)는 무시 + 진행 중이던 호출 상태 리셋
            // (사망/만료로 Settled 진입 시 IsExtracting 잔재가 HUD에 남는 것 방지).
            if (rm.Phase != RunManager.RunPhase.InMission && rm.Phase != RunManager.RunPhase.Extracting)
            {
                if (IsExtracting)
                {
                    IsExtracting = false;
                    Remaining = config != null ? config.helicopterDelaySeconds : 18f;
                }
                return;
            }

            if (_player == null)
            {
                var pc = PlayerController.Instance;
                if (pc != null) _player = pc.transform; else return;
            }

            float radius = config != null ? config.extractionRadius : 4f;
            float delay = config != null ? config.helicopterDelaySeconds : 18f;

            Vector3 d = _player.position - transform.position; d.y = 0f;
            bool inside = d.sqrMagnitude <= radius * radius;

            if (inside)
            {
                if (!IsExtracting)
                {
                    IsExtracting = true;
                    Remaining = delay;
                    rm.RequestExtraction();
                }

                Remaining -= Time.deltaTime;   // 게임 시간 — timeScale 0이면 멈춤
                if (Remaining <= 0f)
                {
                    Remaining = 0f;
                    _completed = true;
                    IsExtracting = false;
                    OnExtractionComplete?.Invoke();
                }
            }
            else if (IsExtracting)
            {
                // 반경 이탈 → 취소. 딜레이 리셋(다음 진입 시 처음부터).
                IsExtracting = false;
                Remaining = delay;
                rm.CancelExtraction();
            }
        }
    }
}
