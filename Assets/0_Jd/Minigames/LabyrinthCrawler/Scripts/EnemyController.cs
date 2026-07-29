using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Maze enemy. Off-duty it patrols room to room through open doorways
    /// (walking the maze graph, not a NavMesh — the maze regenerates every
    /// stage). Spotting the player (range + line of sight) switches to chase:
    /// face them, close to attack range, cast slot 0 on cooldown. Losing sight
    /// hunts the last seen position for a few seconds before the patrol
    /// resumes. Doorways plugged by an illusory wall raycast as blocked, so
    /// patrols never give a secret away by pathing into it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(SpellCaster))]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Enemy Controller")]
    public class EnemyController : MonoBehaviour
    {
        public enum LocomotionMode
        {
            Chase,
            KeepDistance,
            Flying
        }

        #region Configuration

        [Header("Senses")]
        [Tooltip("Shared range at which any enemy archetype can acquire the player. Clear line of sight is always required.")]
        [SerializeField, Min(1f)] private float detectionRange = 15f;

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

        [Header("Combat Movement")]
        [SerializeField] private LocomotionMode locomotionMode = LocomotionMode.Chase;

        [Tooltip("Distance a ranged enemy tries not to let the player cross.")]
        [SerializeField, Min(0f)] private float minimumRange = 5f;

        [Tooltip("Sideways speed while a ranged enemy has a good firing distance.")]
        [SerializeField, Min(0f)] private float strafeSpeed = 2.2f;

        [Tooltip("Backpedal speed when the player gets inside Minimum Range.")]
        [SerializeField, Min(0f)] private float retreatSpeed = 4f;

        [Tooltip("Seconds before choosing the other strafe direction.")]
        [SerializeField] private Vector2 strafeSecondsRange = new Vector2(1.1f, 2.4f);

        [Header("Flight")]
        [Tooltip("Height added to the spawn position for flying enemies.")]
        [SerializeField, Min(0f)] private float flightHeight = 2.1f;

        [SerializeField, Min(0f)] private float hoverAmplitude = 0.22f;
        [SerializeField, Min(0f)] private float hoverFrequency = 1.7f;
        [SerializeField, Min(0.1f)] private float flightVerticalSpeed = 3.5f;

        [Tooltip("How far ahead a flyer checks for lintels and walls.")]
        [SerializeField, Min(0.5f)] private float flightObstacleLookAhead = 4f;

        [Tooltip("Extra space kept between a ducking flyer and the floor.")]
        [SerializeField, Min(0f)] private float doorwayClearance = 0.12f;

        [Tooltip("Extra space kept above the top of a wall.")]
        [SerializeField, Min(0f)] private float wallOverflightClearance = 0.35f;

        [Tooltip("Keeps the chosen duck/climb altitude long enough to clear the obstacle.")]
        [SerializeField, Min(0f)] private float flightManeuverHoldSeconds = 0.85f;

        [Header("Attack")]
        [Tooltip("Layers the enemy's spells may hit. Includes the player layer by default.")]
        [SerializeField] private LayerMask spellHitMask = ~0;

        [Tooltip("Aim at this height above the player's feet.")]
        [SerializeField, Min(0f)] private float targetHeight = 1.2f;

        [Header("Patrol")]
        [Tooltip("Walk speed between rooms while the player is not detected.")]
        [SerializeField, Min(0f)] private float patrolSpeed = 1.9f;

        [Tooltip("Chance to pause and look around after reaching a room.")]
        [SerializeField, Range(0f, 1f)] private float patrolPauseChance = 0.55f;

        [SerializeField] private Vector2 patrolPauseSecondsRange = new Vector2(0.4f, 1.6f);

        [Tooltip("Arrival tolerance around the target room center.")]
        [SerializeField, Min(0.1f)] private float waypointTolerance = 1f;

        [Header("Tracking")]
        [Tooltip("Seconds the enemy hunts the player's last seen position after losing sight.")]
        [SerializeField, Min(0f)] private float trackSeconds = 4f;

        [Header("Wander Fallback")]
        [Tooltip("Aimless drift speed used only when no maze graph is available.")]
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

        [Tooltip("World Y below which a fallen enemy is counted as killed. Without this a foe that walks into a pit falls forever, never reports its death, and the room-clear exit never opens. Sits below the player's fall-respawn line.")]
        [SerializeField] private float pitKillPlaneY = -8f;

        [Header("Audio")]
        [Tooltip("Quiet spatial footsteps while this enemy is actually moving.")]
        [SerializeField] private bool enemyFootstepsEnabled = true;

        [SerializeField] private AudioClip[] enemyFootstepClips;
        [SerializeField, Range(0f, 1f)] private float enemyFootstepVolume = 0.16f;

        [Tooltip("Step interval at the reference speed. Faster enemies naturally step faster.")]
        [SerializeField, Min(0.05f)] private float enemyFootstepInterval = 0.42f;

        [SerializeField, Min(0.1f)] private float enemyFootstepReferenceSpeed = 4f;
        [SerializeField, Range(0f, 0.5f)] private float enemyFootstepPitchJitter = 0.08f;
        [SerializeField, Min(0f)] private float enemyFootstepMinDistance = 1.5f;
        [SerializeField, Min(0.1f)] private float enemyFootstepMaxDistance = 12f;

        #endregion

        #region Runtime State

        private CharacterController characterController;
        private Health health;
        private SpellCaster caster;
        private AudioSource enemyFootstepSource;
        private LabyrinthCrawlerGame game;
        private Transform player;
        private bool reportedDeath;
        private Vector3 wanderDirection;
        private float wanderPhaseEndTime;
        private bool wanderMoving;
        private Vector3 knockbackVelocity;
        private Vector3 patrolTarget;
        private readonly List<Room3D> patrolOptions = new List<Room3D>(4);
        private bool hasPatrolTarget;
        private float patrolPauseUntil;
        private float stuckSince = -1f;
        private Room3D currentPatrolRoom;
        private Room3D lastPatrolRoom;
        private Vector3 lastKnownPlayerPosition;
        private float lastSeenPlayerTime = float.NegativeInfinity;
        private float enemyFootstepTimer;
        private int lastEnemyFootstepClipIndex = -1;
        private float strafeDirection = 1f;
        private float nextStrafeChangeTime;
        private float flightGroundCenterY;
        private float flightCruiseY;
        private float flightTargetY;
        private float flightManeuverUntil;

        public Health Health => health;
        public LocomotionMode MovementMode => locomotionMode;

        #endregion

        #region Lifecycle and Combat State

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            caster = GetComponent<SpellCaster>();
            health.Faction = Faction.Enemy;
            health.OnDied.AddListener(HandleDied);
            health.OnDamaged.AddListener(HandleDamaged);
            ConfigureEnemyFootstepSource();
            flightGroundCenterY = transform.position.y;
            flightCruiseY = transform.position.y + flightHeight;
            flightTargetY = flightCruiseY;
            strafeDirection = Random.value < 0.5f ? -1f : 1f;
            ScheduleNextStrafeChange();

            if (locomotionMode == LocomotionMode.Flying &&
                !TryGetComponent(out FlyingEnemyVisual _))
            {
                gameObject.AddComponent<FlyingEnemyVisual>();
            }

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

            // Fell into a pit: there is no floor below a pit, so a foe that
            // walks in drops forever. Count it dead the moment it clears the
            // kill plane, routing through the normal death path so the foe
            // counter resolves and a lure-into-pit still credits the player.
            if (transform.position.y < pitKillPlaneY)
            {
                health.TakeDamage(float.MaxValue, Faction.Neutral);
                return;
            }

            if (game != null && (!game.IsRunning || game.IsChoosingUpgrade))
            {
                return;
            }

            // Knockback overrides everything until it bleeds off (crowd control).
            if (knockbackVelocity.sqrMagnitude > 0.04f)
            {
                Vector3 gravity = locomotionMode == LocomotionMode.Flying
                    ? Vector3.zero
                    : Physics.gravity * 0.5f;
                characterController.Move((knockbackVelocity + gravity) * Time.deltaTime);
                knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackRecovery * Time.deltaTime);
                return;
            }

            if (!TryGetPlayer(out Transform target))
            {
                Patrol();
                return;
            }

            Vector3 eyePosition = EyePosition;
            Vector3 targetPoint = target.position + Vector3.up * targetHeight;
            Vector3 toTarget = targetPoint - eyePosition;
            float distance = toTarget.magnitude;
            bool hasConfirmedSight =
                distance <= detectionRange &&
                HasLineOfSight(eyePosition, targetPoint, target);

            // Detection is intentionally identical for every archetype:
            // range + clear sight acquires/refreshes the player. Occlusion
            // routes all enemies through the same last-known-position memory.
            if (!hasConfirmedSight)
            {
                TrackOrPatrol();
                return;
            }

            lastKnownPlayerPosition = target.position;
            lastSeenPlayerTime = Time.time;
            hasPatrolTarget = false; // re-plan the patrol after combat ends
            currentPatrolRoom = null; // combat may pull us several rooms away

            FaceToward(target.position);

            if (locomotionMode == LocomotionMode.Chase)
            {
                if (distance > attackRange)
                {
                    MoveCharacter(FlatDirectionTo(target.position) * moveSpeed);
                    return;
                }

                MoveCharacter(Vector3.zero);
            }
            else
            {
                MoveCharacter(GetRangedMovement(target.position, distance));
            }

            if (distance <= attackRange)
            {
                TryAttack(eyePosition, toTarget);
            }
        }

        private void LateUpdate()
        {
            UpdateEnemyFootsteps();
        }

        /// <summary>Shoves the enemy (pulse crowd control); movement resumes once it decays.</summary>
        public void ApplyKnockback(Vector3 impulse)
        {
            knockbackVelocity = impulse;
            hasPatrolTarget = false;
            currentPatrolRoom = null;
        }

        #endregion

        #region Tracking, Patrol, and Wander

        /// <summary>
        /// Sight lost: hunt the last seen position for a while (facing it, so
        /// the pursuit reads as intent), then drop back to patrolling.
        /// </summary>
        private void TrackOrPatrol()
        {
            if (Time.time - lastSeenPlayerTime <= trackSeconds)
            {
                Vector3 flat = lastKnownPlayerPosition - transform.position;
                flat.y = 0f;
                if (flat.magnitude > 1.1f)
                {
                    FaceToward(lastKnownPlayerPosition);
                    MoveCharacter(flat.normalized * moveSpeed);
                    return;
                }
            }

            Patrol();
        }

        /// <summary>
        /// Walks the maze graph room to room. Doorways are re-validated with a
        /// physics ray each pick, so routes plugged by an illusory wall (or
        /// anything else solid) are treated as closed.
        /// </summary>
        private void Patrol()
        {
            ArcadeGen3D maze = game != null ? game.Maze : null;
            if (maze == null || maze.Rooms == null)
            {
                Wander(); // no maze graph available - old aimless drift
                return;
            }

            if (Time.time < patrolPauseUntil)
            {
                MoveCharacter(Vector3.zero);
                return;
            }

            if (!hasPatrolTarget && !TryPickNextPatrolTarget(maze))
            {
                Wander();
                return;
            }

            Vector3 flat = patrolTarget - transform.position;
            flat.y = 0f;

            if (flat.magnitude <= waypointTolerance)
            {
                hasPatrolTarget = false;
                if (Random.value < patrolPauseChance)
                {
                    patrolPauseUntil = Time.time + Random.Range(patrolPauseSecondsRange.x, patrolPauseSecondsRange.y);
                }

                MoveCharacter(Vector3.zero);
                return;
            }

            FaceToward(patrolTarget);
            MoveCharacter(flat.normalized * patrolSpeed);

            // Grinding against a wall (or a plug that appeared mid-route):
            // abandon this waypoint and plan again.
            if ((characterController.collisionFlags & CollisionFlags.Sides) != 0)
            {
                if (stuckSince < 0f)
                {
                    stuckSince = Time.time;
                }
                else if (Time.time - stuckSince > 0.75f)
                {
                    hasPatrolTarget = false;
                    currentPatrolRoom = null;
                    stuckSince = -1f;
                }
            }
            else
            {
                stuckSince = -1f;
            }
        }

        private bool TryPickNextPatrolTarget(ArcadeGen3D maze)
        {
            Room3D[,] rooms = maze.Rooms;
            Room3D current = currentPatrolRoom;
            int roomX;
            int roomZ;
            if (current == null)
            {
                if (!TryFindRoomIndex(rooms, transform.position, out roomX, out roomZ))
                {
                    return false;
                }

                current = rooms[roomX, roomZ];
            }
            else
            {
                roomX = current.Index.x;
                roomZ = current.Index.z;
            }

            patrolOptions.Clear();
            CollectPatrolOption(rooms, current, roomX, roomZ + 1, Room3D.Directions.NORTH, patrolOptions);
            CollectPatrolOption(rooms, current, roomX, roomZ - 1, Room3D.Directions.SOUTH, patrolOptions);
            CollectPatrolOption(rooms, current, roomX + 1, roomZ, Room3D.Directions.EAST, patrolOptions);
            CollectPatrolOption(rooms, current, roomX - 1, roomZ, Room3D.Directions.WEST, patrolOptions);

            if (patrolOptions.Count == 0)
            {
                return false;
            }

            // Prefer pressing on over doubling back when there is a choice.
            if (patrolOptions.Count > 1 && lastPatrolRoom != null)
            {
                patrolOptions.Remove(lastPatrolRoom);
            }

            Room3D next = patrolOptions[Random.Range(0, patrolOptions.Count)];
            lastPatrolRoom = current;
            currentPatrolRoom = next;

            float scatter = Mathf.Min(maze.RoomWidth, maze.RoomLength) * 0.15f;
            Vector2 offset = Random.insideUnitCircle * scatter;
            patrolTarget = next.transform.position + new Vector3(offset.x, 0f, offset.y);
            hasPatrolTarget = true;
            return true;
        }

        private void CollectPatrolOption(Room3D[,] rooms, Room3D current, int roomX, int roomZ, Room3D.Directions door, List<Room3D> options)
        {
            if (current == null || current.IsWallClosed(door) ||
                roomX < 0 || roomZ < 0 ||
                roomX >= rooms.GetLength(0) || roomZ >= rooms.GetLength(1))
            {
                return;
            }

            Room3D neighbor = rooms[roomX, roomZ];

            // Never volunteer to patrol into a pit - that would be a foe
            // walking off a ledge for no reason. A chasing enemy still crosses
            // one going after the player, so pits stay baitable on purpose.
            if (neighbor != null && !neighbor.IsPit && IsDoorwayClear(current, neighbor))
            {
                options.Add(neighbor);
            }
        }

        // An open doorway can still be plugged by an illusory wall's solid
        // collider. Cast room center to room center at chest height; any
        // static hit (real wall, illusory plug) closes the route. Bodies do
        // not count - a crowd is not a wall.
        private bool IsDoorwayClear(Room3D from, Room3D to)
        {
            Vector3 origin = from.transform.position + Vector3.up * 1.2f;
            Vector3 destination = to.transform.position + Vector3.up * 1.2f;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;
            if (distance <= 0.01f)
            {
                return true;
            }

            direction /= distance;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, lineOfSightMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<Health>() != null ||
                    hit.collider.GetComponentInParent<Projectile>() != null ||
                    hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool TryFindRoomIndex(Room3D[,] rooms, Vector3 position, out int roomX, out int roomZ)
        {
            roomX = -1;
            roomZ = -1;
            float bestSqrDistance = float.MaxValue;

            for (int x = 0; x < rooms.GetLength(0); x++)
            {
                for (int z = 0; z < rooms.GetLength(1); z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null)
                    {
                        continue;
                    }

                    Vector3 delta = room.transform.position - position;
                    delta.y = 0f;
                    float sqrDistance = delta.sqrMagnitude;
                    if (sqrDistance < bestSqrDistance)
                    {
                        bestSqrDistance = sqrDistance;
                        roomX = x;
                        roomZ = z;
                    }
                }
            }

            return roomX >= 0;
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
                MoveCharacter(Vector3.zero);
                return;
            }

            FaceToward(transform.position + wanderDirection);
            MoveCharacter(wanderDirection * wanderSpeed);

            // Bounce off walls instead of grinding against them.
            if ((characterController.collisionFlags & CollisionFlags.Sides) != 0)
            {
                wanderDirection = Quaternion.Euler(0f, Random.Range(90f, 270f), 0f) * wanderDirection;
            }
        }

        #endregion

        #region Combat Movement and Attacking

        private Vector3 GetRangedMovement(Vector3 targetPosition, float distance)
        {
            Vector3 toward = FlatDirectionTo(targetPosition);
            if (toward.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            UpdateStrafeDirection();
            Vector3 sideways = Vector3.Cross(Vector3.up, toward) * strafeDirection;

            if (distance < minimumRange)
            {
                return (-toward * retreatSpeed + sideways * strafeSpeed * 0.35f);
            }

            if (distance > attackRange)
            {
                return toward * moveSpeed;
            }

            return sideways * strafeSpeed;
        }

        private void MoveCharacter(Vector3 velocity)
        {
            velocity.y = 0f;
            if (locomotionMode != LocomotionMode.Flying)
            {
                characterController.SimpleMove(velocity);
                return;
            }

            UpdateFlightTarget(velocity);
            float hoverOffset = Mathf.Sin(Time.time * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude;
            float desiredY = flightTargetY + hoverOffset;
            float verticalVelocity = Mathf.Clamp(
                (desiredY - transform.position.y) * flightVerticalSpeed,
                -flightVerticalSpeed,
                flightVerticalSpeed);

            characterController.Move((velocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void UpdateFlightTarget(Vector3 velocity)
        {
            if (velocity.sqrMagnitude < 0.001f)
            {
                if (Time.time >= flightManeuverUntil)
                {
                    flightTargetY = flightCruiseY;
                }

                return;
            }

            Vector3 direction = velocity.normalized;
            if (!TryFindFlightObstacle(transform.position.y, direction, flightObstacleLookAhead, out RaycastHit obstacle))
            {
                if (Time.time >= flightManeuverUntil)
                {
                    flightTargetY = flightCruiseY;
                }

                return;
            }

            float duckY = flightGroundCenterY + doorwayClearance;
            if (!TryFindFlightObstacle(duckY, direction, flightObstacleLookAhead, out _))
            {
                SetFlightManeuver(duckY);
                return;
            }

            float climbY =
                obstacle.collider.bounds.max.y +
                characterController.height * 0.5f +
                wallOverflightClearance;
            if (!TryFindFlightObstacle(climbY, direction, flightObstacleLookAhead, out _))
            {
                SetFlightManeuver(Mathf.Max(flightCruiseY, climbY));
            }
        }

        private void SetFlightManeuver(float targetY)
        {
            flightTargetY = targetY;
            flightManeuverUntil = Time.time + flightManeuverHoldSeconds;
        }

        private bool TryFindFlightObstacle(
            float centerY,
            Vector3 direction,
            float distance,
            out RaycastHit nearestObstacle)
        {
            float radius = Mathf.Max(0.05f, characterController.radius * 0.82f);
            float cylinderHalfHeight = Mathf.Max(0f, characterController.height * 0.5f - radius);
            Vector3 center = new Vector3(transform.position.x, centerY + characterController.center.y, transform.position.z);
            Vector3 bottom = center + Vector3.down * cylinderHalfHeight;
            Vector3 top = center + Vector3.up * cylinderHalfHeight;
            RaycastHit[] hits = Physics.CapsuleCastAll(
                bottom,
                top,
                radius,
                direction,
                distance,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore);

            nearestObstacle = default;
            float nearestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<Health>() != null ||
                    hit.collider.GetComponentInParent<Projectile>() != null)
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestObstacle = hit;
                }
            }

            return nearestDistance < float.MaxValue;
        }

        private void UpdateStrafeDirection()
        {
            if (Time.time >= nextStrafeChangeTime ||
                (characterController.collisionFlags & CollisionFlags.Sides) != 0)
            {
                strafeDirection *= -1f;
                ScheduleNextStrafeChange();
            }
        }

        private void ScheduleNextStrafeChange()
        {
            float minimum = Mathf.Min(strafeSecondsRange.x, strafeSecondsRange.y);
            float maximum = Mathf.Max(strafeSecondsRange.x, strafeSecondsRange.y);
            nextStrafeChangeTime = Time.time + Random.Range(minimum, maximum);
        }

        private void TryAttack(Vector3 eyePosition, Vector3 toTarget)
        {
            caster.TryCast(0, BuildCastContext(eyePosition, toTarget));
        }

        private SpellCastContext BuildCastContext(Vector3 eyePosition, Vector3 toTarget)
        {
            Vector3 aimDirection = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized
                : transform.forward;

            return new SpellCastContext
            {
                Caster = transform,
                Faction = Faction.Enemy,
                AimRay = new Ray(eyePosition, aimDirection),
                Muzzle = eye != null ? eye : null,
                HitMask = spellHitMask
            };
        }

        private Vector3 FlatDirectionTo(Vector3 worldPoint)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
        }

        #endregion

        #region Footstep Audio

        private void UpdateEnemyFootsteps()
        {
            if (!enemyFootstepsEnabled ||
                enemyFootstepSource == null ||
                enemyFootstepClips == null ||
                enemyFootstepClips.Length == 0 ||
                characterController == null ||
                health == null ||
                health.IsDead ||
                (game != null && (!game.IsRunning || game.IsChoosingUpgrade)))
            {
                enemyFootstepTimer = 0f;
                return;
            }

            Vector3 horizontalVelocity = characterController.velocity;
            horizontalVelocity.y = 0f;
            float speed = horizontalVelocity.magnitude;
            if (speed < 0.25f || !characterController.isGrounded)
            {
                enemyFootstepTimer = 0f;
                return;
            }

            float speedRatio = Mathf.Max(0.35f, speed / enemyFootstepReferenceSpeed);
            enemyFootstepTimer += Time.deltaTime * speedRatio;
            if (enemyFootstepTimer >= enemyFootstepInterval)
            {
                enemyFootstepTimer -= enemyFootstepInterval;
                PlayEnemyFootstep();
            }
        }

        private void PlayEnemyFootstep()
        {
            int index = Random.Range(0, enemyFootstepClips.Length);
            if (enemyFootstepClips.Length > 1 && index == lastEnemyFootstepClipIndex)
            {
                index = (index + 1) % enemyFootstepClips.Length;
            }

            lastEnemyFootstepClipIndex = index;
            AudioClip clip = enemyFootstepClips[index];
            if (clip == null)
            {
                return;
            }

            enemyFootstepSource.pitch = 1f + Random.Range(-enemyFootstepPitchJitter, enemyFootstepPitchJitter);
            enemyFootstepSource.PlayOneShot(clip, enemyFootstepVolume);
        }

        private void ConfigureEnemyFootstepSource()
        {
            if (!TryGetComponent(out enemyFootstepSource))
            {
                enemyFootstepSource = gameObject.AddComponent<AudioSource>();
            }

            enemyFootstepSource.playOnAwake = false;
            enemyFootstepSource.loop = false;
            enemyFootstepSource.spatialBlend = 1f;
            enemyFootstepSource.rolloffMode = AudioRolloffMode.Linear;
            enemyFootstepSource.minDistance = enemyFootstepMinDistance;
            enemyFootstepSource.maxDistance = Mathf.Max(enemyFootstepMinDistance + 0.1f, enemyFootstepMaxDistance);
            enemyFootstepSource.priority = 190;
        }

        #endregion

        #region Sensing and Facing

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

        #endregion

        #region Death

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

        #endregion
    }
}
