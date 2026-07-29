using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Reusable on-hit feedback: briefly tints every child renderer whenever the
    /// sibling <see cref="Health"/> takes damage. Uses material property blocks,
    /// so shared materials are never touched.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Sol/Minigames/Shared/Hit Flash")]
    public class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Tint applied while flashing.")]
        [SerializeField] private Color flashColor = new Color(1f, 0.32f, 0.32f, 1f);

        [SerializeField, Min(0.02f)] private float flashSeconds = 0.12f;

        private Health health;
        private Renderer[] flashRenderers;
        private MaterialPropertyBlock propertyBlock;
        private float flashUntil = -1f;
        private bool flashing;

        private void Awake()
        {
            health = GetComponent<Health>();
            flashRenderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            health.OnDamaged.AddListener(HandleDamaged);
        }

        private void OnDisable()
        {
            health.OnDamaged.RemoveListener(HandleDamaged);
            EndFlash();
        }

        private void Update()
        {
            if (flashing && Time.time >= flashUntil)
            {
                EndFlash();
            }
        }

        private void HandleDamaged(float amount)
        {
            flashUntil = Time.time + flashSeconds;
            if (flashing)
            {
                return;
            }

            flashing = true;
            propertyBlock.SetColor(BaseColorId, flashColor);
            propertyBlock.SetColor(ColorId, flashColor);

            foreach (Renderer flashRenderer in flashRenderers)
            {
                if (flashRenderer != null && !(flashRenderer is TrailRenderer) && !(flashRenderer is ParticleSystemRenderer))
                {
                    flashRenderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private void EndFlash()
        {
            if (!flashing)
            {
                return;
            }

            flashing = false;
            foreach (Renderer flashRenderer in flashRenderers)
            {
                if (flashRenderer != null && !(flashRenderer is TrailRenderer) && !(flashRenderer is ParticleSystemRenderer))
                {
                    flashRenderer.SetPropertyBlock(null);
                }
            }
        }
    }
}
