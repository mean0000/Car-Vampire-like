using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy.Data
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Habits/Habits Year", order = 361)]
    public class HabitsYearProfile : ScriptableObject
    {

        public List<CozyHabitProfile> events = new List<CozyHabitProfile>();

        public CozyHabits.Weekday startDay;


        public List<Month> months;
        [System.Serializable]
        public struct Month
        {
            public string displayName;
            public int daysInMonth;

        }


        public int GetDayOfMonth(int day)
        {

            int modifiedDay = day;
            int i = 0;

            while (day > months[i].daysInMonth)
            {
                day -= months[i].daysInMonth;
                i++;
                if (i == months.Count)
                    i = 0;
            }

            return modifiedDay;
        }

        public int GetYearLength()
        {
            int i = 0;

            foreach (Month j in months)
                i += j.daysInMonth;

            return i;
        }


    }
#if UNITY_EDITOR
    [CustomEditor(typeof(HabitsYearProfile))]
    [CanEditMultipleObjects]
    public class E_HabitsYearProfile : Editor
    {


        SerializedProperty eventsList;

        void OnEnable()
        {

            eventsList = serializedObject.FindProperty("events");


        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("months"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("startDay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("events"));

            serializedObject.ApplyModifiedProperties();

        }
        public void DrawManageEvents()
        {

            serializedObject.Update();
            eventsList.arraySize = EditorGUILayout.DelayedIntField("Events", eventsList.arraySize);
            EditorGUI.indentLevel++;

            for (int i = 0; i < eventsList.arraySize; i++)
            {
                SerializedProperty listItem = eventsList.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(listItem, GUIContent.none);
                if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), HabitsEditorUtility.toolbarButtonIcon))
                {
                    eventsList.InsertArrayElementAtIndex(i);
                }
                if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), HabitsEditorUtility.toolbarButtonIcon))
                {
                    eventsList.DeleteArrayElementAtIndex(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            serializedObject.ApplyModifiedProperties();

        }

    }
#endif
}