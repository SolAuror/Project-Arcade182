using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Moving Bar")]
    public class AtomSmasherMovingBar : MonoBehaviour
    {
        [SerializeField] private float physicsPlaneZ;
        [SerializeField] private Vector2 localStartOffset = new Vector2(-1.5f, 0f);
        [SerializeField] private Vector2 localEndOffset = new Vector2(1.5f, 0f);
        [SerializeField] private float speed = 1.5f;

        private Rigidbody rb;
        private Vector3 originLocalPosition;
        private float travel;
        private int travelDirection = 1;
        private bool hasOrigin;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            CaptureOriginIfNeeded();
            LockToPlane();
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
        }

        private void FixedUpdate()
        {
            CaptureOriginIfNeeded();

            float pathLength = Vector2.Distance(localStartOffset, localEndOffset);
            if (pathLength <= 0.001f || speed <= 0f)
            {
                MoveToLocalOffset(localStartOffset);
                return;
            }

            travel += travelDirection * speed * Time.fixedDeltaTime / pathLength;
            if (travel >= 1f)
            {
                travel = 1f;
                travelDirection = -1;
            }
            else if (travel <= 0f)
            {
                travel = 0f;
                travelDirection = 1;
            }

            MoveToLocalOffset(Vector2.Lerp(localStartOffset, localEndOffset, travel));
        }

        public void Initialize(float planeZ)
        {
            physicsPlaneZ = planeZ;
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            originLocalPosition = transform.localPosition;
            hasOrigin = true;
            LockToPlane();
        }

        private void ConfigureRigidbody()
        {
            if (rb == null)
            {
                return;
            }

            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        }

        private void CaptureOriginIfNeeded()
        {
            if (hasOrigin)
            {
                return;
            }

            originLocalPosition = transform.localPosition;
            hasOrigin = true;
        }

        private void MoveToLocalOffset(Vector2 offset)
        {
            Vector3 localPosition = originLocalPosition + new Vector3(offset.x, offset.y, 0f);
            localPosition.z = physicsPlaneZ;

            if (rb == null)
            {
                transform.localPosition = localPosition;
                return;
            }

            Vector3 worldPosition = transform.parent != null
                ? transform.parent.TransformPoint(localPosition)
                : localPosition;

            rb.MovePosition(worldPosition);
        }

        private void LockToPlane()
        {
            Vector3 position = transform.position;
            position.z = physicsPlaneZ;

            if (rb != null)
            {
                rb.position = position;
            }
            else
            {
                transform.position = position;
            }
        }
    }
}
