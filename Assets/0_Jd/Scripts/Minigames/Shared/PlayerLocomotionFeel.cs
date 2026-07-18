using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// First-person locomotion feel for the player rig: subtle head bob and
    /// footstep audio, each individually toggleable in the inspector. Reads
    /// the CharacterController's velocity; the head is the rig's child camera,
    /// resolved lazily. Footsteps fire on the bob's footfall beat so motion
    /// and audio stay in phase - the bob keeps time even while footsteps are
    /// toggled off, and vice versa.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Player/Player Locomotion Feel")]
    public class PlayerLocomotionFeel : MonoBehaviour
    {
        [Header("Toggles")]
        [SerializeField] private bool headBobEnabled = true;
        [SerializeField] private bool footstepsEnabled = true;

        [Header("Head Bob")]
        [Tooltip("Vertical dip depth in meters at full stride.")]
        [SerializeField, Min(0f)] private float bobAmplitude = 0.05f;

        [Tooltip("Sideways sway as a fraction of the vertical amplitude.")]
        [SerializeField, Range(0f, 1f)] private float swayFraction = 0.45f;

        [Tooltip("Footfalls per second at the reference speed.")]
        [SerializeField, Min(0.1f)] private float strideFrequency = 0.9f;

        [Tooltip("Speed (m/s) that maps to a full-intensity stride.")]
        [SerializeField, Min(0.1f)] private float referenceSpeed = 5f;

        [Tooltip("Horizontal speed where the cadence uses the sprint multiplier.")]
        [SerializeField, Min(0.1f)] private float sprintSpeedThreshold = 6.5f;

        [Tooltip("Footstep cadence multiplier once sprinting.")]
        [SerializeField, Min(1f)] private float sprintStrideMultiplier = 1.25f;

        [Tooltip("How quickly the bob eases in and out (per second).")]
        [SerializeField, Min(0.1f)] private float bobSmoothing = 5f;

        [Header("Footsteps")]
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.55f;

        [Tooltip("Quiet layer mixed under the main step for extra surface detail.")]
        [SerializeField] private AudioClip[] footstepLayerClips;

        [SerializeField, Range(0f, 1f)] private float footstepLayerVolume = 0.18f;

        [Tooltip("Adds a quiet reverberant copy of each step for dungeon/stone-room ambience.")]
        [SerializeField] private bool footstepReverbEnabled = true;

        [SerializeField, Range(0f, 1f)] private float footstepReverbVolume = 0.2f;

        [SerializeField] private AudioReverbPreset footstepReverbPreset = AudioReverbPreset.StoneCorridor;

        [Tooltip("Random pitch spread around 1 for variety.")]
        [SerializeField, Range(0f, 0.5f)] private float pitchJitter = 0.12f;

        private CharacterController body;
        private Transform head;
        private Vector3 headBasePosition;
        private AudioSource stepSource;
        private AudioSource layerSource;
        private AudioSource reverbSource;
        private AudioReverbFilter reverbFilter;
        private float phase;     // stride phase; one footfall per PI
        private float intensity; // 0..1 smoothed movement intensity
        private int lastStepBeat = int.MinValue;
        private int lastClipIndex = -1;
        private int lastLayerClipIndex = -1;

        public bool HeadBobEnabled { get => headBobEnabled; set => headBobEnabled = value; }
        public bool FootstepsEnabled { get => footstepsEnabled; set => footstepsEnabled = value; }

        private void Awake()
        {
            body = GetComponent<CharacterController>();

            stepSource = gameObject.AddComponent<AudioSource>();
            ConfigureFootstepSource(stepSource);

            layerSource = gameObject.AddComponent<AudioSource>();
            ConfigureFootstepSource(layerSource);

            reverbSource = gameObject.AddComponent<AudioSource>();
            ConfigureFootstepSource(reverbSource);
            reverbFilter = gameObject.AddComponent<AudioReverbFilter>();
            reverbFilter.reverbPreset = footstepReverbPreset;
            reverbFilter.enabled = footstepReverbEnabled;
        }

        private void LateUpdate()
        {
            if (head == null && !TryCaptureHead())
            {
                return;
            }

            float speed = 0f;
            bool grounded = true;
            if (body != null)
            {
                Vector3 velocity = body.velocity;
                velocity.y = 0f;
                speed = velocity.magnitude;
                grounded = body.isGrounded;
            }

            float speedRatio = Mathf.Clamp01(speed / referenceSpeed);
            float target = grounded ? speedRatio : 0f;
            intensity = Mathf.MoveTowards(intensity, target, bobSmoothing * Time.deltaTime);

            if (intensity > 0.02f)
            {
                // stride keeps loose time with actual speed: slower shuffle,
                // faster sprint cadence
                float cadence = strideFrequency;
                if (speed >= sprintSpeedThreshold)
                {
                    cadence *= sprintStrideMultiplier;
                }

                phase += Mathf.Lerp(0.6f, 1.2f, speedRatio) * cadence * Mathf.PI * Time.deltaTime * 2f;

                // footfall lands at the bob's deepest dip (phase = PI/2 + k*PI)
                int beat = Mathf.FloorToInt((phase - Mathf.PI * 0.5f) / Mathf.PI);
                if (beat != lastStepBeat)
                {
                    lastStepBeat = beat;
                    if (footstepsEnabled && grounded && intensity > 0.3f)
                    {
                        PlayFootstep();
                    }
                }
            }

            Vector3 offset = Vector3.zero;
            if (headBobEnabled && intensity > 0.001f)
            {
                offset.y = -Mathf.Abs(Mathf.Sin(phase)) * bobAmplitude * intensity;
                offset.x = Mathf.Cos(phase) * bobAmplitude * swayFraction * intensity;
            }

            head.localPosition = headBasePosition + offset;
        }

        private void PlayFootstep()
        {
            if (footstepClips == null || footstepClips.Length == 0 || stepSource == null)
            {
                return;
            }

            int index = Random.Range(0, footstepClips.Length);
            if (footstepClips.Length > 1 && index == lastClipIndex)
            {
                index = (index + 1) % footstepClips.Length; // never the same twice
            }

            lastClipIndex = index;
            AudioClip clip = footstepClips[index];
            if (clip == null)
            {
                return;
            }

            float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            stepSource.pitch = pitch;
            stepSource.PlayOneShot(clip, footstepVolume);

            PlayFootstepLayer(pitch);

            if (footstepReverbEnabled && reverbSource != null)
            {
                reverbFilter.reverbPreset = footstepReverbPreset;
                reverbFilter.enabled = true;
                reverbSource.pitch = pitch * 0.96f;
                reverbSource.PlayOneShot(clip, footstepReverbVolume);
            }
            else if (reverbFilter != null)
            {
                reverbFilter.enabled = false;
            }
        }

        private void PlayFootstepLayer(float basePitch)
        {
            if (footstepLayerClips == null || footstepLayerClips.Length == 0 || layerSource == null)
            {
                return;
            }

            int index = Random.Range(0, footstepLayerClips.Length);
            if (footstepLayerClips.Length > 1 && index == lastLayerClipIndex)
            {
                index = (index + 1) % footstepLayerClips.Length;
            }

            lastLayerClipIndex = index;
            AudioClip clip = footstepLayerClips[index];
            if (clip == null)
            {
                return;
            }

            layerSource.pitch = basePitch * Random.Range(0.92f, 1.08f);
            layerSource.PlayOneShot(clip, footstepLayerVolume);
        }

        private static void ConfigureFootstepSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.spatialBlend = 0f; // your own feet are not spatial
        }

        private bool TryCaptureHead()
        {
            Camera rigCamera = GetComponentInChildren<Camera>(true);
            if (rigCamera == null)
            {
                return false;
            }

            head = rigCamera.transform;
            headBasePosition = head.localPosition;
            return true;
        }
    }
}
