using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그레이박스 HUD 갱신. 소음/감염/HP를 매 프레임 반영.
/// 참조가 비어 있어도 안전하게 건너뛴다.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] Text noiseText;
    [SerializeField] Text infectionText;
    [SerializeField] Text hpText;
    [Header("Noise Bar")]
    [SerializeField] Image noiseBarFill;

    [Header("Crafting")]
    [SerializeField] Text partsText;
    [SerializeField] Image craftBarFill;
    [SerializeField] Text craftLabel;

    [Header("Colors")]
    [SerializeField] Color quietColor = new Color(0.4f, 1f, 0.5f);
    [SerializeField] Color loudColor = new Color(1f, 0.25f, 0.15f);

    void Update()
    {
        UpdateNoise();
        UpdateInfection();
        UpdateHP();
        UpdateCrafting();
    }

    void UpdateCrafting()
    {
        var cs = CraftingSystem.Instance;

        if (partsText != null)
            partsText.text = $"PARTS: {(cs != null ? cs.Parts : 0)}";

        bool crafting = cs != null && cs.IsCrafting;
        if (craftBarFill != null)
        {
            craftBarFill.transform.parent.gameObject.SetActive(crafting);
            if (crafting) craftBarFill.fillAmount = cs.CraftProgress01;
        }
        if (craftLabel != null)
            craftLabel.enabled = crafting;
    }

    void UpdateNoise()
    {
        var nm = NoiseManager.Instance;
        float noise = nm != null ? nm.CurrentNoise : 0f;
        bool chase = nm != null && nm.IsChaseThreshold;
        float t = Mathf.Clamp01(noise / 50f);
        Color c = Color.Lerp(quietColor, loudColor, t);

        if (noiseText != null)
        {
            noiseText.text = chase ? $"NOISE: {Mathf.RoundToInt(noise)}  [CHASE!]" : $"NOISE: {Mathf.RoundToInt(noise)}";
            noiseText.color = c;
        }
        if (noiseBarFill != null)
        {
            // 추격 임계(50) 기준으로 정규화 — 바가 가득 차면 곧 추격. 텍스트/링 색과 동일 스케일.
            noiseBarFill.fillAmount = t;
            noiseBarFill.color = c;
        }
    }

    void UpdateInfection()
    {
        if (infectionText == null) return;
        var sm = SyncRateManager.Instance;
        float sync = sm != null ? sm.SyncRate : 0f;
        int cells = Mathf.RoundToInt(sync * 10f);
        infectionText.text = $"INFECTION: {cells} / 10";
    }

    void UpdateHP()
    {
        if (hpText == null) return;
        var pc = PlayerController.Instance;
        if (pc == null) return;
        hpText.text = $"HP: {Mathf.CeilToInt(pc.CurrentHP)} / {Mathf.RoundToInt(pc.MaxHP)}";
    }
}
