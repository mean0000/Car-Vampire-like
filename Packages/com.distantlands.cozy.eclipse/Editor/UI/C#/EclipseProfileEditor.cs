using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;
using DistantLands.Cozy.Data;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(EclipseProfile))]
    public class EclipseProfileEditor : Editor
    {

        VisualElement root;
        public VisualElement TabGroup => root.Q<VisualElement>("tabs");
        public VisualElement TabContent => root.Q<VisualElement>("tab-content");



        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.distantlands.cozy.eclipse/Editor/UI/UXML/eclipse-profile-editor.uxml"
            );

            asset.CloneTree(root);

            SelectTab(0);

            TabGroup.Q<Button>("lighting").RegisterCallback((ClickEvent evt) => { SelectTab(0); });
            TabGroup.Q<Button>("lighting").Add(new Image() { image = (Texture2D)Resources.Load("Icons/Lighting") });
            TabGroup.Q<Button>("fog").RegisterCallback((ClickEvent evt) => { SelectTab(1); });
            TabGroup.Q<Button>("fog").Add(new Image() { image = (Texture2D)Resources.Load("Icons/Fog") });
            TabGroup.Q<Button>("clouds").RegisterCallback((ClickEvent evt) => { SelectTab(2); });
            TabGroup.Q<Button>("clouds").Add(new Image() { image = (Texture2D)Resources.Load("Icons/Clouds") });
            TabGroup.Q<Button>("celestials").RegisterCallback((ClickEvent evt) => { SelectTab(3); });
            TabGroup.Q<Button>("celestials").Add(new Image() { image = (Texture2D)Resources.Load("Icons/Celestials") });

            return root;
        }

        public void SelectTab(int tabIndex)
        {
            DeselectAllTabs();
            switch (tabIndex)
            {
                case 0:
                    TabGroup.Q<Button>("lighting").AddToClassList("selected");
                    TabContent.Q<VisualElement>("lighting").AddToClassList("shown");
                    break;
                case 1:
                    TabGroup.Q<Button>("fog").AddToClassList("selected");
                    TabContent.Q<VisualElement>("fog").AddToClassList("shown");
                    break;
                case 2:
                    TabGroup.Q<Button>("clouds").AddToClassList("selected");
                    TabContent.Q<VisualElement>("clouds").AddToClassList("shown");
                    break;
                case 3:
                    TabGroup.Q<Button>("celestials").AddToClassList("selected");
                    TabContent.Q<VisualElement>("celestials").AddToClassList("shown");
                    break;
                default:
                    TabGroup.Q<Button>("lighting").AddToClassList("selected");
                    TabContent.Q<VisualElement>("lighting").AddToClassList("shown");
                    break;
            }

        }

        public void DeselectAllTabs()
        {
            TabGroup.Query<Button>().ForEach(x => x.RemoveFromClassList("selected"));
            TabContent.Query<VisualElement>().ForEach(x => x.RemoveFromClassList("shown"));
        }


    }
}