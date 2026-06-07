using UnityEngine;

using UnityEngine.Animations;
using UnityEngine.Playables;
using System.Collections.Generic;
using System;

namespace Jorjouto.AnimComposerSystem
{
    /// <summary>
    /// Defines the data for a single animation layer within the animation system.
    /// </summary>
    [Serializable]
    public struct AnimationLayerData
    {
        /// <summary>
        /// The avatar mask used to define which body parts are affected by this layer.
        /// </summary>
        [Tooltip("The avatar mask used to define which body parts are affected by this layer.")]
        public AvatarMask AvatarMask;

        /// <summary>
        /// A flag indicating if this layer should be blended additively with lower layers.
        /// </summary>
        [Tooltip("A flag indicating if this layer should be blended additively with lower layers.")]
        public bool IsAdditive;

        /// <summary>
        /// Indicates the weight of this layer (in case additive is used)
        /// </summary>
        [Tooltip("Indicates the weight of this layer (in case additive is used)")]
        [Range(0.0f, 1.0f)]
        public float LayerWeight;
    }

    /// <summary>
    /// A block containing a layer's playback rate and a list of active animation composers.
    /// </summary>
    public class AnimatorLayersBlock
    {
        /// <summary>
        /// The playback rate for all composers within this layer.
        /// </summary>
        [Tooltip("The playback rate for all composers within this layer.")]
        public float Rate = 1f;

        /// <summary>
        /// A list of currently active animation composers on this layer.
        /// </summary>
        [Tooltip("A list of currently active animation composers on this layer.")]
        public List<ScriptableObject_AnimComposer> ActiveComposers = new();
    }

    /// <summary>
    /// Manages the playback of multiple animations using the Unity Playables API.
    /// This component handles blending between animations, managing animation layers,
    /// and applying root motion.
    /// </summary>
    public class AnimCoordinatorComponent : MonoBehaviour
    {
        /// <summary>
        /// Delegate for an event that occurs when an animation composer starts playing.
        /// </summary>
        /// <param name="CurrentComposer">The <see cref="ScriptableObject_AnimComposer"/> that started.</param>
        public delegate void AnimCoordinatorEvent_AnimStart(ScriptableObject_AnimComposer CurrentComposer);

        /// <summary>
        /// Delegate for an event that occurs while an animation composer is updating.
        /// </summary>
        /// <param name="CurrentComposer">The <see cref="ScriptableObject_AnimComposer"/> that is updating.</param>
        public delegate void AnimCoordinatorEvent_AnimUpdate(ScriptableObject_AnimComposer CurrentComposer);

        /// <summary>
        /// Delegate for an event that occurs when an animation composer ends playback.
        /// </summary>
        /// <param name="CurrentComposer">The <see cref="ScriptableObject_AnimComposer"/> that has ended.</param>
        public delegate void AnimCoordinatorEvent_AnimEnd(ScriptableObject_AnimComposer CurrentComposer);

        /// <summary>
        /// Event that is raised when an animation composer begins playing.
        /// </summary>
        public event AnimCoordinatorEvent_AnimStart OnAnimStart;

        /// <summary>
        /// Event that is raised when an animation composer finishes or is stopped.
        /// </summary>
        public event AnimCoordinatorEvent_AnimEnd OnAnimEnd;

        /// <summary>
        /// Event that is raised when foot ik is blocked.
        /// </summary>    
        [Tooltip("Event that is raised when foot ik is blocked.")]
        [SerializeField, HideInInspector]
        protected IKFootUnBlockedChannel OnIKFootBlocked = null;

        /// <summary>
        /// Event that is raised when foot ik is unblocked.
        /// </summary>    
        [Tooltip("Event that is raised when foot ik is unblocked.")]
        [SerializeField, HideInInspector]
        protected IKFootUnblockedChannel OnIKFootUnblocked = null;

        /// <summary>
        /// A list of custom animation layers, each with an AvatarMask and additive property.
        /// </summary>
        [Tooltip("A list of custom animation layers, each with an AvatarMask and additive property.")]
        [SerializeField]
        private List<AnimationLayerData> AnimationLayers = new();

        /// <summary>
        /// The cached reference to the Animator component on this GameObject or its children.
        /// </summary>
        private Animator animator = null;

        /// <summary>
        /// The cached reference to the RuntimeAnimatorController used for the base layer.
        /// </summary>
        private RuntimeAnimatorController animatorController = null;

        /// <summary>
        /// The core PlayableGraph that drives all animations.
        /// </summary>
        private PlayableGraph playableGraph;

        /// <summary>
        /// The top-level mixer that controls the blending of all animation layers.
        /// </summary>
        private AnimationLayerMixerPlayable topLevelMixer;

        private AnimatorControllerPlayable locomotionPlayable;

        /// <summary>
        /// A list of mixer playables, one for each custom animation layer.
        /// </summary>
        private readonly List<AnimationMixerPlayable> layerMixers = new();

        /// <summary>
        /// A list of layers, each containing its playback rate and active anim composers.
        /// </summary>
        public List<AnimatorLayersBlock> LayersAndActiveAnimComposers { get; private set; } = new();

        /// <summary>
        /// A flag to determine if horizontal root motion should be applied from the current animation.
        /// </summary>
        private bool bShouldApplyHorizontalRootMotion = false;

        /// <summary>
        /// A flag to determine if vertical root motion should be applied from the current animation.
        /// </summary>
        private bool bShouldApplyVerticalRootMotion = false;

        /// <summary>
        /// A flag to determine if rotation root motion should be applied from the current animation.
        /// </summary>
        private bool bShouldApplyRotationRootMotion = false;

        /// <summary>
        /// A flag to block root motion application, even if the animation has it.
        /// </summary>
        private bool bIsRootMotionBlocked = false;

        /// <summary>
        /// The cached reference to the CharacterController component (if found).
        /// </summary>
        private CharacterController characterController;

        /// <summary>
        /// The cached reference to the Rigidbody component (if found).
        /// </summary>
        private Rigidbody rigidBody;

        /// <summary>
        /// The last root motion movement vector, relative to the current frame.
        /// </summary>
        public Vector3 LastRootMotionMovement { get; private set; }

        /// <summary>
        /// A flag to indicate whether hit stop is currently being applied for the character.
        /// </summary>
        public bool IsHitStopApplied { get; private set; }

        /// <summary>
        /// Indicates the current rate being applied to all animations played through the anim composer.
        /// </summary>
        public float Rate { get; private set; } = 1.0f;

        #region Getters

        /// <summary>
        /// Gets a value indicating whether root motion is currently blocked.
        /// </summary>
        /// <returns>True if root motion is blocked, otherwise false.</returns>
        public bool GetIsRootMotionBlocked() => bIsRootMotionBlocked;

        #endregion

        #region Setters

        /// <summary>
        /// Sets the playback rate for all active anim composers across all layers.
        /// </summary>
        /// <param name="newRate">The new playback rate.</param>
        public void SetAnimComposerPlayerRate(float newRate)
        {
            Rate = newRate;

            for (int layer = 0; layer < LayersAndActiveAnimComposers.Count; layer++)
            {
                LayersAndActiveAnimComposers[layer].Rate = newRate;
                layerMixers[layer].SetSpeed(newRate);
            }
        }

        /// <summary>
        /// Sets whether hitstop is being applied (playback rate was decreased)
        /// </summary>
        /// <param name="newRate">The new playback rate.</param>
        public void SetIsHitStopApplied(bool bApply) => IsHitStopApplied = bApply;

        /// <summary>
        /// Enables or disables the blocking of root motion.
        /// </summary>
        /// <param name="bEnable">True to block root motion, false to allow it.</param>
        public void SetRootMotionBlocked(bool bEnable) => bIsRootMotionBlocked = bEnable;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Initializes the PlayableGraph and caches the CharacterController.
        /// </summary>
        private void Awake()
        {
            StoreOrCreateanimatorController();
            InitializePlayableGraph();
            characterController = GetComponent<CharacterController>();
            rigidBody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Called every frame.
        /// Evaluates the PlayableGraph, handles root motion, and updates all active animation composers.
        /// </summary>
        private void Update()
        {
            playableGraph.Evaluate(Time.deltaTime);
            HandleRootMotion();

            // Update each layer independently
            for (int layer = 0; layer < LayersAndActiveAnimComposers.Count; layer++)
            {
                var composers = LayersAndActiveAnimComposers[layer].ActiveComposers;
                var mixer = layerMixers[layer];

                float totalWeight = 0f;

                for (int i = composers.Count - 1; i >= 0; i--)
                {
                    var composer = composers[i];
                    if (composer == null || composer.AnimationClip == null) continue;

                    composer.Tick(Time.deltaTime * LayersAndActiveAnimComposers[layer].Rate);

                    if (composer.ElapsedTime >= composer.AnimationClip.length - 0.02f &&
                        mixer.GetInput(i).GetSpeed() > 0f && composer.AnimationClip.wrapMode != WrapMode.Loop)
                    {
                        mixer.GetInput(i).SetSpeed(0f);
                    }

                    totalWeight += HandleAnimComposerStateLayer(composer, i, layer);
                }

                // Normalize weights
                for (int i = 0; i < composers.Count; i++)
                {
                    float rawWeight = composers[i].CurrentWeight;
                    float ponderedWeight = totalWeight > 0f ? Mathf.Clamp01(rawWeight / totalWeight) : 0f;
                    mixer.SetInputWeight(i, ponderedWeight);
                    composers[i].SetCurrentWeight(ponderedWeight);
                }

                // Apply anim layer's weight as a multiplier
                float layerWeight = Mathf.Clamp01(totalWeight) * AnimationLayers[layer].LayerWeight;
                // Set layer weight in topLevelMixer
                topLevelMixer.SetInputWeight(layer + 1, layerWeight);
            }
        }

        /// <summary>
        /// Called when the GameObject is being destroyed.
        /// Safely destroys the PlayableGraph to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Looks for Animator and Animator Controller.
        /// If an Animator is not found, one is added.
        /// </summary>
        private void StoreOrCreateanimatorController()
        {
            animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                animator = gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = null;
            }
            else
            {
                animatorController = animator.runtimeAnimatorController;
            }
        }

        /// <summary>
        /// Creates and configures the PlayableGraph for animation playback.
        /// </summary>
        private void InitializePlayableGraph()
        {
            playableGraph = PlayableGraph.Create("AnimationSystem");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            SetupAnimations();
            playableGraph.Play();
        }

        /// <summary>
        /// Sets up the core animation playable structure, including the top-level mixer and layered mixers.
        /// </summary>
        private void SetupAnimations()
        {
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = true;

            var playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);

            // Create the top-level mixer with N+1 inputs (base + composer layers)
            bool bNoExtraLayers = AnimationLayers == null || AnimationLayers.Count == 0;
            int totalLayers = AnimationLayers.Count + (bNoExtraLayers ? 2 : 1);
            topLevelMixer = AnimationLayerMixerPlayable.Create(playableGraph, totalLayers);

            // Create the locomotion mixer
            locomotionPlayable = AnimatorControllerPlayable.Create(playableGraph, animatorController);

            // Input 0: locomotion/base
            topLevelMixer.ConnectInput(0, locomotionPlayable, 0);
            topLevelMixer.SetInputWeight(0, 1f);

            // Layered composer mixers
            layerMixers.Clear();
            LayersAndActiveAnimComposers.Clear();

            if (bNoExtraLayers)
            {
                var layerMixer = AnimationMixerPlayable.Create(playableGraph, 0); // Start with 0 inputs
                topLevelMixer.ConnectInput(1, layerMixer, 0);
                topLevelMixer.SetInputWeight(1, 0f);
                topLevelMixer.SetLayerAdditive(1, false);
                layerMixers.Add(layerMixer);
                LayersAndActiveAnimComposers.Add(new AnimatorLayersBlock());
            }
            else
            {
                for (int i = 0; i < AnimationLayers.Count; i++)
                {
                    var layerMixer = AnimationMixerPlayable.Create(playableGraph, 0); // Start with 0 inputs
                    topLevelMixer.ConnectInput(i + 1, layerMixer, 0);
                    topLevelMixer.SetInputWeight(i + 1, 0.0f);
                    topLevelMixer.SetLayerAdditive((uint)(i + 1), AnimationLayers[i].IsAdditive);

                    AvatarMask layerMask = AnimationLayers[i].AvatarMask;
                    if (layerMask != null)
                    {
                        topLevelMixer.SetLayerMaskFromAvatarMask((uint)(i + 1), layerMask);
                    }

                    layerMixers.Add(layerMixer);
                    LayersAndActiveAnimComposers.Add(new AnimatorLayersBlock());
                }

            }
            
            playableOutput.SetSourcePlayable(topLevelMixer);
        }

        #endregion

        #region Anim Composer Playback

        /// <summary>
        /// Plays a single animation composer on its designated layer.
        /// Interrupts any other composers currently playing on the same layer.
        /// </summary>
        /// <param name="newAnimComposer">
        /// The <see cref="ScriptableObject_AnimComposer"/> to play.
        /// </param>
        /// <param name="customBlendInTime">
        /// Optional custom blend-in duration. If not provided, the composer’s
        /// <c>BlendInTime</c> value will be used.
        /// </param>
        /// <param name="customBlendInCurve">
        /// Optional custom blend-in curve to control the interpolation. If not provided,
        /// the composer’s default <c>BlendInCurve</c> will be used.
        /// </param>
        public void PlayAnimComposer(ScriptableObject_AnimComposer newAnimComposer,
                                    float ? customBlendInTime = null, AnimationCurve
                                    customBlendInCurve = null, bool bShouldInstantiate = true, 
                                    bool ? shouldLoop = null)
        {
            if (newAnimComposer == null || newAnimComposer.AnimationClip == null)
            {
                Debug.LogWarning("Invalid composer or AnimationClip.");
                return;
            }

            int layer = newAnimComposer.AnimationLayer; // +1 because 0 is base

            if (layer >= LayersAndActiveAnimComposers.Count)
            {
                Debug.LogWarning($"Layer {layer} does not exist. Please ensure the composer layer is valid.");
                return;
            }

            InterruptActiveAnimComposersInLayer(customBlendInTime ?? newAnimComposer.BlendInTime, layer);

            bool shouldLoopAnim = shouldLoop ?? newAnimComposer.Loop;

            newAnimComposer.AnimationClip.wrapMode = shouldLoopAnim ? WrapMode.Loop : WrapMode.Once;

            SetApplyRootMotions (newAnimComposer.ApplyHorizontalRootMotion,
                                newAnimComposer.ApplyVerticalRootMotion,
                                newAnimComposer.ApplyRotationRootMotion); 

            var newPlayable = AnimationClipPlayable.Create(playableGraph, newAnimComposer.AnimationClip);

            newPlayable.SetSpeed(newAnimComposer.PlayRate);
            newPlayable.SetApplyFootIK(false);

            if (layer == 0)
            {
                // Notify listeners whether foot IK should be considered blocked or unblocked on the base layer.
                if (newAnimComposer.IsFootIK && OnIKFootUnblocked != null)
                {
                    OnIKFootUnblocked.Raise(this);
                }
                else if (!newAnimComposer.IsFootIK && OnIKFootBlocked != null)
                {
                    OnIKFootBlocked.Raise(this);
                }
            }

            // Add to the layer mixer
            var mixer = layerMixers[layer];
            mixer.AddInput(newPlayable, 0, 0f);

            var animComposerInstance = bShouldInstantiate ? Instantiate(newAnimComposer) : newAnimComposer;
            animComposerInstance.Init(gameObject, customBlendInTime, customBlendInCurve, shouldLoop);

            LayersAndActiveAnimComposers[layer].ActiveComposers.Add(animComposerInstance);
            OnAnimStart?.Invoke(animComposerInstance);
        }

        /// <summary>
        /// Configures which components of root motion should be applied
        /// during animation playback.
        /// </summary>
        /// <param name="bApplyHorizontalRootMotion">
        /// Whether to apply horizontal root motion (X and Z axes).
        /// </param>
        /// <param name="bApplyVerticalRootMotion">
        /// Whether to apply vertical root motion (Y axis).
        /// </param>
        /// <param name="bApplyRotationRootMotion">
        /// Whether to apply rotational root motion.
        /// </param>
        private void SetApplyRootMotions(bool bApplyHorizontalRootMotion, bool bApplyVerticalRootMotion, bool bApplyRotationRootMotion)
        {
            bShouldApplyHorizontalRootMotion = bApplyHorizontalRootMotion;
            bShouldApplyVerticalRootMotion = bApplyVerticalRootMotion;
            bShouldApplyRotationRootMotion = bApplyRotationRootMotion;
        }
        
        /// <summary>
        /// Stops all currently active animation composers across every layer,
        /// blending them out over the specified time.
        /// </summary>
        /// <param name="blendOutTime">
        /// The time over which the animations should fade out on all layers.
        /// </param>
        public void InterruptAllAnimComposers(float blendOutTime)
        {
            for (int i = 0; i < LayersAndActiveAnimComposers.Count; i++)
            {
                InterruptActiveAnimComposersInLayer(blendOutTime, i);
            }
            return;
        }

        /// <summary>
        /// Stops all currently active anim composers on a specific layer with a blend-out.
        /// </summary>
        /// <param name="blendOutTime">The time over which the animations should fade out.</param>
        /// <param name="layer">The layer on which to interrupt the animations.</param>
        public void InterruptActiveAnimComposersInLayer(float blendOutTime, int layer)
        {
            if (layer == 0 || layer >= LayersAndActiveAnimComposers.Count)
            {
                SetApplyRootMotions(false, false, false);
            }

            var activeAnimComposers = LayersAndActiveAnimComposers[layer].ActiveComposers;

            for (int i = activeAnimComposers.Count - 1; i >= 0; i--)
            {
                bool bWasPlaying = activeAnimComposers[i].IsPlaying;
                activeAnimComposers[i].Stop(Mathf.Max(blendOutTime, 0.01f));

                if (bWasPlaying)
                {
                    OnAnimEnd?.Invoke(activeAnimComposers[i]);
                }
            }
        }

        #endregion

        #region Animator Wrapped Methods

        public void PlayAnimatorState(string stateName, int layer = 0, float normalizedTime = 0f)
        {
            if (locomotionPlayable.IsValid())
            {
                locomotionPlayable.Play(stateName, layer, normalizedTime);
            }
        }

        #endregion

        #region Anim Composer Updates

        /// <summary>
        /// Handles the state transitions (blend-in, blend-out, full weight) for a single animation composer.
        /// </summary>
        /// <param name="animComposer">The composer to update.</param>
        /// <param name="index">The index of the composer in its layer's list.</param>
        /// <param name="layer">The animation layer index.</param>
        /// <returns>The calculated weight for the composer in its mixer.</returns>
        private float HandleAnimComposerStateLayer(ScriptableObject_AnimComposer animComposer, int index, int layer)
        {
            float elapsedTime = animComposer.ElapsedTime;
            float blendInTime = animComposer.BlendInTime;
            float blendOutTime = animComposer.BlendOutTime;
            float blendOutOffset = animComposer.BlendOutOffset;
            float clipLength = animComposer.AnimationClip.length;

            if (!animComposer.IsPlaying ||
                (elapsedTime >= (clipLength - blendOutTime + blendOutOffset) &&
                animComposer.AnimationClip.wrapMode != WrapMode.Loop))
            {
                return HandleBlendOutLayer(animComposer, index, blendOutTime, layer);
            }
            else if (elapsedTime / animComposer.PlayRate <= blendInTime && blendInTime > 0f)
            {
                return HandleBlendInLayer(animComposer, index, blendInTime, layer);
            }
            else
            {
                EnsureFullWeightLayer(animComposer, index, layer);
                return 1f;
            }
        }

        /// <summary>
        /// Calculates and applies the weight for an animation during its blend-in phase.
        /// </summary>
        /// <param name="animComposer">The composer to blend in.</param>
        /// <param name="index">The index of the composer in its layer's list.</param>
        /// <param name="blendInTime">The total blend-in duration.</param>
        /// <param name="layer">The animation layer index.</param>
        /// <returns>The calculated weight.</returns>
        private float HandleBlendInLayer(ScriptableObject_AnimComposer animComposer, int index, float blendInTime, int layer)
        {
            float t = Mathf.Clamp01(animComposer.CurrentBlendInTime / blendInTime);
            float weight = IsWeightCurveValid(animComposer.BlendInCurve) ? animComposer.BlendInCurve.Evaluate(t) : t;

            animComposer.SetCurrentWeight(weight);
            layerMixers[layer].SetInputWeight(index, weight);
            return weight;
        }

        /// <summary>
        /// Calculates and applies the weight for an animation during its blend-out phase.
        /// Cleans up the composer when the blend-out is complete.
        /// </summary>
        /// <param name="animComposer">The composer to blend out.</param>
        /// <param name="index">The index of the composer in its layer's list.</param>
        /// <param name="blendOutTime">The total blend-out duration.</param>
        /// <param name="layer">The animation layer index.</param>
        /// <returns>The calculated weight.</returns>
        private float HandleBlendOutLayer(ScriptableObject_AnimComposer animComposer, int index, float blendOutTime, int layer)
        {
            if (animComposer.IsPlaying)
            {
                animComposer.Stop();

                if (OnIKFootUnblocked != null)
                {
                    OnIKFootUnblocked.Raise(this);
                }
                
                OnAnimEnd?.Invoke(animComposer);
            }

            float t = Mathf.Clamp01(animComposer.CurrentBlendOutTime / blendOutTime);
            float weight = IsWeightCurveValid(animComposer.BlendOutCurve)
                            ? 1f - animComposer.BlendOutCurve.Evaluate(t) // invert fade-in curve for fade-out
                            : 1f - t;

            layerMixers[layer].SetInputWeight(index, weight);
            animComposer.SetCurrentWeight(weight);

            if (weight == 0.0f)
            {
                DisconnectAnimComposerLayer(index, layer);
                LayersAndActiveAnimComposers[layer].ActiveComposers.RemoveAt(index);
                Destroy(animComposer);
            }

            return weight;
        }

        /// <summary>
        /// Checks if an AnimationCurve is valid for use as a blend-in/out curve.
        /// </summary>
        /// <param name="curve">The curve to validate.</param>
        /// <returns>True if the curve is valid, otherwise false.</returns>
        private bool IsWeightCurveValid(AnimationCurve curve)
        {
            // Check if curve exists
            if (curve == null ||
                curve.length < 2 ||
                curve.keys[0].value != 0f ||
                curve.keys[curve.length - 1].value != 1f ||
                curve.keys[curve.length - 1].time > 1f)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ensures that an animation composer is set to full playback weight (1.0).
        /// </summary>
        /// <param name="animComposer">The composer to set the weight for.</param>
        /// <param name="index">The index of the composer in its layer's list.</param>
        /// <param name="layer">The animation layer index.</param>
        private void EnsureFullWeightLayer(ScriptableObject_AnimComposer animComposer, int index, int layer)
        {
            animComposer.SetCurrentWeight(1f);
            layerMixers[layer].SetInputWeight(index, 1f);
        }

        /// <summary>
        /// Handles the application of root motion from the Animator.
        /// Checks for active animation composers and applies horizontal,
        /// vertical, and rotational root motion selectively, based on the
        /// configured flags. Delegates actual movement and rotation to
        /// <see cref="ApplyRootMotionDisplacement"/> and <see cref="ApplyRootMotionRotation"/>.
        /// </summary>
        private void HandleRootMotion()
        {
            if (bIsRootMotionBlocked)
            {
                return;
            }

            bool hasActiveAnimComposer =
                LayersAndActiveAnimComposers.Count > 0 &&
                LayersAndActiveAnimComposers[0].ActiveComposers.Count > 0;

            if (!hasActiveAnimComposer)
            {
                SetApplyRootMotions(false, false, false);
                return;
            }

            if (animator != null && animator.isActiveAndEnabled)
            {
                Vector3 deltaPos = animator.deltaPosition;

                // Filter components based on flags
                Vector3 appliedDelta = Vector3.zero;

                if (bShouldApplyHorizontalRootMotion)
                {
                    appliedDelta.x = deltaPos.x;
                    appliedDelta.z = deltaPos.z;
                }
                if (bShouldApplyVerticalRootMotion)
                {
                    appliedDelta.y = deltaPos.y;
                }

                if (appliedDelta != Vector3.zero)
                {
                    ApplyRootMotionDisplacement(appliedDelta);
                }

                if (bShouldApplyRotationRootMotion)
                {
                    ApplyRootMotionRotation(animator.deltaRotation);
                }
            }
        }

        /// <summary>
        /// Applies the positional component of root motion to the GameObject.
        /// Chooses the appropriate movement method depending on whether a
        /// <see cref="CharacterController"/>, <see cref="Rigidbody"/>, or plain
        /// Transform is available. Also tracks the last applied root motion
        /// velocity.
        /// </summary>
        /// <param name="deltaPosition">The change in position from root motion.</param>
        protected void ApplyRootMotionDisplacement(Vector3 deltaPosition)
        {
            if (characterController != null && characterController.enabled == true)
            {
                if (deltaPosition.y == 0.0f)
                {
                    deltaPosition.y = -0.01f; // Small downward force to keep grounded
                }
                
                characterController.Move(deltaPosition);
            }
            else if (rigidBody != null && !rigidBody.isKinematic)
            {
                rigidBody.MovePosition(rigidBody.position + deltaPosition);
            }
            else
            {
                transform.position += deltaPosition;
            }

            LastRootMotionMovement = deltaPosition / Time.deltaTime;
        }

        /// <summary>
        /// Applies the rotational component of root motion to the GameObject.
        /// Uses <see cref="Rigidbody.MoveRotation"/> if a non-kinematic Rigidbody
        /// exists, otherwise directly modifies the Transform's rotation.
        /// </summary>
        /// <param name="deltaRotation">The change in rotation from root motion.</param>
        protected void ApplyRootMotionRotation(Quaternion deltaRotation)
        {
            if (rigidBody != null && !rigidBody.isKinematic)
            {
                rigidBody.MoveRotation(rigidBody.rotation * deltaRotation);
            }
            else
            {
                transform.rotation *= deltaRotation;
            }
        }

        #endregion

        #region Anim Composer Cleanup

        /// <summary>
        /// Disconnects a playable from its mixer and compacts the input connections to fill the gap.
        /// </summary>
        /// <param name="inputIndex">The index of the playable to disconnect.</param>
        /// <param name="layer">The animation layer index.</param>
        private void DisconnectAnimComposerLayer(int inputIndex, int layer)
        {
            var mixer = layerMixers[layer];
            var playableToDestroy = mixer.GetInput(inputIndex);
            mixer.DisconnectInput(inputIndex);

            // Compact inputs
            for (int i = inputIndex; i < mixer.GetInputCount() - 1; i++)
            {
                var nextPlayable = mixer.GetInput(i + 1);
                mixer.DisconnectInput(i + 1);
                mixer.ConnectInput(i, nextPlayable, 0);
                float animComposerWeight = mixer.GetInputWeight(i + 1);
                mixer.SetInputWeight(i, animComposerWeight);
            }

            mixer.SetInputCount(mixer.GetInputCount() - 1);

            if (playableToDestroy.IsValid())
            {
                playableGraph.DestroyPlayable(playableToDestroy);
            }
        }

        #endregion
    }
}