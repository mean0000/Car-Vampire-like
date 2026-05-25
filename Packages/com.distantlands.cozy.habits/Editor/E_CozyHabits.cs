using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DistantLands.Cozy.Data;
using UnityEditor;
using System;

namespace DistantLands.Cozy
{

    // [CustomEditor(typeof(CozyHabits))]
    // [CanEditMultipleObjects]
    // public class E_CozyHabits : E_CozyModule
    // {

    //     CozyHabits habits;
    //     SerializedProperty eventsList;

    //     public override GUIContent GetGUIContent()
    //     {

    //         //Place your module's GUI content here.
    //         return new GUIContent("    Habits", (Texture)Resources.Load("Habits"), "Scehdule events, habits, and routines directly in the COZY system.");

    //     }

    //     public override void OpenDocumentationURL()
    //     {
    //         Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/habits-module");
    //     }

    //     void OnEnable()
    //     {
    //         if (target.GetType() != typeof(CozyHabits))
    //             return;
    //         habits = (CozyHabits)target;
    //         eventsList = new SerializedObject(habits.profile).FindProperty("events");
    //     }

    //     public override void DisplayInCozyWindow()
    //     {
    //         serializedObject.Update();

    //         serializedObject.FindProperty("selection").boolValue = EditorGUILayout.BeginFoldoutHeaderGroup(serializedObject.FindProperty("selection").boolValue, "    Selection Settings", EditorUtilities.FoldoutStyle);
    //         EditorGUILayout.EndFoldoutHeaderGroup();

    //         if (serializedObject.FindProperty("selection").boolValue)
    //         {
    //             EditorGUI.indentLevel++;

    //             EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
    //             if (serializedObject.hasModifiedProperties)
    //             {
    //                 serializedObject.ApplyModifiedProperties();
    //                 HabitsEditorUtility.habitsProfile = habits.profile;
    //                 habits.SetupVariables();
    //                 if (habits.currentDay.date > new CozyHabits.ModifiedDate(habits.profile.months[habits.profile.months.Count - 1].daysInMonth - 1, habits.profile.months.Count - 1, habits.currentDay.date.year))
    //                     habits.currentDay.date = new CozyHabits.ModifiedDate(0, 0, 0);
    //             }
    //             EditorGUILayout.PropertyField(serializedObject.FindProperty("currentDay").FindPropertyRelative("date"));
    //             if (serializedObject.hasModifiedProperties)
    //             {
    //                 serializedObject.ApplyModifiedProperties();
    //                 habits.ChangeDay(0);
    //             }

    //             EditorGUI.indentLevel--;
    //         }

    //         float width = EditorGUIUtility.currentViewWidth - 60;

    //         GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
    //         {
    //             fontStyle = FontStyle.Bold,
    //             alignment = TextAnchor.MiddleCenter
    //         };

    //         serializedObject.FindProperty("monthView").boolValue = EditorGUILayout.BeginFoldoutHeaderGroup(serializedObject.FindProperty("monthView").boolValue, "    Month View", EditorUtilities.FoldoutStyle);
    //         EditorGUILayout.EndFoldoutHeaderGroup();
    //         if (serializedObject.FindProperty("monthView").boolValue)
    //         {
    //             DrawMonth();
    //             DrawToolbar();
    //         }


    //         serializedObject.FindProperty("weekView").boolValue = EditorGUILayout.BeginFoldoutHeaderGroup(serializedObject.FindProperty("weekView").boolValue, "    Week View", EditorUtilities.FoldoutStyle);
    //         EditorGUILayout.EndFoldoutHeaderGroup();
    //         if (serializedObject.FindProperty("weekView").boolValue)
    //         {
    //             EditorGUILayout.BeginHorizontal();

    //             for (int i = 0; i < serializedObject.FindProperty("currentWeek").arraySize; i++)
    //             {
    //                 serializedObject.FindProperty("currentWeek").GetArrayElementAtIndex(i).FindPropertyRelative("weekday").intValue = i;
    //                 EditorGUILayout.PropertyField(serializedObject.FindProperty("currentWeek").GetArrayElementAtIndex(i), GUILayout.Width(width / 7));
    //             }

    //             EditorGUILayout.EndHorizontal();
    //             DrawToolbar();
    //         }

    //         serializedObject.FindProperty("dayView").boolValue = EditorGUILayout.BeginFoldoutHeaderGroup(serializedObject.FindProperty("dayView").boolValue, "    Day View", EditorUtilities.FoldoutStyle);
    //         EditorGUILayout.EndFoldoutHeaderGroup();
    //         if (serializedObject.FindProperty("dayView").boolValue)
    //         {
    //             EditorGUILayout.PropertyField(serializedObject.FindProperty("currentDay"));
    //             DrawToolbar();
    //         }


    //         serializedObject.ApplyModifiedProperties();

    //     }

    //     void DrawToolbar()
    //     {

    //         EditorGUILayout.BeginHorizontal();
    //         GUILayout.FlexibleSpace();
    //         if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), HabitsEditorUtility.toolbarButtonIcon))
    //         {
    //             AddNewHabitWizard.OpenWindow(habits);
    //         }
    //         if (GUILayout.Button(EditorGUIUtility.IconContent("_Menu@2x"), HabitsEditorUtility.toolbarButtonIcon))
    //         {
    //             ManageHabits.OpenWindow(habits);
    //         }
    //         if (GUILayout.Button(EditorGUIUtility.IconContent("_Popup@2x"), HabitsEditorUtility.toolbarButtonIcon))
    //         {
    //             HabitsSettings.OpenWindow(habits);
    //         }
    //         EditorGUILayout.EndHorizontal();
    //     }

    //     public void DrawMonth()
    //     {
    //         float width = EditorGUIUtility.currentViewWidth - 70;


    //         GUIStyle titleStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
    //         {
    //             fontStyle = FontStyle.Bold,
    //             fontSize = 20,
    //             alignment = TextAnchor.MiddleCenter,
    //             fixedHeight = 25
    //         };

    //         GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
    //         {
    //             fontStyle = FontStyle.Bold,
    //             alignment = TextAnchor.MiddleCenter,
    //             fixedWidth = width / 7,
    //             stretchWidth = false,
    //             margin = new RectOffset(5, 5, 5, 5),
    //             border = new RectOffset(0, 0, 0, 0)
    //         };

    //         GUIStyle iconStyle = new GUIStyle(GUI.skin.GetStyle("Button"))
    //         {
    //             fixedHeight = width / 7,
    //             fixedWidth = width / 7,
    //             margin = new RectOffset(5, 5, 5, 5),
    //             alignment = TextAnchor.UpperLeft,
    //             fontStyle = FontStyle.Bold
    //         };

    //         EditorGUILayout.Space();

    //         List<GUIContent> monthNames = new List<GUIContent>();

    //         for (int i = 0; i < habits.profile.months.Count; i++)
    //         {
    //             monthNames.Add(new GUIContent(habits.profile.months[i].displayName));
    //         }

    //         EditorGUILayout.BeginHorizontal();
    //         if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), HabitsEditorUtility.nextPreviousButtonStyle))
    //         {
    //             habits.currentDay.date -= habits.profile.months[CozyHabits.ClampWithLoop(habits.currentDay.date.month - 1, 0, habits.profile.months.Count - 1)].daysInMonth;
    //             serializedObject.ApplyModifiedProperties();
    //             habits.ChangeDay(0);
    //         }
    //         habits.currentDay.date.month = EditorGUILayout.Popup(GUIContent.none, habits.currentDay.date.month, monthNames.ToArray(), titleStyle);
    //         if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), HabitsEditorUtility.nextPreviousButtonStyle))
    //         {
    //             habits.currentDay.date += habits.profile.months[habits.currentDay.date.month].daysInMonth;
    //             serializedObject.ApplyModifiedProperties();
    //             habits.ChangeDay(0);
    //         }

    //         EditorGUILayout.EndHorizontal();

    //         EditorGUILayout.BeginHorizontal();

    //         GUILayout.Label("S", labelStyle);
    //         GUILayout.Label("M", labelStyle);
    //         GUILayout.Label("T", labelStyle);
    //         GUILayout.Label("W", labelStyle);
    //         GUILayout.Label("T", labelStyle);
    //         GUILayout.Label("F", labelStyle);
    //         GUILayout.Label("S", labelStyle);
    //         EditorGUILayout.EndHorizontal();


    //         List<GUIContent> icons = new List<GUIContent>();

    //         for (int i = 0; i < (int)habits.currentMonth[0].weekday; i++)
    //         {

    //             icons.Add(GUIContent.none);


    //         }

    //         for (int i = 0; i < habits.currentMonth.Length; i++)
    //         {

    //             CozyHabits.Day day = habits.currentMonth[i];

    //             icons.Add(new GUIContent($"{i + 1}\n{day.events.Length} Events"));


    //         }

    //         for (int i = 0; i < 6 - (int)habits.currentMonth[habits.currentMonth.Length - 1].weekday; i++)
    //         {

    //             icons.Add(GUIContent.none);


    //         }

    //         int dayNumber = serializedObject.FindProperty("currentDay").FindPropertyRelative("date").FindPropertyRelative("day").intValue + (int)habits.currentMonth[0].weekday;
    //         int j = GUILayout.SelectionGrid(dayNumber, icons.ToArray(), 7, iconStyle);
    //         serializedObject.FindProperty("currentDay").FindPropertyRelative("date").FindPropertyRelative("day").intValue = j - (int)habits.currentMonth[0].weekday;

    //         if (serializedObject.hasModifiedProperties)
    //         {
    //             serializedObject.ApplyModifiedProperties();
    //             habits.ChangeDay(0);
    //         }

    //     }


    // }

    [UnityEditor.CustomPropertyDrawer(typeof(WeekdayAttribute))]
    public class WeekdayPropertyDrawer : PropertyDrawer
    {

        WeekdayAttribute weekday;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            weekday = (WeekdayAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);


            var titleRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var backArrow = new Rect(position.x, position.y, 30, EditorGUIUtility.singleLineHeight);
            var forwardArrow = new Rect(position.x + position.width - 30, position.y, 30, EditorGUIUtility.singleLineHeight);
            var scheduleRect = new Rect(position.x, position.y + 5 + EditorGUIUtility.singleLineHeight, position.width, position.height - (5 + EditorGUIUtility.singleLineHeight));
            var toolbarRect = new Rect(position.x, position.y + 5 + (EditorGUIUtility.singleLineHeight * 13), position.width, 20);
            var toolbarButton1Rect = new Rect(position.x + position.width - 20, position.y + 5 + (EditorGUIUtility.singleLineHeight * 13), 20, 20);
            var toolbarButton2Rect = new Rect(position.x + position.width - 42, position.y + 5 + (EditorGUIUtility.singleLineHeight * 13), 20, 20);
            var eventRect = new Rect(position.x, position.y + 10 + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight * 2);
            var timeRect = new Rect(position.x, position.y + 5 + EditorGUIUtility.singleLineHeight, 100, position.height);
            var currentTimeRect = new Rect(position.x, position.y + position.height * HabitsEditorUtility.habits.weatherSphere.timeModule.currentTime, position.width, 2);


            EditorGUI.DrawRect(titleRect, new Color(0.2f, 0.2f, 0.2f));

            GUIStyle titleStyle = new GUIStyle(GUI.skin.GetStyle("BoldLabel"));
            titleStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle eventStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            eventStyle.border = new RectOffset(0, 0, 0, 0);
            eventStyle.margin = new RectOffset(0, 0, 0, 0);
            eventStyle.alignment = TextAnchor.UpperLeft;
            eventStyle.contentOffset = Vector2.zero;
            eventStyle.fixedHeight = 25;
            eventStyle.normal.textColor = Color.white;

            SerializedProperty date = property.FindPropertyRelative("date");
            if (weekday.highlightCurrentDay)
                if (HabitsEditorUtility.habits.currentDay.date.day == date.FindPropertyRelative("day").intValue && HabitsEditorUtility.habits.currentDay.date.month == date.FindPropertyRelative("month").intValue && HabitsEditorUtility.habits.currentDay.date.year == date.FindPropertyRelative("year").intValue)
                {
                    titleStyle.normal.textColor = Color.white;
                    titleStyle.fontStyle = FontStyle.Bold;
                }

            if (weekday.labelTime)
            {
                if (GUI.Button(backArrow, EditorGUIUtility.IconContent("Toolbar Minus")))
                    HabitsEditorUtility.habits.ChangeDay(-1);
                if (GUI.Button(forwardArrow, EditorGUIUtility.IconContent("Toolbar Plus")))
                    HabitsEditorUtility.habits.ChangeDay(1);
            }

            switch (weekday.titleStyle)
            {
                case (WeekdayAttribute.TitleStyle.day):
                    EditorGUI.LabelField(titleRect, $"{date.FindPropertyRelative("day").intValue + 1}/{date.FindPropertyRelative("month").intValue + 1}/{date.FindPropertyRelative("year").intValue}", titleStyle);
                    break;
                case (WeekdayAttribute.TitleStyle.weekdayInitial):
                    EditorGUI.LabelField(titleRect, $"{CozyHabits.GetWeekdayNameFromInt(property.FindPropertyRelative("weekday").intValue)}", titleStyle);
                    break;
                case (WeekdayAttribute.TitleStyle.weekday):
                    EditorGUI.LabelField(titleRect, HabitsEditorUtility.CapitalizeFirstLetter(((CozyHabits.Weekday)property.FindPropertyRelative("weekday").intValue).ToString()), titleStyle);
                    break;
                case (WeekdayAttribute.TitleStyle.fullDayName):
                    EditorGUI.LabelField(titleRect, $"{HabitsEditorUtility.CapitalizeFirstLetter(((CozyHabits.Weekday)property.FindPropertyRelative("weekday").intValue).ToString())}, {HabitsEditorUtility.habitsProfile.months[date.FindPropertyRelative("month").intValue].displayName} {date.FindPropertyRelative("day").intValue + 1}, {date.FindPropertyRelative("year").intValue}", titleStyle);
                    break;
            }
            if (GUI.Button(titleRect, GUIContent.none, eventStyle))
            {

                HabitsEditorUtility.habits.currentDay.date = new CozyHabits.ModifiedDate(date.FindPropertyRelative("day").intValue, date.FindPropertyRelative("month").intValue, date.FindPropertyRelative("year").intValue);
                HabitsEditorUtility.habits.ChangeDay(0);

            }

            EditorGUI.DrawRect(scheduleRect, new Color(0.12f, 0.12f, 0.12f));

            if (weekday.labelTime)
            {

                GUIStyle timeLabelStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
                timeLabelStyle.border = new RectOffset(0, 0, 0, 0);
                timeLabelStyle.margin = new RectOffset(0, 0, 0, 0);
                timeLabelStyle.alignment = TextAnchor.UpperRight;
                timeLabelStyle.fontStyle = FontStyle.Bold;
                timeLabelStyle.contentOffset = Vector2.zero;
                timeLabelStyle.fixedHeight = 25;
                timeLabelStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

                EditorGUI.LabelField(new Rect(scheduleRect.x, scheduleRect.y + scheduleRect.height * 0.25f - 18, scheduleRect.width, 15), "6 AM", timeLabelStyle);
                EditorGUI.LabelField(new Rect(scheduleRect.x, scheduleRect.y + scheduleRect.height * 0.5f - 18, scheduleRect.width, 15), "Noon", timeLabelStyle);
                EditorGUI.LabelField(new Rect(scheduleRect.x, scheduleRect.y + scheduleRect.height * 0.75f - 18, scheduleRect.width, 15), "6 PM", timeLabelStyle);

                EditorGUI.DrawRect(new Rect(scheduleRect.x, scheduleRect.y + scheduleRect.height * 0.25f - 1, scheduleRect.width, 3), new Color(0.5f, 0.5f, 0.5f));
                EditorGUI.DrawRect(new Rect(scheduleRect.x, scheduleRect.y + scheduleRect.height * 0.5f - 1, scheduleRect.width, 3), new Color(0.5f, 0.5f, 0.5f));
                EditorGUI.DrawRect(new Rect(scheduleRect.x, scheduleRect.y + scheduleRect.height * 0.75f - 1, scheduleRect.width, 3), new Color(0.5f, 0.5f, 0.5f));

            }
            // EditorGUI.DrawRect(toolbarRect, new Color(0.25f, 0.25f, 0.25f));
            // EditorGUI.LabelField(titleRect, property.FindPropertyRelative("displayName").stringValue, HabitsEditorUtility.weekdayLabelStyle);
            // GUI.Button(toolbarButton1Rect, EditorGUIUtility.IconContent("_Popup@2x"), HabitsEditorUtility.toolbarButtonIcon);


            SerializedProperty events = property.FindPropertyRelative("events");

            Dictionary<int, Vector2Int> habitsAtTime = new Dictionary<int, Vector2Int>();
            CozyHabits.Calendar calendar = (CozyHabits.Calendar)Enum.ToObject(typeof(CozyHabits.Calendar), EditorPrefs.GetInt("CZY_HabitsCalendar"));

            for (int i = 0; i < events.arraySize; i++)
            {
                CozyHabitProfile prof = (CozyHabitProfile)events.GetArrayElementAtIndex(i).objectReferenceValue;

                if (prof == null)
                    continue;

                if (!calendar.HasFlag(prof.calendar))
                    continue;

                if (habitsAtTime.ContainsKey(prof.startTime.hours))
                    habitsAtTime[prof.startTime.hours] += Vector2Int.right;
                else
                    habitsAtTime.Add(prof.startTime.hours, Vector2Int.right);

            }

            for (int i = 0; i < events.arraySize; i++)
            {

                SerializedProperty prop = events.GetArrayElementAtIndex(i);

                CozyHabitProfile prof = (CozyHabitProfile)prop.objectReferenceValue;

                if (prof == null)
                    continue;

                if (!calendar.HasFlag(prof.calendar))
                    continue;

                float startPercentage = prof.startTime;
                float endPercentage = prof.endTime;
                Vector2Int key = habitsAtTime[prof.startTime.hours];
                Color color = CozyHabits.CalendarColor(prof.calendar);
                Color darkColor = new Color(color.r, color.g, color.b, 0.5f);


                float offset = key.y * scheduleRect.width / key.x; //(2 * CozyHabits.ClampWithLoop(i, 0, 3));

                Rect habitRect;
                Rect boxRect;
                Rect startLine;
                Rect continuedLine;

                if (startPercentage > endPercentage)
                {
                    habitRect = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, scheduleRect.height - (scheduleRect.height * startPercentage) + 1);
                    boxRect = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage),
                                       (scheduleRect.width / key.x), (scheduleRect.height) - (scheduleRect.height * startPercentage) + 1);
                    startLine = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, 2);
                    continuedLine = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), 2, scheduleRect.height - (scheduleRect.height * startPercentage) + 1);

                    EditorGUI.DrawRect(new Rect(scheduleRect.x - offset, scheduleRect.y, 2, (scheduleRect.height * endPercentage) + 1), color);
                    EditorGUI.DrawRect(new Rect(scheduleRect.x - offset, scheduleRect.y,
                                       (scheduleRect.width / key.x), (scheduleRect.height * endPercentage) + 1), darkColor);

                }
                else
                {
                    if (endPercentage - startPercentage > 1.0f / 24)
                    {
                        habitRect = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, (scheduleRect.height * endPercentage) - (scheduleRect.height * startPercentage) + 1);
                        boxRect = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage),
                                           (scheduleRect.width / key.x), (scheduleRect.height * endPercentage) - (scheduleRect.height * startPercentage) + 1);
                        startLine = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, 2);
                        continuedLine = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), 2, (scheduleRect.height * endPercentage) - (scheduleRect.height * startPercentage) + 1);

                    }
                    else
                    {
                        habitRect = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, 25);
                        boxRect = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, 25);
                        startLine = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, 2);
                        continuedLine = new Rect(scheduleRect.x - offset, scheduleRect.y + (scheduleRect.height * startPercentage), 2, 25);
                    }
                }

                Rect habitNameRect = new Rect(boxRect.x + 3, scheduleRect.y + 5 + (scheduleRect.height * startPercentage), scheduleRect.width / key.x, 10);
                Rect habitEditRect = new Rect(habitRect.x + habitRect.width / key.x - 15, habitNameRect.y + 2, 15, 15);

                GUIStyle toolbarButtonStyle = HabitsEditorUtility.toolbarButtonIcon;

                EditorGUI.DrawRect(startLine, color);
                EditorGUI.DrawRect(continuedLine, color);
                EditorGUI.DrawRect(boxRect, darkColor);
                EditorGUI.LabelField(habitNameRect, $"{prof.name} - {prof.startTime.ToString()}", eventStyle);
                if (GUI.Button(habitNameRect, GUIContent.none, eventStyle))
                {
                    EditHabitWizard.OpenWindow(prof);
                }

                habitsAtTime[prof.startTime.hours] -= Vector2Int.up;

            }

            if (EditorPrefs.GetBool("CZY_HabitsShowTime"))
                EditorGUI.DrawRect(currentTimeRect, Color.white);


            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            weekday = (WeekdayAttribute)attribute;

            float lineCount = weekday.linesCount == 0 ? 24 : weekday.linesCount;

            return EditorGUIUtility.singleLineHeight * lineCount + EditorGUIUtility.standardVerticalSpacing * 2f * (lineCount - 1);
        }


    }

    [UnityEditor.CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
    public class EnumFlagsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            property.intValue = EditorGUI.MaskField(position, label, property.intValue, property.enumDisplayNames);
        }
    }

    [UnityEditor.CustomPropertyDrawer(typeof(WeekdayEventAttribute))]
    public class WeekdayEventPropertyDrawer : PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {


            EditorGUI.BeginProperty(position, label, property);


            var titleRect = new Rect(position.x, position.y, position.width - 100, EditorGUIUtility.singleLineHeight);
            var timeRect = new Rect(position.x + position.width - 100, position.y, 100, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(titleRect, property.FindPropertyRelative("displayName"));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineCount = 2;

            return EditorGUIUtility.singleLineHeight * lineCount + EditorGUIUtility.standardVerticalSpacing * 2f * (lineCount - 1);
        }


    }
    
    [UnityEditor.CustomPropertyDrawer(typeof(ModifiedDateAttribute))]
    public class ModifiedDatePropertyDrawer : PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            EditorGUI.BeginProperty(position, label, property);

            var titleRect = new Rect(position.x, position.y, position.width / 2, position.height);
            var dayRect = new Rect(position.x + (position.width / 2), position.y, (position.width / 6) - 1, position.height);
            var monthRect = new Rect(position.x + (position.width / 2) + (position.width / 6), position.y, (position.width / 6) - 1, position.height);
            var yearRect = new Rect(position.x + (position.width / 2) + 2 * (position.width / 6), position.y, (position.width / 6) - 1, position.height);

            EditorGUI.LabelField(titleRect, label);

            List<GUIContent> dayNames = new List<GUIContent>();
            List<GUIContent> monthNames = new List<GUIContent>();
            if (HabitsEditorUtility.habitsProfile)
            {

                for (int i = 0; i < HabitsEditorUtility.habitsProfile.months[property.FindPropertyRelative("month").intValue].daysInMonth; i++)
                {
                    dayNames.Add(new GUIContent((i + 1).ToString()));
                }
                for (int i = 0; i < HabitsEditorUtility.habitsProfile.months.Count; i++)
                {
                    monthNames.Add(new GUIContent(HabitsEditorUtility.habitsProfile.months[i].displayName));
                }

                SerializedProperty day = property.FindPropertyRelative("day");
                SerializedProperty month = property.FindPropertyRelative("month");
                day.intValue = EditorGUI.Popup(dayRect, GUIContent.none, day.intValue, dayNames.ToArray());
                month.intValue = EditorGUI.Popup(monthRect, GUIContent.none, month.intValue, monthNames.ToArray());
                EditorGUI.PropertyField(yearRect, property.FindPropertyRelative("year"), GUIContent.none);
            }
            else
            {

                EditorGUI.PropertyField(dayRect, property.FindPropertyRelative("day"), GUIContent.none);
                EditorGUI.PropertyField(monthRect, property.FindPropertyRelative("month"), GUIContent.none);
                EditorGUI.PropertyField(yearRect, property.FindPropertyRelative("year"), GUIContent.none);
            }


            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineCount = 1;

            return EditorGUIUtility.singleLineHeight * lineCount + EditorGUIUtility.standardVerticalSpacing * 2f * (lineCount - 1);
        }


    }
}
