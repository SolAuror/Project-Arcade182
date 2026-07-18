using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Shockwave for burst spells: a translucent sphere that expands from the
    /// caster to the blast radius while fading out. The sphere is the authored
    /// Resources/SpellBurstVisual.prefab; <see cref="Spawn"/> instantiates it
    /// and drives scale plus tint per burst.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Spell Burst Visual")]
    public class SpellBurstVisual : MonoBehaviour
    {
        private const string PrefabResourcePath = "SpellBurstVisual";

        private static SpellBurstVisual cachedPrefab;

        [Tooltip("Sphere renderer tinted and expanded by the burst. Authored on the prefab.")]
        [SerializeField] private Renderer burstRenderer;

        private Color baseColor;
        private float startTime;
        private float lifeSeconds;
        private float maxDiameter;

        public static SpellBurstVisual Spawn(Vector3 center, float radius, Color color, float lifeSeconds)
        {
            if (cachedPrefab == null)
            {
                cachedPrefab = Resources.Load<SpellBurstVisual>(PrefabResourcePath);
                if (cachedPrefab == null)
                {
                    Debug.LogWarning($"SpellBurstVisual prefab missing from a Resources folder ('{PrefabResourcePath}'); burst skipped.");
                    return null;
                }
            }

            SpellBurstVisual visual = Instantiate(cachedPrefab);
            visual.transform.position = center;
            visual.baseColor = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.55f));
            visual.startTime = Time.time;
            visual.lifeSeconds = Mathf.Max(0.05f, lifeSeconds);
            visual.maxDiameter = Mathf.Max(0.1f, radius * 2f);
            visual.Animate(0f);
            return visual;
        }

        private void Update()
        {
            float progress = (Time.time - startTime) / lifeSeconds;
            if (progress >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Animate(progress);
        }

        private void Animate(float progress)
        {
            // Fast expansion that eases out, fading as it grows.
            float eased = 1f - (1f - progress) * (1f - progress);
            transform.localScale = Vector3.one * Mathf.Lerp(maxDiameter * 0.2f, maxDiameter, eased);

            if (burstRenderer != null)
            {
                burstRenderer.material.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - eased));
            }
        }
    }
}
