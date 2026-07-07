using Unity.Cinemachine; // cinemachine types
using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        private const float FirstPersonPanAxisCenter = 0f; // pan axis center

        [Header("First Person")]
        [SerializeField] private CinemachineCamera firstPersonCamera; // FP camera
        [SerializeField, Min(0f)] private float firstPersonMovementSpeedMultiplier = 1f; // movement speed multiplier
        [SerializeField, Min(0f)] private float firstPersonTurnSpeedMultiplier = 1f; // turn speed multiplier

        private static Vector3 GetFirstPersonMovementDirection(
            Vector2 movementInput,
            Vector3 movementRight,
            Vector3 movementForward)
        {
            return movementRight * movementInput.x + movementForward * movementInput.y; // compute movement vector
        }

        private static bool HasValidFirstPersonSprintDirection(Vector2 movementInput)
        {
            return movementInput.y > MeaningfulMovementInput; // forward input above threshold
        }

        private void ApplyFirstPersonYaw(float yawDeltaDegrees)
        {
            transform.Rotate(Vector3.up, yawDeltaDegrees, Space.World); // rotate body yaw
            cameraMovementFallbackHeading = Quaternion.Euler(0f, transform.eulerAngles.y, 0f); // update fallback heading
        }

        private void ResetFirstPersonCamera()
        {
            if (firstPersonCamera == null || firstPersonCamera.TryGetComponent(out CinemachinePanTilt firstPersonPanTilt) == false)
            {
                return; // nothing to reset
            }

            firstPersonPanTilt.PanAxis.Value = FirstPersonPanAxisCenter; // reset pan value
            firstPersonPanTilt.PanAxis.Center = FirstPersonPanAxisCenter; // reset pan center
        }

        private static bool FirstPersonUsesLookInput() => true; // FP uses look input
        private static bool FirstPersonRendersPlayer() => false; // FP hides player model
        private static bool FirstPersonFacesMovement() => false; // FP does not face movement direction
    }
}
