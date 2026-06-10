using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using DistantLands.Cozy;

public static class CozyDuskSetup
{
    [MenuItem("Tools/COZY/Set Time From EditorPrefs")]
    public static void SetTimeFromPrefs()
    {
        float t = EditorPrefs.GetFloat("CozyDusk_Time", 0.7430f);
        var weather = Object.FindFirstObjectByType<CozyWeather>();
        var time = weather != null ? weather.GetModule<CozyTimeModule>() : null;
        if (time == null)
        {
            EditorPrefs.SetString("CozyDusk_Result", "FAIL weather=" + (weather != null));
            return;
        }
        time.currentTime = t;

        // editor may be unfocused: force a full COZY tick now
        weather.RaiseUpdateWeatherWeights();
        weather.RaiseUpdateFXWeights();
        weather.RaisePropogateVariables();
        weather.RaiseCozyUpdateLoop();
        weather.UpdateShaderVariables();
        DynamicGI.UpdateEnvironment(); // rebuild ambient probe (no auto-update while editor is unfocused)

        var sun = RenderSettings.sun;
        Debug.Log("[CozyDuskSetup] time=" + t
            + " sunEuler=" + (sun != null ? sun.transform.eulerAngles.ToString() : "null")
            + " sunColor=" + (sun != null ? sun.color.ToString() : "null"));

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorPrefs.SetString("CozyDusk_Result", "OK t=" + t
            + " now=" + (float)time.currentTime
            + " sunEuler=" + (sun != null ? sun.transform.eulerAngles.ToString() : "null")
            + " sunColor=" + (sun != null ? sun.color.ToString() : "null"));
    }

    [MenuItem("Tools/COZY/Create Dusk Profile (sun through corridor)")]
    public static void CreateDuskProfile()
    {
        var weather = Object.FindFirstObjectByType<CozyWeather>();
        var atmo = weather != null ? weather.GetModule<CozyAtmosphereModule>() : null;
        if (atmo == null || atmo.atmosphereProfile == null)
        {
            EditorPrefs.SetString("CozyDusk_Result", "FAIL atmo=" + (atmo != null));
            return;
        }

        string srcPath = AssetDatabase.GetAssetPath(atmo.atmosphereProfile);
        string dstPath = "Assets/_Project/Setting/Atmosphere_Dusk_v2.asset";
        if (AssetDatabase.LoadAssetAtPath<DistantLands.Cozy.Data.AtmosphereProfile>(dstPath) == null)
        {
            if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            {
                EditorPrefs.SetString("CozyDusk_Result", "FAIL copy " + srcPath);
                return;
            }
        }
        var profile = AssetDatabase.LoadAssetAtPath<DistantLands.Cozy.Data.AtmosphereProfile>(dstPath);

        float dir = EditorPrefs.GetFloat("CozyDusk_SunDirection", 165f);
        profile.sunDirection.mode = VariableProperty.Mode.constant;
        profile.sunDirection.floatVal = dir;

        // enclosed arena: floor never sees the 5-deg sunset sun, so lift ambient
        // (Hell Express office look: cool ambient base + warm practicals)
        float ambientMul = EditorPrefs.GetFloat("CozyDusk_AmbientMul", 1f);
        profile.ambientLightMultiplier.mode = VariableProperty.Mode.constant;
        profile.ambientLightMultiplier.floatVal = ambientMul;

        float fogMul = EditorPrefs.GetFloat("CozyDusk_FogMul", 1f);
        profile.fogDensityMultiplier.mode = VariableProperty.Mode.constant;
        profile.fogDensityMultiplier.floatVal = fogMul;
        EditorUtility.SetDirty(profile);

        atmo.atmosphereProfile = profile;
        EditorUtility.SetDirty(atmo);

        weather.RaiseUpdateWeatherWeights();
        weather.RaiseUpdateFXWeights();
        weather.RaisePropogateVariables();
        weather.RaiseCozyUpdateLoop();
        weather.UpdateShaderVariables();
        DynamicGI.UpdateEnvironment(); // rebuild ambient probe (no auto-update while editor is unfocused)

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        var sun = RenderSettings.sun;
        EditorPrefs.SetString("CozyDusk_Result", "OK dir=" + dir
            + " sunEuler=" + (sun != null ? sun.transform.eulerAngles.ToString() : "null")
            + " sunColor=" + (sun != null ? sun.color.ToString() : "null"));
    }

    static void SetConst(VariableProperty p, Color c)
    {
        p.mode = VariableProperty.Mode.constant;
        p.colorVal = c;
    }

    [MenuItem("Tools/COZY/Freeze Golden Hour")]
    public static void FreezeGoldenHour()
    {
        var weather = Object.FindFirstObjectByType<CozyWeather>();
        var atmo = weather != null ? weather.GetModule<CozyAtmosphereModule>() : null;
        var profile = atmo != null ? atmo.atmosphereProfile : null;
        if (profile == null || !AssetDatabase.GetAssetPath(profile).Contains("Atmosphere_Dusk_v2"))
        {
            EditorPrefs.SetString("CozyDusk_Result", "FAIL: dusk profile not assigned");
            return;
        }

        // frozen golden hour: keep 15:00 sun elevation, dye the scene dusk
        // sunlightColor carries intensity in COZY (day value was ~1.38 HDR) -> keep hue, restore energy
        SetConst(profile.sunlightColor, new Color(1.00f, 0.62f, 0.38f) * 1.7f);
        SetConst(profile.ambientLightZenithColor, new Color(0.40f, 0.40f, 0.62f));
        SetConst(profile.ambientLightHorizonColor, new Color(0.80f, 0.48f, 0.34f));
        SetConst(profile.skyZenithColor, new Color(0.28f, 0.26f, 0.50f));
        SetConst(profile.skyHorizonColor, new Color(0.95f, 0.55f, 0.35f));
        // fog color ALPHA = per-stage opacity in COZY. alpha 1.0 = full whiteout (2026-06-10 bug).
        // top-down fixed camera ~20m: near stages must be near-transparent, only far stages dense.
        SetConst(profile.fogColor1, new Color(0.50f, 0.38f, 0.42f, 0.06f)); // thin in-frame haze ("공기 한 겹")
        SetConst(profile.fogColor2, new Color(0.42f, 0.32f, 0.42f, 0.10f));
        SetConst(profile.fogColor3, new Color(0.34f, 0.27f, 0.42f, 0.25f));
        SetConst(profile.fogColor4, new Color(0.26f, 0.22f, 0.40f, 0.45f));
        SetConst(profile.fogColor5, new Color(0.18f, 0.16f, 0.34f, 0.75f));
        // push fog transitions beyond the playfield (camera-floor ~20m, arena reads to ~40m)
        profile.fogStart1.mode = VariableProperty.Mode.constant; profile.fogStart1.floatVal = 14f;
        profile.fogStart2.mode = VariableProperty.Mode.constant; profile.fogStart2.floatVal = 30f;
        profile.fogStart3.mode = VariableProperty.Mode.constant; profile.fogStart3.floatVal = 45f;
        profile.fogStart4.mode = VariableProperty.Mode.constant; profile.fogStart4.floatVal = 70f;
        profile.ambientLightMultiplier.mode = VariableProperty.Mode.constant;
        profile.ambientLightMultiplier.floatVal = 2.2f;
        EditorUtility.SetDirty(profile);

        weather.RaiseUpdateWeatherWeights();
        weather.RaiseUpdateFXWeights();
        weather.RaisePropogateVariables();
        weather.RaiseCozyUpdateLoop();
        weather.UpdateShaderVariables();
        DynamicGI.UpdateEnvironment();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetString("CozyDusk_Result", "OK golden hour frozen");
    }

    [MenuItem("Tools/COZY/Setup Dusk In Current Scene")]
    public static void Setup()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Greybox_ScanLit_v2")
        {
            Debug.LogError("[CozyDuskSetup] wrong scene: " + scene.name);
            return;
        }

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "COZY Weather Sphere") Object.DestroyImmediate(root);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Packages/com.distantlands.cozy.core/Content/Prefabs/Cozy Weather Sphere.prefab");
        if (prefab == null)
        {
            Debug.LogError("[CozyDuskSetup] COZY prefab not found");
            return;
        }

        var sphere = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        sphere.name = "COZY Weather Sphere";

        var weather = sphere.GetComponentInChildren<CozyWeather>();
        var time = weather != null ? weather.GetModule<CozyTimeModule>() : null;
        if (time != null) time.currentTime = 0.78125f; // 18:45
        Debug.Log("[CozyDuskSetup] weather=" + (weather != null) + " time=" + (time != null));

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.name == "Sun" && l.type == LightType.Directional && !l.transform.IsChildOf(sphere.transform))
            {
                l.gameObject.SetActive(false);
                Debug.Log("[CozyDuskSetup] manual Sun disabled");
            }
        }
        RenderSettings.fog = false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CozyDuskSetup] done + saved");
    }
}
