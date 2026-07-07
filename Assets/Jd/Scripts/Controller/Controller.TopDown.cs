using Unity.Cinemachine; // cinemachine types
using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        private static readonly Vector3 TopDownFallbackRight = Vector3.right; // fallback right
        private static readonly Vector3 TopDownFallbackForward = Vector3.forward; // fallback forward

        [Header("Top Down")]
        [SerializeField] private CinemachineCamera topDownCamera; // top-down camera
        [SerializeField, Min(0f)] private float topDownMovementSpeedMultiplier = 1f; // movement multiplier
        [SerializeField, Min(0f)] private float topDownTurnSpeedMultiplier = 1f; // turn multiplier

        private static Vector3 GetTopDownMovementDirection(
            Vector2 movementInput,
            Vector3 movementRight,
            Vector3 movementForward)
        {
            return movementRight * movementInput.x + movementForward * movementInput.y; // compute movement vector
        }

        private static bool TryGetTopDownMovementBasis(
            Camera gameplayCamera,
            out Vector3 movementRight,
            out Vector3 movementForward)
        {
            movementRight = Vector3.ProjectOnPlane(gameplayCamera.transform.right, Vector3.up); // project right onto horizontal
            movementForward = Vector3.ProjectOnPlane(gameplayCamera.transform.up, Vector3.up); // project up onto horizontal

            movementRight = movementRight.sqrMagnitude > MeaningfulMovementInputSquared ? movementRight.normalized : TopDownFallbackRight; // normalize or use fallback

            movementForward = movementForward.sqrMagnitude > MeaningfulMovementInputSquared ? movementForward.normalized : TopDownFallbackForward; // normalize or fallback

            return true; // basis obtained
        }

        private static bool HasValidTopDownSprintDirection(Vector2 movementInput)
        {
            return movementInput.sqrMagnitude > MeaningfulMovementInputSquared; // significant input magnitude
        }

        private static bool TopDownUsesLookInput() => false; // no look input
        private static bool TopDownRendersPlayer() => true; // shows player
        private static bool TopDownFacesMovement() => true; // faces movement
    }
}
