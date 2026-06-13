// Venodonte 사수 랩 — 씬 빌더 + 디스크 렌더 캡처. CaniathroxLabCapture의 사수 버전.
// ⚠️MCP Camera_Capture 금지(죽은 프레임 캐시). 캡처 = RenderPipeline.StandardRequest → RT → ReadPixels → PNG.
//
// 라이팅 = ScanLit_v2 베이스라인("Frozen Golden Hour"): 워밍키+쿨필 디렉셔널 2 + 트라이라이트 앰비언트 + 공유 볼륨.
//
// 두 메뉴:
//   - Build Combat Test : 같은 랩 씬을 "여러 사수가 사거리 잡고 사격, 플레이어 위빙으로 피하기"로 구성(저장).
//                         플레이어·적·풀은 런타임 스폰(VenodonteLabSpawner) → 씬엔 Ground/라이팅/카메라/볼륨/스포너만.
//   - Arm Sequence Capture (play mode) : 한 사수의 Aim→Fire 사이클을 감시해 대표 프레임 디스크 렌더.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class VenodonteLabCapture
{
    const string ScenePath = "Assets/_Project/Scenes/Greybox_VenodonteLab.unity";
    const string OutDir = "docs/03_reference/assets/venodonte_lab";

    const string PrefabPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 11/Venodonte/Prefabs/Venodonte_Tint1.prefab";
    const string ControllerPath = "Assets/_Project/Animations/VenodonteAttack.controller";
    const string VolumeProfilePath = "Assets/_Project/Setting/Greybox_ScanLit_v2_Post.asset";

    [MenuItem("ZombieCrush/Venodonte Lab/Build Combat Test")]
    public static void BuildCombatTest()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(6f, 1f, 6f);   // 60×60m 위빙 공간
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            var gmat = new Material(litShader);
            gmat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f, 1f));
            gmat.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gmat;
        }

        BuildScanLitLighting();

        // JudgeCam — 45°/15m(명세 프레이밍).
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
        else Debug.LogWarning($"[VenodonteLab] 볼륨 프로파일 미발견: {VolumeProfilePath}");

        // 런타임 스포너 — 플레이 진입 시 플레이어 + Venodonte 사수 + 공유 풀 생성.
        var spawnerGo = new GameObject("VenodonteLabSpawner");
        var spawner = spawnerGo.AddComponent<VenodonteLabSpawner>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var so = new SerializedObject(spawner);
        var pPrefab = so.FindProperty("venodontePrefab");
        var pCtrl = so.FindProperty("attackController");
        if (pPrefab != null) pPrefab.objectReferenceValue = prefab;
        if (pCtrl != null) pCtrl.objectReferenceValue = ctrl;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (prefab == null) Debug.LogWarning($"[VenodonteLab] 프리팹 미발견: {PrefabPath}");
        if (ctrl == null) Debug.LogWarning($"[VenodonteLab] 컨트롤러 미발견: {ControllerPath}");

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[VenodonteLab] 미니 전투 씬 저장 완료: {ScenePath} — ▶ 플레이로 확정(WASD/방향키 이동, 5마리 사수가 조준 사격).");
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

    // ════════ 캡처 (플레이 모드) — 사수 1마리의 Aim→Fire 사이클을 감시해 디스크 렌더 ════════
    static int _shotMask;
    static Camera _watchCam; static Animator _watchAnim;

    [MenuItem("ZombieCrush/Venodonte Lab/Arm Sequence Capture (play mode)")]
    public static void ArmSequenceCapture()
    {
        if (!Application.isPlaying) { Debug.LogError("[VenodonteLab] 플레이 모드에서만 캡처(VFX·상태머신은 런타임)."); return; }
        var camGo = GameObject.Find("JudgeCam");
        if (camGo == null) { Debug.LogError("[VenodonteLab] JudgeCam 미발견"); return; }
        _watchCam = camGo.GetComponent<Camera>();
        _watchAnim = Object.FindFirstObjectByType<Animator>();
        if (_watchAnim == null) { Debug.LogError("[VenodonteLab] Animator 미발견"); return; }

        Directory.CreateDirectory(OutDir);
        _shotMask = 0;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        Debug.Log("[VenodonteLab] 시퀀스 캡처 무장 — 한 사이클 도는 동안 v1_*.png 자동 저장.");
    }

    static void Watch()
    {
        if (!Application.isPlaying || _watchAnim == null || _watchCam == null)
        { EditorApplication.update -= Watch; return; }

        var info = _watchAnim.GetCurrentAnimatorStateInfo(0);
        bool inTrans = _watchAnim.IsInTransition(0);
        float n = info.normalizedTime % 1f;

        if (info.IsName("Aim") && !inTrans && n >= 0.30f && n <= 0.45f && (_shotMask & 1) == 0)
        { Shot("v1_1_aim_windup"); _shotMask |= 1; }
        else if (info.IsName("Fire") && !inTrans && n >= 0.20f && n <= 0.27f && (_shotMask & 2) == 0)
        { Shot("v1_2_fire_shot1"); _shotMask |= 2; }
        else if (info.IsName("Fire") && !inTrans && n >= 0.60f && n <= 0.68f && (_shotMask & 4) == 0)
        { Shot("v1_3_fire_shot3"); _shotMask |= 4; }
        else if (info.IsName("Reposition") && !inTrans && (_shotMask & 8) == 0)
        { Shot("v1_4_reposition"); _shotMask |= 8; }

        if (_shotMask == 0xF) { EditorApplication.update -= Watch; Debug.Log("[VenodonteLab] 시퀀스 캡처 완료(4컷)."); }
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
        Debug.Log($"[VenodonteLab] 저장: {tag}.png");
    }
}
