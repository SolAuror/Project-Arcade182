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

        [Header("Audio")]
        [Tooltip("One-shot played at the muzzle when this spell is cast.")]
        [SerializeField] private AudioClip castClip;

        [Tooltip("One-shot played at the impact point when this spell lands.")]
        [SerializeField] private AudioClip hitClip;

        [Tooltip("Volume for the cast/hit one-shots.")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public float BaseDamage => baseDamage;
        public float ManaCost => manaCost;
        public float CooldownSeconds => cooldownSeconds;
        public bool ContinuousWhileHeld => continuousWhileHeld;
        public AudioClip HitClip => hitClip;
        public float SfxVolume => sfxVolume;

        /// <summary>Resolve one cast. Implementations must not mutate this asset.</summary>
        public abstract void Cast(in SpellCastContext context);

        /// <summary>Plays the cast one-shot at the muzzle/origin. No-op when unassigned.</summary>
        protected void PlayCastSound(in SpellCastContext context)
        {
            PlaySound(castClip, context.Origin);
        }

        /// <summary>Plays the hit one-shot at a world position. No-op when unassigned.</summary>
        protected void PlayHitSound(Vector3 position)
        {
            PlaySound(hitClip, position);
        }

        // Spatial one-shot that outlives the (often just-destroyed) caster/projectile.
        private void PlaySound(AudioClip clip, Vector3 position)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, sfxVolume);
            }
        }

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
