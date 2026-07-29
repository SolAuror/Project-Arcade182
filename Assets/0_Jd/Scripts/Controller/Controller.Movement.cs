using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        private void UpdateMovement()
        {
            bool isFixedSideOn = cameraMode == CameraMode.FixedSideOn;
            isGrounded = isFixedSideOn ||
                         verticalSpeed <= 0f &&
                         (characterController.isGrounded || CheckGrounded()); // determine grounded state

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
            Vector3 desiredMovementVelocity = desiredMovementDirection * baseMovementSpeed * GetMovementSpeedMultiplier() * externalSpeedMultiplier; // desired velocity

            if (FacesMovement() && desiredMovementDirection.sqrMagnitude > MeaningfulMovementInputSquared)
            {
                Quaternion movementFacingRotation = Quaternion.LookRotation(desiredMovementDirection); // target rotation
                transform.rotation = Quaternion.RotateTowards(transform.rotation, movementFacingRotation, characterTurnSpeed * GetTurnSpeedMultiplier() * Time.deltaTime); // rotate toward movement
            }

            bool hasMovementInput = movementInput.sqrMagnitude > MeaningfulMovementInputSquared;
            float velocityChangeRate = GetHorizontalVelocityChangeRate(hasMovementInput, desiredMovementVelocity);

            horizontalMovementVelocity = Vector3.MoveTowards(horizontalMovementVelocity, desiredMovementVelocity, velocityChangeRate * Time.deltaTime); // smooth velocity

            if (isFixedSideOn)
            {
                verticalSpeed = 0f; // fixed board mode is planar, not a gravity platformer
            }
            else if (isGrounded && verticalSpeed <= 0f)
            {
                verticalSpeed = groundedVerticalSpeed; // reset vertical speed on ground
            }
            else
            {
                float gravityMultiplier = verticalSpeed < 0f ? fallGravityMultiplier : GetRisingGravityMultiplier(); // gravity modifier

                verticalSpeed += gravity * gravityMultiplier * Time.deltaTime; // apply gravity
            }

            CollisionFlags collisionFlags =
                characterController.Move((horizontalMovementVelocity + Vector3.up * verticalSpeed) * Time.deltaTime); // move character

            ResolveVerticalCollisions(collisionFlags);

            if (isFixedSideOn)
            {
                LockFixedSideOnPlane();
            }
        }

        private float GetHorizontalVelocityChangeRate(bool hasMovementInput, Vector3 desiredMovementVelocity)
        {
            if (!isGrounded)
            {
                return airAcceleration;
            }

            if (!hasMovementInput)
            {
                return deceleration;
            }

            if (horizontalMovementVelocity.sqrMagnitude <= MeaningfulMovementInputSquared ||
                desiredMovementVelocity.sqrMagnitude <= MeaningfulMovementInputSquared)
            {
                return acceleration;
            }

            float directionAlignment = Vector3.Dot(
                horizontalMovementVelocity.normalized,
                desiredMovementVelocity.normalized);

            // Direction changes should shed old momentum much faster than a
            // same-direction speed change. This keeps strafing and reversals
            // responsive without making ordinary acceleration instantaneous.
            float directionChangeAmount = Mathf.InverseLerp(1f, -1f, directionAlignment);
            return acceleration * Mathf.Lerp(1f, directionChangeAccelerationMultiplier, directionChangeAmount);
        }

        private bool CheckGrounded()
        {
            float groundCheckRadius = characterController.radius * groundCheckRadiusScale;
            float probeStartOffset = Mathf.Max(characterController.skinWidth, 0.02f);
            Bounds controllerBounds = characterController.bounds;
            Vector3 groundCheckPosition = new Vector3(
                controllerBounds.center.x,
                controllerBounds.min.y + groundCheckRadius + probeStartOffset,
                controllerBounds.center.z);
            float probeDistance = probeStartOffset + groundCheckDistance;

            int hitCount = Physics.SphereCastNonAlloc(
                groundCheckPosition,
                groundCheckRadius,
                Vector3.down,
                groundProbeHits,
                probeDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float minimumGroundNormalY = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundProbeHits[i];
                if (hit.collider != null && hit.normal.y >= minimumGroundNormalY)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveVerticalCollisions(CollisionFlags collisionFlags)
        {
            if ((collisionFlags & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
            {
                verticalSpeed = 0f;
            }

            if ((collisionFlags & CollisionFlags.Below) != 0 && verticalSpeed <= 0f)
            {
                isGrounded = true;
                verticalSpeed = groundedVerticalSpeed;
            }
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

            movementRight = Vector3.ProjectOnPlane(gameplayCamera.transform.right, Vector3.up).normalized; // project camera axes
            movementForward = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized;

            if (movementForward.sqrMagnitude < MeaningfulMovementInputSquared)
                movementForward = cameraMovementFallbackHeading * Vector3.forward; // fallback forward

            if (movementRight.sqrMagnitude < MeaningfulMovementInputSquared)
                movementRight = cameraMovementFallbackHeading * Vector3.right; // fallback right
        }

        private bool AllowsJumping()
        {
            return cameraMode != CameraMode.FixedSideOn; // fixed board play disables jumping
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
                CameraMode.Isometric => HasValidIsometricSprintDirection(movementInput),
                CameraMode.FixedSideOn => HasValidFixedSideOnSprintDirection(movementInput),
                _ => false
            }; // delegate sprint checks per mode
        }

        private Vector3 GetDesiredMovementDirection(Vector2 movementInput, Vector3 movementRight, Vector3 movementForward)
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => GetFirstPersonMovementDirection(movementInput, movementRight, movementForward),
                CameraMode.ThirdPerson => GetThirdPersonMovementDirection(movementInput, movementRight, movementForward),
                CameraMode.Isometric => GetIsometricMovementDirection(movementInput, movementRight, movementForward),
                CameraMode.FixedSideOn => GetFixedSideOnMovementDirection(movementInput, movementRight),
                _ => Vector3.zero
            }; // choose movement calculation by mode
        }

        private float GetMovementSpeedMultiplier()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => firstPersonMovementSpeedMultiplier,
                CameraMode.ThirdPerson => thirdPersonMovementSpeedMultiplier,
                CameraMode.Isometric => isometricMovementSpeedMultiplier,
                CameraMode.FixedSideOn => fixedSideOnMovementSpeedMultiplier,
                _ => 1f
            };
        }

        private float GetTurnSpeedMultiplier()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => firstPersonTurnSpeedMultiplier,
                CameraMode.ThirdPerson => thirdPersonTurnSpeedMultiplier,
                CameraMode.Isometric => isometricTurnSpeedMultiplier,
                CameraMode.FixedSideOn => fixedSideOnTurnSpeedMultiplier,
                _ => 1f
            };
        }

        private bool FacesMovement()
        {
            return cameraMode switch
            {
                CameraMode.FirstPerson => FirstPersonFacesMovement(),
                CameraMode.ThirdPerson => ThirdPersonFacesMovement(),
                CameraMode.Isometric => IsometricFacesMovement(),
                CameraMode.FixedSideOn => true,
                _ => true
            };
        }

        private static Vector3 GetFixedSideOnMovementDirection(Vector2 movementInput, Vector3 movementRight)
        {
            return movementRight * movementInput.x;
        }

        private static bool HasValidFixedSideOnSprintDirection(Vector2 movementInput)
        {
            return Mathf.Abs(movementInput.x) > MeaningfulMovementInput;
        }
    }
}
