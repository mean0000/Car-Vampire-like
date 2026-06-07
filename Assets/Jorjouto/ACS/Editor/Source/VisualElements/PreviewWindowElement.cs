// Author: Jorge Dinares
// Copyright © 2025 Jorge Dinares. All rights reserved.
/// <summary>
/// A VisualElement that represents the preview window displaying the character animation.
/// </summary>
/// <remarks>
/// This element handles all user interactions with the preview window, including
/// camera rotation and panning through mouse input, Window options buttons (resizing window, changing background color, lit / unlit)
/// </remarks>
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Jorjouto.AnimComposerSystem.ACSEditor
{
    [UxmlElement]
    public partial class PreviewWindowElement : VisualElement
    {
        #region Template GUID

        private const string guid = "2dff5750baec7b44e9065818214283cb";
        private readonly string templatePath = AssetDatabase.GUIDToAssetPath(guid);

        #endregion

        #region Visual Elements

        private Image previewWindowImage = null;
        private Button cameraOptionsButton = null;
        private VisualElement cameraOrientationSettings = null;
        private Button rotateLeftButton = null;
        private Button rotateRightButton = null;
        private Button rotateUpButton = null;
        private Button rotateDownButton = null;
        private Button resetCameraButton = null;
        private Button backgroundColorOptionsButton = null;
        private Button toggleUnlitButton = null;
        private VisualElement backgroundColorSettings = null;
        private ColorField previewBackgroundColor = null;
        private Button screeenHeightOptionsButton = null;
        private SliderInt previewHeightSlider = null;

        #endregion

        /// <summary>
        /// Utility for rendering the animation preview in a separate space.
        /// </summary>
        public PreviewRenderUtility PreviewRenderUtility {get; private set; } = null;

        private ScriptableObject_AnimComposer animComposer = null;

        #region Caches

        // Cache mapping original material -> generated unlit preview material to avoid creating/destroying
        private readonly Dictionary<Material, Material> unlitMaterialCache = new();
        // Reverse cache mapping unlit preview material -> original lit material for quick restoration
        private readonly Dictionary<Material, Material> unlitToOriginalMaterial = new();

        // Cached references to avoid repeated GetComponents calls on the preview model (performance)
        private Dictionary<string, Transform> cachedPreviewTransformByName = null;
        private readonly List<Renderer> cachedPreviewRenderers = new();

        // Cache shader lookup to avoid repeated Shader.Find calls
        private Shader unlitTextureShader = null;

        /// <summary>
        /// The render texture containing the rendered animation preview.
        /// Reused to avoid per-frame allocations.
        /// </summary>
        private RenderTexture previewRenderTexture = null;

        /// <summary>
        /// A list of GameObjects attached to the preview.
        /// </summary>
        private readonly List<GameObject> previewAttachedItems = new();

        /// <summary>
        /// Whether the camera is currently in pure FPS rotation mode (from right mouse button dragging).
        /// </summary>
        private bool bIsFPSRotationMode = false;

        /// <summary>
        /// Set of currently pressed keys for continuous movement.
        /// </summary>
        private readonly HashSet<KeyCode> pressedKeys = new();

        private Vector3 lastRenderedCameraPosition = Vector3.zero;
        private Quaternion lastRenderedCameraRotation = Quaternion.identity;
        private float lastRenderedAnimationTime = -1.0f;

        #endregion

        #region Camera

        /// <summary>
        /// The distance of the camera from its target.
        /// </summary>
        private float cameraDistance = 5f;

        /// <summary>
        /// The world position the camera is looking at.
        /// </summary>
        private Vector3 cameraTarget = Vector3.zero;

        /// <summary>
        /// Camera yaw angle (horizontal rotation).
        /// </summary>
        private float cameraYaw = 0f;

        /// <summary>
        /// Camera pitch angle (vertical rotation).
        /// </summary>
        private float cameraPitch = 0f;

        /// <summary>
        /// Additional yaw rotation offset applied on top of the base camera orientation (for discrete buttons).
        /// </summary>
        private float cameraOrientationYawOffset = 0f;

        /// <summary>
        /// Additional pitch rotation offset applied on top of the base camera orientation (for discrete buttons).
        /// </summary>
        private float cameraOrientationPitchOffset = 0f;

        /// <summary>
        /// Sensitivity for horizontal mouse rotation.
        /// </summary>
        [SerializeField]
        [Min(0.0f)]
        [UxmlAttribute("mouse-yaw-sensitivity")]
        private float mouseYawSensitivity = 0.2f;

        /// <summary>
        /// Sensitivity for vertical mouse rotation.
        /// </summary>
        [SerializeField]
        [Min(0.0f)]
        [UxmlAttribute("mouse-pitch-sensitivity")]
        private float mousePitchSensitivity = 0.2f;

        /// <summary>
        /// Sensitivity for panning with mouse.
        /// </summary>
        [SerializeField]
        [Min(0.0f)]
        [UxmlAttribute("mouse-pan-sensitivity")]
        private float mousePanSensitivity = 0.01f;

        /// <summary>
        /// Movement speed for FPS camera.
        /// </summary>
        [SerializeField]
        [Min(0.0f)]
        [UxmlAttribute("camera-movement-speed")]
        private float fpsCameraMovementSpeed = 2f;

        /// <summary>
        /// Movement speed for FPS camera.
        /// </summary>
        [SerializeField]
        [Min(0.0f)]
        [UxmlAttribute("camera-zoom-speed")]
        private float cameraZoomSpeed = 0.1f;

        /// <summary>
        /// Whether the right mouse button is currently held down.
        /// </summary>
        private bool bIsRightMouseButtonHeld = false;

        /// <summary>
        /// Whether the middle mouse button is currently held down.
        /// </summary>
        private bool bIsMiddleMouseButtonHeld = false;

        /// <summary>
        /// Whether the camera has changed in any way and must be recalculated
        /// </summary>
        private bool bHasCameraChanged = false;

        #endregion

        public PreviewWindowElement()
        {
            VisualTreeAsset templateAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
            templateAsset.CloneTree(this);
            StoreAllVisualElements();
        }

        public void Initialize(ScriptableObject_AnimComposer animComposer)
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            this.animComposer = animComposer;
            CreateAllBindings();
            InitializePreviewWindowHeight();
        }

        private void InitializePreviewWindowHeight()
        {
            int cachedWindowHeight = AnimComposerEditorWindow.LoadCachedData().PreviewWindowHeight;

            if(cachedWindowHeight != 0)
            {
                previewHeightSlider.SetValueWithoutNotify(cachedWindowHeight);
                previewWindowImage.style.height = cachedWindowHeight;
            }
        }

        public void UpdateManualCamera()
        {
            if(bHasCameraChanged)
            {
                bHasCameraChanged = false;
                UpdateCameraTransform();
            }
            else if(pressedKeys.Count > 0 && bIsRightMouseButtonHeld)
            {
                HandleFPSMovement();
            }
        }

        private void OnBeforeAssemblyReload()
        {
            CleanupPreviewWindow();
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private void StoreAllVisualElements()
        {
            rotateLeftButton = this.Q<Button>("Rotate90LeftButton");
            rotateRightButton = this.Q<Button>("Rotate90RightButton");
            rotateUpButton = this.Q<Button>("Rotate90UpButton");
            rotateDownButton = this.Q<Button>("Rotate90DownButton");
            resetCameraButton = this.Q<Button>("ResetCameraButton");
            cameraOptionsButton = this.Q<Button>("CameraOptionsButton");
            cameraOrientationSettings = this.Q<VisualElement>("CameraOrientationSettings");
            backgroundColorOptionsButton = this.Q<Button>("BackgroundColorOptionsButton");
            toggleUnlitButton = this.Q<Button>("LitToUnlitButton");
            backgroundColorSettings = this.Q<VisualElement>("BackgroundColorSettings");
            screeenHeightOptionsButton = this.Q<Button>("ScreenHeightOptionsButton");
            previewWindowImage = this.Q<Image>("PreviewWindow");
            previewWindowImage.tooltip = "Right Mouse Button + Drag: Rotate Camera \nRight Mouse Button + WASD: Free Camera Movement \nMiddle Mouse Button + Drag: Pan Camera \nScroll Wheel: Zoom In/Out";
            previewBackgroundColor = this.Q<ColorField>("ScreenBackgroundSelector");
            previewHeightSlider = this.Q<SliderInt>("PreviewHeightSelector");
        }

        public void UpdatePreviewRenderUtility(ref GameObject previewObject, bool bResetCamera)
        {
            //Avoids any duplicates
            if(PreviewRenderUtility != null)
            {
                return;
            }

            PreviewRenderUtility = new PreviewRenderUtility()
            {
                camera = { nearClipPlane = 0.01f, farClipPlane = 100f, fieldOfView = 30f }
            };

            var cam = PreviewRenderUtility.camera;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = animComposer.PreviewBackgroundColor;
            cam.allowHDR = true;                 // Needed for good specular highlights
            cam.allowMSAA = true;                // Enables anti-aliasing
            cam.renderingPath = RenderingPath.Forward;
            cam.useOcclusionCulling = false;

            // Reduce ambient so lighting has contrast
            PreviewRenderUtility.ambientColor = new Color(0.18f, 0.18f, 0.18f, 1f);

            // Disable all lights first
            foreach (var light in PreviewRenderUtility.lights)
            {
                light.enabled = false;
            }

            // -------- KEY LIGHT (main directional) --------
            Light keyLight = PreviewRenderUtility.lights[0];
            keyLight.enabled = true;
            keyLight.type = LightType.Directional;
            keyLight.intensity = 0.6f;
            keyLight.color = Color.white;

            keyLight.transform.rotation = Quaternion.Euler(35f, 135f, 0f);

            // Enable shadows
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 1f;
            keyLight.shadowBias = 0.02f;
            keyLight.shadowNormalBias = 0.35f;

            // -------- FILL LIGHT (soft contrast) --------
            Light fillLight = PreviewRenderUtility.lights[1];
            fillLight.enabled = true;
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.35f;
            fillLight.color = new Color(0.8f, 0.85f, 1f);

            fillLight.transform.rotation = Quaternion.Euler(340f, 218f, 177f);
            fillLight.shadows = LightShadows.None;

            InitializePreviewObject(ref previewObject, animComposer.PreviewModel);
            CachePreviewObjectSocketTransforms(previewObject);

            LoadPreviewItems(previewObject);

            if (animComposer.IsPreviewUnlit)
            {
                SwitchObjectMaterialsToUnlit();
            }

            ResetCamera(bResetCamera);

            if(previewRenderTexture == null)
            {
                ReinitializePreviewTexture();
            }
        }

        #region Bindings

        private void CreateAllBindings()
        {
            CreateIMGUIBindings();
            CreateRotateCameraButtonsBindings();
            CreateToggleUnlitButtonBindings();
            CreateColorFieldBindings();
            CreatePreviewHeightFieldBindings();
            CreateManualCameraControlBindings();
        }

        /// <summary>
        /// Sets up the binding for the IMGUI container to handle camera controls and draw the preview texture.
        /// </summary>
        private void CreateIMGUIBindings()
        {
            previewWindowImage.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                ReinitializePreviewTexture();
            });
        }

        /// <summary>
        /// Sets up bindings for the camera rotation buttons.
        /// </summary>
        private void CreateRotateCameraButtonsBindings()
        {
            cameraOptionsButton.clicked += () =>
            {
                cameraOrientationSettings.style.display =
                                                            cameraOrientationSettings.resolvedStyle.display == DisplayStyle.None ?
                                                            DisplayStyle.Flex : DisplayStyle.None;
            };

            rotateLeftButton.clicked += () => RotateCamera90(true, true);
            rotateRightButton.clicked += () => RotateCamera90(false, true);
            rotateUpButton.clicked += () => RotateCamera90(true, false);
            rotateDownButton.clicked += () => RotateCamera90(false, false);
            resetCameraButton.clicked += () => ResetCamera(true);
        }

        /// <summary>
        /// Sets up bindings for the lit / unlit toggle button.
        /// </summary>
        private void CreateToggleUnlitButtonBindings()
        {
            toggleUnlitButton.clicked += () =>
            {
                animComposer.IsPreviewUnlit = !animComposer.IsPreviewUnlit;

                if (animComposer.IsPreviewUnlit)
                {
                    SwitchObjectMaterialsToUnlit();
                }
                else if(!animComposer.IsPreviewUnlit)
                {
                    SwitchObjectMaterialsToLit();
                }
            };
        }

        /// <summary>
        /// Sets up bindings for the background color field.
        /// </summary>
        private void CreateColorFieldBindings()
        {
            backgroundColorOptionsButton.clicked += () =>
            {
                backgroundColorSettings.style.display =
                                                        backgroundColorSettings.resolvedStyle.display == DisplayStyle.None ?
                                                        DisplayStyle.Flex : DisplayStyle.None;
                              
            };

            previewBackgroundColor.RegisterValueChangedCallback(evt =>
            {
                if(PreviewRenderUtility != null)
                {
                    PreviewRenderUtility.camera.backgroundColor = evt.newValue;
                    ReinitializePreviewTexture();
                }
            });
        }

        /// <summary>
        /// Sets up bindings for the preview height slider.
        /// </summary>
        private void CreatePreviewHeightFieldBindings()
        {
            screeenHeightOptionsButton.clicked += () =>
            {
                previewHeightSlider.style.display =
                                                        previewHeightSlider.resolvedStyle.display == DisplayStyle.None ?
                                                        DisplayStyle.Flex : DisplayStyle.None;
            };

            previewHeightSlider.RegisterValueChangedCallback(evt =>
            {
                previewWindowImage.style.height = evt.newValue;
                AnimComposerEditorWindow.SavePreviewWindowHeight(evt.newValue);
            });
        }

        #endregion

        /// <summary>
        /// Applies the transform data from a <see cref="PreviewItemData"/> to a GameObject in the scene.
        /// </summary>
        /// <param name="attachedObj">The GameObject to transform.</param>
        /// <param name="previewItem">The data containing the transform information.</param>
        private void ApplyPreviewItemTransform(GameObject attachedObj, PreviewItemData previewItem)
        {
            Transform parent = null;

            if (!string.IsNullOrEmpty(previewItem.AttachSocket) &&
                cachedPreviewTransformByName != null)
            {
                cachedPreviewTransformByName.TryGetValue(
                    previewItem.AttachSocket,
                    out parent);
            }

            var t = attachedObj.transform;

            t.SetParent(parent, false);
            t.localPosition = previewItem.OffsetPosition;
            t.localEulerAngles = previewItem.OffsetRotation;
            t.localScale = previewItem.Scale;
        }

        private bool CheckPreviewItemAtIndexIsValid(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= previewAttachedItems.Count)
            {
                return false;
            }

            return previewAttachedItems[itemIndex] != null;
        }

        /// <summary>
        /// Resets the camera's position and orientation to their default values.
        /// </summary>
        private void ResetCamera(bool bResetTransform)
        {
            if (bResetTransform)
            {
                // place camera in front of the target (look back at target)
                cameraTarget = new Vector3(0f, 1f, 0f);
                cameraDistance = 5f;

                // For FPS mouse-look we start yaw so the camera is in front
                cameraYaw = 180f;
                cameraPitch = 0f;

                // For the discrete/orbital view keep a 180° yaw offset so (0,0,-cameraDistance)
                // places the camera in front of the target and LookAt keeps aiming at it.
                cameraOrientationYawOffset = 180f;
                cameraOrientationPitchOffset = 0f;

                pressedKeys.Clear();
                bIsRightMouseButtonHeld = false;
                bIsFPSRotationMode = false;
            }

            UpdateCameraTransform();
        }

        /// <summary>
        /// Applies the current camera orientation and distance to the preview camera's transform.
        /// </summary>
        private void UpdateCameraTransform()
        {
            if (PreviewRenderUtility == null)
            {
                return;
            }

            if (bIsFPSRotationMode)
            {
                // Pure FPS mode: camera rotates independently based on mouse input
                Quaternion fpsCameraRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
                Vector3 cameraOffset = fpsCameraRotation * new Vector3(0f, 0f, -cameraDistance);
                Vector3 finalCameraPosition = cameraTarget + cameraOffset;

                PreviewRenderUtility.camera.transform.SetPositionAndRotation(finalCameraPosition, fpsCameraRotation);
            }
            else
            {
                // Orbital mode: camera orbits around target with discrete rotation offsets
                Quaternion baseOrientation = Quaternion.Euler(cameraOrientationPitchOffset, cameraOrientationYawOffset, 0f);
                Vector3 cameraOffset = baseOrientation * new Vector3(0f, 0f, -cameraDistance);
                Vector3 finalCameraPosition = cameraTarget + cameraOffset;

                PreviewRenderUtility.camera.transform.position = finalCameraPosition;
                PreviewRenderUtility.camera.transform.LookAt(cameraTarget, Vector3.up);
            }
        }

        #region Camera Methods

        /// <summary>
        /// Checks if a key code is a movement key.
        /// </summary>
        private static bool IsMovementKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.W || keyCode == KeyCode.A || keyCode == KeyCode.S || keyCode == KeyCode.D ||
                keyCode == KeyCode.Space || keyCode == KeyCode.LeftControl;
        }

        private void RotateCamera90(bool isPositive, bool isHorizontal)
        {
            // Reset FPS rotation mode when using discrete buttons
            if (bIsFPSRotationMode)
            {
                ResetCamera(true);
                bIsFPSRotationMode = false;
            }

            float angle = isPositive ? 90f : -90f;
            if (isHorizontal)
            {
                cameraOrientationYawOffset += angle;
            }
            else
            {
                cameraOrientationPitchOffset += angle;
                cameraOrientationPitchOffset = Mathf.Clamp(cameraOrientationPitchOffset, -90f, 90f);
            }
            UpdateCameraTransform();
        }

        private void CreateManualCameraControlBindings()
        {
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<WheelEvent>(OnWheel);
        }

        private void OnKeyDown(KeyDownEvent keyDownEvent)
        {
            if(IsMovementKey(keyDownEvent.keyCode))
            {
                pressedKeys.Add(keyDownEvent.keyCode);
            }
        }

        private void OnKeyUp(KeyUpEvent keyUpEvent)
        {
            if(IsMovementKey(keyUpEvent.keyCode))
            {
                pressedKeys.Remove(keyUpEvent.keyCode);
            }
        }

        private void OnPointerDown(PointerDownEvent pointerDownEvent)
        {
            if (pointerDownEvent.button == (int)MouseButton.RightMouse)
            {
                bIsRightMouseButtonHeld = true;
                bIsFPSRotationMode = true;
            }
            else if (pointerDownEvent.button == (int)MouseButton.MiddleMouse)
            {
                bIsMiddleMouseButtonHeld = true;
            }
        }

        private void OnPointerUp(PointerUpEvent pointerUpEvent)
        {
            if (pointerUpEvent.button == 1)
            {
                bIsRightMouseButtonHeld = false;
            }
            else if (pointerUpEvent.button == (int)MouseButton.MiddleMouse)
            {
                bIsMiddleMouseButtonHeld = false;
            }
        }

        private void OnPointerLeave(PointerLeaveEvent pointerLeaveEvent)
        {
            bIsRightMouseButtonHeld = false;
            bIsMiddleMouseButtonHeld = false;
            pressedKeys.Clear();
        }
        
        private void OnPointerMove(PointerMoveEvent pointerMoveEvent)
        {
            if (bIsRightMouseButtonHeld)
            {
                
                // FPS-style mouse look (independent of orbital target)
                cameraYaw += pointerMoveEvent.deltaPosition.x * mouseYawSensitivity;
                cameraPitch += pointerMoveEvent.deltaPosition.y * mousePitchSensitivity;
                cameraPitch = Mathf.Clamp(cameraPitch, -90f, 90f);
                bHasCameraChanged = true;
            }
            else if (bIsMiddleMouseButtonHeld)
            {
                // Pan with middle mouse
                Vector3 camForward = (cameraTarget - PreviewRenderUtility.camera.transform.position).normalized;
                Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;
                Vector3 camUp = Vector3.Cross(camForward, camRight).normalized;
                cameraTarget += (-camRight * pointerMoveEvent.deltaPosition.x + camUp * pointerMoveEvent.deltaPosition.y) * mousePanSensitivity;
                bHasCameraChanged = true;
            }
        }

        private void OnWheel(WheelEvent wheelEvent)
        {
            cameraDistance += wheelEvent.delta.y * cameraZoomSpeed;
            bHasCameraChanged = true;
            wheelEvent.StopPropagation();
        }

        /// <summary>
        /// Handles FPS-style continuous movement based on pressed keys.
        /// </summary>
        private void HandleFPSMovement()
        {
            if (!bIsRightMouseButtonHeld || pressedKeys.Count == 0)
                return;

            Quaternion cameraRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 moveDirection = Vector3.zero;

            if (pressedKeys.Contains(KeyCode.W))
                moveDirection += cameraRotation * Vector3.forward;

            if (pressedKeys.Contains(KeyCode.A))
                moveDirection += cameraRotation * Vector3.left;

            if (pressedKeys.Contains(KeyCode.S))
                moveDirection += cameraRotation * Vector3.back;

            if (pressedKeys.Contains(KeyCode.D))
                moveDirection += cameraRotation * Vector3.right;

            if (pressedKeys.Contains(KeyCode.Space))
                moveDirection += Vector3.up;

            if (pressedKeys.Contains(KeyCode.LeftControl))
                moveDirection += Vector3.down;

            if (moveDirection != Vector3.zero)
            {
                cameraTarget += moveDirection.normalized * fpsCameraMovementSpeed * 0.016f;
                UpdateCameraTransform();
            }
        }

        #endregion

        #region Preview Methods

        /// <summary>
        /// Loads all preview items from the <see cref="ScriptableObject_AnimComposer"/> into the scene.
        /// </summary>
        public void LoadPreviewItems(GameObject previewObject)
        {
            var previewItems = animComposer.PreviewItems;

            if (previewAttachedItems.Count > 0)
            {
                for (int i = previewAttachedItems.Count - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(previewAttachedItems[i]);
                    previewAttachedItems[i] = null;
                }

                previewAttachedItems.Clear();
            }

            if (previewObject == null)
            {
                return;
            }

            cachedPreviewRenderers.Clear();
            cachedPreviewRenderers.AddRange(previewObject.GetComponentsInChildren<Renderer>(true));

            foreach (PreviewItemData previewItem in previewItems)
            {
                if (previewItem.Item == null)
                    continue;

                GameObject newObject = PreviewRenderUtility.InstantiatePrefabInScene(previewItem.Item);
                // Cache renderers for this attached preview item to avoid repeated GetComponents calls later

                cachedPreviewRenderers.AddRange(newObject.GetComponentsInChildren<Renderer>(true));
                ApplyPreviewItemTransform(newObject, previewItem);
                newObject.SetActive(previewItem.Visible);
                previewAttachedItems.Add(newObject);
            }

            ReinitializePreviewTexture();
        }

        public void OnAttachSocketAssignedToPreviewItem(int itemIndex, string socket)
        {
            var itemSource = animComposer.PreviewItems[itemIndex];

            if (!CheckPreviewItemAtIndexIsValid(itemIndex) || itemSource == null)
            {
                return;
            }

            var previewItemInstance = previewAttachedItems[itemIndex];

            cachedPreviewTransformByName.TryGetValue(socket, out var attachSocket);
            previewItemInstance.transform.SetParent(attachSocket, false);

            previewItemInstance.transform.localPosition = itemSource.OffsetPosition;
            previewItemInstance.transform.localEulerAngles = itemSource.OffsetRotation;
            previewItemInstance.transform.localScale = itemSource.Scale;
            ReinitializePreviewTexture();
        }

        private void InitializePreviewObject(ref GameObject previewObject, GameObject previewModel)
        {
            previewObject = PreviewRenderUtility.InstantiatePrefabInScene(previewModel);

            foreach (var smr in previewObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
                smr.forceMatrixRecalculationPerRender = true;
            }

            previewObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private void CachePreviewObjectSocketTransforms(GameObject previewObject)
        {
            // Cache transforms/renderers once so we don't call GetComponentsInChildren repeatedly in hot paths
            var cachedPreviewTransforms = previewObject.GetComponentsInChildren<Transform>(true);
            cachedPreviewRenderers.AddRange(previewObject.GetComponentsInChildren<Renderer>(true));

            // Build a case-insensitive exact-name lookup so socket lookups can be O(1)
            cachedPreviewTransformByName = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            
            for (int i = 0; i < cachedPreviewTransforms.Length; i++)
            {
                var t = cachedPreviewTransforms[i];
                if (t != null && !cachedPreviewTransformByName.ContainsKey(t.name))
                    cachedPreviewTransformByName[t.name] = t;
            }
        }

        /// <summary>
        /// Creates and assigns new unlit materials that match the original materials' base color/texture.
        /// </summary>
        /// <param name="objectToModify">The GameObject whose materials to update.</param>
        private void SwitchObjectMaterialsToUnlit()
        {
            if (unlitTextureShader == null)
                unlitTextureShader = Shader.Find("Universal Render Pipeline/Unlit");

            if (unlitTextureShader == null)
            {
                return;
            }

            foreach (Renderer renderer in cachedPreviewRenderers)
            {
                if(renderer == null)
                {
                    continue;
                }

                // reuse array allocation where possible
                var shared = renderer.sharedMaterials;
                bool anyChanged = false;
                Material[] outMats = new Material[shared.Length];

                for (int i = 0; i < shared.Length; i++)
                {
                    var mat = shared[i];
                    if (mat == null || mat.shader == null || !mat.shader.name.Contains("Lit", StringComparison.OrdinalIgnoreCase))
                    {
                        outMats[i] = mat;
                        continue;
                    }

                    if (!unlitMaterialCache.TryGetValue(mat, out var unlitPreviewMat) || unlitPreviewMat == null)
                    {
                        unlitPreviewMat = new(unlitTextureShader)
                        {
                            color = mat.color
                        };

                        if (mat.HasProperty("_MainTex"))
                        {
                            unlitPreviewMat.SetTexture("_MainTex", mat.GetTexture("_MainTex"));
                        }
                        unlitPreviewMat.CopyMatchingPropertiesFromMaterial(mat);
                        unlitMaterialCache[mat] = unlitPreviewMat;
                        unlitToOriginalMaterial[unlitPreviewMat] = mat;
                    }

                    outMats[i] = unlitPreviewMat;
                    anyChanged = true;
                }

                if (anyChanged)
                {
                    renderer.sharedMaterials = outMats;
                    ReinitializePreviewTexture();
                }
            }
        }

        /// <summary>
        /// Restores original lit materials that were previously replaced with unlit preview materials.
        /// </summary>
        private void SwitchObjectMaterialsToLit()
        {
            foreach (Renderer renderer in cachedPreviewRenderers)
            {
                if(renderer == null)
                {
                    continue;
                }
                
                var shared = renderer.sharedMaterials;
                bool anyChanged = false;
                Material[] outMats = new Material[shared.Length];

                for (int i = 0; i < shared.Length; i++)
                {
                    var mat = shared[i];

                    if (mat == null)
                    {
                        outMats[i] = null;
                        continue;
                    }

                    // Check if this material is one of our generated unlit preview materials
                    if (unlitToOriginalMaterial.TryGetValue(mat, out var originalLit) && originalLit != null)
                    {
                        outMats[i] = originalLit;
                        anyChanged = true;
                    }
                    else
                    {
                        outMats[i] = mat;
                    }
                }

                if (anyChanged)
                {
                    renderer.sharedMaterials = outMats;
                    ReinitializePreviewTexture();
                }
            }
        }

        public void CleanupPreviewWindow()
        {
            // Destroy any cached generated unlit materials
            foreach (var kv in unlitMaterialCache)
            {
                var prevMat = kv.Value;

                if (prevMat != null)
                    Object.DestroyImmediate(prevMat, true);
            }

            unlitMaterialCache.Clear();
            unlitToOriginalMaterial.Clear();
            cachedPreviewRenderers.Clear();

            previewAttachedItems.Clear();

            // Clear cached component arrays
            cachedPreviewTransformByName = null;
            CleanupPreviewTexture();
            PreviewRenderUtility?.Cleanup();
            PreviewRenderUtility = null;

            lastRenderedAnimationTime = -1f;
        }

        public void UpdatePreviewItemPosition(int itemIndex, Vector3 offsetPosition)
        {
            if (CheckPreviewItemAtIndexIsValid(itemIndex))
            {
                previewAttachedItems[itemIndex].transform.localPosition = offsetPosition;
                ReinitializePreviewTexture();
            }
        }

        public void UpdatePreviewItemRotation(int itemIndex, Vector3 offsetRotation)
        {
            if (CheckPreviewItemAtIndexIsValid(itemIndex))
            {
                previewAttachedItems[itemIndex].transform.localEulerAngles = offsetRotation;
                ReinitializePreviewTexture();
            }
        }

        public void UpdatePreviewItemScale(int itemIndex, Vector3 scale)
        {
            if (CheckPreviewItemAtIndexIsValid(itemIndex))
            {
                previewAttachedItems[itemIndex].transform.localScale = scale;
                ReinitializePreviewTexture();
            }
        }

        public void UpdatePreviewItemVisibility(int itemIndex, bool visibility)
        {
            if (CheckPreviewItemAtIndexIsValid(itemIndex))
            {
                previewAttachedItems[itemIndex].SetActive(visibility);
                ReinitializePreviewTexture();
            }
        }

        /// <summary>
        /// Renders the animation preview to a texture.
        /// </summary>
        public void UpdateAnimationToPreviewTexture(GameObject previewObject, float animationTime)
        {
            if (PreviewRenderUtility == null || previewObject == null)
                return;

            var cam = PreviewRenderUtility.camera;
            cam.transform.GetPositionAndRotation(out Vector3 camPos, out Quaternion camRot);
            bool timeChanged = !Mathf.Approximately(animationTime, lastRenderedAnimationTime);
            bool camChanged = camPos != lastRenderedCameraPosition || camRot != lastRenderedCameraRotation;

            if (!(timeChanged || camChanged) || previewRenderTexture == null)
                return;

            cam.Render();

            lastRenderedAnimationTime = animationTime;
            lastRenderedCameraPosition = camPos;
            lastRenderedCameraRotation = camRot;
        }

        private void ReinitializePreviewTexture()
        {
            CleanupPreviewTexture();

            if (PreviewRenderUtility == null)
                return;


            float width = previewWindowImage.contentRect.width;
            float height = previewWindowImage.contentRect.height;

            if(float.IsNaN(width) || float.IsNaN(height) ||
               width == 0 || height == 0)
            {
                return;
            }

            var cam = PreviewRenderUtility.camera;

            previewRenderTexture = new RenderTexture((int)width, (int)height, 24, RenderTextureFormat.ARGB32)
            {
                useMipMap = false,
                autoGenerateMips = false
            };

            previewWindowImage.image = previewRenderTexture;
            cam.targetTexture = previewRenderTexture;
            cam.aspect = width / (float)height;

            cam.Render();
        }

        private void CleanupPreviewTexture()
        {
            if (previewRenderTexture != null)
            {
                previewWindowImage.image = null;
                previewRenderTexture.Release();
                Object.DestroyImmediate(previewRenderTexture);
                previewRenderTexture = null;
            }
        }

        public void ResetLasRenderedAnimationTime() => lastRenderedAnimationTime = -1f;


        #endregion
    }
}
