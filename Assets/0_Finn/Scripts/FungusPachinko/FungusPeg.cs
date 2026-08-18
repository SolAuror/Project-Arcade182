using System.Collections;
using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// A single pachinko peg. When a FungusBall collides with the peg,
    /// the peg briefly flashes using its material's emission, then
    /// smoothly fades back to its normal appearance.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Peg")]
    [RequireComponent(typeof(Collider))]
    public class FungusPeg : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Renderer pegRenderer;
        [SerializeField] private Color flashColor = Color.yellow;

        [Tooltip("Maximum emission brightness.")]
        [SerializeField] private float maxEmissionIntensity = 5f;

        [Tooltip("How long the emission takes to fade out.")]
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("Impact")]
        [Tooltip("Impact speed that produces the maximum flash.")]
        [SerializeField] private float maxImpactVelocity = 5f;

        private Material pegMaterial;
        private Coroutine flashCoroutine;

        private static readonly int EmissionColor =
            Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (pegRenderer == null)
            {
                pegRenderer = GetComponent<Renderer>();
            }

            if (pegRenderer == null)
            {
                Debug.LogWarning(
                    $"FungusPeg on {gameObject.name} could not find a Renderer.",
                    this
                );

                return;
            }

            // Creates a unique material instance for this peg.
            pegMaterial = pegRenderer.material;

            // Make sure emission is enabled.
            pegMaterial.EnableKeyword("_EMISSION");

            // Start with no emission.
            pegMaterial.SetColor(
                EmissionColor,
                Color.black
            );
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Only react to FungusBall collisions.
            if (collision.gameObject.GetComponentInParent<FungusBall>() == null)
            {
                return;
            }

            float impactSpeed = collision.relativeVelocity.magnitude;

            // Convert impact speed into a 0-1 value.
            float impactStrength = Mathf.Clamp01(
                impactSpeed / maxImpactVelocity
            );

            Flash(impactStrength);
        }

        private void Flash(float impactStrength)
        {
            if (pegMaterial == null)
            {
                return;
            }

            // If the peg is already flashing, restart the fade
            // using the new impact strength.
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(
                FlashRoutine(impactStrength)
            );
        }

        private IEnumerator FlashRoutine(float impactStrength)
        {
            // Calculate the starting brightness based on impact strength.
            float intensity = Mathf.Lerp(
                0.5f,
                maxEmissionIntensity,
                impactStrength
            );

            Color startColor = flashColor * intensity;
            Color endColor = Color.black;

            // Immediately set the peg to full brightness.
            pegMaterial.SetColor(
                EmissionColor,
                startColor
            );

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(
                    elapsed / fadeDuration
                );

                // Smooth the fade so it starts quickly and
                // gently fades out.
                t = Mathf.SmoothStep(0f, 1f, t);

                Color currentColor = Color.Lerp(
                    startColor,
                    endColor,
                    t
                );

                pegMaterial.SetColor(
                    EmissionColor,
                    currentColor
                );

                yield return null;
            }

            // Make absolutely sure the emission is completely off.
            pegMaterial.SetColor(
                EmissionColor,
                Color.black
            );

            flashCoroutine = null;
        }

        private void OnDestroy()
        {
            // Destroy the material instance we created.
            if (pegMaterial != null)
            {
                Destroy(pegMaterial);
            }
        }
    }
}