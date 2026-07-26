using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 속도 언어 랩 배선(07-08) — 현재 열린 씬이 RunFeel_Whitebox일 때만 SpeedLanguageDirector를 심고 저장.
/// ⚠️씬 오픈은 여기서 하지 않는다(MCP 함정: OpenScene과 배선은 커맨드 분리 — 07-05 기록).
/// 사용: 씬을 먼저 열고 → 메뉴 ZombieCrush/Labs/Wire SpeedLanguageDirector (RunFeel).
/// </summary>
public static class SpeedLanguageLabWiring
{
    const string ScenePath = "Assets/_Project/Scenes/Labs/RunFeel_Whitebox.unity";

    [MenuItem("ZombieCrush/Labs/Wire SpeedLanguageDirector (RunFeel)")]
    public static void Wire()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError($"[SpeedLanguageLabWiring] 열린 씬이 RunFeel_Whitebox가 아님({scene.path}) — 씬을 먼저 열고 재실행.");
            return;
        }

        // ★Codex M-2: GameObject.Find는 로드된 타 씬까지 뒤진다(멀티씬에서 배선 없이 저장만 하는 헛발) — 대상 씬 루트만 검색
        GameObject go = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "SpeedLanguageDirector") { go = root; break; }
        if (go == null)
        {
            go = new GameObject("SpeedLanguageDirector");
            Undo.RegisterCreatedObjectUndo(go, "Create SpeedLanguageDirector");
        }
        if (go.GetComponent<SpeedLanguageDirector>() == null)
            go.AddComponent<SpeedLanguageDirector>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SpeedLanguageLabWiring] RunFeel_Whitebox에 SpeedLanguageDirector 배선 + 저장 완료.");
    }
}
