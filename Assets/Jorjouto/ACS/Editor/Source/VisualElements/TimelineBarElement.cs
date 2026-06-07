// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
/// <summary>
/// A custom <see cref="VisualElement"/> that serves as a timeline bar for animation playback.
/// </summary>
/// <remarks>
/// This element visually represents the animation's length, frame rate, and current time.
/// It also handles user interactions for scrubbing the timeline and updating the animation time.
/// </remarks>
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    [UxmlElement]
    public partial class TimelineBar : VisualElement
    {
        private const string guid = "beaa8212836a3d345bfbd97429a7ea91";
        private readonly string templatePath = AssetDatabase.GUIDToAssetPath(guid);
        private VisualElement timelineBar = null;

        /// <summary>
        /// The current time of the animation playback in seconds.
        /// </summary>
        public float AnimationTime { get; set; }
        
        /// <summary>
        /// The total length of the animation in seconds.
        /// </summary>
        public float AnimationLength { get; set; }
        
        /// <summary>
        /// The frame rate of the animation.
        /// </summary>
        public float FrameRate { get; set; } = 30f;

        /// <summary>
        /// Event triggered when the animation time changes, typically due to user scrubbing.
        /// </summary>
        public event System.Action<float> OnTimeChanged;

        /// <summary>
        /// Flag indicating whether the user is currently dragging the playhead.
        /// </summary>
        private bool isDragging = false;
        
        /// <summary>
        /// Gets a value indicating whether the timeline is currently being dragged.
        /// </summary>
        public bool IsDragging => isDragging;

        /// <summary>
        /// The total number of frames for the animation.
        /// </summary>
        private int totalFrames;
        
        /// <summary>
        /// Calculates the width of a single frame on the timeline.
        /// </summary>
        /// <returns>The width of a frame in pixels.</returns>
        public float GetFrameWidth() => contentRect.width / Mathf.Max(1, totalFrames);

        /// <summary>
        /// Initializes a new instance of the <see cref="TimelineBar"/> class.
        /// </summary>
        public TimelineBar()
        {
            pickingMode = PickingMode.Position;

            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);

            VisualTreeAsset templateAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
            templateAsset.CloneTree(this);
            timelineBar = this.Q<VisualElement>("TimelineBar");
        }

        /// <summary>
        /// Draws the timeline visuals, including the background, tick marks, and the playhead.
        /// </summary>
        /// <param name="ctx">The mesh generation context for drawing.</param>
        private void DrawTimeline()
        {
            if(timelineBar == null)
            {
                return;
            }

            // Draw red playhead bar
            int currentFrame = Mathf.FloorToInt(AnimationTime * FrameRate);
            totalFrames = Mathf.CeilToInt(AnimationLength * FrameRate);

            if(timelineBar.childCount != totalFrames)
            {
                ResetTimelineBar();
            }
            else
            {
                UpdateTimelineBar(currentFrame);
            }
        }

        /// <summary>
        /// Resets the visual representing the timeline bar. All frames will be recalculated and redrawn.
        /// </summary>
        private void ResetTimelineBar()
        {
            timelineBar.Clear();

            if(totalFrames == 0)
            {
                return;
            }

            for (int i = 0; i < totalFrames; i++)
            {
                VisualElement newFrame = new() { name = "timelineFrame", focusable = false };
                newFrame.AddToClassList("TimelineBarFrame");
                timelineBar.Add(newFrame);
            }

            timelineBar[0].AddToClassList("TimelineBarCurrentFrame");
        }

        /// <summary>
        /// Updates the visual representing the current frame in the timeline.
        /// </summary>
        /// <param name="currentFrame">The current frame to represent in the timeline bar (assign current frame styling).</param>
        private void UpdateTimelineBar(int currentFrame)
        {
            for (int i = 0; i < totalFrames; i++)
            {
                timelineBar[i].RemoveFromClassList("TimelineBarCurrentFrame");

                if(currentFrame == i)
                {
                    timelineBar[i].AddToClassList("TimelineBarCurrentFrame");
                }
            }
        }

        /// <summary>
        /// Handles the MouseDown event to start dragging the playhead.
        /// </summary>
        /// <param name="evt">The mouse down event.</param>
        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                isDragging = true;
                UpdateTimeFromPosition(evt.localMousePosition.x);
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Handles the MouseMove event to update the playhead position while dragging.
        /// </summary>
        /// <param name="evt">The mouse move event.</param>
        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!isDragging)
                return;

            UpdateTimeFromPosition(evt.localMousePosition.x);
            evt.StopPropagation();
        }

        /// <summary>
        /// Handles the MouseUp event to stop dragging the playhead.
        /// </summary>
        /// <param name="evt">The mouse up event.</param>
        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button == 0)
            {
                isDragging = false;
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Handles the MouseLeave event to stop dragging if the mouse leaves the element.
        /// </summary>
        /// <param name="evt">The mouse leave event.</param>
        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            isDragging = false;
        }

        /// <summary>
        /// Updates the animation time based on a local horizontal mouse position.
        /// </summary>
        /// <param name="localX">The local x-coordinate of the mouse.</param>
        private void UpdateTimeFromPosition(float localX)
        {
            if (AnimationLength <= 0 || FrameRate <= 0)
                return;

            float clampedX = Mathf.Clamp(localX, 0f, contentRect.width);
            int frame = Mathf.FloorToInt(clampedX / contentRect.width * totalFrames);
            float newTime = Mathf.Clamp(frame / FrameRate, 0f, AnimationLength);

            AnimationTime = newTime;
            OnTimeChanged?.Invoke(AnimationTime);
            DrawTimeline();
        }

        /// <summary>
        /// Sets the animation time without notifying listeners, primarily used for visual updates.
        /// </summary>
        /// <param name="newTime">The new animation time.</param>
        public void SetTime(float newTime, bool bShouldNotify = false)
        {
            AnimationTime = Mathf.Clamp(newTime, 0f, AnimationLength);

            if(bShouldNotify)
            {
                OnTimeChanged?.Invoke(AnimationTime);
            }
            
            DrawTimeline();
        }
    }
}
    