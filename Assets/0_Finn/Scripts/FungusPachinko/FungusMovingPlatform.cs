
using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// Moves a platform continuously between two points.
    /// The platform moves toward one wall, reverses direction,
    /// then moves toward the opposite wall and repeats.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Moving Platform")]
    [RequireComponent(typeof(Rigidbody))]
    public class FungusMovingPlatform : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;

        [Tooltip("The leftmost position the platform can reach.")]
        [SerializeField] private Transform leftPoint;

        [Tooltip("The rightmost position the platform can reach.")]
        [SerializeField] private Transform rightPoint;

        [Header("Physics")]
        [SerializeField] private bool kinematic = true;

        private Rigidbody body;
        private bool movingRight = true;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            body.isKinematic = kinematic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            if (leftPoint == null || rightPoint == null)
            {
                return;
            }

            Vector3 currentPosition = body.position;

            Vector3 targetPosition = movingRight
                ? rightPoint.position
                : leftPoint.position;

            Vector3 direction = (targetPosition - currentPosition).normalized;

            Vector3 newPosition = currentPosition +
                                  direction * moveSpeed * Time.fixedDeltaTime;

            // Check whether we've reached or passed the target.
            float distanceToTarget = Vector3.Distance(
                newPosition,
                targetPosition
            );

            if (distanceToTarget <= moveSpeed * Time.fixedDeltaTime)
            {
                newPosition = targetPosition;
                movingRight = !movingRight;
            }

            body.MovePosition(newPosition);
        }
    }
}