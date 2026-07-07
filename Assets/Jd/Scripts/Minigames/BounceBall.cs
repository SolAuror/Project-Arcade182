using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Bounce Ball")]
    public class BounceBall : MonoBehaviour
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

        private BounceTargetsGame game;
        private Rigidbody rb;
        private PhysicsMaterial runtimePhysicsMaterial;
        private float launchTime;
        private float settledSince = -1f;
        private bool isFinished;

        public Rigidbody Rigidbody => rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            ConfigureCollider();
            LockToPlane();
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

            BounceTarget target = collision.collider.GetComponentInParent<BounceTarget>();
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

        public void Initialize(BounceTargetsGame owningGame, float planeZ, float boardDrainY, float boardSettleSpeed, float boardSettleSeconds, float boardMaxLifeSeconds)
        {
            game = owningGame;
            physicsPlaneZ = planeZ;
            drainY = boardDrainY;
            settleSpeed = boardSettleSpeed;
            settleSeconds = boardSettleSeconds;
            maxLifeSeconds = boardMaxLifeSeconds;
            isFinished = false;
            settledSince = -1f;
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
