using System;
using UnityEngine;

namespace Run
{
    /// <summary>
    /// 작전 카운트다운. 게임 시간 기준(Time.deltaTime) — timeScale 0(사무실/정산)이면 멈춘다.
    /// Begin(seconds)로 시작, 0이 되면 OnExpired 1회 발화(=sweep 경로).
    /// (Phase 2 자리: Extend() — 업무+시간 재심 통과 시에만)
    /// </summary>
    public class OperationTimer : MonoBehaviour
    {
        public static OperationTimer Instance { get; private set; }

        /// <summary>남은 시간(초).</summary>
        public float Remaining { get; private set; }

        /// <summary>현재 카운트다운이 흐르는 중인지.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>매 프레임(흐를 때) 남은 시간 발화. HUD가 구독.</summary>
        public event Action<float> OnTick;

        /// <summary>남은 시간이 0이 되는 순간 1회 발화. RunManager가 sweep으로 수렴.</summary>
        public event Action OnExpired;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>카운트다운 시작/재시작.</summary>
        public void Begin(float seconds)
        {
            Remaining = Mathf.Max(0f, seconds);
            IsRunning = Remaining > 0f;
            OnTick?.Invoke(Remaining);
        }

        /// <summary>카운트다운 정지(만료 발화 없이). Settle 시 RunManager가 호출.</summary>
        public void Stop()
        {
            IsRunning = false;
        }

        void Update()
        {
            if (!IsRunning) return;

            // 게임 시간 기준 — timeScale 0이면 deltaTime=0이라 자연히 멈춘다.
            Remaining -= Time.deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                IsRunning = false;
                OnTick?.Invoke(0f);
                OnExpired?.Invoke();
                return;
            }
            OnTick?.Invoke(Remaining);
        }
    }
}
