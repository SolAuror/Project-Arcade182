using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Hoops Throwable")]
    public class HoopsThrowable : MonoBehaviour
    {
        [Tooltip("Base score for this ball; multiplied by the hoop's difficulty (size x stage).")]
        [SerializeField, Min(1)] private int points = 1;

        [SerializeField] private float scoreCooldownSeconds = 0.5f;
        [SerializeField] private float resetBelowY = -10f;
        [SerializeField] private bool resetWhenFallen = true;

        [Header("Bounce")]
        [Tooltip("Basketball-like restitution. Average combine keeps bouncy surfaces from compounding it.")]
        [SerializeField, Range(0f, 1f)] private float bounciness = 0.6f;

        [SerializeField, Range(0f, 1f)] private float friction = 0.45f;

        [Tooltip("Spin damping; keeps rebounds from getting erratic.")]
        [SerializeField, Min(0f)] private float angularDamping = 0.8f;

        [Header("Feel")]
        [Tooltip("Flight trail appears above this speed so throws read in the air. 0 disables.")]
        [SerializeField, Min(0f)] private float trailMinSpeed = 4f;

        [Tooltip("Authored trail component. Use the Hoops migration command to add and configure it to existing balls.")]
        [SerializeField] private TrailRenderer flightTrail;

        [SerializeField] private Color trailColor = new Color(1f, 0.72f, 0.35f, 0.5f);

        [Tooltip("Scale squash on hard bounces; eases back each frame. 1 disables.")]
        [SerializeField, Min(1f)] private float impactPunchScale = 1.12f;

        [Tooltip("Impact speed needed to trigger the squash.")]
        [SerializeField, Min(0f)] private float impactPunchMinSpeed = 4f;

        private Rigidbody rb;
        [Tooltip("Authored bounce material. Runtime no longer creates a PhysicsMaterial for each ball.")]
        [SerializeField] private PhysicsMaterial bounceMaterial;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private Vector3 baseScale;
        private float lastScoreTime = -999f;

        public int Points => points;
        public bool CanScore => Time.time - lastScoreTime >= scoreCooldownSeconds;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            baseScale = transform.localScale;
            ConfigureBounce();
        }

        private void ConfigureBounce()
        {
            rb.angularDamping = angularDamping;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Collider ballCollider = GetComponent<Collider>();
            if (ballCollider == null)
            {
                return;
            }

            if (bounceMaterial == null)
            {
                Debug.LogError(
                    $"{nameof(HoopsThrowable)} on {name} requires an authored bounce PhysicsMaterial. " +
                    "Check the authored Hoops throwable prefab.",
                    this);
                return;
            }

            ballCollider.sharedMaterial = bounceMaterial;
        }

        private void Update()
        {
            if (resetWhenFallen && transform.position.y < resetBelowY)
            {
                ResetToSpawn();
            }

            // Trail only while genuinely flying, so held/rolling balls stay clean.
            if (flightTrail != null)
            {
                flightTrail.emitting = rb.linearVelocity.magnitude >= trailMinSpeed;
            }

            if (transform.localScale != baseScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 10f);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (impactPunchScale > 1f && collision.relativeVelocity.magnitude >= impactPunchMinSpeed)
            {
                transform.localScale = baseScale * impactPunchScale;
            }
        }

        private void OnValidate()
        {
            scoreCooldownSeconds = Mathf.Max(0f, scoreCooldownSeconds);
            // Retained as migration source values; runtime uses bounceMaterial.
            bounciness = Mathf.Clamp01(bounciness);
            friction = Mathf.Clamp01(friction);

            if (trailMinSpeed > 0f && flightTrail == null)
            {
                Debug.LogWarning(
                    $"{nameof(HoopsThrowable)} on {name} has no authored TrailRenderer. " +
                    "Check the authored Hoops throwable prefab.",
                    this);
            }
        }

        public void MarkScored()
        {
            lastScoreTime = Time.time;
        }

        public void ResetToSpawn()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            if (flightTrail != null)
            {
                flightTrail.Clear(); // no streak across the court on teleport
            }
        }
    }
}
