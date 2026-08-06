using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonReflex
{
    [DisallowMultipleComponent]
    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float rayDistance = 100f;

        public bool HasRequiredReferences => playerCamera != null;

        private void Awake()
        {
            if (!HasRequiredReferences)
            {
                Debug.LogError(
                    $"{name} requires an authored gameplay Camera reference. Check the authored " +
                    "Neon Reflex scene.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                ReactionTarget target = hit.collider.GetComponent<ReactionTarget>();
                if (target != null) target.ClickTarget();
            }
        }
    }
}
