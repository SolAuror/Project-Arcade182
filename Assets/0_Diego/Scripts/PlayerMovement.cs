using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private Vector2 minimumPosition = new Vector2(-7.5f, -3.5f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(-0.5f, 3.5f);

    private Rigidbody2D playerBody;
    private Vector2 movementDirection;

    private void Awake()
    {
        playerBody = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        // This stops diagonal movement from being faster.
        movementDirection = new Vector2(horizontal, vertical).normalized;
    }

    private void FixedUpdate()
    {
        Vector2 newPosition = playerBody.position + movementDirection * moveSpeed * Time.fixedDeltaTime;
        newPosition.x = Mathf.Clamp(newPosition.x, minimumPosition.x, maximumPosition.x);
        newPosition.y = Mathf.Clamp(newPosition.y, minimumPosition.y, maximumPosition.y);
        playerBody.MovePosition(newPosition);
    }
}
