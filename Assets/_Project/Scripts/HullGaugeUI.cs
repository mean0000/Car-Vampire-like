using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HULL 내구도 게이지 UI.
/// HullManager.OnHullChanged를 구독해 fillImage를 갱신한다.
/// </summary>
public class HullGaugeUI : MonoBehaviour
{
    [SerializeField] Image fillImage;

    private void Start()
    {
        if (HullManager.Instance != null)
        {
            HullManager.Instance.OnHullChanged += UpdateGauge;
            UpdateGauge(HullManager.Instance.HullRatio);
        }
    }

    private void OnDestroy()
    {
        if (HullManager.Instance != null)
            HullManager.Instance.OnHullChanged -= UpdateGauge;
    }

    void UpdateGauge(float ratio)
    {
        if (fillImage == null) return;
        fillImage.fillAmount = ratio;
        // 0 = 적색(파괴 직전), 1 = 청록(완전한 상태)
        fillImage.color = Color.Lerp(Color.red, new Color(0f, 0.8f, 0.8f), ratio);
    }
}
