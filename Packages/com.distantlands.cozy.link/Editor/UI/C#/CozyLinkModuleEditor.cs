using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;
using DistantLands.Cozy.Data;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(LinkModule))]
    public class CozyLinkModuleEditor : CozyModuleEditor
    {

        LinkModule module;
        public override ModuleCategory Category => ModuleCategory.integration;
        public override string ModuleTitle => "Link";
        public override string ModuleSubtitle => "Multiplayer Module";
        public override string ModuleTooltip => "Manage multiplayer integrations with a number of server/client multiplayer solutions.";

        public Button IntegrationSelection => root.Q<Button>("integration-selection");
        public Label SelectedIntegration => root.Q<Label>("selected-integration");

        Button widget;
        VisualElement root;

        void OnEnable()
        {
            if (!target)
                return;

            module = (LinkModule)target;
        }

        public override Button DisplayWidget()
        {
            widget = SmallWidget();
            Label status = widget.Q<Label>("dynamic-status");
            status.style.fontSize = 8;
            status.text = LinkModule.SelectedIntegrationName;

            return widget;

        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.distantlands.cozy.link/Editor/UI/UXML/link-module-editor.uxml"
            );

            root.Bind(serializedObject);

            asset.CloneTree(root);

            SelectedIntegration.text = LinkModule.SelectedIntegrationName;
            
            IntegrationSelection.RegisterCallback((ClickEvent evt) =>
            {
                LinkIntegrations.Init();
            });


            return root;

        }
        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/link-module");
        }


    }
}