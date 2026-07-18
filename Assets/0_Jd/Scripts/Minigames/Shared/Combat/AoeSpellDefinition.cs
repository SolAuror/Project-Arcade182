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

        [Header("Knockback")]
        [Tooltip("Impulse pushing struck targets away from the caster (crowd control).")]
        [SerializeField, Min(0f)] private float knockbackForce = 9f;

        [Tooltip("Upward tilt mixed into the knockback direction.")]
        [SerializeField, Range(0f, 1f)] private float knockbackUpward = 0.25f;

        [Header("Burst Visual")]
        [SerializeField] private Color burstColor = new Color(0.55f, 0.35f, 1f, 1f);
        [SerializeField, Min(0.02f)] private float burstLifeSeconds = 0.35f;

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
            HashSet<Projectile> reflected = new HashSet<Projectile>();
            HashSet<ISpellImpactReceiver> notified = new HashSet<ISpellImpactReceiver>();

            foreach (Collider overlap in overlaps)
            {
                if (IsSelfHit(context, overlap))
                {
                    continue;
                }

                // Enemy projectiles caught in the pulse are batted back and
                // become the caster's shots.
                Projectile projectile = overlap.GetComponentInParent<Projectile>();
                if (projectile != null)
                {
                    if (projectile.Owner != context.Faction && reflected.Add(projectile))
                    {
                        projectile.Reflect(context.Faction, center, context.Caster);
                    }

                    continue;
                }

                // Reactive surfaces caught in the pulse ripple once each.
                ISpellImpactReceiver receiver = SpellImpactReceiverUtility.Find(overlap);
                if (receiver != null && notified.Add(receiver))
                {
                    Vector3 surfacePoint = overlap.ClosestPoint(center);
                    Vector3 toCenter = center - surfacePoint;
                    Vector3 surfaceNormal = toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : Vector3.up;
                    receiver.OnSpellImpact(surfacePoint, surfaceNormal, context.Faction);
                }

                Health health = FindHealth(overlap);
                if (health != null && health.Faction != context.Faction && damaged.Add(health))
                {
                    health.TakeDamage(GetDamage(context), context.Faction);
                    ApplyKnockback(center, health);
                }
            }

            SpellBurstVisual.Spawn(center, radius, burstColor, burstLifeSeconds);
            PlayCastSound(context);
            if (damaged.Count > 0)
            {
                PlayHitSound(center);
            }
        }

        private void ApplyKnockback(Vector3 center, Health health)
        {
            if (knockbackForce <= 0f)
            {
                return;
            }

            Vector3 away = health.transform.position - center;
            away.y = 0f;
            Vector3 direction = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.forward;
            Vector3 impulse = (direction + Vector3.up * knockbackUpward).normalized * knockbackForce;

            if (health.TryGetComponent(out Rigidbody body) && !body.isKinematic)
            {
                body.AddForce(impulse, ForceMode.VelocityChange);
            }
            else if (health.TryGetComponent(out EnemyController enemy))
            {
                enemy.ApplyKnockback(impulse);
            }
        }
    }
}
