using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Friendly cousin of the black hole: a gentler gravity well that, instead
    /// of swallowing a captured ball, teleports it to its linked twin portal
    /// and re-aims the exit along the twin's up axis at unchanged speed — a
    /// learnable bank shot rather than a random shuffle. A short per-ball
    /// cooldown keeps the exit portal from instantly recapturing the ball.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Wormhole")]
    public class AtomSmasherWormhole : MonoBehaviour
    {
        [Header("Link")]
        [Tooltip("Twin portal a captured ball exits from.")]
        [SerializeField] private AtomSmasherWormhole linkedPortal;

        [Header("Gravity")]
        [Tooltip("Pull acceleration on a ball 1 unit out; falls off with the square of distance.")]
        [SerializeField, Min(0f)] private float gravityStrength = 10f;

        [Tooltip("Balls beyond this distance feel no pull.")]
        [SerializeField, Min(0.25f)] private float pullRadius = 1.9f;

        [Tooltip("Cap on the pull so close passes curve instead of snapping in.")]
        [SerializeField, Min(0f)] private float maxAcceleration = 30f;

        [Header("Transit")]
        [Tooltip("Balls inside this radius are teleported to the twin portal.")]
        [SerializeField, Min(0.05f)] private float captureRadius = 0.33f;

        [Tooltip("Seconds a teleported ball is ignored by both portals, so the exit can't recapture it.")]
        [SerializeField, Min(0f)] private float reentryCooldownSeconds = 0.35f;

        [Tooltip("Exit speed as a multiple of entry speed; 1 preserves it.")]
        [SerializeField, Min(0.1f)] private float exitSpeedMultiplier = 1f;

        [Header("Look")]
        [Tooltip("Tint applied to child renderers so wormholes read differently from black holes.")]
        [SerializeField] private Color portalTint = new Color(0.3f, 0.95f, 1f, 1f);

        [Tooltip("Optional child spun for the swirl look.")]
        [SerializeField] private Transform swirlDisc;

        [SerializeField] private float discDegreesPerSecond = 220f;

        private readonly Dictionary<AtomSmasherBall, float> recentTransits = new Dictionary<AtomSmasherBall, float>();
        private readonly HashSet<AtomSmasherBall> pulledThisStep = new HashSet<AtomSmasherBall>();

        private void Awake()
        {
            ApplyPortalTint();
        }

        private void FixedUpdate()
        {
            PullAndCaptureBalls();
        }

        private void Update()
        {
            if (swirlDisc != null)
            {
                swirlDisc.Rotate(0f, 0f, discDegreesPerSecond * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>Marks a ball as freshly teleported so this portal ignores it briefly.</summary>
        public void MarkTransit(AtomSmasherBall ball)
        {
            if (ball != null)
            {
                recentTransits[ball] = Time.time;
            }
        }

        private bool IsOnCooldown(AtomSmasherBall ball)
        {
            return recentTransits.TryGetValue(ball, out float transitTime) &&
                   Time.time - transitTime < reentryCooldownSeconds;
        }

        private void PullAndCaptureBalls()
        {
            pulledThisStep.Clear();
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, pullRadius, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider nearbyCollider in nearbyColliders)
            {
                AtomSmasherBall ball = nearbyCollider.GetComponentInParent<AtomSmasherBall>();
                if (ball == null || ball.Rigidbody == null || !pulledThisStep.Add(ball) || IsOnCooldown(ball))
                {
                    continue;
                }

                Vector3 toCore = transform.position - ball.Rigidbody.position;
                toCore.z = 0f;
                float distance = toCore.magnitude;

                if (distance <= captureRadius && linkedPortal != null)
                {
                    Teleport(ball);
                    continue;
                }

                float acceleration = gravityStrength / Mathf.Max(distance * distance, 0.25f);
                if (maxAcceleration > 0f)
                {
                    acceleration = Mathf.Min(acceleration, maxAcceleration);
                }

                ball.Rigidbody.AddForce(toCore.normalized * acceleration, ForceMode.Acceleration);
            }
        }

        // Exit is re-aimed along the twin's up axis so each wormhole pair is a
        // plannable trajectory the player can learn, not a dice roll.
        private void Teleport(AtomSmasherBall ball)
        {
            float speed = ball.PlanarVelocity.magnitude * exitSpeedMultiplier;

            Vector3 exitDirection = linkedPortal.transform.up;
            exitDirection.z = 0f;
            if (exitDirection.sqrMagnitude < 0.0001f)
            {
                exitDirection = Vector3.up;
            }

            exitDirection.Normalize();

            Vector3 exitPosition = linkedPortal.transform.position;
            ball.Rigidbody.position = new Vector3(exitPosition.x, exitPosition.y, ball.Rigidbody.position.z);
            ball.Rigidbody.linearVelocity = exitDirection * Mathf.Max(speed, 0.01f);

            MarkTransit(ball);
            linkedPortal.MarkTransit(ball);
        }

        // Reuses the black hole's meshes/materials; the tint is what says
        // "transit, not doom" at a glance.
        private void ApplyPortalTint()
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer portalRenderer in GetComponentsInChildren<Renderer>())
            {
                portalRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", portalTint);
                block.SetColor("_Color", portalTint);
                portalRenderer.SetPropertyBlock(block);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.95f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, pullRadius);
            Gizmos.DrawWireSphere(transform.position, captureRadius);
            if (linkedPortal != null)
            {
                Gizmos.DrawLine(transform.position, linkedPortal.transform.position);
            }
        }
    }
}
