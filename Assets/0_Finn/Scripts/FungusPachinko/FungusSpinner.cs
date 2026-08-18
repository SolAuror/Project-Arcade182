using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// Rotates a platform continuously around its local Z axis.
    /// Designed for the XY plane used by Fungus Pachinko.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Rotating Platform")]
    [RequireComponent(typeof(Rigidbody))]
    public class FungusRotatingPlatform : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 90f;

        [Tooltip("Reverse the direction of rotation.")]
        [SerializeField] private bool reverseDirection = false;

        [Header("Physics")]
        [SerializeField] private bool kinematic = true;

        private Rigidbody body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            body.isKinematic = kinematic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            float direction = reverseDirection ? -1f : 1f;

            float rotationAmount =
                rotationSpeed *
                direction *
                Time.fixedDeltaTime;

            Quaternion rotation = Quaternion.Euler(
                0f,
                0f,
                rotationAmount
            );

            body.MoveRotation(body.rotation * rotation);
        }
    }
}
