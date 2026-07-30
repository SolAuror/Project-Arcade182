using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class AirFootyArenaPresentation : MonoBehaviour
{
    [SerializeField] private Color pitchLineColor = new Color(0.18f, 0.9f, 1f, 0.8f);
    [SerializeField] private Color playerColor = new Color(0.1f, 0.55f, 1f, 0.85f);
    [SerializeField] private Color aiColor = new Color(1f, 0.18f, 0.25f, 0.85f);

    private Material lineMaterial;

    private void Awake()
    {
        if (transform.Find("AirFooty Pitch Markings") != null)
        {
            return;
        }

        BuildPitchMarkings();
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }

    private void BuildPitchMarkings()
    {
        GameObject markings = new GameObject("AirFooty Pitch Markings");
        markings.transform.SetParent(transform, false);

        lineMaterial = CreateLineMaterial();
        CreateLine(
            "Halfway Line",
            markings.transform,
            new[]
            {
                new Vector3(0f, 0.035f, -3.75f),
                new Vector3(0f, 0.035f, 3.75f)
            },
            pitchLineColor,
            0.055f,
            false);

        CreateCircle(
            "Centre Circle",
            markings.transform,
            Vector3.up * 0.037f,
            1.15f,
            pitchLineColor,
            0.05f);

        CreateLine(
            "Player Goal Accent",
            markings.transform,
            new[]
            {
                new Vector3(-8.05f, 0.05f, -1.45f),
                new Vector3(-8.05f, 0.05f, 1.45f)
            },
            playerColor,
            0.12f,
            false);

        CreateLine(
            "AI Goal Accent",
            markings.transform,
            new[]
            {
                new Vector3(8.05f, 0.05f, -1.45f),
                new Vector3(8.05f, 0.05f, 1.45f)
            },
            aiColor,
            0.12f,
            false);
    }

    private void CreateCircle(
        string objectName,
        Transform parent,
        Vector3 center,
        float radius,
        Color color,
        float width)
    {
        const int segments = 64;
        Vector3[] positions = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            positions[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        CreateLine(objectName, parent, positions, color, width, true);
    }

    private void CreateLine(
        string objectName,
        Transform parent,
        Vector3[] positions,
        Color color,
        float width,
        bool loop)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.loop = loop;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sharedMaterial = lineMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        return shader != null
            ? new Material(shader) { name = "AirFooty Pitch Lines (Runtime)" }
            : null;
    }
}
