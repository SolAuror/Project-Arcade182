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
        [SerializeField] private int scoreValue = 100;
        [SerializeField] private bool requiredTarget = true;
        [SerializeField] private bool deactivateOnHit = true;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color hitColor = Color.black;

        [Header("Death Pop")]
        [Tooltip("Brief expand-then-shrink when smashed instead of vanishing instantly. 0 disables.")]
        [SerializeField, Min(0f)] private float deathPopSeconds = 0.16f;

        [SerializeField, Min(1f)] private float deathPopScale = 1.35f;

        private bool hasBeenHit;
        private MaterialPropertyBlock propertyBlock;
        private Collider[] targetColliders;
        private Vector3 baseScale;
        private Coroutine deathPopRoutine;

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
            ApplyColor(activeColor);
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
            if (targetRenderers == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
