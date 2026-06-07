// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
/// <summary>
/// A VisualElement that represents a single action block on an animation track.
/// </summary>
/// <remarks>
/// This element handles all user interactions for a single action block, including
/// selection, dragging to move, and resizing its start and end times. It also
/// manages the display of the corresponding property panel in the Inspector.
/// </remarks>
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    /// <summary>
    /// UI Toolkit element responsible for displaying and managing a list of preview items.
    /// </summary>
    /// <remarks>
    /// Each preview item represents a GameObject that can be attached to a socket on a preview model,
    /// with configurable transform offsets, scale, and visibility.
    /// </remarks>
    [UxmlElement]
    public partial class PreviewItemsViewElement : VisualElement
    {
        #region Events

        /// <summary>
        /// Event triggered when a preview item GameObject is assigned or removed.
        /// </summary>
        public event Action<int, GameObject> OnPreviewItemChanged;
        
        /// <summary>
        /// Event triggered when an attach socket is assigned to the preview item.
        /// </summary>
        public event Action<int, string> OnAttachSocketAssignedToPreviewItem;

        /// <summary>
        /// Event triggered when the offset position of the preview item changes.
        /// </summary>
        public event Action<int, Vector3> OnPreviewItemOffsetPositionChanged;

        /// <summary>
        /// Event triggered when the offset rotation of the preview item changes.
        /// </summary>
        public event Action<int, Vector3> OnPreviewItemOffsetRotationChanged;

        /// <summary>
        /// Event triggered when the scale of the preview item changes.
        /// </summary>
        public event Action<int, Vector3> OnPreviewItemScaleChanged;

        /// <summary>
        /// Event triggered when the visibility of the preview item changes.
        /// </summary>
        public event Action<int, bool> OnPreviewItemVisibilityChanged;

        #endregion

        #region Fields

        /// <summary>
        /// UXML template used to create each item row in the list.
        /// </summary>
        [SerializeField]
        [UxmlAttribute("item-row-template")]
        private VisualTreeAsset itemRowTemplate;
        
        #endregion
        
        #region Properties

        #region Visual Elements

        /// <summary>
        /// Root container that holds all UI elements of the preview item system.
        /// </summary>
        private readonly VisualElement itemsContainer = new () { name = "ItemsContainer" };

        /// <summary>
        /// Container that holds the list of preview item rows.
        /// </summary>
        private readonly VisualElement itemListContainer = new() { name = "ItemListContainer" };

        /// <summary>
        /// Container that holds action buttons such as add and remove.
        /// </summary>
        private readonly VisualElement buttonsContainer = new() { name = "ButtonsContainer" };

        /// <summary>
        /// Button used to add a new preview item to the list.
        /// </summary>
        private readonly Button addItemButton = new() { text = "Add Item" };

        /// <summary>
        /// Button used to remove the currently selected preview item.
        /// </summary>
        private readonly Button removeItemButton = new() { text = "Remove Item" };

        #endregion
        
        #region Caches

        /// <summary>
        /// Serialized property representing the array of preview items.
        /// </summary>
        private SerializedProperty previewItemsProp = null;

        /// <summary>
        /// The preview model whose sockets can be used to attach preview items.
        /// </summary>
        private GameObject previewModel = null;

        /// <summary>
        /// Dictionary storing available socket options for attaching preview items.
        /// </summary>
        private readonly Dictionary<string, string> socketOptions = new();

        /// <summary>
        /// Index of the currently selected item row.
        /// </summary>
        private int selectedItemIndex = -1;

        /// <summary>
        /// Temporary storage used for copy/paste operations on preview items.
        /// </summary>
        private SerializedProperty copiedItemData = null;

        #endregion

        #endregion

        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewItemsViewElement"/> class.
        /// </summary>
        public PreviewItemsViewElement()
        {
            AddToClassList("DetailsPanelFoldout");
            addItemButton.clicked += AddNewItemToList;
            removeItemButton.clicked += RemoveItemFromList;
            Add(itemsContainer);
            itemsContainer.Add(itemListContainer);
            itemsContainer.Add(buttonsContainer);
            focusable = true;
            addItemButton.focusable = true;
            removeItemButton.focusable = true;
            buttonsContainer.Add(addItemButton);
            buttonsContainer.Add(removeItemButton);
            addItemButton.AddToClassList("CustomButton");
            removeItemButton.AddToClassList("CustomButton");

            RegisterCallback<FocusEvent>(ev =>
            {
                if (selectedItemIndex < 0)
                {
                    return;
                }

                itemListContainer[selectedItemIndex].Q<VisualElement>("ItemRowWrapper").RemoveFromClassList("PreviewItemRow_Focused");
                selectedItemIndex = -1;
            });

            BindKeyboardShortcuts();
        }

        #endregion

        #region Functions

        #region Public Functions

        /// <summary>
        /// Initializes the preview item list using the provided preview model and serialized property.
        /// </summary>
        /// <param name="previewModel">The model used to extract socket attachment points.</param>
        /// <param name="previewItemsProp">Serialized array property containing preview item data.</param>
        public void InitializeItemList(GameObject previewModel, SerializedProperty previewItemsProp)
        {
            this.previewItemsProp = previewItemsProp;
            this.previewModel = previewModel;

            ReinitializeItemRows();
        }

        #endregion

        #region Private Functions

        #region Initialization

        /// <summary>
        /// Rebuilds the entire list of preview item rows from the serialized property array.
        /// </summary>
        private void ReinitializeItemRows()
        {
            itemListContainer.Clear();

            for (int i = 0; i < previewItemsProp.arraySize; i++)
            {
                var itemProp = previewItemsProp.GetArrayElementAtIndex(i);
                var itemRow = itemRowTemplate.Instantiate();
                itemListContainer.Add(itemRow);
                InitializeItemRow(itemRow, itemProp, i);
            }
        }

        /// <summary>
        /// Initializes a row representing a preview item.
        /// </summary>
        /// <param name="itemRow">The UI element representing the row.</param>
        /// <param name="itemProp">The serialized property for the item.</param>
        /// <param name="itemIndex">Index of the item in the array.</param>
        private void InitializeItemRow(VisualElement itemRow, SerializedProperty itemProp, int itemIndex)
        {
            itemRow.focusable = true;
            InitializeItemRowFoldoutLabel(itemRow, itemProp);
            InitializeItemRowDropdownText(itemRow, itemProp);
            BindRowFields(itemRow, itemProp);
            AddFieldCallbacks(itemRow, itemProp, itemIndex);
            itemProp.serializedObject.Update();
            itemProp.serializedObject.ApplyModifiedProperties();
            ChooseItemRowBackgroundBasedOnPreviewItemField(itemRow, itemProp);
        }

        /// <summary>
        /// Initializes the foldout label of an item row based on the assigned GameObject.
        /// </summary>
        private void InitializeItemRowFoldoutLabel(VisualElement itemRow, SerializedProperty itemProp)
        {
            Label label = itemProp.FindPropertyRelative("Item").objectReferenceValue ?
                                new Label(itemProp.FindPropertyRelative("Item").objectReferenceValue.name) :
                                new Label("None");

            var foldout = itemRow.Q<Foldout>();
            foldout.text = label.text;
        }

        /// <summary>
        /// Initializes the dropdown field text for selecting an attachment socket.
        /// </summary>
        private void InitializeItemRowDropdownText(VisualElement itemRow, SerializedProperty itemProp)
        {
            var attachSocketDropdownField = itemRow.Q<Button>("ItemDropdownField");
            string attachSocket = itemProp.FindPropertyRelative("AttachSocket").stringValue;

            if(!string.IsNullOrEmpty(attachSocket))
            {
                attachSocketDropdownField.text = attachSocket;
            }
        }

        /// <summary>
        /// Populates the socket options dictionary using transforms from the preview model.
        /// </summary>
        private void AssignAllSocketOptions()
        {
            if (previewModel == null)
                return;

            Transform[] previewModelTransforms = previewModel.GetComponentsInChildren<Transform>();
            socketOptions.Clear();

            socketOptions.Add("None", "None");

            for (int i = 1; i < previewModelTransforms.Length; i++)
            {
                Transform t = previewModelTransforms[i];
                string path = t.name;
                Transform parent = t.parent;

                while (parent != null && parent != previewModel.transform)
                {
                    path = parent.name + "/" + path;
                    parent = parent.parent;
                }

                if (!socketOptions.ContainsKey(t.name))
                {
                    socketOptions.Add(t.name, path);
                }
            }
        }

        #endregion

        #region Bindings

        /// <summary>
        /// Binds UI fields in the row to serialized properties.
        /// </summary>
        private void BindRowFields(VisualElement itemRow, SerializedProperty itemProp)
        {
            var itemField = itemRow.Q<ObjectField>();
            var offsetPositionField = itemRow.Q<Vector3Field>("OffsetPositionField");
            var offsetRotationField = itemRow.Q<Vector3Field>("OffsetRotationField");
            var scaleField = itemRow.Q<Vector3Field>("ScaleField");
            var visibilityToggleField = itemRow.Q<Toggle>("VisibilityToggle");

            itemField.BindProperty(itemProp.FindPropertyRelative("Item"));
            offsetPositionField.BindProperty(itemProp.FindPropertyRelative("OffsetPosition"));
            offsetRotationField.BindProperty(itemProp.FindPropertyRelative("OffsetRotation"));
            scaleField.BindProperty(itemProp.FindPropertyRelative("Scale"));
            visibilityToggleField.BindProperty(itemProp.FindPropertyRelative("Visible"));
        }

        /// <summary>
        /// Registers keyboard shortcuts for common list operations.
        /// </summary>
        private void BindKeyboardShortcuts()
        {
            RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode)
                {
                    case KeyCode.C:
                        OnPreviewItemCopied();
                        break;

                    case KeyCode.V:
                        if (selectedItemIndex >= 0)
                        {
                            OnPreviewItemPasted();
                        }
                        break;

                    case KeyCode.I or
                         KeyCode.Plus or
                         KeyCode.KeypadPlus:
                        AddNewItemToList();
                        break;

                    case KeyCode.D:
                        if (selectedItemIndex >= 0)
                        {
                            OnPreviewItemDuplicated();
                        }
                        break;

                    case KeyCode.Delete or
                         KeyCode.KeypadPeriod or
                         KeyCode.Minus or
                         KeyCode.KeypadMinus:
                        DeleteItemAtSelectedIndex();
                        break;

                    case KeyCode.W or
                         KeyCode.UpArrow or
                         KeyCode.Keypad8:
                        MoveItemUpTheList();
                        break;

                    case KeyCode.S or
                         KeyCode.DownArrow or
                         KeyCode.Keypad2:
                        MoveItemDownTheList();
                        break;                          
                }
                evt.StopPropagation();
            });
        }

        #endregion

        #region Callbacks

        /// <summary>
        /// Registers value change callbacks for row UI fields.
        /// </summary>
        private void AddFieldCallbacks(VisualElement itemRow, SerializedProperty itemProp, int itemIndex)
        {
            var itemField = itemRow.Q<ObjectField>();
            var attachSocketDropdownField = itemRow.Q<Button>("ItemDropdownField");
            var offsetPositionField = itemRow.Q<Vector3Field>("OffsetPositionField");
            var offsetRotationField = itemRow.Q<Vector3Field>("OffsetRotationField");
            var scaleField = itemRow.Q<Vector3Field>("ScaleField");
            var visibilityToggleField = itemRow.Q<Toggle>("VisibilityToggle");
            var foldout = itemRow.Q<Foldout>();

            itemRow.RegisterCallback<FocusEvent>(OnRowFocused);
            itemRow.RegisterCallback<MouseUpEvent>(OnRowClicked);

            itemField.RegisterValueChangedCallback(evt =>
            {
                foldout.text = evt.newValue ? evt.newValue.name : "None";
                ChooseItemRowBackgroundBasedOnPreviewItemField(itemRow, itemProp);
                OnPreviewItemChanged?.Invoke(itemIndex, (GameObject)evt.newValue);
            });

            attachSocketDropdownField.clicked += ()=>
            {
                AssignAllSocketOptions();

                StringListSearchProvider stringListSearchProvider = ScriptableObject.CreateInstance<StringListSearchProvider>();
                stringListSearchProvider.Init(socketOptions.Values.ToArray(), (x) =>
                                    {
                                        itemProp.FindPropertyRelative("AttachSocket").stringValue = x;
                                        attachSocketDropdownField.text = x;
                                        itemProp.serializedObject.ApplyModifiedProperties(); 
                                        OnAttachSocketAssignedToPreviewItem?.Invoke(itemIndex, x);
                                    });

                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), stringListSearchProvider);
            };

            offsetPositionField.RegisterValueChangedCallback(evt =>
            {
                OnPreviewItemOffsetPositionChanged?.Invoke(itemIndex, evt.newValue);
            });

            offsetRotationField.RegisterValueChangedCallback(evt =>
            {
                OnPreviewItemOffsetRotationChanged?.Invoke(itemIndex, evt.newValue);
            });

            scaleField.RegisterValueChangedCallback(evt =>
            {
                OnPreviewItemScaleChanged?.Invoke(itemIndex, evt.newValue);
            });

            visibilityToggleField.RegisterValueChangedCallback(evt =>
            {
                OnPreviewItemVisibilityChanged?.Invoke(itemIndex, evt.newValue);
            });
        }

        #region Row Callbacks

        /// /// <summary>
        /// Highlights the currently focused row and updates the selected index.
        /// </summary>
        private void OnRowFocused(FocusEvent ev)
        {
            foreach (var child in itemListContainer.Children())
            {
                if (child == ev.target)
                {
                    child.Q<VisualElement>("ItemRowWrapper").AddToClassList("PreviewItemRow_Focused");
                }
                else
                {
                    child.Q<VisualElement>("ItemRowWrapper").RemoveFromClassList("PreviewItemRow_Focused");
                }
            }

            selectedItemIndex = itemListContainer.Children().ToList().IndexOf((VisualElement)ev.target);

            ev.StopPropagation();
        }

        /// <summary>
        /// Handles right-click events on item rows to open the context menu.
        /// </summary>
        private void OnRowClicked(MouseUpEvent ev)
        {
            if (ev.button == 1)
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Copy Item Data (C)"), false, OnPreviewItemCopied);

                if (copiedItemData != null)
                {
                    menu.AddItem(new GUIContent("Paste Item Data (V)"), false, OnPreviewItemPasted);
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste Item Data (V)"));
                }

                menu.AddItem(new GUIContent("Delete Item Data (Del or -)"), false, RemoveItemFromList);
                menu.AddItem(new GUIContent("Duplicate Item Data (D)"), false, OnPreviewItemDuplicated);
                menu.AddItem(new GUIContent("Insert Item Data (I or +)"), false, AddNewItemToList);
                menu.AddItem(new GUIContent("Move up Item Data (W or Up Arrow)"), false, MoveItemUpTheList);
                menu.AddItem(new GUIContent("Move down Item Data (S or Down Arrow)"), false, MoveItemDownTheList);

                menu.DropDown(new Rect(GUIUtility.GUIToScreenPoint(ev.mousePosition), Vector2.zero));

                Debug.Log("Right clicked row");

                ev.StopPropagation();
            }
        }

        #endregion

        #region Context Menu Callbacks

        /// <summary>
        /// Copies the currently selected preview item data.
        /// </summary>
        private void OnPreviewItemCopied()
        {
            if (selectedItemIndex < 0)
            {
                Debug.LogWarning("No item selected to copy");
                return;
            }

            copiedItemData = previewItemsProp.GetArrayElementAtIndex(selectedItemIndex).Copy();
            Debug.Log($"Copied preview item at index {selectedItemIndex}");
        }

        /// <summary>
        /// Pastes previously copied preview item data into the selected row.
        /// </summary>
        private void OnPreviewItemPasted()
        {
            if (copiedItemData == null)
            {
                Debug.LogWarning("No item data copied to paste");
                return;
            }

            var targetProp = previewItemsProp.GetArrayElementAtIndex(selectedItemIndex);

            targetProp.FindPropertyRelative("Item").objectReferenceValue = copiedItemData.FindPropertyRelative("Item").objectReferenceValue;
            targetProp.FindPropertyRelative("AttachSocket").stringValue = copiedItemData.FindPropertyRelative("AttachSocket").stringValue;
            targetProp.FindPropertyRelative("OffsetPosition").vector3Value = copiedItemData.FindPropertyRelative("OffsetPosition").vector3Value;
            targetProp.FindPropertyRelative("OffsetRotation").vector3Value = copiedItemData.FindPropertyRelative("OffsetRotation").vector3Value;
            targetProp.FindPropertyRelative("Scale").vector3Value = copiedItemData.FindPropertyRelative("Scale").vector3Value;
            targetProp.FindPropertyRelative("Visible").boolValue = copiedItemData.FindPropertyRelative("Visible").boolValue;

            previewItemsProp.serializedObject.ApplyModifiedProperties();
            ReinitializeItemRows();

            Debug.Log($"Pasted preview item at index {selectedItemIndex}");
        }

        /// <summary>
        /// Duplicates the currently selected preview item.
        /// </summary>
        private void OnPreviewItemDuplicated()
        {
            int index = selectedItemIndex;
            previewItemsProp.InsertArrayElementAtIndex(index + 1);

            var sourceProp = previewItemsProp.GetArrayElementAtIndex(index);
            var targetProp = previewItemsProp.GetArrayElementAtIndex(index + 1);

            targetProp.FindPropertyRelative("Item").objectReferenceValue = sourceProp.FindPropertyRelative("Item").objectReferenceValue;
            targetProp.FindPropertyRelative("AttachSocket").stringValue = sourceProp.FindPropertyRelative("AttachSocket").stringValue;
            targetProp.FindPropertyRelative("OffsetPosition").vector3Value = sourceProp.FindPropertyRelative("OffsetPosition").vector3Value;
            targetProp.FindPropertyRelative("OffsetRotation").vector3Value = sourceProp.FindPropertyRelative("OffsetRotation").vector3Value;
            targetProp.FindPropertyRelative("Scale").vector3Value = sourceProp.FindPropertyRelative("Scale").vector3Value;
            targetProp.FindPropertyRelative("Visible").boolValue = sourceProp.FindPropertyRelative("Visible").boolValue;

            previewItemsProp.serializedObject.ApplyModifiedProperties();
            ReinitializeItemRows();
        }

        #endregion

        #region Item List Actions

        /// <summary>
        /// Adds a new preview item to the list.
        /// </summary>
        /// <remarks>
        /// If an item is selected, the new item will be inserted at the selected index.
        /// Otherwise, it will be appended to the end of the list.
        /// </remarks>
        private void AddNewItemToList()
        {
            int index = selectedItemIndex >= 0
                        ? selectedItemIndex
                        : previewItemsProp.arraySize;

            previewItemsProp.InsertArrayElementAtIndex(index);

            var itemProp = previewItemsProp.GetArrayElementAtIndex(index);

            itemProp.FindPropertyRelative("Item").objectReferenceValue = null;
            itemProp.FindPropertyRelative("AttachSocket").stringValue = string.Empty;
            itemProp.FindPropertyRelative("OffsetPosition").vector3Value = Vector3.zero;
            itemProp.FindPropertyRelative("OffsetRotation").vector3Value = Vector3.zero;
            itemProp.FindPropertyRelative("Scale").vector3Value = Vector3.one;
            itemProp.FindPropertyRelative("Visible").boolValue = true;

            previewItemsProp.serializedObject.ApplyModifiedProperties();

            ReinitializeItemRows();
            selectedItemIndex = -1;
        }

        /// <summary>
        /// Moves the currently selected preview item one position up in the list.
        /// </summary>
        private void MoveItemUpTheList()
        {
            if (selectedItemIndex <= 0)
            {
                return;
            }

            int newIndex = selectedItemIndex - 1;

            previewItemsProp.MoveArrayElement(selectedItemIndex, newIndex);
            previewItemsProp.serializedObject.ApplyModifiedProperties();

            ReinitializeItemRows();
            selectedItemIndex = newIndex;
            itemListContainer[selectedItemIndex].Focus();
        }

        /// <summary>
        /// Moves the currently selected preview item one position down in the list.
        /// </summary>
        private void MoveItemDownTheList()
        {
            if (selectedItemIndex < 0 || selectedItemIndex >= previewItemsProp.arraySize - 1)
            {
                return;
            }

            int newIndex = selectedItemIndex + 1;

            previewItemsProp.MoveArrayElement(selectedItemIndex, newIndex);
            previewItemsProp.serializedObject.ApplyModifiedProperties();

            ReinitializeItemRows();
            selectedItemIndex = newIndex;
            itemListContainer[selectedItemIndex].Focus();
        }

        /// <summary>
        /// Removes the currently selected preview item from the list.
        /// </summary>
        private void RemoveItemFromList()
        {
            selectedItemIndex = selectedItemIndex >= 0 ? selectedItemIndex : previewItemsProp.arraySize - 1;
            DeleteItemAtSelectedIndex();
        }

        /// <summary>
        /// Deletes the preview item at the selected index.
        /// </summary>
        private void DeleteItemAtSelectedIndex()
        {
            if (selectedItemIndex < 0)
            {
                return;
            }

            previewItemsProp.DeleteArrayElementAtIndex(selectedItemIndex);
            previewItemsProp.serializedObject.ApplyModifiedProperties();
            ReinitializeItemRows();
            selectedItemIndex = -1;
        }

        #endregion

        #endregion

        #region Other

        /// <summary>
        /// Updates the row background style based on whether a preview item is assigned.
        /// </summary>
        private void ChooseItemRowBackgroundBasedOnPreviewItemField(VisualElement itemRow, SerializedProperty itemProp)
        {
            VisualElement itemRowWrapper = itemRow.Q<VisualElement>("ItemRowWrapper");

            if (itemProp.FindPropertyRelative("Item").objectReferenceValue != null)
            {
                itemRowWrapper.AddToClassList("ItemRow_ItemAssigned");
                itemRowWrapper.RemoveFromClassList("ItemRow_Invalid");
            }
            else
            {
                itemRowWrapper.RemoveFromClassList("ItemRow_ItemAssigned");
                itemRowWrapper.AddToClassList("ItemRow_Invalid");
            }
        }

        #endregion

        #endregion

        #endregion

    }
}