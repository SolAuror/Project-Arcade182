using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Floating world-space damage number: rises, faces the camera, fades out,
    /// and destroys itself. Spawn via <see cref="Spawn"/> from any combat code.
    /// The visual (text plus a dark readability drop shadow) is the authored
    /// Resources/DamagePopup.prefab; the static spawners only instantiate it
    /// and fill in message, color, life, and size.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Shared/Damage Popup")]
    public class DamagePopup : MonoBehaviour
    {
        private const string PrefabResourcePath = "DamagePopup";
        // Sized for legibility at the 240-line retro render target.
        private const float BaseCharacterSize = 0.05f;

        private static readonly Color DefaultColor = new Color(1f, 0.85f, 0.3f, 1f);
        private static DamagePopup cachedPrefab;

        [SerializeField, Min(0.1f)] private float lifeSeconds = 0.9f;
        [SerializeField, Min(0f)] private float riseSpeed = 1.25f;

        [Tooltip("Main text. Authored on the prefab.")]
        [SerializeField] private TextMesh textMesh;

        [Tooltip("Dark offset copy behind the text so pops stay readable. Authored on the prefab.")]
        [SerializeField] private TextMesh shadowMesh;

        private Color baseColor = DefaultColor;
        private float dieTime;

        public static DamagePopup Spawn(Vector3 position, float amount, Color? color = null, float sizeScale = 1f)
        {
            return SpawnText(position, Mathf.Max(1f, Mathf.Round(amount)).ToString("0"), color, 0f, sizeScale);
        }

        /// <summary>Floating world-space message (clerk dialogue, pickup notices, etc.).</summary>
        public static DamagePopup SpawnText(Vector3 position, string message, Color? color = null, float lifeSeconds = 0f, float sizeScale = 1f)
        {
            if (cachedPrefab == null)
            {
                cachedPrefab = Resources.Load<DamagePopup>(PrefabResourcePath);
                if (cachedPrefab == null)
                {
                    Debug.LogWarning($"DamagePopup prefab missing from a Resources folder ('{PrefabResourcePath}'); popup '{message}' skipped.");
                    return null;
                }
            }

            Vector2 jitter = Random.insideUnitCircle * 0.2f;
            DamagePopup popup = Instantiate(cachedPrefab);
            popup.transform.position = position + new Vector3(jitter.x, Random.value * 0.2f, jitter.y);
            popup.Configure(message, color ?? DefaultColor, lifeSeconds, sizeScale);
            return popup;
        }

        private void Configure(string message, Color color, float overrideLifeSeconds, float sizeScale)
        {
            baseColor = color;
            if (overrideLifeSeconds > 0f)
            {
                lifeSeconds = overrideLifeSeconds;
            }

            dieTime = Time.time + lifeSeconds;

            float characterSize = BaseCharacterSize * Mathf.Max(0.1f, sizeScale);
            if (textMesh != null)
            {
                textMesh.text = message;
                textMesh.color = baseColor;
                textMesh.characterSize = characterSize;
            }

            if (shadowMesh != null)
            {
                shadowMesh.text = message;
                shadowMesh.characterSize = characterSize;
                shadowMesh.color = new Color(0f, 0f, 0f, baseColor.a * 0.85f);
            }
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
                if (shadowMesh != null)
                {
                    shadowMesh.color = new Color(0f, 0f, 0f, 0.85f * baseColor.a * alpha);
                }
            }
        }
    }
}
