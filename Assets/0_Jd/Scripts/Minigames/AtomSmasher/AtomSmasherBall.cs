using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Ball")]
    public class AtomSmasherBall : MonoBehaviour
    {
        [Header("Plane Lock")]
        [SerializeField] private float physicsPlaneZ = 0f;

        [Header("Bounce")]
        [SerializeField, Range(0f, 1f)] private float bounciness = 0.95f;
        [SerializeField, Range(0f, 1f)] private float friction = 0f;

        [Header("Round End")]
        [SerializeField] private float drainY = -6.5f;
        [SerializeField] private float settleSpeed = 0.15f;
        [SerializeField] private float settleSeconds = 1.25f;
        [SerializeField] private float minimumLifeSeconds = 0.5f;
        [SerializeField] private float maxLifeSeconds = 14f;

        [Header("Feedback")]
        [SerializeField] private Renderer[] ballRenderers;
        [SerializeField] private TrailRenderer boostTrail;

        [Header("Feel")]
        [Tooltip("Scale punch applied on impacts; eases back each frame. 1 disables.")]
        [SerializeField, Min(1f)] private float impactPunchScale = 1.25f;

        [Tooltip("How fast punched/spawned scale settles back to normal.")]
        [SerializeField, Min(1f)] private float scaleSettleSpeed = 12f;

        [Tooltip("Scale the ball pops in from when launched. 1 disables.")]
        [SerializeField, Range(0.1f, 1f)] private float spawnScale = 0.55f;

        [Header("Trail Look")]
        [Tooltip("Trail width while no effect is active.")]
        [SerializeField, Min(0f)] private float baseTrailWidth = 0.07f;

        [Tooltip("Trail color while no effect is active.")]
        [SerializeField] private Color baseTrailColor = new Color(0.75f, 0.85f, 1f, 0.45f);

        [Tooltip("Trail width while a temporary effect is active.")]
        [SerializeField, Min(0f)] private float boostTrailWidth = 0.24f;

        private AtomSmasherGame game;
        private Rigidbody rb;
        private PhysicsMaterial runtimePhysicsMaterial;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseScale;
        private float launchTime;
        private float settledSince = -1f;
        private float visualEffectEndTime = -1f;
        private bool isFinished;

        public Rigidbody Rigidbody => rb;
        public Vector3 PlanarVelocity => rb != null ? new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, 0f) : Vector3.zero;

        /// <summary>Rebounds off walls/obstructions since the last launch.</summary>
        public int BounceCount { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            baseScale = transform.localScale;
            ResolveRenderers();
            ConfigureRigidbody();
            ConfigureCollider();
            ApplyBaseTrail();
            LockToPlane();
        }

        private void Update()
        {
            if (visualEffectEndTime > 0f && Time.time >= visualEffectEndTime)
            {
                ClearTemporaryVisualState();
            }

            // Ease spawn pops and impact punches back to normal size.
            if (transform.localScale != baseScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * scaleSettleSpeed);
            }
        }

        private void OnValidate()
        {
            settleSpeed = Mathf.Max(0f, settleSpeed);
            settleSeconds = Mathf.Max(0f, settleSeconds);
            minimumLifeSeconds = Mathf.Max(0f, minimumLifeSeconds);
            maxLifeSeconds = Mathf.Max(1f, maxLifeSeconds);
        }

        private void FixedUpdate()
        {
            LockToPlane();

            if (isFinished)
            {
                return;
            }

            if (transform.position.y <= drainY)
            {
                FinishBall();
                return;
            }

            if (Time.time - launchTime < minimumLifeSeconds)
            {
                return;
            }

            if (Time.time - launchTime >= maxLifeSeconds)
            {
                FinishBall();
                return;
            }

            Vector3 planarVelocity = rb.linearVelocity;
            planarVelocity.z = 0f;

            if (planarVelocity.magnitude <= settleSpeed)
            {
                if (settledSince < 0f)
                {
                    settledSince = Time.time;
                }

                if (Time.time - settledSince >= settleSeconds)
                {
                    FinishBall();
                }
            }
            else
            {
                settledSince = -1f;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isFinished)
            {
                return;
            }

            if (impactPunchScale > 1f)
            {
                transform.localScale = baseScale * impactPunchScale;
            }

            AtomSmasherTarget target = collision.collider.GetComponentInParent<AtomSmasherTarget>();
            if (target != null)
            {
                target.TryHit(this);
            }
            else
            {
                BounceCount++; // walls and obstructions count as rebounds
            }
        }

        private void OnDestroy()
        {
            if (!isFinished)
            {
                game?.NotifyBallFinished(this, false);
            }
        }

        public void Initialize(AtomSmasherGame owningGame, float planeZ, float boardDrainY, float boardSettleSpeed, float boardSettleSeconds, float boardMaxLifeSeconds)
        {
            game = owningGame;
            physicsPlaneZ = planeZ;
            drainY = boardDrainY;
            settleSpeed = boardSettleSpeed;
            settleSeconds = boardSettleSeconds;
            maxLifeSeconds = boardMaxLifeSeconds;
            isFinished = false;
            settledSince = -1f;
            ClearTemporaryVisualState();
            ConfigureRigidbody();
            ConfigureCollider();
            LockToPlane();
        }

        public void Launch(Vector3 velocity)
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            launchTime = Time.time;
            isFinished = false;
            settledSince = -1f;
            BounceCount = 0;

            if (baseScale == Vector3.zero)
            {
                baseScale = transform.localScale;
            }

            transform.localScale = baseScale * spawnScale; // pop-in on launch

            rb.linearVelocity = new Vector3(velocity.x, velocity.y, 0f);
            rb.angularVelocity = Vector3.zero;
            LockToPlane();
        }

        public bool TryBoostPlanarVelocity(float multiplier, float maxSpeed)
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (rb == null)
            {
                return false;
            }

            Vector3 velocity = PlanarVelocity;
            if (velocity.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float boostedSpeed = velocity.magnitude * Mathf.Max(0.01f, multiplier);
            if (maxSpeed > 0f)
            {
                boostedSpeed = Mathf.Min(boostedSpeed, maxSpeed);
            }

            rb.linearVelocity = velocity.normalized * boostedSpeed;
            settledSince = -1f;
            LockToPlane();
            return true;
        }

        public void ResetDecayTimer()
        {
            launchTime = Time.time;
            settledSince = -1f;
        }

        public void ApplyTemporaryVisualState(Color color, float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            ResolveRenderers();
            propertyBlock ??= new MaterialPropertyBlock();

            if (ballRenderers != null)
            {
                foreach (Renderer ballRenderer in ballRenderers)
                {
                    if (ballRenderer == null)
                    {
                        continue;
                    }

                    ballRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_BaseColor", color);
                    propertyBlock.SetColor("_Color", color);
                    ballRenderer.SetPropertyBlock(propertyBlock);
                }
            }

            if (boostTrail != null)
            {
                // The trail always emits; effects make it wider and tinted.
                boostTrail.emitting = true;
                boostTrail.startWidth = boostTrailWidth;
                boostTrail.endWidth = 0f;
                boostTrail.startColor = new Color(color.r, color.g, color.b, 0.9f);
                boostTrail.endColor = new Color(color.r, color.g, color.b, 0f);
            }

            visualEffectEndTime = Time.time + seconds;
        }

        private void ResolveRenderers()
        {
            if (ballRenderers == null || ballRenderers.Length == 0)
            {
                ballRenderers = GetComponentsInChildren<Renderer>();
            }
        }

        private void ClearTemporaryVisualState()
        {
            visualEffectEndTime = -1f;

            if (ballRenderers != null)
            {
                foreach (Renderer ballRenderer in ballRenderers)
                {
                    if (ballRenderer != null)
                    {
                        ballRenderer.SetPropertyBlock(null);
                    }
                }
            }

            ApplyBaseTrail();
        }

        private void ApplyBaseTrail()
        {
            if (boostTrail == null)
            {
                return;
            }

            boostTrail.emitting = true;
            boostTrail.startWidth = baseTrailWidth;
            boostTrail.endWidth = 0f;
            boostTrail.startColor = baseTrailColor;
            boostTrail.endColor = new Color(baseTrailColor.r, baseTrailColor.g, baseTrailColor.b, 0f);
        }

        /// <summary>Ends the ball immediately (pit traps, hazards) as if it drained.</summary>
        public void ForceFinish()
        {
            FinishBall();
        }

        private void ConfigureRigidbody()
        {
            if (rb == null)
            {
                return;
            }

            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezePositionZ;
        }

        private void ConfigureCollider()
        {
            Collider ballCollider = GetComponent<Collider>();
            if (ballCollider == null)
            {
                return;
            }

            if (runtimePhysicsMaterial == null)
            {
                runtimePhysicsMaterial = new PhysicsMaterial($"{name} Bounce")
                {
                    bounciness = bounciness,
                    dynamicFriction = friction,
                    staticFriction = friction,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                    frictionCombine = PhysicsMaterialCombine.Minimum
                };
            }

            ballCollider.material = runtimePhysicsMaterial;
        }

        private void LockToPlane()
        {
            Vector3 position = transform.position;
            position.z = physicsPlaneZ;
            transform.position = position;

            if (rb == null)
            {
                return;
            }

            Vector3 velocity = rb.linearVelocity;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
        }

        private void FinishBall()
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            game?.NotifyBallFinished(this);
        }
    }
}
