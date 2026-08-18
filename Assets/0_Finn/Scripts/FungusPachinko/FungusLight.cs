using System;
using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// One board light. A lit light turns off the first time a ball passes through its
    /// trigger, worth one point. Trigger-based on purpose: balls should sail through
    /// lights freely instead of bouncing off them like pegs.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Light")]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(AudioSource))]
    public class FungusLight : MonoBehaviour
    {
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private Material litMaterial;
        [SerializeField] private Material unlitMaterial;

        [Header("Light Audio")]
        [SerializeField] private AudioClip lightSound;
        [SerializeField] private float soundVolume = 1f;
        [SerializeField] private float startingPitch = 1f;
        [SerializeField] private float pitchIncreasePerLight = 0.05f;
        [SerializeField] private float maxPitch = 2f;

        public bool IsLit { get; private set; } = true;

        /// <summary>Raised once when a ball turns this light off.</summary>
        public event Action<FungusLight> TurnedOff;

        private Collider triggerCollider;
        private AudioSource audioSource;

        private static int lightsCollected;


        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();
            audioSource = GetComponent<AudioSource>();

            ApplyVisual();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsLit || other.GetComponentInParent<FungusBall>() == null)
            {
                return;
            }

            TurnOff();
        }

        public void TurnOff()
        {
            if (!IsLit)
            {
                return;
            }

            IsLit = false;

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            ApplyVisual();

            lightsCollected++;

            float pitch = startingPitch + (lightsCollected - 1) * pitchIncreasePerLight;

            audioSource.pitch = Mathf.Min(pitch, maxPitch);

            if (lightSound != null)
            {
                audioSource.PlayOneShot(lightSound, soundVolume);
            }

            TurnedOff?.Invoke(this);
        }

        /// <summary>Relights the light for a fresh board (replay / attract mode).</summary>
        public void ResetLight()
        {
            IsLit = true;
            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
            }

            ApplyVisual();
        }

        public static void ResetCollectionCount()
        {
            lightsCollected = 0;
        }

        private void ApplyVisual()
        {
            if (visualRenderer == null)
            {
                return;
            }

            Material target = IsLit ? litMaterial : unlitMaterial;
            if (target != null)
            {
                visualRenderer.sharedMaterial = target;
            }
        }
    }
}
