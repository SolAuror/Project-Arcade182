using Unity.Cinemachine; // cinemachine types
using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        [Header("Isometric")]
        [SerializeField] private CinemachineCamera isometricCamera; // isometric camera
        [SerializeField, Min(0f)] private float isometricMovementSpeedMultiplier = 1f; // movement multiplier
        [SerializeField, Min(0f)] private float isometricTurnSpeedMultiplier = 1f; // turn multiplier

        private static Vector3 GetIsometricMovementDirection(
            Vector2 movementInput,
            Vector3 movementRight,
            Vector3 movementForward)
        {
            return movementRight * movementInput.x + movementForward * movementInput.y; // compute movement vector
        }

        private static bool HasValidIsometricSprintDirection(Vector2 movementInput)
        {
            return movementInput.y > MeaningfulMovementInput; // forward input threshold
        }

        private static bool IsometricUsesLookInput() => true; // uses look input
        private static bool IsometricRendersPlayer() => true; // shows player model
        private static bool IsometricFacesMovement() => true; // faces movement
    }
}
