using Sol.Grab;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.Outline
{
    /// <summary>
    /// Native arcade outline hover detector. Outlines any OutlineComponent under the selected ray.
    /// </summary>
    public class OutlineManager : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Max raycast distance for hover detection.")]
        public float raycastDistance = 100f;

        [Tooltip("Layers included in outline hover detection.")]
        public LayerMask detectionLayerMask = Physics.DefaultRaycastLayers & ~(1 << 3);

        [Tooltip("Mouse: ray from cursor. Crosshair: ray from screen center.")]
        [SerializeField] private GrabMode rayMode = GrabMode.Crosshair;

        [Tooltip("Camera used for crosshair and mouse raycasts. Falls back to Camera.main.")]
        [SerializeField] private Camera gameplayCamera;

        private OutlineComponent _currentOutlinedObject;

        public static OutlineManager Instance { get; private set; }
        public GrabMode CurrentRayMode => rayMode;
        public OutlineComponent CurrentOutlinedObject => _currentOutlinedObject;

        public void SetRayMode(GrabMode mode) => rayMode = mode;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple OutlineManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            Camera activeCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (activeCamera == null)
            {
                SetOutlinedObject(null);
                return;
            }

            OutlineComponent hoveredComponent = null;
            Ray ray = GetAimRay(activeCamera);

            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, detectionLayerMask, QueryTriggerInteraction.Ignore))
            {
                hoveredComponent = hit.collider.GetComponent<OutlineComponent>()
                    ?? hit.collider.GetComponentInParent<OutlineComponent>();

                if (hoveredComponent != null && hoveredComponent.alwaysVisible)
                    hoveredComponent = null;
            }

            SetOutlinedObject(hoveredComponent);
        }

        /// <summary>
        /// Manually set which object should have an outline. Pass null to clear.
        /// </summary>
        public void SetOutlinedObject(OutlineComponent component)
        {
            if (component == _currentOutlinedObject)
                return;

            if (_currentOutlinedObject != null && !_currentOutlinedObject.alwaysVisible)
                _currentOutlinedObject.HideOutline();

            _currentOutlinedObject = component;

            if (_currentOutlinedObject != null)
                _currentOutlinedObject.ShowOutline();
        }

        private Ray GetAimRay(Camera cam)
        {
            if (rayMode == GrabMode.Mouse && Mouse.current != null)
                return cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
    }
}
