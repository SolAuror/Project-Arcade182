using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Procedural shockwave for burst spells: a translucent sphere that expands
    /// from the caster to the blast radius while fading out. Built at runtime —
    /// no prefab required (same approach as <see cref="AtomSmasherElectron"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Spell Burst Visual")]
    public class SpellBurstVisual : MonoBehaviour
    {
        private Renderer burstRenderer;
        private Color baseColor;
        private float startTime;
        private float lifeSeconds;
        private float maxDiameter;

        public static SpellBurstVisual Spawn(Vector3 center, float radius, Color color, float lifeSeconds)
        {
            GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "Spell Burst";
            burst.transform.position = center;

            Collider burstCollider = burst.GetComponent<Collider>();
            if (burstCollider != null)
            {
                Destroy(burstCollider);
            }

            Renderer burstRenderer = burst.GetComponent<Renderer>();
            if (burstRenderer != null)
            {
                Shader transparentShader = Shader.Find("Sprites/Default");
                if (transparentShader != null)
                {
                    burstRenderer.material = new Material(transparentShader);
                }
            }

            SpellBurstVisual visual = burst.AddComponent<SpellBurstVisual>();
            visual.burstRenderer = burstRenderer;
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
