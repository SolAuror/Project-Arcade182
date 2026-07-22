using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AIPlayer : MonoBehaviour
{
    [SerializeField] private Transform ball;
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float reactionDelay = 0.15f;
    [SerializeField] private Vector2 defensivePosition = new Vector2(5.5f, 0f);
    [SerializeField] private Vector2 minimumPosition = new Vector2(0.5f, -3.5f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(7.5f, 3.5f);

    private Rigidbody2D aiBody;
    private Vector2 targetPosition;
    private float reactionTimer;

    private void Awake()
    {
        aiBody = GetComponent<Rigidbody2D>();
        targetPosition = defensivePosition;
    }

    private void FixedUpdate()
    {
        if (ball == null) return;

        reactionTimer -= Time.fixedDeltaTime;
        // The delay makes the AI react a little less perfectly.
        if (reactionTimer <= 0f)
        {
            targetPosition = ball.position.x > 0f ? (Vector2)ball.position : defensivePosition;
            reactionTimer = reactionDelay;
        }

        Vector2 newPosition = Vector2.MoveTowards(aiBody.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
        newPosition.x = Mathf.Clamp(newPosition.x, minimumPosition.x, maximumPosition.x);
        newPosition.y = Mathf.Clamp(newPosition.y, minimumPosition.y, maximumPosition.y);
        aiBody.MovePosition(newPosition);
    }
}
