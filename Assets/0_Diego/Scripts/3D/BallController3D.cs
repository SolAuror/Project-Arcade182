using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallController3D : MonoBehaviour
{
    [SerializeField] private float launchSpeed = 6f;
    [SerializeField] private float minimumSpeed = 3f;
    [SerializeField] private float maximumSpeed = 12f;
    [SerializeField] private float maximumX = 9f;
    [SerializeField] private float maximumZ = 3.5f;

    private Rigidbody ballBody;
    private Vector3 startingPosition;
    private bool canMove = true;

    private void Awake()
    {
        ballBody = GetComponent<Rigidbody>();
        startingPosition = ballBody.position;
        ballBody.useGravity = false;
    }

    private void Start()
    {
        LaunchBall();
    }

    private void FixedUpdate()
    {
        if (!canMove) return;

        KeepBallInsideArena();

        Vector3 flatVelocity = new Vector3(ballBody.linearVelocity.x, 0f, ballBody.linearVelocity.z);
        float speed = flatVelocity.magnitude;

        if (speed < minimumSpeed)
        {
            Vector3 direction = speed > 0.05f ? flatVelocity.normalized : RandomLaunchDirection();
            ballBody.linearVelocity = direction * minimumSpeed;
        }
        else if (speed > maximumSpeed)
        {
            ballBody.linearVelocity = flatVelocity.normalized * maximumSpeed;
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
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
    }

    public void ResetBall()
    {
        ballBody.position = startingPosition;
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        canMove = true;
        LaunchBall();
    }

    private void LaunchBall()
    {
        ballBody.linearVelocity = RandomLaunchDirection() * launchSpeed;
    }

    private Vector3 RandomLaunchDirection()
    {
        float horizontal = Random.value < 0.5f ? -1f : 1f;
        float vertical = Random.Range(-0.75f, 0.75f);
        return new Vector3(horizontal, 0f, vertical).normalized;
    }
}
