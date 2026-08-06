using UnityEngine;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// First-person damage feedback for the player: a full-screen red flash on
    /// every hit and a slow heartbeat vignette while health is critical. The
    /// full-screen overlay is authored on the player prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Sol/Minigames/Shared/Player Hit Feedback")]
    public class PlayerHitFeedback : MonoBehaviour
    {
        [Header("Damage Flash")]
        [SerializeField, Range(0f, 1f)] private float flashMaxAlpha = 0.38f;
        [SerializeField, Min(0.05f)] private float flashSeconds = 0.35f;

        [Header("Low Health Pulse")]
        [Tooltip("Heartbeat vignette activates below this fraction of max health.")]
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;

        [SerializeField, Range(0f, 1f)] private float lowHealthPulseAlpha = 0.14f;
        [SerializeField, Min(0.1f)] private float lowHealthPulseHz = 1.2f;

        [SerializeField] private Color overlayColor = new Color(0.85f, 0.05f, 0.05f, 1f);

        [Header("Widgets")]
        [Tooltip("Full-screen flash image authored on the player prefab.")]
        [SerializeField] private Image overlayImage;

        private Health health;
        private float flashStrength;

        private void Awake()
        {
            health = GetComponent<Health>();

            if (overlayImage == null)
            {
                Debug.LogError(
                    $"{name} requires an authored full-screen damage overlay Image. " +
                    "Check the authored player prefab.",
                    this);
                enabled = false;
            }
            else
            {
                SetOverlayAlpha(0f);
            }
        }

        private void OnEnable()
        {
            health.OnDamaged.AddListener(HandleDamaged);
        }

        private void OnDisable()
        {
            health.OnDamaged.RemoveListener(HandleDamaged);
            flashStrength = 0f;
            SetOverlayAlpha(0f);
        }

        private void HandleDamaged(float amount)
        {
            flashStrength = 1f;
        }

        private void Update()
        {
            if (overlayImage == null)
            {
                return;
            }

            if (flashStrength > 0f)
            {
                flashStrength = Mathf.Max(0f, flashStrength - Time.deltaTime / flashSeconds);
            }

            float alpha = flashMaxAlpha * flashStrength;

            // Heartbeat vignette while critical, so danger reads without the HUD.
            if (!health.IsDead && health.Normalized > 0f && health.Normalized <= lowHealthThreshold)
            {
                float pulse = (Mathf.Sin(Time.time * lowHealthPulseHz * Mathf.PI * 2f) + 1f) * 0.5f;
                alpha = Mathf.Max(alpha, lowHealthPulseAlpha * pulse);
            }

            SetOverlayAlpha(alpha);
        }

        private void SetOverlayAlpha(float alpha)
        {
            if (overlayImage != null)
            {
                overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, alpha);
            }
        }
    }
}
