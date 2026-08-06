using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AirFootyTeamMember3D))]
public sealed class AirFootySideAI3D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 6.2f;
    [SerializeField, Min(0f)] private float midfieldOverlap = 0.15f;
    [SerializeField, Min(0f)] private float defensiveDepth = 5.5f;
    [SerializeField, Min(0.1f)] private float contactOffset = 1.5f;
    [SerializeField, Min(0.1f)] private float strikeRange = 1.65f;
    [SerializeField, Min(0f)] private float strikeSpeed = 9f;
    [SerializeField, Min(0f)] private float strikeCooldown = 0.7f;
    [SerializeField, Min(0f)] private float wallSweepSkin = 0.012f;

    [Header("Reaction")]
    [Tooltip(
        "Seconds between re-aiming. Re-deciding every physics step made the " +
        "side track the ball perfectly, which read as relentless and robotic.")]
    [SerializeField, Min(0f)] private float reactionInterval = 0.16f;
    [Tooltip("Random extra delay per decision, so two sides never move in lockstep.")]
    [SerializeField, Min(0f)] private float reactionJitter = 0.07f;
    [Tooltip(
        "How far off the ideal spot it wanders. This drifts continuously - a " +
        "fresh random offset per decision reads as a twitch, not as imprecision.")]
    [SerializeField, Min(0f)] private float positioningError = 0.5f;
    [Tooltip("How quickly that wander moves around. Low is lazy, high is fidgety.")]
    [SerializeField, Min(0.01f)] private float driftSpeed = 0.35f;

    [Header("Steering")]
    [Tooltip("Velocity change per second. Lower feels heavier and less reactive.")]
    [SerializeField, Min(0.1f)] private float acceleration = 26f;
    [Tooltip("Smoothing onto a newly decided spot, so decisions do not yank the body.")]
    [SerializeField, Min(0.01f)] private float targetSmoothing = 0.14f;
    [Tooltip("Tighter smoothing while fleeing a live ball in overtime.")]
    [SerializeField, Min(0.01f)] private float evadeSmoothing = 0.05f;
    [Tooltip("Eases down inside this radius instead of stopping dead and re-starting.")]
    [SerializeField, Min(0.05f)] private float arrivalRadius = 0.5f;

    [Header("Overtime")]
    [Tooltip("Standoff while the ball can kill: outside contact, inside pulse range.")]
    [SerializeField, Min(0.1f)] private float overtimeStandoff = 1.9f;
    [Tooltip("Break off and run if an armed ball closes inside this radius.")]
    [SerializeField, Min(0.1f)] private float overtimeDangerRadius = 1.3f;

    private Rigidbody aiBody;
    private AirFootyStrikeMotor3D strikeMotor;
    private bool overtime;
    private BallController3D[] balls;
    private BallController3D targetBall;
    private readonly List<GoalZone3D> opponentGoals = new();
    private GoalZone3D targetGoal;
    private GameManager3D gameManager;
    private AirFootyTeam team;
    private Vector3 homeDirection;
    private Vector3 arenaCentre;
    private bool useFourPlayerTeamArea;
    private int nextGoalIndex;
    private bool movementEnabled;
    private float strikeReadyAt;
    private Vector3 cachedTarget;
    private Vector3 smoothedTarget;
    private Vector3 smoothedTargetVelocity;
    private Vector3 steerVelocity;
    private float nextDecisionTime;
    private float driftSeed;
    private bool targetInitialised;

    public AirFootyTeam Team => team;
    public Vector3 CurrentPlanarVelocity { get; private set; }

    private void Awake()
    {
        aiBody = GetComponent<Rigidbody>();
        strikeMotor = GetComponent<AirFootyStrikeMotor3D>();
        AirFootyArenaMovement3D.ConfigureStrikerPhysics(aiBody, wallSweepSkin);
        // Per-instance, so four sides do not wander in unison.
        driftSeed = Random.Range(0f, 512f);
    }

    /// <summary>
    /// Switches the side to overtime play: keep clear of the ball and move it with
    /// pulses instead of running into it.
    /// </summary>
    public void SetOvertime(bool enabled)
    {
        overtime = enabled;
    }

    public void Configure(
        AirFootyTeam configuredTeam,
        BallController3D[] availableBalls,
        GoalZone3D[] availableGoals,
        GameManager3D manager,
        bool fourPlayerMode,
        Vector3 centre,
        float apexDepth,
        float goalLineDepth)
    {
        team = configuredTeam;
        homeDirection = AirFootyTeamMember3D.HomeDirection(team);
        balls = availableBalls;
        gameManager = manager;
        useFourPlayerTeamArea = fourPlayerMode;
        arenaCentre = centre;
        movementEnabled = true;
        CacheOpponentGoals(availableGoals);

        AirFootyTeamMember3D member = GetComponent<AirFootyTeamMember3D>();
        if (member == null)
        {
            Debug.LogError(
                $"{nameof(AirFootySideAI3D)} on {name} requires an authored {nameof(AirFootyTeamMember3D)}.",
                this);
            enabled = false;
            return;
        }
        member.Configure(team);

        // The motor ships on every striker but only the human's was ever given a
        // team. Overtime needs it, because pulsing is the only way to move a ball.
        if (strikeMotor == null)
        {
            strikeMotor = GetComponent<AirFootyStrikeMotor3D>();
        }
        strikeMotor?.ConfigureTeam(team);
        strikeMotor?.SetPulseWaveEmission(true);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            CurrentPlanarVelocity = Vector3.zero;
            // Drop stored momentum, otherwise it lurches on the next kick-off
            // with whatever heading it had when play stopped.
            steerVelocity = Vector3.zero;
            smoothedTargetVelocity = Vector3.zero;
            targetInitialised = false;
        }
    }

    private void FixedUpdate()
    {
        if (!movementEnabled || team == AirFootyTeam.None || balls == null)
        {
            CurrentPlanarVelocity = Vector3.zero;
            return;
        }

        targetBall = SelectTargetBall();
        EnsureTargetGoal();

        Vector3 evasion = Vector3.zero;
        bool evading = overtime && TryResolveEvasion(out evasion);
        if (evading)
        {
            // Getting clear of a live ball is a reflex, not a decision, so it
            // skips the reaction delay. A side that dithered here would just
            // look stupid rather than beatable.
            cachedTarget = evasion;
        }
        else if (Time.time >= nextDecisionTime)
        {
            // Committing to a spot for a beat at a time is what stops the side
            // tracking the ball frame-perfectly.
            nextDecisionTime = Time.time + reactionInterval +
                               Random.Range(0f, reactionJitter);
            cachedTarget = targetBall != null
                ? ContactTarget(targetBall)
                : arenaCentre + homeDirection * defensiveDepth;
        }

        // The imprecision has to wander rather than resample. An independent
        // random offset per decision is white noise, and reads as a flinch every
        // time it lands; smooth noise reads as a player not quite settling.
        Vector3 aimPoint = cachedTarget;
        if (!evading)
        {
            float wander = Time.time * driftSpeed;
            aimPoint += new Vector3(
                Mathf.PerlinNoise(driftSeed, wander) - 0.5f,
                0f,
                Mathf.PerlinNoise(driftSeed + 41.7f, wander) - 0.5f) *
                (positioningError * 2f);
        }
        aimPoint.y = aiBody.position.y;

        if (!targetInitialised)
        {
            targetInitialised = true;
            smoothedTarget = aimPoint;
        }

        // Smoothing the pursued point preserves the reaction delay - it still
        // commits late - without the body being yanked when a decision lands.
        smoothedTarget = Vector3.SmoothDamp(
            smoothedTarget,
            aimPoint,
            ref smoothedTargetVelocity,
            evading ? evadeSmoothing : targetSmoothing,
            float.PositiveInfinity,
            Time.fixedDeltaTime);

        Vector3 toTarget = smoothedTarget - aiBody.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // Ease down on approach rather than stopping dead at a hard cutoff, which
        // is what made it buzz on the spot once it arrived.
        Vector3 desiredVelocity = distance > 0.0001f
            ? toTarget / distance *
              (moveSpeed * Mathf.Clamp01(distance / arrivalRadius))
            : Vector3.zero;

        // Carry momentum instead of snapping to a new heading, so a change of
        // mind costs it a moment of turn like it would a player.
        steerVelocity = Vector3.MoveTowards(
            steerVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);

        Vector3 desiredPosition =
            aiBody.position + steerVelocity * Time.fixedDeltaTime;
        Vector3 newPosition = useFourPlayerTeamArea
            ? AirFootyArenaMovement3D.ResolvePositionOnTeamSide(
                aiBody,
                desiredPosition,
                arenaCentre,
                homeDirection,
                -midfieldOverlap,
                wallSweepSkin)
            : AirFootyArenaMovement3D.ResolvePositionOnHalf(
                aiBody,
                desiredPosition,
                homeDirection,
                -midfieldOverlap,
                wallSweepSkin);

        CurrentPlanarVelocity = Time.fixedDeltaTime > 0f
            ? (newPosition - aiBody.position) / Time.fixedDeltaTime
            : Vector3.zero;
        CurrentPlanarVelocity = Vector3.ProjectOnPlane(
            CurrentPlanarVelocity,
            Vector3.up);
        aiBody.MovePosition(newPosition);

        TryStrikeTarget();
    }

    private BallController3D SelectTargetBall()
    {
        BallController3D best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < balls.Length; i++)
        {
            BallController3D candidate = balls[i];
            if (candidate == null || !candidate.CanMove)
            {
                continue;
            }

            Vector3 position = candidate.transform.position;
            Vector3 velocity = candidate.PlanarVelocity;
            float threat = Vector3.Dot(position, homeDirection) +
                           Vector3.Dot(velocity, homeDirection) * 0.65f;
            float distancePenalty = Vector3.Distance(aiBody.position, position) * 0.12f;
            float score = threat - distancePenalty;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private Vector3 ContactTarget(BallController3D selectedBall)
    {
        Vector3 ballPosition = selectedBall.transform.position;
        float homeProjection = Vector3.Dot(
            ballPosition - arenaCentre,
            homeDirection);
        float reachableProjection = useFourPlayerTeamArea
            ? -midfieldOverlap
            : -0.35f;
        if (homeProjection < reachableProjection &&
            Vector3.Distance(aiBody.position, ballPosition) > 3.4f)
        {
            return arenaCentre + homeDirection * defensiveDepth;
        }

        // A pulse pushes the ball straight away from the striker, so sitting on
        // the far side of the ball from the target goal aims it. In overtime the
        // same spot is held further back, clear of a lethal touch.
        Vector3 attackDirection = DirectionToTargetGoal(ballPosition);
        float standoff = overtime ? overtimeStandoff : contactOffset;
        return ballPosition - attackDirection * standoff;
    }

    /// <summary>
    /// Finds the nearest armed ball that is already too close and returns a spot
    /// directly away from it. Inert balls are ignored: they are safe to crowd.
    /// </summary>
    private bool TryResolveEvasion(out Vector3 evasionTarget)
    {
        evasionTarget = Vector3.zero;
        BallController3D threat = null;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < balls.Length; i++)
        {
            BallController3D candidate = balls[i];
            if (candidate == null || !candidate.IsLethal)
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - aiBody.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < closest)
            {
                closest = distance;
                threat = candidate;
            }
        }

        if (threat == null || closest > overtimeDangerRadius)
        {
            return false;
        }

        Vector3 away = aiBody.position - threat.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude <= 0.0001f)
        {
            away = homeDirection;
        }

        evasionTarget = aiBody.position + away.normalized * (overtimeStandoff * 1.5f);
        return true;
    }

    private void TryStrikeTarget()
    {
        if (targetBall == null || Time.time < strikeReadyAt)
        {
            return;
        }

        Vector3 toBall = targetBall.transform.position - aiBody.position;
        toBall.y = 0f;
        float reach = overtime && strikeMotor != null
            ? strikeMotor.GetPulseRadius(1f)
            : strikeRange;
        if (toBall.sqrMagnitude > reach * reach)
        {
            return;
        }

        Vector3 attackDirection = DirectionToTargetGoal(
            targetBall.transform.position);
        bool isBehindBall = Vector3.Dot(toBall.normalized, attackDirection) > 0.15f;
        if (!isBehindBall)
        {
            return;
        }

        // Overtime refuses ApplyStrike outright, so the side has to pulse. The
        // motor handles its own cooldown, radius and wave.
        if (overtime)
        {
            if (strikeMotor == null)
            {
                return;
            }

            AirFootyStrikeResult result = strikeMotor.TryPulse(1f);
            if (result == AirFootyStrikeResult.Hit ||
                result == AirFootyStrikeResult.Perfect)
            {
                strikeReadyAt = Time.time + strikeCooldown;
                targetGoal = SelectNextOpponentGoal();
            }

            return;
        }

        if (targetBall.ApplyStrike(
                team,
                AirFootyTouchType.TapKick,
                attackDirection,
                strikeSpeed))
        {
            strikeReadyAt = Time.time + strikeCooldown;
            targetGoal = SelectNextOpponentGoal();
        }
    }

    private void CacheOpponentGoals(GoalZone3D[] availableGoals)
    {
        opponentGoals.Clear();
        HashSet<AirFootyTeam> cachedTeams = new();
        if (availableGoals != null)
        {
            foreach (GoalZone3D goal in availableGoals)
            {
                if (goal != null &&
                    goal.OwnerTeam != AirFootyTeam.None &&
                    goal.OwnerTeam != team &&
                    cachedTeams.Add(goal.OwnerTeam))
                {
                    opponentGoals.Add(goal);
                }
            }
        }

        opponentGoals.Sort((left, right) =>
            left.OwnerTeam.CompareTo(right.OwnerTeam));
        nextGoalIndex = opponentGoals.Count > 0
            ? (int)team % opponentGoals.Count
            : 0;
        targetGoal = SelectNextOpponentGoal();
    }

    private void EnsureTargetGoal()
    {
        if (targetGoal == null ||
            targetGoal.OwnerTeam == team ||
            (gameManager != null &&
             gameManager.IsTeamEliminated(targetGoal.OwnerTeam)))
        {
            targetGoal = SelectNextOpponentGoal();
        }
    }

    private GoalZone3D SelectNextOpponentGoal()
    {
        if (opponentGoals.Count == 0)
        {
            return null;
        }

        for (int attempt = 0; attempt < opponentGoals.Count; attempt++)
        {
            int index = nextGoalIndex % opponentGoals.Count;
            nextGoalIndex = (nextGoalIndex + 1) % opponentGoals.Count;
            GoalZone3D candidate = opponentGoals[index];
            if (candidate != null &&
                candidate.OwnerTeam != team &&
                (gameManager == null ||
                 !gameManager.IsTeamEliminated(candidate.OwnerTeam)))
            {
                return candidate;
            }
        }

        return null;
    }

    private Vector3 DirectionToTargetGoal(Vector3 ballPosition)
    {
        EnsureTargetGoal();
        Vector3 direction = targetGoal != null
            ? targetGoal.transform.position - ballPosition
            : -homeDirection;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : -homeDirection;
    }

    private void OnCollisionEnter(Collision collision)
    {
        ApplyMovementContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        ApplyMovementContact(collision);
    }

    private void ApplyMovementContact(Collision collision)
    {
        BallController3D contactedBall =
            collision.collider.GetComponentInParent<BallController3D>();
        contactedBall?.ApplyMovementContact(
            team,
            CurrentPlanarVelocity,
            aiBody.position);
    }
}
