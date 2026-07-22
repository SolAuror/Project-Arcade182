using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallController : MonoBehaviour
{
    [SerializeField] private float launchSpeed = 6f;
    [SerializeField] private float minimumSpeed = 3f;
    [SerializeField] private float maximumSpeed = 12f;

    private Rigidbody2D ballBody;
    private Vector2 startingPosition;
    private bool canMove = true;

    private void Awake()
    {
        ballBody = GetComponent<Rigidbody2D>();
        startingPosition = ballBody.position;
        ballBody.bodyType = RigidbodyType2D.Dynamic;
        ballBody.gravityScale = 0f;
    }

    private void Start()
    {
        LaunchBall();
    }

    private void FixedUpdate()
    {
        if (!canMove) return;

        float speed = ballBody.linearVelocity.magnitude;
        // Keep the ball moving without letting it become too fast.
        if (speed < minimumSpeed)
        {
            Vector2 direction = speed > 0.05f ? ballBody.linearVelocity.normalized : RandomLaunchDirection();
            ballBody.linearVelocity = direction * minimumSpeed;
        }
        else if (speed > maximumSpeed)
        {
            ballBody.linearVelocity = ballBody.linearVelocity.normalized * maximumSpeed;
        }
    }

    public void StopBall()
    {
        canMove = false;
        ballBody.linearVelocity = Vector2.zero;
        ballBody.angularVelocity = 0f;
    }

    public void ResetBall()
    {
        ballBody.position = startingPosition;
        ballBody.linearVelocity = Vector2.zero;
        ballBody.angularVelocity = 0f;
        canMove = true;
        LaunchBall();
    }

    private void LaunchBall()
    {
        ballBody.linearVelocity = RandomLaunchDirection() * launchSpeed;
    }

    private Vector2 RandomLaunchDirection()
    {
        float horizontal = Random.value < 0.5f ? -1f : 1f;
        float vertical = Random.Range(-0.75f, 0.75f);
        // A strong horizontal value avoids an almost vertical launch.
        return new Vector2(horizontal, vertical).normalized;
    }
}
