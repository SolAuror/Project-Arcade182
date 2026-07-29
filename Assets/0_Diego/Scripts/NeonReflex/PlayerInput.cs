using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonReflex
{
    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float rayDistance = 100f;

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
