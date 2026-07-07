using Unity.Cinemachine; // cinemachine types
using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        [Header("Third Person")]
        [SerializeField] private CinemachineCamera thirdPersonCamera; // 3P camera
        [SerializeField, Min(0f)] private float thirdPersonMovementSpeedMultiplier = 1f; // movement multiplier
        [SerializeField, Min(0f)] private float thirdPersonTurnSpeedMultiplier = 1f; // turn multiplier

        private static Vector3 GetThirdPersonMovementDirection(
            Vector2 movementInput,
            Vector3 movementRight,
            Vector3 movementForward)
        {
            return movementRight * movementInput.x + movementForward * movementInput.y; // compute movement vector
        }

        private static bool HasValidThirdPersonSprintDirection(Vector2 movementInput)
        {
            return movementInput.y > MeaningfulMovementInput; // forward input above threshold
        }

        private static bool ThirdPersonUsesLookInput() => true; // uses look input
        private static bool ThirdPersonRendersPlayer() => true; // shows player model
        private static bool ThirdPersonFacesMovement() => true; // faces movement direction
    }
}
