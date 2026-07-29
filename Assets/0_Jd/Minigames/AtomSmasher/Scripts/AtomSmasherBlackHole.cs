using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Gravity-well hazard: bends ball trajectories toward its core inside the
    /// pull radius (a well-aimed shot can slingshot around it), swallows any
    /// ball that crosses the event horizon, and vacuums up every electron
    /// spark on the board. Spawns like any other wave obstruction.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Black Hole")]
    public class AtomSmasherBlackHole : MonoBehaviour
    {
        [Header("Gravity")]
        [Tooltip("Pull acceleration on a ball 1 unit from the core; falls off with the square of distance.")]
        [SerializeField, Min(0f)] private float gravityStrength = 26f;

        [Tooltip("Balls beyond this distance feel no pull.")]
        [SerializeField, Min(0.5f)] private float pullRadius = 3.25f;

        [Tooltip("Cap on the pull so close passes curve instead of snapping in.")]
        [SerializeField, Min(0f)] private float maxAcceleration = 45f;

        [Header("Event Horizon")]
        [Tooltip("Balls inside this radius are swallowed and the shot ends.")]
        [SerializeField, Min(0.05f)] private float eventHorizonRadius = 0.42f;

        [Header("Electron Sparks")]
        [Tooltip("Sparks are pulled from anywhere on the board at this multiple of the gravity strength.")]
        [SerializeField, Min(0f)] private float electronPullMultiplier = 2.5f;

        [Tooltip("Sparks inside this radius are consumed.")]
        [SerializeField, Min(0.02f)] private float electronConsumeRadius = 0.3f;

        [Header("Feedback")]
        [Tooltip("Optional VFX spawned where a ball is swallowed.")]
        [SerializeField] private GameObject swallowVfxPrefab;

        [SerializeField, Min(0.1f)] private float swallowVfxLifeSeconds = 2f;

        [Tooltip("Optional child spun around the board plane for an accretion-disc look.")]
        [SerializeField] private Transform accretionDisc;

        [SerializeField] private float discDegreesPerSecond = -140f;

        private readonly HashSet<AtomSmasherBall> pulledThisStep = new HashSet<AtomSmasherBall>();

        private void FixedUpdate()
        {
            PullBalls();
        }

        private void Update()
        {
            if (accretionDisc != null)
            {
                accretionDisc.Rotate(0f, 0f, discDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            PullElectrons(Time.deltaTime);
        }

        private void PullBalls()
        {
            pulledThisStep.Clear();
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, pullRadius, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider nearbyCollider in nearbyColliders)
            {
                AtomSmasherBall ball = nearbyCollider.GetComponentInParent<AtomSmasherBall>();
                if (ball == null || ball.Rigidbody == null || !pulledThisStep.Add(ball))
                {
                    continue;
                }

                Vector3 toCore = transform.position - ball.Rigidbody.position;
                toCore.z = 0f;
                float distance = toCore.magnitude;

                if (distance <= eventHorizonRadius)
                {
                    Swallow(ball);
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

        private void PullElectrons(float deltaTime)
        {
            IReadOnlyList<AtomSmasherElectron> electrons = AtomSmasherElectron.Active;
            for (int i = electrons.Count - 1; i >= 0; i--)
            {
                AtomSmasherElectron electron = electrons[i];
                if (electron == null)
                {
                    continue;
                }

                Vector3 toCore = transform.position - electron.transform.position;
                toCore.z = 0f;
                float distance = toCore.magnitude;

                if (distance <= electronConsumeRadius)
                {
                    electron.Consume();
                    continue;
                }

                // Gentle 1/r falloff so even far sparks visibly spiral in.
                float acceleration = gravityStrength * electronPullMultiplier / Mathf.Max(distance, 0.5f);
                electron.Attract(transform.position, acceleration, deltaTime);
            }
        }

        private void Swallow(AtomSmasherBall ball)
        {
            if (swallowVfxPrefab != null)
            {
                GameObject vfx = Instantiate(swallowVfxPrefab, ball.transform.position, Quaternion.identity);
                Destroy(vfx, swallowVfxLifeSeconds);
            }

            ball.ForceFinish();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, pullRadius);
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(transform.position, eventHorizonRadius);
        }
    }
}
