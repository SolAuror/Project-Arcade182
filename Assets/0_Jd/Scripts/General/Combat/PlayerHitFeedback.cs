using UnityEngine;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// First-person damage feedback for the player: a full-screen red flash on
    /// every hit and a slow heartbeat vignette while health is critical. Builds
    /// its own overlay canvas at runtime — no prefab wiring required.
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
        [Tooltip("Full-screen flash image, authored on the player prefab. Built at runtime only as a fallback.")]
        [SerializeField] private Image overlayImage;

        private Health health;
        private float flashStrength;

        private void Awake()
        {
            health = GetComponent<Health>();

            if (overlayImage == null)
            {
                BuildOverlay(); // fallback for players without the authored overlay
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

        private void BuildOverlay()
        {
            GameObject overlayObject = new GameObject("Player Damage Overlay");
            overlayObject.transform.SetParent(transform, false);

            Canvas canvas = overlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40; // above gameplay HUD, below modal screens

            GameObject imageObject = new GameObject("Flash");
            imageObject.transform.SetParent(overlayObject.transform, false);

            overlayImage = imageObject.AddComponent<Image>();
            overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
            overlayImage.raycastTarget = false;

            RectTransform rect = overlayImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
