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

        private CharacterController characterController;
        private Health health;
        private SpellCaster caster;
        private LabyrinthCrawlerGame game;
        private Transform player;
        private bool reportedDeath;

        public Health Health => health;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            caster = GetComponent<SpellCaster>();
            health.Faction = Faction.Enemy;
            health.OnDied.AddListener(HandleDied);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDied.RemoveListener(HandleDied);
            }
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

            if (!TryGetPlayer(out Transform target))
            {
                return;
            }

            Vector3 eyePosition = EyePosition;
            Vector3 targetPoint = target.position + Vector3.up * targetHeight;
            Vector3 toTarget = targetPoint - eyePosition;
            float distance = toTarget.magnitude;

            if (distance > detectionRange || !HasLineOfSight(eyePosition, targetPoint, target))
            {
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
            game?.NotifyEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
