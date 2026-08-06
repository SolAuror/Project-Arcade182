using UnityEngine;
using UnityEngine.Rendering;

public sealed class AirFootyWorldPopup : MonoBehaviour
{
    private TextMesh textMesh;
    private Color baseColor;
    private float startTime;
    private float lifeSeconds;

    public static void Spawn(Vector3 position, string message, Color color)
    {
        GameObject popupObject =
            AirFootyPrefabLibrary.InstantiateWorldPopup(position);
        if (popupObject == null)
        {
            Debug.LogError("AirFooty world popup prefab is missing.");
            return;
        }

        popupObject.name = "AirFooty Goal Message";
        AirFootyWorldPopup popup = popupObject.GetComponent<AirFootyWorldPopup>();
        if (popup == null)
        {
            Debug.LogError("AirFooty world popup prefab is missing its authored AirFootyWorldPopup component.", popupObject);
            Object.Destroy(popupObject);
            return;
        }
        popup.Configure(message, color, 0.9f);
    }

    private void Configure(string message, Color color, float lifetime)
    {
        textMesh = GetComponent<TextMesh>();
        if (textMesh == null)
        {
            Debug.LogError("AirFooty world popup prefab is missing its authored TextMesh.", this);
            return;
        }
        textMesh.text = message;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 72;
        textMesh.characterSize = 0.04f;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.color = color;

        MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        baseColor = color;
        lifeSeconds = lifetime;
        startTime = Time.unscaledTime;
    }

    private void Update()
    {
        float progress =
            (Time.unscaledTime - startTime) / Mathf.Max(0.01f, lifeSeconds);
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Camera view = AirFootyCameraLookup.FindDisplayCamera();
        if (view != null)
        {
            transform.rotation = view.transform.rotation;
        }

        transform.position += Vector3.up * (0.7f * Time.unscaledDeltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(
            0.75f,
            1.15f,
            1f - (1f - progress) * (1f - progress));
        textMesh.color = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            1f - progress);
    }
}
