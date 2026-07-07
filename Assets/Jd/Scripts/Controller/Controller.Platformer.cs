using Unity.Cinemachine; // cinemachine types
using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        [Header("Platformer")]
        [SerializeField] private CinemachineCamera platformerCamera; // platformer camera
        [SerializeField, Min(0f)] private float platformerMovementSpeedMultiplier = 1f; // movement multiplier
        [SerializeField, Min(0f)] private float platformerTurnSpeedMultiplier = 1f; // turn multiplier

        private static Vector3 GetPlatformerMovementDirection(Vector2 movementInput, Vector3 movementRight)
        {
            return movementRight * movementInput.x; // lateral movement only
        }

        private static bool HasValidPlatformerSprintDirection(Vector2 movementInput)
        {
            return Mathf.Abs(movementInput.x) > MeaningfulMovementInput; // significant horizontal input
        }

        private static bool PlatformerUsesLookInput() => false; // no look input
        private static bool PlatformerRendersPlayer() => true; // shows player
        private static bool PlatformerFacesMovement() => true; // faces movement
    }
}
