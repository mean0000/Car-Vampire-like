using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Jorjouto.AnimComposerSystem.Sample
{
    /// <summary>
    /// Represents an action block that instantiates an item.
    /// Supports instantiation, attachment to sockets, looping and scaling.
    /// Includes debugging functionality for previewing an instanced GameObject in the editor.
    /// </summary>
    [BlockColor("#002b30ff")]
    [System.Serializable]
    public class ActionBlock_InstantiatePrefab : ActionBlock_Base
    {
        #region Fields

        /// <summary>
        /// The prefab to instantiate and play.
        /// </summary>
        [Tooltip("The prefab to instantiate and play.")]
        public GameObject PrefabToInstantiate = null;

        /// <summary>
        /// The name of the socket (child transform) where the Prefab should spawn.
        /// If null or not found, defaults to the parent transform.
        /// </summary>
        [Tooltip("The name of the socket (child transform) where the instance should spawn. " +
                "If null or not found, defaults to the parent transform.")]
        public string SpawnSocket = null;

        /// <summary>
        /// Whether the instance should attach to the socket transform.
        /// </summary>
        [Tooltip("Whether the instance should attach to the socket transform.")]
        public bool ShouldAttachToSocket = false;

        /// <summary>
        /// The local position offset of the instance relative to the socket.
        /// </summary>
        [Tooltip("The local position offset of the instance relative to the socket.")]
        public Vector3 instanceLocalPosition = Vector3.zero;

        /// <summary>
        /// The local rotation offset of the instance relative to the socket.
        /// </summary>
        [Tooltip("The local rotation offset of the instance relative to the socket.")]
        public Vector3 instanceLocalRotation = Vector3.zero;

        /// <summary>
        /// The local scale of the instance.
        /// </summary>
        [Tooltip("The local scale of the instance.")]
        public Vector3 instanceLocalScale = Vector3.one;

        #endregion

        #region Properties

        /// <summary>
        /// The instantiated GameObject.
        /// </summary>
        private GameObject instance = null;

        #endregion

        #region Standard Functions

        /// <summary>
        /// Called when the action block starts execution.
        /// Instantiates the GameObject and applies transforms.
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
                instance = Object.Instantiate(PrefabToInstantiate);
                ApplyInstanceTransform(Owner);
            }
        }

        /// <summary>
        /// Called when the action block exits execution.
        /// Stops looping particle systems if applicable.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (instance == null)
            {
                return;
            }
            
            instance.transform.SetParent(null, false);
            Object.Destroy(instance);
            instance = null;
        }

        /// <summary>
        /// Checks whether the action block can start execution.
        /// </summary>
        /// <returns>True if the owner and prefab are valid, false otherwise.</returns>
        public override bool CheckCanStartActionBlock()
        {
            return
                Owner &&
                PrefabToInstantiate;
        }

        #endregion

        #region Transform Functions

        /// <summary>
        /// Applies the transform properties to the instantiated GameObject,
        /// including position, rotation, scale, and optional socket attachment.
        /// </summary>
        /// <param name="parentObject">The parent GameObject to attach or position the instance relative to.</param>
        private void ApplyInstanceTransform(GameObject parentObject)
        {
            if (parentObject == null || instance == null)
            {
                return;
            }

            Transform foundSocket = null;

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
                instance.transform.SetParent(foundSocket, true);
                instance.transform.localPosition = instanceLocalPosition;
                instance.transform.localEulerAngles = instanceLocalRotation;
            }
            else
            {
                instance.transform.SetPositionAndRotation(foundSocket.TransformPoint(instanceLocalPosition),
                                                        foundSocket.rotation * Quaternion.Euler(instanceLocalRotation));
            }

            instance.transform.localScale = instanceLocalScale;
        }
        
        #endregion

        #region Debug

        #if UNITY_EDITOR

        /// <summary>
        /// Called when starting the action block in debug mode.
        /// Instantiates the GameObject in the preview scene and prepares it for simulation.
        /// </summary>
        /// <param name="previewRenderUtility">The preview render utility used for debug visualization.</param>
        /// <param name="previewObject">The preview GameObject used in debugging.</param>
        /// <param name="debugAudioSource">Unused in instance debugging (retained for consistency).</param>
        /// <param name="startTime">The debug start time.</param>
        /// <param name="endTime">The debug end time.</param>
        /// <param name="rate">The debug playback rate.</param>
        public override void OnDebugStart(PreviewRenderUtility previewRenderUtility,
                                        GameObject previewObject,
                                        AudioSource debugAudioSource,
                                        float startTime,
                                        float endTime,
                                        float rate)
        {
            base.OnDebugStart(previewRenderUtility, previewObject, debugAudioSource, startTime, endTime, rate);

            if (instance != null)
            {
                Object.DestroyImmediate(instance);
                instance = null;
            }

            if (PrefabToInstantiate == null)
            {
                return;
            }

            instance = previewRenderUtility?.InstantiatePrefabInScene(PrefabToInstantiate);
            ApplyInstanceTransform(previewObject);
        }

        /// <summary>
        /// Called during debug updates to re-evaluate the instance transform.
        /// </summary>
        /// <param name="deltaTime">The time step to simulate.</param>
        public override void OnDebugUpdate(float deltaTime)
        {
            if (instance == null || !ShouldAttachToSocket)
            {
                return;
            }

            ApplyInstanceTransform(previewObject);
        }

        /// <summary>
        /// Called when exiting debug mode, cleaning up any instantiated prefab.
        /// </summary>
        public override void OnDebugExit()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }

            instance = null;
        }

        #endif

        #endregion
    }
}
