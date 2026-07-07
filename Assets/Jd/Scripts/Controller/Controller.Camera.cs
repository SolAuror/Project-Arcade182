using Sol.Grab; // grab utilities
using Sol.Outline; // outline utilities
using Unity.Cinemachine; // cinemachine types
using UnityEngine; // Unity core
using UnityEngine.InputSystem; // input system

namespace Player
{
    public partial class Controller
    {
        private const float MinimumInputDeltaTime = 0.0001f; // small floor to avoid division by zero

        [Header("Camera")]
        [SerializeField] private Camera outputCamera;        // camera we modify for player visibility
        [SerializeField] private CinemachineBrain cameraBrain; // optional cinemachine brain for blends
        [SerializeField] private GameObject crosshairObject; // crosshair UI object
        [SerializeField] private float crosshairInteractionRayDistance = 10f; // screen-center targeting distance
        [SerializeField] private float mouseInteractionRayDistance = 30f;     // cursor targeting distance

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 0.12f; // mouse sensitivity multiplier
        [SerializeField] private float gamepadLookSpeed = 180f;  // gamepad look speed (deg/sec)

        private CameraMode appliedCameraMode;                  // cached camera mode
        private int outputCameraCullingMaskWithPlayer;         // mask including player layer
        private bool wasIsometricPointerModifierActive;        // previous isometric modifier state

        private void InitializeCamera()
        {
            if (outputCamera == null)
                outputCamera = Camera.main; // fallback to main camera

            if (outputCamera != null)
                outputCameraCullingMaskWithPlayer = outputCamera.cullingMask | PlayerLayerMask; // prepare mask including player

            CinemachineInputAxisController[] cameraInputControllers =
                GetComponentsInChildren<CinemachineInputAxisController>(true); // gather cinemachine input controllers
            foreach (CinemachineInputAxisController inputController in cameraInputControllers)
                inputController.ReadControlValueOverride = ReadCameraInput; // override input reader

            appliedCameraMode = cameraMode;
            wasIsometricPointerModifierActive = IsIsometricPointerModifierActive();
            ApplyCameraMode();
        }

        private void EnableCamera()
        {
            ApplyInteractionPolicy(true); // apply interaction policy on enable
        }

        private void DisableCamera()
        {
            SetCursorLocked(false); // unlock cursor on disable
        }

        private void UpdateCameraMode()
        {
            bool isIsometricPointerModifierActive = IsIsometricPointerModifierActive();
            if (appliedCameraMode != cameraMode)
            {
                appliedCameraMode = cameraMode;
                wasIsometricPointerModifierActive = isIsometricPointerModifierActive;
                ApplyCameraMode(); // mode changed
            }
            else if (wasIsometricPointerModifierActive != isIsometricPointerModifierActive)
            {
                wasIsometricPointerModifierActive = isIsometricPointerModifierActive;
                ApplyInteractionPolicy(true); // modifier changed
            }

            if (UsesLookInput() && !UsesMouseInteraction() && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked); // toggle cursor lock on Escape
            }

            ApplyInteractionPolicy(false); // refresh interaction policy
        }

        private void ApplyCameraMode() // enable/disable cameras and update policies
        {
            SetCameraActive(firstPersonCamera, cameraMode == CameraMode.FirstPerson);
            SetCameraActive(thirdPersonCamera, cameraMode == CameraMode.ThirdPerson);
            SetCameraActive(topDownCamera, cameraMode == CameraMode.TopDown);
            SetCameraActive(isometricCamera, cameraMode == CameraMode.Isometric);
            SetCameraActive(platformerCamera, cameraMode == CameraMode.Platformer);

            ResetActiveCameraMode();
            UpdatePlayerLayerVisibility();
            ApplyInteractionPolicy(true);
            cameraBrain?.ResetState();
        }

        private void UpdatePlayerLayerVisibility()
        {
            if (outputCamera == null)
                return;

            outputCamera.cullingMask = RendersPlayer()
                ? outputCameraCullingMaskWithPlayer
                : outputCameraCullingMaskWithPlayer & ~PlayerLayerMask; // hide player layer when not rendering player
        }

        private float ReadCameraInput(
            InputAction lookAction,
            IInputAxisOwner.AxisDescriptor.Hints axisHint,
            Object inputContext,
            CinemachineInputAxisController.Reader.ControlValueReader defaultReader)
        {
            if (!UsesLookInput() || UsesMouseInteraction() || Cursor.lockState != CursorLockMode.Locked || IsGrabRotationActive())
            {
                return 0f; // ignore input in these cases
            }

            float lookInput = ReadScaledCameraInput(lookAction, axisHint, inputContext, defaultReader);
            if (cameraMode == CameraMode.FirstPerson && axisHint == IInputAxisOwner.AxisDescriptor.Hints.X)
            {
                ApplyFirstPersonYaw(lookInput * Time.deltaTime); // apply yaw directly in first person
                return 0f; // prevent cinemachine from using X input
            }

            return lookInput; // use scaled input
        }

        private float ReadScaledCameraInput(
            InputAction lookAction,
            IInputAxisOwner.AxisDescriptor.Hints axisHint,
            Object inputContext,
            CinemachineInputAxisController.Reader.ControlValueReader defaultReader)
        {
            float lookInput = defaultReader(lookAction, axisHint, inputContext, null);
            if (lookAction.activeControl?.device is Pointer)
            {
                float inputDeltaTime = Mathf.Max(Time.deltaTime, MinimumInputDeltaTime);
                return lookInput * mouseSensitivity / inputDeltaTime; // scale mouse input by sensitivity
            }

            return lookInput * gamepadLookSpeed; // scale gamepad input by speed
        }

        private Camera GetGameplayCamera()
        {
            return Camera.main != null ? Camera.main : outputCamera; // prefer main camera
        }

        private bool UsesLookInput()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => FirstPersonUsesLookInput(),
                CameraMode.ThirdPerson => ThirdPersonUsesLookInput(),
                CameraMode.TopDown => TopDownUsesLookInput(),
                CameraMode.Isometric => IsometricUsesLookInput(),
                CameraMode.Platformer => PlatformerUsesLookInput(),
                _ => false
            };
        }

        private bool RendersPlayer()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => FirstPersonRendersPlayer(),
                CameraMode.ThirdPerson => ThirdPersonRendersPlayer(),
                CameraMode.TopDown => TopDownRendersPlayer(),
                CameraMode.Isometric => IsometricRendersPlayer(),
                CameraMode.Platformer => PlatformerRendersPlayer(),
                _ => true
            };
        }

        private void ApplyInteractionPolicy(bool shouldApplyCursorState)
        {
            bool usesMouseInteraction = UsesMouseInteraction();
            GrabMode interactionMode = usesMouseInteraction ? GrabMode.Mouse : GrabMode.Crosshair;
            float interactionRayDistance = usesMouseInteraction || cameraMode == CameraMode.Isometric
                ? mouseInteractionRayDistance
                : crosshairInteractionRayDistance;

            if (shouldApplyCursorState)
                SetCursorLocked(ShouldLockCursor());

            if (crosshairObject != null)
                crosshairObject.SetActive(interactionMode == GrabMode.Crosshair);

            GrabManager grabManager = GrabManager.Instance;
            if (grabManager != null)
            {
                grabManager.SetGrabMode(interactionMode);
                grabManager.raycastDistance = interactionRayDistance;
            }

            OutlineManager outlineManager = OutlineManager.Instance;
            if (outlineManager != null)
            {
                outlineManager.SetRayMode(interactionMode);
                outlineManager.raycastDistance = interactionRayDistance;
            }
        }

        private bool UsesMouseInteraction()
        {
            return cameraMode switch
            {
                CameraMode.TopDown => true,
                CameraMode.Platformer => true,
                CameraMode.Isometric => IsIsometricPointerModifierActive(),
                _ => false
            };
        }

        private bool ShouldLockCursor()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => true,
                CameraMode.ThirdPerson => true,
                CameraMode.Isometric => !IsIsometricPointerModifierActive(),
                _ => false
            };
        }

        private static bool IsIsometricPointerModifierActive()
        {
            return Keyboard.current?.tabKey.isPressed == true; // hold tab for pointer interaction
        }

        private void ResetActiveCameraMode()
        {
            if (cameraMode == CameraMode.FirstPerson)
                ResetFirstPersonCamera();
        }

        private static bool IsGrabRotationActive()
        {
            return GrabManager.Instance != null && GrabManager.Instance.HeldObject != null && GrabManager.Instance.rotationMode;
        }

        private static void SetCameraActive(CinemachineCamera cinemachineCamera, bool shouldBeActive)
        {
            if (cinemachineCamera != null)
                cinemachineCamera.gameObject.SetActive(shouldBeActive);
        }

        private static void SetCursorLocked(bool shouldLockCursor)
        {
            Cursor.lockState = shouldLockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shouldLockCursor;
        }
    }
}
