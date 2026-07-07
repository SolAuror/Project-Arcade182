using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.2f; // desired jump height
        [SerializeField] private float lowJumpGravityMultiplier = 2.5f; // stronger gravity for low jumps
        [SerializeField] private float coyoteTime = 0.12f; // grace period after leaving ground
        [SerializeField] private float jumpBufferTime = 0.12f; // buffer time for jump input before landing

        private float coyoteTimeRemaining; // remaining coyote time
        private float jumpBufferTimeRemaining; // remaining jump buffer

        private void UpdateJumpTimers()
        {
            coyoteTimeRemaining = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimeRemaining - Time.deltaTime); // update coyote

            jumpBufferTimeRemaining = inputActions.Player.Jump.WasPressedThisFrame() ? jumpBufferTime : Mathf.Max(0f, jumpBufferTimeRemaining - Time.deltaTime); // update buffer
        }

        private void ApplyJump()
        {
            if (jumpBufferTimeRemaining <= 0f || coyoteTimeRemaining <= 0f)
                return; // can't jump yet

            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity); // set vertical speed for jump
            ClearJumpTimers(); // consume timers
            isGrounded = false; // we're airborne
        }

        private void ClearJumpTimers()
        {
            coyoteTimeRemaining = 0f; // reset coyote
            jumpBufferTimeRemaining = 0f; // reset buffer
        }
    }
}
