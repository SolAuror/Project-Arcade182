using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Bumper")]
    public class AtomSmasherBumper : MonoBehaviour
    {
        private enum BumperSide
        {
            Left,
            Right
        }

        [SerializeField] private BumperSide side = BumperSide.Left;
        [SerializeField] private Transform bumperPivot;
        [SerializeField] private float restAngle;
        [SerializeField] private float pressedAngle = 42f;
        [SerializeField] private float pressDegreesPerSecond = 720f;
        [SerializeField] private float releaseDegreesPerSecond = 420f;
        [SerializeField] private float reboundImpulse = 9f;
        [SerializeField] private float reboundCooldownSeconds = 0.08f;
        [SerializeField] private float impactBounceDegrees = 6f;
        [SerializeField] private float impactBounceReturnDegreesPerSecond = 180f;
        [SerializeField] private LayerMask ballHitLayers = ~0;
        [SerializeField] private float overlapPadding = 0.06f;

        private readonly Dictionary<AtomSmasherBall, float> lastReboundTimes = new Dictionary<AtomSmasherBall, float>();
        private readonly Collider[] overlapBuffer = new Collider[16];
        private InputSystem_Actions actions;
        private InputAction bumperAction;
        private InputActionMap atomSmasherMap;
        private Rigidbody pivotRigidbody;
        private Collider bumperCollider;
        private Quaternion pivotBaseRotation;
        private float currentAngle;
        private float targetAngle;
        private float impactAngleOffset;
        private bool hasBaseRotation;

        private void Awake()
        {
            bumperCollider = GetComponent<Collider>();
            ResolvePivot();
            ResolvePivotRigidbody();
            CaptureBaseRotation();
            currentAngle = restAngle;
            targetAngle = restAngle;
            ApplyPivotRotation();
        }

        private void OnEnable()
        {
            if (actions == null)
            {
                actions = new InputSystem_Actions();
            }

            bumperAction = side == BumperSide.Left
                ? actions.AtomSmasher.LeftBumper
                : actions.AtomSmasher.RightBumper;
            atomSmasherMap = actions.AtomSmasher.Get();
            atomSmasherMap.Enable();
        }

        private void OnDisable()
        {
            atomSmasherMap?.Disable();
            bumperAction = null;
            atomSmasherMap = null;
        }

        private void OnDestroy()
        {
            actions?.Dispose();
            actions = null;
        }

        private void OnValidate()
        {
            pressDegreesPerSecond = Mathf.Max(0f, pressDegreesPerSecond);
            releaseDegreesPerSecond = Mathf.Max(0f, releaseDegreesPerSecond);
            reboundImpulse = Mathf.Max(0f, reboundImpulse);
            reboundCooldownSeconds = Mathf.Max(0f, reboundCooldownSeconds);
            impactBounceDegrees = Mathf.Max(0f, impactBounceDegrees);
            impactBounceReturnDegreesPerSecond = Mathf.Max(0f, impactBounceReturnDegreesPerSecond);
            overlapPadding = Mathf.Max(0f, overlapPadding);
        }

        private void FixedUpdate()
        {
            ResolvePivot();
            ResolvePivotRigidbody();
            CaptureBaseRotation();

            targetAngle = bumperAction != null && bumperAction.IsPressed() ? pressedAngle : restAngle;
            float speed = Mathf.Abs(targetAngle - currentAngle) > 0.01f && targetAngle != restAngle
                ? pressDegreesPerSecond
                : releaseDegreesPerSecond;

            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, speed * Time.fixedDeltaTime);
            impactAngleOffset = Mathf.MoveTowards(impactAngleOffset, 0f, impactBounceReturnDegreesPerSecond * Time.fixedDeltaTime);
            ApplyPivotRotation();
            ScanOverlappingBalls();
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryHandleBallContact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryHandleBallContact(collision);
        }

        private void ResolvePivot()
        {
            if (bumperPivot == null)
            {
                bumperPivot = transform.parent;
            }
        }

        private void ResolvePivotRigidbody()
        {
            if (bumperPivot == null)
            {
                pivotRigidbody = null;
                return;
            }

            if (pivotRigidbody == null || pivotRigidbody.transform != bumperPivot)
            {
                pivotRigidbody = bumperPivot.GetComponent<Rigidbody>();
            }

            ConfigurePivotRigidbody();
        }

        private void ConfigurePivotRigidbody()
        {
            if (pivotRigidbody == null)
            {
                return;
            }

            pivotRigidbody.useGravity = false;
            pivotRigidbody.isKinematic = true;
            pivotRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            pivotRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            pivotRigidbody.constraints = RigidbodyConstraints.FreezePositionX |
                                         RigidbodyConstraints.FreezePositionY |
                                         RigidbodyConstraints.FreezePositionZ |
                                         RigidbodyConstraints.FreezeRotationX |
                                         RigidbodyConstraints.FreezeRotationY;
        }

        private void CaptureBaseRotation()
        {
            if (hasBaseRotation || bumperPivot == null)
            {
                return;
            }

            pivotBaseRotation = bumperPivot.localRotation;
            hasBaseRotation = true;
        }

        private void ApplyPivotRotation()
        {
            if (bumperPivot == null || !hasBaseRotation)
            {
                return;
            }

            Quaternion localRotation = pivotBaseRotation * Quaternion.Euler(0f, 0f, currentAngle + impactAngleOffset);

            if (pivotRigidbody == null)
            {
                bumperPivot.localRotation = localRotation;
                return;
            }

            Quaternion worldRotation = bumperPivot.parent != null
                ? bumperPivot.parent.rotation * localRotation
                : localRotation;

            pivotRigidbody.MoveRotation(worldRotation);
        }

        private void TryHandleBallContact(Collision collision)
        {
            AtomSmasherBall ball = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<AtomSmasherBall>()
                : collision.collider.GetComponentInParent<AtomSmasherBall>();

            TryApplyRebound(ball);
        }

        private void ScanOverlappingBalls()
        {
            if (!IsPressed() || bumperCollider == null || !bumperCollider.enabled)
            {
                return;
            }

            int hitCount = GetOverlapHits();
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = overlapBuffer[i];
                if (hitCollider == null || hitCollider == bumperCollider)
                {
                    continue;
                }

                AtomSmasherBall ball = hitCollider.GetComponentInParent<AtomSmasherBall>();
                TryApplyRebound(ball);
            }
        }

        private int GetOverlapHits()
        {
            if (bumperCollider is BoxCollider boxCollider)
            {
                Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, Abs(boxCollider.transform.lossyScale));
                halfExtents += Vector3.one * overlapPadding;

                return Physics.OverlapBoxNonAlloc(
                    boxCollider.transform.TransformPoint(boxCollider.center),
                    halfExtents,
                    overlapBuffer,
                    boxCollider.transform.rotation,
                    ballHitLayers,
                    QueryTriggerInteraction.Ignore);
            }

            Bounds bounds = bumperCollider.bounds;
            float radius = bounds.extents.magnitude + overlapPadding;
            return Physics.OverlapSphereNonAlloc(bounds.center, radius, overlapBuffer, ballHitLayers, QueryTriggerInteraction.Ignore);
        }

        private void TryApplyRebound(AtomSmasherBall ball)
        {
            if (!IsPressed())
            {
                return;
            }

            if (ball == null || ball.Rigidbody == null)
            {
                return;
            }

            if (lastReboundTimes.TryGetValue(ball, out float lastReboundTime) &&
                Time.time - lastReboundTime < reboundCooldownSeconds)
            {
                return;
            }

            lastReboundTimes[ball] = Time.time;
            ball.ResetDecayTimer();
            TriggerImpactBounce();

            Vector3 origin = bumperPivot != null ? bumperPivot.position : transform.position;
            Vector3 direction = ball.transform.position - origin;
            direction.z = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = transform.up;
                direction.z = 0f;
            }

            ball.Rigidbody.AddForce(direction.normalized * reboundImpulse, ForceMode.Impulse);
        }

        private bool IsPressed()
        {
            return bumperAction != null && bumperAction.IsPressed();
        }

        private void TriggerImpactBounce()
        {
            if (impactBounceDegrees <= 0f)
            {
                return;
            }

            float configuredPressDelta = pressedAngle - restAngle;
            float pressDirection = Mathf.Sign(configuredPressDelta);
            if (Mathf.Approximately(configuredPressDelta, 0f))
            {
                pressDirection = side == BumperSide.Left ? 1f : -1f;
            }

            impactAngleOffset = -pressDirection * impactBounceDegrees;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
