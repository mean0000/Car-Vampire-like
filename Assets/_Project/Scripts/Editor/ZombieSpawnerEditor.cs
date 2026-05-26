using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ZombieSpawner))]
public class ZombieSpawnerEditor : Editor
{
    const string PrefabFolder = "Assets/ithappy/Zombies_Pack/Prefabs/Zombies_Demo";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Prefab Distribution", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto-Distribute Prefabs (Shuffle & Split 20/20/20)"))
        {
            AutoDistribute();
        }
    }

    void AutoDistribute()
    {
        // 폴더 내 프리팹 GUID 수집
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[ZombieSpawnerEditor] {PrefabFolder} 에서 프리팹을 찾지 못했습니다.");
            return;
        }

        // 경로 → 이름 기준 정렬
        var paths = new List<string>();
        foreach (string guid in guids)
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.OrdinalIgnoreCase);

        // 로드
        var prefabs = new List<GameObject>();
        foreach (string path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) prefabs.Add(go);
        }

        if (prefabs.Count == 0)
        {
            Debug.LogWarning("[ZombieSpawnerEditor] 로드 가능한 프리팹이 없습니다.");
            return;
        }

        // Fisher-Yates 셔플
        for (int i = prefabs.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (prefabs[i], prefabs[j]) = (prefabs[j], prefabs[i]);
        }

        // 3그룹 균등 분배
        int total = prefabs.Count;
        int groupSize = total / 3;
        int remainder = total % 3;

        // 나머지는 앞 그룹부터 1개씩 추가
        int rangedCount  = groupSize + (remainder > 0 ? 1 : 0);
        int chargerCount = groupSize + (remainder > 1 ? 1 : 0);
        int laserCount   = total - rangedCount - chargerCount;

        var ranged  = prefabs.GetRange(0, rangedCount).ToArray();
        var charger = prefabs.GetRange(rangedCount, chargerCount).ToArray();
        var laser   = prefabs.GetRange(rangedCount + chargerCount, laserCount).ToArray();

        // Undo 등록 후 SerializedObject로 배열 할당
        Undo.RecordObject(target, "Auto-Distribute Zombie Prefabs");

        SerializedObject so = new SerializedObject(target);
        AssignArray(so.FindProperty("rangedPrefabs"),  ranged);
        AssignArray(so.FindProperty("chargerPrefabs"), charger);
        AssignArray(so.FindProperty("laserPrefabs"),   laser);
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(target);
        Debug.Log($"[ZombieSpawnerEditor] 배분 완료 — Ranged:{ranged.Length} Charger:{charger.Length} Laser:{laser.Length} (총 {total}개)");
    }

    static void AssignArray(SerializedProperty arrayProp, GameObject[] items)
    {
        arrayProp.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
