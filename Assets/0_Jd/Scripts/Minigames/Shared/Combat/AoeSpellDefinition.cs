using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Burst spell: damages every opposite-faction <see cref="Health"/> within a
    /// sphere around the caster. Radius grows with upgrade bonuses.
    /// </summary>
    [CreateAssetMenu(fileName = "Spell_Aoe", menuName = "Sol/Spells/Area Spell")]
    public class AoeSpellDefinition : SpellDefinition
    {
        [Header("Area")]
        [Tooltip("Blast radius before upgrade bonuses.")]
        [SerializeField, Min(0.5f)] private float baseRadius = 5f;

        [Header("Burst Visual")]
        [SerializeField] private Color burstColor = new Color(0.55f, 0.35f, 1f, 1f);
        [SerializeField, Min(0.02f)] private float burstLifeSeconds = 0.2f;

        public float BaseRadius => baseRadius;

        public float GetRadius(in SpellCastContext context)
        {
            return baseRadius + Mathf.Max(0f, context.RadiusBonus);
        }

        public override void Cast(in SpellCastContext context)
        {
            Vector3 center = context.Caster != null ? context.Caster.position : context.Origin;
            float radius = GetRadius(context);

            Collider[] overlaps = Physics.OverlapSphere(center, radius, context.HitMask, QueryTriggerInteraction.Ignore);
            HashSet<Health> damaged = new HashSet<Health>();

            foreach (Collider overlap in overlaps)
            {
                if (IsSelfHit(context, overlap))
                {
                    continue;
                }

                Health health = FindHealth(overlap);
                if (health != null && health.Faction != context.Faction && damaged.Add(health))
                {
                    health.TakeDamage(GetDamage(context), context.Faction);
                }
            }

            SpawnBurst(center, radius);
        }

        private void SpawnBurst(Vector3 center, float radius)
        {
            GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = $"{DisplayName} Burst";
            burst.transform.position = center;
            burst.transform.localScale = Vector3.one * (radius * 2f);

            Collider burstCollider = burst.GetComponent<Collider>();
            if (burstCollider != null)
            {
                Destroy(burstCollider);
            }

            Renderer burstRenderer = burst.GetComponent<Renderer>();
            if (burstRenderer != null)
            {
                burstRenderer.material.color = burstColor;
            }

            Destroy(burst, burstLifeSeconds);
        }
    }
}
