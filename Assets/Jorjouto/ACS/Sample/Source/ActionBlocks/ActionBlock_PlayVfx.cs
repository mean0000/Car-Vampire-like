using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Jorjouto.AnimComposerSystem.Sample
{
    /// <summary>
    /// Represents an action block that plays a visual effect (VFX) when executed.
    /// Supports instantiation, attachment to sockets, looping, scaling, and simulation speed adjustments.
    /// Includes debugging functionality for previewing VFX in the editor.
    /// </summary>
    [ActionSubGroup("FX")]
    [BlockColor("#310015ff")]
    [System.Serializable]
    public class ActionBlock_PlayVfx : ActionBlock_Base
    {
        #region Fields

        [Space(10)]
        [Header("Child Variables")]
        [Space(10)]

        /// <summary>
        /// The VFX prefab to instantiate and play.
        /// </summary>
        [Tooltip("The VFX prefab to instantiate and play.")]
        public GameObject VfxToPlay;

        /// <summary>
        /// Determines whether the VFX should loop continuously.
        /// </summary>
        [Tooltip("Determines whether the VFX should loop continuously.")]
        public bool Loop = false;

        /// <summary>
        /// The name of the socket (child transform) where the VFX should spawn.
        /// If null or not found, defaults to the parent transform.
        /// </summary>
        [Tooltip("The name of the socket (child transform) where the VFX should spawn. " +
                "If null or not found, defaults to the parent transform.")]
        public string SpawnSocket = null;

        /// <summary>
        /// Whether the VFX should attach to the socket transform.
        /// </summary>
        [Tooltip("Whether the VFX should attach to the socket transform.")]
        public bool ShouldAttachToSocket = false;

        /// <summary>
        /// Whether the VFX should follow the socket in the y axis. Not needed if ShouldAttachToSocket is true.
        /// </summary>
        [Tooltip("Whether the VFX should attach to the socket transform.")]
        public bool ShouldFollowSocketYTransform = false;

        /// <summary>
        /// The local position offset of the VFX relative to the socket.
        /// </summary>
        [Tooltip("The local position offset of the VFX relative to the socket.")]
        public Vector3 VfxLocalPosition = Vector3.zero;

        /// <summary>
        /// The local rotation offset of the VFX relative to the socket.
        /// </summary>
        [Tooltip("The local rotation offset of the VFX relative to the socket.")]
        public Vector3 VfxLocalRotation = Vector3.zero;

        /// <summary>
        /// The local scale of the VFX.
        /// </summary>
        [Tooltip("The local scale of the VFX.")]
        public Vector3 VfxLocalScale = Vector3.one;

        /// <summary>
        /// The point in time in the simulation where the vfx will start playing.
        /// </summary>
        [Tooltip("The point in time in the simulation where the vfx will start playing.")]
        public float VfxStartTime = 0.0f;

        /// <summary>
        /// The playback rate multiplier for the VFX particle systems.
        /// </summary>
        [Tooltip("The playback rate multiplier for the VFX particle systems.")]
        public float VfxRate = 1.0f;

        #endregion

        #region Properties

        /// <summary>
        /// The instantiated VFX GameObject.
        /// </summary>
        private GameObject vfxObject = null;

        /// <summary>
        /// The transform of the found socket for VFX attachment.
        /// </summary>
        private Transform foundSocket = null;

        #if UNITY_EDITOR

        /// <summary> 
        /// Flag indicating whether the notify state has actually ended. 
        /// Past this time we still simulate the vfx but the particle system is no longer emitting new particles.
        /// </summary>
        private bool hasVfxEnded = false;

        /// <summary>
        /// The primary particle system used for debugging and updates.
        /// </summary>
        private ParticleSystem particleSystem = null;

        /// <summary> 
        /// The debug timer to track elapsed time during debug playback.
        /// </summary>
        private float debugTimer = 0.0f;

        #endif

        #endregion

        #region Standard Functions

        /// <summary>
        /// Called when the action block starts execution.
        /// Instantiates the VFX and applies transforms and particle settings.
        /// </summary>
        /// <param name="owner">The GameObject that owns this action block.</param>
        /// <param name="startTime">The start time of the action.</param>
        /// <param name="endTime">The end time of the action.</param>
        /// <param name="rate">The rate of execution.</param>
        public override void OnStart(GameObject owner, float startTime, float endTime, float rate)
        {
            base.OnStart(owner, startTime, endTime, rate);

            if (IsActive)
            {
                vfxObject = Object.Instantiate(VfxToPlay);

                if(VfxStartTime > 0.0f)
                {
                    ParticleSystem mainParticle = vfxObject.GetComponentInChildren<ParticleSystem>();
                    mainParticle.Simulate(VfxStartTime, withChildren: true, restart: false);
                    mainParticle.Play();
                }

                ApplyVfxTransform(owner);
                PrepareParticles();
            }
        }

        /// <summary>
        /// Called during action block updates.
        /// Updates the VFX transform if necessary.
        /// </summary>
        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            UpdateVfxTransform(Owner, false);
        }

        /// <summary>
        /// Called when the action block exits execution.
        /// Stops looping particle systems if applicable.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (vfxObject == null || !Loop)
            {
                return;
            }

            vfxObject.transform.SetParent(null, true);

            foreach (var ps in vfxObject.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>
        /// Checks whether the action block can start execution.
        /// </summary>
        /// <returns>True if the owner and VFX prefab are valid, false otherwise.</returns>
        public override bool CheckCanStartActionBlock()
        {
            return
                Owner &&
                VfxToPlay;
        }

        #endregion

        #region Particle Functions

        /// <summary>
        /// Prepares the particle systems for playback by setting loop, rate,
        /// lifetime multiplier, and destruction behavior.
        /// </summary>
        private void PrepareParticles()
        {
            if (!vfxObject)
            {
                return;
            }

            ParticleSystem parentParticle = vfxObject.GetComponentInChildren<ParticleSystem>();

            if (parentParticle)
            {
                var main = parentParticle.main;

                if (parentParticle.transform.parent == null || parentParticle.transform.parent.GetComponent<ParticleSystem>() == null)
                {
                    main.stopAction = ParticleSystemStopAction.Destroy;
                }

                foreach (var ps in vfxObject.GetComponentsInChildren<ParticleSystem>())
                {
                    main = ps.main;
                    main.loop = Loop;
                    main.simulationSpeed *= VfxRate * rate;        
                }
            }
        }

        /// <summary>
        /// Applies the transform properties to the instantiated VFX,
        /// including position, rotation, scale, and optional socket attachment.
        /// </summary>
        /// <param name="parentObject">The parent GameObject to attach or position the VFX relative to.</param>
        private void ApplyVfxTransform(GameObject parentObject)
        {
            if (parentObject == null || vfxObject == null)
            {
                return;
            }

            foundSocket = null;

            if (!string.IsNullOrEmpty(SpawnSocket))
            {
                foundSocket = parentObject.GetComponentsInChildren<Transform>()
                                        .FirstOrDefault(t => t.name.Contains(SpawnSocket, System.StringComparison.OrdinalIgnoreCase));

                if (foundSocket == null)
                {
                    foundSocket = parentObject.transform;
                }
            }
            else
            {
                foundSocket = parentObject.transform;
            }

            if (ShouldAttachToSocket)
            {
                vfxObject.transform.SetParent(foundSocket, false);
                vfxObject.transform.localPosition = VfxLocalPosition;
                vfxObject.transform.localEulerAngles = VfxLocalRotation;
            }
            else
            {
                vfxObject.transform.position = foundSocket.TransformPoint(VfxLocalPosition);
                vfxObject.transform.rotation = parentObject.transform.rotation * Quaternion.Euler(VfxLocalRotation);
            }

            vfxObject.transform.localScale = VfxLocalScale;
        }

        private void UpdateVfxTransform(GameObject parentObject, bool isDebug)
        {
            if (parentObject == null || vfxObject == null || foundSocket == null)
            {
                return;
            }

            if (!ShouldAttachToSocket && (ShouldFollowSocketYTransform || isDebug))
            {
                // Compute the world position from the parent's transform and the configured local offset,
                // but override Y so the VFX follows the socket's Y while keeping the configured Y offset.
                float yPosition = foundSocket.position.y + VfxLocalPosition.y;
                vfxObject.transform.position = isDebug ?
                                                        foundSocket.TransformPoint(VfxLocalPosition) :
                                                        new Vector3(vfxObject.transform.position.x, yPosition, vfxObject.transform.position.z);
            }

            if (!isDebug)
            {
                return;
            }

            if (ShouldAttachToSocket)
            {
                vfxObject.transform.localPosition = VfxLocalPosition;
                vfxObject.transform.localEulerAngles = VfxLocalRotation;
                vfxObject.transform.localScale = VfxLocalScale;
            }
            else
            {
                vfxObject.transform.rotation = parentObject.transform.rotation * Quaternion.Euler(VfxLocalRotation);
                vfxObject.transform.localScale = VfxLocalScale;
            }
        }

        #endregion

        #region Debug
        
        #if UNITY_EDITOR

        /// <summary>
        /// Called when starting the action block in debug mode.
        /// Instantiates the VFX in the preview scene and prepares it for simulation.
        /// </summary>
        /// <param name="previewRenderUtility">The preview render utility used for debug visualization.</param>
        /// <param name="previewObject">The preview GameObject used in debugging.</param>
        /// <param name="debugAudioSource">Unused in VFX debugging (retained for consistency).</param>
        /// <param name="startTime">The debug start time.</param>
        /// <param name="endTime">The debug end time.</param>
        /// <param name="rate">The debug playback rate.</param>
        public override void OnDebugStart(
                                        List<GameObject> previewAttachedItems,
                                        PreviewRenderUtility previewRenderUtility,
                                        GameObject previewObject,
                                        AudioSource debugAudioSource,
                                        float startTime,
                                        float endTime,
                                        float rate)
        {
            base.OnDebugStart(previewAttachedItems, previewRenderUtility, previewObject, debugAudioSource, startTime, endTime, rate);

            if (particleSystem != null)
            {
                Object.DestroyImmediate(particleSystem);
                particleSystem = null;
            }

            if (vfxObject != null)
            {
                Object.DestroyImmediate(vfxObject);
                vfxObject = null;
            }

            if (VfxToPlay == null)
            {
                particleSystem = null;
                return;
            }

            vfxObject = previewRenderUtility.InstantiatePrefabInScene(VfxToPlay);
            ApplyVfxTransform(previewObject);
            PrepareParticles();

            if (vfxObject != null)
            {
                particleSystem = vfxObject.GetComponentInChildren<ParticleSystem>();
            }

            if (particleSystem != null)
            {
                particleSystem.Simulate(VfxStartTime, true, true, false);
            }

            debugTimer = 0.0f;
            hasVfxEnded = false;
        }

        /// <summary>
        /// Called during debug updates to simulate VFX over time.
        /// </summary>
        /// <param name="deltaTime">The time step to simulate.</param>
        public override void OnDebugUpdate(float deltaTime)
        {
            if (vfxObject == null || particleSystem == null)
            {
                return;
            }

            if(!hasVfxEnded)
            {
                UpdateVfxTransform(previewObject, true);

                debugTimer += deltaTime;

                if (debugTimer > duration)
                {
                    hasVfxEnded = true;

                    if(Loop)
                    {
                        vfxObject.transform.SetParent(null, true);
                    }

                    foreach (var ps in vfxObject.GetComponentsInChildren<ParticleSystem>())
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
            
            particleSystem.Simulate(deltaTime * VfxRate, true, false, false);
        }

        /// <summary>
        /// Called when exiting debug mode, cleaning up any instantiated VFX and particle systems.
        /// </summary>
        public override void OnDebugExit()
        {
            if (particleSystem != null)
            {
                Object.DestroyImmediate(particleSystem);
            }

            if (vfxObject != null)
            {
                Object.DestroyImmediate(vfxObject);
            }

            vfxObject = null;
            particleSystem = null;
        }

        #endif

        #endregion
    }
}
