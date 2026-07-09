using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Authored spell asset shared by players and enemies in any minigame.
    /// Subclasses implement <see cref="Cast"/>; new spell variants are new
    /// assets (or a small new subclass) — casters never change.
    /// </summary>
    public abstract class SpellDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Name shown in HUDs and upgrade cards.")]
        [SerializeField] private string displayName = "Spell";

        [Tooltip("Optional HUD icon.")]
        [SerializeField] private Sprite icon;

        [Header("Stats")]
        [Tooltip("Damage before upgrade multipliers.")]
        [SerializeField, Min(0f)] private float baseDamage = 10f;

        [Tooltip("Mana spent per cast. Casters without a Mana component cast for free.")]
        [SerializeField, Min(0f)] private float manaCost = 10f;

        [Tooltip("Seconds between casts before upgrade multipliers.")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.5f;

        [Tooltip("While the cast input is held, keep re-casting every cooldown tick (sustained beams/streams).")]
        [SerializeField] private bool continuousWhileHeld;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public float BaseDamage => baseDamage;
        public float ManaCost => manaCost;
        public float CooldownSeconds => cooldownSeconds;
        public bool ContinuousWhileHeld => continuousWhileHeld;

        /// <summary>Resolve one cast. Implementations must not mutate this asset.</summary>
        public abstract void Cast(in SpellCastContext context);

        protected float GetDamage(in SpellCastContext context)
        {
            float multiplier = context.DamageMultiplier > 0f ? context.DamageMultiplier : 1f;
            return baseDamage * multiplier;
        }

        protected static bool IsSelfHit(in SpellCastContext context, Component hit)
        {
            return context.Caster != null &&
                   hit != null &&
                   (hit.transform == context.Caster || hit.transform.IsChildOf(context.Caster));
        }

        protected static Health FindHealth(Component hit)
        {
            return hit != null ? hit.GetComponentInParent<Health>() : null;
        }
    }
}
