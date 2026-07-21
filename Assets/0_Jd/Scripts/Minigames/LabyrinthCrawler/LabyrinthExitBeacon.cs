using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Vertical "portal" beam that rises above the maze walls to mark the exit.
    /// Hidden until the game reveals it: the default rule reveals once every
    /// enemy is felled, and the Cartographer upgrade adds an early timed reveal
    /// (see <see cref="LabyrinthCrawlerGame.ExitRevealAfterSeconds"/>). The beam
    /// reads red while enemies still live and light blue once the room is clear,
    /// and fades out as the player closes in so it never blinds you at the pad.
    ///
    /// Drawn with two world-space <see cref="LineRenderer"/> layers (a bright
    /// core inside a wide translucent glow) so the exit pad's squashed scale
    /// never distorts the column. Instanced onto the active pad each stage by
    /// <see cref="LabyrinthCrawlerGame.ConfigureExitBeacon"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Exit Beacon")]
    public class LabyrinthExitBeacon : MonoBehaviour
    {
        [Header("Layers")]
        [Tooltip("Bright inner beam line.")]
        [SerializeField] private LineRenderer coreLine;

        [Tooltip("Wide translucent outer glow line.")]
        [SerializeField] private LineRenderer glowLine;

        [Header("Shape")]
        [Tooltip("How far up the beam rises from the pad, in world units. Set tall enough to clear the maze walls.")]
        [SerializeField, Min(1f)] private float height = 10f;

        [Tooltip("Lift of the beam base off the floor.")]
        [SerializeField, Min(0f)] private float groundOffset = 0.1f;

        [Header("Color")]
        [Tooltip("Beam color while enemies are still alive (portal not yet safe).")]
        [SerializeField] private Color hostileColor = new Color(1f, 0.25f, 0.2f, 1f);

        [Tooltip("Beam color once every enemy is felled.")]
        [SerializeField] private Color clearColor = new Color(0.4f, 0.85f, 1f, 1f);

        [Header("Proximity Fade")]
        [Tooltip("At or beyond this horizontal distance the beam is fully opaque.")]
        [SerializeField, Min(1f)] private float fadeStartDistance = 14f;

        [Tooltip("At or within this horizontal distance the beam is at its faintest.")]
        [SerializeField, Min(0f)] private float fadeEndDistance = 3f;

        [Tooltip("Alpha multiplier when the player is right on the pad.")]
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.06f;

        [Header("Feel")]
        [Tooltip("How quickly the beam eases between red and blue when the room clears.")]
        [SerializeField, Min(0f)] private float colorLerpSpeed = 6f;

        [SerializeField, Min(0f)] private float pulseRate = 2.5f;
        [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.12f;

        [Tooltip("Relative alpha of the glow layer against the core.")]
        [SerializeField, Range(0f, 1f)] private float glowAlphaScale = 0.35f;

        private LabyrinthCrawlerGame game;
        private Color currentColor;
        private bool shown;

        private void Awake()
        {
            currentColor = clearColor;
            if (coreLine != null)
            {
                coreLine.useWorldSpace = true;
            }

            if (glowLine != null)
            {
                glowLine.useWorldSpace = true;
            }

            SetShown(false);
        }

        /// <summary>Binds the beacon to the running game and starts it hidden.</summary>
        public void Bind(LabyrinthCrawlerGame owningGame)
        {
            game = owningGame;
            currentColor = clearColor;
            SetShown(false);
        }

        private void Update()
        {
            if (game == null || !game.IsRunning)
            {
                SetShown(false);
                return;
            }

            bool enemiesAlive = game.EnemiesRemaining > 0;
            bool revealed = !enemiesAlive || game.StageElapsedSeconds >= game.ExitRevealAfterSeconds;
            if (!revealed)
            {
                SetShown(false);
                return;
            }

            SetShown(true);
            UpdateBeam(enemiesAlive);
        }

        private void UpdateBeam(bool enemiesAlive)
        {
            Vector3 basePoint = transform.position + Vector3.up * groundOffset;
            Vector3 topPoint = basePoint + Vector3.up * height;
            SetLinePositions(coreLine, basePoint, topPoint);
            SetLinePositions(glowLine, basePoint, topPoint);

            Color target = enemiesAlive ? hostileColor : clearColor;
            currentColor = Color.Lerp(currentColor, target, 1f - Mathf.Exp(-colorLerpSpeed * Time.deltaTime));

            float pulse = 1f + Mathf.Sin(Time.time * pulseRate) * pulseAmount;
            float alpha = ProximityAlpha(basePoint) * pulse;

            // Core reads hot (pushed toward white); glow carries the hue.
            ApplyColor(coreLine, Color.Lerp(currentColor, Color.white, 0.6f), alpha);
            ApplyColor(glowLine, currentColor, alpha * glowAlphaScale);
        }

        private float ProximityAlpha(Vector3 basePoint)
        {
            Transform player = game != null && game.PlayerHealth != null ? game.PlayerHealth.transform : null;
            if (player == null)
            {
                return 1f;
            }

            Vector3 delta = player.position - basePoint;
            delta.y = 0f;
            float distance = delta.magnitude;
            float farness = Mathf.InverseLerp(fadeEndDistance, fadeStartDistance, distance); // 0 near, 1 far
            return Mathf.Lerp(minAlpha, 1f, farness);
        }

        private static void SetLinePositions(LineRenderer line, Vector3 a, Vector3 b)
        {
            if (line == null)
            {
                return;
            }

            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        private static void ApplyColor(LineRenderer line, Color rgb, float alpha)
        {
            if (line == null)
            {
                return;
            }

            Color tint = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(alpha));
            line.startColor = tint;
            line.endColor = tint;
        }

        private void SetShown(bool show)
        {
            if (shown == show)
            {
                return;
            }

            shown = show;
            if (coreLine != null)
            {
                coreLine.enabled = show;
            }

            if (glowLine != null)
            {
                glowLine.enabled = show;
            }
        }
    }
}
