using System;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public enum AirFootyTeam
{
    None = 0,
    Blue = 1,
    Red = 2,
    Green = 3,
    Gold = 4,
    Player = Blue,
    AI = Red
}

public enum AirFootyTouchType
{
    None,
    Passive,
    TapKick,
    ChargedKick,
    DashKick
}

[RequireComponent(typeof(Rigidbody))]
public class BallController3D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float launchSpeed = 3.1f;
    [SerializeField, Min(0f)] private float linearDamping = 0.035f;
    [SerializeField, Min(0f)] private float stalledSpeedThreshold = 0.4f;
    [SerializeField, Min(0.1f)] private float stalledDuration = 1.25f;
    [SerializeField, Min(0.1f)] private float stalledNearStrikerDuration = 3f;
    [SerializeField, Min(0f)] private float stalledStrikerProximity = 1.35f;
    [FormerlySerializedAs("maximumSpeed")]
    [SerializeField, Min(0.1f)] private float ordinaryMaximumSpeed = 12f;
    [SerializeField, Min(0f)] private float passiveContactMaximumSpeed = 4.5f;
    [SerializeField, Min(0f)] private float movementContactMinimumSpeed = 2.4f;
    [SerializeField, Min(0.1f)] private float movementContactFullApproachSpeed = 8f;
    [SerializeField, Min(0f)] private float movementContactMinimumApproachSpeed = 0.6f;
    [SerializeField, Range(0f, 1f)] private float movementContactRadialBlend = 0.35f;
    [SerializeField, Range(0f, 1f)] private float movementContactTangentRetention = 0.65f;
    [SerializeField, Range(0f, 1f)] private float passiveContactMomentumRetention = 0.96f;
    [SerializeField, Range(0f, 1f)] private float pulseTangentRetention = 0.94f;
    [SerializeField, Min(0f)] private float movementContactRetriggerSeconds = 0.08f;

    [Header("Collision Reliability")]
    [SerializeField, Min(1)] private int solverIterations = 16;
    [SerializeField, Min(1)] private int solverVelocityIterations = 12;
    [SerializeField, Min(0f)] private float maximumDepenetrationVelocity = 32f;
    [SerializeField, Min(0f)] private float ballContactOffset = 0.01f;
    [SerializeField, Min(0f)] private float arenaBoundaryContactOffset = 0.018f;
    [SerializeField, Min(0f)] private float wallSweepSkin = 0.01f;
    [SerializeField, Range(0f, 1f)] private float wallSweepRestitution = 0.98f;

    [Header("Air-Hockey Feel")]
    [SerializeField, Min(0f)] private float trailMinSpeed = 3.5f;
    [SerializeField] private Color trailColor = new Color(0.25f, 0.85f, 1f, 0.75f);
    [SerializeField, Min(0f)] private float hardImpactSpeed = 6.5f;
    [SerializeField, Range(0f, 1f)] private float hardImpactCameraTrauma = 0.08f;

    private Rigidbody ballBody;
    private SphereCollider ballCollider;
    private Vector3 startingPosition;
    private Renderer ballRenderer;
    private TrailRenderer speedTrail;
    private AudioSource impactAudio;
    private AirFootyCameraFx cameraFx;
    private bool canMove;
    private bool stallReported;
    private float stalledTimer;
    private float lastStrikeFixedTime = float.NegativeInfinity;
    private float activeStrikeContactUntil = float.NegativeInfinity;
    private AirFootyTeam activeStrikeTeam;
    private float nextPlayerMovementContactTime;
    private float nextAiMovementContactTime;
    private float rallyMaximumSpeed = float.PositiveInfinity;
    private Vector3 preSimulationPlanarVelocity;
    private bool overtimeLethal;
    private readonly Collider[] stalledProximityResults = new Collider[8];

    public event Action Stalled;
    public event Action<AirFootyTeam, AirFootyTouchType> DeliberateStrike;
    public event Action<Collision> CollisionEntered;
    public event Action ShotSequenceReset;
    public event Action PlayStopped;

    /// <summary>Victim team, then the team that armed the ball.</summary>
    public event Action<AirFootyTeam, AirFootyTeam> LethalContact;

    /// <summary>
    /// The team whose pulse armed this ball. Only a deliberate strike sets it, so
    /// a player who is merely shoved into the ball never inherits ownership.
    /// </summary>
    public AirFootyTeam ArmedOwner { get; private set; }

    /// <summary>
    /// True once overtime is live and somebody has claimed the ball. Until then
    /// the ball is inert and can be walked into safely.
    /// </summary>
    public bool IsLethal =>
        overtimeLethal && canMove && ArmedOwner != AirFootyTeam.None;

    public bool CanMove => canMove;
    public float OrdinaryMaximumSpeed => ordinaryMaximumSpeed;
    public float CurrentMaximumSpeed =>
        Mathf.Min(ordinaryMaximumSpeed, rallyMaximumSpeed);
    public Vector3 PlanarVelocity => ballBody != null
        ? new Vector3(ballBody.linearVelocity.x, 0f, ballBody.linearVelocity.z)
        : Vector3.zero;
    public AirFootyTeam LastTouchTeam { get; private set; }
    public AirFootyTouchType LastTouchType { get; private set; }
    public float LastTouchTime { get; private set; } = float.NegativeInfinity;

    private void Awake()
    {
        ballBody = GetComponent<Rigidbody>();
        ballCollider = GetComponent<SphereCollider>();
        startingPosition = ballBody.position;
        ballBody.useGravity = false;
        ballRenderer = GetComponent<Renderer>();
        ConfigurePhysics();
        ConfigureArenaBoundaryColliders();
        BuildSpeedTrail();
        BuildImpactAudio();
        BuildHoverPresentation();
    }

    private void FixedUpdate()
    {
        if (!canMove) return;

        Vector3 flatVelocity = new Vector3(ballBody.linearVelocity.x, 0f, ballBody.linearVelocity.z);
        float speed = flatVelocity.magnitude;

        if (!Mathf.Approximately(ballBody.linearVelocity.y, 0f))
        {
            ballBody.linearVelocity = flatVelocity;
        }

        float currentMaximumSpeed = CurrentMaximumSpeed;
        if (speed > currentMaximumSpeed)
        {
            ballBody.linearVelocity = flatVelocity.normalized * currentMaximumSpeed;
            speed = currentMaximumSpeed;
        }

        PreventStaticWallTunnelling();
        flatVelocity = new Vector3(ballBody.linearVelocity.x, 0f, ballBody.linearVelocity.z);
        speed = flatVelocity.magnitude;
        preSimulationPlanarVelocity = flatVelocity;
        UpdateStallDetection(speed);
    }

    private void Update()
    {
        if (speedTrail != null)
        {
            speedTrail.emitting = canMove &&
                                  ballBody.linearVelocity.sqrMagnitude >= trailMinSpeed * trailMinSpeed;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        AirFootyTeam strikerTeam = ResolveStrikerTeam(collision.collider);

        // An armed ball vaporises whoever it reaches, including the team that
        // armed it. This returns before the passive-touch bookkeeping below on
        // purpose: registering the victim would overwrite LastTouchTeam and the
        // goal would be credited to the player who just died.
        if (strikerTeam != AirFootyTeam.None && IsLethal)
        {
            LethalContact?.Invoke(strikerTeam, ArmedOwner);
            return;
        }

        if (strikerTeam != AirFootyTeam.None &&
            !IsActiveStrikeCollision(strikerTeam))
        {
            PreservePassiveImpactMomentum(collision);
            RegisterTouch(strikerTeam, AirFootyTouchType.Passive);
        }

        PlayImpactFeedback(speed);
        CollisionEntered?.Invoke(collision);
    }

    /// <summary>
    /// Arms or disarms the overtime contingency for this ball. Turning it on
    /// leaves the ball inert: it only becomes lethal once somebody pulses it.
    /// </summary>
    public void SetOvertimeLethal(bool lethal)
    {
        overtimeLethal = lethal;
        if (!lethal)
        {
            ArmedOwner = AirFootyTeam.None;
        }
    }

    /// <summary>
    /// Marks the instant an inert ball goes live, in the colour of whoever claimed
    /// it. Without this the switch from safe to lethal is invisible.
    /// </summary>
    private void FlashArmed(AirFootyTeam owner)
    {
        if (ballRenderer == null || !isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(AirFootyFeedbackUtility.FlashRenderer(
            ballRenderer,
            AirFootyTeamMember3D.ColorFor(owner),
            0.18f));
    }

    private void PlayImpactFeedback(float speed)
    {
        if (speed < 1.5f)
        {
            return;
        }

        if (impactAudio != null)
        {
            impactAudio.pitch = Mathf.Lerp(
                0.8f,
                1.35f,
                Mathf.InverseLerp(1.5f, ordinaryMaximumSpeed, speed));
            impactAudio.volume = Mathf.Lerp(
                0.08f,
                0.28f,
                Mathf.InverseLerp(1.5f, ordinaryMaximumSpeed, speed));
            impactAudio.PlayOneShot(AirFootyFeedbackUtility.ImpactClip);
        }

        if (speed >= hardImpactSpeed)
        {
            ResolveCameraFx();
            cameraFx?.AddTrauma(hardImpactCameraTrauma);
            if (ballRenderer != null)
            {
                StartCoroutine(AirFootyFeedbackUtility.FlashRenderer(
                    ballRenderer,
                    new Color(0.35f, 0.9f, 1f, 1f),
                    0.08f));
            }
        }
    }

    private void PreservePassiveImpactMomentum(Collision collision)
    {
        Vector3 outgoingVelocity = ballBody.linearVelocity;
        outgoingVelocity.y = 0f;
        float incomingSpeed = preSimulationPlanarVelocity.magnitude;
        float outgoingSpeed = outgoingVelocity.magnitude;

        if (incomingSpeed <= passiveContactMaximumSpeed)
        {
            if (outgoingSpeed > passiveContactMaximumSpeed)
            {
                ballBody.linearVelocity =
                    outgoingVelocity.normalized * passiveContactMaximumSpeed;
            }
            return;
        }

        float retainedSpeed = Mathf.Min(
            incomingSpeed * passiveContactMomentumRetention,
            CurrentMaximumSpeed);
        if (outgoingSpeed >= retainedSpeed)
        {
            return;
        }

        Vector3 outgoingDirection;
        if (outgoingSpeed > 0.01f)
        {
            // Preserve the solver's deflection while restoring lost pace.
            outgoingDirection = outgoingVelocity / outgoingSpeed;
        }
        else
        {
            Vector3 normal = collision.contactCount > 0
                ? collision.GetContact(0).normal
                : -preSimulationPlanarVelocity.normalized;
            normal.y = 0f;
            outgoingDirection = normal.sqrMagnitude > 0.0001f
                ? Vector3.Reflect(
                    preSimulationPlanarVelocity.normalized,
                    normal.normalized)
                : -preSimulationPlanarVelocity.normalized;
        }

        ballBody.linearVelocity = outgoingDirection * retainedSpeed;
    }

    public void StopBall()
    {
        canMove = false;
        ResetStallDetection();
        preSimulationPlanarVelocity = Vector3.zero;
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        if (speedTrail != null)
        {
            speedTrail.emitting = false;
        }
        PlayStopped?.Invoke();
    }

    public void ResetBall()
    {
        PrepareKickoff();
        LaunchBall();
    }

    public void PrepareKickoff()
    {
        ballBody.position = startingPosition;
        ballBody.rotation = Quaternion.identity;
        preSimulationPlanarVelocity = Vector3.zero;
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        canMove = false;
        ClearTouchMetadata();
        ResetStallDetection();
        ShotSequenceReset?.Invoke();
        if (speedTrail != null)
        {
            speedTrail.emitting = false;
            speedTrail.Clear();
        }
    }

    public void LaunchBall(float horizontalDirection = 0f)
    {
        canMove = true;
        ResetStallDetection();
        ballBody.linearVelocity = RandomLaunchDirection(horizontalDirection) * launchSpeed;
    }

    public bool ApplyStrike(
        AirFootyTeam team,
        AirFootyTouchType touchType,
        Vector3 direction,
        float targetSpeed)
    {
        // Overtime is pulse only. Refusing here covers the player's dash kick and
        // both AI controllers in one place.
        if (overtimeLethal ||
            !canMove ||
            team == AirFootyTeam.None ||
            touchType == AirFootyTouchType.None ||
            touchType == AirFootyTouchType.Passive ||
            Mathf.Approximately(Time.fixedTime, lastStrikeFixedTime))
        {
            return false;
        }

        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        lastStrikeFixedTime = Time.fixedTime;
        activeStrikeTeam = team;
        activeStrikeContactUntil = Time.fixedTime + Mathf.Max(0.05f, Time.fixedDeltaTime * 1.5f);
        ArmedOwner = team;
        RegisterTouch(team, touchType);
        ResetStallDetection();
        DeliberateStrike?.Invoke(team, touchType);

        float speed = Mathf.Clamp(targetSpeed, 0f, CurrentMaximumSpeed);
        ballBody.linearVelocity = flatDirection.normalized * speed;
        ballBody.angularVelocity = Vector3.zero;

        return true;
    }

    public void SetRallyPresentation(float maximumSpeed, Color color)
    {
        rallyMaximumSpeed = Mathf.Max(0.1f, maximumSpeed);
        if (speedTrail != null)
        {
            speedTrail.startColor = color;
            speedTrail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        CapPlanarSpeed(CurrentMaximumSpeed);
    }

    public bool ApplyMovementContact(
        AirFootyTeam team,
        Vector3 strikerVelocity,
        Vector3 strikerPosition)
    {
        // Overtime is pulse only: bodies no longer shove the ball around, and an
        // armed ball kills on contact instead.
        if (overtimeLethal ||
            !canMove ||
            team == AirFootyTeam.None ||
            IsActiveStrikeCollision(team))
        {
            return false;
        }

        ref float nextContactTime = ref (
            team == AirFootyTeam.Player
                ? ref nextPlayerMovementContactTime
                : ref nextAiMovementContactTime);
        if (Time.fixedTime < nextContactTime)
        {
            return false;
        }

        strikerVelocity.y = 0f;
        float strikerSpeed = strikerVelocity.magnitude;
        if (strikerSpeed <= 0.001f)
        {
            return false;
        }

        Vector3 toBall = ballBody.position - strikerPosition;
        toBall.y = 0f;
        if (toBall.sqrMagnitude <= 0.0001f)
        {
            toBall = strikerVelocity;
        }
        toBall.Normalize();

        float approachSpeed = Vector3.Dot(strikerVelocity, toBall);
        if (approachSpeed < movementContactMinimumApproachSpeed)
        {
            return false;
        }

        float drive = Mathf.InverseLerp(
            movementContactMinimumApproachSpeed,
            movementContactFullApproachSpeed,
            approachSpeed);
        float targetSpeed = Mathf.Lerp(
            movementContactMinimumSpeed,
            passiveContactMaximumSpeed,
            drive);
        Vector3 movementDirection = strikerVelocity / strikerSpeed;
        Vector3 contactDirection = Vector3.Slerp(
            movementDirection,
            toBall,
            movementContactRadialBlend).normalized;

        Vector3 existingVelocity = ballBody.linearVelocity;
        existingVelocity.y = 0f;
        float existingSpeed = existingVelocity.magnitude;

        // Passive contact may create low-speed dribble energy, but it must not
        // turn a fast shot into an easy catch. Let PhysX resolve that impact.
        if (existingSpeed > passiveContactMaximumSpeed)
        {
            nextContactTime =
                Time.fixedTime + movementContactRetriggerSeconds;
            RegisterTouch(team, AirFootyTouchType.Passive);
            ResetStallDetection();
            return false;
        }

        Vector3 retainedTangent =
            (existingVelocity - Vector3.Project(existingVelocity, contactDirection)) *
            movementContactTangentRetention;
        Vector3 result = contactDirection * targetSpeed + retainedTangent;
        if (result.sqrMagnitude > passiveContactMaximumSpeed * passiveContactMaximumSpeed)
        {
            result = result.normalized * passiveContactMaximumSpeed;
        }
        else
        {
            float retainedSpeed = existingSpeed * passiveContactMomentumRetention;
            if (result.sqrMagnitude < retainedSpeed * retainedSpeed)
            {
                result = result.sqrMagnitude > 0.0001f
                    ? result.normalized * retainedSpeed
                    : existingVelocity * passiveContactMomentumRetention;
            }
        }

        nextContactTime = Time.fixedTime + movementContactRetriggerSeconds;
        ballBody.linearVelocity = result;
        ballBody.angularVelocity = Vector3.zero;
        RegisterTouch(team, AirFootyTouchType.Passive);
        ResetStallDetection();
        return true;
    }

    public bool ApplyPulse(
        AirFootyTeam team,
        Vector3 pulseOrigin,
        float radius,
        float impulse,
        AirFootyTouchType touchType)
    {
        if (!canMove ||
            team == AirFootyTeam.None ||
            radius <= 0f ||
            impulse <= 0f ||
            Mathf.Approximately(Time.fixedTime, lastStrikeFixedTime))
        {
            return false;
        }

        Vector3 outward = ballBody.position - pulseOrigin;
        outward.y = 0f;
        float distance = outward.magnitude;
        if (distance > radius || distance <= 0.0001f)
        {
            return false;
        }
        outward /= distance;

        lastStrikeFixedTime = Time.fixedTime;
        activeStrikeTeam = team;
        activeStrikeContactUntil =
            Time.fixedTime + Mathf.Max(0.05f, Time.fixedDeltaTime * 1.5f);
        // In overtime this is the moment an inert ball goes live, and every later
        // pulse hands the kill credit to whoever touched it last.
        bool armingNow = overtimeLethal && ArmedOwner == AirFootyTeam.None;
        ArmedOwner = team;
        if (armingNow)
        {
            FlashArmed(team);
        }
        RegisterTouch(team, touchType);
        ResetStallDetection();
        DeliberateStrike?.Invoke(team, touchType);

        Vector3 existingVelocity = ballBody.linearVelocity;
        existingVelocity.y = 0f;
        float existingOutwardSpeed =
            Mathf.Max(0f, Vector3.Dot(existingVelocity, outward));
        Vector3 tangent =
            existingVelocity - outward * Vector3.Dot(existingVelocity, outward);
        Vector3 result =
            outward * (existingOutwardSpeed + impulse) +
            tangent * pulseTangentRetention;
        float cap = CurrentMaximumSpeed;
        if (result.sqrMagnitude > cap * cap)
        {
            result = result.normalized * cap;
        }

        ballBody.linearVelocity = result;
        ballBody.angularVelocity = Vector3.zero;
        return true;
    }

    private Vector3 RandomLaunchDirection(float horizontalDirection = 0f)
    {
        float horizontal = Mathf.Abs(horizontalDirection) > 0.01f
            ? Mathf.Sign(horizontalDirection)
            : Random.value < 0.5f ? -1f : 1f;
        float vertical = Random.Range(-0.75f, 0.75f);
        return new Vector3(horizontal, 0f, vertical).normalized;
    }

    private void ConfigurePhysics()
    {
        ballBody.linearDamping = linearDamping;
        ballBody.solverIterations = solverIterations;
        ballBody.solverVelocityIterations = solverVelocityIterations;
        ballBody.maxDepenetrationVelocity = maximumDepenetrationVelocity;
        ballBody.interpolation = RigidbodyInterpolation.Interpolate;
        ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ballBody.constraints |= RigidbodyConstraints.FreezePositionY;
        if (ballCollider != null)
        {
            ballCollider.contactOffset =
                Mathf.Max(0.001f, ballContactOffset);
        }
    }

    private void ConfigureArenaBoundaryColliders()
    {
        Collider[] colliders =
            transform.root.GetComponentsInChildren<Collider>(true);
        PhysicsMaterial bounceMaterial =
            ballCollider != null
                ? ballCollider.sharedMaterial
                : null;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider boundary = colliders[i];
            if (boundary == null ||
                boundary.isTrigger ||
                !AirFootyArenaMovement3D.IsAuthoredArenaBoundary(boundary))
            {
                continue;
            }

            boundary.contactOffset =
                Mathf.Max(0.001f, arenaBoundaryContactOffset);
            if (boundary.sharedMaterial == null &&
                bounceMaterial != null)
            {
                boundary.sharedMaterial = bounceMaterial;
            }
        }
    }

    private void PreventStaticWallTunnelling()
    {
        Vector3 flatVelocity = new Vector3(
            ballBody.linearVelocity.x,
            0f,
            ballBody.linearVelocity.z);
        float speed = flatVelocity.magnitude;
        if (speed < 0.01f)
        {
            return;
        }

        Vector3 direction = flatVelocity / speed;
        float sweepDistance = speed * Time.fixedDeltaTime + wallSweepSkin;
        if (!ballBody.SweepTest(
                direction,
                out RaycastHit hit,
                sweepDistance,
                QueryTriggerInteraction.Ignore) ||
            !IsStaticArenaWall(hit, direction))
        {
            return;
        }

        Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
        if (wallNormal.sqrMagnitude < 0.01f)
        {
            return;
        }

        float safeDistance = Mathf.Max(0f, hit.distance - wallSweepSkin);
        ballBody.position += direction * safeDistance;
        ballBody.linearVelocity =
            Vector3.Reflect(flatVelocity, wallNormal.normalized) * wallSweepRestitution;
        PlayImpactFeedback(speed);
    }

    private bool IsStaticArenaWall(
        RaycastHit hit,
        Vector3 movementDirection)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null ||
            hitCollider.isTrigger ||
            hit.rigidbody != null ||
            !AirFootyArenaMovement3D.IsAuthoredArenaBoundary(hitCollider) ||
            AirFootyArenaMovement3D.IsGoalBack(hitCollider))
        {
            return false;
        }

        return ballCollider == null ||
               !AirFootyArenaMovement3D.IsMovingAwayFromBoundary(
                   ballCollider,
                   hitCollider,
                   ballBody.worldCenterOfMass,
                   movementDirection,
                   hit,
                   wallSweepSkin,
                   true);
    }

    private void UpdateStallDetection(float speed)
    {
        if (speed >= stalledSpeedThreshold)
        {
            ResetStallDetection();
            return;
        }

        stalledTimer += Time.fixedDeltaTime;
        float requiredDuration = IsWithinStrikerControlRange()
            ? stalledNearStrikerDuration
            : stalledDuration;
        if (stallReported || stalledTimer < requiredDuration)
        {
            return;
        }

        stallReported = true;
        canMove = false;
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        Stalled?.Invoke();
    }

    private bool IsWithinStrikerControlRange()
    {
        if (stalledStrikerProximity <= 0f)
        {
            return false;
        }

        Vector3 ballPosition = ballBody.position;
        int hitCount = Physics.OverlapSphereNonAlloc(
            ballPosition,
            stalledStrikerProximity,
            stalledProximityResults,
            ~0,
            QueryTriggerInteraction.Ignore);
        float proximitySquared = stalledStrikerProximity * stalledStrikerProximity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = stalledProximityResults[i];
            if (hit == null)
            {
                continue;
            }

            Transform striker = null;
            PlayerMovement3D player = hit.GetComponentInParent<PlayerMovement3D>();
            if (player != null)
            {
                striker = player.transform;
            }
            else
            {
                AIPlayer3D ai = hit.GetComponentInParent<AIPlayer3D>();
                if (ai != null)
                {
                    striker = ai.transform;
                }
            }

            if (striker == null)
            {
                continue;
            }

            Vector3 toStriker = striker.position - ballPosition;
            toStriker.y = 0f;
            if (toStriker.sqrMagnitude <= proximitySquared)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetStallDetection()
    {
        stalledTimer = 0f;
        stallReported = false;
    }

    private void RegisterTouch(AirFootyTeam team, AirFootyTouchType touchType)
    {
        LastTouchTeam = team;
        LastTouchType = touchType;
        LastTouchTime = Time.time;
    }

    private void ClearTouchMetadata()
    {
        LastTouchTeam = AirFootyTeam.None;
        LastTouchType = AirFootyTouchType.None;
        LastTouchTime = float.NegativeInfinity;
        // A re-dropped ball goes back to inert, so the victim gets a safe window
        // and whoever pulses first owns the threat again.
        ArmedOwner = AirFootyTeam.None;
        activeStrikeTeam = AirFootyTeam.None;
        activeStrikeContactUntil = float.NegativeInfinity;
        nextPlayerMovementContactTime = float.NegativeInfinity;
        nextAiMovementContactTime = float.NegativeInfinity;
    }

    private bool IsActiveStrikeCollision(AirFootyTeam strikerTeam)
    {
        return strikerTeam == activeStrikeTeam &&
               Time.fixedTime <= activeStrikeContactUntil;
    }

    private static AirFootyTeam ResolveStrikerTeam(Collider collider)
    {
        AirFootyTeamMember3D member =
            collider.GetComponentInParent<AirFootyTeamMember3D>();
        if (member != null && member.Team != AirFootyTeam.None)
        {
            return member.Team;
        }

        if (collider.GetComponentInParent<PlayerMovement3D>() != null)
        {
            return AirFootyTeam.Player;
        }

        return collider.GetComponentInParent<AIPlayer3D>() != null
            ? AirFootyTeam.AI
            : AirFootyTeam.None;
    }

    private void CapPlanarSpeed(float speedCap)
    {
        Vector3 flatVelocity = new Vector3(
            ballBody.linearVelocity.x,
            0f,
            ballBody.linearVelocity.z);
        float cap = Mathf.Min(Mathf.Max(0f, speedCap), CurrentMaximumSpeed);
        if (flatVelocity.sqrMagnitude > cap * cap)
        {
            ballBody.linearVelocity = flatVelocity.normalized * cap;
        }
        else if (!Mathf.Approximately(ballBody.linearVelocity.y, 0f))
        {
            ballBody.linearVelocity = flatVelocity;
        }
    }

    private void BuildSpeedTrail()
    {
        speedTrail = GetComponent<TrailRenderer>();
        if (speedTrail == null)
        {
            Debug.LogError("AirFooty ball is missing its authored TrailRenderer.", this);
            return;
        }

        speedTrail.time = 0.2f;
        speedTrail.startWidth = 0.18f;
        speedTrail.endWidth = 0f;
        speedTrail.startColor = trailColor;
        speedTrail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        speedTrail.minVertexDistance = 0.04f;
        speedTrail.emitting = false;
        speedTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (speedTrail.sharedMaterial == null)
        {
            Debug.LogError("AirFooty ball TrailRenderer is missing its authored material.", speedTrail);
        }
    }

    private void BuildImpactAudio()
    {
        impactAudio = GetComponent<AudioSource>();
        if (impactAudio == null)
        {
            Debug.LogError("AirFooty ball is missing its authored impact AudioSource.", this);
            return;
        }
        impactAudio.playOnAwake = false;
        impactAudio.spatialBlend = 0.35f;
        impactAudio.dopplerLevel = 0f;
    }

    private void BuildHoverPresentation()
    {
        Transform authoredVisual = transform.Find("AirFooty Ball Hover");
        if (authoredVisual == null)
        {
            Debug.LogError("AirFooty ball is missing its authored AirFooty Ball Hover child.", this);
            return;
        }
        AirFootyHoverVisual hover = authoredVisual.GetComponent<AirFootyHoverVisual>();
        if (hover == null)
        {
            Debug.LogError("AirFooty Ball Hover is missing its authored AirFootyHoverVisual component.", authoredVisual);
            return;
        }
        hover.Initialize(transform, startingPosition.y, trailColor);
    }

    private void ResolveCameraFx()
    {
        if (cameraFx == null)
        {
            Camera displayCamera = AirFootyCameraLookup.FindDisplayCamera();
            if (displayCamera != null)
            {
                cameraFx = displayCamera.GetComponent<AirFootyCameraFx>();
            }
        }
    }

    private void OnValidate()
    {
        launchSpeed = Mathf.Max(0f, launchSpeed);
        linearDamping = Mathf.Max(0f, linearDamping);
        stalledSpeedThreshold = Mathf.Max(0f, stalledSpeedThreshold);
        stalledDuration = Mathf.Max(0.1f, stalledDuration);
        stalledNearStrikerDuration = Mathf.Max(stalledDuration, stalledNearStrikerDuration);
        stalledStrikerProximity = Mathf.Max(0f, stalledStrikerProximity);
        ordinaryMaximumSpeed = Mathf.Max(0.1f, ordinaryMaximumSpeed);
        passiveContactMaximumSpeed =
            Mathf.Clamp(passiveContactMaximumSpeed, 0f, ordinaryMaximumSpeed);
        movementContactMinimumSpeed =
            Mathf.Clamp(movementContactMinimumSpeed, 0f, passiveContactMaximumSpeed);
        movementContactFullApproachSpeed =
            Mathf.Max(0.1f, movementContactFullApproachSpeed);
        movementContactMinimumApproachSpeed =
            Mathf.Clamp(
                movementContactMinimumApproachSpeed,
                0f,
                movementContactFullApproachSpeed);
        movementContactRadialBlend = Mathf.Clamp01(movementContactRadialBlend);
        movementContactTangentRetention =
            Mathf.Clamp01(movementContactTangentRetention);
        passiveContactMomentumRetention =
            Mathf.Clamp01(passiveContactMomentumRetention);
        pulseTangentRetention = Mathf.Clamp01(pulseTangentRetention);
        movementContactRetriggerSeconds = Mathf.Max(0f, movementContactRetriggerSeconds);
        solverIterations = Mathf.Max(1, solverIterations);
        solverVelocityIterations = Mathf.Max(1, solverVelocityIterations);
        maximumDepenetrationVelocity = Mathf.Max(0f, maximumDepenetrationVelocity);
        ballContactOffset = Mathf.Max(0f, ballContactOffset);
        arenaBoundaryContactOffset =
            Mathf.Max(0f, arenaBoundaryContactOffset);
        wallSweepSkin = Mathf.Max(0f, wallSweepSkin);
        wallSweepRestitution = Mathf.Clamp01(wallSweepRestitution);
        trailMinSpeed = Mathf.Max(0f, trailMinSpeed);
        hardImpactSpeed = Mathf.Max(0f, hardImpactSpeed);
    }
}
