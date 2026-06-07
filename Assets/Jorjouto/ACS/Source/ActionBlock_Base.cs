// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
using System;
using UnityEditor;
using UnityEngine;

namespace Jorjouto.AnimComposerSystem
{
    /// <summary>
    /// Attribute that allows defining sub-groups for each action block type.
    /// This enables grouping in context menus for easier selection without instantiating them.
    /// The grouping is handled via reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ActionSubGroupAttribute : Attribute
    {
        /// <summary>
        /// The name of the subgroup.
        /// </summary>
        public string Group { get; }

        /// <summary>
        /// Creates a new instance of <see cref="ActionSubGroupAttribute"/>.
        /// </summary>
        /// <param name="group">The subgroup name.</param>
        public ActionSubGroupAttribute(string group) => Group = group;
    }

    /// <summary>
    /// Attribute that allows defining a specific color for an action block type.
    /// If not applied, the action block color defaults to black.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class BlockColorAttribute : Attribute
    {
        /// <summary>
        /// The color value in hex format.
        /// </summary>
        public string ColorHex { get; }

        /// <summary>
        /// Creates a new instance of <see cref="BlockColorAttribute"/>.
        /// </summary>
        /// <param name="hex">The hex color string.</param>
        public BlockColorAttribute(string hex) => ColorHex = hex;
    }

    /// <summary>
    /// The abstract base class for all action blocks.
    /// Provides lifecycle methods for execution and debugging, along with shared properties.
    /// </summary>
    [Serializable]
    public abstract class ActionBlock_Base
    {
        [Header("Base Settings")]
        [Space(10)]

        /// <summary>
        /// Whether debugging is enabled for this action block.
        /// </summary>
        [Tooltip("Whether debugging is enabled for this action block.")]
        public bool DebugEnabled = false;

        /// <summary>
        /// Whether debugging should exit automatically when the animation ends.
        /// </summary>
        [Tooltip("Whether debugging should exit automatically when the animation ends.")]
        public bool DebugExitOnAnimationEnd = false;

        /// <summary>
        /// Whether the action block is currently active.
        /// </summary>
        public bool IsActive { get; protected set; } = false;

        /// <summary>
        /// The owner GameObject of this action block.
        /// </summary>
        public GameObject Owner { get; private set; } = null;

        /// <summary>
        /// The GameObject used for preview rendering during debug mode.
        /// </summary>
        protected GameObject previewObject = null;

        /// <summary>
        /// The utility used for preview rendering in debug mode.
        /// </summary>
        #if UNITY_EDITOR
        protected PreviewRenderUtility previewRenderUtility = null;
        #endif

        /// <summary>
        /// The audio source used during debug playback.
        /// </summary>
        protected AudioSource debugAudioSource = null;

        /// <summary>
        /// The start time of the action block.
        /// </summary>
        protected float startTime;

        /// <summary>
        /// The end time of the action block.
        /// </summary>
        protected float endTime;

        /// <summary>
        /// The execution rate of the action block.
        /// </summary>
        protected float rate;

        /// <summary>
        /// The duration of the action block.
        /// </summary>
        protected float duration;

        /// <summary>
        /// Creates a shallow copy of this action block instance.
        /// </summary>
        /// <returns>A cloned instance of the action block.</returns>
        public virtual ActionBlock_Base Clone()
        {
            return (ActionBlock_Base)MemberwiseClone();
        }

        /// <summary>
        /// Called when the action block starts execution.
        /// Initializes timing values and determines if the block can be activated.
        /// </summary>
        /// <param name="owner">The GameObject that owns this action block.</param>
        /// <param name="startTime">The start time of the action.</param>
        /// <param name="endTime">The end time of the action.</param>
        /// <param name="rate">The execution rate.</param>
        public virtual void OnStart(GameObject owner, float startTime, float endTime, float rate)
        {
            Owner = owner;
            this.startTime = startTime;
            this.endTime = endTime;
            this.rate = rate;
            duration = (endTime - startTime) / rate;
            IsActive = CheckCanStartActionBlock();
        }

        /// <summary>
        /// Called every frame during execution.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last update.</param>
        public virtual void OnUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// Called when the action block exits execution.
        /// Resets the <see cref="IsActive"/> flag.
        /// </summary>
        public virtual void OnExit()
        {
            IsActive = false;
        }

        /// <summary>
        /// Determines whether the action block can start execution.
        /// Override to add custom validation.
        /// </summary>
        /// <returns>True if the block can start, false otherwise.</returns>
        public virtual bool CheckCanStartActionBlock()
        {
            return true;
        }

        #region Debug

        #if UNITY_EDITOR

        /// <summary>
        /// Called when starting the action block in debug mode.
        /// Initializes debug-specific fields and calculates duration.
        /// </summary>
        /// <param name="previewRenderUtility">The preview render utility for debugging.</param>
        /// <param name="previewObject">The preview GameObject used in debugging.</param>
        /// <param name="debugAudioSource">The audio source used for debug playback.</param>
        /// <param name="startTime">The debug start time.</param>
        /// <param name="endTime">The debug end time.</param>
        /// <param name="rate">The debug execution rate.</param>
        public virtual void OnDebugStart(PreviewRenderUtility previewRenderUtility,
                                        GameObject previewObject,
                                        AudioSource debugAudioSource,
                                        float startTime,
                                        float endTime,
                                        float rate)
        {
            this.previewRenderUtility = previewRenderUtility;
            this.previewObject = previewObject;
            this.debugAudioSource = debugAudioSource;
            this.startTime = startTime;
            this.endTime = endTime;
            this.rate = rate;
            duration = (endTime - startTime) / rate;
        }

        /// <summary>
        /// Called every frame in debug mode.
        /// </summary>
        /// <param name="deltaTime">The time step of the update.</param>
        public virtual void OnDebugUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// Called when exiting debug mode.
        /// </summary>
        public virtual void OnDebugExit()
        {
        }

        #endif

        #endregion
    }
}
