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
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    
    public class ActionBlockElement : VisualElement
    {
        #region Events

        /// <summary>
        /// Event triggered when this action block is selected.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockSelected;
        
        /// <summary>
        /// Event triggered when the mouse hovers over this action block.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockHovered;
        
        /// <summary>
        /// Event triggered when the mouse leaves this action block.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockUnHovered;
        
        /// <summary>
        /// Event triggered when the mouse button is released after interacting with this block.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockReleased;
        
        /// <summary>
        /// Event triggered when the mouse moves inside the block.
        /// </summary>
        public event Action<ActionBlockElement, float> OnMouseMoveInBlock;

        /// <summary>
        /// Event triggered when the action block is copied via the context menu.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockCopied;

        /// <summary>
        /// Event triggered when the action block is deleted via the context menu.
        /// </summary>
        public event Action<ActionBlockElement> OnActionBlockDeleted;

        #endregion
              
        #region Variables

        #region Public Variables

        /// <summary>
        /// The index of the track this action block belongs to.
        /// </summary>
        public int TrackIndex { get; private set; }
        
        /// <summary>
        /// The index of this action block within its track.
        /// </summary>
        public int ActionBlockIndex { get; private set; }

        #endregion

        #region Private Variables

        /// <summary>
        /// The threshold for detecting a resize attempt on the block's edges.
        /// </summary>
        /// <returns>The edge threshold in pixels.</returns>
        private const float edgeThreshold = 10f;

        /// <summary>
        /// The underlying data object for this action block.
        /// </summary>
        private readonly ActionBlockData actionBlockData;
        
        /// <summary>
        /// The total length of the animation clip in seconds.
        /// </summary>
        private readonly float clipLength;
        
        /// <summary>
        /// The total number of frames in the animation clip.
        /// </summary>
        private readonly int clipFrames;
        
        /// <summary>
        /// The frame rate of the animation clip.
        /// </summary>
        private readonly float frameRate;
        
        /// <summary>
        /// The name of the action, derived from its type.
        /// </summary>
        private readonly string actionName;

        /// <summary>
        /// The Label assigned to the action block.
        /// </summary>
        private Label label;
        
        /// <summary>
        /// Flag indicating if the user is currently resizing the left edge of the block.
        /// </summary>
        private bool resizingLeft;
        
        /// <summary>
        /// Flag indicating if the user is currently resizing the right edge of the block.
        /// </summary>
        private bool resizingRight;
        
        /// <summary>
        /// Flag indicating if the user is currently dragging the block.
        /// </summary>
        private bool dragging;
        
        /// <summary>
        /// The accumulated mouse movement delta for drag/resize operations.
        /// </summary>
        private float accumulatedMouseDelta;
        
        /// <summary>
        /// The width of a single frame in pixels on the timeline.
        /// </summary>
        private float frameWidth;
        
        /// <summary>
        /// The VisualElement panel used to display the details of the selected action block.
        /// </summary>
        private readonly VisualElement actionBlockDataPanel;
        
        /// <summary>
        /// A reference to the custom cursor element for changing cursor visuals.
        /// </summary>
        private readonly CursorElement customCursor;

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionBlockElement"/> class.
        /// </summary>
        /// <param name="customCursor">The custom cursor element.</param>
        /// <param name="actionBlockDataPanel">The panel for displaying action block data.</param>
        /// <param name="data">The underlying data for this action block.</param>
        /// <param name="trackIndex">The index of the parent track.</param>
        /// <param name="actionBlockIndex">The index of this block within its track.</param>
        /// <param name="clipLength">The total animation clip length.</param>
        /// <param name="frameRate">The frame rate of the animation.</param>
        public ActionBlockElement(
            CursorElement customCursor,
            VisualElement actionBlockDataPanel,
            ActionBlockData data,
            int trackIndex,
            int actionBlockIndex,
            float clipLength,
            float frameRate)
        {
            this.customCursor = customCursor;
            this.actionBlockDataPanel = actionBlockDataPanel;
            actionBlockData = data;
            this.clipLength = clipLength;
            clipFrames = Mathf.CeilToInt(clipLength * frameRate);
            this.frameRate = frameRate;
            TrackIndex = trackIndex;
            ActionBlockIndex = actionBlockIndex;

            AddToClassList("action-block");
            AddToClassList("action-block-selected");
            EnableInClassList("action-block-selected", false);
            UpdatePosition();

            if(data.Action != null)
            {
                style.backgroundColor = GetColorForType(data.Action.GetType());

                string actionType = data.Action.GetType().Name;
                actionName = actionType.StartsWith("ActionBlock_") ? actionType["ActionBlock_".Length..] : actionType;

                label = new(actionName + " (" + actionBlockData.StartFrame + " - " + actionBlockData.EndFrame + ")");
                label.AddToClassList("action-block-label");
                label.pickingMode = PickingMode.Ignore;
                tooltip = "ActionBlock element. Click to open details. Drag to move. Drag edges to resize. Right-click for options.";

                Add(label);

                RegisterCallback<MouseDownEvent>(OnMouseDownInsideBlock);
                RegisterCallback<MouseMoveEvent>(OnMouseMoveInsideBlock);
                RegisterCallback<MouseEnterEvent>(OnBlockHovered);
                RegisterCallback<MouseLeaveEvent>(OnBlockUnHovered);
            }
        }

        #endregion
        
        #region Functions

        #region Public Functions

        public float GetEdgeThreshold() => edgeThreshold;

        /// <summary>
        /// Resets the dragging and resizing state when the mouse is released.
        /// </summary>
        public void ReleaseActionBlock()
        {
            OnActionBlockReleased?.Invoke(this);

            resizingLeft = false;
            resizingRight = false;
            dragging = false;

            customCursor.SetHasItemSelected(false);
        }

        /// <summary>
        /// Updates the position and size of the block based on mouse movement.
        /// </summary>
        /// <param name="deltaX">The horizontal mouse movement delta.</param>
        public void OnMouseMove(float deltaX)
        {
            if (!dragging && !resizingLeft && !resizingRight)
            {
                return; // No interaction, no need to update
            }

            accumulatedMouseDelta += deltaX;

            int deltaFrame = Mathf.RoundToInt(accumulatedMouseDelta / frameWidth);

            if (Mathf.Abs(deltaFrame) < 1)
            {
                return; // No movement, no need to update
            }

            accumulatedMouseDelta = 0.0f;

            if (dragging)
            {
                 int deltaFrameInt = Mathf.Clamp(
                                                    deltaFrame,
                                                    -actionBlockData.StartFrame,
                                                    clipFrames - actionBlockData.EndFrame
                                                    );

                actionBlockData.StartFrame += deltaFrameInt;
                actionBlockData.EndFrame += deltaFrameInt;
                actionBlockData.StartTime = actionBlockData.StartFrame / frameRate;
                actionBlockData.EndTime = actionBlockData.EndFrame / frameRate;
                UpdatePosition();
                UpdateBlockLabel();
                label.text = actionName + " (" + actionBlockData.StartFrame + " - " + actionBlockData.EndFrame + ")";
            }
            else if (resizingLeft)
            {
                // Clamp so that start frame never goes below 0 and block never shrinks below 1 frame
                int minDelta = -actionBlockData.StartFrame;
                int maxDelta = actionBlockData.EndFrame - actionBlockData.StartFrame - 1;

                int deltaFrameInt = Mathf.Clamp(deltaFrame, minDelta, maxDelta);

                actionBlockData.StartFrame += deltaFrameInt;
                actionBlockData.StartTime = actionBlockData.StartFrame / frameRate;

                UpdatePosition();
                UpdateBlockLabel();
            }
            else if (resizingRight)
            {
                // Clamp so that end frame never goes beyond clipFrames and block never shrinks below 1 frame
                int minDelta = -(actionBlockData.EndFrame - actionBlockData.StartFrame - 1);
                int maxDelta = clipFrames - actionBlockData.EndFrame;

                int deltaFrameInt = Mathf.Clamp(deltaFrame, minDelta, maxDelta);

                actionBlockData.EndFrame += deltaFrameInt;
                actionBlockData.EndTime = actionBlockData.EndFrame / frameRate;

                UpdatePosition();
                UpdateBlockLabel();
            }
        }

        /// <summary>
        /// Deselects the action block, hiding its selection highlight and data panel.
        /// </summary>
        public void DeselectBlock()
        {
            EnableInClassList("action-block-selected", false);

            customCursor.SetHasItemSelected(false);

            if (actionBlockDataPanel != null)
            {
                actionBlockDataPanel.Clear();
            }
        }

        /// <summary>
        /// Selects the action block, highlights it, and displays its properties in the data panel.
        /// </summary>
        /// <param name="serializedAnimComposer">The serialized object of the animation composer.</param>
        public void SelectActionBlock(SerializedObject serializedAnimComposer)
        {
            EnableInClassList("action-block-selected", true);

            serializedAnimComposer.Update();
            SerializedProperty actionProp = serializedAnimComposer.FindProperty($"Tracks.Array.data[{TrackIndex}].ActionBlocks.Array.data[{ActionBlockIndex}].Action");

            actionBlockDataPanel.Clear();

            if (actionProp != null)
            {
                actionProp.isExpanded = true;

                PropertyField actionBlockPropertyField = new(actionProp)
                {
                    name = "ActionBlockProperties",
                    label = actionName
                };

                float desiredAlpha = 0.8f;

                var originalColor = style.backgroundColor.value;
                Color finalBackgroundColor = new(originalColor.r, originalColor.g, originalColor.b, desiredAlpha);

                var panelStyle = actionBlockDataPanel.style;

                panelStyle.backgroundColor = finalBackgroundColor;
                actionBlockPropertyField.AddToClassList("action-blockData");

                actionBlockPropertyField.BindProperty(actionProp);
                actionBlockDataPanel.Add(actionBlockPropertyField);
                actionBlockDataPanel.style.display = DisplayStyle.Flex;
            }
        }

        #endregion

        #region Private Functions

        /// <summary>
        /// Handles the MouseDown event inside the action block to start drag/resize operations or open a context menu.
        /// </summary>
        /// <param name="evt">The mouse down event.</param>
        private void OnMouseDownInsideBlock(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                var localPos = evt.localMousePosition.x;

                OnActionBlockSelected?.Invoke(this);
                customCursor.SetHasItemSelected(true);

                frameWidth = parent.layout.width / clipFrames;

                if (localPos < edgeThreshold || localPos > layout.width - edgeThreshold)
                {
                    float distToLeft = localPos;
                    float distToRight = layout.width - localPos;

                    if (distToLeft <= distToRight)
                    {
                        resizingLeft = true;
                        accumulatedMouseDelta = 0.0f;
                    }
                    else
                    {
                        resizingRight = true;
                        accumulatedMouseDelta = 0.0f;
                    }
                }
                else
                {
                    dragging = true;
                    accumulatedMouseDelta = 0.0f;
                }
            }
            // Check if right-click
            else if (evt.button == 1)
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Copy Action Block (C)"), false, () => OnActionBlockCopied?.Invoke(this));
                menu.AddItem(new GUIContent("Delete Action Block (Del or - or BackSpace)"), false, () => OnActionBlockDeleted?.Invoke(this));

                // Show the menu at the current mouse position
                menu.DropDown(new Rect(evt.mousePosition, Vector2.zero));
            }

            evt.StopPropagation();
        }


        /// <summary>
        /// Handles the MouseEnter event to notify that the block is being hovered.
        /// </summary>
        /// <param name="evt">The mouse enter event.</param>
        private void OnBlockHovered(MouseEnterEvent evt)
        {
            OnActionBlockHovered?.Invoke(this);
        }

        /// <summary>
        /// Handles the MouseLeave event to notify that the block is no longer being hovered.
        /// </summary>
        /// <param name="evt">The mouse leave event.</param>
        private void OnBlockUnHovered(MouseLeaveEvent evt)
        {
            OnActionBlockUnHovered?.Invoke(this);
        }

        /// <summary>
        /// Handles the MouseMove event inside the block to provide mouse position information.
        /// </summary>
        /// <param name="evt">The mouse move event.</param>
        private void OnMouseMoveInsideBlock(MouseMoveEvent evt)
        {
            OnMouseMoveInBlock?.Invoke(this, evt.localMousePosition.x);
        }

        /// <summary>
        /// Handles the MouseUp event to stop drag/resize operations.
        /// </summary>
        public void OnMouseUp()
        {
            OnActionBlockReleased?.Invoke(this);
            customCursor.SetHasItemSelected(false);

            dragging = false;
            resizingLeft = false;
            resizingRight = false;
        }

        /// <summary>
        /// Updates the visual position and width of the block based on its data.
        /// </summary>
        private void UpdatePosition()
        {
            // Clamp Start and End Times inside the full clip
            actionBlockData.StartTime = Mathf.Clamp(actionBlockData.StartTime, 0f, clipLength);
            actionBlockData.EndTime = Mathf.Clamp(actionBlockData.EndTime, 0f, clipLength);

            // Ensure block is at least 1 frame wide
            float minDuration = 1f / frameRate;
            if (actionBlockData.EndTime - actionBlockData.StartTime < minDuration)
            {
                actionBlockData.EndTime = actionBlockData.StartTime + minDuration;
                if (actionBlockData.EndTime > clipLength)
                {
                    actionBlockData.EndTime = clipLength;
                    actionBlockData.StartTime = actionBlockData.EndTime - minDuration;
                }
            }

            float startPercent = actionBlockData.StartFrame / (float)clipFrames;
            float widthPercent = (actionBlockData.EndFrame - actionBlockData.StartFrame) / (float)clipFrames;

            style.left = Length.Percent(startPercent * 100f);
            style.width = Length.Percent(widthPercent * 100f);
        }

        /// <summary>
        /// Updates the block label to represent the current frame range.
        /// </summary>
        private void UpdateBlockLabel()
        {
            label.text = actionName + " (" + actionBlockData.StartFrame + " - " + actionBlockData.EndFrame + ")";
        }

        /// <summary>
        /// Retrieves the color for a given action block type from a custom attribute.
        /// </summary>
        /// <param name="blockType">The type of the action block.</param>
        /// <returns>The color defined by the <see cref="BlockColorAttribute"/>, or black if not found.</returns>
        private Color GetColorForType(Type blockType)
        {
            var attr = (BlockColorAttribute)Attribute.GetCustomAttribute(blockType, typeof(BlockColorAttribute));
            if (attr != null && ColorUtility.TryParseHtmlString(attr.ColorHex, out var color))
            {
                return color;
            }

            return Color.black; // default if no attribute or invalid
        }

        #endregion

        #endregion
    }
}