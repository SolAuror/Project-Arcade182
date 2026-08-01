using System.Collections;
using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Target")]
    public class AtomSmasherTarget : MonoBehaviour
    {
        [SerializeField] private AtomSmasherGame game;
        [SerializeField] private int scoreValue = 10;
        [SerializeField] private bool requiredTarget = true;
        [SerializeField] private bool deactivateOnHit = true;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color hitColor = Color.black;

        [Header("Readability")]
        [Tooltip("Self-lit floor added to the idle tint so the atom type stays legible against any board lighting. 0 leaves the material's authored emission alone.")]
        [SerializeField, Min(0f)] private float emissionIntensity;

        [Tooltip("Pulses per second on the self-lit floor — the nervous flicker that marks a volatile atom. 0 keeps it steady.")]
        [SerializeField, Min(0f)] private float emissionPulseSpeed;

        [Tooltip("How far the pulse dips below full brightness.")]
        [SerializeField, Range(0f, 1f)] private float emissionPulseDepth = 0.35f;

        [Header("Death Pop")]
        [Tooltip("Brief expand-then-shrink when smashed instead of vanishing instantly. 0 disables.")]
        [SerializeField, Min(0f)] private float deathPopSeconds = 0.16f;

        [SerializeField, Min(1f)] private float deathPopScale = 1.35f;

        private bool hasBeenHit;
        private MaterialPropertyBlock propertyBlock;
        private Collider[] targetColliders;
        private Vector3 baseScale;
        private Coroutine deathPopRoutine;
        private Color currentColor = Color.white;
        private float pulsePhase;

        public int ScoreValue => scoreValue;
        public bool RequiredTarget => requiredTarget;
        public bool HasBeenHit => hasBeenHit;
        public Color ActiveColor => activeColor;

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<AtomSmasherGame>();
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }

            targetColliders = GetComponentsInChildren<Collider>(true);
            baseScale = transform.localScale;
            propertyBlock = new MaterialPropertyBlock();

            // Offset each atom's pulse so a full board throbs as a field of
            // independent particles instead of strobing in unison.
            pulsePhase = Random.value * Mathf.PI * 2f;
            ApplyColor(activeColor);
        }

        private void Update()
        {
            if (hasBeenHit || emissionIntensity <= 0f || emissionPulseSpeed <= 0f || emissionPulseDepth <= 0f)
            {
                return;
            }

            WriteTint();
        }

        private void OnValidate()
        {
            scoreValue = Mathf.Max(0, scoreValue);
        }

        private void OnCollisionEnter(Collision collision)
        {
            AtomSmasherBall ball = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<AtomSmasherBall>()
                : collision.collider.GetComponentInParent<AtomSmasherBall>();

            if (ball != null)
            {
                TryHit(ball);
            }
        }

        public void AssignGame(AtomSmasherGame owningGame)
        {
            game = owningGame;
        }

        public void ResetTarget()
        {
            if (deathPopRoutine != null)
            {
                StopCoroutine(deathPopRoutine);
                deathPopRoutine = null;
            }

            hasBeenHit = false;
            gameObject.SetActive(true);

            if (baseScale != Vector3.zero)
            {
                transform.localScale = baseScale;
            }

            SetCollidersEnabled(true);
            ApplyColor(activeColor);
        }

        public bool TryHit(AtomSmasherBall ball)
        {
            if (hasBeenHit)
            {
                return false;
            }

            // Unstable atoms only die to rebound shots; direct hits deflect.
            AtomSmasherUnstableTarget unstable = GetComponent<AtomSmasherUnstableTarget>();
            if (unstable != null && !unstable.AllowsHitFrom(ball))
            {
                unstable.DeflectBall(ball);
                return false;
            }

            hasBeenHit = true;
            ApplyColor(hitColor);
            game?.RegisterTargetHit(this, ball);
            GetComponent<AtomSmasherExplosiveTarget>()?.Detonate(ball);

            if (deactivateOnHit)
            {
                // Colliders drop immediately so the pop stays purely visual.
                SetCollidersEnabled(false);

                if (deathPopSeconds > 0f && gameObject.activeInHierarchy)
                {
                    deathPopRoutine = StartCoroutine(DeathPop());
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }

            return true;
        }

        private IEnumerator DeathPop()
        {
            float elapsed = 0f;
            while (elapsed < deathPopSeconds)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / deathPopSeconds);

                // Quick swell, then collapse to nothing.
                float scale = progress < 0.35f
                    ? Mathf.Lerp(1f, deathPopScale, progress / 0.35f)
                    : Mathf.Lerp(deathPopScale, 0f, (progress - 0.35f) / 0.65f);

                transform.localScale = baseScale * scale;
                yield return null;
            }

            deathPopRoutine = null;
            transform.localScale = baseScale;
            gameObject.SetActive(false);
        }

        private void SetCollidersEnabled(bool value)
        {
            if (targetColliders == null)
            {
                return;
            }

            foreach (Collider targetCollider in targetColliders)
            {
                if (targetCollider != null)
                {
                    targetCollider.enabled = value;
                }
            }
        }

        /// <summary>Retints the idle look (runtime quantum marking and similar).</summary>
        public void SetActiveColorOverride(Color color)
        {
            activeColor = color;
            if (!hasBeenHit)
            {
                ApplyColor(color);
            }
        }

        private void ApplyColor(Color color)
        {
            currentColor = color;
            WriteTint();
        }

        // Atom types read purely off their shading: hue plus a self-lit floor
        // that keeps the type legible wherever the atom drifts, and an optional
        // pulse for the ones that should feel unstable.
        private void WriteTint()
        {
            if (targetRenderers == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            Color emission = currentColor * (emissionIntensity * CurrentPulseScale());

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", currentColor);
                propertyBlock.SetColor("_Color", currentColor);

                if (emissionIntensity > 0f)
                {
                    propertyBlock.SetColor("_EmissionColor", emission);
                }

                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private float CurrentPulseScale()
        {
            if (emissionPulseSpeed <= 0f || emissionPulseDepth <= 0f)
            {
                return 1f;
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * emissionPulseSpeed * Mathf.PI * 2f + pulsePhase);
            return Mathf.Lerp(1f - emissionPulseDepth, 1f, wave);
        }
    }
}
