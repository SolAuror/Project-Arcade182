using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Floating world-space damage number: rises, faces the camera, fades out,
    /// and destroys itself. Spawn via <see cref="Spawn"/> from any combat code.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Shared/Damage Popup")]
    public class DamagePopup : MonoBehaviour
    {
        private static readonly Color DefaultColor = new Color(1f, 0.85f, 0.3f, 1f);

        [SerializeField, Min(0.1f)] private float lifeSeconds = 0.7f;
        [SerializeField, Min(0f)] private float riseSpeed = 1.4f;

        private TextMesh textMesh;
        private Color baseColor = DefaultColor;
        private float dieTime;

        public static DamagePopup Spawn(Vector3 position, float amount, Color? color = null)
        {
            return SpawnText(position, Mathf.Max(1f, Mathf.Round(amount)).ToString("0"), color);
        }

        /// <summary>Floating world-space message (clerk dialogue, pickup notices, etc.).</summary>
        public static DamagePopup SpawnText(Vector3 position, string message, Color? color = null, float lifeSeconds = 0f)
        {
            Vector2 jitter = Random.insideUnitCircle * 0.2f;
            GameObject popupObject = new GameObject("DamagePopup");
            popupObject.transform.position = position + new Vector3(jitter.x, Random.value * 0.2f, jitter.y);

            TextMesh text = popupObject.AddComponent<TextMesh>();
            text.text = message;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 46;
            text.characterSize = 0.035f;
            text.fontStyle = FontStyle.Bold;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                Renderer textRenderer = popupObject.GetComponent<MeshRenderer>();
                if (textRenderer != null)
                {
                    textRenderer.material = font.material;
                }
            }

            DamagePopup popup = popupObject.AddComponent<DamagePopup>();
            popup.textMesh = text;
            popup.baseColor = color ?? DefaultColor;
            text.color = popup.baseColor;

            if (lifeSeconds > 0f)
            {
                popup.lifeSeconds = lifeSeconds;
                popup.dieTime = Time.time + lifeSeconds; // Awake already ran with the default
            }

            return popup;
        }

        private void Awake()
        {
            dieTime = Time.time + lifeSeconds;
        }

        private void Update()
        {
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            Camera viewCamera = Camera.main;
            if (viewCamera != null)
            {
                transform.rotation = viewCamera.transform.rotation;
            }

            float remaining = dieTime - Time.time;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (textMesh != null)
            {
                float alpha = Mathf.Clamp01(remaining / (lifeSeconds * 0.5f));
                textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            }
        }
    }
}
