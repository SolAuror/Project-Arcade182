using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Close-range arc attack with an authored-looking triple slash. Unlike a
    /// hitscan beam, the damage volume and visual both read as a melee swing.
    /// </summary>
    [CreateAssetMenu(fileName = "Spell_Melee", menuName = "Sol/Spells/Melee Spell")]
    public sealed class MeleeSpellDefinition : SpellDefinition
    {
        [Header("Melee")]
        [SerializeField, Min(0.5f)] private float range = 2.5f;
        [SerializeField, Range(10f, 180f)] private float arcDegrees = 105f;

        [Header("Slash Visual")]
        [SerializeField] private Color slashColor = new Color(1f, 0.18f, 0.035f, 1f);
        [SerializeField, Min(0.02f)] private float slashLifeSeconds = 0.22f;
        [SerializeField, Min(0.01f)] private float slashWidth = 0.11f;
        [SerializeField] private float slashVerticalOffset = -0.28f;

        public float Range => range;
        public float ArcDegrees => arcDegrees;

        public override void Cast(in SpellCastContext context)
        {
            Vector3 forward = context.AimRay.direction;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f
                ? forward.normalized
                : context.Caster != null ? context.Caster.forward : Vector3.forward;

            Vector3 center = context.Caster != null ? context.Caster.position : context.Origin;
            float effectiveRange = range + Mathf.Max(0f, context.RadiusBonus);
            Collider[] overlaps = Physics.OverlapSphere(
                center,
                effectiveRange,
                context.HitMask,
                QueryTriggerInteraction.Ignore);

            HashSet<Health> damaged = new HashSet<Health>();
            bool hitAnything = false;
            foreach (Collider overlap in overlaps)
            {
                if (IsSelfHit(context, overlap))
                {
                    continue;
                }

                Health target = FindHealth(overlap);
                if (target == null ||
                    target.Faction == context.Faction ||
                    damaged.Contains(target))
                {
                    continue;
                }

                Vector3 toTarget = target.transform.position - center;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > effectiveRange * effectiveRange ||
                    Vector3.Angle(forward, toTarget) > arcDegrees * 0.5f ||
                    !HasClearPath(context, center, target.transform.position))
                {
                    continue;
                }

                damaged.Add(target);
                target.TakeDamage(GetDamage(context), context.Faction);
                SpellBurstVisual.Spawn(
                    target.transform.position + Vector3.up * 0.8f,
                    0.42f,
                    slashColor,
                    0.12f);
                hitAnything = true;
            }

            MeleeSlashVisual.Spawn(
                context.Origin + Vector3.up * slashVerticalOffset,
                forward,
                effectiveRange,
                arcDegrees,
                slashColor,
                slashLifeSeconds,
                slashWidth);

            PlayCastSound(context);
            if (hitAnything)
            {
                PlayHitSound(center + forward * effectiveRange * 0.7f);
            }
        }

        private static bool HasClearPath(
            in SpellCastContext context,
            Vector3 casterPosition,
            Vector3 targetPosition)
        {
            Vector3 origin = casterPosition + Vector3.up * 0.8f;
            Vector3 destination = targetPosition + Vector3.up * 0.8f;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;
            if (distance <= 0.01f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction / distance,
                distance,
                context.HitMask,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (IsSelfHit(context, hit.collider) ||
                    hit.collider.GetComponentInParent<Health>() != null ||
                    hit.collider.GetComponentInParent<Projectile>() != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
