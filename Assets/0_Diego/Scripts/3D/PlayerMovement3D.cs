using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private Vector2 minimumPosition = new Vector2(-7.5f, -3.5f);
    [SerializeField] private Vector2 maximumPosition = new Vector2(-0.5f, 3.5f);

    [Header("View-relative Controls")]
    [SerializeField] private bool cameraRelativeMovement = true;
    [SerializeField] private Camera movementCamera;

    private Rigidbody playerBody;
    private Vector3 movementDirection;
    private InputAction moveAction;
    private bool movementEnabled = true;
    private float moveSpeedMultiplier = 1f;

    public Vector3 CurrentMovementDirection => movementDirection;

    private void Awake()
    {
        playerBody = GetComponent<Rigidbody>();
        ResolveMovementCamera();
        BuildMoveAction();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        movementDirection = Vector3.zero;
        moveSpeedMultiplier = 1f;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
    }

    private void Update()
    {
        if (!movementEnabled || moveAction == null)
        {
            movementDirection = Vector3.zero;
            return;
        }

        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        movementDirection = ResolveMovementDirection(input);
    }

    private void FixedUpdate()
    {
        Vector3 newPosition =
            playerBody.position +
            movementDirection * (moveSpeed * moveSpeedMultiplier * Time.fixedDeltaTime);
        newPosition.x = Mathf.Clamp(newPosition.x, minimumPosition.x, maximumPosition.x);
        newPosition.z = Mathf.Clamp(newPosition.z, minimumPosition.y, maximumPosition.y);
        playerBody.MovePosition(newPosition);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            movementDirection = Vector3.zero;
            moveSpeedMultiplier = 1f;
        }
    }

    public void SetMoveSpeedMultiplier(float multiplier)
    {
        moveSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    private Vector3 ResolveMovementDirection(Vector2 input)
    {
        if (!cameraRelativeMovement)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        ResolveMovementCamera();
        if (movementCamera == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 cameraForward =
            Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up);
        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            cameraForward =
                Vector3.ProjectOnPlane(movementCamera.transform.up, Vector3.up);
        }
        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        cameraForward.Normalize();
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);
        Vector3 direction = cameraRight * input.x + cameraForward * input.y;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void ResolveMovementCamera()
    {
        if (movementCamera == null || !movementCamera.isActiveAndEnabled)
        {
            movementCamera = Camera.main;
        }
    }

    private void BuildMoveAction()
    {
        moveAction = new InputAction("AirFooty Move", InputActionType.Value);

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.AddBinding("<Gamepad>/dpad");
    }
}
