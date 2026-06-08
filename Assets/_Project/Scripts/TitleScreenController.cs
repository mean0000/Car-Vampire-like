using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 타이틀 글리치 연출 + 진입 메뉴.
/// 글리치는 글씨에만(화면 효과 없음). 지직거림은 부제가 "있음↔없음" 바뀌는 순간에만 발생한다.
/// 색분리는 로고/부제 뒤에 적/청 고스트 사본 2장을 깔아 흉내낸다(셰이더 불필요).
/// 로고는 TMP Glow로 은은하게 발광시켜 간판처럼 보이게 한다.
/// 버튼은 AddListener로 연결(MCP/빌더 친화).
/// </summary>
public class TitleScreenController : MonoBehaviour
{
    [Header("Glitch Targets (글씨에만)")]
    public TMP_Text subtitleText;
    public TMP_Text logoText;

    [Header("Logo Glow (간판 느낌)")]
    public bool logoGlow = true;
    public Color glowColor = new Color(0.204f, 0.890f, 1f, 1f); // #34E3FF
    [Range(0f, 1f)] public float glowPower = 0.35f;
    [Range(0f, 1f)] public float glowOuter = 0.4f;

    [Header("Menu")]
    public Button startButton;
    public Button settingsButton;
    public Button quitButton;
    public string gameSceneName = "WeaponSelect";

    const string PREFIX = ":특이사항 ";
    const string HIDDEN = "없음";   // 은폐(공식 입장)
    const string TRUTH  = "있음";   // 진실
    const string COL_HIDDEN = "#EAF6F8";
    const string COL_TRUTH  = "#FF4242";

    static readonly Color GhostRed  = new Color(1f, 0.259f, 0.259f);   // #FF4242
    static readonly Color GhostCyan = new Color(0.204f, 0.890f, 1f);   // #34E3FF

    class Sep
    {
        public TMP_Text red, cyan;
        public CanvasGroup cgR, cgC;
        public RectTransform rtR, rtC;
        public Vector2 home;
    }

    Sep _sub, _logo;

    void Awake()
    {
        if (subtitleText != null) _sub = MakeSep(subtitleText);
        if (logoText != null) _logo = MakeSep(logoText);
    }

    void Start()
    {
        Time.timeScale = 1f;
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        ApplyGlow();
        if (subtitleText != null) StartCoroutine(BootThenIdle());
    }

    void ApplyGlow()
    {
        if (!logoGlow || logoText == null) return;
        var mat = logoText.fontMaterial; // 접근 시 인스턴스화(공유 머티리얼 오염 방지)
        mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
        mat.SetColor(ShaderUtilities.ID_GlowColor, glowColor);
        mat.SetFloat(ShaderUtilities.ID_GlowPower, glowPower);
        mat.SetFloat(ShaderUtilities.ID_GlowOuter, glowOuter);
        mat.SetFloat(ShaderUtilities.ID_GlowInner, 0f);
        mat.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
    }

    // ---------- 색분리 고스트 ----------
    Sep MakeSep(TMP_Text src)
    {
        var s = new Sep { home = src.rectTransform.anchoredPosition };
        s.red = MakeGhost(src, GhostRed, out s.cgR, out s.rtR);
        s.cyan = MakeGhost(src, GhostCyan, out s.cgC, out s.rtC);
        return s;
    }

    TMP_Text MakeGhost(TMP_Text src, Color c, out CanvasGroup cg, out RectTransform rt)
    {
        var srcRT = src.rectTransform;
        var go = new GameObject(src.name + "_Ghost", typeof(RectTransform));
        rt = go.GetComponent<RectTransform>();
        rt.SetParent(srcRT.parent, false);
        rt.anchorMin = srcRT.anchorMin; rt.anchorMax = srcRT.anchorMax;
        rt.pivot = srcRT.pivot;
        rt.sizeDelta = srcRT.sizeDelta;
        rt.anchoredPosition = srcRT.anchoredPosition;
        rt.localScale = srcRT.localScale;
        rt.SetSiblingIndex(srcRT.GetSiblingIndex());   // 메인보다 뒤(아래)로

        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = src.font;
        t.fontSize = src.fontSize;
        t.fontStyle = src.fontStyle;
        t.alignment = src.alignment;
        t.characterSpacing = src.characterSpacing;
        t.enableWordWrapping = false;
        t.color = c;
        t.raycastTarget = false;
        t.text = StripTags(src.text);

        cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        return t;
    }

    static string StripTags(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        bool inTag = false;
        foreach (char ch in s)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(ch);
        }
        return sb.ToString();
    }

    void ApplySep(Sep s, float alpha, float mag)
    {
        if (s == null) return;
        s.cgR.alpha = alpha; s.cgC.alpha = alpha;
        s.rtR.anchoredPosition = s.home + new Vector2(Random.Range(-mag, -mag * 0.3f), Random.Range(0f, mag * 0.6f));
        s.rtC.anchoredPosition = s.home + new Vector2(Random.Range(mag * 0.3f, mag), Random.Range(-mag * 0.6f, 0f));
    }

    void HideSep(Sep s)
    {
        if (s == null) return;
        s.cgR.alpha = 0f; s.cgC.alpha = 0f;
        s.rtR.anchoredPosition = s.home;
        s.rtC.anchoredPosition = s.home;
    }

    void GlitchOff()
    {
        HideSep(_sub);
        HideSep(_logo);
    }

    // ---------- 시퀀스 (지직은 '있음↔없음' 바뀌는 순간에만) ----------
    IEnumerator BootThenIdle()
    {
        // A) 부팅: "있음"(진실) 잠깐 노출 → 토글 버스트로 지직 → "없음" 정정·고정
        SetSubtitle(true);
        ApplySep(_sub, 0.6f, 4f);
        ApplySep(_logo, 0.5f, 4f);
        yield return new WaitForSeconds(0.9f);

        float end = Time.time + 0.8f;
        bool truth = true;
        while (Time.time < end)
        {
            truth = !truth;
            SetSubtitle(truth);
            ApplySep(_sub, 0.65f, 5f);
            ApplySep(_logo, 0.55f, 4.5f);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.09f));
        }
        SetSubtitle(false);
        GlitchOff();

        // B) idle 재발: 한순간 "있음"이 새어나왔다 도로 "없음" — 바뀌는 동안만 지직
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(2.5f, 5.5f));
            SetSubtitle(true);
            float e2 = Time.time + Random.Range(0.14f, 0.24f);
            while (Time.time < e2)
            {
                ApplySep(_sub, 0.7f, 5f);
                ApplySep(_logo, 0.6f, 4.5f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.08f));
            }
            SetSubtitle(false);
            GlitchOff();
        }
    }

    void SetSubtitle(bool truth)
    {
        string word = truth ? TRUTH : HIDDEN;
        string col = truth ? COL_TRUTH : COL_HIDDEN;
        subtitleText.text = PREFIX + "<color=" + col + ">" + word + "</color>";
        if (_sub != null)
        {
            _sub.red.text = PREFIX + word;
            _sub.cyan.text = PREFIX + word;
        }
    }

    // ---------- 메뉴 ----------
    public void StartGame()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        Debug.Log("[Title] 설정 — 추후 구현 예정");
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
