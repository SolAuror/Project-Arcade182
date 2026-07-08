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

            consumed = true;

            Health health = collision.collider.GetComponentInParent<Health>();
            if (health != null && health.Faction != owner)
            {
                health.TakeDamage(damage, owner);
            }

            if (hitVfxPrefab != null)
            {
                Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                GameObject vfx = Instantiate(hitVfxPrefab, point, Quaternion.identity);
                Destroy(vfx, hitVfxLifeSeconds);
            }

            Destroy(gameObject);
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
