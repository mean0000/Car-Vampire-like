using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy.Data
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/ReSound/FX", order = 361)]
    public class ReSoundFX : FXProfile
    {
        public ReSoundTrack track;
        ReSoundModule module;

        public override void PlayEffect(float weight)
        {

            if (!module)
                if (InitializeEffect(null) == false)
                    return;

            module.fXes[this] = weight;
            module.fxWeight += weight;

        }

        public override bool InitializeEffect(CozyWeather weather)
        {

            base.InitializeEffect(weather);

            if (!weatherSphere.GetModule<ReSoundModule>())
                return false;

            module = weatherSphere.GetModule<ReSoundModule>();

            if (!module.fXes.ContainsKey(this))
                module.fXes.Add(this, 0);

            return true;

        }



    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ReSoundFX))]
    [CanEditMultipleObjects]
    public class E_ReSoundFX : E_FXProfile
    {

        SerializedProperty track;
        SerializedProperty transitionTimeModifier;

        void OnEnable()
        {
            track = serializedObject.FindProperty("track");
            transitionTimeModifier = serializedObject.FindProperty("transitionTimeModifier");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(track);
            EditorGUILayout.PropertyField(transitionTimeModifier);

            serializedObject.ApplyModifiedProperties();

        }

        public override void RenderInWindow(Rect pos)
        {

            float space = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var propPosA = new Rect(pos.x, pos.y + space, pos.width, EditorGUIUtility.singleLineHeight);
            var propPosB = new Rect(pos.x, pos.y + space * 2, pos.width, EditorGUIUtility.singleLineHeight);

            serializedObject.Update();

            EditorGUI.PropertyField(propPosA, track);
            EditorGUI.PropertyField(propPosB, transitionTimeModifier);

            serializedObject.ApplyModifiedProperties();
        }

        public override float GetLineHeight()
        {

            return 2;

        }

    }
#endif
}