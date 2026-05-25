using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace DistantLands.Cozy.Data
{

    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/ReSound/Setlist", order = 361)]
    public class ReSoundSetlist : ScriptableObject
    {

        [Tooltip("The list of ReSound tracks that will play on this setlist.")]
        public List<ReSoundTrack> availableTracks;
        public enum ProgressionMode { weightedRandom, random, progression }
        public ProgressionMode progressionMode;
        public enum StartingStyle { startWithRandomSong, startWithInitialSong }
        public StartingStyle startingStyle;

        [Tooltip("The ReSound track that will be played initially.")]
        public ReSoundTrack initialSong;
        public float minSilenceTime;
        public float maxSilenceTime;
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(ReSoundSetlist))]
    [CanEditMultipleObjects]
    public class E_ReSoundSetlist : Editor
    {

        SerializedProperty availableTracks;
        SerializedProperty initialSong;
        SerializedProperty startingStyle;
        SerializedProperty progressionMode;
        SerializedProperty minSilenceTime;
        SerializedProperty maxSilenceTime;
        ReSoundSetlist prof;
        Vector2 scrollPos;

        void OnEnable()
        {
            availableTracks = serializedObject.FindProperty("availableTracks");
            initialSong = serializedObject.FindProperty("initialSong");
            startingStyle = serializedObject.FindProperty("startingStyle");
            progressionMode = serializedObject.FindProperty("progressionMode");
            minSilenceTime = serializedObject.FindProperty("minSilenceTime");
            maxSilenceTime = serializedObject.FindProperty("maxSilenceTime");
            prof = (ReSoundSetlist)target;

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(startingStyle);
            if (prof.startingStyle == ReSoundSetlist.StartingStyle.startWithInitialSong)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(initialSong);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(progressionMode);
            EditorGUILayout.PropertyField(minSilenceTime);
            EditorGUILayout.PropertyField(maxSilenceTime);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(availableTracks);

            serializedObject.ApplyModifiedProperties();


        }

    }
#endif
}