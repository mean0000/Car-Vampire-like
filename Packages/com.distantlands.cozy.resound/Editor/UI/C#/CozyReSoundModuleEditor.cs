using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;
using DistantLands.Cozy.Data;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(ReSoundModule))]
    public class CozyEclipseModuleEditor : CozyBiomeModuleEditor
    {

        ReSoundModule module;
        public override ModuleCategory Category => ModuleCategory.utility;
        public override string ModuleTitle => "ReSound";
        public override string ModuleSubtitle => "Dynamic Soundtrack Module";
        public override string ModuleTooltip => "Manage your weather with more control.";

        public VisualElement SelectionContainer => root.Q<VisualElement>("selection-container");
        public VisualElement ProfileContainer => root.Q<VisualElement>("profile-container");
        bool selectionWindowIsOpen;
        bool djWindowIsOpen;
        bool setlistWindowIsOpen;


        Button widget;
        VisualElement root;

        void OnEnable()
        {
            if (!target)
                return;

            module = (ReSoundModule)target;
        }

        public override Button DisplayWidget()
        {
            widget = SmallWidget();
            Label status = widget.Q<Label>("dynamic-status");
            status.style.fontSize = 8;
            status.text = module.currentTrack ? module.currentTrack.name : "No track";

            return widget;
        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();


            IMGUIContainer container = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                EditorGUILayout.BeginHorizontal();


                if (module.paused)
                {
                    if (GUILayout.Button(EditorGUIUtility.IconContent("PlayButton")))
                    {
                        module.Play();
                    }
                }
                else
                {
                    if (GUILayout.Button(EditorGUIUtility.IconContent("PauseButton")))
                    {
                        module.Pause();
                    }
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("StepButton")))
                {
                    module.Skip();
                }

                GUILayout.Label(new GUIContent(module.currentTrack ? module.currentTrack.name : "No track playing currently - "), EditorStyles.boldLabel);


                EditorGUILayout.EndHorizontal();

                selectionWindowIsOpen = EditorGUILayout.BeginFoldoutHeaderGroup(selectionWindowIsOpen, new GUIContent("    Selection Settings"), EditorUtilities.FoldoutStyle);
                EditorGUILayout.EndFoldoutHeaderGroup();

                if (selectionWindowIsOpen)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("DJ"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("setlist"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("mixerGroup"));
                    EditorGUI.indentLevel--;

                }

                djWindowIsOpen = EditorGUILayout.BeginFoldoutHeaderGroup(djWindowIsOpen, new GUIContent("    DJ Settings"), EditorUtilities.FoldoutStyle);
                EditorGUILayout.EndFoldoutHeaderGroup();

                if (djWindowIsOpen)
                {
                    EditorGUI.indentLevel++;
                    if (module.DJ)
                        CreateEditor(module.DJ).OnInspectorGUI();
                    else
                        EditorGUILayout.HelpBox("Assign a DJ & setlist to begin using ReSound.", MessageType.Warning);
                    EditorGUI.indentLevel--;

                }


                setlistWindowIsOpen = EditorGUILayout.BeginFoldoutHeaderGroup(setlistWindowIsOpen, new GUIContent("    Setlist Settings"), EditorUtilities.FoldoutStyle);
                EditorGUILayout.EndFoldoutHeaderGroup();

                if (setlistWindowIsOpen)
                {
                    EditorGUI.indentLevel++;
                    if (module.setlist)
                        CreateEditor(module.setlist).OnInspectorGUI();
                    else
                        EditorGUILayout.HelpBox("Assign a DJ & setlist to begin using ReSound.", MessageType.Warning);
                    EditorGUI.indentLevel--;

                }

                serializedObject.ApplyModifiedProperties();
            });


            root.Add(container);

            return root;

        }

        public override VisualElement DisplayBiomeUI()
        {
            root = new VisualElement();

            IMGUIContainer container = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                EditorGUILayout.BeginHorizontal();

                if (module.paused)
                {
                    if (GUILayout.Button(EditorGUIUtility.IconContent("PlayButton")))
                    {
                        module.Play();
                    }
                }
                else
                {
                    if (GUILayout.Button(EditorGUIUtility.IconContent("PauseButton")))
                    {
                        module.Pause();
                    }
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("StepButton")))
                {
                    module.Skip();
                }

                GUILayout.Label(new GUIContent(module.currentTrack ? module.currentTrack.name : "No track playing currently - "), EditorStyles.boldLabel);

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("setlist"));
                serializedObject.ApplyModifiedProperties();
            });

            root.Add(container);
            return root;
        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/resound-module");
        }


    }
}