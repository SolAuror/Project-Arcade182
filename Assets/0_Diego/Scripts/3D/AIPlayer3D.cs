using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AIPlayer3D : MonoBehaviour
{
    [SerializeField] private Transform ball;
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float reactionDelay = 0.15f;
    [SerializeField] private Vector2 defensivePosition = new Vector2(5.5f, 0f);
    [SerializeField] private Vector2 minimumPosition = new Vector2(0.5f, -3.5f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(7.5f, 3.5f);

    [Header("Goal-Wall Recovery")]
    [SerializeField, Min(0.1f)] private float goalWallEnterDistance = 0.55f;
    [SerializeField, Min(0.1f)] private float goalWallExitDistance = 1.15f;
    [SerializeField, Min(0f)] private float goalWallWaitBehindBall = 0.75f;

    private Rigidbody aiBody;
    private Vector3 targetPosition;
    private float reactionTimer;
    private bool movementEnabled = true;
    private bool waitingForGoalWallBounce;

    private void Awake()
    {
        aiBody = GetComponent<Rigidbody>();
        targetPosition = new Vector3(defensivePosition.x, aiBody.position.y, defensivePosition.y);
    }

    private void FixedUpdate()
    {
        if (ball == null || !movementEnabled) return;

        reactionTimer -= Time.fixedDeltaTime;
        if (reactionTimer <= 0f)
        {
            UpdateTargetPosition();
            reactionTimer = reactionDelay;
        }

        Vector3 newPosition = Vector3.MoveTowards(aiBody.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
        newPosition.x = Mathf.Clamp(newPosition.x, minimumPosition.x, maximumPosition.x);
        newPosition.z = Mathf.Clamp(newPosition.z, minimumPosition.y, maximumPosition.y);
        aiBody.MovePosition(newPosition);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        reactionTimer = 0f;
        if (!enabled)
        {
            targetPosition = aiBody.position;
            waitingForGoalWallBounce = false;
        }
    }

    private void UpdateTargetPosition()
    {
        if (ball.position.x <= 0f)
        {
            waitingForGoalWallBounce = false;
            targetPosition = DefensiveTarget();
            return;
        }

        float goalWallDistance = DistanceFromGoalWall(ball.position.x);
        if (!waitingForGoalWallBounce && goalWallDistance <= goalWallEnterDistance)
        {
            waitingForGoalWallBounce = true;
        }
        else if (waitingForGoalWallBounce && goalWallDistance >= goalWallExitDistance)
        {
            waitingForGoalWallBounce = false;
        }

        targetPosition = waitingForGoalWallBounce
            ? GoalWallWaitTarget()
            : new Vector3(ball.position.x, aiBody.position.y, ball.position.z);
    }

    private Vector3 DefensiveTarget()
    {
        return new Vector3(defensivePosition.x, aiBody.position.y, defensivePosition.y);
    }

    private Vector3 GoalWallWaitTarget()
    {
        // The AI attacks toward negative X, so it waits on the goal-wall side of
        // the ball when possible. It keeps matching Z so long-wall shots remain blockable.
        float waitingX = Mathf.Clamp(
            ball.position.x + goalWallWaitBehindBall,
            minimumPosition.x,
            maximumPosition.x);
        float waitingZ = Mathf.Clamp(
            ball.position.z,
            minimumPosition.y,
            maximumPosition.y);

        return new Vector3(waitingX, aiBody.position.y, waitingZ);
    }

    private float DistanceFromGoalWall(float xPosition)
    {
        // The striker's maximum X is its legal goal-wall-side limit. Ball positions
        // beyond that limit are treated as touching the goal-wall recovery zone.
        return Mathf.Max(0f, maximumPosition.x - xPosition);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        reactionDelay = Mathf.Max(0.01f, reactionDelay);
        goalWallEnterDistance = Mathf.Max(0.1f, goalWallEnterDistance);
        goalWallExitDistance =
            Mathf.Max(goalWallEnterDistance + 0.1f, goalWallExitDistance);
        goalWallWaitBehindBall = Mathf.Max(0f, goalWallWaitBehindBall);
    }
}
