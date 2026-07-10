using UnityEngine;

public class PlayerPaddle : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private Vector2 minimumPosition = new Vector2(-8f, -4f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(0f, 4f);

    private Rigidbody2D paddleBody;
    private Vector2 moveInput;

    private void Awake()
    {
        paddleBody = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // Unity's default axes support both WASD and the arrow keys.
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical).normalized;
    }

    private void FixedUpdate()
    {
        Vector2 newPosition =
            paddleBody.position + moveInput * moveSpeed * Time.fixedDeltaTime;

        newPosition.x = Mathf.Clamp(
            newPosition.x, minimumPosition.x, maximumPosition.x);
        newPosition.y = Mathf.Clamp(
            newPosition.y, minimumPosition.y, maximumPosition.y);

        paddleBody.MovePosition(newPosition);
    }
}
