using UnityEngine;
using UnityEngine.Rendering;

public sealed class AirFootyHoverVisual : MonoBehaviour
{
    private Transform target;
    private LineRenderer hoverRing;
    private Renderer shadowRenderer;
    private Material ringMaterial;
    private Material shadowMaterial;
    private Color ringColor;
    private float ballHeight;

    public void Initialize(Transform followTarget, float height, Color color)
    {
        target = followTarget;
        ballHeight = height;
        ringColor = color;
        name = "AirFooty Ball Hover";

        BuildShadow();
        BuildRing();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = new Vector3(target.position.x, 0.025f, target.position.z);
        transform.rotation = Quaternion.identity;

        float height = Mathf.Max(0.05f, target.position.y - 0.02f);
        float heightFade = Mathf.Clamp01(1.1f - height / Mathf.Max(0.1f, ballHeight * 2f));
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.06f;
        if (hoverRing != null)
        {
            hoverRing.transform.localScale = Vector3.one * pulse;
            Color color = new Color(ringColor.r, ringColor.g, ringColor.b, 0.35f + heightFade * 0.45f);
            hoverRing.startColor = color;
            hoverRing.endColor = color;
        }

        if (shadowRenderer != null)
        {
            shadowRenderer.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f) *
                                                  Mathf.Lerp(1.1f, 0.7f, heightFade);
        }
    }

    private void OnDestroy()
    {
        if (ringMaterial != null)
        {
            Destroy(ringMaterial);
        }
        if (shadowMaterial != null)
        {
            Destroy(shadowMaterial);
        }
    }

    private void BuildRing()
    {
        GameObject ringObject = new GameObject("Hover Ring");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, 0.018f, 0f);
        hoverRing = ringObject.AddComponent<LineRenderer>();
        hoverRing.useWorldSpace = false;
        hoverRing.loop = true;
        hoverRing.positionCount = 48;
        hoverRing.startWidth = 0.045f;
        hoverRing.endWidth = 0.045f;
        hoverRing.shadowCastingMode = ShadowCastingMode.Off;
        hoverRing.receiveShadows = false;
        hoverRing.textureMode = LineTextureMode.Stretch;

        for (int i = 0; i < hoverRing.positionCount; i++)
        {
            float angle = (float)i / hoverRing.positionCount * Mathf.PI * 2f;
            hoverRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.48f, 0f, Mathf.Sin(angle) * 0.48f));
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            ringMaterial = new Material(shader)
            {
                name = "AirFooty Hover Ring (Runtime)"
            };
            hoverRing.sharedMaterial = ringMaterial;
        }
    }

    private void BuildShadow()
    {
        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadow.name = "Hover Shadow";
        shadow.transform.SetParent(transform, false);
        shadow.transform.localPosition = Vector3.zero;
        shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shadow.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);

        Collider collider = shadow.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        shadowRenderer = shadow.GetComponent<Renderer>();
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            shadowMaterial = new Material(shader)
            {
                name = "AirFooty Hover Shadow (Runtime)",
                color = new Color(0f, 0.06f, 0.12f, 0.28f)
            };
            shadowRenderer.sharedMaterial = shadowMaterial;
        }
    }
}
