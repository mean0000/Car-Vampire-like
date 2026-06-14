// Crassorrid 브루트 랩 — 씬 빌더 + 디스크 렌더 캡처. DimaxillosaurusLabCapture의 브루트 버전.
// ⚠️MCP Camera_Capture 금지(죽은 프레임 캐시). 캡처 = RenderPipeline.StandardRequest → RT → ReadPixels → PNG.
//
// ★텔레그래프 렌더 경로(이 종이 첫 소비자): 장판 쿼드는 PickupInfo 레이어(13) → 프로젝트 URP 렌더러의 PickupInfoOverlay
//   RenderObjects(이벤트 600)가 콘 어둠 면제로 재드로우. 랩 씬엔 시야콘이 없어도 PickupInfo 레이어+ZTest LEqual 경로를
//   그대로 써서 *실 경로 검증*(정적 캡처로도 레드오렌지 원 장판이 차오르는지 확인 가능 — Dimax 부채꼴 검증 선례).
//
// 메뉴:
//   - 1. Setup Data : CrassorridLabSetup(클립·이벤트·머티리얼·컨트롤러). 먼저 실행.
//   - 2. Build Combat Test : 랩 씬 구성·저장(플레이어·적·풀은 런타임 스폰).
//   - 3. Arm Smash Capture (play mode) : 한 브루트의 Roar→Approach→Windup(장판 차오름)→Strike(임팩트) 대표 프레임 디스크 렌더.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CrassorridLabCapture
{
    const string ScenePath = "Assets/_Project/Scenes/Greybox_CrassorridLab.unity";
    const string OutDir = "docs/03_reference/assets/crassorrid_lab";
    const string PrefabPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack vol 9/Crassorrid/Prefab/Crassorrid.prefab";
    const string ControllerPath = "Assets/_Project/Animations/CrassorridBrawler.controller";
    const string VolumeProfilePath = "Assets/_Project/Setting/Greybox_ScanLit_v2_Post.asset";

    [MenuItem("ZombieCrush/Crassorrid Lab/2. Build Combat Test")]
    public static void BuildCombatTest()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(8f, 1f, 8f);   // 80×80m (브루트 전진·접근 여유)
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            var gmat = new Material(litShader);
            gmat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f, 1f));
            gmat.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gmat;
        }

        BuildScanLitLighting();

        // JudgeCam — 45°/15m. (브루트가 커서 약간 더 빼고 싶으면 camDist↑ — 시작 15m.)
        var camGo = new GameObject("JudgeCam");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 200f;
        const float camPitch = 45f, camDist = 16f;
        Vector3 aimPoint = new Vector3(0f, 1.5f, 0f);
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
        else Debug.LogWarning($"[CrassorridLab] 볼륨 프로파일 미발견: {VolumeProfilePath}");

        // 런타임 스포너.
        var spawnerGo = new GameObject("CrassorridLabSpawner");
        var spawner = spawnerGo.AddComponent<CrassorridLabSpawner>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var so = new SerializedObject(spawner);
        var pPrefab = so.FindProperty("crassorridPrefab");
        var pCtrl = so.FindProperty("attackController");
        if (pPrefab != null) pPrefab.objectReferenceValue = prefab;
        if (pCtrl != null) pCtrl.objectReferenceValue = ctrl;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (prefab == null) Debug.LogWarning($"[CrassorridLab] 프리팹 미발견: {PrefabPath}");
        if (ctrl == null) Debug.LogWarning($"[CrassorridLab] 컨트롤러 미발견(먼저 '1. Setup Data' 실행): {ControllerPath}");

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[CrassorridLab] 미니 전투 씬 저장: {ScenePath} — ▶ 플레이로 확정(WASD/방향키 이동, 브루트가 접근→정지→윈드업(장판 차오름)→내려찍기).");
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

    // ════════ 캡처 (플레이 모드) — 브루트 1마리의 Roar→Approach→Windup(장판)→Strike(임팩트) 사이클 감시·디스크 렌더 ════════
    //   ★상태명 watcher가 컨트롤러 상태명과 정확히 일치해야 한다(§Dimax 함정: rename 시 watcher 미발화 → _shotMask 영원 미완 → 무한폴링 스톨).
    static int _shotMask;
    static Camera _watchCam; static Animator _watchAnim;

    [MenuItem("ZombieCrush/Crassorrid Lab/3. Arm Smash Capture (play mode)")]
    public static void ArmSmashCapture()
    {
        if (!Application.isPlaying) { Debug.LogError("[CrassorridLab] 플레이 모드에서만 캡처(VFX·상태머신은 런타임)."); return; }
        var camGo = GameObject.Find("JudgeCam");
        if (camGo == null) { Debug.LogError("[CrassorridLab] JudgeCam 미발견"); return; }
        _watchCam = camGo.GetComponent<Camera>();
        _watchAnim = Object.FindFirstObjectByType<Animator>();
        if (_watchAnim == null) { Debug.LogError("[CrassorridLab] Animator 미발견"); return; }

        Directory.CreateDirectory(OutDir);
        _shotMask = 0;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        Debug.Log("[CrassorridLab] 스매시 캡처 무장 — live_*.png 자동(Roar/Approach/Windup 장판차오름/Windup 가득/Strike 임팩트).");
    }

    static void Watch()
    {
        if (!Application.isPlaying || _watchAnim == null || _watchCam == null)
        { EditorApplication.update -= Watch; return; }

        var info = _watchAnim.GetCurrentAnimatorStateInfo(0);
        bool inTrans = _watchAnim.IsInTransition(0);
        float n = info.normalizedTime % 1f;

        // ★상태명 일치(컨트롤러): Roar / Approach / SmashWindup / SmashStrike. 임팩트 = Strike norm (20-15)/(30-15)≈0.333.
        if (info.IsName("Roar") && !inTrans && n >= 0.40f && n <= 0.55f && (_shotMask & 1) == 0)
        { Shot("live_1_roar"); _shotMask |= 1; }
        else if (info.IsName("Approach") && !inTrans && n >= 0.30f && n <= 0.70f && (_shotMask & 2) == 0)
        { Shot("live_2_approach"); _shotMask |= 2; }
        else if (info.IsName("SmashWindup") && !inTrans && n >= 0.40f && n <= 0.55f && (_shotMask & 4) == 0)
        { Shot("live_3_windup_filling"); _shotMask |= 4; }   // ★장판 절반 차오름(예고원 가독)
        else if (info.IsName("SmashWindup") && !inTrans && n >= 0.90f && n <= 1.0f && (_shotMask & 8) == 0)
        { Shot("live_4_windup_full"); _shotMask |= 8; }      // ★장판 거의 가득(임팩트 직전)
        else if (info.IsName("SmashStrike") && !inTrans && n >= 0.28f && n <= 0.40f && (_shotMask & 16) == 0)
        { Shot("live_5_strike_impact"); _shotMask |= 16; }   // ★내려찍기 임팩트(장판 발동)

        if (_shotMask == 0x1F) { EditorApplication.update -= Watch; Debug.Log("[CrassorridLab] 스매시 캡처 완료(5컷)."); }
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
        Debug.Log($"[CrassorridLab] 저장: {tag}.png");
    }
}
