using System.Collections.Generic;
using UnityEngine;
using DistantLands.Cozy.Data;
using UnityEditor;
using DistantLands.Cozy.EditorScripts;

namespace DistantLands.Cozy
{
    public class AddNewHabitWizard : EditorWindow
    {
        public Vector2 scrollPos;
        public CozyHabits habitsModule;
        public CozyHabitProfile profile;
        string displayName = "New Habit";
        static string path = "Assets/";
        int selection = 0;
        List<string> options = new List<string>() { "Create New Profile", "Add Existing Profile" };

        public static void OpenWindow(CozyHabits habitsModule)
        {

            AddNewHabitWizard window = (AddNewHabitWizard)EditorWindow.GetWindow(typeof(AddNewHabitWizard), false, "Add Habit");
            window.habitsModule = habitsModule;
            window.minSize = new Vector2(400, 500);
            window.Show();


        }

        private void OnGUI()
        {

            selection = GUILayout.SelectionGrid(selection, options.ToArray(), 2);

            if (selection == 0)
            {
                if (profile == null)
                {

                    profile = ScriptableObject.CreateInstance<CozyHabitProfile>();
                    profile.startDate = habitsModule.currentDay.date;
                    profile.endDate = habitsModule.currentDay.date;

                }

                EditorGUI.indentLevel = 1;

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

                displayName = EditorGUILayout.TextField("Habit Name", displayName);

                Editor.CreateEditor(profile, typeof(E_CozyHabitProfile)).OnInspectorGUI();

                EditorGUILayout.EndScrollView();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save Habit"))
                {

                    path = EditorUtility.SaveFilePanel("Select save location", path, displayName, "asset");
                    AssetDatabase.CreateAsset(profile, path.Substring(Application.dataPath.Length - 6));
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    habitsModule.profile.events.Add((CozyHabitProfile)AssetDatabase.LoadAssetAtPath(path.Substring(Application.dataPath.Length - 6), typeof(CozyHabitProfile)));
                    Close();

                }
            }
            else
            {
                SerializedObject so = new SerializedObject(this);
                so.Update();

                EditorGUILayout.PropertyField(so.FindProperty("profile"));
                so.ApplyModifiedProperties();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add Habit"))
                {

                    habitsModule.profile.events.Add(profile);
                    Close();

                }


            }

        }
    }

    public class EditHabitWizard : EditorWindow
    {
        public Vector2 scrollPos;
        public string profName;
        public CozyHabitProfile profile;

        public static void OpenWindow(CozyHabitProfile profile)
        {

            EditHabitWizard window = (EditHabitWizard)EditorWindow.GetWindow(typeof(EditHabitWizard), false, $"Edit Habit");
            window.profile = profile;
            window.profName = profile.name;
            window.minSize = new Vector2(400, 500);
            window.Show();


        }

        private void OnGUI()
        {

            EditorGUI.indentLevel = 1;

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.BeginHorizontal();
            profName = EditorGUILayout.TextField("Habit Name", profName);
            if (GUILayout.Button("Rename Habit"))
            {
                string assetPath = AssetDatabase.GetAssetPath(profile);
                AssetDatabase.RenameAsset(assetPath, profName);
                AssetDatabase.Refresh();
            }
            EditorGUILayout.EndHorizontal();
            Editor.CreateEditor(profile, typeof(E_CozyHabitProfile)).OnInspectorGUI();

            EditorGUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Select Habit"))
            {
                Selection.activeObject = profile;
                Close();
            }
            if (GUILayout.Button("Remove Habit"))
            {
                HabitsEditorUtility.habitsProfile.events.Remove(profile);
                Close();
            }

        }
    }

    public class ManageHabits : EditorWindow
    {
        public Vector2 scrollPos;
        public CozyHabits habitsModule;

        public static void OpenWindow(CozyHabits habitsModule)
        {

            ManageHabits window = (ManageHabits)EditorWindow.GetWindow(typeof(ManageHabits), false, "Manage Habit");
            window.habitsModule = habitsModule;
            window.minSize = new Vector2(400, 500);
            window.Show();


        }

        private void OnGUI()
        {

            EditorGUI.indentLevel = 1;

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            (Editor.CreateEditor(habitsModule.profile, typeof(E_HabitsYearProfile)) as E_HabitsYearProfile).DrawManageEvents();

            EditorGUILayout.EndScrollView();

        }
    }

    public class HabitsSettings : EditorWindow
    {
        public CozyHabits habitsModule;

        public static void OpenWindow(CozyHabits habitsModule)
        {

            HabitsSettings window = (HabitsSettings)EditorWindow.GetWindow(typeof(HabitsSettings), false, "Habits Settings");
            window.habitsModule = habitsModule;
            window.minSize = new Vector2(400, 500);
            window.Show();


        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("HABITS Settings");
            EditorPrefs.SetInt("CZY_HabitsCalendar", EditorGUILayout.MaskField("Calendars to Show", (int)EditorPrefs.GetInt("CZY_HabitsCalendar"), new string[10] { "Red", "Orange", "Yellow", "Green", "Light Blue", "Dark Blue", "Purple", "Pink", "White", "Grey" }));
            EditorPrefs.SetBool("CZY_HabitsShowTime", EditorGUILayout.Toggle("Show Current Time", EditorPrefs.GetBool("CZY_HabitsShowTime")));
        }
    }

}