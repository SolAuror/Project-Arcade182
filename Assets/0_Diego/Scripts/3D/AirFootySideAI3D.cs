using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class AirFootySideAI3D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 6.7f;
    [SerializeField, Min(0f)] private float midfieldOverlap = 0.15f;
    [SerializeField, Min(0f)] private float defensiveDepth = 5.5f;
    [SerializeField, Min(0.1f)] private float contactOffset = 1.5f;
    [SerializeField, Min(0.1f)] private float strikeRange = 1.65f;
    [SerializeField, Min(0f)] private float strikeSpeed = 9f;
    [SerializeField, Min(0f)] private float strikeCooldown = 0.55f;
    [SerializeField, Min(0f)] private float wallSweepSkin = 0.012f;

    private Rigidbody aiBody;
    private BallController3D[] balls;
    private BallController3D targetBall;
    private readonly List<GoalZone3D> opponentGoals = new();
    private GoalZone3D targetGoal;
    private GameManager3D gameManager;
    private AirFootyTeam team;
    private Vector3 homeDirection;
    private Vector3 arenaCentre;
    private bool useFourPlayerTeamArea;
    private float teamAreaApexDepth = 1.1f;
    private float teamAreaGoalLineDepth = 7.75f;
    private int nextGoalIndex;
    private bool movementEnabled;
    private float strikeReadyAt;

    public AirFootyTeam Team => team;
    public Vector3 CurrentPlanarVelocity { get; private set; }

    private void Awake()
    {
        aiBody = GetComponent<Rigidbody>();
        AirFootyArenaMovement3D.ConfigureStrikerPhysics(aiBody, wallSweepSkin);
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
        teamAreaApexDepth = Mathf.Max(0.1f, apexDepth);
        teamAreaGoalLineDepth = Mathf.Max(
            teamAreaApexDepth + 0.5f,
            goalLineDepth);
        movementEnabled = true;
        CacheOpponentGoals(availableGoals);

        AirFootyTeamMember3D member = GetComponent<AirFootyTeamMember3D>();
        if (member == null)
        {
            member = gameObject.AddComponent<AirFootyTeamMember3D>();
        }
        member.Configure(team);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            CurrentPlanarVelocity = Vector3.zero;
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
        Vector3 desiredTarget = targetBall != null
            ? ContactTarget(targetBall)
            : arenaCentre + homeDirection * defensiveDepth;
        desiredTarget.y = aiBody.position.y;

        Vector3 toTarget = desiredTarget - aiBody.position;
        toTarget.y = 0f;
        Vector3 direction = toTarget.sqrMagnitude > 0.01f
            ? toTarget.normalized
            : Vector3.zero;
        Vector3 desiredPosition =
            aiBody.position + direction * (moveSpeed * Time.fixedDeltaTime);
        Vector3 newPosition = useFourPlayerTeamArea
            ? AirFootyArenaMovement3D.ResolvePositionInTeamSemicircle(
                aiBody,
                desiredPosition,
                arenaCentre,
                homeDirection,
                teamAreaApexDepth,
                teamAreaGoalLineDepth,
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
            ? teamAreaApexDepth - 0.25f
            : -0.35f;
        if (homeProjection < reachableProjection &&
            Vector3.Distance(aiBody.position, ballPosition) > 3.4f)
        {
            return arenaCentre + homeDirection * defensiveDepth;
        }

        Vector3 attackDirection = DirectionToTargetGoal(ballPosition);
        return ballPosition - attackDirection * contactOffset;
    }

    private void TryStrikeTarget()
    {
        if (targetBall == null || Time.time < strikeReadyAt)
        {
            return;
        }

        Vector3 toBall = targetBall.transform.position - aiBody.position;
        toBall.y = 0f;
        if (toBall.sqrMagnitude > strikeRange * strikeRange)
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
