using System;
using Sol.Grab;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sol.Arcade
{
    /// <summary>
    /// Simple interactable exit for the maze clerk.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Maze Exit Interactable")]
    public class MazeExitInteractable : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Scene name or path to load when this exit is used.")]
        [SerializeField] private string destinationSceneName;

        [Header("Gate")]
        [Tooltip("If disabled, interacting with this exit will not load the destination scene.")]
        [SerializeField] private bool exitEnabled = true;

        [Header("Interaction")]
        [Tooltip("Maximum distance for activating this exit.")]
        [SerializeField] private float interactDistance = 10f;

        [Tooltip("Layers checked by the activation raycast.")]
        [SerializeField] private LayerMask interactLayerMask = Physics.DefaultRaycastLayers & ~(1 << 3);

        [Tooltip("Camera used for interaction raycasts. Falls back to Camera.main.")]
        [SerializeField] private Camera gameplayCamera;

        private InputSystem_Actions _actions;
        private bool _isLoading;

        public bool ExitEnabled
        {
            get => exitEnabled;
            set => exitEnabled = value;
        }

        public void TryUseExit()
        {
            if (_isLoading || !IsPlayerAimingAtThisExit())
                return;

            if (!exitEnabled)
            {
                Debug.Log($"{name} exit is currently locked.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(destinationSceneName))
            {
                Debug.LogWarning($"{name} has no destination scene assigned.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(destinationSceneName))
            {
                Debug.LogWarning(
                    $"{name} cannot load '{destinationSceneName}'. Add the scene to Build Settings and check the assigned name/path.",
                    this);
                return;
            }

            _isLoading = true;
            SceneManager.LoadScene(destinationSceneName, LoadSceneMode.Single);
        }

        private void Awake()
        {
            _actions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_actions == null)
                _actions = new InputSystem_Actions();

            _actions.Player.Interact.started += OnInteractStarted;
            _actions.Player.Attack.started += OnPrimaryActionStarted;
            _actions.Player.Enable();
        }

        private void OnDisable()
        {
            if (_actions != null)
            {
                _actions.Player.Interact.started -= OnInteractStarted;
                _actions.Player.Attack.started -= OnPrimaryActionStarted;
                _actions.Player.Disable();
            }
        }

        private void OnDestroy()
        {
            _actions?.Dispose();
            _actions = null;
        }

        private void OnValidate()
        {
            interactDistance = Mathf.Max(0f, interactDistance);
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            TryUseExit();
        }

        private void OnPrimaryActionStarted(InputAction.CallbackContext context)
        {
            if (GrabManager.Instance != null && GrabManager.Instance.IsAimingAtGrabbable())
                return;

            TryUseExit();
        }

        private bool IsPlayerAimingAtThisExit()
        {
            Camera activeCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (activeCamera == null)
                return false;

            Ray ray = GetInteractionRay(activeCamera);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                GetInteractionDistance(),
                interactLayerMask,
                QueryTriggerInteraction.Ignore);

            // Check beyond sibling desk colliders, but only accept the clerk hierarchy.
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    return true;
            }

            return false;
        }

        private float GetInteractionDistance()
        {
            return GrabManager.Instance != null
                ? Mathf.Max(interactDistance, GrabManager.Instance.raycastDistance)
                : interactDistance;
        }

        private Ray GetInteractionRay(Camera activeCamera)
        {
            if (GrabManager.Instance != null)
                return GrabManager.Instance.GetAimRay(activeCamera);

            return activeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
    }
}
