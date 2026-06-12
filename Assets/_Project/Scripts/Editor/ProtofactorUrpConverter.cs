using UnityEngine;
using UnityEditor;

/// <summary>
/// Protofactor Monster Full Pack Vol.2의 빌트인 Standard 머티리얼을 URP/Lit으로 일괄 변환한다.
/// 팩은 3.3GB라 gitignore 대상 — 새 머신에서 에셋스토어 재임포트 후 이 메뉴를 한 번 실행해야
/// 마젠타(셰이더 비호환)가 풀린다. 멱등: 이미 URP인 머티리얼은 건너뜀.
/// (2026-06-13 몬스터 파이프라인 세션에서 실행 검증된 변환 로직의 보존본 —
///  docs/01_handoffs/2026-06-13-monster-pipeline-handoff.md 함정 ① 참조)
/// </summary>
public static class ProtofactorUrpConverter
{
    const string PackRoot = "Assets/Protofactor/Monster Full Pack Vol 2";

    [MenuItem("Tools/ZombieCrush/Convert Protofactor Materials to URP")]
    public static void Convert()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[ProtofactorUrpConverter] URP/Lit 셰이더를 찾지 못함 — URP 프로젝트가 맞는지 확인.");
            return;
        }

        int converted = 0, skipped = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PackRoot }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null) continue;
            if (mat.shader.name != "Standard") { skipped++; continue; }

            // Standard 프로퍼티 캡처
            Texture albedo   = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color   color    = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture normal   = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float   bumpScale= mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
            Texture metal    = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
            float   metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float   smooth   = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            Texture occ      = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
            Texture emis     = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color   emisCol  = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            bool    hasEmis  = mat.IsKeywordEnabled("_EMISSION");
            Vector2 tiling   = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset   = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;

            Undo.RecordObject(mat, "Protofactor URP convert");
            mat.shader = urpLit;
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", color);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetTextureOffset("_BaseMap", offset);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.SetFloat("_BumpScale", bumpScale); mat.EnableKeyword("_NORMALMAP"); }
            if (metal != null) { mat.SetTexture("_MetallicGlossMap", metal); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smooth);
            if (occ != null) { mat.SetTexture("_OcclusionMap", occ); mat.EnableKeyword("_OCCLUSIONMAP"); }
            if (hasEmis && (emis != null || emisCol.maxColorComponent > 0f))
            {
                mat.SetTexture("_EmissionMap", emis);
                mat.SetColor("_EmissionColor", emisCol);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            converted++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[ProtofactorUrpConverter] 변환 {converted}건, 스킵(이미 URP 등) {skipped}건 — 완료.");
    }
}
