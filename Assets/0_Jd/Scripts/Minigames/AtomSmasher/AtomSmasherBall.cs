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

        private AtomSmasherGame game;
        private Rigidbody rb;
        private PhysicsMaterial runtimePhysicsMaterial;
        private MaterialPropertyBlock propertyBlock;
        private float launchTime;
        private float settledSince = -1f;
        private float visualEffectEndTime = -1f;
        private bool isFinished;

        public Rigidbody Rigidbody => rb;
        public Vector3 PlanarVelocity => rb != null ? new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, 0f) : Vector3.zero;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ResolveRenderers();
            ConfigureRigidbody();
            ConfigureCollider();
            LockToPlane();
        }

        private void Update()
        {
            if (visualEffectEndTime > 0f && Time.time >= visualEffectEndTime)
            {
                ClearTemporaryVisualState();
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

            AtomSmasherTarget target = collision.collider.GetComponentInParent<AtomSmasherTarget>();
            if (target != null)
            {
                target.TryHit(this);
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
                boostTrail.emitting = true;
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

            if (boostTrail != null)
            {
                boostTrail.emitting = false;
            }
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
