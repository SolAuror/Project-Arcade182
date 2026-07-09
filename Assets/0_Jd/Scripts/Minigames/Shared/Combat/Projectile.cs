using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Reusable rigidbody projectile fired by <see cref="ProjectileSpellDefinition"/>.
    /// Applies damage to the first opposite-faction <see cref="Health"/> it touches,
    /// spawns optional hit VFX, and despawns. Used by players and enemies alike.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Shared/Projectile")]
    public class Projectile : MonoBehaviour
    {
        [Header("Projectile")]
        [Tooltip("Faction damage is attributed to.")]
        [SerializeField] private Faction owner = Faction.Neutral;

        [Tooltip("Damage applied on impact.")]
        [SerializeField, Min(0f)] private float damage = 10f;

        [Tooltip("Seconds before the projectile despawns on its own.")]
        [SerializeField, Min(0.1f)] private float lifeSeconds = 5f;

        [Header("Feedback")]
        [Tooltip("Optional VFX spawned at the impact point.")]
        [SerializeField] private GameObject hitVfxPrefab;

        [Tooltip("Seconds before spawned hit VFX is destroyed.")]
        [SerializeField, Min(0.1f)] private float hitVfxLifeSeconds = 2f;

        private Rigidbody rb;
        private float despawnTime;
        private bool consumed;

        public Faction Owner => owner;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            despawnTime = Time.time + lifeSeconds;
        }

        private void Update()
        {
            if (Time.time >= despawnTime)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>Arms and fires the projectile. Collisions with the owner hierarchy are ignored.</summary>
        public void Launch(Faction ownerFaction, float damageAmount, Vector3 velocity, Transform ownerRoot, float lifetime = 5f)
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            owner = ownerFaction;
            damage = Mathf.Max(0f, damageAmount);
            lifeSeconds = Mathf.Max(0.1f, lifetime);
            despawnTime = Time.time + lifeSeconds;
            consumed = false;

            ConfigureRigidbody();
            IgnoreOwnerColliders(ownerRoot);

            rb.linearVelocity = velocity;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (consumed)
            {
                return;
            }

            // Friendly projectiles pass through each other (a fireball must
            // never eat another of the caster's shots). Opposing projectiles
            // still collide, so shots can intercept enemy bolts.
            Projectile otherProjectile = collision.collider.GetComponentInParent<Projectile>();
            if (otherProjectile != null && otherProjectile.Owner == owner)
            {
                IgnoreCollisionsWith(collision.collider.transform.root);
                return;
            }

            consumed = true;

            Health health = collision.collider.GetComponentInParent<Health>();
            if (health != null && health.Faction != owner)
            {
                health.TakeDamage(damage, owner);
            }

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            SpawnHitVfx(point);
            Destroy(gameObject);
        }

        /// <summary>Destroys the projectile mid-air as if it hit something (shot down by a beam).</summary>
        public void Detonate()
        {
            if (consumed)
            {
                return;
            }

            consumed = true;
            SpawnHitVfx(transform.position);
            Destroy(gameObject);
        }

        /// <summary>
        /// Bats the projectile back (pulse reflect): it now belongs to the
        /// reflector's faction and flies away from the reflection point.
        /// </summary>
        public void Reflect(Faction newOwner, Vector3 awayFrom, Transform newOwnerRoot)
        {
            if (consumed || rb == null)
            {
                return;
            }

            owner = newOwner;

            Vector3 direction = transform.position - awayFrom;
            direction.y *= 0.25f; // mostly level so it can find enemies
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = -rb.linearVelocity;
            }

            float speed = Mathf.Max(rb.linearVelocity.magnitude, 8f);
            rb.linearVelocity = direction.normalized * speed;
            transform.rotation = Quaternion.LookRotation(direction.normalized);
            despawnTime = Time.time + lifeSeconds; // fresh life for the return trip
            IgnoreOwnerColliders(newOwnerRoot);
        }

        private void SpawnHitVfx(Vector3 point)
        {
            if (hitVfxPrefab == null)
            {
                return;
            }

            GameObject vfx = Instantiate(hitVfxPrefab, point, Quaternion.identity);
            Destroy(vfx, hitVfxLifeSeconds);
        }

        private void IgnoreCollisionsWith(Transform otherRoot)
        {
            if (otherRoot == null)
            {
                return;
            }

            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                foreach (Collider other in otherRoot.GetComponentsInChildren<Collider>())
                {
                    if (own != null && other != null)
                    {
                        Physics.IgnoreCollision(own, other);
                    }
                }
            }
        }

        private void ConfigureRigidbody()
        {
            if (rb == null)
            {
                return;
            }

            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void IgnoreOwnerColliders(Transform ownerRoot)
        {
            if (ownerRoot == null)
            {
                return;
            }

            Collider[] ownColliders = GetComponentsInChildren<Collider>();
            Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>();
            foreach (Collider own in ownColliders)
            {
                foreach (Collider ownerCollider in ownerColliders)
                {
                    if (own != null && ownerCollider != null)
                    {
                        Physics.IgnoreCollision(own, ownerCollider);
                    }
                }
            }
        }
    }
}
