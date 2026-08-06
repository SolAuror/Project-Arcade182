using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Animates the authored flight wings on the flying-enemy prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlyingEnemyVisual : MonoBehaviour
    {
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
            leftWing = visual.Find("Left Flight Wing");
            rightWing = visual.Find("Right Flight Wing");
            if (leftWing == null || rightWing == null)
            {
                Debug.LogError(
                    $"{name} requires authored Left Flight Wing and Right Flight Wing children. " +
                    "Check the authored flying-enemy prefab.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            float flap = Mathf.Sin(Time.time * 10f + phase);
            leftWing.localRotation = Quaternion.Euler(8f, -18f, 18f + flap * 32f);
            rightWing.localRotation = Quaternion.Euler(8f, 18f, -18f - flap * 32f);
            visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(Time.time * 3.4f + phase) * 0.08f);
        }

    }
}
