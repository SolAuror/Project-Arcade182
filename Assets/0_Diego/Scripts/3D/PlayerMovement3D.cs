using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement3D : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private Vector2 minimumPosition = new Vector2(-7.5f, -3.5f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(-0.5f, 3.5f);

    private Rigidbody playerBody;
    private Vector3 movementDirection;

    private void Awake()
    {
        playerBody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            movementDirection = Vector3.zero;
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;
        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed) vertical += 1f;

        movementDirection = new Vector3(horizontal, 0f, vertical).normalized;
    }

    private void FixedUpdate()
    {
        Vector3 newPosition = playerBody.position + movementDirection * moveSpeed * Time.fixedDeltaTime;
        newPosition.x = Mathf.Clamp(newPosition.x, minimumPosition.x, maximumPosition.x);
        newPosition.z = Mathf.Clamp(newPosition.z, minimumPosition.y, maximumPosition.y);
        playerBody.MovePosition(newPosition);
    }
}
