using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Spawns an authored <see cref="Projectile"/> prefab along the aim ray.
    /// </summary>
    [CreateAssetMenu(fileName = "Spell_Projectile", menuName = "Sol/Spells/Projectile Spell")]
    public class ProjectileSpellDefinition : SpellDefinition
    {
        [Header("Projectile")]
        [Tooltip("Required authored prefab with a Projectile component.")]
        [SerializeField] private Projectile projectilePrefab;

        [Tooltip("Launch speed in units/second.")]
        [SerializeField, Min(0.1f)] private float speed = 18f;

        [Tooltip("Seconds before the projectile despawns.")]
        [SerializeField, Min(0.1f)] private float lifeSeconds = 5f;

        [Tooltip("Spawn distance in front of the muzzle, keeping it clear of the caster.")]
        [SerializeField, Min(0f)] private float spawnOffset = 0.8f;

        public float Speed => speed;

        public override void Cast(in SpellCastContext context)
        {
            Vector3 direction = context.AimRay.direction.normalized;
            Vector3 spawnPosition = context.Origin + direction * spawnOffset;

            if (projectilePrefab == null)
            {
                Debug.LogError(
                    $"Projectile spell '{name}' requires an authored Projectile prefab.",
                    this);
                return;
            }

            Projectile projectile = Instantiate(
                projectilePrefab,
                spawnPosition,
                Quaternion.LookRotation(direction));

            projectile.SetImpactSound(HitClip, SfxVolume);
            projectile.Launch(context.Faction, GetDamage(context), direction * speed, context.Caster, lifeSeconds);
            PlayCastSound(context);
        }

    }
}
