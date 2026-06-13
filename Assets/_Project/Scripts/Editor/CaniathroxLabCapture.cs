// Caniathrox 공격 연출 랩 — 씬 빌더 + 디스크 렌더 캡처(v4: 상태머신 주도).
// ⚠️MCP Camera_Capture 금지(죽은 프레임 캐시). 캡처 = RenderPipeline.StandardRequest → RT → ReadPixels → PNG.
//
// 라이팅 = ScanLit_v2 베이스라인("Frozen Golden Hour"): 워밍키+쿨필 디렉셔널 2개 + 트라이라이트 앰비언트 + 공유 볼륨 프로파일.
//
// v4 변경: 드라이버가 Playable/freeze 대신 AnimatorController(CaniathroxAttack.controller)를 구동.
//   캡처도 freeze 위상 스크럽 대신 "플레이 중 상태 진입을 감시해 디스크 렌더"한다(상태머신이 진실).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CaniathroxLabCapture
{
    const string ScenePath = "Assets/_Project/Scenes/Greybox_CaniathroxLab.unity";
    const string CombatScenePath = "Assets/_Project/Scenes/Greybox_CaniathroxLab.unity";   // 같은 랩 씬을 미니 전투로 재구성(저장 OK)
    const string OutDir = "docs/03_reference/assets/caniathrox_lab";

    const string PrefabPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 8/Caniathrox/Prefab/Caniathrox.prefab";
    const string ControllerPath = "Assets/_Project/Animations/CaniathroxAttack.controller";
    const string VolumeProfilePath = "Assets/_Project/Setting/Greybox_ScanLit_v2_Post.asset";

    // ════════ 씬 빌드 (신규 랩 씬만 저장) ════════

    [MenuItem("ZombieCrush/Caniathrox Lab/Build Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);

        BuildScanLitLighting();

        // JudgeCam — 권위값(피치45°·FOV50·거리12m). aimPoint=(-0.5,1.3,0).
        var camGo = new GameObject("JudgeCam");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        const float camPitch = 45f, camDist = 12f;
        Vector3 aimPoint = new Vector3(-0.5f, 1.3f, 0f);
        float up   = camDist * Mathf.Sin(camPitch * Mathf.Deg2Rad);
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
        else Debug.LogWarning($"[CaniathroxLab] 볼륨 프로파일 미발견: {VolumeProfilePath}");

        // 더미 — 접근 거리 확보 위해 몬스터에서 9m 떨어뜨림(x=6, 몬스터 x=-3).
        var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        dummy.name = "SpitTargetDummy";
        dummy.transform.position = new Vector3(6f, 1f, 0f);
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            var dummyMat = new Material(litShader);
            dummyMat.SetColor("_BaseColor", new Color(0.4f, 0.4f, 0.42f, 1f));
            dummyMat.SetFloat("_Smoothness", 0.05f);
            dummyMat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(dummyMat, "Assets/_Project/Setting/CaniLab_DummyMat.mat");
            dummy.GetComponent<MeshRenderer>().sharedMaterial = dummyMat;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject monster = null;
        if (prefab != null)
        {
            monster = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            monster.name = "Caniathrox";
            monster.transform.position = new Vector3(-3f, 0f, 0f);
        }
        else Debug.LogWarning($"[CaniathroxLab] Caniathrox 프리팹 미발견: {PrefabPath}");

        var demoGo = new GameObject("CaniathroxAttackDemo");
        var demo = demoGo.AddComponent<CaniathroxAttackDemo>();
        demo.spitTarget = dummy.transform;
        if (monster != null)
        {
            demo.model = monster.transform;
            demo.modelAnimator = monster.GetComponentInChildren<Animator>();
        }
        // v4: 클립 직접 와이어링 대신 상태머신 컨트롤러 1개만 할당.
        demo.attackController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (demo.attackController == null)
            Debug.LogWarning($"[CaniathroxLab] 컨트롤러 미발견: {ControllerPath}");

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[CaniathroxLab] 씬 저장 완료: {ScenePath}");
    }

    // ════════ 미니 전투 테스트 빌드 — 플레이어 + 여러 마리 Caniathrox 추격(런타임 스포너) ════════
    //   기존 Build Scene(1마리 더미 데모)과 별개. 이 메뉴는 같은 랩 씬을 "여러 마리 달려와 피하기"로 재구성한다.
    //   플레이어·적은 런타임 스폰(LabCombatSpawner)이라 씬엔 Ground/라이팅/카메라/볼륨/스포너만 저장(씬 비대화 회피).
    [MenuItem("ZombieCrush/Caniathrox Lab/Build Combat Test")]
    public static void BuildCombatTest()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(6f, 1f, 6f);   // 60×60m — 회피 공간 확보(데모보다 넓게)
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null)
        {
            var gmat = new Material(litShader);
            gmat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f, 1f));
            gmat.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gmat;
        }

        BuildScanLitLighting();

        // JudgeCam — 45°/15m(명세 프레이밍). 플레이어는 원점이라 aimPoint=원점 위.
        var camGo = new GameObject("JudgeCam");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.farClipPlane = 200f;
        const float camPitch = 45f, camDist = 15f;
        Vector3 aimPoint = new Vector3(0f, 1f, 0f);
        float up   = camDist * Mathf.Sin(camPitch * Mathf.Deg2Rad);
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
        else Debug.LogWarning($"[CaniathroxLab] 볼륨 프로파일 미발견: {VolumeProfilePath}");

        // 런타임 스포너 — 플레이 진입 시 플레이어 캡슐 + Caniathrox 여러 마리 생성·와이어링.
        var spawnerGo = new GameObject("LabCombatSpawner");
        var spawner = spawnerGo.AddComponent<LabCombatSpawner>();
        // SerializeField 와이어링(리플렉션 불요) — SerializedObject로 프리팹/컨트롤러 주입.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var so = new SerializedObject(spawner);
        var pPrefab = so.FindProperty("caniathroxPrefab");
        var pCtrl = so.FindProperty("attackController");
        if (pPrefab != null) pPrefab.objectReferenceValue = prefab;
        if (pCtrl != null) pCtrl.objectReferenceValue = ctrl;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (prefab == null) Debug.LogWarning($"[CaniathroxLab] Caniathrox 프리팹 미발견: {PrefabPath}");
        if (ctrl == null) Debug.LogWarning($"[CaniathroxLab] 컨트롤러 미발견: {ControllerPath}");

        EditorSceneManager.SaveScene(scene, CombatScenePath);
        Debug.Log($"[CaniathroxLab] 미니 전투 씬 저장 완료: {CombatScenePath} — ▶ 플레이로 확정(플레이어 WASD/방향키 이동, 6마리 추격).");
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

    // ════════ 캡처 (플레이 모드) — 상태 진입을 감시해 디스크 렌더 ════════
    //   freeze 위상 스크럽 폐기(v4). 대신 살아있는 상태머신을 따라가며 각 정체성 동작의 대표 프레임을 굽는다.
    //   ★플레이 모드에서 이 메뉴를 누른 뒤, 한 사이클이 도는 동안 자동으로 v4_*.png가 채워진다.
    static int _shotMask;

    [MenuItem("ZombieCrush/Caniathrox Lab/Arm Sequence Capture (play mode)")]
    public static void ArmSequenceCapture()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[CaniathroxLab] 플레이 모드에서만 캡처 가능(VFX·상태머신은 런타임).");
            return;
        }
        var camGo = GameObject.Find("JudgeCam");
        if (camGo == null) { Debug.LogError("[CaniathroxLab] JudgeCam 미발견"); return; }
        var cam = camGo.GetComponent<Camera>();
        var anim = Object.FindFirstObjectByType<Animator>();
        if (anim == null) { Debug.LogError("[CaniathroxLab] Animator 미발견"); return; }

        Directory.CreateDirectory(OutDir);
        _shotMask = 0;
        EditorApplication.update -= Watch;
        _watchCam = cam; _watchAnim = anim;
        EditorApplication.update += Watch;
        Debug.Log("[CaniathroxLab] 시퀀스 캡처 무장 — 한 사이클 도는 동안 v4_*.png 자동 저장.");
    }

    static Camera _watchCam; static Animator _watchAnim;
    static void Watch()
    {
        if (!Application.isPlaying || _watchAnim == null || _watchCam == null)
        { EditorApplication.update -= Watch; return; }

        var info = _watchAnim.GetCurrentAnimatorStateInfo(0);
        bool inTrans = _watchAnim.IsInTransition(0);
        float n = info.normalizedTime % 1f;

        if (info.IsName("Approach") && !inTrans && (_shotMask & 1) == 0)
        { Shot("v4_1_approach"); _shotMask |= 1; }
        else if (info.IsName("Lunge") && !inTrans && n >= 0.36f && n <= 0.45f && (_shotMask & 2) == 0)
        { Shot("v4_3_lunge_apex"); _shotMask |= 2; }
        else if (info.IsName("Lunge") && !inTrans && n >= 0.58f && n <= 0.66f && (_shotMask & 4) == 0)
        { Shot("v4_4_land_impact"); _shotMask |= 4; }
        else if (info.IsName("Spit") && !inTrans && n >= 0.40f && n <= 0.55f && (_shotMask & 8) == 0)
        { Shot("v4_5_spit"); _shotMask |= 8; }

        if (_shotMask == 0xF) { EditorApplication.update -= Watch; Debug.Log("[CaniathroxLab] 시퀀스 캡처 완료(4컷)."); }
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
        Debug.Log($"[CaniathroxLab] 저장: {tag}.png");
    }
}
