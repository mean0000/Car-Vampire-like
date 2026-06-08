using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Greybox_ScanLit 씬에 목업(_mockups/ui_mockup.html "02 · IN-GAME HUD")을 UGUI로 빌드한다.
/// 메뉴: Tools > ZombieCrush > Build In-Game HUD
///
/// - 기존 "HUD Canvas"(ScreenSpaceOverlay)를 재사용하고 그 아래 "HUD_V2" 루트를 만든다.
/// - 재실행 시 기존 "HUD_V2"를 먼저 지워 중복 적층을 막는다(idempotent).
/// - 레거시 텍스트 HUD(XPBarUI/SyncRateUI/HUDController/DashChargesUI)는 SetActive(false)로 숨긴다(삭제 X).
/// - LevelUpPanel / ChestRewardPanel 등 게임플레이 패널은 손대지 않는다.
/// MCP는 씬 저장 다이얼로그에 막히므로 에디터 메뉴로 1회 실행한다.
/// </summary>
public static class HudScreenBuilder
{
    const string ScenePath = "Assets/_Project/Scenes/Greybox_ScanLit.unity";
    const string FontDir = "Assets/_Project/Font";
    const string RootName = "HUD_V2";

    // 목업(_mockups/ui_mockup.html)은 .wrap max-width 980px의 .hud-stage 기준으로 그려졌다.
    // 실제 캔버스는 1920 폭(ScaleWithScreenSize, match=width)이므로 모든 px를 1920/980≈1.95배 키운다.
    // 아래 빌더의 숫자 리터럴은 "목업 px"이며 V()/F() 헬퍼가 S배 스케일한다.
    const float S = 1.95f;
    static Vector2 V(float x, float y) => new Vector2(x * S, y * S);
    static float F(float v) => v * S;

    // 화면 가장자리 통일 거터 (목업 px 기준 16px → 실제 31.2px).
    // 모든 코너 패널의 anchoredPosition에 이 값을 사용한다.
    const float MARGIN = 16f;

    // 흰 사각 스프라이트 에셋 경로 (에디터 빌드 시 생성, 씬 재로드 후에도 유효).
    const string WhiteSquarePath = "Assets/_Project/Art/UI/white_square.png";

    static TMP_FontAsset _display, _body, _mono;

    // 팔레트 (목업 :root)
    static readonly Color CYAN      = Hex("#34E3FF");
    static readonly Color CYAND     = Hex("#0FA8C4");
    static readonly Color AMBER     = Hex("#FFB02E");
    static readonly Color RED       = Hex("#FF4242");
    static readonly Color WHITE     = Hex("#EAF6F8");
    static readonly Color TEXT      = Hex("#CDD9DC");
    static readonly Color MUTED     = Hex("#6F8388");
    static readonly Color PANEL     = new Color(16f/255f, 28f/255f, 31f/255f, 0.86f);
    static readonly Color PANELLINE = Hex("#274247");
    static readonly Color XPTRACK   = new Color(12f/255f, 23f/255f, 26f/255f, 1f); // #0c171a
    static readonly Color BARTRACK  = new Color(21f/255f, 38f/255f, 43f/255f, 1f); // #15262b
    static readonly Color PIP_OFF   = new Color(0.08f, 0.16f, 0.18f, 1f);

    static Sprite _whiteSprite;

    [MenuItem("Tools/ZombieCrush/Build In-Game HUD")]
    public static void Build()
    {
        _display = Load("BlackHanSans Dynamic SDF");
        _body    = Load("Pretendard-Regular Dynamic SDF");
        _mono    = Load("Galmuri11 Dynamic SDF");
        if (_display == null || _body == null || _mono == null)
        {
            Debug.LogError("[HudBuilder] 폰트 에셋 누락 — 먼저 Tools > ZombieCrush > Generate Dynamic Fonts 실행.");
            return;
        }

        // 씬이 이미 열려 있지 않으면 연다.
        var active = EditorSceneManager.GetActiveScene();
        if (active.path != ScenePath)
            active = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGO = FindHudCanvas();
        if (canvasGO == null)
        {
            Debug.LogError("[HudBuilder] 'HUD Canvas'를 찾지 못했습니다. 씬에 Canvas가 있는지 확인하세요.");
            return;
        }

        // 단색 직각 사각 스프라이트 — 프로젝트에 실제 직렬화된 PNG 에셋을 사용한다.
        // UISprite.psd(빌트인 9-slice 둥근 스프라이트)를 쓰면 짧은 바가 타원으로 보이므로 금지.
        _whiteSprite = EnsureWhiteSquareSprite();
        if (_whiteSprite == null)
        {
            // 스프라이트가 null이면 모든 Image가 null 스프라이트로 빌드되어 타원 버그가 되살아난다.
            // 씬을 건드리기 전에 중단(삭제/저장 안 함).
            Debug.LogError("[HudBuilder] 흰 사각 스프라이트 생성 실패 — 빌드 중단(씬 미변경). 한 번 더 실행하면 정착될 수 있음.");
            return;
        }

        // 기존 HUD_V2 제거(idempotent).
        var prev = canvasGO.transform.Find(RootName);
        if (prev != null) Object.DestroyImmediate(prev.gameObject);

        // 레거시 HUD 숨김(삭제 X).
        DisableLegacyHud();

        // RunStats 백엔드 보장(런 타이머/킬 카운터) — 씬에 없으면 GameManager(또는 신규 GO)에 부착.
        EnsureRunStats();

        // ── HUD_V2 루트(캔버스 전체 stretch) ──
        var root = NewRect(RootName, canvasGO.transform);
        Stretch(root);
        var ctrl = root.gameObject.AddComponent<HudV2Controller>();

        BuildVignette(root, ctrl);
        BuildXP(root, ctrl);
        BuildSync(root, ctrl);
        BuildTopCenter(root, ctrl);
        BuildEvents(root, ctrl);
        BuildPlayerCard(root, ctrl);
        BuildWeaponCorner(root, ctrl);
        BuildCenterAnchor(root, ctrl);
        BuildPrompt(root, ctrl);
        BuildPartsToast(root, ctrl);
        BuildScanlines(root, ctrl);

        // 비네트는 SYNC 핸들러가 구동 — SyncRateUI 레거시와 중복되지 않도록 위에서 비활성화함.
        ctrl.vignette = _vignetteRef;

        EditorUtility.SetDirty(canvasGO);
        VerifyRefs(ctrl);
        EditorSceneManager.MarkSceneDirty(active);
        EditorSceneManager.SaveScene(active);
        Debug.Log("[HudBuilder] In-Game HUD 빌드 완료 + 씬 저장됨 — " + ScenePath);
    }

    // ───────────────────── 위젯 빌더 ─────────────────────

    static Image _vignetteRef;
    static void BuildVignette(RectTransform parent, HudV2Controller ctrl)
    {
        // 화면 가장자리 어두운 비네트 — 라디얼 스프라이트는 컨트롤러가 런타임에 생성/주입한다
        // (에디터 베이크 텍스처는 씬 재로드 시 null이 되므로 여기선 굽지 않음).
        var img = NewImage("Vignette", parent, Color.clear);
        Stretch(img.rectTransform);
        img.raycastTarget = false;
        img.color = new Color(0.7f, 0f, 0f, 0f);   // 초기 alpha 0(SYNC<0.7)
        img.transform.SetAsFirstSibling();          // 다른 위젯 뒤에 깔림
        _vignetteRef = img;
    }

    static void BuildXP(RectTransform parent, HudV2Controller ctrl)
    {
        // 상단 풀폭 5px 바 (목업 .xp)
        var track = NewImage("XP_Track", parent, XPTRACK);
        track.raycastTarget = false;
        // overflow:hidden — XP 그라디언트 fill이 트랙 밖으로 나가지 않도록
        track.gameObject.AddComponent<RectMask2D>();
        var t = track.rectTransform;
        t.anchorMin = new Vector2(0, 1); t.anchorMax = new Vector2(1, 1);
        t.pivot = new Vector2(0.5f, 1f);
        t.offsetMin = V(0, -5); t.offsetMax = new Vector2(0, 0);

        var fill = NewImage("XP_Fill", track.rectTransform, CYAN);
        fill.raycastTarget = false;
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;
        ctrl.xpFill = fill;

        // 좌상단 Lv.N 라벨(목업엔 별도 없지만 플레이어 카드와 별개로 작은 라벨 — XP 바 바로 아래 좌측)
        var lvl = MakeTMP("XP_Level", parent, _mono, 13f, CYAN, TextAlignmentOptions.TopLeft, "Lv.1");
        var lr = lvl.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0, 1);
        lr.pivot = new Vector2(0, 1);
        lr.sizeDelta = V(120, 22);
        lr.anchoredPosition = V(MARGIN, -8);
        ctrl.levelLabel = lvl;
    }

    static void BuildSync(RectTransform parent, HudV2Controller ctrl)
    {
        // 좌상단 SYNC RATE 패널 (목업 .sync) — width 230
        var panel = MakePanel("Sync_Panel", parent, V(230, 82), out Image bg);
        panel.anchorMin = panel.anchorMax = new Vector2(0, 1);
        panel.pivot = new Vector2(0, 1);
        panel.anchoredPosition = V(MARGIN, -MARGIN);
        ctrl.syncPanel = panel;
        ctrl.syncPanelBg = bg;

        // row: 라벨(왼) + % (오)
        // 목업 .sync .lbl small — "SYNC RATE" 옆에 작은 muted 부제 "동기화율".
        // 인라인 <color>는 컨트롤러가 tier마다 바꾸는 base .color에 덮이지 않으므로 부제는 항상 muted 유지.
        var lbl = MakeTMP("Sync_Label", panel, _body, 13f, CYAN, TextAlignmentOptions.TopLeft,
            "SYNC RATE<size=77%> <color=#6F8388>동기화율</color></size>");
        lbl.fontStyle = FontStyles.Bold;
        var lr = lbl.rectTransform;
        lr.anchorMin = new Vector2(0, 1); lr.anchorMax = new Vector2(0, 1); lr.pivot = new Vector2(0, 1);
        lr.anchoredPosition = V(12, -10); lr.sizeDelta = V(180, 20);
        ctrl.syncLabel = lbl;

        var pct = MakeTMP("Sync_Pct", panel, _mono, 20f, CYAN, TextAlignmentOptions.TopRight, "0%");
        var pr = pct.rectTransform;
        pr.anchorMin = new Vector2(1, 1); pr.anchorMax = new Vector2(1, 1); pr.pivot = new Vector2(1, 1);
        pr.anchoredPosition = V(-12, -8); pr.sizeDelta = V(90, 26);
        ctrl.syncPct = pct;

        // bar (목업 .bar height 9)
        var barTrack = NewImage("Sync_BarTrack", panel, BARTRACK);
        barTrack.raycastTarget = false;
        var bt = barTrack.rectTransform;
        bt.anchorMin = new Vector2(0, 1); bt.anchorMax = new Vector2(1, 1); bt.pivot = new Vector2(0.5f, 1f);
        bt.offsetMin = V(12, 0); bt.offsetMax = V(-12, 0);
        bt.sizeDelta = new Vector2(bt.sizeDelta.x, F(9));
        bt.anchoredPosition = V(0, -40);
        // ⚠️ 외곽선 금지: RectMask2D는 자식만 클리핑하고 트랙 자신의 Outline 메시(±2px)는
        // 마스크 밖으로 새어 흐릿해진다. 평평한 직각 바(Hades/VS 레퍼런스)로 간다 — 트랙색+필+틱으로 정의.
        // overflow:hidden 근사 — fill이 트랙 밖으로 나가지 않도록
        barTrack.gameObject.AddComponent<RectMask2D>();

        var barFill = NewImage("Sync_BarFill", barTrack.rectTransform, CYAN);
        barFill.raycastTarget = false;
        Stretch(barFill.rectTransform);
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 0f;
        ctrl.syncBarFill = barFill;

        // 세그먼트 틱 3개 (33%, 66%) — SYNC는 3단계(30/60/90) 기준이므로 3분할
        AddSegmentTicks(barTrack.rectTransform, 3, new Color(1f, 1f, 1f, 0.18f));

        var state = MakeTMP("Sync_State", panel, _mono, 10f, MUTED, TextAlignmentOptions.TopLeft, "상태: 안정 / STABLE");
        var sr = state.rectTransform;
        sr.anchorMin = new Vector2(0, 1); sr.anchorMax = new Vector2(1, 1); sr.pivot = new Vector2(0, 1);
        sr.anchoredPosition = V(12, -56); sr.sizeDelta = V(206, 16);
        ctrl.syncState = state;
    }

    static void BuildTopCenter(RectTransform parent, HudV2Controller ctrl)
    {
        // 상단 중앙 클러스터 (목업 .hud-top)
        var cluster = NewRect("TopCenter", parent);
        cluster.anchorMin = cluster.anchorMax = new Vector2(0.5f, 1f);
        cluster.pivot = new Vector2(0.5f, 1f);
        cluster.anchoredPosition = V(0, -MARGIN);
        cluster.sizeDelta = V(360, 120);

        var time = MakeTMP("Time", cluster, _mono, 24f, WHITE, TextAlignmentOptions.Top, "00:00");
        var tr = time.rectTransform;
        tr.anchorMin = new Vector2(0.5f, 1); tr.anchorMax = new Vector2(0.5f, 1); tr.pivot = new Vector2(0.5f, 1);
        tr.anchoredPosition = new Vector2(0, 0); tr.sizeDelta = V(200, 30);
        ctrl.timeLabel = time;

        var kills = MakeTMP("Kills", cluster, _mono, 11f, MUTED, TextAlignmentOptions.Top, "REMOVED · 0000");
        var kr = kills.rectTransform;
        kr.anchorMin = new Vector2(0.5f, 1); kr.anchorMax = new Vector2(0.5f, 1); kr.pivot = new Vector2(0.5f, 1);
        kr.anchoredPosition = V(0, -30); kr.sizeDelta = V(240, 16);
        ctrl.killsLabel = kills;

        // 보스바 (목업 .boss) — START HIDDEN
        var bossPanel = MakePanel("Boss", cluster, V(340, 34), out _);
        var bp = bossPanel;
        bp.anchorMin = new Vector2(0.5f, 1); bp.anchorMax = new Vector2(0.5f, 1); bp.pivot = new Vector2(0.5f, 1);
        bp.anchoredPosition = V(0, -52);
        ctrl.bossRoot = bossPanel.gameObject;

        var bn = MakeTMP("Boss_Name", bossPanel, _mono, 10f, RED, TextAlignmentOptions.TopLeft, "변종 · ELITE");
        var bnr = bn.rectTransform;
        bnr.anchorMin = new Vector2(0, 1); bnr.anchorMax = new Vector2(0, 1); bnr.pivot = new Vector2(0, 1);
        bnr.anchoredPosition = V(10, -5); bnr.sizeDelta = V(240, 14);
        ctrl.bossName = bn;

        var bpct = MakeTMP("Boss_Pct", bossPanel, _mono, 10f, RED, TextAlignmentOptions.TopRight, "100%");
        var bpr = bpct.rectTransform;
        bpr.anchorMin = new Vector2(1, 1); bpr.anchorMax = new Vector2(1, 1); bpr.pivot = new Vector2(1, 1);
        bpr.anchoredPosition = V(-10, -5); bpr.sizeDelta = V(60, 14);
        ctrl.bossPct = bpct;

        var btrack = NewImage("Boss_BarTrack", bossPanel, BARTRACK);
        btrack.raycastTarget = false;
        // overflow:hidden 근사
        btrack.gameObject.AddComponent<RectMask2D>();
        var btr = btrack.rectTransform;
        btr.anchorMin = new Vector2(0, 0); btr.anchorMax = new Vector2(1, 0); btr.pivot = new Vector2(0.5f, 0);
        btr.offsetMin = V(10, 6); btr.offsetMax = V(-10, 6 + 7);
        var bfill = NewImage("Boss_BarFill", btrack.rectTransform, RED);
        bfill.raycastTarget = false;
        Stretch(bfill.rectTransform);
        bfill.type = Image.Type.Filled; bfill.fillMethod = Image.FillMethod.Horizontal;
        bfill.fillOrigin = (int)Image.OriginHorizontal.Left; bfill.fillAmount = 1f;
        ctrl.bossBarFill = bfill;

        bossPanel.gameObject.SetActive(false);   // START HIDDEN
    }

    static void BuildEvents(RectTransform parent, HudV2Controller ctrl)
    {
        // 우상단 이벤트 트래커 (목업 .events) width 222
        var panel = MakePanel("Events", parent, V(222, 116), out _);
        panel.anchorMin = panel.anchorMax = new Vector2(1, 1);
        panel.pivot = new Vector2(1, 1);
        panel.anchoredPosition = V(-MARGIN, -MARGIN);

        var eh = MakeTMP("Events_Header", panel, _mono, 10f, CYAND, TextAlignmentOptions.TopLeft, "EVENT TRACKER · 현장 이벤트");
        var ehr = eh.rectTransform;
        ehr.anchorMin = new Vector2(0, 1); ehr.anchorMax = new Vector2(1, 1); ehr.pivot = new Vector2(0, 1);
        ehr.anchoredPosition = V(12, -8); ehr.sizeDelta = V(200, 14);

        // 3행: warn(빨강) / gold(앰버) / info(시안)
        string[] labels = { "도시 붕괴 임박", "본사 보급 투하", "생존자 신호 포착" };
        Color[] dots = { RED, AMBER, CYAN };
        Color[] txts = { Hex("#F0C0C0"), Hex("#F0DCA0"), TEXT };
        for (int i = 0; i < 3; i++)
        {
            float y = -28 - i * 27;
            var dot = NewImage("Events_Dot" + i, panel, dots[i]);
            dot.raycastTarget = false;
            dot.type = Image.Type.Simple;   // 원형 Knob은 9-slice 부적합
            dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // 빌트인 원형 스프라이트
            var dr = dot.rectTransform;
            dr.anchorMin = new Vector2(0, 1); dr.anchorMax = new Vector2(0, 1); dr.pivot = new Vector2(0, 1);
            dr.anchoredPosition = V(12, y - 6); dr.sizeDelta = V(7, 7);

            var lab = MakeTMP("Events_Label" + i, panel, _body, 12f, txts[i], TextAlignmentOptions.TopLeft, labels[i]);
            var labr = lab.rectTransform;
            labr.anchorMin = new Vector2(0, 1); labr.anchorMax = new Vector2(0, 1); labr.pivot = new Vector2(0, 1);
            labr.anchoredPosition = V(26, y); labr.sizeDelta = V(150, 18);

            var tm = MakeTMP("Events_Timer" + i, panel, _mono, 11f, MUTED, TextAlignmentOptions.TopRight, "0:00");
            var tmr = tm.rectTransform;
            tmr.anchorMin = new Vector2(1, 1); tmr.anchorMax = new Vector2(1, 1); tmr.pivot = new Vector2(1, 1);
            tmr.anchoredPosition = V(-12, y); tmr.sizeDelta = V(50, 16);
            ctrl.eventTimers[i] = tm;

            // 목업 .events li border-bottom — 마지막 행 제외하고 얇은 구분선(panel-line 0.4α).
            if (i < 2)
            {
                var sep = NewImage("Events_Sep" + i, panel, new Color(39f/255f, 66f/255f, 71f/255f, 0.4f));
                sep.raycastTarget = false;
                sep.type = Image.Type.Simple;
                var spr = sep.rectTransform;
                spr.anchorMin = new Vector2(0, 1); spr.anchorMax = new Vector2(1, 1); spr.pivot = new Vector2(0.5f, 1);
                spr.offsetMin = V(12, 0); spr.offsetMax = V(-12, 0);
                spr.sizeDelta = new Vector2(spr.sizeDelta.x, F(1));
                spr.anchoredPosition = V(0, y - 13.5f);
            }
        }
    }

    static void BuildPlayerCard(RectTransform parent, HudV2Controller ctrl)
    {
        // 하단 좌 (목업 .pcard) — 초상화 + 이름 + HP + 버프
        var card = NewRect("PlayerCard", parent);
        card.anchorMin = card.anchorMax = new Vector2(0, 0);
        card.pivot = new Vector2(0, 0);
        card.anchoredPosition = V(MARGIN, MARGIN);
        card.sizeDelta = V(260, 80);

        // 초상화 58x58
        var portrait = NewImage("Portrait", card, XPTRACK);
        portrait.raycastTarget = false;
        Outline1px(portrait.gameObject, PANELLINE);
        var ppr = portrait.rectTransform;
        ppr.anchorMin = new Vector2(0, 0); ppr.anchorMax = new Vector2(0, 0); ppr.pivot = new Vector2(0, 0);
        ppr.anchoredPosition = new Vector2(0, 0); ppr.sizeDelta = V(58, 58);
        var pTxt = MakeTMP("Portrait_Txt", portrait.rectTransform, _mono, 9f, MUTED, TextAlignmentOptions.Center, "초상화");
        Stretch(pTxt.rectTransform);

        // 이름 + 캡션 + HP + 버프
        float infoX = 68;
        var nm = MakeTMP("Name", card, _body, 14f, WHITE, TextAlignmentOptions.BottomLeft, "김도현");
        nm.fontStyle = FontStyles.Bold;
        var nmr = nm.rectTransform;
        nmr.anchorMin = new Vector2(0, 0); nmr.anchorMax = new Vector2(0, 0); nmr.pivot = new Vector2(0, 0);
        nmr.anchoredPosition = V(infoX, 58); nmr.sizeDelta = V(120, 18);
        ctrl.playerName = nm;

        var sub = MakeTMP("Sub", card, _mono, 9f, MUTED, TextAlignmentOptions.BottomLeft, "현장요원 · LV.1");
        var subr = sub.rectTransform;
        subr.anchorMin = new Vector2(0, 0); subr.anchorMax = new Vector2(0, 0); subr.pivot = new Vector2(0, 0);
        subr.anchoredPosition = V(infoX, 44); subr.sizeDelta = V(140, 14);
        ctrl.playerSub = sub;

        var cap = MakeTMP("HP_Cap", card, _mono, 9f, MUTED, TextAlignmentOptions.BottomLeft, "[HP] 체력");
        var capr = cap.rectTransform;
        capr.anchorMin = new Vector2(0, 0); capr.anchorMax = new Vector2(0, 0); capr.pivot = new Vector2(0, 0);
        capr.anchoredPosition = V(infoX, 30); capr.sizeDelta = V(120, 12);

        var hpTrack = NewImage("HP_Track", card, BARTRACK);
        hpTrack.raycastTarget = false;
        // ⚠️ 외곽선 금지(SYNC 트랙과 동일 이유): RectMask2D가 자신의 Outline 메시를 못 잘라 ±2px 번짐.
        // overflow:hidden 근사
        hpTrack.gameObject.AddComponent<RectMask2D>();
        var hptr = hpTrack.rectTransform;
        hptr.anchorMin = new Vector2(0, 0); hptr.anchorMax = new Vector2(0, 0); hptr.pivot = new Vector2(0, 0);
        hptr.anchoredPosition = V(infoX, 18); hptr.sizeDelta = V(180, 9);
        var hpFill = NewImage("HP_Fill", hpTrack.rectTransform, Hex("#3AD07A"));
        hpFill.raycastTarget = false;
        Stretch(hpFill.rectTransform);
        hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left; hpFill.fillAmount = 1f;
        ctrl.hpFill = hpFill;

        // 세그먼트 틱 4개 (25% 간격) — HP는 4분할(25/50/75)
        AddSegmentTicks(hpTrack.rectTransform, 4, new Color(1f, 1f, 1f, 0.18f));

        // 버프 3칸 22x22
        string[] bl = { "SPD", "ARM", "DoT" };
        for (int i = 0; i < 3; i++)
        {
            var bx = NewImage("Buff_" + bl[i], card, XPTRACK);
            bx.raycastTarget = false;
            Outline1px(bx.gameObject, PANELLINE);
            var bxr = bx.rectTransform;
            bxr.anchorMin = new Vector2(0, 0); bxr.anchorMax = new Vector2(0, 0); bxr.pivot = new Vector2(0, 0);
            bxr.anchoredPosition = V(infoX + i * 27, 0); bxr.sizeDelta = V(22, 22);
            var bt = MakeTMP("Buff_Txt", bx.rectTransform, _mono, 9f, Color.white, TextAlignmentOptions.Center, bl[i]);
            Stretch(bt.rectTransform);
            // 아이콘 점등은 컨트롤러가 bx(Image) color로 구동.
            bx.color = new Color(1f, 1f, 1f, 0.18f);
            if (i == 0) ctrl.buffSpd = bx;
            else if (i == 1) ctrl.buffArm = bx;
            else ctrl.buffDot = bx;
        }
    }

    static void BuildWeaponCorner(RectTransform parent, HudV2Controller ctrl)
    {
        // 하단 우 (목업 .weapon) — 대시 핍(왼) + 무기 박스(오)
        var wrap = NewRect("WeaponCorner", parent);
        wrap.anchorMin = wrap.anchorMax = new Vector2(1, 0);
        wrap.pivot = new Vector2(1, 0);
        wrap.anchoredPosition = V(-MARGIN, MARGIN);
        wrap.sizeDelta = V(230, 80);

        // 무기 박스 (목업 .wbox) — 우측
        var wbox = MakePanel("WeaponBox", wrap, V(160, 72), out _);
        wbox.anchorMin = new Vector2(1, 0); wbox.anchorMax = new Vector2(1, 0); wbox.pivot = new Vector2(1, 0);
        wbox.anchoredPosition = new Vector2(0, 0);

        var wi = NewImage("Weapon_Icon", wbox, Color.clear);
        wi.raycastTarget = false;
        Outline1px(wi.gameObject, PANELLINE);
        var wir = wi.rectTransform;
        wir.anchorMin = new Vector2(0, 1); wir.anchorMax = new Vector2(0, 1); wir.pivot = new Vector2(0, 1);
        wir.anchoredPosition = V(10, -9); wir.sizeDelta = V(30, 30);
        var wiTxt = MakeTMP("Weapon_IconTxt", wi.rectTransform, _mono, 10f, AMBER, TextAlignmentOptions.Center, "REV");
        Stretch(wiTxt.rectTransform);
        ctrl.weaponIcon = wiTxt;

        var wn = MakeTMP("Weapon_Name", wbox, _body, 13f, WHITE, TextAlignmentOptions.TopLeft, "리볼버");
        wn.fontStyle = FontStyles.Bold;
        var wnr = wn.rectTransform;
        wnr.anchorMin = new Vector2(0, 1); wnr.anchorMax = new Vector2(1, 1); wnr.pivot = new Vector2(0, 1);
        wnr.anchoredPosition = V(48, -8); wnr.sizeDelta = V(100, 16);
        ctrl.weaponName = wn;

        // 목업 .wbox .wn small — 무기 이름 아래 작은 muted 모드라인(코스메틱 플레이스홀더).
        var wsub = MakeTMP("Weapon_Sub", wbox, _mono, 9f, MUTED, TextAlignmentOptions.TopLeft, "OVERDRIVE");
        var wsr = wsub.rectTransform;
        wsr.anchorMin = new Vector2(0, 1); wsr.anchorMax = new Vector2(1, 1); wsr.pivot = new Vector2(0, 1);
        wsr.anchoredPosition = V(48, -24); wsr.sizeDelta = V(100, 12);

        var ammo = MakeTMP("Weapon_Ammo", wbox, _mono, 22f, WHITE, TextAlignmentOptions.BottomLeft, "6 / 6");
        var ar = ammo.rectTransform;
        ar.anchorMin = new Vector2(0, 0); ar.anchorMax = new Vector2(1, 0); ar.pivot = new Vector2(0, 0);
        ar.anchoredPosition = V(12, 8); ar.sizeDelta = V(140, 26);
        ctrl.ammoLabel = ammo;

        // 대시 핍 (목업 .dash) — 왼쪽, 무기 박스 왼편
        var dash = NewRect("Dash", wrap);
        dash.anchorMin = new Vector2(1, 0); dash.anchorMax = new Vector2(1, 0); dash.pivot = new Vector2(1, 0);
        dash.anchoredPosition = V(-168, 0); dash.sizeDelta = V(56, 72);

        var pipsRow = NewRect("Dash_Pips", dash);
        pipsRow.anchorMin = new Vector2(0.5f, 1); pipsRow.anchorMax = new Vector2(0.5f, 1); pipsRow.pivot = new Vector2(0.5f, 1);
        pipsRow.anchoredPosition = V(0, -8); pipsRow.sizeDelta = V(50, 18);
        ctrl.dashPips = BuildPipRow(pipsRow, 3, 9, 18, 4, true);

        var dlab = MakeTMP("Dash_Label", dash, _mono, 9f, MUTED, TextAlignmentOptions.Top, "DASH");
        var dlr = dlab.rectTransform;
        dlr.anchorMin = new Vector2(0.5f, 1); dlr.anchorMax = new Vector2(0.5f, 1); dlr.pivot = new Vector2(0.5f, 1);
        dlr.anchoredPosition = V(0, -30); dlr.sizeDelta = V(56, 14);
    }

    static void BuildCenterAnchor(RectTransform parent, HudV2Controller ctrl)
    {
        // 중앙 플레이어 추종 루트 (목업 .player) — 탄약 미니(머리 위) + 대시 미니(발 밑).
        // 컨트롤러가 매 프레임 플레이어 스크린좌표로 anchoredPosition을 갱신한다.
        var follow = NewRect("CenterFollow", parent);
        follow.anchorMin = follow.anchorMax = new Vector2(0.5f, 0.5f);
        follow.pivot = new Vector2(0.5f, 0.5f);
        follow.anchoredPosition = Vector2.zero;
        follow.sizeDelta = V(80, 60);
        ctrl.centerFollowRoot = follow;

        // 탄약 미니 — 머리 위. ⚠️ 아래 Y(68/-56)는 첫 프레임 후 컨트롤러 headOffset/feetOffset이
        // 매 프레임 덮어쓴다(런타임 권위). 런타임 위치 조정은 HudV2Controller 인스펙터에서 할 것.
        var ammoMini = MakeTMP("AmmoMini", follow, _mono, 13f, WHITE, TextAlignmentOptions.Center, "6 / 6");
        var amr = ammoMini.rectTransform;
        amr.anchorMin = new Vector2(0.5f, 0.5f); amr.anchorMax = new Vector2(0.5f, 0.5f); amr.pivot = new Vector2(0.5f, 0.5f);
        amr.anchoredPosition = new Vector2(0, 68f); amr.sizeDelta = V(80, 18);
        ctrl.ammoMini = ammoMini;

        // 대시 미니 행 — 발 밑(Y 오프셋은 컨트롤러 feetOffset이 매 프레임 구동).
        var miniRow = NewRect("DashMini", follow);
        miniRow.anchorMin = new Vector2(0.5f, 0.5f); miniRow.anchorMax = new Vector2(0.5f, 0.5f); miniRow.pivot = new Vector2(0.5f, 0.5f);
        miniRow.anchoredPosition = new Vector2(0, -56f); miniRow.sizeDelta = V(50, 6);
        ctrl.dashMiniPips = BuildPipRow(miniRow, 3, 9, 5, 4, true);
    }

    static void BuildPrompt(RectTransform parent, HudV2Controller ctrl)
    {
        // 중앙 하단 상호작용 프롬프트 (목업 .prompt) — START HIDDEN
        var panel = NewRect("Prompt", parent);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(10f/255f, 20f/255f, 23f/255f, 0.82f);
        bg.raycastTarget = false;
        Outline1px(panel.gameObject, PANELLINE);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0);
        panel.pivot = new Vector2(0.5f, 0);
        panel.anchoredPosition = V(0, 104);
        panel.sizeDelta = V(280, 34);

        var key = NewImage("Prompt_Key", panel, CYAN);
        key.raycastTarget = false;
        var kr = key.rectTransform;
        kr.anchorMin = new Vector2(0, 0.5f); kr.anchorMax = new Vector2(0, 0.5f); kr.pivot = new Vector2(0, 0.5f);
        kr.anchoredPosition = V(8, 0); kr.sizeDelta = V(24, 20);
        var keyTxt = MakeTMP("Prompt_KeyTxt", key.rectTransform, _mono, 11f, Hex("#0A1316"), TextAlignmentOptions.Center, "E");
        keyTxt.fontStyle = FontStyles.Bold;
        Stretch(keyTxt.rectTransform);

        var lab = MakeTMP("Prompt_Label", panel, _body, 13f, TEXT, TextAlignmentOptions.Left, "보급 상자 개봉");
        var lr = lab.rectTransform;
        lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(0, 0.5f);
        lr.offsetMin = V(40, 0); lr.offsetMax = V(-8, 0);
        ctrl.promptLabel = lab;

        ctrl.promptRoot = panel.gameObject;
        panel.gameObject.SetActive(false);   // START HIDDEN
    }

    static void BuildPartsToast(RectTransform parent, HudV2Controller ctrl)
    {
        // 부품 토스트 (목업 .toast) — 중앙 약간 우상단에서 떠오름
        var toast = NewRect("PartsToast", parent);
        toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 0.5f);
        toast.pivot = new Vector2(0.5f, 0.5f);
        toast.anchoredPosition = V(60, 20);
        toast.sizeDelta = V(200, 24);
        var cg = toast.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

        var lab = MakeTMP("PartsToast_Label", toast, _mono, 13f, AMBER, TextAlignmentOptions.Center, "[ PART ] +1 회수");
        Stretch(lab.rectTransform);

        ctrl.partsToastGroup = cg;
        ctrl.partsToastLabel = lab;
        ctrl.partsToastRect = toast;
    }

    static void BuildScanlines(RectTransform parent, HudV2Controller ctrl)
    {
        // 최상단 스캔라인 오버레이 — 텍스처는 컨트롤러가 런타임에 생성/주입한다
        // (에디터 베이크 텍스처는 씬 재로드 시 null이 되므로 여기선 굽지 않음).
        var rt = NewRect("Scanlines", parent);
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        Stretch(rt);
        rt.SetAsLastSibling();
        ctrl.scanlines = raw;
        rt.gameObject.SetActive(false); // CRT 스캔라인 비활성(화면 뿌연 질감 제거). 켜려면 GameObject 활성화.
    }

    // ───────────────────── 핍 행 빌더 ─────────────────────
    static Image[] BuildPipRow(RectTransform row, int count, float w, float h, float gap, bool skew)
    {
        w *= S; h *= S; gap *= S;
        var pips = new Image[count];
        float total = count * w + (count - 1) * gap;
        float startX = -total / 2f + w / 2f;
        for (int i = 0; i < count; i++)
        {
            var pip = NewImage("Pip" + i, row, PIP_OFF);
            pip.raycastTarget = false;
            var pr = pip.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(w, h);
            pr.anchoredPosition = new Vector2(startX + i * (w + gap), 0);
            if (skew) pip.transform.localRotation = Quaternion.Euler(0, 0, 0); // skew는 UGUI 미지원 — 직립으로 근사
            pips[i] = pip;
        }
        return pips;
    }

    // ───────────────────── 레거시 HUD 비활성 ─────────────────────
    static void DisableLegacyHud()
    {
        DisableAll<XPBarUI>();
        DisableAll<SyncRateUI>();
        DisableAll<HUDController>();
        DisableAll<DashChargesUI>();

        // 목업에 없는 잔여 레거시 오브젝트(이름 지정) 비활성화 — 새 HUD와 시각 겹침 방지.
        // NoiseBar: 소음 미터(목업에 없음). TimerText: 새 HUD Time 라벨로 대체. StealthLabel: 스텔스 폐기 잔재.
        // 모두 Canvas 루트도, 게임플레이 패널(LevelUpPanel/ChestRewardPanel)도 아니므로 SetActive(false) 안전.
        DisableByName("NoiseBar");
        DisableByName("TimerText");
        DisableByName("StealthLabel");
    }

    static void DisableByName(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) return;
        // 게임플레이 패널은 절대 끄지 않도록 가드(이름이 우연히 겹쳐도 안전).
        if (go.name == "LevelUpPanel" || go.name == "ChestRewardPanel" || go.GetComponent<Canvas>() != null) return;
        go.SetActive(false);
        Debug.Log($"[HudBuilder] 잔여 레거시 오브젝트 비활성화: '{goName}'");
    }

    static void EnsureRunStats()
    {
        if (Object.FindFirstObjectByType<RunStats>(FindObjectsInactive.Include) != null) return;

        var gm = GameObject.Find("GameManager");
        if (gm != null)
        {
            gm.AddComponent<RunStats>();
            Debug.Log("[HudBuilder] RunStats 부착: GameManager");
        }
        else
        {
            var go = new GameObject("RunStats");
            go.AddComponent<RunStats>();
            Debug.Log("[HudBuilder] RunStats GO 신규 생성.");
        }
    }

    static void DisableAll<T>() where T : Component
    {
        var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in found)
        {
            if (c == null) continue;

            // ★ 안전: 컴포넌트가 Canvas나 그 외 공유 GameObject(자식이 여럿)에 붙어 있으면
            // GameObject를 통째로 끄지 않는다(HUD Canvas를 끄면 새 HUD_V2/LevelUpPanel까지 사라짐).
            // 대신 컴포넌트만 비활성화하고, 그 컴포넌트가 참조하는 레거시 UI 자식 GameObject들만 끈다.
            bool onCanvas = c.GetComponent<Canvas>() != null;
            if (onCanvas)
            {
                var bh = c as MonoBehaviour;
                if (bh != null) bh.enabled = false;
                DeactivateReferencedUI(c);
                Debug.Log($"[HudBuilder] 레거시 HUD 컴포넌트 비활성화(Canvas 보존): {typeof(T).Name} on '{c.gameObject.name}'");
            }
            else
            {
                c.gameObject.SetActive(false);
                Debug.Log($"[HudBuilder] 레거시 HUD GameObject 비활성화: {typeof(T).Name} on '{c.gameObject.name}'");
            }
        }
    }

    /// <summary>컴포넌트가 직렬화로 참조하는 UI(Graphic/RectTransform) GameObject들을 SetActive(false). Canvas 자체는 제외.</summary>
    static void DeactivateReferencedUI(Component c)
    {
        var so = new SerializedObject(c);
        var p = so.GetIterator();
        bool enter = true;
        var seen = new HashSet<GameObject>();
        while (p.NextVisible(enter))
        {
            enter = false;
            if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
            var go = AsGameObject(p.objectReferenceValue);
            if (go == null || go == c.gameObject) continue;       // Canvas 자신은 건드리지 않음
            // 게임플레이 패널/Canvas는 절대 끄지 않도록 가드(DisableByName과 동일).
            if (go.name == "LevelUpPanel" || go.name == "ChestRewardPanel" || go.GetComponent<Canvas>() != null) continue;
            // 참조가 Graphic이면 그 행 컨테이너(부모)까지 끄면 위험 → 해당 GameObject만 끈다.
            if (seen.Add(go)) go.SetActive(false);
        }
    }

    static GameObject AsGameObject(Object o)
    {
        if (o is GameObject g) return g;
        if (o is Component cc) return cc.gameObject;
        return null;
    }

    // ───────────────────── 검증 ─────────────────────
    static void VerifyRefs(HudV2Controller c)
    {
        var nulls = new List<string>();
        void Chk(string n, Object o) { if (o == null) nulls.Add(n); }
        Chk("xpFill", c.xpFill); Chk("levelLabel", c.levelLabel);
        Chk("syncPanel", c.syncPanel); Chk("syncPanelBg", c.syncPanelBg);
        Chk("syncLabel", c.syncLabel); Chk("syncPct", c.syncPct);
        Chk("syncBarFill", c.syncBarFill); Chk("syncState", c.syncState);
        Chk("vignette", c.vignette); Chk("scanlines", c.scanlines);
        Chk("timeLabel", c.timeLabel); Chk("killsLabel", c.killsLabel);
        Chk("bossRoot", c.bossRoot); Chk("bossName", c.bossName);
        Chk("bossPct", c.bossPct); Chk("bossBarFill", c.bossBarFill);
        Chk("hpFill", c.hpFill); Chk("playerName", c.playerName); Chk("playerSub", c.playerSub);
        Chk("buffSpd", c.buffSpd); Chk("buffArm", c.buffArm); Chk("buffDot", c.buffDot);
        Chk("weaponName", c.weaponName); Chk("weaponIcon", c.weaponIcon);
        Chk("ammoLabel", c.ammoLabel); Chk("ammoMini", c.ammoMini);
        Chk("centerFollowRoot", c.centerFollowRoot);
        Chk("promptRoot", c.promptRoot); Chk("promptLabel", c.promptLabel);
        Chk("partsToastGroup", c.partsToastGroup); Chk("partsToastLabel", c.partsToastLabel);
        Chk("partsToastRect", c.partsToastRect);
        for (int i = 0; i < c.eventTimers.Length; i++) Chk("eventTimers[" + i + "]", c.eventTimers[i]);
        for (int i = 0; i < c.dashPips.Length; i++) Chk("dashPips[" + i + "]", c.dashPips[i]);
        for (int i = 0; i < c.dashMiniPips.Length; i++) Chk("dashMiniPips[" + i + "]", c.dashMiniPips[i]);

        if (nulls.Count == 0)
            Debug.Log("[HudBuilder] VERIFY OK — HudV2Controller 모든 직렬화 참조 연결됨.");
        else
            Debug.LogError("[HudBuilder] VERIFY FAIL — null 참조: " + string.Join(", ", nulls));
    }

    // ───────────────────── 헬퍼 ─────────────────────
    static GameObject FindHudCanvas()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cv in canvases)
            if (cv.gameObject.name == "HUD Canvas") return cv.gameObject;
        // 폴백: 첫 ScreenSpaceOverlay
        foreach (var cv in canvases)
            if (cv.renderMode == RenderMode.ScreenSpaceOverlay) return cv.gameObject;
        return null;
    }

    static TMP_FontAsset Load(string name) =>
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "/" + name + ".asset");

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
        // white_square.png는 border=0 순수 사각 스프라이트이므로 Simple이 정확한 직각을 보장한다.
        // Sliced를 쓰면 border 0이라도 내부적으로 9분할 처리되어 미세 라운딩이 발생할 수 있음.
        if (_whiteSprite != null)
        {
            img.sprite = _whiteSprite;
            img.type = Image.Type.Simple;
        }
        return img;
    }

    /// <summary>목업 .panel 근사: 배경(--panel) + 1px 외곽선(--panel-line) + 우상단 절단 강조선.</summary>
    static RectTransform MakePanel(string name, Transform parent, Vector2 size, out Image bg)
    {
        var rt = NewRect(name, parent);
        rt.sizeDelta = size;
        bg = rt.gameObject.AddComponent<Image>();
        bg.color = PANEL;
        bg.raycastTarget = false;
        if (_whiteSprite != null)
        {
            bg.sprite = _whiteSprite;
            bg.type = Image.Type.Simple;
        }
        Outline1px(rt.gameObject, PANELLINE);

        // 우상단 모서리 절단 강조선(목업 .panel::before) — 작은 회전 시안 라인 근사.
        var accent = NewImage(name + "_Accent", rt, CYAND);
        accent.raycastTarget = false;
        accent.type = Image.Type.Simple;   // 얇은 회전 라인은 9-slice 부적합
        var ar = accent.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(1, 1); ar.pivot = new Vector2(1, 1);
        ar.sizeDelta = V(20, 2);
        ar.anchoredPosition = V(-2, -2);
        accent.transform.localRotation = Quaternion.Euler(0, 0, 45f);
        return rt;
    }

    static void Outline1px(GameObject go, Color c)
    {
        var ol = go.GetComponent<Outline>();
        if (ol == null) ol = go.AddComponent<Outline>();
        ol.effectColor = c;
        ol.effectDistance = new Vector2(F(1), -F(1));
    }

    static TextMeshProUGUI MakeTMP(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align, string text)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size * S;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.richText = true;
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ───────────────────── 세그먼트 틱 ─────────────────────
    /// <summary>
    /// 바 트랙 위에 등간격 세그먼트 틱(수직 1px 반투명 라인)을 배치한다.
    /// 목업 .bar는 overflow:hidden으로 fill이 트랙 밖으로 나가지 않는 것을 전제한다.
    /// Unity Image.Type.Filled는 RectMask2D가 없으면 트랙을 벗어날 수 있으므로
    /// 이 함수를 호출하기 전에 반드시 트랙 GO에 RectMask2D가 붙어 있어야 한다.
    /// segments=4 이면 틱이 3개(25%, 50%, 75% 위치). dividers = segments-1.
    /// </summary>
    static void AddSegmentTicks(RectTransform track, int segments, Color tickColor)
    {
        if (segments < 2) return;
        int tickCount = segments - 1;
        for (int i = 1; i <= tickCount; i++)
        {
            float frac = i / (float)segments;
            var tick = NewImage("Tick" + i, track, tickColor);
            tick.raycastTarget = false;
            tick.type = Image.Type.Simple;
            var tr = tick.rectTransform;
            tr.anchorMin = new Vector2(frac, 0f);
            tr.anchorMax = new Vector2(frac, 1f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(Mathf.Round(F(1)), 0f);   // 폭 2px(정수, 서브픽셀 흐림 방지), 높이는 앵커로 트랙 전체
            tr.anchoredPosition = Vector2.zero;
        }
    }

    // ───────────────────── 흰 사각 스프라이트 에셋 보장 ─────────────────────
    /// <summary>
    /// 8×8 흰색 PNG를 프로젝트에 생성(또는 재사용)하고 Sprite로 임포트해 반환한다.
    /// border=0 → 9-slice 라운딩 없는 순수 직각 사각형.
    /// </summary>
    static Sprite EnsureWhiteSquareSprite()
    {
        string absPath = Path.GetFullPath(WhiteSquarePath);

        // PNG 파일이 없을 때만 8x8 흰색으로 생성(디스크 기록).
        if (!File.Exists(absPath))
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color32[64];
            for (int i = 0; i < 64; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] pngBytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string absDir = Path.GetDirectoryName(absPath);
            if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir);
            File.WriteAllBytes(absPath, pngBytes);
            AssetDatabase.ImportAsset(WhiteSquarePath, ImportAssetOptions.ForceUpdate);
        }

        // 임포터 설정을 항상 검증/정규화 — 기존 에셋이 잘못된 border/타입/압축이어도 교정한다.
        // (기존-에셋 조기반환으로 정규화를 건너뛰면 잘못 임포트된 스프라이트가 그대로 쓰임.)
        var importer = AssetImporter.GetAtPath(WhiteSquarePath) as TextureImporter;
        if (importer != null)
        {
            bool dirty =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.spriteBorder != Vector4.zero ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = Vector4.zero;   // 9-slice border 없음 → 직각 보장
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        // SaveAndReimport는 동기 임포트라 직후 로드가 유효하다. (AssetDatabase.Refresh()는
        // 도메인 리로드를 유발해 일부 자동화 컨텍스트에서 실행을 중단시키므로 호출하지 않는다.)
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
        if (sprite == null)
            Debug.LogError("[HudBuilder] white_square.png 스프라이트 로드 실패 — 경로: " + WhiteSquarePath);
        return sprite;
    }

    static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString(h, out var c);
        return c;
    }
}
