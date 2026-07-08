using System;
using System.Collections;
using Sol.Grab;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sol.Arcade
{
    /// <summary>
    /// Loads a configured scene when the player aims at this arcade machine and interacts or clicks it.
    /// Optionally feeds a preview camera into the cabinet screen through a RenderTexture.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Arcade Machine Launcher")]
    public class ArcadeMachineLauncher : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Scene name or path to load. The scene must be in Build Settings.")]
        [SerializeField] private string targetSceneName;

        [Header("Interaction")]
        [Tooltip("Maximum distance for activating this cabinet.")]
        [SerializeField] private float interactDistance = 10f;

        [Tooltip("Layers checked by the activation raycast.")]
        [SerializeField] private LayerMask interactLayerMask = Physics.DefaultRaycastLayers & ~(1 << 3);

        [Tooltip("Camera used for interaction raycasts. Falls back to Camera.main.")]
        [SerializeField] private Camera gameplayCamera;

        [Header("Live Preview")]
        [Tooltip("Renderer that contains the cabinet screen material.")]
        [SerializeField] private Renderer screenRenderer;

        [Tooltip("Material slot on Screen Renderer that should receive the preview texture.")]
        [SerializeField] private int screenMaterialIndex = 0;

        [Tooltip("Camera rendering the hub-local preview diorama.")]
        [SerializeField] private Camera previewCamera;

        [Tooltip("RenderTexture shown on the cabinet screen and assigned to Preview Camera.")]
        [SerializeField] private RenderTexture previewTexture;

        private InputSystem_Actions _actions;
        private Material _runtimeScreenMaterial;
        private Material _originalScreenMaterial;
        private bool _isLoading;

        public void TryLaunch()
        {
            if (_isLoading || !IsPlayerAimingAtThisMachine())
                return;

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning($"{name} has no arcade scene assigned.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogWarning(
                    $"{name} cannot load '{targetSceneName}'. Add the scene to Build Settings and check the assigned name/path.",
                    this);
                return;
            }

            _isLoading = true;
            StartCoroutine(LoadSceneAfterCurrentFrame(targetSceneName));
        }

        private IEnumerator LoadSceneAfterCurrentFrame(string sceneName)
        {
            yield return new WaitForEndOfFrame();
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            ConfigurePreview();
        }

        private void OnEnable()
        {
            if (_actions == null)
                _actions = new InputSystem_Actions();

            _actions.Player.Interact.started += OnInteractStarted;
            _actions.Player.Attack.started += OnPrimaryActionStarted;
            _actions.Player.Enable();
            ConfigurePreview();
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
            RestoreScreenMaterial();
            _actions?.Dispose();
            _actions = null;
        }

        private void OnValidate()
        {
            interactDistance = Mathf.Max(0f, interactDistance);
            screenMaterialIndex = Mathf.Max(0, screenMaterialIndex);
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            TryLaunch();
        }

        private void OnPrimaryActionStarted(InputAction.CallbackContext context)
        {
            if (GrabManager.Instance != null && GrabManager.Instance.IsAimingAtGrabbable())
                return;

            TryLaunch();
        }

        private bool IsPlayerAimingAtThisMachine()
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

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            Transform playerRoot = activeCamera.transform.root;

            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(playerRoot))
                    continue;

                return hitTransform == transform || hitTransform.IsChildOf(transform);
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

        private void ConfigurePreview()
        {
            if (previewCamera != null)
                previewCamera.targetTexture = previewTexture;

            if (screenRenderer == null || previewTexture == null)
                return;

            Material[] materials = screenRenderer.sharedMaterials;
            if (screenMaterialIndex >= materials.Length || materials[screenMaterialIndex] == null)
            {
                Debug.LogWarning($"{name} has an invalid screen material index.", this);
                return;
            }

            // Clone the material so preview assignment does not edit the asset.
            if (_runtimeScreenMaterial == null)
            {
                _originalScreenMaterial = materials[screenMaterialIndex];
                _runtimeScreenMaterial = new Material(_originalScreenMaterial)
                {
                    name = $"{_originalScreenMaterial.name} (Arcade Preview)"
                };
            }

            _runtimeScreenMaterial.mainTexture = previewTexture;
            materials[screenMaterialIndex] = _runtimeScreenMaterial;
            screenRenderer.materials = materials;
        }

        private void RestoreScreenMaterial()
        {
            if (screenRenderer != null && _originalScreenMaterial != null)
            {
                Material[] materials = screenRenderer.sharedMaterials;
                if (screenMaterialIndex < materials.Length)
                {
                    materials[screenMaterialIndex] = _originalScreenMaterial;
                    screenRenderer.materials = materials;
                }
            }

            if (_runtimeScreenMaterial != null)
            {
                Destroy(_runtimeScreenMaterial);
                _runtimeScreenMaterial = null;
            }
        }
    }
}
