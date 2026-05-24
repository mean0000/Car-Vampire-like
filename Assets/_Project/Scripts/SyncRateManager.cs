using UnityEngine;

/// <summary>
/// 나노봇 동기화율(SYNC RATE) 관리 싱글톤.
/// 0f = 완전 억제, 1f = 완전 동기화(게임오버).
/// </summary>
[DefaultExecutionOrder(-100)]
public class SyncRateManager : MonoBehaviour
{
    public static SyncRateManager Instance { get; private set; }

    private float _syncRate = 0f;

    /// <summary>현재 동기화율 (0f ~ 1f)</summary>
    public float SyncRate => _syncRate;

    /// <summary>동기화율 변경 이벤트 (현재 syncRate 전달)</summary>
    public event System.Action<float> OnSyncChanged;

    /// <summary>동기화율 100% 도달 이벤트</summary>
    public event System.Action OnSyncMaxed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>동기화율 증가. 부스트(억제 해제) 시 호출. amount는 0~1 범위.</summary>
    public void AddSync(float amount)
    {
        if (_syncRate >= 1f) return;

        _syncRate = Mathf.Clamp01(_syncRate + amount);
        OnSyncChanged?.Invoke(_syncRate);

        if (_syncRate >= 1f)
            OnSyncMaxed?.Invoke();
    }

    /// <summary>동기화율 감소. 피트스톱 진입 시 호출. amount는 0~1 범위.</summary>
    public void ReduceSync(float amount)
    {
        _syncRate = Mathf.Clamp01(_syncRate - amount);
        OnSyncChanged?.Invoke(_syncRate);
    }
}
