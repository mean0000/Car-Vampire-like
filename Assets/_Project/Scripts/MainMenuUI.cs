using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 시작 화면. START → 게임 씬 로드, QUIT → 종료.
/// 버튼은 코드에서 AddListener로 연결(인스펙터 영구 이벤트 직렬화 회피 → MCP 와이어링 단순).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] string gameSceneName = "WeaponSelect";
    [SerializeField] Button startButton;
    [SerializeField] Button quitButton;

    void Start()
    {
        // 이전 씬에서 timeScale=0으로 멈춘 채 메뉴로 돌아온 경우 대비(레벨업/상자 패널 정지 복구)
        Time.timeScale = 1f;
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
