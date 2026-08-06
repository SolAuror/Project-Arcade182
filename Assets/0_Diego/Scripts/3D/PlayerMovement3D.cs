using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField, Min(0f)] private float midfieldOverlap = 0.15f;
    [SerializeField, Min(0f)] private float wallSweepSkin = 0.012f;

    [Header("Response")]
    [Tooltip(
        "Seconds to reach full speed. Deliberately short: this is here to give " +
        "the striker weight, not to put latency between the stick and the puck.")]
    [SerializeField, Min(0f)] private float accelerationTime = 0.045f;
    [Tooltip("Seconds to coast to a stop. Slightly longer than acceleration, so it reads as a hovering puck.")]
    [SerializeField, Min(0f)] private float decelerationTime = 0.085f;

    [Header("View-relative Controls")]
    [SerializeField] private bool cameraRelativeMovement = true;
    [SerializeField] private Camera movementCamera;

    private Rigidbody playerBody;
    private Vector3 movementDirection;
    private InputAction moveAction;
    private bool movementEnabled = true;
    private float moveSpeedMultiplier = 1f;
    private Vector3 dashDirection;
    private Vector3 smoothedVelocity;
    private float dashSpeedMultiplier;
    private float dashUntil;
    private bool useFourPlayerTeamArea;
    private Vector3 arenaCentre;

    public Vector3 CurrentMovementDirection => movementDirection;
    public Vector3 CurrentPlanarVelocity { get; private set; }
    public bool IsDashing => movementEnabled && Time.time < dashUntil;

    private void Awake()
    {
        playerBody = GetComponent<Rigidbody>();
        AirFootyArenaMovement3D.ConfigureStrikerPhysics(
            playerBody,
            wallSweepSkin);
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
        CurrentPlanarVelocity = Vector3.zero;
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
        Vector3 previousPosition = playerBody.position;
        bool dashing = IsDashing;
        Vector3 activeDirection = dashing ? dashDirection : movementDirection;
        float activeSpeedMultiplier =
            dashing ? dashSpeedMultiplier : moveSpeedMultiplier;
        Vector3 targetVelocity =
            activeDirection * (moveSpeed * activeSpeedMultiplier);

        if (dashing)
        {
            // A dash snaps. Ramping it would blunt the one move that is meant to
            // feel like a commitment.
            smoothedVelocity = targetVelocity;
        }
        else
        {
            float ramp = targetVelocity.sqrMagnitude >= smoothedVelocity.sqrMagnitude
                ? accelerationTime
                : decelerationTime;
            smoothedVelocity = ramp <= 0f
                ? targetVelocity
                : Vector3.MoveTowards(
                    smoothedVelocity,
                    targetVelocity,
                    moveSpeed / ramp * Time.fixedDeltaTime);
        }

        Vector3 newPosition =
            previousPosition + smoothedVelocity * Time.fixedDeltaTime;
        AirFootyTeam team = ResolveTeam();
        newPosition = useFourPlayerTeamArea
            ? AirFootyArenaMovement3D.ResolvePositionOnTeamSide(
                playerBody,
                newPosition,
                arenaCentre,
                AirFootyTeamMember3D.HomeDirection(team),
                -midfieldOverlap,
                wallSweepSkin)
            : AirFootyArenaMovement3D.ResolvePositionOnHalf(
                playerBody,
                newPosition,
                AirFootyTeamMember3D.HomeDirection(team),
                -midfieldOverlap,
                wallSweepSkin);
        Vector3 planarVelocity = Time.fixedDeltaTime > 0f
            ? (newPosition - previousPosition) / Time.fixedDeltaTime
            : Vector3.zero;
        planarVelocity.y = 0f;
        CurrentPlanarVelocity = planarVelocity;
        playerBody.MovePosition(newPosition);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ApplyMovementContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        ApplyMovementContact(collision);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            movementDirection = Vector3.zero;
            moveSpeedMultiplier = 1f;
            CurrentPlanarVelocity = Vector3.zero;
            // Drop the ramp too, or the striker coasts on through a kick-off
            // freeze or a goal reset instead of stopping where it stood.
            smoothedVelocity = Vector3.zero;
            CancelDash();
        }
    }

    public void SetMoveSpeedMultiplier(float multiplier)
    {
        moveSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void ConfigureTeamArea(
        bool fourPlayerMode,
        Vector3 centre,
        float apexDepth,
        float goalLineDepth)
    {
        useFourPlayerTeamArea = fourPlayerMode;
        arenaCentre = centre;
    }

    public void BeginDash(
        Vector3 direction,
        float duration,
        float speedMultiplier)
    {
        direction.y = 0f;
        if (!movementEnabled || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        dashDirection = direction.normalized;
        dashSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
        dashUntil = Time.time + Mathf.Max(0.01f, duration);
    }

    public void BoostActiveDash(
        float speedMultiplier,
        float extraDuration)
    {
        if (!IsDashing)
        {
            return;
        }

        dashSpeedMultiplier *= Mathf.Max(1f, speedMultiplier);
        dashUntil += Mathf.Max(0f, extraDuration);
    }

    public void CancelDash()
    {
        dashUntil = float.NegativeInfinity;
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
            movementCamera = AirFootyCameraLookup.FindDisplayCamera();
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

    private void ApplyMovementContact(Collision collision)
    {
        if (IsDashing)
        {
            return;
        }

        BallController3D ball = collision.collider.GetComponentInParent<BallController3D>();
        ball?.ApplyMovementContact(
            ResolveTeam(),
            CurrentPlanarVelocity,
            playerBody.position);
    }

    private AirFootyTeam ResolveTeam()
    {
        AirFootyTeamMember3D member = GetComponent<AirFootyTeamMember3D>();
        return member != null && member.Team != AirFootyTeam.None
            ? member.Team
            : AirFootyTeam.Blue;
    }
}

internal static class AirFootyArenaMovement3D
{
    private const int MaximumSlideIterations = 3;
    private const int MaximumSweepHits = 16;
    private static readonly RaycastHit[] SweepHits =
        new RaycastHit[MaximumSweepHits];

    public static void ConfigureStrikerPhysics(
        Rigidbody body,
        float contactOffset)
    {
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;
        body.maxDepenetrationVelocity = 16f;

        SphereCollider sphere = body.GetComponent<SphereCollider>();
        if (sphere != null)
        {
            sphere.contactOffset = Mathf.Max(
                0.001f,
                contactOffset);
        }
    }

    public static Vector3 ResolvePosition(
        Rigidbody body,
        Vector3 desiredPosition,
        float minimumX,
        float maximumX,
        float skin)
    {
        Vector3 currentPosition = body.position;
        desiredPosition.y = currentPosition.y;
        desiredPosition.x = Mathf.Clamp(
            desiredPosition.x,
            minimumX,
            maximumX);

        Vector3 displacement = desiredPosition - currentPosition;
        displacement.y = 0f;
        float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            return desiredPosition;
        }

        SphereCollider sphere = body.GetComponent<SphereCollider>();
        if (sphere == null)
        {
            return desiredPosition;
        }

        Vector3 worldCentreOffset =
            body.transform.TransformPoint(sphere.center) - body.position;
        Vector3 scale = body.transform.lossyScale;
        float radius =
            sphere.radius *
            Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));

        Vector3 resolvedPosition = currentPosition;
        Vector3 remaining = displacement;
        for (int iteration = 0;
             iteration < MaximumSlideIterations;
             iteration++)
        {
            distance = remaining.magnitude;
            if (distance <= 0.0001f)
            {
                break;
            }

            Vector3 direction = remaining / distance;
            Vector3 worldCentre =
                resolvedPosition + worldCentreOffset;
            if (!TryFindBoundaryHit(
                    sphere,
                    worldCentre,
                    radius,
                    direction,
                    distance,
                    skin,
                    iteration == 0,
                    out RaycastHit hit))
            {
                resolvedPosition += remaining;
                remaining = Vector3.zero;
                break;
            }

            float safeDistance = Mathf.Clamp(
                hit.distance - skin,
                0f,
                distance);
            resolvedPosition += direction * safeDistance;
            remaining -= direction * safeDistance;

            Vector3 wallNormal = hit.normal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude > 0.0001f)
            {
                remaining = Vector3.ProjectOnPlane(
                    remaining,
                    wallNormal.normalized);
            }
            else
            {
                remaining = Vector3.zero;
            }
        }

        resolvedPosition.y = currentPosition.y;
        resolvedPosition.x = Mathf.Clamp(
            resolvedPosition.x,
            minimumX,
            maximumX);
        return resolvedPosition;
    }

    public static Vector3 ResolvePositionOnHalf(
        Rigidbody body,
        Vector3 desiredPosition,
        Vector3 homeDirection,
        float minimumHomeProjection,
        float skin)
    {
        homeDirection.y = 0f;
        if (homeDirection.sqrMagnitude <= 0.0001f)
        {
            return ResolvePosition(
                body,
                desiredPosition,
                float.NegativeInfinity,
                float.PositiveInfinity,
                skin);
        }

        homeDirection.Normalize();
        desiredPosition = ClampToHalf(
            desiredPosition,
            homeDirection,
            minimumHomeProjection);
        Vector3 resolved = ResolvePosition(
            body,
            desiredPosition,
            float.NegativeInfinity,
            float.PositiveInfinity,
            skin);
        return ClampToHalf(
            resolved,
            homeDirection,
            minimumHomeProjection);
    }

    public static Vector3 ResolvePositionOnTeamSide(
        Rigidbody body,
        Vector3 desiredPosition,
        Vector3 arenaCentre,
        Vector3 homeDirection,
        float minimumHomeProjection,
        float skin)
    {
        homeDirection.y = 0f;
        if (homeDirection.sqrMagnitude <= 0.0001f)
        {
            return ResolvePosition(
                body,
                desiredPosition,
                float.NegativeInfinity,
                float.PositiveInfinity,
                skin);
        }

        homeDirection.Normalize();
        desiredPosition = ClampToTeamHalf(
            desiredPosition,
            arenaCentre,
            homeDirection,
            minimumHomeProjection);
        Vector3 resolved = ResolvePosition(
            body,
            desiredPosition,
            float.NegativeInfinity,
            float.PositiveInfinity,
            skin);
        return ClampToTeamHalf(
            resolved,
            arenaCentre,
            homeDirection,
            minimumHomeProjection);
    }

    private static Vector3 ClampToTeamHalf(
        Vector3 position,
        Vector3 arenaCentre,
        Vector3 homeDirection,
        float minimumProjection)
    {
        Vector3 relative = position - arenaCentre;
        float projection = Vector3.Dot(relative, homeDirection);
        if (projection < minimumProjection)
        {
            position += homeDirection * (minimumProjection - projection);
        }

        return position;
    }

    private static Vector3 ClampToHalf(
        Vector3 position,
        Vector3 homeDirection,
        float minimumProjection)
    {
        float projection = Vector3.Dot(position, homeDirection);
        if (projection < minimumProjection)
        {
            position += homeDirection * (minimumProjection - projection);
        }

        return position;
    }

    private static bool TryFindBoundaryHit(
        SphereCollider sphere,
        Vector3 worldCentre,
        float radius,
        Vector3 direction,
        float distance,
        float skin,
        bool allowPenetrationCheck,
        out RaycastHit nearestHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            worldCentre,
            radius,
            direction,
            SweepHits,
            distance + skin,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool foundSurface = false;
        nearestHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = SweepHits[i];
            Collider collider = hit.collider;
            if (collider == null ||
                collider == sphere ||
                collider.attachedRigidbody != null ||
                !IsAuthoredArenaBoundary(collider) ||
                hit.distance >= nearestDistance)
            {
                continue;
            }
            if (IsMovingAwayFromBoundary(
                    sphere,
                    collider,
                    worldCentre,
                    direction,
                    hit,
                    skin,
                    allowPenetrationCheck))
            {
                continue;
            }

            foundSurface = true;
            nearestHit = hit;
            nearestDistance = hit.distance;
        }

        return foundSurface;
    }

    public static bool IsMovingAwayFromBoundary(
        SphereCollider sphere,
        Collider boundary,
        Vector3 worldCentre,
        Vector3 movementDirection,
        RaycastHit hit,
        float skin,
        bool allowPenetrationCheck)
    {
        if (hit.distance > Mathf.Max(0.002f, skin * 2f))
        {
            return false;
        }

        if (allowPenetrationCheck &&
            Physics.ComputePenetration(
                sphere,
                sphere.transform.position,
                sphere.transform.rotation,
                boundary,
                boundary.transform.position,
                boundary.transform.rotation,
                out Vector3 separationDirection,
                out _))
        {
            separationDirection.y = 0f;
            if (separationDirection.sqrMagnitude > 0.0001f)
            {
                return Vector3.Dot(
                           movementDirection,
                           separationDirection.normalized) > 0.001f;
            }
        }

        bool supportsClosestPoint =
            boundary is BoxCollider ||
            boundary is SphereCollider ||
            boundary is CapsuleCollider ||
            boundary is MeshCollider boundaryMesh && boundaryMesh.convex;
        if (supportsClosestPoint)
        {
            Vector3 awayFromSurface =
                worldCentre - boundary.ClosestPoint(worldCentre);
            awayFromSurface.y = 0f;
            if (awayFromSurface.sqrMagnitude > 0.000001f)
            {
                return Vector3.Dot(
                           movementDirection,
                           awayFromSurface.normalized) > 0.001f;
            }
        }

        Vector3 hitNormal = hit.normal;
        hitNormal.y = 0f;
        return hitNormal.sqrMagnitude > 0.0001f &&
               Vector3.Dot(
                   movementDirection,
                   hitNormal.normalized) > 0.001f;
    }

    public static bool IsAuthoredArenaBoundary(Collider collider)
    {
        Transform current = collider.transform;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName.IndexOf(
                    "Wall",
                    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf(
                    "Corner",
                    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf(
                    "Goal",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    public static bool IsGoalBack(Collider collider)
    {
        Transform current = collider != null
            ? collider.transform
            : null;
        while (current != null)
        {
            if (current.name.IndexOf(
                    "Goal Back",
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
