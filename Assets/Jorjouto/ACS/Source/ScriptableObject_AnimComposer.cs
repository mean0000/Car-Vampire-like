// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
/// <summary>
/// A ScriptableObject that defines a complete, multi-track animation and its associated data, 
/// including playback settings, preview models, and custom action blocks.
/// </summary>
/// <remarks>
/// This asset is used by the <see cref="AnimCoordinatorComponent"/> to play animations 
/// with custom logic and behavior beyond a standard Unity AnimationClip.
/// </remarks>
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jorjouto.AnimComposerSystem
{
    /// <summary>
    /// Contains data for a single action block, representing a custom event or behavior
    /// that occurs during an animation track.
    /// </summary>
    [Serializable]
    public class ActionBlockData
    {
        /// <summary>
        /// The base class for the custom action or event to be executed.
        /// </summary>
        [SerializeReference]
        public ActionBlock_Base Action = null;

        public bool IsDisabled;

        /// <summary>
        /// The starting frame of the action block within the animation.
        /// </summary>
        public int StartFrame;

        /// <summary>
        /// The ending frame of the action block within the animation.
        /// </summary>
        public int EndFrame;

        /// <summary>
        /// The starting time (in seconds) of the action block.
        /// </summary>
        public float StartTime;

        /// <summary>
        /// The ending time (in seconds) of the action block.
        /// </summary>
        public float EndTime;

        /// <summary>
        /// Creates a new, independent copy of this action block data.
        /// </summary>
        /// <returns>A new <see cref="ActionBlockData"/> instance with cloned values.</returns>
        public ActionBlockData Clone()
        {
            return new ActionBlockData
            {
                StartFrame = StartFrame,
                EndFrame = EndFrame,
                StartTime = StartTime,
                EndTime = EndTime,
                Action = Action.Clone(),
                IsDisabled = IsDisabled
            };
        }

    }

    /// <summary>
    /// Defines a single item to be previewed along with the animation, such as a weapon or accessory.
    /// </summary>
    [Serializable]
    public class PreviewItemData
    {
        /// <summary>
        /// The GameObject to be displayed as a preview item.
        /// </summary>
        public GameObject Item = null;

        /// <summary>
        /// The name of the bone or socket to attach the preview item to.
        /// </summary>
        public string AttachSocket = null;

        /// <summary>
        /// The positional offset of the item relative to its attachment socket.
        /// </summary>
        public Vector3 OffsetPosition;

        /// <summary>
        /// The rotational offset of the item relative to its attachment socket.
        /// </summary>
        public Vector3 OffsetRotation;

        /// <summary>
        /// The scale of the preview item.
        /// </summary>
        public Vector3 Scale = Vector3.one;

        /// <summary>
        /// Whether the preview item is visible.
        /// </summary>
        public bool Visible = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewItemData"/> class with all properties.
        /// </summary>
        /// <param name="item">The GameObject to preview.</param>
        /// <param name="attachSocket">The name of the socket to attach the item to.</param>
        /// <param name="offsetPosition">The positional offset.</param>
        /// <param name="offsetRotation">The rotational offset.</param>
        /// <param name="scale">The scale of the item.</param>
        /// <param name="visible">Whether the item is visible.</param>
        public PreviewItemData(GameObject item, string attachSocket, Vector3 offsetPosition, Vector3 offsetRotation, Vector3 scale, bool visible)
        {
            Item = item;
            AttachSocket = attachSocket;
            OffsetPosition = offsetPosition;
            OffsetRotation = offsetRotation;
            Scale = scale;
            Visible = visible;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PreviewItemData"/> class as a copy of another instance.
        /// </summary>
        /// <param name="other">The <see cref="PreviewItemData"/> instance to copy from.</param>
        public PreviewItemData(PreviewItemData other)
        {
            CopyValues(other);
        }

        /// <summary>
        /// Copies all values from another <see cref="PreviewItemData"/> instance into this one.
        /// </summary>
        /// <param name="other">The source <see cref="PreviewItemData"/> to copy from.</param>
        public void CopyValues(PreviewItemData other)
        {
            Item = other.Item;                       
            AttachSocket = other.AttachSocket;       
            OffsetPosition = other.OffsetPosition;
            OffsetRotation = other.OffsetRotation;
            Scale = other.Scale;
            Visible = other.Visible;
        }

        /// <summary>
        /// Compares this instance to another <see cref="PreviewItemData"/> to check for equality.
        /// </summary>
        /// <param name="other">The other <see cref="PreviewItemData"/> instance to compare against.</param>
        /// <returns>True if the instances are equal, otherwise false.</returns>
        public bool Equals(PreviewItemData other) =>
            other != null
            && Item == other.Item
            && string.Equals(AttachSocket, other.AttachSocket, StringComparison.Ordinal)
            && OffsetPosition == other.OffsetPosition
            && OffsetRotation == other.OffsetRotation
            && Scale == other.Scale
            && Visible == other.Visible;

    }

    /// <summary>
    /// Represents a single animation track that holds a list of action blocks.
    /// </summary>
    [Serializable]
    public class AnimationTrack
    {
        /// <summary>
        /// A list of <see cref="ActionBlockData"/> instances on this track.
        /// </summary>
        public List<ActionBlockData> ActionBlocks = new();
    }

    public class RuntimeAnimComposer
    {
        /// <summary>
        /// Gets a value indicating whether the animation is currently playing.
        /// </summary>
        public bool IsPlaying { get; private set; } 

        /// <summary>
        /// Determines whether the animation clip should loop.
        /// </summary>
        public bool Loop { get; private set; } 

        /// <summary>
        /// Gets the total length of the animation clip in seconds, as defined by the source <see cref="ScriptableObject_AnimComposer"/>.
        /// </summary>
        public float AnimationLength  { get; private set; }

        /// <summary>
        /// Gets the total number of frames in the animation clip, calculated from the clip's length and frame rate.
        /// </summary>
        public int AnimationFrameCount { get; private set; } 

        /// <summary>
        /// An additional duration to extend the animation's total playback time for a more controlled blend-out, as defined by the source <see cref="ScriptableObject_AnimComposer"/>.
        /// </summary>
        public float BlendOutOffset { get; private set; }

        /// <summary>
        /// The duration over which the animation will fade in.
        /// </summary>
        public float BlendInTime { get; private set; }

        /// <summary>
        /// The custom curve used to control the blend-in progression.
        /// </summary>
        public AnimationCurve BlendInCurve { get; private set; }

        /// <summary>
        /// The duration over which the animation will fade out.
        /// </summary>
        public float BlendOutTime { get; private set; }

        /// <summary>
        /// The custom curve used to control the blend-out progression.
        /// </summary>
        public AnimationCurve BlendOutCurve { get; private set; }

        /// <summary>
        /// The playback rate of the animation clip.
        /// </summary>
        public float PlayRate = 1.0f;

        public WrapMode WrapMode { get; private set; }

        /// <summary>
        /// Gets the current elapsed time of the animation playback.
        /// </summary>
        public float ElapsedTime {get; private set;} = 0.0f;

        /// <summary>
        /// Gets the current playback weight of this animation composer within its layer.
        /// </summary>
        public float CurrentWeight { get; private set; } = 0.0f;

        /// <summary>
        /// Gets the GameObject that is "owning" or playing this animation composer.
        /// </summary>
        public GameObject Owner { get; private set; } = null;

        /// <summary>
        /// Sets the current playback weight of this animation composer.
        /// </summary>
        /// <param name="weight">The new weight value.</param>
        public void SetCurrentWeight(float weight) => CurrentWeight = weight;


        /// <summary>
        /// Gets the current elapsed blend-in time.
        /// </summary>
        public float CurrentBlendInTime { get; private set; } = 0.0f;

        /// <summary>
        /// Gets the current elapsed blend-out time.
        /// </summary>
        public float CurrentBlendOutTime { get; private set; } = 0.0f;


        /// <summary>
        /// Gets the source <see cref="ScriptableObject_AnimComposer"/> asset that this runtime instance is based on.
        /// </summary>
        public ScriptableObject_AnimComposer SourceAnimComposer { get; private set; }


        /// <summary>
        /// Gets all of the action blocks from all the tracks in the source
        /// </summary>
        public List<ActionBlockData> ActionBlocks { get; private set; }

        /// <summary>
        /// Initializes the animation composer for playback, setting its initial state.
        /// </summary>
        /// <param name="owner">The GameObject that will own this animation.</param>
        public RuntimeAnimComposer(ScriptableObject_AnimComposer sourceAnimComposer, GameObject owner, 
                        float ? customBlendInTime = null, 
                        AnimationCurve customBlendInCurve = null,
                        bool shouldForceLoop = false)
        {
            IsPlaying = true;
            SourceAnimComposer = sourceAnimComposer;
            ElapsedTime = 0.0f;
            CurrentWeight = 0.0f;
            CurrentBlendInTime = 0f;
            CurrentBlendOutTime = 0f;
            AnimationLength = sourceAnimComposer.AnimationClip.length;
            AnimationFrameCount = Mathf.RoundToInt(AnimationLength * sourceAnimComposer.AnimationClip.frameRate);
            PlayRate = sourceAnimComposer.PlayRate;
            Owner = owner;
            
            BlendInTime = customBlendInTime ?? sourceAnimComposer.BlendInTime;
            BlendInCurve = customBlendInCurve ?? sourceAnimComposer.BlendInCurve;
            BlendOutTime = sourceAnimComposer.BlendOutTime;
            BlendOutCurve = sourceAnimComposer.BlendOutCurve;
            BlendOutOffset = sourceAnimComposer.BlendOutOffset;
            Loop = shouldForceLoop || sourceAnimComposer.Loop;
            WrapMode = sourceAnimComposer.AnimationClip.wrapMode;

            ActionBlocks = new List<ActionBlockData>();

            foreach (var track in sourceAnimComposer.Tracks)
            {
                foreach (var actionBlock in track.ActionBlocks)
                {
                    ActionBlocks.Add(actionBlock.Clone());
                }
            }
        }

        /// <summary>
        /// Updates the state of the animation and processes any active action blocks.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last frame.</param>
        public void Tick(float deltaTime)
        {
            float previousElapsedTime = ElapsedTime;

            if (CurrentBlendInTime < BlendInTime)
            {
                CurrentBlendInTime = Mathf.Min(CurrentBlendInTime + deltaTime, BlendInTime);
            }
            if (!IsPlaying)
            {
                CurrentBlendOutTime = Mathf.Min(CurrentBlendOutTime + deltaTime, BlendOutTime);
            }

            ElapsedTime += deltaTime * PlayRate;

            if (Loop && AnimationFrameCount > 2)
            {
                ElapsedTime %= AnimationLength;
            }
            else
            {
                ElapsedTime = Mathf.Min(ElapsedTime, AnimationLength + Math.Max(0, BlendOutOffset));
            }

            if (!IsPlaying)
            {
                return;
            }

            foreach (var actionBlock in ActionBlocks)
            {
                ActionBlock_Base actionInstance = actionBlock.Action;

                if (actionInstance == null || actionBlock.IsDisabled)
                {
                    continue;
                }

                bool frameOverlapsBlock = CheckFrameOverlapsBlock(previousElapsedTime, ElapsedTime, actionBlock);

                bool actionStartedThisFrame = false;

                if (!actionInstance.IsActive && frameOverlapsBlock)
                {
                    actionInstance.OnStart(Owner, actionBlock.StartTime, actionBlock.EndTime, PlayRate);

                    if (actionInstance.IsActive)
                    {
                        actionInstance.OnUpdate(0.0f);
                        actionStartedThisFrame = true;
                    }
                }

                if (actionInstance.IsActive)
                {
                    if (ElapsedTime >= actionBlock.EndTime ||
                        ElapsedTime < actionBlock.StartTime)
                    {
                        actionInstance.OnExit();
                    }
                    else if (!actionStartedThisFrame)
                    {
                        actionInstance.OnUpdate(deltaTime);
                    }
                }
            }
        }

        /// <summary>
        /// Stops the animation playback and triggers an optional blend-out.
        /// </summary>
        /// <param name="blendOutTime">The optional duration for the blend-out. If null, the asset's default BlendOutTime is used.</param>
        public void Stop(float? blendOutTime = null)
        {
            if (blendOutTime != null)
            {
                BlendOutTime = (float)blendOutTime;
            }

            if (ElapsedTime < (AnimationLength - BlendOutTime + BlendOutOffset))
            {
                ElapsedTime = AnimationLength - BlendOutTime + BlendOutOffset;
                CurrentBlendOutTime = 0f;
            }

            foreach (var actionBlock in ActionBlocks)
            {
                ActionBlock_Base actionInstance = actionBlock.Action;

                if (actionInstance != null && actionInstance.IsActive)
                {
                    actionInstance.OnExit();
                }
            }

            IsPlaying = false;        
        }

        /// <summary>
        /// Determines whether the transition from a previous frame time to the current frame time crosses
        /// into the specified action block.
        /// </summary>
        /// <param name="previousElapsedTime">The elapsed animation time at the previous frame.</param>
        /// <param name="currentElapsedTime">The elapsed animation time at the current frame.</param>
        /// <param name="block">The action block to check for overlap.</param>
        /// <returns><c>true</c> if the time interval between frames overlaps the block; otherwise, <c>false</c>.</returns>
        private static bool CheckFrameOverlapsBlock(float previousElapsedTime, float currentElapsedTime, ActionBlockData block)
        {
            if (currentElapsedTime >= previousElapsedTime)
            {
                return previousElapsedTime < block.EndTime && currentElapsedTime >= block.StartTime;
            }

            return previousElapsedTime < block.EndTime || currentElapsedTime >= block.StartTime;
        }
    }
    
    [CreateAssetMenu(fileName = "ScriptableObject_AnimComposer", menuName = "ScriptableObjects/AnimComposer")]
    public class ScriptableObject_AnimComposer : ScriptableObject
    {
        /// <summary>
        /// The core AnimationClip that this composer asset controls.
        /// </summary>
        public AnimationClip AnimationClip;

        /// <summary>
        /// The model used for previewing the animation in the editor.
        /// </summary>
        public GameObject PreviewModel;

        /// <summary>
        /// A list of items to display on the preview model.
        /// </summary>
        public List<PreviewItemData> PreviewItems = new();

        /// <summary>
        /// A list of animation tracks, each containing custom action blocks.
        /// </summary>
        public List<AnimationTrack> Tracks = new();

        /// <summary>
        /// The animation layer on which this composer will play.
        /// </summary>
        [Min(0)]
        public int AnimationLayer = 0;

        /// <summary>
        /// The duration over which the animation will fade in.
        /// </summary>
        [Min(0.0f)]
        public float BlendInTime = 0.0f;

        /// <summary>
        /// The custom curve used to control the blend-in progression.
        /// </summary>
        public AnimationCurve BlendInCurve;

        /// <summary>
        /// The duration over which the animation will fade out.
        /// </summary>
        [Min(0.0f)]
        public float BlendOutTime = 0.0f;

        /// <summary>
        /// The custom curve used to control the blend-out progression.
        /// </summary>
        public AnimationCurve BlendOutCurve;

        /// <summary>
        /// An additional duration to extend the animation's total playback time for a more controlled blend-out.
        /// </summary>
        [Min(0.0f)]
        public float BlendOutOffset = 0.0f;

        /// <summary>
        /// The playback rate of the animation clip.
        /// </summary>
        [Min(0.0f)]
        public float PlayRate = 1.0f;

        /// <summary>
        /// Determines whether the animation clip should loop.
        /// </summary>
        public bool Loop = false;

        /// <summary>
        /// A flag indicating if this animation uses horizontal root motion.
        /// </summary>
        public bool ApplyHorizontalRootMotion = false;

        /// <summary>
        /// A flag indicating if this animation uses vertical root motion.
        /// </summary>
        public bool ApplyVerticalRootMotion = false;

        /// <summary>
        /// A flag indicating if this animation uses rotation root motion.
        /// </summary>
        public bool ApplyRotationRootMotion = false;

        /// <summary>
        /// A flag to enable or disable the preview of root motion in the editor.
        /// </summary>
        public bool PreviewRootMotion = false;

        /// <summary>
        /// The background color for the animation preview pane in the editor.
        /// </summary>
        public Color PreviewBackgroundColor = Color.gray;

        /// <summary>
        /// Whether preview character and items should use unlit materials.
        /// </summary>
        public bool IsPreviewUnlit = false;

        /// <summary>
        /// A flag indicating whether foot Inverse Kinematics (IK) should be applied.
        /// </summary>
        public bool IsFootIK = false;

        /// <summary>
        /// A flag indicating whether foot Inverse Kinematics (IK) 
        /// should be previewed in the animation preview window in the inspector.
        /// </summary>
        public bool PreviewFootIK = true;

        /// <summary>
        /// A list of curves representing the root motion movement.
        /// </summary>
        public List<AnimationCurve> RootMotionCurves = new();

        /// <summary>
        /// A list of normalized root motion curves.
        /// </summary>
        public List<AnimationCurve> NormalizedRootMotionCurves = new();

        /// <summary>
        /// The number of samples to use for normalizing the root motion curves.
        /// </summary>
        public int NormalizationSamples = 300;
    }
}