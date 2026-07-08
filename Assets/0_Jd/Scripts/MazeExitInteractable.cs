using System;
using Sol.Grab;
using Sol.Minigames;
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

        [Header("Minigame")]
        [Tooltip("If assigned, using this exit completes the maze stage instead of loading Destination Scene Name.")]
        [SerializeField] private LabyrinthCrawlerGame labyrinthCrawlerGame;

        [Header("Interaction")]
        [Tooltip("Maximum distance for activating this exit.")]
        [SerializeField] private float interactDistance = 10f;

        [Tooltip("Layers checked by the activation raycast.")]
        [SerializeField] private LayerMask interactLayerMask = Physics.DefaultRaycastLayers & ~(1 << 3);

        [Tooltip("Camera used for interaction raycasts. Falls back to Camera.main.")]
        [SerializeField] private Camera gameplayCamera;

        private InputSystem_Actions _actions;
        private InputAction _interactAction;
        private InputAction _attackAction;
        private InputActionMap _activeActionMap;
        private bool _isLoading;

        public bool ExitEnabled
        {
            get => exitEnabled;
            set => exitEnabled = value;
        }

        public void AssignLabyrinthCrawlerGame(LabyrinthCrawlerGame game)
        {
            labyrinthCrawlerGame = game;
            _isLoading = false;

            if (isActiveAndEnabled)
            {
                UnbindInput();
                BindInput();
            }
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

            if (labyrinthCrawlerGame != null)
            {
                labyrinthCrawlerGame.CompleteEscape();
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

            BindInput();
        }

        private void OnDisable()
        {
            UnbindInput();
        }

        private void BindInput()
        {
            _interactAction = labyrinthCrawlerGame != null
                ? _actions.FindAction("LabyrinthCrawler/Interact", false) ?? _actions.Player.Interact
                : _actions.Player.Interact;
            _attackAction = labyrinthCrawlerGame != null
                ? _actions.FindAction("LabyrinthCrawler/Attack", false) ?? _actions.Player.Attack
                : _actions.Player.Attack;
            _activeActionMap = _interactAction?.actionMap ?? _attackAction?.actionMap;

            if (_interactAction != null)
            {
                _interactAction.started += OnInteractStarted;
            }

            if (_attackAction != null)
            {
                _attackAction.started += OnPrimaryActionStarted;
            }

            _activeActionMap?.Enable();
        }

        private void UnbindInput()
        {
            if (_actions != null)
            {
                if (_interactAction != null)
                {
                    _interactAction.started -= OnInteractStarted;
                }

                if (_attackAction != null)
                {
                    _attackAction.started -= OnPrimaryActionStarted;
                }

                _activeActionMap?.Disable();
            }

            _interactAction = null;
            _attackAction = null;
            _activeActionMap = null;
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
