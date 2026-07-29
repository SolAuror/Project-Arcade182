using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Short-lived triple crescent that makes a melee arc legible without an
    /// animator or imported VFX dependency.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeSlashVisual : MonoBehaviour
    {
        private const int SlashCount = 3;
        private const int SegmentCount = 18;

        private static Material sharedMaterial;

        private readonly LineRenderer[] lines = new LineRenderer[SlashCount];
        private Color color;
        private float startTime;
        private float lifeSeconds;

        public static MeleeSlashVisual Spawn(
            Vector3 origin,
            Vector3 forward,
            float range,
            float arcDegrees,
            Color color,
            float lifeSeconds,
            float width)
        {
            GameObject visualObject = new GameObject("Melee Triple Slash");
            visualObject.transform.position = origin;
            visualObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            MeleeSlashVisual visual = visualObject.AddComponent<MeleeSlashVisual>();
            visual.color = color;
            visual.lifeSeconds = Mathf.Max(0.05f, lifeSeconds);
            visual.startTime = Time.time;
            visual.Build(range, arcDegrees, width);
            return visual;
        }

        private void Build(float range, float arcDegrees, float width)
        {
            for (int slashIndex = 0; slashIndex < SlashCount; slashIndex++)
            {
                GameObject lineObject = new GameObject($"Claw Arc {slashIndex + 1}");
                lineObject.transform.SetParent(transform, false);

                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = SegmentCount;
                line.startWidth = width;
                line.endWidth = width * 0.55f;
                line.numCornerVertices = 3;
                line.sharedMaterial = GetMaterial();
                line.startColor = color;
                line.endColor = new Color(1f, 0.55f, 0.08f, color.a);
                lines[slashIndex] = line;

                float radius = range * (0.74f + slashIndex * 0.07f);
                float verticalOffset = (slashIndex - 1) * 0.18f;
                for (int segment = 0; segment < SegmentCount; segment++)
                {
                    float progress = segment / (SegmentCount - 1f);
                    float angle = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, progress) * Mathf.Deg2Rad;
                    float diagonal = (progress - 0.5f) * 0.18f;
                    line.SetPosition(
                        segment,
                        new Vector3(
                            Mathf.Sin(angle) * radius,
                            verticalOffset + diagonal,
                            Mathf.Cos(angle) * radius));
                }
            }
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - startTime) / lifeSeconds);
            float eased = 1f - (1f - progress) * (1f - progress);
            transform.localScale = Vector3.one * Mathf.Lerp(0.68f, 1.06f, eased);
            transform.Rotate(0f, Time.deltaTime * 65f, 0f, Space.Self);

            float alpha = 1f - eased;
            foreach (LineRenderer line in lines)
            {
                line.startColor = new Color(color.r, color.g, color.b, color.a * alpha);
                line.endColor = new Color(1f, 0.55f, 0.08f, color.a * alpha);
            }

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private static Material GetMaterial()
        {
            if (sharedMaterial != null)
            {
                return sharedMaterial;
            }

            sharedMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Melee Slash (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedMaterial;
        }
    }
}
