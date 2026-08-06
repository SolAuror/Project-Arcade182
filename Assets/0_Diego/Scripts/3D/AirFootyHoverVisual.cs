using UnityEngine;
using UnityEngine.Rendering;

public sealed class AirFootyHoverVisual : MonoBehaviour
{
    private Transform target;
    private LineRenderer hoverRing;
    private Renderer shadowRenderer;
    private Color ringColor;
    private float ballHeight;

    public void Initialize(Transform followTarget, float height, Color color)
    {
        target = followTarget;
        ballHeight = height;
        ringColor = color;
        name = "AirFooty Ball Hover";

        ResolveAuthoredParts();
        if (shadowRenderer == null || hoverRing == null)
        {
            Debug.LogError(
                "AirFooty Ball Hover is missing its authored Hover Ring or Hover Shadow child.",
                this);
        }
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

    private void ResolveAuthoredParts()
    {
        Transform ring = transform.Find("Hover Ring");
        hoverRing = ring != null ? ring.GetComponent<LineRenderer>() : null;

        Transform shadow = transform.Find("Hover Shadow");
        shadowRenderer = shadow != null ? shadow.GetComponent<Renderer>() : null;
    }

}
