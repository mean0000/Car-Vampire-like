using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 시 업그레이드 선택 UI.
/// Show() → 게임 일시정지 + 패널 활성화 + 버튼 3개 랜덤 표시.
/// </summary>
public class UpgradeMenuUI : MonoBehaviour
{
    public static UpgradeMenuUI Instance { get; private set; }

    [SerializeField] GameObject upgradePanel;
    [SerializeField] Button[] upgradeButtons;   // Inspector에서 버튼 3개 연결
    [SerializeField] TMPro.TextMeshProUGUI[] buttonLabels; // 버튼당 텍스트 1개씩

    CarController _car;

    // 업그레이드 옵션 정의
    struct UpgradeOption
    {
        public string label;
        public System.Action<CarController, XPManager> apply;
    }

    UpgradeOption[] _allOptions;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _allOptions = new UpgradeOption[]
        {
            new UpgradeOption
            {
                label = "속도 +10%",
                apply = (car, xp) => car.UpgradeMaxSpeed(1.1f)
            },
            new UpgradeOption
            {
                label = "부스트 용량 +30%",
                apply = (car, xp) => car.UpgradeBoostCapacity(1.3f)
            },
            new UpgradeOption
            {
                label = "좀비 처치 XP +1",
                apply = (car, xp) => xp.bonusXP += 1
            },
            new UpgradeOption
            {
                label = "드리프트 충전 +50%",
                apply = (car, xp) => car.UpgradeDriftFuelRate(1.5f)
            },
        };
    }

    void Start()
    {
        _car = FindFirstObjectByType<CarController>();

        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    public bool IsPanelOpen => upgradePanel != null && upgradePanel.activeSelf;

    public void Show()
    {
        if (upgradePanel == null || upgradeButtons == null || upgradeButtons.Length < 3) return;
        if (upgradePanel.activeSelf) return; // 이미 열려있으면 무시

        // 옵션 4개 중 3개 랜덤 선택
        int[] picked = PickThree(_allOptions.Length);

        for (int i = 0; i < Mathf.Min(upgradeButtons.Length, picked.Length); i++)
        {
            if (upgradeButtons[i] == null) continue;

            UpgradeOption opt = _allOptions[picked[i]];

            if (buttonLabels != null && i < buttonLabels.Length && buttonLabels[i] != null)
                buttonLabels[i].text = opt.label;

            upgradeButtons[i].onClick.RemoveAllListeners();
            upgradeButtons[i].onClick.AddListener(() => OnUpgradeSelected(opt));
        }

        upgradePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    void OnUpgradeSelected(UpgradeOption opt)
    {
        if (_car != null && XPManager.Instance != null)
            opt.apply(_car, XPManager.Instance);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    void OnDestroy()
    {
        // 패널이 열린 채로 오브젝트가 파괴될 경우 timeScale 복구
        if (IsPanelOpen)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }

    // Fisher-Yates shuffle로 0~count-1 중 3개 선택
    int[] PickThree(int count)
    {
        int[] indices = new int[count];
        for (int i = 0; i < count; i++) indices[i] = i;

        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
        }

        int take = Mathf.Min(3, count);
        int[] result = new int[take];
        for (int i = 0; i < take; i++) result[i] = indices[i];
        return result;
    }
}
