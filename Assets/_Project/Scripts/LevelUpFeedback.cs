using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 레벨업을 "한 박자"로 느끼게 하는 XP바 주스. XPManager.OnLevelChanged 구독.
/// "LEVEL UP" 메시지/능력 선택은 LevelUpChoiceUI(카드 패널)가 담당하므로 여기선 바 자체만:
/// 레벨 텍스트 펀치 + XP바 화이트 플래시.
/// 카드 패널이 timeScale=0으로 멈추므로 트윈은 SetUpdate(true)(언스케일드)로 돌려 정지 중에도 보이게 한다.
/// 카메라 쉐이크(Feel 영역)는 레벨업이 잦아 멀미를 유발하므로 의도적으로 제외.
/// 색은 DOTween.To(코어)로 트윈해 UI 단축 모듈 의존을 피한다.
/// </summary>
public class LevelUpFeedback : MonoBehaviour
{
    [SerializeField] RectTransform levelText;   // 펀치할 Lv 텍스트
    [SerializeField] Image barFill;             // 화이트 플래시할 XP 채움

    Color _fillBase;

    void Start()
    {
        if (barFill != null) _fillBase = barFill.color;
        if (XPManager.Instance != null)
            XPManager.Instance.OnLevelChanged += HandleLevelUp;
    }

    void OnDestroy()
    {
        if (XPManager.Instance != null)
            XPManager.Instance.OnLevelChanged -= HandleLevelUp;
    }

    void HandleLevelUp(int newLevel)
    {
        if (levelText != null)
        {
            levelText.DOKill();
            levelText.localScale = Vector3.one;
            levelText.DOPunchScale(Vector3.one * 0.6f, 0.35f, 8, 0.6f).SetUpdate(true);
        }

        if (barFill != null)
        {
            barFill.DOKill();
            barFill.color = Color.white;
            DOTween.To(() => barFill.color, c => barFill.color = c, _fillBase, 0.45f)
                   .SetTarget(barFill)
                   .SetUpdate(true);
        }
    }
}
