// 텔레그래프 랩 — 씬 빌더 + 디스크 렌더 캡처 유틸 (MCP RunCommand에서 ExecuteMenuItem으로 구동).
// 캡처 = 검증된 디스크 렌더 레시피: RenderPipeline.StandardRequest → RT → ReadPixels → PNG.
// ⚠️MCP Camera_Capture 금지(죽은 프레임 캐시 — 프로젝트 실측 함정).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class TelegraphLabCapture
{
    const string ScenePath = "Assets/_Project/Scenes/Greybox_TelegraphLab.unity";
    const string OutDir = "docs/03_reference/assets/telegraph_lab";

    // ════════ 씬 빌드 (신규 랩 씬만 저장 — 기존 씬 저장 금지) ════════

    [MenuItem("ZombieCrush/Telegraph Lab/Build Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 지면 30×30m — 머티리얼은 TelegraphLab.Awake가 런타임 생성(씬에 비에셋 머티리얼 직렬화 회피)
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = Color.white;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // JudgeCam — 45° 피치 부감, 높이 15m, 4종이 한 프레임에
        var camGo = new GameObject("JudgeCam");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        camGo.transform.SetPositionAndRotation(new Vector3(-1f, 15f, -13f), Quaternion.Euler(45f, 0f, 0f));

        new GameObject("TelegraphLab").AddComponent<TelegraphLab>();

        // 오클루전 검증용 캡슐 — 원 장판(x=-8.5) 위. ZTest LEqual이 살아있으면 캡슐이 장판을 가린다.
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "OcclusionCapsule";
        capsule.transform.position = new Vector3(-8.5f, 1f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[TelegraphLabCapture] 씬 저장 완료: {ScenePath}");
    }

    // ════════ 캡처 (플레이 모드에서 호출) ════════

    [MenuItem("ZombieCrush/Telegraph Lab/Capture P020")]
    public static void CaptureP020() { Capture(0.20f, "telegraph_p020.png"); }

    [MenuItem("ZombieCrush/Telegraph Lab/Capture P055")]
    public static void CaptureP055() { Capture(0.55f, "telegraph_p055.png"); }

    [MenuItem("ZombieCrush/Telegraph Lab/Capture P090")]
    public static void CaptureP090() { Capture(0.90f, "telegraph_p090.png"); }

    static void Capture(float progress, string fileName)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[TelegraphLabCapture] 플레이 모드에서만 캡처 가능(쿼드는 Awake 생성)");
            return;
        }
        var lab = Object.FindFirstObjectByType<TelegraphLab>();
        if (lab == null)
        {
            Debug.LogError("[TelegraphLabCapture] TelegraphLab 미발견 — 랩 씬인가?");
            return;
        }
        lab.debugProgress = progress;
        lab.ApplyNow();

        var camGo = GameObject.Find("JudgeCam");
        if (camGo == null)
        {
            Debug.LogError("[TelegraphLabCapture] JudgeCam 미발견");
            return;
        }
        var cam = camGo.GetComponent<Camera>();

        const int W = 1920, H = 1080;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        var req = new RenderPipeline.StandardRequest { destination = rt };
        if (!RenderPipeline.SupportsRenderRequest(cam, req))
        {
            Debug.LogError("[TelegraphLabCapture] StandardRequest 미지원");
            rt.Release();
            return;
        }
        RenderPipeline.SubmitRenderRequest(cam, req);

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        Directory.CreateDirectory(OutDir);
        string path = Path.Combine(OutDir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Debug.Log($"[TelegraphLabCapture] 저장: {path} (progress={progress:0.00})");
    }
}
