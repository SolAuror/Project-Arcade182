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

    private Rigidbody aiBody;
    private Vector3 targetPosition;
    private float reactionTimer;

    private void Awake()
    {
        aiBody = GetComponent<Rigidbody>();
        targetPosition = new Vector3(defensivePosition.x, aiBody.position.y, defensivePosition.y);
    }

    private void FixedUpdate()
    {
        if (ball == null) return;

        reactionTimer -= Time.fixedDeltaTime;
        if (reactionTimer <= 0f)
        {
            targetPosition = ball.position.x > 0f
                ? new Vector3(ball.position.x, aiBody.position.y, ball.position.z)
                : new Vector3(defensivePosition.x, aiBody.position.y, defensivePosition.y);
            reactionTimer = reactionDelay;
        }

        Vector3 newPosition = Vector3.MoveTowards(aiBody.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
        newPosition.x = Mathf.Clamp(newPosition.x, minimumPosition.x, maximumPosition.x);
        newPosition.z = Mathf.Clamp(newPosition.z, minimumPosition.y, maximumPosition.y);
        aiBody.MovePosition(newPosition);
    }
}
