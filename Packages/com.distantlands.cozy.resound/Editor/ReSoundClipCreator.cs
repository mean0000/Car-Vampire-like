using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DistantLands.Cozy.Data;

// [CustomEditor(typeof(AudioClip))]
public class ReSoundClipCreator : Editor
{
    static string path = "Assets";
    [MenuItem("Tools/Cozy: Stylized Weather 3/Create ReSound Tracks")]
    public static void ConvertToReSoundClip()
    {

        int fail = 0;

        path = "Assets" + EditorUtility.SaveFolderPanel("Select a Folder", path, "").Substring(Application.dataPath.Length);

        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("Conversion cancelled");
            return;
        }

        // if (Selection.activeObject.GetType() == typeof(AudioClip))
        foreach (object clip in Selection.objects)
        {
            if (clip.GetType() != typeof(AudioClip))
            {
                fail++;
                continue;
            }

            ReSoundTrack example = CreateInstance<ReSoundTrack>();
            example.clip = (AudioClip)clip;
            example.volume = 1;
            string trackPath = $"{path}/{example.clip.name}.asset";
            AssetDatabase.CreateAsset(example, trackPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = example;

        }
        Debug.LogError($"Successfully converted {Selection.count - fail} with {fail} incompatbile objects.");
    }
}
