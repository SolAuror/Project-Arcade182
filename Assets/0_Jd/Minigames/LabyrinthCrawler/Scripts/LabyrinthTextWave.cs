using UnityEngine;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Lightweight per-glyph sine wave authored on the floor-banner Text.
    /// Kept in a matching script file so Unity can persist it as a component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabyrinthTextWave : BaseMeshEffect
    {
        private float amplitude = 0.65f;
        private float frequency = 0.12f;
        private float speed = 2f;

        public void Configure(float newAmplitude, float newFrequency, float newSpeed)
        {
            amplitude = Mathf.Max(0f, newAmplitude);
            frequency = Mathf.Max(0f, newFrequency);
            speed = Mathf.Max(0f, newSpeed);
            graphic?.SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (graphic != null && graphic.enabled && amplitude > 0f)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper.currentVertCount == 0 || amplitude <= 0f)
            {
                return;
            }

            UIVertex vertex = default;
            float phase = Time.unscaledTime * speed;
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                vertex.position.y += Mathf.Sin(phase + vertex.position.x * frequency) * amplitude;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }
    }
}
