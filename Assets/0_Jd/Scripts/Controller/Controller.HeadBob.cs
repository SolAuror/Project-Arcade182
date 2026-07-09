using UnityEngine; // Unity core

namespace Player
{
    public partial class Controller
    {
        [Header("Head Bob")]
        [SerializeField] private bool enableHeadBob = true; // toggle for the first person walk bob

        [Tooltip("Vertical bob height at walk speed.")]
        [SerializeField, Min(0f)] private float headBobAmplitude = 0.045f;

        [Tooltip("Step cycles per second at walk speed; scales with actual speed.")]
        [SerializeField, Min(0.1f)] private float headBobFrequency = 1.7f;

        [Tooltip("Sideways sway as a fraction of the vertical bob.")]
        [SerializeField, Range(0f, 1f)] private float headBobSwayRatio = 0.55f;

        [Tooltip("How quickly the bob fades in/out when starting or stopping.")]
        [SerializeField, Min(0.5f)] private float headBobSettleSpeed = 6f;

        private Vector3 headBobBaseLocalPosition; // authored FP camera local position
        private bool headBobBaseCaptured;
        private float headBobPhase;
        private float headBobWeight;

        // The FP rig is a Cinemachine vcam with rotation-only control, so its
        // transform position is the live camera position; offsetting its local
        // position bobs the view without fighting Cinemachine.
        private void UpdateHeadBob()
        {
            if (firstPersonCamera == null)
                return;

            Transform cameraTransform = firstPersonCamera.transform;
            if (!headBobBaseCaptured)
            {
                headBobBaseLocalPosition = cameraTransform.localPosition;
                headBobBaseCaptured = true;
            }

            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            bool bobbing = enableHeadBob &&
                           cameraMode == CameraMode.FirstPerson &&
                           isGrounded &&
                           speed > MeaningfulMovementInput;

            headBobWeight = Mathf.MoveTowards(headBobWeight, bobbing ? 1f : 0f, headBobSettleSpeed * Time.deltaTime);

            if (headBobWeight <= 0f)
            {
                if (cameraTransform.localPosition != headBobBaseLocalPosition)
                    cameraTransform.localPosition = headBobBaseLocalPosition;

                headBobPhase = 0f; // next walk starts at the bottom of the step
                return;
            }

            float speedScale = walkSpeed > 0f ? Mathf.Clamp(speed / walkSpeed, 0.5f, 1.6f) : 1f;
            headBobPhase += Time.deltaTime * headBobFrequency * speedScale * Mathf.PI * 2f;

            // Classic figure-8: vertical bounces once per step, sway once per stride.
            float vertical = Mathf.Sin(headBobPhase * 2f) * headBobAmplitude;
            float lateral = Mathf.Sin(headBobPhase) * headBobAmplitude * headBobSwayRatio;

            cameraTransform.localPosition = headBobBaseLocalPosition + new Vector3(lateral, vertical, 0f) * headBobWeight;
        }
    }
}
