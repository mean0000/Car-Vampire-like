using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DistantLands.Cozy.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    public class CozyHabitListener : MonoBehaviour
    {

        public CozyHabitProfile habit;
        public UnityEvent startEvent;
        public UnityEvent updateEvent;
        public UnityEvent endEvent;


        public void OnEnable()
        {
            if (habit == null)
                return;

            habit.onStart += startEvent.Invoke;
            habit.onUpdate += updateEvent.Invoke;
            habit.onEnd += endEvent.Invoke;
        }
        public void OnDisable()
        {
            if (habit == null)
                return;

            habit.onStart -= startEvent.Invoke;
            habit.onUpdate -= updateEvent.Invoke;
            habit.onEnd -= endEvent.Invoke;
        }
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(CozyHabitListener))]
    public class E_CozyHabitListener : Editor
    {
        SerializedProperty habit;
        SerializedProperty startEvent;
        SerializedProperty updateEvent;
        SerializedProperty endEvent;
        CozyHabitListener listener;

        void OnEnable()
        {
            habit = serializedObject.FindProperty("habit");
            startEvent = serializedObject.FindProperty("startEvent");
            updateEvent = serializedObject.FindProperty("updateEvent");
            endEvent = serializedObject.FindProperty("endEvent");
            listener = (CozyHabitListener)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(habit);
            if (EditorGUI.EndChangeCheck())
            {
                listener.OnDisable();
                serializedObject.ApplyModifiedProperties();
                listener.OnEnable();
            }

            EditorGUILayout.PropertyField(startEvent);
            EditorGUILayout.PropertyField(updateEvent);
            EditorGUILayout.PropertyField(endEvent);

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}