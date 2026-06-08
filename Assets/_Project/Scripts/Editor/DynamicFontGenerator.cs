using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// 타이틀/HUD용 한글 폰트를 TextMeshPro Dynamic SDF Font Asset으로 생성한다.
/// MCP로는 TMP 폰트 에셋 쓰기가 막혀서(저장 중 진행바/리로드), 에디터 메뉴로 1회 실행하는 방식.
/// 메뉴: Tools > ZombieCrush > Generate Dynamic Fonts
/// </summary>
public static class DynamicFontGenerator
{
    const string FontDir = "Assets/_Project/Font";

    [MenuItem("Tools/ZombieCrush/Generate Dynamic Fonts")]
    public static void Generate()
    {
        // 이전 MCP 테스트 잔여물 정리
        if (AssetDatabase.LoadAssetAtPath<Object>(FontDir + "/_mcp_write_test.asset") != null)
            AssetDatabase.DeleteAsset(FontDir + "/_mcp_write_test.asset");

        Build(FontDir + "/BlackHanSans-Regular.ttf", FontDir + "/BlackHanSans Dynamic SDF.asset");
        Build(FontDir + "/Galmuri11.ttf", FontDir + "/Galmuri11 Dynamic SDF.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DynamicFontGenerator] 완료 — 동적 SDF 폰트 에셋 생성됨.");
    }

    static void Build(string srcPath, string outPath)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(srcPath);
        if (font == null)
        {
            Debug.LogError("[DynamicFontGenerator] 원본 폰트 없음: " + srcPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null)
            AssetDatabase.DeleteAsset(outPath);

        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(
            font,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 1024,
            atlasHeight: 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fa == null)
        {
            Debug.LogError("[DynamicFontGenerator] CreateFontAsset 실패: " + srcPath);
            return;
        }

        string baseName = Path.GetFileNameWithoutExtension(outPath);
        fa.name = baseName;

        AssetDatabase.CreateAsset(fa, outPath);

        // 아틀라스 텍스처 / 머티리얼을 서브 에셋으로 포함
        if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
        {
            fa.atlasTextures[0].name = baseName + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        }
        if (fa.material != null)
        {
            fa.material.name = baseName + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }

        EditorUtility.SetDirty(fa);
        Debug.Log("[DynamicFontGenerator] 생성: " + outPath);
    }
}
