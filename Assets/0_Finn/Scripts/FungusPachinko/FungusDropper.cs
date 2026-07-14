using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Finn.Minigames
{
    /// <summary>
    /// The player-controlled dropper riding a rail across the top of the board — the only
    /// script in the game that reads input. A/D slides it, Space raises DropRequested so
    /// the controller can spawn a ball. Uses the dedicated "FungusPachinko" action map on
    /// the project-wide input asset, resolved by name so nothing here depends on the
    /// generated InputSystem_Actions wrapper.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Dropper")]
    public class FungusDropper : MonoBehaviour
    {
        private const string ActionMapName = "FungusPachinko";

        [SerializeField] private float railHalfWidth = 2.6f;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private Transform ballSpawnPoint;

        /// <summary>Raised when the player presses Drop while input is allowed.</summary>
        public event Action DropRequested;

        /// <summary>Gate flipped by the controller: aiming on, ball-in-flight off.</summary>
        public bool AllowInput { get; set; } = true;

        public Transform BallSpawnPoint => ballSpawnPoint != null ? ballSpawnPoint : transform;

        private InputActionMap actionMap;
        private InputAction moveAction;
        private InputAction dropAction;

        private void OnEnable()
        {
            actionMap = InputSystem.actions != null
                ? InputSystem.actions.FindActionMap(ActionMapName, throwIfNotFound: false)
                : null;
            if (actionMap == null)
            {
                Debug.LogWarning(
                    $"FungusDropper: action map '{ActionMapName}' not found on the project-wide input asset; dropper input disabled.",
                    this);
                return;
            }

            moveAction = actionMap.FindAction("Move");
            dropAction = actionMap.FindAction("Drop");
            if (dropAction != null)
            {
                dropAction.performed += HandleDropPerformed;
            }

            actionMap.Enable();
        }

        private void OnDisable()
        {
            if (dropAction != null)
            {
                dropAction.performed -= HandleDropPerformed;
            }

            actionMap?.Disable();
            actionMap = null;
            moveAction = null;
            dropAction = null;
        }

        private void Update()
        {
            if (moveAction == null || !AllowInput)
            {
                return;
            }

            float input = moveAction.ReadValue<float>();
            if (Mathf.Approximately(input, 0f))
            {
                return;
            }

            Vector3 localPosition = transform.localPosition;
            localPosition.x = Mathf.Clamp(
                localPosition.x + input * moveSpeed * Time.deltaTime,
                -railHalfWidth,
                railHalfWidth);
            transform.localPosition = localPosition;
        }

        private void HandleDropPerformed(InputAction.CallbackContext context)
        {
            if (AllowInput)
            {
                DropRequested?.Invoke();
            }
        }
    }
}
