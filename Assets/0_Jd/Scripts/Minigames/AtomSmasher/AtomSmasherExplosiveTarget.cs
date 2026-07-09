using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Companion for an atom that detonates when smashed: every atom in the
    /// blast radius is chain-hit (scoring with the ball's chain multiplier),
    /// and the player's ball is destroyed in the blast.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AtomSmasherTarget))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Explosive Target")]
    public class AtomSmasherExplosiveTarget : MonoBehaviour
    {
        [Header("Blast")]
        [SerializeField, Min(0.5f)] private float blastRadius = 2.3f;

        [Tooltip("The ball that triggered the blast is consumed by it.")]
        [SerializeField] private bool destroyTriggeringBall = true;

        [Header("Feedback")]
        [Tooltip("Optional VFX spawned at the blast center.")]
        [SerializeField] private GameObject blastVfxPrefab;

        [SerializeField, Min(0.1f)] private float blastVfxScale = 2.5f;
        [SerializeField, Min(0.1f)] private float blastVfxLifeSeconds = 2f;

        private bool detonated;

        public void Detonate(AtomSmasherBall ball)
        {
            if (detonated)
            {
                return;
            }

            detonated = true;

            if (blastVfxPrefab != null)
            {
                GameObject vfx = Instantiate(blastVfxPrefab, transform.position, Quaternion.identity);
                vfx.transform.localScale *= blastVfxScale;
                Destroy(vfx, blastVfxLifeSeconds);
            }

            // Chain-hit everything in range; nested explosives cascade through
            // their own Detonate calls (each guarded by its own flag).
            Collider[] overlaps = Physics.OverlapSphere(transform.position, blastRadius, ~0, QueryTriggerInteraction.Ignore);
            HashSet<AtomSmasherTarget> chained = new HashSet<AtomSmasherTarget>();

            foreach (Collider overlap in overlaps)
            {
                AtomSmasherTarget target = overlap.GetComponentInParent<AtomSmasherTarget>();
                if (target == null || target.gameObject == gameObject || target.HasBeenHit || !chained.Add(target))
                {
                    continue;
                }

                target.TryHit(ball);
            }

            if (destroyTriggeringBall && ball != null)
            {
                ball.ForceFinish();
            }
        }
    }
}
