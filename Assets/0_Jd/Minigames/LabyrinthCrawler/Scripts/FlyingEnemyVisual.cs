using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Lightweight prototype silhouette for flying enemies. It adds bright
    /// articulated wings to the existing primitive enemy model so the new
    /// movement archetype is readable without requiring imported art.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlyingEnemyVisual : MonoBehaviour
    {
        private static Material sharedWingMaterial;

        private Transform visual;
        private Transform leftWing;
        private Transform rightWing;
        private Vector3 visualRestPosition;
        private float phase;

        private void Awake()
        {
            visual = transform.Find("Visual");
            if (visual == null)
            {
                visual = transform;
            }

            visualRestPosition = visual.localPosition;
            phase = Random.Range(0f, Mathf.PI * 2f);
            leftWing = ResolveOrCreateWing("Left Flight Wing", -1f);
            rightWing = ResolveOrCreateWing("Right Flight Wing", 1f);
        }

        private void Update()
        {
            float flap = Mathf.Sin(Time.time * 10f + phase);
            leftWing.localRotation = Quaternion.Euler(8f, -18f, 18f + flap * 32f);
            rightWing.localRotation = Quaternion.Euler(8f, 18f, -18f - flap * 32f);
            visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(Time.time * 3.4f + phase) * 0.08f);
        }

        private Transform ResolveOrCreateWing(string wingName, float side)
        {
            Transform authoredWing = visual.Find(wingName);
            if (authoredWing != null)
            {
                return authoredWing;
            }

            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = wingName;
            wing.transform.SetParent(visual, false);
            wing.transform.localPosition = new Vector3(side * 0.72f, 0.48f, -0.05f);
            wing.transform.localScale = new Vector3(0.82f, 0.055f, 0.34f);

            if (wing.TryGetComponent(out Collider wingCollider))
            {
                Destroy(wingCollider);
            }

            Renderer renderer = wing.GetComponent<Renderer>();
            renderer.sharedMaterial = GetWingMaterial();
            return wing.transform;
        }

        private static Material GetWingMaterial()
        {
            if (sharedWingMaterial != null)
            {
                return sharedWingMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            sharedWingMaterial = new Material(shader)
            {
                name = "Flying Enemy Wings (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(0.15f, 0.95f, 1f, 1f)
            };

            if (sharedWingMaterial.HasProperty("_EmissionColor"))
            {
                sharedWingMaterial.EnableKeyword("_EMISSION");
                sharedWingMaterial.SetColor("_EmissionColor", new Color(0.04f, 0.7f, 0.85f, 1f));
            }

            return sharedWingMaterial;
        }
    }
}
