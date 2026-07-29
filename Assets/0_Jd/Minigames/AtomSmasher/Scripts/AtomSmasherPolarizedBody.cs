using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Gives an object an electric charge that bends charged balls passing
    /// nearby: opposite charges attract (a matched approach curves into the
    /// hit), like charges repel. Neutral balls fly straight through — the
    /// mechanic only engages after the ball takes a charge from a polarizer
    /// gate. On an AtomSmasherTarget it recolors through the target's own
    /// tint system; on anything else it tints child renderers directly.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Polarized Body")]
    public class AtomSmasherPolarizedBody : MonoBehaviour
    {
        private enum BodyCharge
        {
            Positive,
            Negative
        }

        [SerializeField] private BodyCharge charge = BodyCharge.Positive;

        [Header("Field")]
        [Tooltip("Bend acceleration on a charged ball 1 unit out; eases off with distance.")]
        [SerializeField, Min(0f)] private float fieldStrength = 14f;

        [Tooltip("Balls beyond this distance feel nothing.")]
        [SerializeField, Min(0.25f)] private float fieldRadius = 2.25f;

        [Tooltip("Cap so close passes curve instead of snapping.")]
        [SerializeField, Min(0f)] private float maxAcceleration = 28f;

        [Header("Look")]
        [SerializeField] private Color positiveColor = new Color(1f, 0.32f, 0.25f, 1f);
        [SerializeField] private Color negativeColor = new Color(0.3f, 0.55f, 1f, 1f);

        private readonly HashSet<AtomSmasherBall> pushedThisStep = new HashSet<AtomSmasherBall>();

        private float ChargeSign => charge == BodyCharge.Positive ? 1f : -1f;

        // Start, not Awake: spawn registration resets the target (recoloring
        // it) between Awake and the first frame, and the override must win.
        private void Start()
        {
            ApplyChargeColor();
        }

        private void FixedUpdate()
        {
            pushedThisStep.Clear();
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, fieldRadius, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider nearbyCollider in nearbyColliders)
            {
                AtomSmasherBall ball = nearbyCollider.GetComponentInParent<AtomSmasherBall>();
                if (ball == null || ball.Rigidbody == null || !pushedThisStep.Add(ball))
                {
                    continue;
                }

                float ballSign = ball.ChargeSign;
                if (ballSign == 0f)
                {
                    continue;
                }

                Vector3 toBody = transform.position - ball.Rigidbody.position;
                toBody.z = 0f;
                float distance = toBody.magnitude;
                if (distance < 0.01f)
                {
                    continue;
                }

                float acceleration = fieldStrength / Mathf.Max(distance, 0.5f);
                if (maxAcceleration > 0f)
                {
                    acceleration = Mathf.Min(acceleration, maxAcceleration);
                }

                // Coulomb convention: like charges repel, opposites attract.
                Vector3 force = toBody.normalized * (acceleration * -(ballSign * ChargeSign));
                ball.Rigidbody.AddForce(force, ForceMode.Acceleration);
            }
        }

        private void ApplyChargeColor()
        {
            Color chargeColor = charge == BodyCharge.Positive ? positiveColor : negativeColor;

            AtomSmasherTarget target = GetComponent<AtomSmasherTarget>();
            if (target != null)
            {
                target.SetActiveColorOverride(chargeColor);
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer bodyRenderer in GetComponentsInChildren<Renderer>())
            {
                bodyRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", chargeColor);
                block.SetColor("_Color", chargeColor);
                bodyRenderer.SetPropertyBlock(block);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = charge == BodyCharge.Positive
                ? new Color(1f, 0.3f, 0.25f, 0.5f)
                : new Color(0.3f, 0.55f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, fieldRadius);
        }
    }
}
