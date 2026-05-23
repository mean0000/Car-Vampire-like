using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CarController의 부스터 연료 비율을 UI Image fillAmount로 표시.
/// Image Type = Filled로 설정된 fillImage에 연결할 것.
/// </summary>
public class BoostGaugeUI : MonoBehaviour
{
    [SerializeField] private CarController car;
    [SerializeField] private Image fillImage;

    [SerializeField] private Color colorNormal = new Color(1f, 0.85f, 0f, 1f); // 노란색
    [SerializeField] private Color colorActive = Color.white;                   // 부스터 활성
    [SerializeField] private Color colorEmpty  = new Color(0.3f, 0.3f, 0.3f, 1f); // 빈 상태

    private void Update()
    {
        if (car == null) return;

        float ratio = car.BoostFuelRatio;
        fillImage.fillAmount = ratio;

        if (ratio <= 0f)
            fillImage.color = colorEmpty;
        else if (car.IsBoostActive)
            fillImage.color = colorActive;
        else
            fillImage.color = colorNormal;
    }
}
