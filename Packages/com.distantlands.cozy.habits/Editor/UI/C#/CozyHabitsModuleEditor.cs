using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;
using DistantLands.Cozy.Data;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyHabits))]
    public class CozyHabitsModuleEditor : CozyModuleEditor
    {

        CozyHabits module;
        public override ModuleCategory Category => ModuleCategory.time;
        public override string ModuleTitle => "Habits";
        public override string ModuleSubtitle => "Extended Calendar Module";
        public override string ModuleTooltip => "Schedule events, habits, and routines directly in the COZY system.";

        public VisualElement SelectionContainer => root.Q<VisualElement>("selection-container");
        public VisualElement MonthContainer => root.Q<VisualElement>("month-container");
        public VisualElement DailyContainer => root.Q<VisualElement>("daily-container");
        public VisualElement WeekContainer => root.Q<VisualElement>("week-container");


        Button widget;
        VisualElement root;

        void OnEnable()
        {
            if (!target)
                return;

            module = (CozyHabits)target;
        }

        public override Button DisplayWidget()
        {
            widget = SmallWidget();
            Label status = widget.Q<Label>("dynamic-status");
            status.style.fontSize = 8;
            status.text = "";

            return widget;

        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.distantlands.cozy.habits/Editor/UI/UXML/habits-module-editor.uxml"
            );

            asset.CloneTree(root);

            SelectionContainer.Add(new IMGUIContainer(() =>
            {
                if (serializedObject == null) return;
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
                if (serializedObject.hasModifiedProperties)
                {
                    serializedObject.ApplyModifiedProperties();
                    HabitsEditorUtility.habitsProfile = module.profile;
                    module.SetupVariables();
                    if (module.currentDay.date > new CozyHabits.ModifiedDate(module.profile.months[module.profile.months.Count - 1].daysInMonth - 1, module.profile.months.Count - 1, module.currentDay.date.year))
                        module.currentDay.date = new CozyHabits.ModifiedDate(0, 0, 0);
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentDay").FindPropertyRelative("date"));
                if (serializedObject.hasModifiedProperties)
                {
                    serializedObject.ApplyModifiedProperties();
                    module.ChangeDay(0);
                }
                serializedObject.ApplyModifiedProperties();

            }));

            MonthContainer.Add(new IMGUIContainer(() =>
            {
                serializedObject.Update();
                DrawMonth();
                DrawToolbar();
                serializedObject.ApplyModifiedProperties();
            }));

            WeekContainer.Add(new IMGUIContainer(() =>
            {
                serializedObject.Update();
                float width = EditorGUIUtility.currentViewWidth - 60;
                EditorGUILayout.BeginHorizontal();

                for (int i = 0; i < serializedObject.FindProperty("currentWeek").arraySize; i++)
                {
                    serializedObject.FindProperty("currentWeek").GetArrayElementAtIndex(i).FindPropertyRelative("weekday").intValue = i;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("currentWeek").GetArrayElementAtIndex(i), GUILayout.Width(width / 7));
                }

                EditorGUILayout.EndHorizontal();
                DrawToolbar();
                serializedObject.ApplyModifiedProperties();
            }));

            DailyContainer.Add(new IMGUIContainer(() =>
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentDay"));
                DrawToolbar();
                serializedObject.ApplyModifiedProperties();
            }));

            return root;

        }

        void DrawToolbar()
        {

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), HabitsEditorUtility.toolbarButtonIcon))
            {
                AddNewHabitWizard.OpenWindow(module);
            }
            if (GUILayout.Button(EditorGUIUtility.IconContent("_Menu@2x"), HabitsEditorUtility.toolbarButtonIcon))
            {
                ManageHabits.OpenWindow(module);
            }
            if (GUILayout.Button(EditorGUIUtility.IconContent("_Popup@2x"), HabitsEditorUtility.toolbarButtonIcon))
            {
                HabitsSettings.OpenWindow(module);
            }
            EditorGUILayout.EndHorizontal();
        }

        public void DrawMonth()
        {
            float width = EditorGUIUtility.currentViewWidth - 70;


            GUIStyle titleStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
            {
                fontStyle = FontStyle.Bold,
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 25
            };

            GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = width / 7,
                stretchWidth = false,
                margin = new RectOffset(5, 5, 5, 5),
                border = new RectOffset(0, 0, 0, 0)
            };

            GUIStyle iconStyle = new GUIStyle(GUI.skin.GetStyle("Button"))
            {
                fixedHeight = width / 7,
                fixedWidth = width / 7,
                margin = new RectOffset(5, 5, 5, 5),
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold
            };

            EditorGUILayout.Space();

            List<GUIContent> monthNames = new List<GUIContent>();

            for (int i = 0; i < module.profile.months.Count; i++)
            {
                monthNames.Add(new GUIContent(module.profile.months[i].displayName));
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), HabitsEditorUtility.nextPreviousButtonStyle))
            {
                module.currentDay.date -= module.profile.months[CozyHabits.ClampWithLoop(module.currentDay.date.month - 1, 0, module.profile.months.Count - 1)].daysInMonth;
                serializedObject.ApplyModifiedProperties();
                module.ChangeDay(0);
            }
            module.currentDay.date.month = EditorGUILayout.Popup(GUIContent.none, module.currentDay.date.month, monthNames.ToArray(), titleStyle);
            if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), HabitsEditorUtility.nextPreviousButtonStyle))
            {
                module.currentDay.date += module.profile.months[module.currentDay.date.month].daysInMonth;
                serializedObject.ApplyModifiedProperties();
                module.ChangeDay(0);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("S", labelStyle);
            GUILayout.Label("M", labelStyle);
            GUILayout.Label("T", labelStyle);
            GUILayout.Label("W", labelStyle);
            GUILayout.Label("T", labelStyle);
            GUILayout.Label("F", labelStyle);
            GUILayout.Label("S", labelStyle);
            EditorGUILayout.EndHorizontal();


            List<GUIContent> icons = new List<GUIContent>();

            for (int i = 0; i < (int)module.currentMonth[0].weekday; i++)
            {

                icons.Add(GUIContent.none);


            }

            for (int i = 0; i < module.currentMonth.Length; i++)
            {

                CozyHabits.Day day = module.currentMonth[i];

                icons.Add(new GUIContent($"{i + 1}\n{day.events.Length} Events"));


            }

            for (int i = 0; i < 6 - (int)module.currentMonth[module.currentMonth.Length - 1].weekday; i++)
            {

                icons.Add(GUIContent.none);


            }

            int dayNumber = serializedObject.FindProperty("currentDay").FindPropertyRelative("date").FindPropertyRelative("day").intValue + (int)module.currentMonth[0].weekday;
            int j = GUILayout.SelectionGrid(dayNumber, icons.ToArray(), 7, iconStyle);
            serializedObject.FindProperty("currentDay").FindPropertyRelative("date").FindPropertyRelative("day").intValue = j - (int)module.currentMonth[0].weekday;

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                module.ChangeDay(0);
            }

        }


        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/habits-module");
        }


    }
}