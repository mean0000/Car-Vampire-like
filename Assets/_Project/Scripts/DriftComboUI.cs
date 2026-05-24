using UnityEngine;
using TMPro;

public class DriftComboUI : MonoBehaviour
{
    public static DriftComboUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI comboText;   // "DRIFT x3" 텍스트
    [SerializeField] private TextMeshProUGUI bonusText;   // "+15 XP" 팝업 텍스트
    [SerializeField] private int bonusXpPerKill = 5;      // 드리프트 킬 1개당 보너스 XP

    private int _comboCount;
    private float _resetTimer;
    private const float ResetDelay = 2f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (comboText != null) comboText.gameObject.SetActive(false);
        if (bonusText != null) bonusText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_comboCount <= 0) return;
        _resetTimer -= Time.deltaTime;
        if (_resetTimer <= 0f)
            ResetCombo();
    }

    public void RegisterDriftKill()
    {
        _comboCount++;
        _resetTimer = ResetDelay;

        int bonus = bonusXpPerKill * _comboCount;
        XPManager.Instance?.AddXP(bonus);

        if (comboText != null)
        {
            comboText.gameObject.SetActive(true);
            comboText.text = $"DRIFT x{_comboCount}";
        }

        if (bonusText != null)
        {
            bonusText.gameObject.SetActive(true);
            bonusText.text = $"+{bonus} XP";
            StopCoroutine("HideBonusText");
            StartCoroutine("HideBonusText");
        }
    }

    private void ResetCombo()
    {
        _comboCount = 0;
        if (comboText != null) comboText.gameObject.SetActive(false);
        if (bonusText != null) bonusText.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator HideBonusText()
    {
        yield return new WaitForSeconds(1f);
        if (bonusText != null) bonusText.gameObject.SetActive(false);
    }
}
