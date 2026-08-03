using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Everything a <see cref="SpellDefinition"/> needs to resolve one cast.
    /// Built by the caller (player input or enemy AI); <see cref="SpellCaster"/>
    /// fills in the per-slot runtime values before invoking the definition.
    /// </summary>
    public struct SpellCastContext
    {
        /// <summary>Root transform of the caster; used to ignore self-hits.</summary>
        public Transform Caster;

        /// <summary>Faction damage is attributed to.</summary>
        public Faction Faction;

        /// <summary>Aim ray. Player: camera center; enemy: eye toward target.</summary>
        public Ray AimRay;

        /// <summary>Optional spawn origin for projectiles/beams. Falls back to AimRay origin.</summary>
        public Transform Muzzle;

        /// <summary>Layers spells may hit.</summary>
        public LayerMask HitMask;

        /// <summary>Current spell level (1 = base). Filled by SpellCaster.</summary>
        public int Level;

        /// <summary>Damage multiplier from upgrades (1 = base). Filled by SpellCaster.</summary>
        public float DamageMultiplier;

        /// <summary>Flat radius bonus from upgrades. Filled by SpellCaster.</summary>
        public float RadiusBonus;

        public Vector3 Origin => Muzzle != null ? Muzzle.position : AimRay.origin;
    }
}
