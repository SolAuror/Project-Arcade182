using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Field window that charges balls passing through it: neutral balls
    /// polarize positive, charged balls flip sign. The charge decides how
    /// polarized atoms and blockers bend the ball (attract or repel), so
    /// gates are the deliberate lever for setting up matched approaches —
    /// polarized objects themselves never flip the ball.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Polarizer Gate")]
    public class AtomSmasherPolarizerGate : MonoBehaviour
    {
        [Tooltip("Seconds a ball is ignored after a flip so one pass is exactly one flip.")]
        [SerializeField, Min(0f)] private float flipCooldownSeconds = 0.5f;

        [Header("Look")]
        [SerializeField] private Renderer positivePost;
        [SerializeField] private Renderer negativePost;
        [SerializeField] private Color positiveColor = new Color(1f, 0.32f, 0.25f, 1f);
        [SerializeField] private Color negativeColor = new Color(0.3f, 0.55f, 1f, 1f);

        [Tooltip("Optional pane spun slowly for a live-field look.")]
        [SerializeField] private Transform fieldPane;

        [SerializeField] private float paneDegreesPerSecond = 90f;

        private readonly Dictionary<AtomSmasherBall, float> recentFlips = new Dictionary<AtomSmasherBall, float>();

        private void Awake()
        {
            TintPost(positivePost, positiveColor);
            TintPost(negativePost, negativeColor);

            Collider gateCollider = GetComponent<Collider>();
            if (gateCollider != null)
            {
                gateCollider.isTrigger = true;
            }
        }

        private void Update()
        {
            if (fieldPane != null)
            {
                fieldPane.Rotate(0f, paneDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            AtomSmasherBall ball = other.GetComponentInParent<AtomSmasherBall>();
            if (ball == null)
            {
                return;
            }

            if (recentFlips.TryGetValue(ball, out float lastFlip) && Time.time - lastFlip < flipCooldownSeconds)
            {
                return;
            }

            recentFlips[ball] = Time.time;
            ball.SetPolarity(ball.Charge == AtomSmasherBall.Polarity.Positive
                ? AtomSmasherBall.Polarity.Negative
                : AtomSmasherBall.Polarity.Positive);
        }

        private static void TintPost(Renderer post, Color color)
        {
            if (post == null)
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            post.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            post.SetPropertyBlock(block);
        }
    }
}
