using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Short-lived triple crescent that makes a melee arc legible without an
    /// animator or imported VFX dependency.
    ///
    /// The arcs are the authored Resources/VFX/MeleeSlashVisual.prefab — three
    /// LineRenderers and their material are assets, not built at runtime. Only
    /// the curve itself is computed per cast, because its radius and sweep come
    /// from the casting spell's range and arc.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Melee Slash Visual")]
    public sealed class MeleeSlashVisual : MonoBehaviour
    {
        private const string PrefabResourcePath = "VFX/MeleeSlashVisual";
        private const int SegmentCount = 18;

        private static MeleeSlashVisual cachedPrefab;

        [Tooltip("Authored claw arcs, innermost first. Their points are " +
                 "rewritten per cast from the spell's range and arc.")]
        [SerializeField] private LineRenderer[] lines;

        [Tooltip("Colour the arcs fade towards at their trailing edge.")]
        [SerializeField] private Color trailColor = new Color(1f, 0.55f, 0.08f, 1f);

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
            if (cachedPrefab == null)
            {
                cachedPrefab = Resources.Load<MeleeSlashVisual>(PrefabResourcePath);
                if (cachedPrefab == null)
                {
                    Debug.LogWarning(
                        $"MeleeSlashVisual prefab missing from a Resources folder " +
                        $"('{PrefabResourcePath}'); slash skipped. The authored " +
                        "copy is Assets/0_Jd/Resources/VFX/MeleeSlashVisual.prefab.");
                    return null;
                }
            }

            MeleeSlashVisual visual = Instantiate(cachedPrefab);
            visual.transform.SetPositionAndRotation(
                origin,
                Quaternion.LookRotation(forward, Vector3.up));
            visual.color = color;
            visual.lifeSeconds = Mathf.Max(0.05f, lifeSeconds);
            visual.startTime = Time.time;
            visual.ApplyGeometry(range, arcDegrees, width);
            return visual;
        }

        private void ApplyGeometry(float range, float arcDegrees, float width)
        {
            if (lines == null)
            {
                return;
            }

            for (int slashIndex = 0; slashIndex < lines.Length; slashIndex++)
            {
                LineRenderer line = lines[slashIndex];
                if (line == null)
                {
                    continue;
                }

                line.positionCount = SegmentCount;
                line.startWidth = width;
                line.endWidth = width * 0.55f;
                line.startColor = color;
                line.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, color.a);

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
            if (lines != null)
            {
                foreach (LineRenderer line in lines)
                {
                    if (line == null)
                    {
                        continue;
                    }

                    line.startColor = new Color(color.r, color.g, color.b, color.a * alpha);
                    line.endColor = new Color(
                        trailColor.r,
                        trailColor.g,
                        trailColor.b,
                        color.a * alpha);
                }
            }

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
