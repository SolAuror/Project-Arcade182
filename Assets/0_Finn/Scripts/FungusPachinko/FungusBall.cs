using System;
using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// A single pachinko ball. Purely physical: it falls, bounces off pegs and bumpers,
    /// and reports when it is done (drained, settled, or timed out). The ball never
    /// scores anything itself — lights detect the ball, not the other way around.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Ball")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class FungusBall : MonoBehaviour
    {
        [SerializeField] private float settleSpeedThreshold = 0.05f;
        [SerializeField] private float settleSeconds = 2f;
        [SerializeField] private float maxLifetimeSeconds = 30f;

        [Header("Collision Audio")]
        [SerializeField] private AudioClip[] collisionClips;
        [SerializeField] private float minCollisionVelocity = 0.5f;
        [SerializeField] private float maxImpactVelocity = 5f;
        [SerializeField] private float minVolume = 0.2f;
        [SerializeField] private float maxVolume = 0.8f;
        [SerializeField] private float minPitch = 0.9f;
        [SerializeField] private float maxPitch = 1.1f;
        [SerializeField] private float soundCooldown = 0.03f;

        /// <summary>Raised exactly once when the ball is finished, before it is destroyed.</summary>
        public event Action<FungusBall> Finished;

        private Rigidbody body;
        private AudioSource audioSource;

        private float settleTimer;
        private float lifeTimer;
        private float lastCollisionSoundTime;

        private bool finished;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();

            body.constraints = RigidbodyConstraints.FreezePositionZ |
                               RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationY;

            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (finished || collisionClips.Length == 0)
            {
                return;
            }

            if (Time.time - lastCollisionSoundTime < soundCooldown)
            {
                return;
            }

            float impactSpeed = collision.relativeVelocity.magnitude;

            if (impactSpeed < minCollisionVelocity)
            {
                return;
            }

            lastCollisionSoundTime = Time.time;

            AudioClip clip = collisionClips[
                UnityEngine.Random.Range(0, collisionClips.Length)
            ];

            float impactStrength = Mathf.Clamp01(
                impactSpeed / maxImpactVelocity
            );

            float volume = Mathf.Lerp(
                minVolume,
                maxVolume,
                impactStrength
            );

            audioSource.pitch = UnityEngine.Random.Range(
                minPitch,
                maxPitch
            );

            audioSource.PlayOneShot(clip, volume);
        }

        private void FixedUpdate()
        {
            if (finished)
            {
                return;
            }

            lifeTimer += Time.fixedDeltaTime;

            if (lifeTimer >= maxLifetimeSeconds)
            {
                Finish();
                return;
            }

            if (body.linearVelocity.sqrMagnitude <
                settleSpeedThreshold * settleSpeedThreshold)
            {
                settleTimer += Time.fixedDeltaTime;

                if (settleTimer >= settleSeconds)
                {
                    Finish();
                }
            }
            else
            {
                settleTimer = 0f;
            }
        }

        /// <summary>
        /// Retires the ball (drain, settle, or timeout). Safe to call repeatedly.
        /// </summary>
        public void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            Finished?.Invoke(this);
        }
    }
}
