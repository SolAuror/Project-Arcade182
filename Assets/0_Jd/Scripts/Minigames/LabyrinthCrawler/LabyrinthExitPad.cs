using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Stand-on exit zone in the end room. Clears the stage instantly when
    /// every enemy is dead, otherwise after an interruptible dwell. Player
    /// detection is a spherical trigger on the pad (a fallback sphere is added
    /// in Awake when the prefab carries none) - the old scale-relative bounds
    /// check broke on the authored pad's squashed root scale. The visual
    /// breathes subtly: a small footprint pulse with the material's emission
    /// pulsing in sync, driven through a MaterialPropertyBlock (Arcade/PS1/Lit
    /// has no _Color property, so Material.color must never be touched).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Exit Pad")]
    public class LabyrinthExitPad : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Zone")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Trigger radius (local units) for the fallback sphere added when no trigger collider is authored on the pad.")]
        [SerializeField, Min(0.05f)] private float fallbackTriggerRadius = 0.5f;

        [Header("Clear")]
        [Tooltip("Seconds the player must stand here while enemies are alive.")]
        [SerializeField, Min(0f)] private float clearDwellSeconds = 1.5f;

        [Header("Feel")]
        [Tooltip("Idle breathing amplitude of the pad footprint.")]
        [SerializeField, Min(0f)] private float idlePulseScale = 0.012f;

        [Tooltip("Pulse amplitude while channeling the dwell or when the stage is clearable.")]
        [SerializeField, Min(0f)] private float activePulseScale = 0.035f;

        [Tooltip("How strongly the emission breathes with the size pulse (0 = steady glow).")]
        [SerializeField, Range(0f, 1f)] private float emissionPulse = 0.45f;

        private LabyrinthCrawlerGame game;
        private float dwell;
        private bool cleared;
        private int playerOverlaps;
        private Renderer padRenderer;
        private Transform padVisual;
        private Vector3 padVisualBaseScale;
        private Color baseEmission;
        private bool hasEmission;
        private MaterialPropertyBlock propertyBlock;
        private bool visualCaptured;

        public bool PlayerInside => playerOverlaps > 0;
        public float DwellProgress => clearDwellSeconds > 0f ? Mathf.Clamp01(dwell / clearDwellSeconds) : 1f;

        /// <summary>
        /// Binds the pad to the running game. Zone size stays as authored on
        /// the prefab; only the dwell comes from game config.
        /// </summary>
        public void Initialize(LabyrinthCrawlerGame owningGame, float dwellSeconds)
        {
            game = owningGame;
            clearDwellSeconds = Mathf.Max(0f, dwellSeconds);
            dwell = 0f;
            cleared = false;
            playerOverlaps = 0;
        }

        private void Awake()
        {
            EnsureTrigger();
        }

        // Authored pads carry their own trigger sphere; this safety net keeps
        // a stage clearable even if a pad variant loses its collider.
        private void EnsureTrigger()
        {
            foreach (Collider ownCollider in GetComponents<Collider>())
            {
                if (ownCollider != null && ownCollider.isTrigger)
                {
                    return;
                }
            }

            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = fallbackTriggerRadius;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                playerOverlaps++;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                playerOverlaps = Mathf.Max(0, playerOverlaps - 1);
            }
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && (other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag));
        }

        private void Update()
        {
            if (cleared || game == null || !game.IsRunning || game.IsChoosingUpgrade)
            {
                return;
            }

            AnimateVisual();

            if (!PlayerInside)
            {
                dwell = 0f; // stepping off resets the channel
                return;
            }

            if (game.EnemiesRemaining == 0)
            {
                Clear();
                return;
            }

            dwell += Time.deltaTime;
            if (dwell >= clearDwellSeconds)
            {
                Clear();
            }
        }

        // Pad reads as alive: a subtle breath when idle, beckoning when the
        // stage is clearable, ramping bright while the dwell channels - size
        // and emission pulse together.
        private void AnimateVisual()
        {
            if (!visualCaptured && !TryCaptureVisual())
            {
                return;
            }

            float pulseRate;
            float pulseAmplitude;
            float lift;

            if (PlayerInside)
            {
                pulseRate = 9f;
                pulseAmplitude = activePulseScale;
                lift = game.EnemiesRemaining > 0 ? DwellProgress : 1f;
            }
            else if (game.EnemiesRemaining == 0)
            {
                pulseRate = 4f;
                pulseAmplitude = activePulseScale;
                lift = 0.35f;
            }
            else
            {
                pulseRate = 2f;
                pulseAmplitude = idlePulseScale;
                lift = 0f;
            }

            float wave = Mathf.Sin(Time.time * pulseRate); // -1..1
            float scalePulse = 1f + wave * pulseAmplitude;
            padVisual.localScale = new Vector3(
                padVisualBaseScale.x * scalePulse,
                padVisualBaseScale.y,
                padVisualBaseScale.z * scalePulse);

            if (hasEmission)
            {
                float glow = (1f + wave * emissionPulse) * (1f + lift * 1.6f);
                padRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, baseEmission * glow);
                padRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private bool TryCaptureVisual()
        {
            padRenderer = GetComponentInChildren<Renderer>();
            if (padRenderer == null)
            {
                return false;
            }

            padVisual = padRenderer.transform;
            padVisualBaseScale = padVisual.localScale;
            propertyBlock = new MaterialPropertyBlock();

            Material sharedMaterial = padRenderer.sharedMaterial;
            hasEmission = sharedMaterial != null && sharedMaterial.HasProperty(EmissionColorId);
            if (hasEmission)
            {
                baseEmission = sharedMaterial.GetColor(EmissionColorId);
            }

            visualCaptured = true;
            return true;
        }

        private void Clear()
        {
            cleared = true;
            playerOverlaps = 0;
            dwell = 0f;
            game.CompleteEscape();
        }
    }
}
