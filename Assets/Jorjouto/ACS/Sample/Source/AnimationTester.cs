using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem;

#endif


namespace Jorjouto.AnimComposerSystem.Sample
{
    [Serializable]
    struct AnimData
    {
        #if ENABLE_INPUT_SYSTEM
        public InputActionReference inputAction;
        #endif

        public string legacyInput;

        public ScriptableObject_AnimComposer animation;

        public readonly bool IsValid()
        {
            #if ENABLE_INPUT_SYSTEM
            return inputAction != null && animation != null;
            #else
            return animation != null;
            #endif
        }
    }

    [RequireComponent(typeof(AnimCoordinatorComponent))]
    public class AnimationTester : MonoBehaviour
    {
        [SerializeField]
        private List<AnimData> ButtonAnims;

        #if ENABLE_INPUT_SYSTEM
        [SerializeField]
        private InputActionReference CancelAnimationsInputActions;
        #else
        [SerializeField]
        private string CancelAnimationsLegacyInput = "escape";
        #endif

        private AnimCoordinatorComponent animCoordinator;

        void Awake()
        {
            animCoordinator = GetComponent<AnimCoordinatorComponent>();

            #if ENABLE_INPUT_SYSTEM

            if (ButtonAnims == null) return;

            foreach (AnimData animData in ButtonAnims)
            {
                if (animData.IsValid())
                {
                    animData.inputAction.action.Enable();
                    animData.inputAction.action.started += OnAnimButtonPressed;
                }
            }

            if (CancelAnimationsInputActions != null)
            {
                CancelAnimationsInputActions.action.Enable();
                CancelAnimationsInputActions.action.started += OnAnimsEndButtonPressed;
            }

            #endif
        }

        #if ENABLE_INPUT_SYSTEM

        private void OnAnimButtonPressed(InputAction.CallbackContext callback)
        {
            foreach (AnimData animData in ButtonAnims)
            {
                if (animData.IsValid() && animData.inputAction.action == callback.action)
                {
                    animCoordinator.PlayAnimComposer(animData.animation);
                    break;
                }
            }
        }

        private void OnAnimsEndButtonPressed(InputAction.CallbackContext callback)
        {
            animCoordinator.InterruptAllAnimComposers(0.2f);
        }

        private void OnDestroy()
        {
            if (ButtonAnims != null)
            {
                foreach (var animData in ButtonAnims)
                {
                    if (animData.inputAction != null && animData.inputAction.action != null)
                    {
                        animData.inputAction.action.started -= OnAnimButtonPressed;
                        animData.inputAction.action.Disable();
                    }
                }
            }

            if (CancelAnimationsInputActions != null && CancelAnimationsInputActions.action != null)
            {
                CancelAnimationsInputActions.action.started -= OnAnimsEndButtonPressed;
                CancelAnimationsInputActions.action.Disable();
            }
        }

        #else

        private void Update()
        {
            if (ButtonAnims == null) return;

            foreach (var buttonAnimPair in ButtonAnims)
            {
                if (buttonAnimPair.IsValid() && 
                    Input.GetKeyDown(buttonAnimPair.legacyInput))
                {
                    animCoordinator.PlayAnimComposer(buttonAnimPair.animation);
                    return;
                }
            }

            if(Input.GetKeyDown(CancelAnimationsLegacyInput))
            {
                animCoordinator.InterruptAllAnimComposers(0.2f);
            }
        }

        #endif
    }
}
