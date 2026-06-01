using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 무기 선택 화면. 카드(WeaponLoadout.Weapons 개수)를 띄워 하나를 고르게 하고,
/// '출발'을 누르면 고른 무기를 WeaponLoadout에 기록한 뒤 게임 씬을 로드한다.
/// 카드 제목/설명은 WeaponLoadout(단일 출처)에서 채운다 — 스탯과 UI가 어긋나지 않게.
/// 버튼은 코드에서 AddListener로 연결(인스펙터 영구 이벤트 직렬화 회피 → MCP 와이어링 단순).
/// 시작 시 0번을 기본 선택해 '출발'이 항상 동작한다.
/// </summary>
public class WeaponSelectUI : MonoBehaviour
{
    [SerializeField] string gameSceneName = "Greybox_MVP";
    [SerializeField] Button[] cardButtons;        // 무기 카드 버튼들 (WeaponLoadout.Weapons 순서)
    [SerializeField] Image[] cardHighlights;      // 카드별 선택 표시 테두리 (cardButtons와 같은 순서)
    [SerializeField] TMPro.TextMeshProUGUI[] cardTitles;
    [SerializeField] TMPro.TextMeshProUGUI[] cardDescs;
    [SerializeField] Button goButton;

    [SerializeField] Color highlightOn = new Color(0.55f, 0.82f, 0.35f, 1f);
    [SerializeField] Color highlightOff = new Color(1f, 1f, 1f, 0f);

    int _selected = 0;

    void Start()
    {
        Time.timeScale = 1f;   // 정지 상태로 넘어온 경우 대비

        var weapons = WeaponLoadout.Weapons;
        if (cardButtons != null)
        {
            for (int i = 0; i < cardButtons.Length; i++)
            {
                if (cardButtons[i] == null) continue;

                if (i < weapons.Length)
                {
                    if (cardTitles != null && i < cardTitles.Length && cardTitles[i] != null)
                        cardTitles[i].text = weapons[i].name;
                    if (cardDescs != null && i < cardDescs.Length && cardDescs[i] != null)
                        cardDescs[i].text = $"{weapons[i].desc}\n\nDMG {weapons[i].damage}  ·  사거리 {weapons[i].range:0}  ·  소음 {weapons[i].gunshotNoise:0}";

                    int idx = i;   // 클로저 캡처
                    cardButtons[i].onClick.AddListener(() => Select(idx));
                }
                else
                {
                    cardButtons[i].gameObject.SetActive(false);
                }
            }
        }

        if (goButton != null) goButton.onClick.AddListener(Go);

        Select(0);
    }

    void Select(int idx)
    {
        _selected = idx;
        if (cardHighlights == null) return;
        for (int i = 0; i < cardHighlights.Length; i++)
        {
            if (cardHighlights[i] != null)
                cardHighlights[i].color = (i == idx) ? highlightOn : highlightOff;
        }
    }

    void Go()
    {
        WeaponLoadout.SelectedIndex = _selected;
        SceneManager.LoadScene(gameSceneName);
    }
}
