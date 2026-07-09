using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Companion for an atom that vibrates erratically and cannot be smashed by
    /// a direct shot: the ball must reach it off a rebound (at least one bounce
    /// since launch). Direct hits get knocked off course instead.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AtomSmasherTarget))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Unstable Target")]
    public class AtomSmasherUnstableTarget : MonoBehaviour
    {
        [Header("Rebound Rule")]
        [Tooltip("Bounces the ball needs since launch before this atom can be smashed.")]
        [SerializeField, Min(1)] private int requiredBounces = 1;

        [Header("Deflection")]
        [Tooltip("Direct hits get their velocity rotated by up to this angle.")]
        [SerializeField, Range(5f, 90f)] private float deflectAngleDegrees = 35f;

        [SerializeField, Min(0.1f)] private float deflectSpeedMultiplier = 1.05f;
        [SerializeField, Min(0f)] private float deflectCooldownSeconds = 0.15f;
        [SerializeField] private Color deflectFlashColor = new Color(1f, 0.25f, 0.75f, 1f);

        [Header("Vibration")]
        [SerializeField, Min(0f)] private float jitterAmplitude = 0.07f;
        [SerializeField, Min(0f)] private float jitterFrequency = 22f;

        private Vector3 baseLocalPosition;
        private float lastDeflectTime = -999f;
        private float noiseSeed;

        private void OnEnable()
        {
            baseLocalPosition = transform.localPosition;
            noiseSeed = Random.value * 100f;
        }

        private void OnDisable()
        {
            transform.localPosition = baseLocalPosition;
        }

        private void Update()
        {
            if (jitterAmplitude <= 0f)
            {
                return;
            }

            float t = Time.time * jitterFrequency;
            Vector3 jitter = new Vector3(
                Mathf.PerlinNoise(noiseSeed, t) - 0.5f,
                Mathf.PerlinNoise(noiseSeed + 37f, t) - 0.5f,
                0f) * (2f * jitterAmplitude);

            transform.localPosition = baseLocalPosition + jitter;
        }

        public bool AllowsHitFrom(AtomSmasherBall ball)
        {
            return ball != null && ball.BounceCount >= requiredBounces;
        }

        /// <summary>Knocks a directly-hitting ball off course instead of dying.</summary>
        public void DeflectBall(AtomSmasherBall ball)
        {
            if (ball == null || ball.Rigidbody == null || Time.time - lastDeflectTime < deflectCooldownSeconds)
            {
                return;
            }

            // Only shove balls actually touching us (chain explosions etc. stay out).
            if ((ball.transform.position - transform.position).sqrMagnitude > 4f)
            {
                return;
            }

            lastDeflectTime = Time.time;

            Vector3 velocity = ball.PlanarVelocity;
            if (velocity.sqrMagnitude <= 0.01f)
            {
                return;
            }

            float angle = Random.Range(0.5f, 1f) * deflectAngleDegrees * (Random.value < 0.5f ? -1f : 1f);
            Vector3 deflected = Quaternion.Euler(0f, 0f, angle) * velocity * deflectSpeedMultiplier;
            ball.Rigidbody.linearVelocity = new Vector3(deflected.x, deflected.y, 0f);
            ball.ApplyTemporaryVisualState(deflectFlashColor, 0.3f);
        }
    }
}
