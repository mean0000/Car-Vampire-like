using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Run;
using Meta;

/// <summary>
/// 익스트랙션 루프 스켈레톤의 씬 와이어링(멱등). Tools/RunLoop/Setup Scene.
/// 매니저 오브젝트(RunManager+Timer+Harvest+MetaProgress) · ExtractionPoint(시안 마커) ·
/// RunLoopUI 캔버스를 생성/갱신하고 SO·참조를 연결한 뒤 씬을 저장한다.
/// CozyDuskSetup 패턴을 따른다. 여러 번 실행해도 중복 생성하지 않는다(이름으로 찾아 재사용).
/// </summary>
public static class RunLoopSetup
{
    const string DataDir = "Assets/_Project/Data/Run/";
    const string TargetScene = "Greybox_ScanLit_v2";

    static readonly Color CyanEmissive = new Color(0.35f, 0.75f, 0.85f) * 2f;

    [MenuItem("Tools/RunLoop/Setup Scene")]
    public static void Setup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != TargetScene)
        {
            Debug.LogError("[RunLoopSetup] wrong scene: " + scene.name + " (need " + TargetScene + ")");
            return;
        }

        // ── SO 로드 ──
        var runConfig = Load<RunConfig>("RunConfig");
        var bioStrain = Load<StrainDef>("Strain_Bio1");
        var dmgUpgrade = Load<UpgradeDef>("Upgrade_Damage");
        var frUpgrade = Load<UpgradeDef>("Upgrade_FireRate");
        var escProfile = Load<EscalationProfile>("EscalationProfile");
        if (runConfig == null || bioStrain == null || dmgUpgrade == null || frUpgrade == null || escProfile == null)
        {
            Debug.LogError("[RunLoopSetup] SO 누락 — 먼저 Run SO 에셋을 생성하세요.");
            return;
        }

        var pretendard = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/Font/Pretendard-Regular Dynamic SDF.asset");

        // ── 메타(영구) — ★런 매니저와 반드시 별도 GO: MetaProgress의 DontDestroyOnLoad가
        // 같은 GO의 RunManager까지 영구화시켜 씬 리로드(재출동) 후 Settled 상태가 잔존하는 버그 전력 ──
        var metaGO = GetOrCreate("MetaProgress", null);
        var metaProgress = EnsureComponent<MetaProgress>(metaGO);

        // ── 매니저 루트(씬 수명 — 리로드마다 새로 시작해야 함) ──
        var managers = GetOrCreate("RunLoop_Managers", null);
        var staleMeta = managers.GetComponent<MetaProgress>();
        if (staleMeta != null) Object.DestroyImmediate(staleMeta);   // 과거 셋업 마이그레이션

        var timer = EnsureComponent<OperationTimer>(managers);
        var harvest = EnsureComponent<RunHarvest>(managers);
        var runManager = EnsureComponent<RunManager>(managers);

        // MetaProgress SO 연결
        SetField(metaProgress, "bioStrain", bioStrain);
        SetField(metaProgress, "damageUpgrade", dmgUpgrade);
        SetField(metaProgress, "fireRateUpgrade", frUpgrade);

        // ── ExtractionPoint(시안 마커) ──
        var extraction = BuildExtractionPoint(runConfig);

        // ── 스포너에 EscalationProfile 주입 ──
        var spawner = Object.FindFirstObjectByType<ZombieSpawner>();
        if (spawner != null) SetField(spawner, "escalationProfile", escProfile);

        // ── UI 캔버스 ──
        var ui = BuildUI(pretendard, out CanvasGroup sweepFlash);

        // ── RunManager 참조 연결 ──
        SetField(runManager, "config", runConfig);
        SetField(runManager, "timer", timer);
        SetField(runManager, "harvest", harvest);
        SetField(runManager, "spawner", spawner);
        SetField(runManager, "extractionPoint", extraction);
        SetField(runManager, "sweepFlash", sweepFlash);

        // ── 저장 ──
        EditorUtility.SetDirty(managers);
        if (spawner != null) EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RunLoopSetup] done + saved. Managers/Extraction/UI wired.");
    }

    // ──────────────────────────────────────────── ExtractionPoint

    static ExtractionPoint BuildExtractionPoint(RunConfig config)
    {
        var go = GameObject.Find("ExtractionPoint");
        if (go == null)
        {
            go = new GameObject("ExtractionPoint");
            // 플레이어 시작(-38,1,-38)에서 도심 중심 방향 ~12m — 스캔으로 확인한 유일한 클리어 Ground 지점.
            Vector3 player = new Vector3(-38f, 1f, -38f);
            Vector3 toCenter = (Vector3.zero - player); toCenter.y = 0f; toCenter.Normalize();
            Vector3 pos = player + toCenter * 12f;
            // 정확한 지면 Y 샘플(Ground layer 6).
            if (Physics.Raycast(new Vector3(pos.x, 50f, pos.z), Vector3.down, out RaycastHit hit, 100f, 1 << 6))
                pos.y = hit.point.y;
            else
                pos.y = 1f;
            go.transform.position = pos;
        }

        var ep = EnsureComponent<ExtractionPoint>(go);
        SetField(ep, "config", config);

        // 시안 이미시브 마커(바닥 링/실린더). 라이트 금지·이미시브만.
        BuildCyanMarker(go.transform, config.extractionRadius);
        return ep;
    }

    static void BuildCyanMarker(Transform parent, float radius)
    {
        var existing = parent.Find("CyanMarker");
        GameObject marker;
        if (existing != null) marker = existing.gameObject;
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "CyanMarker";
            // 콜라이더 제거 — ExtractionPoint는 거리 폴링이라 물리 콜라이더 불필요(물리 간섭 차단).
            var col = marker.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            marker.transform.SetParent(parent, false);
        }

        // 납작한 디스크(바닥 링 형태). 실린더 기본 높이 2 → 0.1로 납작.
        marker.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        marker.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

        var mr = marker.GetComponent<MeshRenderer>();
        var mat = BuildCyanMaterial();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    static Material BuildCyanMaterial()
    {
        const string matPath = DataDir + "M_ExtractionCyan.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        // URP Unlit: BaseColor에 HDR 시안. 이미시브 키워드도 켜 블룸에 잡히게.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", CyanEmissive);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", CyanEmissive);
        mat.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ──────────────────────────────────────────── UI

    static GameObject BuildUI(TMP_FontAsset font, out CanvasGroup sweepFlash)
    {
        var root = GameObject.Find("RunLoopUI");
        bool fresh = root == null;
        if (fresh)
        {
            root = new GameObject("RunLoopUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;   // HudV2 위에
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // 매번 자식을 재구축(멱등) — 기존 자식 제거 후 다시 생성.
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        var ui = EnsureComponent<RunLoopUI>(root);

        // ── sweep 백색 플래시(최하단? 아니 최상단 풀스크린) ──
        var flashGO = NewUIChild(root.transform, "SweepFlash");
        Stretch(flashGO);
        var flashImg = flashGO.AddComponent<Image>();
        flashImg.color = Color.white;
        flashImg.raycastTarget = false;
        sweepFlash = flashGO.AddComponent<CanvasGroup>();
        sweepFlash.alpha = 0f;
        sweepFlash.interactable = false;
        sweepFlash.blocksRaycasts = false;

        // ── HUD 띠 ──
        var hudRoot = NewUIChild(root.transform, "HUD");
        Stretch(hudRoot);
        var hudCG = hudRoot.AddComponent<CanvasGroup>();   // raycast 무시(HUD는 표시만)
        hudCG.interactable = false; hudCG.blocksRaycasts = false;

        var timerLabel = MakeText(hudRoot.transform, "TimerLabel", font, 64, TextAlignmentOptions.Center);
        AnchorTop(timerLabel.rectTransform, 0f, -70f, 400f, 90f);
        timerLabel.text = "15:00";

        var harvestLabel = MakeText(hudRoot.transform, "HarvestLabel", font, 34, TextAlignmentOptions.Left);
        AnchorTopLeft(harvestLabel.rectTransform, 40f, -150f, 500f, 50f);
        harvestLabel.text = "생체 strain: 0";

        var extractionLabel = MakeText(hudRoot.transform, "ExtractionLabel", font, 40, TextAlignmentOptions.Center);
        AnchorTop(extractionLabel.rectTransform, 0f, -170f, 700f, 60f);
        extractionLabel.color = new Color(0.35f, 0.85f, 0.95f);
        extractionLabel.text = "회수 헬기 도착까지 N초";
        extractionLabel.gameObject.SetActive(false);

        // ── 사무실 패널(풀스크린) ──
        var officePanel = NewUIChild(root.transform, "OfficePanel");
        Stretch(officePanel);
        AddPanelBg(officePanel, new Color(0.05f, 0.07f, 0.08f, 0.97f));

        var officeTitle = MakeText(officePanel.transform, "OfficeTitle", font, 56, TextAlignmentOptions.Center);
        AnchorTop(officeTitle.rectTransform, 0f, -120f, 900f, 80f);
        officeTitle.text = "현장 사무소 — 재출동 승인";

        var walletLabel = MakeText(officePanel.transform, "WalletLabel", font, 40, TextAlignmentOptions.Center);
        AnchorTop(walletLabel.rectTransform, 0f, -240f, 900f, 60f);
        walletLabel.text = "보유 strain — 생체 1성: 0";

        var dmgBtn = MakeButton(officePanel.transform, "DamageBuyButton", font, out var dmgLabel);
        AnchorCenter(dmgBtn.GetComponent<RectTransform>(), 0f, 60f, 720f, 90f);
        dmgLabel.text = "사격 피해  Lv.0 → 1   비용 3";

        var frBtn = MakeButton(officePanel.transform, "FireRateBuyButton", font, out var frLabel);
        AnchorCenter(frBtn.GetComponent<RectTransform>(), 0f, -50f, 720f, 90f);
        frLabel.text = "연사 속도  Lv.0 → 1   비용 3";

        var deployBtn = MakeButton(officePanel.transform, "DeployButton", font, out var deployLabel);
        AnchorCenter(deployBtn.GetComponent<RectTransform>(), 0f, -200f, 480f, 110f);
        deployLabel.text = "출격";
        deployLabel.fontSize = 48;
        SetButtonColor(deployBtn, new Color(0.12f, 0.45f, 0.5f));

        // ── 정산 패널(풀스크린) ──
        var settlementPanel = NewUIChild(root.transform, "SettlementPanel");
        Stretch(settlementPanel);
        AddPanelBg(settlementPanel, new Color(0.04f, 0.05f, 0.06f, 0.98f));

        var settleTitle = MakeText(settlementPanel.transform, "SettlementTitle", font, 56, TextAlignmentOptions.Center);
        AnchorTop(settleTitle.rectTransform, 0f, -140f, 1100f, 90f);
        settleTitle.text = "정산 보고서";

        var settleBody = MakeText(settlementPanel.transform, "SettlementBody", font, 38, TextAlignmentOptions.Center);
        AnchorCenter(settleBody.rectTransform, 0f, 40f, 1000f, 360f);
        settleBody.text = "";

        var returnBtn = MakeButton(settlementPanel.transform, "ReturnButton", font, out var returnLabel);
        AnchorCenter(returnBtn.GetComponent<RectTransform>(), 0f, -260f, 520f, 110f);
        returnLabel.text = "사무실로 (재출동 승인)";
        returnLabel.fontSize = 38;
        SetButtonColor(returnBtn, new Color(0.4f, 0.18f, 0.14f));

        // 시작 표시 상태: 사무실만 켬(RunManager.Start가 Office 진입 시 다시 동기화).
        officePanel.SetActive(true);
        settlementPanel.SetActive(false);
        hudRoot.SetActive(false);

        // ── RunLoopUI 참조 연결 ──
        SetField(ui, "hudRoot", hudRoot);
        SetField(ui, "timerLabel", timerLabel);
        SetField(ui, "harvestLabel", harvestLabel);
        SetField(ui, "extractionLabel", extractionLabel);
        SetField(ui, "officePanel", officePanel);
        SetField(ui, "walletLabel", walletLabel);
        SetField(ui, "deployButton", deployBtn);
        SetField(ui, "damageBuyButton", dmgBtn);
        SetField(ui, "damageBuyLabel", dmgLabel);
        SetField(ui, "fireRateBuyButton", frBtn);
        SetField(ui, "fireRateBuyLabel", frLabel);
        SetField(ui, "settlementPanel", settlementPanel);
        SetField(ui, "settlementTitle", settleTitle);
        SetField(ui, "settlementBody", settleBody);
        SetField(ui, "returnButton", returnBtn);

        EditorUtility.SetDirty(root);
        return root;
    }

    // ──────────────────────────────────────────── UI helpers

    static GameObject NewUIChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void AddPanelBg(GameObject panel, Color color)
    {
        var img = panel.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;   // 패널 뒤 클릭 차단
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var go = NewUIChild(parent, name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = new Color(0.88f, 0.96f, 0.97f);
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name, TMP_FontAsset font, out TextMeshProUGUI label)
    {
        var go = NewUIChild(parent, name);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.17f, 0.19f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        label = MakeText(go.transform, "Label", font, 36, TextAlignmentOptions.Center);
        Stretch(label.gameObject);
        return btn;
    }

    static void SetButtonColor(Button btn, Color c)
    {
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    static void AnchorTop(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void AnchorTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void AnchorCenter(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // ──────────────────────────────────────────── generic helpers

    static T Load<T>(string name) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(DataDir + name + ".asset");

    static GameObject GetOrCreate(string name, Transform parent)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
        }
        return go;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    /// <summary>SerializeField/private 필드에 SerializedObject로 안전하게 주입(Inspector 연결 보존).</summary>
    static void SetField(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogWarning("[RunLoopSetup] field not found: " + fieldName + " on " + target.GetType().Name);
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
