using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SYNC RATE 수치를 fillImage fillAmount와 색상으로 표시.
/// vignetteImage(선택)가 있으면 syncRate 70% 이상부터 alpha 증가.
/// </summary>
public class SyncRateUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI syncLabel;          // 선택
    [SerializeField] private Image vignetteImage;                // 선택, null 허용

    [SerializeField] private Color colorLow  = new Color(0.2f, 0.6f, 0.8f, 1f); // 회청색
    [SerializeField] private Color colorHigh = new Color(0.9f, 0.1f, 0.1f, 1f); // 진홍색

    private void OnEnable()
    {
        if (SyncRateManager.Instance == null) return;
        SyncRateManager.Instance.OnSyncChanged += HandleSyncChanged;
        HandleSyncChanged(SyncRateManager.Instance.SyncRate);
    }

    private void OnDisable()
    {
        if (SyncRateManager.Instance != null)
            SyncRateManager.Instance.OnSyncChanged -= HandleSyncChanged;
    }

    private void HandleSyncChanged(float syncRate)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = syncRate;
            fillImage.color = Color.Lerp(colorLow, colorHigh, syncRate);
        }

        if (vignetteImage != null)
        {
            // 70% 이상부터 alpha 0 → 0.6 선형 증가
            float vignetteT = Mathf.Clamp01((syncRate - 0.7f) / 0.3f);
            Color c = vignetteImage.color;
            c.a = vignetteT * 0.6f;
            vignetteImage.color = c;
        }
    }
}
