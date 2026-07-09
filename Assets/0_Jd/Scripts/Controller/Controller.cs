using UnityEngine;

/// <summary>
/// I present to thee, the Arcade Controller script! it supports multiple camera modes with ascociated movement rules. The camera system is designed
/// to be easily extensible, allowing for the addition of new camera modes and the customization of existing ones. 
/// The movement system includes walking, sprinting, and air control, with configurable speeds and acceleration values. 
/// Grounding is handled using a sphere check, with adjustable parameters for ground detection. 
/// using the new input system, Arcade Controller supports both keyboard/mouse and gamepad input.

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public partial class Controller : MonoBehaviour
    {
        private const int PlayerLayerIndex = 3;                                                                     //the player layer in 3 for this project
        private const int PlayerLayerMask = 1 << PlayerLayerIndex;                                              //layermask for the player layer, for showing/hiding the player using the culling mask of the output camera
        private const int AllLayersExceptPlayer = ~PlayerLayerMask;                                             //layermask for all layers except the player layer, for operations where we ignore the player.
        private const float MeaningfulMovementInput = 0.1f;                                                     //the min movement input magnitude to be considered meaningful. 
        private const float MeaningfulMovementInputSquared = MeaningfulMovementInput * MeaningfulMovementInput; //the squared meaningful movement, for avoiding recalculations when comparing against squared input magnitude.

        // Platformer remains available manually but is unsuitable for random maze starts.
        private static readonly CameraMode[] RandomStartCameraModes =
        {
            CameraMode.FirstPerson,
            CameraMode.ThirdPerson,
            CameraMode.TopDown,
            CameraMode.Isometric
        };

        public enum CameraMode  //the 'cameraMode' state machine that controls the current camera mode, which in turn controls movement rulesets and player visibility. New camera modes can be added here, and then implemented in ApplyCameraMode() and ReadCameraInput() to define the behavior of the new mode.
        {
            FirstPerson,
            ThirdPerson,
            TopDown,
            Isometric,
            Platformer,
            FixedSideOn
        }

        [Header("Mode")]
        [SerializeField] private CameraMode cameraMode = CameraMode.FirstPerson; //current camera mode 
        [SerializeField] private float fixedSideOnPlaneZ = 0f; // board-plane lock for fixed side-on minigames

        [Tooltip("Choose a random non-Platformer camera mode before the controller initializes.")]
        [SerializeField] private bool randomizeCameraModeOnStart = false;

        [Header("Movement")]                                        
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float acceleration = 35f;
        [SerializeField] private float deceleration = 45f;
        [SerializeField] private float airAcceleration = 12f;
        [SerializeField] private float characterTurnSpeed = 720f;

        [Header("Gravity")]                                             
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float fallGravityMultiplier = 1.8f;

        [Header("Grounding")]
        [SerializeField] private LayerMask groundLayers = AllLayersExceptPlayer;
        [SerializeField] private float groundCheckDistance = 0.08f;
        [SerializeField, Range(0.5f, 1f)] private float groundCheckRadiusScale = 0.9f;
        [SerializeField] private float groundedVerticalSpeed = -2f;

        private CharacterController characterController;
        private InputSystem_Actions inputActions;
        private Vector3 horizontalMovementVelocity;
        private Quaternion cameraMovementFallbackHeading;
        private float verticalSpeed;
        private bool isGrounded;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            inputActions = new InputSystem_Actions();
            cameraMovementFallbackHeading = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            ApplyRandomStartCameraMode();
            InitializeCamera();

            isGrounded = CheckGrounded();
        }

        private void ApplyRandomStartCameraMode()
        {
            if (!randomizeCameraModeOnStart)
                return;

            cameraMode = RandomStartCameraModes[Random.Range(0, RandomStartCameraModes.Length)];
        }

        private void OnEnable()
        {
            inputActions.Player.Enable();
            EnableCamera();
        }

        private void OnDisable()
        {
            inputActions.Player.Disable();
            DisableCamera();
        }

        private void OnDestroy()
        {
            inputActions.Dispose();
        }

        private void Update()
        {
            UpdateCameraMode();
            UpdateMovement();
            UpdateHeadBob();
        }
    }
}
