// Dimaxillosaurus 격투 랩 — 씬 빌더 + 디스크 렌더 캡처. VenodonteLabCapture의 근접 버전.
// ⚠️MCP Camera_Capture 금지(죽은 프레임 캐시). 캡처 = RenderPipeline.StandardRequest → RT → ReadPixels → PNG.
//
// ★텔레그래프 렌더 경로: 장판 쿼드는 PickupInfo 레이어(13) → 프로젝트 URP 렌더러의 PickupInfoOverlay
//   RenderObjects(이벤트 600)가 콘 어둠 면제로 재드로우. 랩 씬엔 시야콘이 없어 어차피 보이지만,
//   실 경로 검증 위해 PickupInfo 레이어 + ZTest LEqual 경로를 그대로 쓴다(TelegraphPad/Pool 내장).
//
// 메뉴:
//   - 1. Setup Data : DimaxillosaurusLabSetup(클립·이벤트·머티리얼·컨트롤러). 먼저 실행.
//   - 2. Build Combat Test : 랩 씬 구성·저장(플레이어·적·풀은 런타임 스폰).
//   - 3. Arm Combo Capture (play mode) : 한 격투체의 Roar→Combo 사이클 대표 프레임 디스크 렌더.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class DimaxillosaurusLabCapture
{
    const string ScenePath = "Assets/_Project/Scenes/Greybox_DimaxillosaurusLab.unity";
    const string OutDir = "docs/03_reference/assets/dimaxillosaurus_lab";
    const string PrefabPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 7/Dimaxillosaurus/Prefab/Dimaxillosaurus.prefab";
    const string ControllerPath = "Assets/_Project/Animations/DimaxillosaurusBrawler.controller";
    const string VolumeProfilePath = "Assets/_Project/Setting/Greybox_ScanLit_v2_Post.asset";

    [MenuItem("ZombieCrush/Dimaxillosaurus Lab/2. Build Combat Test")]
    public static void BuildCombatTest()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(6f, 1f, 6f);   // 60×60m
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            var gmat = new Material(litShader);
            gmat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f, 1f));
            gmat.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gmat;
        }

        BuildScanLitLighting();

        // JudgeCam — 45°/15m.
        var camGo = new GameObject("JudgeCam");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 200f;
        const float camPitch = 45f, camDist = 15f;
        Vector3 aimPoint = new Vector3(0f, 1f, 0f);
        float up = camDist * Mathf.Sin(camPitch * Mathf.Deg2Rad);
        float back = camDist * Mathf.Cos(camPitch * Mathf.Deg2Rad);
        camGo.transform.SetPositionAndRotation(
            new Vector3(aimPoint.x, aimPoint.y + up, aimPoint.z - back),
            Quaternion.Euler(camPitch, 0f, 0f));
        var cad = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cad.renderPostProcessing = true;

        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile != null) vol.sharedProfile = profile;
        else Debug.LogWarning($"[DimaxLab] 볼륨 프로파일 미발견: {VolumeProfilePath}");

        // 런타임 스포너.
        var spawnerGo = new GameObject("DimaxillosaurusLabSpawner");
        var spawner = spawnerGo.AddComponent<DimaxillosaurusLabSpawner>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var so = new SerializedObject(spawner);
        var pPrefab = so.FindProperty("dimaxPrefab");
        var pCtrl = so.FindProperty("attackController");
        if (pPrefab != null) pPrefab.objectReferenceValue = prefab;
        if (pCtrl != null) pCtrl.objectReferenceValue = ctrl;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (prefab == null) Debug.LogWarning($"[DimaxLab] 프리팹 미발견: {PrefabPath}");
        if (ctrl == null) Debug.LogWarning($"[DimaxLab] 컨트롤러 미발견(먼저 '1. Setup Data' 실행): {ControllerPath}");

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[DimaxLab] 미니 전투 씬 저장: {ScenePath} — ▶ 플레이로 확정(WASD/방향키 이동, 4마리 격투체가 접근→정지→포효→클로 콤보).");
    }

    static void BuildScanLitLighting()
    {
        var keyGo = new GameObject("Directional Light (Warm Key)");
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.78f, 0.55f, 1f);
        key.intensity = 2.6f;
        key.shadows = LightShadows.Soft;
        keyGo.transform.rotation = new Quaternion(-0.000000009088109f, 0.9781476f, -0.2079117f, -0.00000004275619f);
        RenderSettings.sun = key;

        var fillGo = new GameObject("Directional Light (Cool Fill)");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.75f, 0.8f, 0.95f, 1f);
        fill.intensity = 0.8f;
        fill.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.Euler(140f, 30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor    = new Color(0.88f, 0.88f, 1.364f, 2.2f);
        RenderSettings.ambientEquatorColor= new Color(1.496f, 0.8976f, 0.6358f, 1.87f);
        RenderSettings.ambientGroundColor = new Color(0.748f, 0.4488f, 0.3179f, 1.87f);
        RenderSettings.ambientIntensity = 1f;
    }

    // ════════ 캡처 (플레이 모드) — 격투체 1마리의 Roar→Combo 사이클을 감시해 디스크 렌더 ════════
    static int _shotMask;
    static Camera _watchCam; static Animator _watchAnim;

    [MenuItem("ZombieCrush/Dimaxillosaurus Lab/3. Arm Combo Capture (play mode)")]
    public static void ArmComboCapture()
    {
        if (!Application.isPlaying) { Debug.LogError("[DimaxLab] 플레이 모드에서만 캡처(VFX·상태머신은 런타임)."); return; }
        var camGo = GameObject.Find("JudgeCam");
        if (camGo == null) { Debug.LogError("[DimaxLab] JudgeCam 미발견"); return; }
        _watchCam = camGo.GetComponent<Camera>();
        _watchAnim = Object.FindFirstObjectByType<Animator>();
        if (_watchAnim == null) { Debug.LogError("[DimaxLab] Animator 미발견"); return; }

        Directory.CreateDirectory(OutDir);
        _shotMask = 0;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        Debug.Log("[DimaxLab] 좌우 단발 캡처 무장 — live_*.png 자동 저장(Roar/L채움/L컨택/R채움/R컨택).");
    }

    static void Watch()
    {
        if (!Application.isPlaying || _watchAnim == null || _watchCam == null)
        { EditorApplication.update -= Watch; return; }

        var info = _watchAnim.GetCurrentAnimatorStateInfo(0);
        bool inTrans = _watchAnim.IsInTransition(0);
        float n = info.normalizedTime % 1f;

        // ★좌우 단발 교대 — 4구간 이즈 분할: 컨택은 *Strike* 구간(컨택 norm = (12.25-9)/(16-9)≈0.464 L / (12.845-9)/7≈0.549 R). 채움(윈드업)=Windup.
        //   (★상태 rename: 옛 LeftClaw_Swing/RightClaw_Swing → Windup/Strike/FollowOut로 분할 — 캡처 watcher도 동반 갱신, §7.1 스톨 함정 회피.)
        if (info.IsName("Roar") && !inTrans && n >= 0.45f && n <= 0.60f && (_shotMask & 1) == 0)
        { Shot("live_1_roar"); _shotMask |= 1; }
        else if (info.IsName("LeftClaw_Windup") && !inTrans && n >= 0.55f && n <= 0.75f && (_shotMask & 2) == 0)
        { Shot("live_2_left_windup"); _shotMask |= 2; }
        else if (info.IsName("LeftClaw_Strike") && !inTrans && n >= 0.42f && n <= 0.52f && (_shotMask & 4) == 0)
        { Shot("live_3_left_contact"); _shotMask |= 4; }
        else if (info.IsName("RightClaw_Windup") && !inTrans && n >= 0.55f && n <= 0.75f && (_shotMask & 8) == 0)
        { Shot("live_4_right_windup"); _shotMask |= 8; }
        else if (info.IsName("RightClaw_Strike") && !inTrans && n >= 0.50f && n <= 0.60f && (_shotMask & 16) == 0)
        { Shot("live_5_right_contact"); _shotMask |= 16; }

        if (_shotMask == 0x1F) { EditorApplication.update -= Watch; Debug.Log("[DimaxLab] 좌우 단발 캡처 완료(5컷)."); }
    }

    static void Shot(string tag)
    {
        const int W = 1920, H = 1080;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        var req = new RenderPipeline.StandardRequest { destination = rt };
        if (!RenderPipeline.SupportsRenderRequest(_watchCam, req)) { rt.Release(); Object.DestroyImmediate(rt); return; }
        RenderPipeline.SubmitRenderRequest(_watchCam, req);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(OutDir, tag + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        Debug.Log($"[DimaxLab] 저장: {tag}.png");
    }
}
