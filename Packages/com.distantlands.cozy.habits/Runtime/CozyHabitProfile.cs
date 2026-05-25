using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy.Data
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Habits/Cozy Habit", order = 361)]
    public class CozyHabitProfile : ScriptableObject
    {

        public delegate void OnStart();
        public event OnStart onStart;
        public void RaiseOnStart()
        {
            if (onStart != null)
                onStart();
        }
        public delegate void OnEnd();
        public event OnEnd onEnd;
        public void RaiseOnEnd()
        {
            if (onEnd != null)
                onEnd();
        }
        public delegate void OnUpdate();
        public event OnUpdate onUpdate;
        public void RaiseOnUpdate()
        {
            if (onUpdate != null)
                onUpdate();
        }
        public bool isEventRunning;
        public CozyHabits.ModifiedDate startDate;
        public CozyHabits.ModifiedDate endDate;
        public enum RepeatStyle { never, daily, everyOtherDay, weekdays, weekends, weekly, monthly, annually }
        public RepeatStyle repeatStyle;
        public bool allDay;
        public bool overnight;
        public bool runHabitOnStart = true;
        public bool runHabitOnEnd = false;
        public bool runHabitContinuously;
        public bool dateRange;
        [FormatTime]
        public MeridiemTime startTime;
        [FormatTime]
        public MeridiemTime endTime;
        public CozyHabits.Weekday weekday;
        public CozyHabits.Calendar calendar;

        public List<WeatherProfile> cancelIfWeatherIsPlaying;

    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CozyHabitProfile))]
    [CanEditMultipleObjects]
    public class E_CozyHabitProfile : Editor
    {


        CozyHabitProfile t;
        SerializedProperty eventsList;

        void OnEnable()
        {

            t = (CozyHabitProfile)target;


        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("repeatStyle"));
            if (t.repeatStyle == CozyHabitProfile.RepeatStyle.weekly)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("weekday"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("startDate"));
            if ((t.allDay || !t.dateRange) && !t.runHabitContinuously && !t.runHabitOnEnd)
            {
                serializedObject.FindProperty("endDate").FindPropertyRelative("day").intValue = serializedObject.FindProperty("startDate").FindPropertyRelative("day").intValue;
                serializedObject.FindProperty("endDate").FindPropertyRelative("month").intValue = serializedObject.FindProperty("startDate").FindPropertyRelative("month").intValue;
                serializedObject.FindProperty("endDate").FindPropertyRelative("year").intValue = serializedObject.FindProperty("startDate").FindPropertyRelative("year").intValue;
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("endDate"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allDay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dateRange"));
            EditorGUILayout.Space();

            if (!t.allDay)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startTime"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("endTime"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("runHabitOnStart"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("runHabitContinuously"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("runHabitOnEnd"));

            EditorGUILayout.Space();

            SerializedProperty list = serializedObject.FindProperty("cancelIfWeatherIsPlaying");

            list.arraySize = EditorGUILayout.DelayedIntField("Cancel if Weather is Playing", list.arraySize);
            EditorGUI.indentLevel++;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty listItem = list.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(listItem, GUIContent.none);
                if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), HabitsEditorUtility.toolbarButtonIcon))
                {
                    list.InsertArrayElementAtIndex(i);
                }
                if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), HabitsEditorUtility.toolbarButtonIcon))
                {
                    list.DeleteArrayElementAtIndex(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("calendar"));

            if (serializedObject.FindProperty("allDay").boolValue == true)
            {
                t.startTime = new MeridiemTime(0, 0);
                t.endTime = new MeridiemTime(24, 1);
            }

            t.overnight = t.endTime < t.startTime;
            

            serializedObject.ApplyModifiedProperties();

        }

    }
#endif
}