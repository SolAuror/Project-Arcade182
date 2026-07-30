using System;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public enum AirFootyTeam
{
    None,
    Player,
    AI
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
    [SerializeField, Min(0f)] private float launchSpeed = 2.5f;
    [SerializeField, Min(0f)] private float linearDamping = 0.18f;
    [SerializeField, Min(0f)] private float stalledSpeedThreshold = 0.4f;
    [SerializeField, Min(0.1f)] private float stalledDuration = 1.25f;
    [SerializeField, Min(0.1f)] private float stalledNearStrikerDuration = 3f;
    [SerializeField, Min(0f)] private float stalledStrikerProximity = 1.35f;
    [FormerlySerializedAs("maximumSpeed")]
    [SerializeField, Min(0.1f)] private float ordinaryMaximumSpeed = 12f;
    [SerializeField, Min(0f)] private float passiveContactMaximumSpeed = 4.5f;
    [SerializeField] private float maximumX = 9f;
    [SerializeField] private float maximumZ = 3.5f;

    [Header("Collision Reliability")]
    [SerializeField, Min(1)] private int solverIterations = 12;
    [SerializeField, Min(1)] private int solverVelocityIterations = 8;
    [SerializeField, Min(0f)] private float maximumDepenetrationVelocity = 24f;
    [SerializeField, Min(0f)] private float wallSweepSkin = 0.015f;
    [SerializeField, Range(0f, 1f)] private float wallSweepRestitution = 0.9f;

    [Header("Air-Hockey Feel")]
    [SerializeField, Min(0f)] private float trailMinSpeed = 3.5f;
    [SerializeField] private Color trailColor = new Color(0.25f, 0.85f, 1f, 0.75f);
    [SerializeField, Min(0f)] private float hardImpactSpeed = 6.5f;
    [SerializeField, Range(0f, 1f)] private float hardImpactCameraTrauma = 0.08f;

    private Rigidbody ballBody;
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
    private readonly Collider[] stalledProximityResults = new Collider[8];

    public event Action Stalled;

    public bool CanMove => canMove;
    public float OrdinaryMaximumSpeed => ordinaryMaximumSpeed;
    public AirFootyTeam LastTouchTeam { get; private set; }
    public AirFootyTouchType LastTouchType { get; private set; }
    public float LastTouchTime { get; private set; } = float.NegativeInfinity;

    private void Awake()
    {
        ballBody = GetComponent<Rigidbody>();
        startingPosition = ballBody.position;
        ballBody.useGravity = false;
        ballRenderer = GetComponent<Renderer>();
        ConfigurePhysics();
        BuildSpeedTrail();
        BuildImpactAudio();
        BuildHoverPresentation();
    }

    private void FixedUpdate()
    {
        if (!canMove) return;

        KeepBallInsideArena();

        Vector3 flatVelocity = new Vector3(ballBody.linearVelocity.x, 0f, ballBody.linearVelocity.z);
        float speed = flatVelocity.magnitude;

        if (!Mathf.Approximately(ballBody.linearVelocity.y, 0f))
        {
            ballBody.linearVelocity = flatVelocity;
        }

        if (speed > ordinaryMaximumSpeed)
        {
            ballBody.linearVelocity = flatVelocity.normalized * ordinaryMaximumSpeed;
            speed = ordinaryMaximumSpeed;
        }

        PreventStaticWallTunnelling();
        flatVelocity = new Vector3(ballBody.linearVelocity.x, 0f, ballBody.linearVelocity.z);
        speed = flatVelocity.magnitude;
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
        if (strikerTeam != AirFootyTeam.None &&
            !IsActiveStrikeCollision(strikerTeam))
        {
            RegisterTouch(strikerTeam, AirFootyTouchType.Passive);
            CapPlanarSpeed(passiveContactMaximumSpeed);
        }

        PlayImpactFeedback(speed);
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

    private void KeepBallInsideArena()
    {
        Vector3 position = ballBody.position;
        Vector3 velocity = ballBody.linearVelocity;
        bool movedBall = false;

        // This catches the ball if a fast collision pushes it through a wall.
        if (position.z > maximumZ)
        {
            position.z = maximumZ;
            velocity.z = -Mathf.Abs(velocity.z);
            movedBall = true;
        }
        else if (position.z < -maximumZ)
        {
            position.z = -maximumZ;
            velocity.z = Mathf.Abs(velocity.z);
            movedBall = true;
        }

        // The goal trigger is before this limit, so goals can still be scored.
        if (position.x > maximumX)
        {
            position.x = maximumX;
            velocity.x = -Mathf.Abs(velocity.x);
            movedBall = true;
        }
        else if (position.x < -maximumX)
        {
            position.x = -maximumX;
            velocity.x = Mathf.Abs(velocity.x);
            movedBall = true;
        }

        if (movedBall)
        {
            ballBody.position = position;
            ballBody.linearVelocity = velocity;
        }
    }

    public void StopBall()
    {
        canMove = false;
        ResetStallDetection();
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        if (speedTrail != null)
        {
            speedTrail.emitting = false;
        }
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
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        canMove = false;
        ClearTouchMetadata();
        ResetStallDetection();
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
        if (!canMove ||
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
        RegisterTouch(team, touchType);
        ResetStallDetection();

        float speed = Mathf.Clamp(targetSpeed, 0f, ordinaryMaximumSpeed);
        ballBody.linearVelocity = flatDirection.normalized * speed;
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
            !IsStaticArenaWall(hit))
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

    private static bool IsStaticArenaWall(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null ||
            hitCollider.isTrigger ||
            hit.rigidbody != null ||
            Mathf.Abs(hit.normal.y) > 0.5f)
        {
            return false;
        }

        string colliderName = hitCollider.name;
        return colliderName.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 &&
               !colliderName.EndsWith("Goal Back", StringComparison.Ordinal);
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
        activeStrikeTeam = AirFootyTeam.None;
        activeStrikeContactUntil = float.NegativeInfinity;
    }

    private bool IsActiveStrikeCollision(AirFootyTeam strikerTeam)
    {
        return strikerTeam == activeStrikeTeam &&
               Time.fixedTime <= activeStrikeContactUntil;
    }

    private static AirFootyTeam ResolveStrikerTeam(Collider collider)
    {
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
        float cap = Mathf.Min(Mathf.Max(0f, speedCap), ordinaryMaximumSpeed);
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
            speedTrail = gameObject.AddComponent<TrailRenderer>();
        }

        speedTrail.time = 0.2f;
        speedTrail.startWidth = 0.18f;
        speedTrail.endWidth = 0f;
        speedTrail.startColor = trailColor;
        speedTrail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        speedTrail.minVertexDistance = 0.04f;
        speedTrail.emitting = false;
        speedTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            speedTrail.material = new Material(shader)
            {
                name = "AirFooty Speed Trail (Runtime)"
            };
        }
    }

    private void BuildImpactAudio()
    {
        impactAudio = gameObject.AddComponent<AudioSource>();
        impactAudio.playOnAwake = false;
        impactAudio.spatialBlend = 0.35f;
        impactAudio.dopplerLevel = 0f;
    }

    private void BuildHoverPresentation()
    {
        GameObject visual = new GameObject("AirFooty Ball Hover");
        AirFootyHoverVisual hover = visual.AddComponent<AirFootyHoverVisual>();
        hover.Initialize(transform, startingPosition.y, trailColor);
    }

    private void ResolveCameraFx()
    {
        if (cameraFx == null && Camera.main != null)
        {
            cameraFx = Camera.main.GetComponent<AirFootyCameraFx>();
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
        solverIterations = Mathf.Max(1, solverIterations);
        solverVelocityIterations = Mathf.Max(1, solverVelocityIterations);
        maximumDepenetrationVelocity = Mathf.Max(0f, maximumDepenetrationVelocity);
        wallSweepSkin = Mathf.Max(0f, wallSweepSkin);
        trailMinSpeed = Mathf.Max(0f, trailMinSpeed);
        hardImpactSpeed = Mathf.Max(0f, hardImpactSpeed);
    }
}
