// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
/// <summary>
/// A VisualElement that represents an animation track in the AnimComposer editor.
/// </summary>
/// <remarks>
/// This element manages a collection of <see cref="ActionBlockElement"/>s, handles user
/// interactions like selection and context menus, and relays events to the parent editor window.
/// It also manages the custom mouse cursor when hovering over its action blocks.
/// </remarks>
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    public class ActionTrackElement : VisualElement
    {
        #region Events

        /// <summary>
        /// Event triggered when an action block on this track is selected.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockSelected;
        
        /// <summary>
        /// Event triggered when this track is selected (by clicking on empty space).
        /// </summary>
        public event Action<ActionTrackElement> OnActionTrackSelected;
        
        /// <summary>
        /// Event triggered when an action block is copied.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockCopied;
        
        /// <summary>
        /// Event triggered when an action block is deleted.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockDeleted;

        //Menu options
        /// <summary>
        /// Event triggered when the "Copy Track" context menu option is clicked.
        /// </summary>
        public event Action<ActionTrackElement> OnTrackMenuClickCopy;
        
        /// <summary>
        /// Event triggered when the "Paste Action Block" context menu option is clicked.
        /// </summary>
        /// <param name="position">The mouse position where the menu was clicked.</param>
        public event Action<ActionTrackElement, Vector2> OnTrackMenuClickPasteBlock;
        
        /// <summary>
        /// Event triggered when the "Paste Action Track" context menu option is clicked.
        /// </summary>
        public event Action<ActionTrackElement> OnTrackMenuClickPasteTrack;
        
        /// <summary>
        /// Event triggered when the "Add new Action Block" context menu option is clicked.
        /// </summary>
        /// <param name="position">The mouse position where the menu was clicked.</param>
        /// <param name="typeIndex">The index of the action block type to add.</param>
        public event Action<ActionTrackElement, Vector2, int> OnTrackMenuClickAddBlock;
        
        /// <summary>
        /// Event triggered when the "Delete Track" context menu option is clicked.
        /// </summary>
        public event Action<ActionTrackElement> OnTrackMenuClickDelete;
        
        /// <summary>
        /// Event triggered when the "Insert new Track" context menu option is clicked.
        /// </summary>
        public event Action<ActionTrackElement> OnTrackMenuClickInsert;
        
        /// <summary>
        /// Event triggered when the "Move up Track" context menu option is clicked.
        /// </summary>
        public event Action<ActionTrackElement> OnTrackMenuClickMoveUp;
        
        /// <summary>
        /// Event triggered when the "Move down Track" context menu option is clicked.
        /// </summary>
        public event Action<ActionTrackElement> OnTrackMenuClickMoveDown;

        #endregion

        #region Variables

        #region Public Variables

        /// <summary>
        /// Gets the index of this track within the list of tracks.
        /// </summary>
        public int TrackIndex { get; private set; }

        #endregion

        #region Private Variables

        /// <summary>
        /// The underlying data object for this animation track.
        /// </summary>
        private readonly AnimationTrack trackData;
        
        /// <summary>
        /// The panel element used to display data for selected action blocks.
        /// </summary>
        private readonly VisualElement actionBlockData;
        
        /// <summary>
        /// A reference to the custom cursor element.
        /// </summary>
        private readonly CursorElement customCursor;
        
        /// <summary>
        /// A list of all <see cref="ActionBlockElement"/>s on this track.
        /// </summary>
        private readonly List<ActionBlockElement> blockElements = new();
        
        /// <summary>
        /// The action block currently being hovered over by the mouse.
        /// </summary>
        private ActionBlockElement hoveredActionBlock;
        
        /// <summary>
        /// The position of the context menu when it is opened.
        /// </summary>
        private Vector2 contentMenuPosition;
        
        /// <summary>
        /// An array of action block type names that can be added to this track.
        /// </summary>
        private readonly string[] actionBlockTypeNames;

        #endregion

        #endregion   
        
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionTrackElement"/> class.
        /// </summary>
        /// <param name="actionBlockTypeNames">A list of available action block type names.</param>
        /// <param name="customCursor">The custom cursor element.</param>
        /// <param name="actionBlockDataContainer">The container for action block data.</param>
        /// <param name="trackData">The underlying data for this track.</param>
        /// <param name="trackIndex">The index of this track.</param>
        public ActionTrackElement(
            string[] actionBlockTypeNames,
            CursorElement customCursor,
            VisualElement actionBlockDataContainer,
            AnimationTrack trackData,
            int trackIndex)
        {
            this.actionBlockTypeNames = actionBlockTypeNames;
            this.customCursor = customCursor;
            actionBlockData = actionBlockDataContainer;
            TrackIndex = trackIndex;
            this.trackData = trackData;
            focusable = true;

            AddToClassList("action-track");
            ExecuteTrackClickBindings();
            DeselectTrack();
        }

        #endregion

        #region Functions

        #region Public Functions

        /// <summary>
        /// Rebuilds all action block elements on this track based on the current track data.
        /// </summary>
        /// <param name="animationLength">The total length of the animation clip.</param>
        /// <param name="frameRate">The frame rate of the animation.</param>
        public void RebuildActionBlocks(float animationLength, float frameRate)
        {
            Clear();
            blockElements.Clear();
            for (int blockIndex = 0; blockIndex < trackData.ActionBlocks.Count; blockIndex++)
            {
                ActionBlockData actionBlock = trackData.ActionBlocks[blockIndex];

                var blockElement = new ActionBlockElement(
                    customCursor,
                    actionBlockData,
                    actionBlock,
                    TrackIndex,
                    blockIndex,
                    animationLength,
                    frameRate
                );

                //Relay event upwards.
                blockElement.OnActionBlockSelected += (actionBlock) => OnActionBlockSelected?.Invoke(actionBlock); ;
                blockElement.OnActionBlockHovered += (actionBlock) => hoveredActionBlock = actionBlock;
                blockElement.OnActionBlockUnHovered += OnActionBlockUnHovered;
                blockElement.OnActionBlockReleased += OnActionBlockReleased;
                blockElement.OnMouseMoveInBlock += OnMouseMoveInsideBlock;
                blockElement.OnActionBlockCopied += (actionBlock) => OnActionBlockCopied?.Invoke(actionBlock);
                blockElement.OnActionBlockDeleted += (actionBlock) => OnActionBlockDeleted?.Invoke(actionBlock);

                Add(blockElement);
                blockElements.Add(blockElement);
            }
        }

        /// <summary>
        /// Deselects the track, removing its highlight.
        /// </summary>
        public void DeselectTrack()
        {
            if(customCursor == null)
            {
                return;
            }
            customCursor.SetHasItemSelected(false);
            RemoveFromClassList("action-track-selected");
        }

        #endregion

        #region Private Functions

        /// <summary>
        /// Registers callbacks for mouse events on the track to handle selection and context menus.
        /// </summary>
        private void ExecuteTrackClickBindings()
        {
            // Clicking empty space on track triggers track click
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    OnActionTrackSelected?.Invoke(this);
                    
                    if (actionBlockData != null)
                    {
                        actionBlockData.Clear();
                        actionBlockData.style.display = DisplayStyle.None;
                    }

                    AddToClassList("action-track-selected");
                }
                else if (evt.button == 1) // 1 = right mouse button in UI Toolkit
                {
                    var menu = new GenericMenu();

                    if (customCursor.GetHasBlockCopied())
                    {
                        menu.AddItem(new GUIContent("Paste Action Block (V)"), false, () => OnTrackMenuClickPasteBlock?.Invoke(this, contentMenuPosition));
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Action Block (V)"));
                    }

                    if (customCursor.GetHasTrackCopied())
                    {
                        menu.AddItem(new GUIContent("Paste Action Track (D)"), false, () => OnTrackMenuClickPasteTrack?.Invoke(this));
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Action Track (D)"));
                    }

                    // Add an "Add" option for each action type
                    for (int i = 0; i < actionBlockTypeNames.Length; i++)
                    {
                        int capturedIndex = i;
                        menu.AddItem(new GUIContent($"Add new Action Block /{actionBlockTypeNames[i]}"), false, () =>
                        {
                            OnTrackMenuClickAddBlock?.Invoke(this, contentMenuPosition, capturedIndex);
                        });
                    }

                    menu.AddItem(new GUIContent("Copy Track (C)"), false, () => OnTrackMenuClickCopy?.Invoke(this));
                    menu.AddItem(new GUIContent("Delete Track (Del or - or BackSpace)"), false, () => OnTrackMenuClickDelete?.Invoke(this));
                    menu.AddItem(new GUIContent("Insert new Track (I or +)"), false, () => OnTrackMenuClickInsert?.Invoke(this));
                    menu.AddItem(new GUIContent("Move up Track (W or Up Arrow)"), false, () => OnTrackMenuClickMoveUp?.Invoke(this));
                    menu.AddItem(new GUIContent("Move down Track (S or Down Arrow)"), false, () => OnTrackMenuClickMoveDown?.Invoke(this));

                    // Show the menu at the current mouse position
                    menu.DropDown(new Rect(evt.mousePosition, Vector2.zero));
                    contentMenuPosition = evt.localMousePosition;
                }

                evt.StopPropagation();
            });
        }

        /// <summary>
        /// Handles the event when the mouse leaves an action block.
        /// </summary>
        /// <param name="actionBlock">The action block that the mouse has left.</param>
        private void OnActionBlockUnHovered(ActionBlockElement actionBlock)
        {
            if (hoveredActionBlock == actionBlock)
            {
                hoveredActionBlock = null;
                customCursor.TryChangeMouseCursor(null, true);
            }
        }

        /// <summary>
        /// Handles the event when the mouse is released from an action block.
        /// </summary>
        /// <param name="actionBlock">The action block that was released.</param>
        private void OnActionBlockReleased(ActionBlockElement actionBlock)
        {
            if (hoveredActionBlock == null)
            {
                customCursor.TryChangeMouseCursor(null, false);
            }
        }

        /// <summary>
        /// Handles mouse movement events inside an action block to change the cursor.
        /// </summary>
        /// <param name="actionBlock">The action block the mouse is inside.</param>
        /// <param name="mouseXPos">The local horizontal mouse position.</param>
        private void OnMouseMoveInsideBlock(ActionBlockElement actionBlock, float mouseXPos)
        {
            if (hoveredActionBlock != actionBlock)
            {
                return;
            }

            float edgeThreshold = actionBlock.GetEdgeThreshold();

            if (mouseXPos < edgeThreshold || mouseXPos > actionBlock.layout.width - edgeThreshold)
            {
                customCursor.TryChangeMouseCursor("StretchCursor", true);
            }
            else
            {
                customCursor.TryChangeMouseCursor("DragCursor", true);
            }
        }

        #endregion

        #endregion
    }
}