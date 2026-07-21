using System;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// The prize sitting inside a secret room: a floating cache the player
    /// collects by walking into it. Its contents are deliberately not readable
    /// from the outside - the roll happens on pickup in
    /// <see cref="LabyrinthCrawlerGame"/> - so every cache carries the same
    /// "what's in it?" beat rather than telegraphing a dud.
    ///
    /// Spawned by <see cref="LabyrinthSecretPass"/> in dead-end rooms only, and
    /// torn down with the rest of the stage on the next maze rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Secret Cache")]
    public class LabyrinthSecretCache : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Zone")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Pickup radius (local units) for the fallback trigger added when the prefab carries none.")]
        [SerializeField, Min(0.05f)] private float fallbackTriggerRadius = 1.4f;

        [Header("Visual")]
        [Tooltip("Floating part that bobs and spins. Falls back to this transform.")]
        [SerializeField] private Transform floater;

        [Tooltip("Renderer pulsed through a MaterialPropertyBlock (Arcade/PS1/Lit exposes no _Color, so the material is never touched directly).")]
        [SerializeField] private Renderer glowRenderer;

        [SerializeField, Min(0f)] private float bobHeight = 0.18f;
        [SerializeField, Min(0f)] private float bobRate = 1.8f;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 55f;
        [SerializeField, Min(0f)] private float emissionPulseRate = 2.4f;
        [SerializeField, Range(0f, 1f)] private float emissionPulse = 0.35f;

        [Header("Collect")]
        [Tooltip("Burst color when the cache is taken. Alpha 0 disables the burst.")]
        [SerializeField] private Color collectBurstColor = new Color(1f, 0.85f, 0.4f, 1f);

        [SerializeField, Min(0.1f)] private float collectBurstRadius = 1.1f;

        [Tooltip("One-shot played at the cache when it is collected.")]
        [SerializeField] private AudioClip collectClip;

        [SerializeField, Range(0f, 1f)] private float collectVolume = 0.8f;

        private Action<LabyrinthSecretCache> onCollected;
        private Vector3 floaterBasePosition;
        private MaterialPropertyBlock propertyBlock;
        private Color baseEmission;
        private bool hasEmission;
        private bool collected;

        private void Awake()
        {
            if (floater == null)
            {
                floater = transform;
            }

            floaterBasePosition = floater.localPosition;
            EnsureTrigger();
            CaptureEmission();
        }

        /// <summary>Binds the pickup callback. A null callback still lets the cache be collected (and vanish).</summary>
        public void Initialize(Action<LabyrinthSecretCache> collectedCallback)
        {
            onCollected = collectedCallback;
            collected = false;
        }

        // Authored caches carry their own trigger; this keeps a prefab variant
        // that loses its collider collectable.
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

        private void CaptureEmission()
        {
            if (glowRenderer == null)
            {
                glowRenderer = GetComponentInChildren<Renderer>();
            }

            if (glowRenderer == null)
            {
                return;
            }

            propertyBlock = new MaterialPropertyBlock();
            Material sharedMaterial = glowRenderer.sharedMaterial;
            hasEmission = sharedMaterial != null && sharedMaterial.HasProperty(EmissionColorId);
            if (hasEmission)
            {
                baseEmission = sharedMaterial.GetColor(EmissionColorId);
            }
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            AnimateVisual();
        }

        private void AnimateVisual()
        {
            if (floater != null)
            {
                float wave = Mathf.Sin(Time.time * bobRate);
                floater.localPosition = floaterBasePosition + Vector3.up * (wave * bobHeight);
                floater.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            if (hasEmission && glowRenderer != null)
            {
                float glow = 1f + Mathf.Sin(Time.time * emissionPulseRate) * emissionPulse;
                glowRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, baseEmission * glow);
                glowRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other == null)
            {
                return;
            }

            if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag))
            {
                return;
            }

            Collect();
        }

        private void Collect()
        {
            collected = true;

            if (collectBurstColor.a > 0f)
            {
                SpellBurstVisual.Spawn(transform.position, collectBurstRadius, collectBurstColor, 0.35f);
            }

            if (collectClip != null)
            {
                AudioSource.PlayClipAtPoint(collectClip, transform.position, collectVolume);
            }

            // The game rolls and applies the contents (and owns the popup), so
            // the cache never needs to know what it was holding.
            onCollected?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
