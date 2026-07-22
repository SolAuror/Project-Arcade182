using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Ball : MonoBehaviour
{
    [SerializeField] private float launchSpeed = 7f;
    [SerializeField] private float resetDelay = 1f;

    private Rigidbody2D ballBody;
    private Vector2 startingPosition;

    private void Awake()
    {
        ballBody = GetComponent<Rigidbody2D>();
        startingPosition = transform.position;
    }

    private void Start()
    {
        LaunchBall();
    }

    public void ResetBall()
    {
        StopAllCoroutines();
        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        ballBody.linearVelocity = Vector2.zero;
        ballBody.angularVelocity = 0f;
        ballBody.position = startingPosition;

        yield return new WaitForSeconds(resetDelay);

        LaunchBall();
    }

    private void LaunchBall()
    {
        // Choose a random left/right direction with a small vertical angle.
        float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
        float verticalDirection = Random.Range(-0.7f, 0.7f);
        Vector2 direction =
            new Vector2(horizontalDirection, verticalDirection).normalized;

        ballBody.linearVelocity = direction * launchSpeed;
    }
}

