using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Reusable hitscan beam. Drives the authored two-layer "beacon" prefab -
    /// a bright core line inside a wide translucent glow shell, with subtle
    /// motes emitted along the beam - and falls back to a single LineRenderer
    /// on this object when no layers are assigned (legacy runtime beam).
    /// <see cref="Flash"/> shows the beam between two points and it hides
    /// itself after a short life; rapid re-Flashes of the same instance (the
    /// player's held Laser) read as one steady laminar beam.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Shared/Hitscan Beam")]
    public class HitscanBeam : MonoBehaviour
    {
        [Tooltip("Bright inner beam. Auto-falls back to a LineRenderer on this object.")]
        [SerializeField] private LineRenderer coreLine;

        [Tooltip("Wide translucent outer shell (the beacon layer). Optional.")]
        [SerializeField] private LineRenderer glowLine;

        [Tooltip("Subtle sparkles emitted along the beam while it shows. Optional.")]
        [SerializeField] private ParticleSystem motes;

        private float hideTime;

        private void Awake()
        {
            if (coreLine == null)
            {
                coreLine = GetComponent<LineRenderer>();
            }

            SetVisible(false);
            enabled = false;
        }

        /// <summary>Tints every layer; the core is pushed toward white so it reads hot.</summary>
        public void SetTint(Color color)
        {
            if (coreLine != null)
            {
                Color core = Color.Lerp(color, Color.white, 0.6f);
                coreLine.startColor = core;
                coreLine.endColor = core;
            }

            if (glowLine != null)
            {
                Color glow = new Color(color.r, color.g, color.b, 0.35f);
                glowLine.startColor = glow;
                glowLine.endColor = glow;
            }

            if (motes != null)
            {
                ParticleSystem.MainModule main = motes.main;
                main.startColor = new Color(color.r, color.g, color.b, 0.6f);
            }
        }

        /// <summary>Shows the beam between two points for <paramref name="life"/> seconds.</summary>
        public void Flash(Vector3 start, Vector3 end, float life)
        {
            if (coreLine == null)
            {
                coreLine = GetComponent<LineRenderer>();
                if (coreLine == null)
                {
                    return;
                }
            }

            coreLine.SetPosition(0, start);
            coreLine.SetPosition(1, end);

            if (glowLine != null)
            {
                glowLine.SetPosition(0, start);
                glowLine.SetPosition(1, end);
            }

            if (motes != null)
            {
                // Lay the emitter edge (local X) along the beam span.
                Vector3 span = end - start;
                float length = span.magnitude;
                if (length > 0.01f)
                {
                    motes.transform.position = (start + end) * 0.5f;
                    motes.transform.rotation = Quaternion.FromToRotation(Vector3.right, span / length);
                    ParticleSystem.ShapeModule shape = motes.shape;
                    shape.radius = length * 0.5f;
                }
            }

            SetVisible(true);
            hideTime = Time.time + Mathf.Max(0.01f, life);
            enabled = true; // resume Update until it hides itself
        }

        private void Update()
        {
            if (Time.time < hideTime)
            {
                return;
            }

            SetVisible(false);
            enabled = false; // idle until the next Flash
        }

        private void SetVisible(bool visible)
        {
            if (coreLine != null)
            {
                coreLine.enabled = visible;
            }

            if (glowLine != null)
            {
                glowLine.enabled = visible;
            }

            if (motes != null)
            {
                ParticleSystem.EmissionModule emission = motes.emission;
                emission.enabled = visible; // live motes finish on their own
            }
        }
    }
}
