using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AIPaddle : MonoBehaviour
{
    [SerializeField] private Transform ball;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Vector2 minimumPosition = new Vector2(0f, -4f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(8f, 4f);

    private Rigidbody2D paddleBody;

    private void Awake()
    {
        paddleBody = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (ball == null)
        {
            return;
        }

        Vector2 targetPosition = paddleBody.position;

        // Follow the ball only while it is on the AI's half.
        if (ball.position.x > 0f)
        {
            targetPosition = Vector2.MoveTowards(
                paddleBody.position,
                ball.position,
                moveSpeed * Time.fixedDeltaTime);
        }

        targetPosition.x = Mathf.Clamp(
            targetPosition.x, minimumPosition.x, maximumPosition.x);
        targetPosition.y = Mathf.Clamp(
            targetPosition.y, minimumPosition.y, maximumPosition.y);

        paddleBody.MovePosition(targetPosition);
    }
}
