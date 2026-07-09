using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Simple maze enemy: idles until the player is in range with line of sight,
    /// then steers toward them on XZ, stops at attack range, and casts its
    /// <see cref="SpellCaster"/> slot 0 on cooldown. Reuses the shared Health and
    /// SpellCaster; no NavMesh — the maze regenerates every stage, so a
    /// CharacterController plus LOS gating is enough. (Runtime NavMeshSurface
    /// baking is a possible future upgrade.)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(SpellCaster))]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Enemy Controller")]
    public class EnemyController : MonoBehaviour
    {
        [Header("Senses")]
        [Tooltip("Range at which the player can be noticed (needs line of sight).")]
        [SerializeField, Min(1f)] private float detectionRange = 14f;

        [Tooltip("Layers blocking or confirming line of sight (walls + player).")]
        [SerializeField] private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;

        [Tooltip("Eye origin for sight checks and casting. Falls back to Eye Height above the feet.")]
        [SerializeField] private Transform eye;

        [SerializeField, Min(0f)] private float eyeHeight = 1.4f;

        [Header("Chase")]
        [Tooltip("Stops approaching and starts casting inside this range.")]
        [SerializeField, Min(0.5f)] private float attackRange = 8f;

        [SerializeField, Min(0f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0f)] private float turnSpeedDegrees = 360f;

        [Header("Attack")]
        [Tooltip("Layers the enemy's spells may hit. Includes the player layer by default.")]
        [SerializeField] private LayerMask spellHitMask = ~0;

        [Tooltip("Aim at this height above the player's feet.")]
        [SerializeField, Min(0f)] private float targetHeight = 1.2f;

        [Header("Wander")]
        [Tooltip("Aimless patrol speed while the player is not detected.")]
        [SerializeField, Min(0f)] private float wanderSpeed = 1.7f;

        [SerializeField] private Vector2 wanderMoveSecondsRange = new Vector2(1.5f, 3.5f);
        [SerializeField] private Vector2 wanderPauseSecondsRange = new Vector2(0.5f, 2f);

        [Header("Knockback")]
        [Tooltip("How quickly knockback velocity bleeds off (units/sec of deceleration).")]
        [SerializeField, Min(1f)] private float knockbackRecovery = 14f;

        [Header("Death")]
        [Tooltip("Shockwave color when this enemy dies. Alpha 0 disables the burst.")]
        [SerializeField] private Color deathBurstColor = new Color(0.95f, 0.3f, 0.2f, 1f);

        [SerializeField, Min(0.1f)] private float deathBurstRadius = 1.2f;

        private CharacterController characterController;
        private Health health;
        private SpellCaster caster;
        private LabyrinthCrawlerGame game;
        private Transform player;
        private bool reportedDeath;
        private Vector3 wanderDirection;
        private float wanderPhaseEndTime;
        private bool wanderMoving;
        private Vector3 knockbackVelocity;

        public Health Health => health;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            caster = GetComponent<SpellCaster>();
            health.Faction = Faction.Enemy;
            health.OnDied.AddListener(HandleDied);
            health.OnDamaged.AddListener(HandleDamaged);

            if (!TryGetComponent(out HitFlash _))
            {
                gameObject.AddComponent<HitFlash>();
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDied.RemoveListener(HandleDied);
                health.OnDamaged.RemoveListener(HandleDamaged);
            }
        }

        private void HandleDamaged(float amount)
        {
            DamagePopup.Spawn(transform.position + Vector3.up * 1.1f, amount);
        }

        public void Initialize(LabyrinthCrawlerGame owningGame)
        {
            game = owningGame;
        }

        private void Update()
        {
            if (health.IsDead)
            {
                return;
            }

            if (game != null && (!game.IsRunning || game.IsChoosingUpgrade))
            {
                return;
            }

            // Knockback overrides everything until it bleeds off (crowd control).
            if (knockbackVelocity.sqrMagnitude > 0.04f)
            {
                characterController.Move((knockbackVelocity + Physics.gravity * 0.5f) * Time.deltaTime);
                knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackRecovery * Time.deltaTime);
                return;
            }

            if (!TryGetPlayer(out Transform target))
            {
                Wander();
                return;
            }

            Vector3 eyePosition = EyePosition;
            Vector3 targetPoint = target.position + Vector3.up * targetHeight;
            Vector3 toTarget = targetPoint - eyePosition;
            float distance = toTarget.magnitude;

            if (distance > detectionRange || !HasLineOfSight(eyePosition, targetPoint, target))
            {
                Wander();
                return;
            }

            FaceToward(target.position);

            if (distance > attackRange)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    characterController.SimpleMove(direction.normalized * moveSpeed);
                }
            }
            else
            {
                characterController.SimpleMove(Vector3.zero); // keep gravity applied

                SpellCastContext castContext = new SpellCastContext
                {
                    Caster = transform,
                    Faction = Faction.Enemy,
                    AimRay = new Ray(eyePosition, toTarget.normalized),
                    Muzzle = eye != null ? eye : null,
                    HitMask = spellHitMask
                };

                caster.TryCast(0, castContext);
            }
        }

        /// <summary>Shoves the enemy (pulse crowd control); movement resumes once it decays.</summary>
        public void ApplyKnockback(Vector3 impulse)
        {
            knockbackVelocity = impulse;
        }

        private void Wander()
        {
            if (Time.time >= wanderPhaseEndTime)
            {
                wanderMoving = !wanderMoving;
                if (wanderMoving)
                {
                    wanderDirection = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    wanderPhaseEndTime = Time.time + Random.Range(wanderMoveSecondsRange.x, wanderMoveSecondsRange.y);
                }
                else
                {
                    wanderPhaseEndTime = Time.time + Random.Range(wanderPauseSecondsRange.x, wanderPauseSecondsRange.y);
                }
            }

            if (!wanderMoving || wanderSpeed <= 0f)
            {
                characterController.SimpleMove(Vector3.zero); // keep gravity applied
                return;
            }

            FaceToward(transform.position + wanderDirection);
            characterController.SimpleMove(wanderDirection * wanderSpeed);

            // Bounce off walls instead of grinding against them.
            if ((characterController.collisionFlags & CollisionFlags.Sides) != 0)
            {
                wanderDirection = Quaternion.Euler(0f, Random.Range(90f, 270f), 0f) * wanderDirection;
            }
        }

        private Vector3 EyePosition => eye != null ? eye.position : transform.position + Vector3.up * eyeHeight;

        private bool TryGetPlayer(out Transform target)
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null ? playerObject.transform : null;
            }

            target = player;
            return target != null;
        }

        private bool HasLineOfSight(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            if (Physics.Raycast(from, direction / distance, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                Transform hitTransform = hit.collider.transform;
                return hitTransform == target || hitTransform.IsChildOf(target);
            }

            return true; // nothing between us and the target point
        }

        private void FaceToward(Vector3 worldPoint)
        {
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion desired = Quaternion.LookRotation(flat.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeedDegrees * Time.deltaTime);
        }

        private void HandleDied()
        {
            if (reportedDeath)
            {
                return;
            }

            reportedDeath = true;

            if (deathBurstColor.a > 0f)
            {
                SpellBurstVisual.Spawn(transform.position + Vector3.up * 0.9f, deathBurstRadius, deathBurstColor, 0.35f);
            }

            game?.NotifyEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
