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
        [SerializeField] private float pressDegreesPerSecond = 600f;
        [SerializeField] private float releaseDegreesPerSecond = 360f;
        [SerializeField] private float reboundImpulse = 9f;
        [SerializeField] private float reboundCooldownSeconds = 0.08f;
        [SerializeField] private float impactBounceDegrees = 6f;
        [SerializeField] private float impactBounceReturnDegreesPerSecond = 180f;
        [SerializeField] private LayerMask ballHitLayers = ~0;
        [SerializeField] private float overlapPadding = 0.06f;

        [Header("Charge")]
        [Tooltip("Seconds after a press during which the paddle kicks balls; time it as the ball arrives.")]
        [SerializeField, Min(0.02f)] private float hotWindowSeconds = 0.18f;

        [Tooltip("Recharge after the hot window before the paddle can kick again; blocks hold/mash juggling.")]
        [SerializeField, Min(0f)] private float rechargeSeconds = 1.2f;

        [Tooltip("Tint while discharged; eases back to the authored look as charge returns.")]
        [SerializeField] private Color rechargeTint = new Color(0.4f, 0.4f, 0.45f, 1f);

        [Tooltip("Flash while the kick window is live.")]
        [SerializeField] private Color hotFlashColor = new Color(1.4f, 1.25f, 0.7f, 1f);

        [Tooltip("Renderers tinted by charge state; auto-resolved from children when empty.")]
        [SerializeField] private Renderer[] chargeRenderers;

        private readonly Dictionary<AtomSmasherBall, float> lastReboundTimes = new Dictionary<AtomSmasherBall, float>();
        private readonly HashSet<AtomSmasherBall> ballsKickedThisPress = new HashSet<AtomSmasherBall>();
        private readonly Collider[] overlapBuffer = new Collider[16];
        private InputSystem_Actions actions;
        private InputAction bumperAction;
        private InputActionMap atomSmasherMap;
        private Rigidbody pivotRigidbody;
        private Collider bumperCollider;
        private MaterialPropertyBlock chargePropertyBlock;
        private Quaternion pivotBaseRotation;
        private float currentAngle;
        private float targetAngle;
        private float impactAngleOffset;
        private float hotUntilTime = -1f;
        private float chargeReadyTime;
        private bool wasPressedLastTick;
        private bool isArmedPress;
        private bool chargeTintApplied;
        private bool hasBaseRotation;

        private void Awake()
        {
            bumperCollider = GetComponent<Collider>();

            if (chargeRenderers == null || chargeRenderers.Length == 0)
            {
                chargeRenderers = GetComponentsInChildren<Renderer>();
            }

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

        private void Update()
        {
            UpdateChargeTint();
        }

        private void OnValidate()
        {
            hotWindowSeconds = Mathf.Max(0.02f, hotWindowSeconds);
            rechargeSeconds = Mathf.Max(0f, rechargeSeconds);
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

            // The whole paddle needs charge: a discharged press neither
            // swings nor kicks, since the swing alone can juggle the ball
            // through the kinematic collision.
            bool pressed = IsPressed();
            if (pressed && !wasPressedLastTick)
            {
                isArmedPress = TryArmHotWindow();
            }
            else if (!pressed)
            {
                isArmedPress = false;
            }

            wasPressedLastTick = pressed;

            targetAngle = pressed && isArmedPress ? pressedAngle : restAngle;
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
            if (!IsHot || bumperCollider == null || !bumperCollider.enabled)
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
            if (!IsHot)
            {
                return;
            }

            if (ball == null || ball.Rigidbody == null)
            {
                return;
            }

            // One kick per ball per press: contact-hold can't machine-gun
            // impulses or life refreshes inside a single hot window.
            if (ballsKickedThisPress.Contains(ball))
            {
                return;
            }

            if (lastReboundTimes.TryGetValue(ball, out float lastReboundTime) &&
                Time.time - lastReboundTime < reboundCooldownSeconds)
            {
                return;
            }

            ballsKickedThisPress.Add(ball);
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

        private bool IsHot => Time.time < hotUntilTime;

        // A press only fires when the capacitor is charged; discharged
        // presses are swallowed entirely until the recharge completes.
        private bool TryArmHotWindow()
        {
            if (Time.time < chargeReadyTime)
            {
                return false;
            }

            hotUntilTime = Time.time + hotWindowSeconds;
            chargeReadyTime = hotUntilTime + rechargeSeconds;
            ballsKickedThisPress.Clear();
            return true;
        }

        // The paddle mesh is its own charge gauge: flash while hot, dimmed
        // while discharged, easing back to the authored look as it refills.
        private void UpdateChargeTint()
        {
            if (chargeRenderers == null || chargeRenderers.Length == 0)
            {
                return;
            }

            if (IsHot)
            {
                ApplyChargeTint(hotFlashColor);
                return;
            }

            if (Time.time >= chargeReadyTime)
            {
                if (chargeTintApplied)
                {
                    ClearChargeTint();
                }

                return;
            }

            float progress = rechargeSeconds <= 0f
                ? 1f
                : 1f - Mathf.Clamp01((chargeReadyTime - Time.time) / rechargeSeconds);
            ApplyChargeTint(Color.Lerp(rechargeTint, Color.white, progress));
        }

        private void ApplyChargeTint(Color color)
        {
            chargePropertyBlock ??= new MaterialPropertyBlock();
            chargeTintApplied = true;

            foreach (Renderer chargeRenderer in chargeRenderers)
            {
                if (chargeRenderer == null)
                {
                    continue;
                }

                chargeRenderer.GetPropertyBlock(chargePropertyBlock);
                chargePropertyBlock.SetColor("_BaseColor", color);
                chargePropertyBlock.SetColor("_Color", color);
                chargeRenderer.SetPropertyBlock(chargePropertyBlock);
            }
        }

        private void ClearChargeTint()
        {
            chargeTintApplied = false;

            foreach (Renderer chargeRenderer in chargeRenderers)
            {
                if (chargeRenderer != null)
                {
                    chargeRenderer.SetPropertyBlock(null);
                }
            }
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
