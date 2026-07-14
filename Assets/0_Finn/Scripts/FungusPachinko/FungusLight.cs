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
    public class FungusLight : MonoBehaviour
    {
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private Material litMaterial;
        [SerializeField] private Material unlitMaterial;

        public bool IsLit { get; private set; } = true;

        /// <summary>Raised once when a ball turns this light off.</summary>
        public event Action<FungusLight> TurnedOff;

        private Collider triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();
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
