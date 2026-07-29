using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Impact effects for the Atom Smasher's standalone board camera: a
    /// trauma-driven shake (offset + roll) with a small FOV kick. Safe here
    /// because this camera is not shared with the player controller. Events
    /// feed <see cref="AddTrauma"/>; intensity decays on unscaled time so
    /// hitstop never freezes the shake mid-jolt.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Camera Fx")]
    public class AtomSmasherCameraFx : MonoBehaviour
    {
        [Header("Shake")]
        [Tooltip("Positional shake at full trauma, in world units.")]
        [SerializeField, Min(0f)] private float maxShakeOffset = 0.16f;

        [SerializeField, Min(0f)] private float maxShakeRollDegrees = 1.1f;

        [Tooltip("Trauma lost per second; higher settles faster.")]
        [SerializeField, Min(0.1f)] private float traumaDecayPerSecond = 1.7f;

        [SerializeField, Min(0.1f)] private float shakeNoiseSpeed = 14f;

        [Header("FOV Kick")]
        [Tooltip("Degrees added to the field of view at full trauma.")]
        [SerializeField, Min(0f)] private float fovKickDegrees = 2.5f;

        private Camera fxCamera;
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float baseFieldOfView;
        private float baseOrthographicSize;
        private float trauma;

        /// <summary>Adds shake intensity (clamped 0-1). Small taps stack into bigger jolts.</summary>
        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
        }

        /// <summary>Re-reads the camera's projection as the shake baseline (call after a 2D/3D toggle).</summary>
        public void RefreshBaseProjection()
        {
            if (fxCamera != null)
            {
                baseFieldOfView = fxCamera.fieldOfView;
                baseOrthographicSize = fxCamera.orthographicSize;
            }
        }

        private void Awake()
        {
            fxCamera = GetComponent<Camera>();
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            RefreshBaseProjection();
        }

        private void OnDisable()
        {
            trauma = 0f;
            transform.SetLocalPositionAndRotation(baseLocalPosition, baseLocalRotation);
            RestoreProjection();
        }

        private void LateUpdate()
        {
            if (trauma <= 0f)
            {
                return;
            }

            trauma = Mathf.Max(0f, trauma - traumaDecayPerSecond * Time.unscaledDeltaTime);
            float shake = trauma * trauma; // squared so small trauma stays subtle

            if (shake <= 0.0001f)
            {
                transform.SetLocalPositionAndRotation(baseLocalPosition, baseLocalRotation);
                RestoreProjection();
                return;
            }

            float time = Time.unscaledTime * shakeNoiseSpeed;
            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(time, 0.3f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0.7f, time) - 0.5f) * 2f,
                0f) * (maxShakeOffset * shake);
            float roll = (Mathf.PerlinNoise(time, 9.1f) - 0.5f) * 2f * maxShakeRollDegrees * shake;

            transform.SetLocalPositionAndRotation(
                baseLocalPosition + offset,
                baseLocalRotation * Quaternion.Euler(0f, 0f, roll));

            // Kick whichever projection is live; ortho uses the same relative zoom.
            if (fxCamera.orthographic)
            {
                float kickFraction = baseFieldOfView > 0f ? fovKickDegrees / baseFieldOfView : 0f;
                fxCamera.orthographicSize = baseOrthographicSize * (1f + kickFraction * shake);
            }
            else
            {
                fxCamera.fieldOfView = baseFieldOfView + fovKickDegrees * shake;
            }
        }

        private void RestoreProjection()
        {
            if (fxCamera == null)
            {
                return;
            }

            fxCamera.fieldOfView = baseFieldOfView;
            fxCamera.orthographicSize = baseOrthographicSize;
        }
    }
}
