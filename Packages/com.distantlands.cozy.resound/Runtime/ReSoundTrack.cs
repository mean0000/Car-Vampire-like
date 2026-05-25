using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DistantLands.Cozy;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy.Data
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/ReSound/Track", order = 361)]
    public class ReSoundTrack : ScriptableObject
    {
        [Tooltip("Multiplier for the computational chance that this track will play; 0 being never, and 2 being twice as likely as the average.")]
        [Range(0, 2)]
        public float likelihood = 1;
        public enum ClipType { singleClip, playlist }
        public ClipType clipType = ClipType.singleClip;
        [Tooltip("Animation curves that increase or decrease weather chance based on time, temprature, etc.")]
        public List<ChanceEffector> chances = new List<ChanceEffector>();
        public AudioClip clip;
        public AudioClip[] playlist;
        [Range(0, 1)]
        public float volume = 1;
        public float GetChance(CozyWeather weather, float inTime)
        {
            float i = likelihood;
            foreach (ChanceEffector j in chances)
            {
                i *= j.GetChanceAtTime(weather, inTime);
            }
            return i > 0 ? i : 0;
        }

        public float GetChance(CozyWeather weather)
        {
            float i = likelihood;

            foreach (ChanceEffector j in chances)
            {
                i *= j.GetChance(weather);
            }

            return i > 0 ? i : 0;
        }

    }
#if UNITY_EDITOR
    [CustomEditor(typeof(ReSoundTrack))]
    [CanEditMultipleObjects]
    public class E_ReSoundTrack : Editor
    {

        ReSoundTrack prof;
        public SerializedProperty clipType;
        public SerializedProperty clip;
        public SerializedProperty playlist;
        public SerializedProperty volume;
        public SerializedProperty startTransition;
        public SerializedProperty endTransition;
        public SerializedProperty transitionTime;



        void OnEnable()
        {

            prof = (ReSoundTrack)target;
            clipType = serializedObject.FindProperty("clipType");
            clip = serializedObject.FindProperty("clip");
            playlist = serializedObject.FindProperty("playlist");
            volume = serializedObject.FindProperty("volume");
            startTransition = serializedObject.FindProperty("startTransition");
            endTransition = serializedObject.FindProperty("endTransition");
            transitionTime = serializedObject.FindProperty("transitionTime");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Song Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(clipType);
            EditorGUI.indentLevel++;

            if (prof.clipType == ReSoundTrack.ClipType.singleClip)
                EditorGUILayout.PropertyField(clip);
            else
                EditorGUILayout.PropertyField(playlist);

            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(volume);
            EditorGUILayout.Space();


            EditorGUILayout.LabelField("Playback Behavior", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("likelihood"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("chances"), new GUIContent("Chance Effectors"), true);
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();


        }
    }
#endif
}