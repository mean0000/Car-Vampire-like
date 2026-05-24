using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SYNC RATE 100% 도달 시 게임오버 패널을 표시하는 싱글톤 UI.
/// SyncRateManager.OnSyncMaxed를 구독해 자동 활성화된다.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [SerializeField] private GameObject panel;

    public bool IsPanelOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panel != null)
            panel.SetActive(false);
    }

    private void Start()
    {
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncMaxed += Show;
    }

    private void OnDisable()
    {
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncMaxed -= Show;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncMaxed -= Show;
    }

    private void Show()
    {
        if (IsPanelOpen) return;
        if (panel == null) return;
        panel.SetActive(true);
        IsPanelOpen = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 재시작 버튼에서 호출. timeScale 복구 후 현재 씬 재로드.
    /// </summary>
    public void OnRestartPressed()
    {
        IsPanelOpen = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
