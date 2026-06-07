using UnityEngine;

#if ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem;

#endif

namespace Jorjouto.AnimComposerSystem.Sample
{
    [RequireComponent(typeof(Camera))]
    public class FreeFlyCamera : MonoBehaviour
    {
        [Header("Movement")]
        public float MoveSpeed = 5f;        // Normal movement speed
        public float FastMoveSpeed = 15f;   // Speed when holding Shift

        [Header("Look")]
        public float LookSensitivity = 0.2f;
        public bool InvertY = false;

        private float rotationX;
        private float rotationY;

        #if ENABLE_INPUT_SYSTEM

        [Header("Input")]
        public InputActionReference CameraOrientationInputAxis = null;
        public InputActionReference SprintButton = null;
        public InputActionReference CameraMoveForwardButton = null;
        public InputActionReference CameraMoveBackButton = null;
        public InputActionReference CameraMoveLeftButton = null;
        public InputActionReference CameraMoveRightButton = null;
        public InputActionReference CameraMoveUpButton = null;
        public InputActionReference CameraMoveDownButton = null;

        #endif  

        [Header("Legacy Input")]
        public string SprintKey = "left shift";
        public string MoveForwardKey = "w";
        public string MoveBackKey = "s";
        public string MoveLeftKey = "a";
        public string MoveRightKey = "d";
        public string MoveUpKey = "e";
        public string MoveDownKey = "q";

        void Awake()
        {
            // Lock and hide the cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Initialize rotation from current transform
            Vector3 angles = transform.eulerAngles;
            rotationX = angles.y;
            rotationY = angles.x;

            #if ENABLE_INPUT_SYSTEM

            CameraOrientationInputAxis?.action?.Enable();
            SprintButton?.action?.Enable();
            CameraMoveForwardButton?.action?.Enable();
            CameraMoveBackButton?.action?.Enable();
            CameraMoveLeftButton?.action?.Enable();
            CameraMoveRightButton?.action?.Enable();
            CameraMoveUpButton?.action?.Enable();
            CameraMoveDownButton?.action?.Enable();

            #endif  
        }

        void Update()
        {
            HandleMouseLook();
            HandleMovement();
        }

        void HandleMouseLook()
        {
            #if ENABLE_INPUT_SYSTEM
            float mouseX = CameraOrientationInputAxis ?
                            CameraOrientationInputAxis.action.ReadValue<Vector2>().x * LookSensitivity :
                            0.0f;

            float mouseY = CameraOrientationInputAxis ?
                            CameraOrientationInputAxis.action.ReadValue<Vector2>().y * LookSensitivity :
                            0.0f;
            #else
            float mouseX = Input.GetAxis("Mouse X") * LookSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * LookSensitivity;
            #endif

            rotationX += mouseX;
            rotationY += InvertY ? mouseY : -mouseY;
            rotationY = Mathf.Clamp(rotationY, -89f, 89f);

            transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
        }

        #if ENABLE_INPUT_SYSTEM

        private void HandleMovement()
        {
            float speed = MoveSpeed;

            if (SprintButton && SprintButton.action.IsPressed())
            {
                speed = FastMoveSpeed;
            }

            Vector3 move = Vector3.zero;

            if (CameraMoveForwardButton && CameraMoveBackButton &&
                CameraMoveLeftButton && CameraMoveRightButton)
            {
                float fwdValue = CameraMoveForwardButton.action.ReadValue<float>();
                float bckwdValue = CameraMoveBackButton.action.ReadValue<float>();
                float leftValue = CameraMoveLeftButton.action.ReadValue<float>();
                float rightValue = CameraMoveRightButton.action.ReadValue<float>();

                move = new(
                rightValue - leftValue,  // A/D
                0f,
                fwdValue - bckwdValue     // W/S
                );
            }
            

            if (CameraMoveUpButton && CameraMoveDownButton)
            {
                float upValue = CameraMoveUpButton.action.ReadValue<float>();
                float downValue = CameraMoveDownButton.action.ReadValue<float>();

                move.y = upValue - downValue;
            }

            // Normalize diagonal movement
            move = speed * Time.deltaTime * transform.TransformDirection(move.normalized);

            transform.position += move;
        }

        #else

        private void HandleMovement()
        {
            float speed = MoveSpeed;
            
            if (Input.GetKey(SprintKey))
            {
                speed = FastMoveSpeed;
            }

            float fwdValue = Input.GetKey(MoveForwardKey) ? 1.0f : 0.0f;
            float bckwdValue = Input.GetKey(MoveBackKey) ? 1.0f : 0.0f;
            float leftValue = Input.GetKey(MoveLeftKey) ? 1.0f : 0.0f;
            float rightValue = Input.GetKey(MoveRightKey) ? 1.0f : 0.0f;

            Vector3 move = new(rightValue - leftValue,  // A/D
                       0f,
                       fwdValue - bckwdValue     // W/S
                       );
            
            float upValue = Input.GetKey(MoveUpKey) ? 1.0f : 0.0f;
            float downValue = Input.GetKey(MoveDownKey) ? 1.0f : 0.0f;

            move.y = upValue - downValue;

            // Normalize diagonal movement
            move = speed * Time.deltaTime * transform.TransformDirection(move.normalized);

            transform.position += move;
        }

        #endif
    }
}
