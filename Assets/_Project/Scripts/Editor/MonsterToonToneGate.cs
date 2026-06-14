// MonsterToonToneGate.cs  (TEST / capture rig — re-judgment of §6 "괴수 셀셰이드 금지")
// Builds before/after comparison captures: Protofactor monster under
//   A = original PBR (untouched)  vs  B = MonsterToon default  vs  C = MonsterToon graphic.
//
// v2 staging: clones each monster onto an ISOLATED pad far from the city (so the busy CityMap
//   doesn't clutter the close-up), drops ONE Synty Toon-City ground tile + building behind it as
//   the low-poly fidelity reference, and lights the subject FAIRLY and READABLY (warm key + cool
//   fill, Frozen-Golden-Hour palette but bright enough to read the shading). Each monster is
//   distance-fit to fill the same fraction of frame, regardless of real height (2m vs 15m).
//   The exposure here is for COMPARISON LEGIBILITY, not final game exposure — at true 15m top-down
//   the baseline is near-black (see the separate _WIDE shots from the real JudgeCam).
//
// Renders in EDIT MODE via RenderPipeline.SubmitRenderRequest (sidesteps playmode/stale-asm block).
// ⚠️ MCP Camera_Capture forbidden (dead frames). ⚠️ No System.Reflection. ⚠️ Never saves the scene.
// Originals are never touched (clones only); pad + clones + lights destroyed after capture.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MonsterToonToneGate
{
    const string ScenePath = "Assets/_Project/Scenes/ToneGateLab.unity";
    const string OutDir    = "docs/captures/2026-06-13-monster-toon-tonegate";
    const string ShaderName = "ZombieCrush/MonsterToon";
    const int W = 1600, H = 1600;

    const string GroundGuid   = "c28dffdf0257e42f49446687dcd07383"; // Pavement_1A_4x4
    const string BuildingGuid = "64a95b20fedc14d9f8c766b486f2e13c"; // Building_13A

    static readonly Vector3 PadOrigin = new Vector3(400f, 0f, 400f); // far from the city

    // Representative spread: small quadruped / tall beast / humanoid anchor.
    static readonly string[] Monsters = { "Caniathrox", "Fulgurodonte", "Crassorrid" };

    [MenuItem("ZombieCrush/Tone Gate/Capture Monster Toon Comparison")]
    public static void Capture()
    {
        if (Application.isPlaying)
        { Debug.LogError("[ToneGate] 에디터 점유 가드: 플레이 중엔 실행 금지(중단)."); return; }

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var stage = GameObject.Find("_ToneGateStage");
        if (stage == null) { Debug.LogError("[ToneGate] _ToneGateStage 미발견"); return; }
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError("[ToneGate] MonsterToon 셰이더 미발견"); return; }

        Directory.CreateDirectory(OutDir);

        var trash = new List<GameObject>();   // everything we spawn, torn down at the end

        // ---------- Isolated pad: Synty ground + one building backdrop ----------
        var groundPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(GroundGuid));
        var buildingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(BuildingGuid));

        if (groundPrefab != null)
        {
            // Tile is 4x4m — space CONTIGUOUSLY at 4m (8m left visible gaps). 13x13 = 52m floor.
            const float tile = 4f;
            for (int gx = -6; gx <= 6; gx++)
            for (int gz = -6; gz <= 6; gz++)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab);
                g.transform.position = PadOrigin + new Vector3(gx * tile, 0f, gz * tile);
                trash.Add(g);
            }
        }
        if (buildingPrefab != null)
        {
            // Flank the subject and set well back (+Z) so Synty geo frames it without dominating.
            var bld = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab);
            bld.transform.position = PadOrigin + new Vector3(-22f, 0f, 26f);
            bld.transform.rotation = Quaternion.Euler(0, -20f, 0);
            trash.Add(bld);
            var bld2 = (GameObject)PrefabUtility.InstantiatePrefab(buildingPrefab);
            bld2.transform.position = PadOrigin + new Vector3(20f, 0f, 24f);
            bld2.transform.rotation = Quaternion.Euler(0, 25f, 0);
            trash.Add(bld2);
        }

        // ---------- Fair, readable lighting on the pad (Frozen Golden Hour palette) ----------
        var keyGo = new GameObject("~ToneKey"); trash.Add(keyGo);
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.86f, 0.66f);   // warm key
        key.intensity = 2.1f;
        key.shadows = LightShadows.Soft;
        // Key from behind-above the CAMERA (camera looks +Z from -Z), raked to camera-left, so the
        // camera-facing surfaces are LIT (not rim-lit). Camera is south (-Z) looking north (+Z),
        // so the key points north+down: yaw ~25deg off-axis, 42deg pitch.
        keyGo.transform.rotation = Quaternion.Euler(42f, 25f, 0f);

        var fillGo = new GameObject("~ToneFill"); trash.Add(fillGo);
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.62f, 0.72f, 0.95f); // cool fill from camera-right, flat
        fill.intensity = 0.7f;
        fill.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.Euler(18f, -40f, 0f);

        // ---------- Capture camera (inherits JudgeCam post) ----------
        var judge = GameObject.Find("JudgeCam");
        var srcCam = judge != null ? judge.GetComponent<Camera>() : Camera.main;
        var camGo = new GameObject("~ToneGateCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
        trash.Add(camGo);
        var cam = camGo.AddComponent<Camera>();
        if (srcCam != null) cam.CopyFrom(srcCam);
        cam.fieldOfView = 38f;        // a bit longer lens for less perspective distortion on close-ups
        cam.farClipPlane = 800f;
        cam.nearClipPlane = 0.1f;
        var cad = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var srcCad = srcCam != null ? srcCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
        cad.renderPostProcessing = srcCad == null || srcCad.renderPostProcessing;
        cad.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        int shots = 0;
        const float pitch = 42f;       // close to the 45deg game pitch
        const float framing = 1.7f;    // >1 leaves headroom around the subject

        foreach (var mname in Monsters)
        {
            var srcT = stage.transform.Find(mname);
            if (srcT == null) { Debug.LogWarning("[ToneGate] 미발견: " + mname); continue; }

            // Clone the monster onto the pad (originals untouched).
            // ⚠️ Some parked monsters (Fulgurodonte/Crassorrid) sit with their ROOT inactive on the
            //    stage, so the clone inherits inactive → must force-activate or nothing renders.
            var clone = Object.Instantiate(srcT.gameObject);
            clone.name = "~clone_" + mname;
            clone.SetActive(true);
            clone.transform.position = PadOrigin;
            clone.transform.rotation = Quaternion.Euler(0, 180f, 0); // face -Z toward camera
            trash.Add(clone);

            var rends = clone.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) { Debug.LogWarning("[ToneGate] 렌더러 없음: " + mname); continue; }

            // Combined bounds (after placement).
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);

            // Distance-fit: frame the bounds to the same fraction of the view, whatever the real size.
            float radius = b.extents.magnitude;
            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float dist = (radius / Mathf.Tan(halfFov)) * framing;
            Vector3 aim = b.center;
            float up   = dist * Mathf.Sin(pitch * Mathf.Deg2Rad);
            float back = dist * Mathf.Cos(pitch * Mathf.Deg2Rad);
            camGo.transform.SetPositionAndRotation(
                new Vector3(aim.x, aim.y + up, aim.z - back),
                Quaternion.Euler(pitch, 0f, 0f));

            // -- A = PBR (as-is) --
            Shot(cam, $"{mname}_A_pbr"); shots++;

            // Build toon materials from this monster's albedo.
            var srcMat = rends.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null);
            Texture baseMap = srcMat != null && srcMat.HasProperty("_BaseMap") ? srcMat.GetTexture("_BaseMap") : null;

            var toonB = MakeToon(shader, baseMap, graphic: false);
            ApplyMat(rends, toonB);
            Shot(cam, $"{mname}_B_toon_default"); shots++;

            var toonC = MakeToon(shader, baseMap, graphic: true);
            ApplyMat(rends, toonC);
            Shot(cam, $"{mname}_C_toon_graphic"); shots++;

            Object.DestroyImmediate(toonB);
            Object.DestroyImmediate(toonC);
        }

        // ---------- Wide game-distance reference from the REAL JudgeCam (baseline exposure) ----------
        if (judge != null)
        {
            camGo.transform.SetPositionAndRotation(judge.transform.position, judge.transform.rotation);
            cam.fieldOfView = 50f;
            Shot(cam, "_WIDE_A_pbr", 1920, 1080); shots++;

            var allRends = stage.GetComponentsInChildren<Renderer>(true);
            var allOrig = allRends.Select(r => r.sharedMaterials.ToArray()).ToArray();
            var made = new List<Material>();
            foreach (var r in allRends)
            {
                var sm = r.sharedMaterials.FirstOrDefault(m => m != null);
                Texture bm = sm != null && sm.HasProperty("_BaseMap") ? sm.GetTexture("_BaseMap") : null;
                var mt = MakeToon(shader, bm, graphic: false);
                made.Add(mt);
                var arr = r.sharedMaterials;
                for (int i = 0; i < arr.Length; i++) arr[i] = mt;
                r.sharedMaterials = arr;
            }
            Shot(cam, "_WIDE_B_toon", 1920, 1080); shots++;
            for (int i = 0; i < allRends.Length; i++) allRends[i].sharedMaterials = allOrig[i];
            foreach (var m in made) Object.DestroyImmediate(m);
        }

        // ---------- Teardown ----------
        foreach (var go in trash) if (go != null) Object.DestroyImmediate(go);
        Debug.Log($"[ToneGate] 완료: {shots}컷 → {OutDir}  (씬 미저장, 원본 무변경, 임시 오브젝트 정리됨)");
    }

    // Both presets PRESERVE albedo color (no desaturation) — flatten grays out the saturated
    // accent colors that are the monsters' identity AND the §3 color-canon's point. The toon
    // levers are the RAMP (posterized lighting) and the OUTLINE, not flattening.
    // B (default): 3 bands, soft edge, thin outline — "stylized but faithful".
    // C (graphic): 2 bands, hard edge, thick outline, bright lit pop — "Borderlands-ish".
    static Material MakeToon(Shader sh, Texture baseMap, bool graphic)
    {
        var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        if (baseMap != null) m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", Color.white);   // keep texture albedo as-is (no tint, no flatten target)
        m.SetColor("_ShadeTint", new Color(0.62f, 0.66f, 0.82f)); // cool, lifted shadow
        m.SetColor("_OutlineColor", new Color(0.035f, 0.035f, 0.05f));
        m.SetFloat("_SatFlatten", 0f);           // OFF — preserve color canon
        m.SetFloat("_DetailFlatten", 0f);        // OFF — preserve texture
        if (!graphic)
        {
            m.SetFloat("_RampSteps", 3f); m.SetFloat("_RampSmoothness", 0.07f);
            m.SetFloat("_LitBoost", 1.20f); m.SetFloat("_ShadeFloor", 0.40f);
            m.SetFloat("_OutlineWidth", 1.1f); m.SetFloat("_RimIntensity", 0.5f);
        }
        else
        {
            m.SetFloat("_RampSteps", 2f); m.SetFloat("_RampSmoothness", 0.025f);
            m.SetFloat("_LitBoost", 1.45f); m.SetFloat("_ShadeFloor", 0.42f);
            m.SetFloat("_OutlineWidth", 2.0f); m.SetFloat("_RimIntensity", 0.7f);
        }
        return m;
    }

    static void ApplyMat(Renderer[] rends, Material m)
    {
        foreach (var r in rends)
        {
            var arr = r.sharedMaterials;
            for (int i = 0; i < arr.Length; i++) arr[i] = m;
            r.sharedMaterials = arr;
        }
    }

    static void Shot(Camera cam, string tag) => Render(cam, W, H, tag);
    static void Shot(Camera cam, string tag, int w, int h) => Render(cam, w, h, tag);

    static void Render(Camera cam, int w, int h, string tag)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var req = new RenderPipeline.StandardRequest { destination = rt };
        if (!RenderPipeline.SupportsRenderRequest(cam, req))
        { Debug.LogError("[ToneGate] RenderRequest 미지원"); rt.Release(); Object.DestroyImmediate(rt); return; }
        RenderPipeline.SubmitRenderRequest(cam, req);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(OutDir, tag + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        Debug.Log("[ToneGate] 저장: " + tag + ".png");
    }
}