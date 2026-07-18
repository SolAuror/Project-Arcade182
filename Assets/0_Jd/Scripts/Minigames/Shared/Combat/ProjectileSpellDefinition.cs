using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Spawns a <see cref="Projectile"/> along the aim ray. Uses the assigned
    /// prefab, or builds a simple sphere at runtime when none is set.
    /// </summary>
    [CreateAssetMenu(fileName = "Spell_Projectile", menuName = "Sol/Spells/Projectile Spell")]
    public class ProjectileSpellDefinition : SpellDefinition
    {
        [Header("Projectile")]
        [Tooltip("Prefab with a Projectile component. Optional: a plain sphere is built when empty.")]
        [SerializeField] private Projectile projectilePrefab;

        [Tooltip("Launch speed in units/second.")]
        [SerializeField, Min(0.1f)] private float speed = 18f;

        [Tooltip("Seconds before the projectile despawns.")]
        [SerializeField, Min(0.1f)] private float lifeSeconds = 5f;

        [Tooltip("Spawn distance in front of the muzzle, keeping it clear of the caster.")]
        [SerializeField, Min(0f)] private float spawnOffset = 0.8f;

        [Header("Fallback Visual")]
        [Tooltip("Diameter of the runtime-built sphere when no prefab is assigned.")]
        [SerializeField, Min(0.05f)] private float fallbackScale = 0.3f;

        [SerializeField] private Color fallbackColor = new Color(1f, 0.55f, 0.1f, 1f);

        public float Speed => speed;

        public override void Cast(in SpellCastContext context)
        {
            Vector3 direction = context.AimRay.direction.normalized;
            Vector3 spawnPosition = context.Origin + direction * spawnOffset;

            Projectile projectile = projectilePrefab != null
                ? Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction))
                : BuildFallbackProjectile(spawnPosition);

            projectile.SetImpactSound(HitClip, SfxVolume);
            projectile.Launch(context.Faction, GetDamage(context), direction * speed, context.Caster, lifeSeconds);
            PlayCastSound(context);
        }

        private Projectile BuildFallbackProjectile(Vector3 position)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"{DisplayName} Projectile";
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * fallbackScale;

            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            if (sphereRenderer != null)
            {
                sphereRenderer.material.color = fallbackColor;
            }

            sphere.AddComponent<Rigidbody>();
            return sphere.AddComponent<Projectile>();
        }
    }
}
