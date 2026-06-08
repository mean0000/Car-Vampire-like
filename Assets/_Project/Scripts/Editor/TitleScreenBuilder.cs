using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 새 Title.unity 씬을 빈 씬으로 만들고 키아트 배경 + 큰 로고 + 광범위 글리치 타이틀을 빌드한다.
/// 메뉴: Tools > ZombieCrush > Build Title Screen  (재실행 시 빈 씬으로 새로 만듦)
/// MCP는 씬 저장 다이얼로그에 막혀서 에디터 메뉴로 1회 실행하는 방식.
/// </summary>
public static class TitleScreenBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Title.unity";
    const string FontDir = "Assets/_Project/Font";
    const string GameScene = "WeaponSelect";

    static TMP_FontAsset _display, _body, _mono;

    static Color BG     = Hex("#0B1316");
    static Color WHITE  = Hex("#EAF6F8");
    static Color CYAN   = Hex("#34E3FF");
    static Color CYAND  = Hex("#0FA8C4");
    static Color MUTED  = Hex("#6F8388");

    [MenuItem("Tools/ZombieCrush/Build Title Screen")]
    public static void Build()
    {
        _display = Load("BlackHanSans Dynamic SDF");
        _body    = Load("Pretendard-Regular Dynamic SDF");
        _mono    = Load("Galmuri11 Dynamic SDF");
        if (_display == null || _body == null || _mono == null)
        {
            Debug.LogError("[TitleBuilder] 폰트 에셋 누락 — 먼저 Tools > ZombieCrush > Generate Dynamic Fonts 실행.");
            return;
        }

        // 새 빈 씬 (현재 씬이 dirty면 Unity가 저장 여부 물어봄)
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라(검은 배경) — "No cameras rendering" 경고 방지
        var camGO = new GameObject("Main Camera", typeof(Camera));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BG;
        cam.cullingMask = 0;

        EnsureEventSystem();

        // ---- Canvas ----
        var canvasGO = new GameObject("Title Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ---- Background (단색 — 키아트는 일단 제외, 따로 놀아서) ----
        var bg = NewImage("Background", canvasGO.transform, BG);
        bg.raycastTarget = false;
        Stretch(bg.rectTransform);

        // ---- Content (흔들림 대상: 상단 클러스터 + 하단 메뉴) ----
        var content = NewRect("Content", canvasGO.transform);
        Stretch(content);

        // == 상단 클러스터 (인장/로고/부제/태그) ==
        var top = NewRect("TopCluster", content);
        top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.anchoredPosition = new Vector2(0, -230);   // 좀 더 아래로
        var vlg = top.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 20;
        vlg.childControlWidth = false; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        var fitTop = top.gameObject.AddComponent<ContentSizeFitter>();
        fitTop.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitTop.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 1) 부서 인장
        var sealGO = NewRect("Seal", top);
        sealGO.sizeDelta = new Vector2(78, 78);
        AddLayoutSize(sealGO.gameObject, 78, 78);
        var sealRing = NewImage("Ring", sealGO, Color.white);
        Stretch(sealRing.rectTransform);
        var sealText = MakeTMP("SealText", sealGO, _mono, 9f, CYAN, TextAlignmentOptions.Center, "사후처리부\nOFFICIAL\nSEAL");
        Stretch(sealText.rectTransform);
        sealText.lineSpacing = 6f;

        // 2+3) 타이틀 블록: 로고 + ":특이사항 없음"을 바짝 붙인 한 덩어리
        var block = NewRect("TitleBlock", top);
        var bvlg = block.gameObject.AddComponent<VerticalLayoutGroup>();
        bvlg.childAlignment = TextAnchor.UpperCenter;
        bvlg.spacing = 6;
        bvlg.childControlWidth = false; bvlg.childControlHeight = false;
        bvlg.childForceExpandWidth = false; bvlg.childForceExpandHeight = false;
        var bfit = block.gameObject.AddComponent<ContentSizeFitter>();
        bfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 로고 (LogoSlot 안에 stretch — 색분리 고스트가 레이아웃을 안 흔들게)
        // Bottom 정렬: 글리프가 슬롯 위로만 넘쳐 아래 부제와 안 겹치게
        var logoSlot = NewRect("LogoSlot", block);
        logoSlot.sizeDelta = new Vector2(1500, 230);
        AddLayoutSize(logoSlot.gameObject, 1500, 230);
        var logo = MakeTMP("Logo", logoSlot, _display, 240f, WHITE, TextAlignmentOptions.Bottom, "사후처리부");
        logo.characterSpacing = -6f;
        Stretch(logo.rectTransform);

        // 글리치 부제 — 로고 바로 아래(Top 정렬), 콜론 접두로 라벨 느낌
        var subSlot = NewRect("SubtitleSlot", block);
        subSlot.sizeDelta = new Vector2(720, 60);
        AddLayoutSize(subSlot.gameObject, 720, 60);
        var subtitle = MakeTMP("Subtitle", subSlot, _display, 40f, WHITE, TextAlignmentOptions.Top,
            ":특이사항 <color=#EAF6F8>없음</color>");
        subtitle.characterSpacing = 2f;
        Stretch(subtitle.rectTransform);

        // 4) 태그라인
        var tag = MakeTMP("Tagline", top, _mono, 16f, MUTED, TextAlignmentOptions.Center,
            "AFTERCARE DIVISION · CASE STATUS: ROUTINE");
        tag.characterSpacing = 4f;
        FitText(tag);

        // == 하단 메뉴 (가로) ==
        var menuRow = NewRect("MenuRow", content);
        menuRow.anchorMin = menuRow.anchorMax = new Vector2(0.5f, 0f);
        menuRow.pivot = new Vector2(0.5f, 0f);
        menuRow.anchoredPosition = new Vector2(0, 110);
        var hlg = menuRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 48;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var fitM = menuRow.gameObject.AddComponent<ContentSizeFitter>();
        fitM.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitM.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        AddLayoutSize(menuRow.gameObject, 0, 48, onlyHeight: true);

        var btnStart = MakeMenuButton("Btn_Start", menuRow, "게임 시작", CYAN);
        var btnSettings = MakeMenuButton("Btn_Settings", menuRow, "설정", MUTED);
        var btnQuit = MakeMenuButton("Btn_Quit", menuRow, "종료", MUTED);

        // ---- Footer (흔들림에서 제외 → Content 밖) ----
        var footer = MakeTMP("Footer", canvasGO.transform, _mono, 16f, CYAND, TextAlignmentOptions.Right, "v0.0.1 · DEMO BUILD");
        var fr = footer.rectTransform;
        fr.anchorMin = fr.anchorMax = new Vector2(1, 0);
        fr.pivot = new Vector2(1, 0);
        fr.sizeDelta = new Vector2(360, 28);
        fr.anchoredPosition = new Vector2(-24, 18);

        // ---- Scanlines (최상단 오버레이, 은은한 CRT 무드) ----
        var scan = NewRawImage("Scanlines", canvasGO.transform);
        Stretch(scan.rectTransform);
        scan.raycastTarget = false;
        scan.transform.SetAsLastSibling();

        // ---- 컨트롤러 + FX 연결 ----
        var ctrl = canvasGO.AddComponent<TitleScreenController>();
        ctrl.subtitleText = subtitle;
        ctrl.logoText = logo;
        ctrl.startButton = btnStart;
        ctrl.settingsButton = btnSettings;
        ctrl.quitButton = btnQuit;
        ctrl.gameSceneName = GameScene;

        var fx = canvasGO.AddComponent<TitleFX>();
        fx.scanlines = scan;
        fx.sealRing = sealRing;

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[TitleBuilder] 타이틀 화면 빌드 완료 — " + ScenePath + " 저장됨.");
    }

    // ---------- helpers ----------
    static TMP_FontAsset Load(string name) =>
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "/" + name + ".asset");

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image NewImage(string name, Transform parent, Color c)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static RawImage NewRawImage(string name, Transform parent)
    {
        var rt = NewRect(name, parent);
        return rt.gameObject.AddComponent<RawImage>();
    }

    static TextMeshProUGUI MakeTMP(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, string text)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    static void FitText(TextMeshProUGUI t)
    {
        var fit = t.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static Button MakeMenuButton(string name, Transform parent, string label, Color normal)
    {
        var t = MakeTMP(name, parent, _body, 34f, normal, TextAlignmentOptions.Center, label);
        t.fontStyle = FontStyles.Bold;
        t.raycastTarget = true;
        FitText(t);

        var btn = t.gameObject.AddComponent<Button>();
        btn.targetGraphic = t;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = Color.white;            // 곱연산이라 흰색=원색 유지
        cb.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
        cb.pressedColor = CYAN;
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.12f;
        cb.colorMultiplier = 1f;
        btn.colors = cb;
        return btn;
    }

    static void AddLayoutSize(GameObject go, float w, float h, bool onlyHeight = false)
    {
        var le = go.AddComponent<LayoutElement>();
        if (!onlyHeight) le.preferredWidth = w;
        le.preferredHeight = h;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString(h, out var c);
        return c;
    }
}
