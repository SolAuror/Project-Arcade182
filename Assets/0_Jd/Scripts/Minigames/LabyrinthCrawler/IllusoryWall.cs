using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Sol.Minigames
{
    /// <summary>
    /// A fake wall slab hiding a secret passage. Solid to enemies and spells
    /// (impacts ripple its surface via <see cref="ISpellImpactReceiver"/>) but
    /// intangible to the player: walking fully through reveals the secret and
    /// dither-dissolves the wall, while backing out the way you came keeps it
    /// hidden. Place the prefab where a wall slab would sit; the visual child
    /// matches the dungeon wall cubes (7.5 x 6 x 0.5) and local Z is the thin
    /// axis the player crosses.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Illusory Wall")]
    public class IllusoryWall : MonoBehaviour, ISpellImpactReceiver
    {
        private const int MaxRipples = 8;

        private static readonly int RipplePointsId = Shader.PropertyToID("_RipplePoints");
        private static readonly int RippleAmpsId = Shader.PropertyToID("_RippleAmps");
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

        [Header("Passage")]
        [Tooltip("Blocks enemies and spells; the player is ignored so they can pass. Auto-found (first non-trigger collider) when unset.")]
        [SerializeField] private Collider solidCollider;

        [Tooltip("Detects the player entering/leaving the wall. Auto-found (first trigger collider) when unset.")]
        [SerializeField] private Collider passTrigger;

        [SerializeField] private string playerTag = "Player";

        [Header("Reveal")]
        [Tooltip("Seconds the dissolve takes once the player walks through.")]
        [SerializeField, Min(0.05f)] private float dissolveSeconds = 2f;

        [SerializeField] private AudioClip revealClip;
        [SerializeField, Range(0f, 1f)] private float revealVolume = 0.9f;

        [Tooltip("Tint of the garbled whisper that floats up when the wall gives way.")]
        [SerializeField] private Color whisperColor = new Color(0.55f, 0.9f, 0.6f, 1f);
        [SerializeField] private UnityEvent onRevealed = new UnityEvent();

        [Header("Ripples")]
        [Tooltip("Ripple amplitude for spell impacts (shooting a suspicious wall is the sanctioned way to test it).")]
        [SerializeField, Range(0f, 2f)] private float spellRippleAmp = 1f;

        [Tooltip("Ripple amplitude when the player brushes the wall.")]
        [SerializeField, Range(0f, 2f)] private float touchRippleAmp = 0.55f;

        [Tooltip("Closest the player's collider center must be to the solid slab before brush ripples fire.")]
        [SerializeField, Min(0.05f)] private float touchRippleMaxDistance = 1.1f;

        [Tooltip("Minimum seconds between brush ripples while the player remains inside the wall trigger.")]
        [SerializeField, Min(0.05f)] private float touchRippleInterval = 0.35f;

        private readonly Vector4[] ripplePoints = new Vector4[MaxRipples];
        private readonly float[] rippleAmps = new float[MaxRipples];

        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private Transform playerRoot; // resolved lazily; rooms spawn after the player
        private Transform insideBody;
        private int nextRipple;
        private float entrySide;
        private float nextTouchRippleTime;
        private bool revealed;

        public bool IsRevealed => revealed;
        public UnityEvent OnRevealed => onRevealed;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < MaxRipples; i++)
            {
                ripplePoints[i] = new Vector4(0f, 0f, 0f, -1000f); // ancient start time = inert
                rippleAmps[i] = 0f;
            }

            foreach (Collider ownCollider in GetComponentsInChildren<Collider>(true))
            {
                if (ownCollider.isTrigger)
                {
                    passTrigger = passTrigger != null ? passTrigger : ownCollider;
                }
                else
                {
                    solidCollider = solidCollider != null ? solidCollider : ownCollider;
                }
            }

            if (solidCollider == null || passTrigger == null)
            {
                Debug.LogWarning($"{name} needs one solid collider and one trigger collider to act as an illusory wall.", this);
            }

            PushRipples();
        }

        private void Update()
        {
            if (revealed)
            {
                return;
            }

            if (playerRoot == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    playerRoot = player.transform;
                    IgnorePlayerCollisions(player);
                }
            }

            // Trigger exit is the usual reveal path; this catches the player
            // crossing the wall's mid-plane while still inside the trigger.
            if (insideBody != null && entrySide != 0f &&
                Mathf.Sign(SideOf(insideBody.position)) != Mathf.Sign(entrySide))
            {
                Reveal(insideBody.position);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (revealed || !IsPlayer(other))
            {
                return;
            }

            insideBody = other.transform;
            entrySide = SideOf(other.transform.position);

            TryEmitTouchRipple(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (revealed || !IsPlayer(other))
            {
                return;
            }

            if (insideBody == null)
            {
                insideBody = other.transform;
                entrySide = SideOf(other.transform.position);
            }

            TryEmitTouchRipple(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (revealed || !IsPlayer(other))
            {
                return;
            }

            float exitSide = SideOf(other.transform.position);
            bool walkedThrough = entrySide != 0f && Mathf.Sign(exitSide) != Mathf.Sign(entrySide);
            insideBody = null;
            entrySide = 0f;

            if (walkedThrough)
            {
                Reveal(other.transform.position);
            }
        }

        /// <summary>Spell impacts ripple the surface instead of scarring it.</summary>
        public void OnSpellImpact(Vector3 point, Vector3 normal, Faction faction)
        {
            if (!revealed)
            {
                EmitRipple(point, spellRippleAmp);
            }
        }

        /// <summary>Queues a surface ripple expanding from the world-space point.</summary>
        public void EmitRipple(Vector3 worldPoint, float amplitude)
        {
            ripplePoints[nextRipple] = new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, Time.timeSinceLevelLoad);
            rippleAmps[nextRipple] = Mathf.Max(0f, amplitude);
            nextRipple = (nextRipple + 1) % MaxRipples;
            PushRipples();
        }

        /// <summary>Drops the disguise: farewell ripple, dissolve, passage opens for everyone.</summary>
        public void Reveal(Vector3 fromPoint)
        {
            if (revealed)
            {
                return;
            }

            revealed = true;
            EmitRipple(fromPoint, 1.6f); // farewell surge racing outward as it dissolves

            if (solidCollider != null)
            {
                solidCollider.enabled = false;
            }

            if (passTrigger != null)
            {
                passTrigger.enabled = false;
            }

            if (revealClip != null)
            {
                AudioSource.PlayClipAtPoint(revealClip, transform.position, revealVolume);
            }

            DamagePopup.SpawnText(transform.position, BuildWhisper(), whisperColor, 1.7f, 1.5f);

            onRevealed.Invoke();
            StartCoroutine(DissolveRoutine());
        }

        private IEnumerator DissolveRoutine()
        {
            float elapsed = 0f;
            while (elapsed < dissolveSeconds)
            {
                elapsed += Time.deltaTime;
                SetDissolve(Mathf.SmoothStep(0f, 1f, elapsed / dissolveSeconds));
                yield return null;
            }

            SetDissolve(1f);
            foreach (Renderer wallRenderer in renderers)
            {
                if (wallRenderer != null)
                {
                    wallRenderer.enabled = false;
                }
            }
        }

        private void SetDissolve(float amount)
        {
            foreach (Renderer wallRenderer in renderers)
            {
                if (wallRenderer == null)
                {
                    continue;
                }

                wallRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(DissolveAmountId, Mathf.Clamp01(amount));
                wallRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void PushRipples()
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer wallRenderer in renderers)
            {
                if (wallRenderer == null)
                {
                    continue;
                }

                wallRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVectorArray(RipplePointsId, ripplePoints);
                propertyBlock.SetFloatArray(RippleAmpsId, rippleAmps);
                wallRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void IgnorePlayerCollisions(GameObject player)
        {
            if (solidCollider == null)
            {
                return;
            }

            foreach (Collider playerCollider in player.GetComponentsInChildren<Collider>(true))
            {
                if (playerCollider != null && playerCollider.gameObject.activeInHierarchy && playerCollider.enabled)
                {
                    Physics.IgnoreCollision(solidCollider, playerCollider, true);
                }
            }
        }

        private void TryEmitTouchRipple(Collider other)
        {
            if (Time.time < nextTouchRippleTime)
            {
                return;
            }

            Vector3 probePoint = other.bounds.center;
            Vector3 touchPoint = solidCollider != null ? solidCollider.ClosestPoint(probePoint) : probePoint;

            if (solidCollider != null &&
                Vector3.Distance(probePoint, touchPoint) > touchRippleMaxDistance)
            {
                return;
            }

            EmitRipple(touchPoint, touchRippleAmp);
            nextTouchRippleTime = Time.time + touchRippleInterval;
        }

        // The wall does not announce "Secret!" - it whispers something that
        // was never meant to be heard.
        private static readonly string[] WhisperSyllables =
        {
            "yth", "gn'", "kha", "thl", "hl'", "sha", "ur", "og", "za",
            "vor", "fht", "r'ly", "ne", "ia", "ck", "oth", "ngu", "sk"
        };

        private static string BuildWhisper()
        {
            System.Text.StringBuilder garble = new System.Text.StringBuilder();
            int syllables = Random.Range(2, 4);
            for (int i = 0; i < syllables; i++)
            {
                garble.Append(WhisperSyllables[Random.Range(0, WhisperSyllables.Length)]);
            }

            // Space the glyphs apart so the garble stays legible at 240p.
            System.Text.StringBuilder whisper = new System.Text.StringBuilder(garble.Length * 2 + 3);
            for (int i = 0; i < garble.Length; i++)
            {
                if (i > 0)
                {
                    whisper.Append(' ');
                }

                whisper.Append(garble[i]);
            }

            return whisper.Append(" ...").ToString();
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && (other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag));
        }

        private float SideOf(Vector3 worldPosition)
        {
            return Vector3.Dot(worldPosition - transform.position, transform.forward);
        }
    }
}
