using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        private void UpdateMovement()
        {
            bool isFixedSideOn = cameraMode == CameraMode.FixedSideOn;
            isGrounded = isFixedSideOn || verticalSpeed <= 0f && CheckGrounded(); // determine grounded state

            if (AllowsJumping())
            {
                UpdateJumpTimers(); // update jump timers
                ApplyJump(); // apply jump if triggered
            }
            else
            {
                ClearJumpTimers(); // clear jump-related timers
            }

            Vector2 movementInput = Vector2.ClampMagnitude(inputActions.Player.Move.ReadValue<Vector2>(), 1f); // read movement input
            bool isSprinting = inputActions.Player.Sprint.IsPressed() && HasValidSprintDirection(movementInput); // sprint if valid
            float baseMovementSpeed = isSprinting ? sprintSpeed : walkSpeed; // choose base speed

            GetMovementBasis(out Vector3 movementRight, out Vector3 movementForward); // get movement axes
            Vector3 desiredMovementDirection = GetDesiredMovementDirection(movementInput, movementRight, movementForward); // desired direction
            Vector3 desiredMovementVelocity = desiredMovementDirection * baseMovementSpeed * GetMovementSpeedMultiplier(); // desired velocity

            if (FacesMovement() && desiredMovementDirection.sqrMagnitude > MeaningfulMovementInputSquared)
            {
                Quaternion movementFacingRotation = Quaternion.LookRotation(desiredMovementDirection); // target rotation
                transform.rotation = Quaternion.RotateTowards(transform.rotation, movementFacingRotation, characterTurnSpeed * GetTurnSpeedMultiplier() * Time.deltaTime); // rotate toward movement
            }

            float velocityChangeRate = isGrounded
                ? (movementInput.sqrMagnitude > MeaningfulMovementInputSquared ? acceleration : deceleration)
                : airAcceleration; // choose acceleration rate

            horizontalMovementVelocity = Vector3.MoveTowards(horizontalMovementVelocity, desiredMovementVelocity, velocityChangeRate * Time.deltaTime); // smooth velocity

            if (isFixedSideOn)
            {
                verticalSpeed = 0f; // fixed board mode is planar, not a gravity platformer
            }
            else if (isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = groundedVerticalSpeed; // reset vertical speed on ground
            }

            if (!isFixedSideOn)
            {
                float gravityMultiplier = verticalSpeed < 0f ? fallGravityMultiplier : GetRisingGravityMultiplier(); // gravity modifier

                verticalSpeed += gravity * gravityMultiplier * Time.deltaTime; // apply gravity
            }

            characterController.Move((horizontalMovementVelocity + Vector3.up * verticalSpeed) * Time.deltaTime); // move character

            if (isFixedSideOn)
            {
                LockFixedSideOnPlane();
            }
        }

        private bool CheckGrounded()
        {
            float groundCheckRadius = characterController.radius * groundCheckRadiusScale; // sphere radius
            Vector3 groundCheckPosition = characterController.bounds.center + Vector3.down * (characterController.bounds.extents.y - groundCheckRadius + groundCheckDistance); // sphere center

            return Physics.CheckSphere(groundCheckPosition, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore); // physics check
        }

        private void GetMovementBasis(out Vector3 movementRight, out Vector3 movementForward)
        {
            Camera gameplayCamera = GetGameplayCamera(); // preferred camera
            if (gameplayCamera == null)
            {
                movementRight = transform.right; // fallback axes
                movementForward = transform.forward;
                return;
            }

            if (TryGetModeMovementBasis(gameplayCamera, out movementRight, out movementForward))
                return; // mode-specific basis

            movementRight = Vector3.ProjectOnPlane(gameplayCamera.transform.right, Vector3.up).normalized; // project camera axes
            movementForward = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized;

            if (movementForward.sqrMagnitude < MeaningfulMovementInputSquared)
                movementForward = cameraMovementFallbackHeading * Vector3.forward; // fallback forward

            if (movementRight.sqrMagnitude < MeaningfulMovementInputSquared)
                movementRight = cameraMovementFallbackHeading * Vector3.right; // fallback right
        }

        private bool TryGetModeMovementBasis(Camera gameplayCamera, out Vector3 movementRight, out Vector3 movementForward)
        {
            if (cameraMode == CameraMode.TopDown)
                return TryGetTopDownMovementBasis(gameplayCamera, out movementRight, out movementForward); // top-down basis

            movementRight = Vector3.zero; // no special basis
            movementForward = Vector3.zero;
            return false;
        }

        private bool AllowsJumping()
        {
            return cameraMode != CameraMode.TopDown && cameraMode != CameraMode.FixedSideOn; // fixed board play disables jumping
        }

        private void LockFixedSideOnPlane()
        {
            Vector3 position = transform.position;
            position.z = fixedSideOnPlaneZ;
            transform.position = position;
        }

        private float GetRisingGravityMultiplier()
        {
            return verticalSpeed > 0f && AllowsJumping() && !inputActions.Player.Jump.IsPressed() ? lowJumpGravityMultiplier : 1f; // low jump gravity
        }

        private bool HasValidSprintDirection(Vector2 movementInput)
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => HasValidFirstPersonSprintDirection(movementInput),
                CameraMode.ThirdPerson => HasValidThirdPersonSprintDirection(movementInput),
                CameraMode.TopDown => HasValidTopDownSprintDirection(movementInput),
                CameraMode.Isometric => HasValidIsometricSprintDirection(movementInput),
                CameraMode.Platformer => HasValidPlatformerSprintDirection(movementInput),
                CameraMode.FixedSideOn => HasValidPlatformerSprintDirection(movementInput),
                _ => false
            }; // delegate sprint checks per mode
        }

        private Vector3 GetDesiredMovementDirection(Vector2 movementInput, Vector3 movementRight, Vector3 movementForward)
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => GetFirstPersonMovementDirection(movementInput, movementRight, movementForward),
                CameraMode.ThirdPerson => GetThirdPersonMovementDirection(movementInput, movementRight, movementForward),
                CameraMode.TopDown => GetTopDownMovementDirection(movementInput, movementRight, movementForward),
                CameraMode.Isometric => GetIsometricMovementDirection(movementInput, movementRight, movementForward),
                CameraMode.Platformer => GetPlatformerMovementDirection(movementInput, movementRight),
                CameraMode.FixedSideOn => GetPlatformerMovementDirection(movementInput, movementRight),
                _ => Vector3.zero
            }; // choose movement calculation by mode
        }

        private float GetMovementSpeedMultiplier()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => firstPersonMovementSpeedMultiplier,
                CameraMode.ThirdPerson => thirdPersonMovementSpeedMultiplier,
                CameraMode.TopDown => topDownMovementSpeedMultiplier,
                CameraMode.Isometric => isometricMovementSpeedMultiplier,
                CameraMode.Platformer => platformerMovementSpeedMultiplier,
                CameraMode.FixedSideOn => platformerMovementSpeedMultiplier,
                _ => 1f
            };
        }

        private float GetTurnSpeedMultiplier()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => firstPersonTurnSpeedMultiplier,
                CameraMode.ThirdPerson => thirdPersonTurnSpeedMultiplier,
                CameraMode.TopDown => topDownTurnSpeedMultiplier,
                CameraMode.Isometric => isometricTurnSpeedMultiplier,
                CameraMode.Platformer => platformerTurnSpeedMultiplier,
                CameraMode.FixedSideOn => platformerTurnSpeedMultiplier,
                _ => 1f
            };
        }

        private bool FacesMovement()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => FirstPersonFacesMovement(),
                CameraMode.ThirdPerson => ThirdPersonFacesMovement(),
                CameraMode.TopDown => TopDownFacesMovement(),
                CameraMode.Isometric => IsometricFacesMovement(),
                CameraMode.Platformer => PlatformerFacesMovement(),
                CameraMode.FixedSideOn => PlatformerFacesMovement(),
                _ => true
            };
        }
    }
}
