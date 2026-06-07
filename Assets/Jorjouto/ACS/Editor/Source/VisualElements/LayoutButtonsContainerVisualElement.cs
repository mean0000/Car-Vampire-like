// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.

/// <summary>
/// A VisualElement that represents the buttons that will toggle configuration panel layout and visibility.
/// </summary>
/// <remarks>
/// This element shows buttons that allow users to place configuration panel at any side of the animation preview window,
/// as well as completely hiding the configuration panel.
/// </remarks>
using UnityEditor;
using UnityEngine.UIElements;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    /// <summary>
    /// UI Toolkit container that provides controls for changing the layout and visibility
    /// of the configuration panel inside the animation preview window.
    /// </summary>
    [UxmlElement]
    public partial class LayoutButtonsContainerVisualElement : VisualElement
    {
        #region Template GUID

        /// <summary>
        /// GUID that references the UXML template used to build this UI element.
        /// </summary>
        private const string guid = "9ab1a82dc7d40bf42aa7dee4ec2d16b8";

        /// <summary>
        /// Asset path resolved from the GUID pointing to the UXML template.
        /// </summary>
        private readonly string templatePath = AssetDatabase.GUIDToAssetPath(guid);

        #endregion

        #region Visual Elements

        /// <summary>
        /// Button used to toggle the visibility of the configuration panel.
        /// </summary>
        private Button hideConfigurationButton = null;

        /// <summary>
        /// Button used to place the configuration panel on the left side of the window.
        /// </summary>
        private Button leftOrientationButton = null;

        /// <summary>
        /// Button used to place the configuration panel on the right side of the window.
        /// </summary>
        private Button rightOrientationButton = null;

        /// <summary>
        /// Button used to place the configuration panel at the top of the window.
        /// </summary>
        private Button topOrientationButton = null;

        /// <summary>
        /// Button used to place the configuration panel at the bottom of the window.
        /// </summary>
        private Button bottomOrientationButton = null;

        /// <summary>
        /// Container that holds the orientation buttons.
        /// </summary>
        private VisualElement orientationButtonsContainer = null;

        /// <summary>
        /// Parent container that holds both the preview area and configuration panel.
        /// Its flex direction determines the panel orientation.
        /// </summary>
        private VisualElement configurationParentElement = null;

        /// <summary>
        /// ScrollView containing the configuration panel UI.
        /// </summary>
        private ScrollView configurationPanelScrollView = null;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutButtonsContainerVisualElement"/> class.
        /// </summary>
        /// <remarks>
        /// Loads the UI template and retrieves references to all required visual elements.
        /// </remarks>
        public LayoutButtonsContainerVisualElement()
        {
            VisualTreeAsset templateAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
            templateAsset.CloneTree(this);
            StoreAllVisualElements();
        }

        /// <summary>
        /// Initializes the layout buttons container with the configuration panel references.
        /// </summary>
        /// <param name="configurationParentElement">
        /// Parent element whose flex direction determines the layout orientation.
        /// </param>
        /// <param name="configurationPanelScrollView">
        /// ScrollView containing the configuration panel UI.
        /// </param>
        public void Initialize(VisualElement configurationParentElement, ScrollView configurationPanelScrollView)
        {
            this.configurationParentElement = configurationParentElement;
            this.configurationPanelScrollView = configurationPanelScrollView;
            CreateAllBindings();
            InitializeConfigurationPanel();
        }

        /// <summary>
        /// Retrieves and stores references to all visual elements defined in the UXML template.
        /// </summary>
        private void StoreAllVisualElements()
        {
            hideConfigurationButton = this.Q<Button>("HideConfigurationButton");
            leftOrientationButton = this.Q<Button>("LeftOrientationButton");
            rightOrientationButton = this.Q<Button>("RightOrientationButton");
            topOrientationButton = this.Q<Button>("TopOrientationButton");
            bottomOrientationButton = this.Q<Button>("BottomOrientationButton");
            orientationButtonsContainer = this.Q<VisualElement>("OrientationButtonsContainer");
        }

        /// <summary>
        /// Initializes the configuration panel layout using cached window configuration data.
        /// </summary>
        /// <remarks>
        /// If a previously saved configuration exists, the panel orientation will be restored.
        /// </remarks>
        private void InitializeConfigurationPanel()
        {
            var cachedVariables = AnimComposerEditorWindow.LoadCachedData();

            if(cachedVariables.IsWindowConfigurationSet)
            {
                configurationParentElement.style.flexDirection = cachedVariables.WindowConfiguration;
            }
        }

        #region Bindings

        /// <summary>
        /// Creates all button bindings for the layout and visibility controls.
        /// </summary>
        private void CreateAllBindings()
        {
            CreateHideDisplayButtonBindings();
            CreateLeftOrientationButtonBindings();
            CreateRightOrientationButtonBindings();
            CreateTopOrientationButtonBindings();
            CreateBottomOrientationButtonBindings();
        }

        /// <summary>
        /// Sets up the binding for the hide button to toggle configuration panel visibility.
        /// </summary>
        private void CreateHideDisplayButtonBindings()
        {
            hideConfigurationButton.clicked += () =>
            {
                if(configurationPanelScrollView == null)
                {
                    return;
                }

                if(configurationPanelScrollView.style.display == DisplayStyle.None)
                {
                    configurationPanelScrollView.style.display = DisplayStyle.Flex;
                    orientationButtonsContainer.RemoveFromClassList("OrientationButtons_Hidden");
                    hideConfigurationButton.RemoveFromClassList("HideConfigurationButton_Hidden");
                    hideConfigurationButton.AddToClassList("HideConfigurationButton_Visible");
                }
                else
                {
                    configurationPanelScrollView.style.display = DisplayStyle.None;
                    orientationButtonsContainer.AddToClassList("OrientationButtons_Hidden");
                    hideConfigurationButton.AddToClassList("HideConfigurationButton_Hidden");
                    hideConfigurationButton.RemoveFromClassList("HideConfigurationButton_Visible");
                }
            };
        }

        /// <summary>
        /// Creates the binding for the button that sets the configuration panel to the left side.
        /// </summary>
        private void CreateLeftOrientationButtonBindings()
        {
            leftOrientationButton.clicked += () =>
            {
                if(configurationParentElement != null)
                {
                    configurationParentElement.style.flexDirection = FlexDirection.Row;
                    AnimComposerEditorWindow.SaveWindowConfiguration(FlexDirection.Row);
                }
            };
        }

        /// <summary>
        /// Creates the binding for the button that sets the configuration panel to the right side.
        /// </summary>
        private void CreateRightOrientationButtonBindings()
        {
            rightOrientationButton.clicked += () =>
            {
                if(configurationParentElement != null)
                {
                    configurationParentElement.style.flexDirection = FlexDirection.RowReverse;
                    AnimComposerEditorWindow.SaveWindowConfiguration(FlexDirection.RowReverse);
                }
            };
        }

        /// <summary>
        /// Creates the binding for the button that sets the configuration panel to the top side.
        /// </summary>
        private void CreateTopOrientationButtonBindings()
        {
            topOrientationButton.clicked += () =>
            {
                if(configurationParentElement != null)
                {
                    configurationParentElement.style.flexDirection = FlexDirection.Column;
                    AnimComposerEditorWindow.SaveWindowConfiguration(FlexDirection.Column);
                }
            };
        }

        /// <summary>
        /// Creates the binding for the button that sets the configuration panel to the bottom side.
        /// </summary>
        private void CreateBottomOrientationButtonBindings()
        {
            bottomOrientationButton.clicked += () =>
            {
                if(configurationParentElement != null)
                {
                    configurationParentElement.style.flexDirection = FlexDirection.ColumnReverse;
                    AnimComposerEditorWindow.SaveWindowConfiguration(FlexDirection.ColumnReverse);
                }
            };
        }

        #endregion
    }
}