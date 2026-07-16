using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AtomSmasherTarget))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Moving Target")]
    public class AtomSmasherMovingTarget : MonoBehaviour
    {
        [SerializeField] private float physicsPlaneZ;

        [Header("Drift")]
        [Tooltip("Half-extent of the horizontal sine drift.")]
        [SerializeField, Min(0f)] private float driftExtentX = 1.25f;

        [Tooltip("Half-extent of the vertical sine drift; the board band is short, keep this modest.")]
        [SerializeField, Min(0f)] private float driftExtentY = 0.55f;

        [Tooltip("Full back-and-forth cycles per second on X; low values read as lazy drift.")]
        [SerializeField, Min(0.005f)] private float driftCyclesX = 0.09f;

        [Tooltip("Cycles per second on Y. Deliberately not a clean ratio of X so loops precess.")]
        [SerializeField, Min(0.005f)] private float driftCyclesY = 0.057f;

        [Tooltip("Chance to drift on both axes at once (a lazy open loop).")]
        [SerializeField, Range(0f, 1f)] private float bidirectionalChance = 0.35f;

        [Tooltip("Chance a single-axis drifter moves vertically instead of horizontally.")]
        [SerializeField, Range(0f, 1f)] private float verticalChance = 0.25f;

        [Header("Avoidance")]
        [Tooltip("Solid neighbors inside this range bend the drift path aside. Balls are ignored — they must still hit us.")]
        [SerializeField, Min(0f)] private float avoidRadius = 1f;

        [Tooltip("Seconds to ease into a dodge when a neighbor crowds the path.")]
        [SerializeField, Min(0.02f)] private float avoidEaseSeconds = 0.35f;

        [Tooltip("Seconds to relax back onto the drift path once clear.")]
        [SerializeField, Min(0.02f)] private float avoidRelaxSeconds = 1.1f;

        [Tooltip("The dodge never pushes farther than this off the authored path.")]
        [SerializeField, Min(0f)] private float maxAvoidOffset = 1.1f;

        private static readonly Collider[] avoidHits = new Collider[12];

        private Rigidbody rb;
        private Vector3 originLocalPosition;
        private bool hasOrigin;
        private float driftTime;
        private float phaseX;
        private float phaseY;
        private bool driftHorizontal = true;
        private bool driftVertical;
        private Vector2 avoidOffset;
        private Rect clampBounds;
        private bool hasClampBounds;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            CaptureOriginIfNeeded();
            RollDriftMode();
            LockToPlane();
        }

        private void FixedUpdate()
        {
            CaptureOriginIfNeeded();

            driftTime += Time.fixedDeltaTime;

            Vector2 pathOffset = Vector2.zero;
            if (driftHorizontal)
            {
                pathOffset.x = Mathf.Sin(phaseX + driftTime * driftCyclesX * 2f * Mathf.PI) * driftExtentX;
            }

            if (driftVertical)
            {
                pathOffset.y = Mathf.Sin(phaseY + driftTime * driftCyclesY * 2f * Mathf.PI) * driftExtentY;
            }

            UpdateAvoidance(pathOffset);
            MoveToLocalOffset(pathOffset + avoidOffset);
        }

        public void Initialize(float planeZ, Rect bounds)
        {
            physicsPlaneZ = planeZ;
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            originLocalPosition = transform.localPosition;
            hasOrigin = true;
            driftTime = 0f;
            avoidOffset = Vector2.zero;

            // Drifters may inherit a replaced atom's spot outside the spawn
            // band, so the cage grows to include the origin — the path then
            // clamps at the band edge instead of sailing into a wall.
            clampBounds = bounds;
            Vector2 origin = new Vector2(originLocalPosition.x, originLocalPosition.y);
            clampBounds.xMin = Mathf.Min(clampBounds.xMin, origin.x);
            clampBounds.xMax = Mathf.Max(clampBounds.xMax, origin.x);
            clampBounds.yMin = Mathf.Min(clampBounds.yMin, origin.y);
            clampBounds.yMax = Mathf.Max(clampBounds.yMax, origin.y);
            hasClampBounds = true;

            RollDriftMode();
            LockToPlane();
        }

        private void RollDriftMode()
        {
            phaseX = Random.Range(0f, 2f * Mathf.PI);
            phaseY = Random.Range(0f, 2f * Mathf.PI);

            if (Random.value < bidirectionalChance)
            {
                driftHorizontal = true;
                driftVertical = true;
            }
            else if (Random.value < verticalChance)
            {
                driftHorizontal = false;
                driftVertical = true;
            }
            else
            {
                driftHorizontal = true;
                driftVertical = false;
            }
        }

        // The dodge is a smoothed offset, not physics: neighbors inside the
        // avoid radius push it away from the path, and it eases back once
        // clear. Balls are deliberately ignored so the atom stays hittable.
        private void UpdateAvoidance(Vector2 pathOffset)
        {
            Vector2 probeLocal = new Vector2(originLocalPosition.x, originLocalPosition.y) + pathOffset + avoidOffset;
            Vector3 probeWorld = LocalOffsetToWorld(probeLocal);

            Vector2 desiredDodge = Vector2.zero;
            int hitCount = Physics.OverlapSphereNonAlloc(probeWorld, avoidRadius, avoidHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = avoidHits[i];
                if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<AtomSmasherBall>() != null)
                {
                    continue;
                }

                Vector3 closest = hit.ClosestPoint(probeWorld);
                Vector3 away = probeWorld - closest;
                away.z = 0f;
                float distance = away.magnitude;
                if (distance >= avoidRadius)
                {
                    continue;
                }

                Vector2 direction = distance > 0.001f
                    ? new Vector2(away.x, away.y) / distance
                    : Random.insideUnitCircle.normalized;
                desiredDodge += direction * (1f - distance / avoidRadius);
            }

            desiredDodge = Vector2.ClampMagnitude(desiredDodge, 1f) * maxAvoidOffset;

            float ease = desiredDodge.sqrMagnitude > avoidOffset.sqrMagnitude ? avoidEaseSeconds : avoidRelaxSeconds;
            float blend = 1f - Mathf.Exp(-Time.fixedDeltaTime / ease);
            avoidOffset = Vector2.Lerp(avoidOffset, desiredDodge, blend);
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
            Vector2 target = new Vector2(originLocalPosition.x, originLocalPosition.y) + offset;
            if (hasClampBounds)
            {
                target.x = Mathf.Clamp(target.x, clampBounds.xMin, clampBounds.xMax);
                target.y = Mathf.Clamp(target.y, clampBounds.yMin, clampBounds.yMax);
            }

            Vector3 localPosition = new Vector3(target.x, target.y, physicsPlaneZ);

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

        private Vector3 LocalOffsetToWorld(Vector2 local)
        {
            Vector3 localPosition = new Vector3(local.x, local.y, physicsPlaneZ);
            return transform.parent != null ? transform.parent.TransformPoint(localPosition) : localPosition;
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
