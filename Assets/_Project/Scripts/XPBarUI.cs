using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// XPManager 이벤트를 구독해 XP 바와 레벨 텍스트를 갱신.
/// fillImage: Image Type = Filled로 설정할 것.
/// </summary>
public class XPBarUI : MonoBehaviour
{
    [SerializeField] Image fillImage;
    [SerializeField] TMPro.TextMeshProUGUI levelText;

    void Start()
    {
        if (XPManager.Instance != null)
        {
            XPManager.Instance.OnXPChanged += HandleXPChanged;
            XPManager.Instance.OnLevelChanged += HandleLevelChanged;
            Refresh();
        }
    }

    void OnDestroy()
    {
        if (XPManager.Instance != null)
        {
            XPManager.Instance.OnXPChanged -= HandleXPChanged;
            XPManager.Instance.OnLevelChanged -= HandleLevelChanged;
        }
    }

    void HandleXPChanged(int currentXP, int maxXP)
    {
        if (fillImage != null)
            fillImage.fillAmount = maxXP > 0 ? currentXP / (float)maxXP : 0f;
    }

    void HandleLevelChanged(int newLevel)
    {
        if (levelText != null)
            levelText.text = $"Lv.{newLevel}";
    }

    void Refresh()
    {
        if (XPManager.Instance == null) return;
        HandleXPChanged(XPManager.Instance.CurrentXP, XPManager.Instance.GetCurrentThreshold());
        HandleLevelChanged(XPManager.Instance.CurrentLevel);
    }
}
