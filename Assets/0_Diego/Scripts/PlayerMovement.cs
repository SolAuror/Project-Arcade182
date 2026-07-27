using UnityEngine;
using UnityEngine.InputSystem;

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
        if (Keyboard.current == null)
        {
            movementDirection = Vector2.zero;
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed) vertical += 1f;

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
