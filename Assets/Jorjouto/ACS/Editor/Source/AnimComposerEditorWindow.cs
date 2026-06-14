using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Callbacks;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    /// <summary>
    /// Cached editor state used to persist UI configuration between
    /// editor window reloads and domain reloads.
    /// </summary>
    [System.Serializable]
    public struct EditorCacheData
    {
        /// <summary>
        /// Stores the height of the preview window used in the editor.
        /// </summary>
        public int PreviewWindowHeight;

        /// <summary>
        /// Stores the current layout configuration of the editor window.
        /// </summary>
        public StyleEnum<FlexDirection> WindowConfiguration;

        /// <summary>
        /// Indicates whether a window layout configuration has been saved.
        /// </summary>
        public bool IsWindowConfigurationSet;
    }

    /// <summary>
    /// Handles opening the <see cref="AnimComposerEditorWindow"/> when
    /// a <see cref="ScriptableObject_AnimComposer"/> asset is opened
    /// from the Unity project window.
    /// </summary>
    public static class AnimComposerAssetOpener
    {
        /// <summary>
        /// Intercepts asset open events and launches the AnimComposer editor
        /// when a compatible asset is opened.
        /// </summary>
        /// <param name="instanceID">Instance ID of the asset being opened.</param>
        /// <returns>
        /// True if the asset was handled and the custom editor window was opened;
        /// otherwise false so Unity can process the asset normally.
        /// </returns>

        #if UNITY_6000_4_OR_NEWER
        [OnOpenAsset]
        public static bool OpenAsset(EntityId instanceID)
        {
            var retrievedObject = EditorUtility.EntityIdToObject(instanceID);
            
            if (retrievedObject is ScriptableObject_AnimComposer animComposerAsset)
            {
                AnimComposerEditorWindow.Open();
                AnimComposerEditorWindow window = EditorWindow.GetWindow<AnimComposerEditorWindow>();
                window.SetAsset(animComposerAsset);
                return true;
            }

            return false;
        }
        #else
        [OnOpenAsset]
        public static bool OpenAsset(int instanceID)
        {
            #pragma warning disable CS0618 // Disable the "obsolete" warning
            var retrievedObject = EditorUtility.InstanceIDToObject(instanceID);
            #pragma warning restore CS0618 // Re-enable it immediately
            
            if (retrievedObject is ScriptableObject_AnimComposer animComposerAsset)
            {
                AnimComposerEditorWindow.Open();
                AnimComposerEditorWindow window = EditorWindow.GetWindow<AnimComposerEditorWindow>();
                window.SetAsset(animComposerAsset);
                return true;
            }

            return false;
        }
        #endif
    }

    /// <summary>
    /// Custom editor window used for editing and previewing
    /// <see cref="ScriptableObject_AnimComposer"/> assets.
    /// </summary>
    /// <remarks>
    /// This window hosts the full animation composition editor UI,
    /// including animation preview, timeline editing, and action block
    /// management.
    /// </remarks>
    public class AnimComposerEditorWindow : EditorWindow
    {
        /// <summary>
        /// Serialized cache data storing persistent UI configuration.
        /// </summary>
        [SerializeField]
        private EditorCacheData cacheData;

        /// <summary>
        /// The currently loaded AnimComposer asset being edited.
        /// </summary>
        private ScriptableObject_AnimComposer targetAsset = null;

        /// <summary>
        /// Serialized wrapper used to bind the asset to the UI Toolkit
        /// interface and support undo/redo operations.
        /// </summary>
        private SerializedObject serializedObject = null;

        /// <summary>
        /// The active editor UI controller responsible for constructing
        /// and managing the visual interface.
        /// </summary>
        private AnimComposerEditorUI currentUI = null;

        /// <summary>
        /// Opens the AnimComposer editor window from the Unity menu.
        /// </summary>
        [MenuItem("Window/AnimComposer/Editor")]
        public static void Open()
        {
            var window = GetWindow<AnimComposerEditorWindow>();
            window.titleContent = new GUIContent("AnimComposerEditor");
            window.Show();
        }

        /// <summary>
        /// Assigns an AnimComposer asset to the editor window and rebuilds
        /// the interface to reflect the new asset.
        /// </summary>
        /// <param name="asset">The AnimComposer asset to edit.</param>
        public void SetAsset(ScriptableObject_AnimComposer asset)
        {
            targetAsset = asset;
            var window = GetWindow<AnimComposerEditorWindow>();
            window.titleContent = new GUIContent("AnimComposerEditor_" + asset.name);
            RebuildUI();
        }

        /// <summary>
        /// Called by Unity when the UI Toolkit interface for this window
        /// is first created.
        /// </summary>
        /// <remarks>
        /// This method rebuilds the entire editor UI based on the currently
        /// selected AnimComposer asset.
        /// </remarks>
        private void CreateGUI()
        {
            RebuildUI();
        }

        /// <summary>
        /// Reconstructs the editor UI and binds it to the current asset.
        /// </summary>
        /// <remarks>
        /// Any existing UI is disposed and removed before creating a new
        /// <see cref="AnimComposerEditorUI"/> instance. If no asset is
        /// assigned, a placeholder message is displayed.
        /// </remarks>
        void RebuildUI()
        {
            currentUI?.Dispose();
            rootVisualElement.Clear();

            if (targetAsset == null)
            {
                rootVisualElement.Add(new Label("Select an AnimComposer asset."));
                return;
            }

            serializedObject = new SerializedObject(targetAsset);

            currentUI = new AnimComposerEditorUI(targetAsset, serializedObject);
            rootVisualElement.Add(currentUI.Root);
            currentUI.Root.Bind(serializedObject);
        }

        /// <summary>
        /// Called when the active Unity editor selection changes.
        /// </summary>
        /// <remarks>
        /// If the selected object is an AnimComposer asset, the window
        /// automatically loads it into the editor.
        /// </remarks>
        private void OnSelectionChange()
        {
            if (Selection.activeObject is ScriptableObject_AnimComposer asset)
            {
                SetAsset(asset);
            }
        }

        #region Cache Editor Methods

        /// <summary>
        /// Saves the height of the preview window into the cached editor state.
        /// </summary>
        /// <param name="height">The preview window height in pixels.</param>
        public static void SavePreviewWindowHeight(int height)
        {
            var window = GetWindow<AnimComposerEditorWindow>();
            window.cacheData.PreviewWindowHeight = height;
        }

        /// <summary>
        /// Saves the editor window layout configuration.
        /// </summary>
        /// <param name="configuration">
        /// The layout orientation used by the editor interface.
        /// </param>
        public static void SaveWindowConfiguration(StyleEnum<FlexDirection> configuration)
        {
            var window = GetWindow<AnimComposerEditorWindow>();
            window.cacheData.IsWindowConfigurationSet = true;
            window.cacheData.WindowConfiguration = configuration;
        }

        /// <summary>
        /// Loads the cached editor state.
        /// </summary>
        /// <returns>
        /// The cached <see cref="EditorCacheData"/> stored in the window.
        /// </returns>
        public static EditorCacheData LoadCachedData()
        {
            var window = GetWindow<AnimComposerEditorWindow>();
            return window.cacheData;
        }

        #endregion

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            currentUI?.Dispose();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && currentUI != null)
            {
                RebuildUI();
            }
        }
    }
}