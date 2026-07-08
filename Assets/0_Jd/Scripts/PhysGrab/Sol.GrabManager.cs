using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Sol.Grab
{
    public enum GrabMode
    {
        Mouse,
        Crosshair
    }

    public enum GrabInputBinding
    {
        Attack,
        Interact
    }

    public enum HoldDistanceOrigin
    {
        Camera,
        Transform
    }

    /// <summary>
    /// Native arcade physics grabber. Add one to a scene and mark movable objects with GrabbableComponent.
    /// </summary>
    public class GrabManager : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Max raycast distance for grab detection.")]
        public float raycastDistance = 100f;

        [Tooltip("Layer mask for grab raycast.")]
        public LayerMask raycastLayerMask = Physics.DefaultRaycastLayers & ~(1 << 3);

        [Tooltip("Mouse: ray from cursor. Crosshair: ray from screen center.")]
        [SerializeField] private GrabMode grabMode = GrabMode.Crosshair;

        [Tooltip("Camera used for crosshair and mouse raycasts. Falls back to Camera.main.")]
        [SerializeField] private Camera gameplayCamera;

        [Header("Input")]
        [Tooltip("Input action used for grab hold/release.")]
        [SerializeField] private GrabInputBinding grabInput = GrabInputBinding.Attack;

        [Tooltip("Enable grabbing functionality.")]
        public bool isGrabbingEnabled = true;

        [Tooltip("Allow objects to be frozen with middle click.")]
        public bool isLockingEnabled = true;

        [Tooltip("Allow held objects to be thrown with right click.")]
        public bool isThrowingEnabled = true;

        [Tooltip("Allow R to rotate held objects while the key is held.")]
        [FormerlySerializedAs("allowMiddleClickRotationToggle")]
        [SerializeField] private bool allowKeyboardRotation = true;

        [Header("Throw")]
        [Tooltip("Forward speed applied when throwing a held object.")]
        [SerializeField] private float throwSpeed = 8f;

        [Tooltip("Small upward bias added to the throw direction.")]
        [SerializeField] private float throwUpwardBias = 0.08f;

        [Tooltip("Maximum final throw velocity.")]
        [SerializeField] private float maxThrowSpeed = 10f;

        [Tooltip("Extra speed added when throwing from close to the hold origin.")]
        [SerializeField] private float closeThrowBonusSpeed = 6f;

        [Tooltip("Held distance where throw bonus reaches full strength.")]
        [SerializeField] private float fullPowerThrowDistance = 1f;

        [Tooltip("Held distance where throw bonus falls back to base throw speed.")]
        [SerializeField] private float basePowerThrowDistance = 3f;

        [Header("Held Object")]
        [Tooltip("Scroll wheel sensitivity for adjusting hold distance.")]
        public float scrollSensitivity = 0.5f;

        [Tooltip("Master toggle: freeze/unfreeze all grabbable objects.")]
        public bool isFrozen = false;

        [Tooltip("Enable rotation mode. Held objects rotate from mouse movement instead of moving.")]
        public bool rotationMode = false;

        [Tooltip("Rotation sensitivity for mouse movement.")]
        public float rotationSensitivity = 2f;

        [Tooltip("Camera: hold distance is measured from camera. Transform: use Hold Origin if assigned.")]
        [SerializeField] private HoldDistanceOrigin holdDistanceOrigin = HoldDistanceOrigin.Camera;

        [Tooltip("Optional origin used when Hold Distance Origin is Transform.")]
        [SerializeField] private Transform holdOrigin;

        private GrabbableComponent _heldObject;
        private GrabbableComponent _hoveredObject;
        private readonly List<FrozenObjectData> _frozenObjects = new();
        private InputSystem_Actions _actions;
        private InputAction _grabAction;
        private float _currentHoldDistance;
        private Vector2 _scrollInput;
        private bool _previousFrozenState;

        public static GrabManager Instance { get; private set; }

        public GrabMode CurrentGrabMode => grabMode;
        public GrabbableComponent HeldObject => _heldObject;
        public GrabbableComponent HoveredObject => _hoveredObject;
        public int FrozenObjectCount => _frozenObjects.Count;

        private struct FrozenObjectData
        {
            public GrabbableComponent component;
            public Rigidbody rb;
            public bool wasKinematic;
            public bool hadGravity;
            public RigidbodyConstraints constraints;
        }

        public GrabbableComponent GetFrozenObject(int index) => _frozenObjects[index].component;

        public void ForceRelease() => ReleaseHeldObject();

        public void SetGrabMode(GrabMode mode) => grabMode = mode;

        public Ray GetAimRay(Camera cam)
        {
            if (grabMode == GrabMode.Mouse && Mouse.current != null)
                return cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            return cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        public bool IsAimingAtGrabbable()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null)
                return false;

            Ray ray = GetAimRay(activeCamera);
            return Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask, QueryTriggerInteraction.Ignore)
                && ResolveGrabbable(hit) != null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple GrabManagers detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _actions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_actions == null)
                _actions = new InputSystem_Actions();

            HookInput();
            _actions.Player.Enable();
            _actions.UI.Enable();
        }

        private void OnDisable()
        {
            UnhookInput();
            _actions?.Player.Disable();
            _actions?.UI.Disable();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnhookInput();
            _actions?.Dispose();
            _actions = null;
        }

        private void Update()
        {
            if (isFrozen != _previousFrozenState)
            {
                _previousFrozenState = isFrozen;
                if (isFrozen)
                    FreezeAllObjects();
                else
                    UnfreezeAllObjects();
            }

            UpdateHoveredObject();
            PollUtilityInputs();

            if (_heldObject != null && Mathf.Abs(_scrollInput.y) > 0.01f)
            {
                _currentHoldDistance = Mathf.Clamp(
                    _currentHoldDistance + _scrollInput.y * scrollSensitivity,
                    0.5f,
                    raycastDistance);
                _scrollInput = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (_heldObject == null || activeCamera == null) return;
            if (IsFrozen(_heldObject) || isFrozen) return;

            if (rotationMode)
            {
                RotateHeldObject(activeCamera);
            }
            else
            {
                _heldObject.MoveToward(GetHoldPosition(activeCamera));
            }
        }

        private void HookInput()
        {
            UnhookInput();

            _grabAction = grabInput == GrabInputBinding.Interact
                ? _actions.Player.Interact
                : _actions.Player.Attack;

            _grabAction.started += OnGrabInputStarted;
            _grabAction.canceled += OnGrabInputCanceled;
            _actions.UI.ScrollWheel.performed += OnScroll;
            _actions.UI.ScrollWheel.canceled += OnScroll;
        }

        private void OnValidate()
        {
            throwSpeed = Mathf.Max(0f, throwSpeed);
            throwUpwardBias = Mathf.Max(0f, throwUpwardBias);
            maxThrowSpeed = Mathf.Max(0f, maxThrowSpeed);
            closeThrowBonusSpeed = Mathf.Max(0f, closeThrowBonusSpeed);
            fullPowerThrowDistance = Mathf.Max(0f, fullPowerThrowDistance);
            basePowerThrowDistance = Mathf.Max(fullPowerThrowDistance, basePowerThrowDistance);
            raycastDistance = Mathf.Max(0f, raycastDistance);
            scrollSensitivity = Mathf.Max(0f, scrollSensitivity);
            rotationSensitivity = Mathf.Max(0f, rotationSensitivity);
        }

        private void UnhookInput()
        {
            if (_actions == null)
                return;

            if (_grabAction != null)
            {
                _grabAction.started -= OnGrabInputStarted;
                _grabAction.canceled -= OnGrabInputCanceled;
                _grabAction = null;
            }

            _actions.UI.ScrollWheel.performed -= OnScroll;
            _actions.UI.ScrollWheel.canceled -= OnScroll;
        }

        private void OnGrabInputStarted(InputAction.CallbackContext context)
        {
            if (!isGrabbingEnabled || isFrozen || _heldObject != null)
                return;

            TryGrab();
        }

        private void OnGrabInputCanceled(InputAction.CallbackContext context)
        {
            ReleaseHeldObject();
        }

        private void OnScroll(InputAction.CallbackContext context)
        {
            _scrollInput = context.ReadValue<Vector2>();
        }

        private void PollUtilityInputs()
        {
            rotationMode = allowKeyboardRotation &&
                _heldObject != null &&
                Keyboard.current?.rKey.isPressed == true;

            if (Mouse.current == null)
            {
                return;
            }

            if (isThrowingEnabled && Mouse.current.rightButton.wasPressedThisFrame)
                TryThrowHeldObject();

            if (isLockingEnabled && Mouse.current.middleButton.wasPressedThisFrame)
                ToggleLockTarget();
        }

        private void ToggleLockTarget()
        {
            GrabbableComponent target = _heldObject != null ? _heldObject : _hoveredObject;
            if (target == null)
                return;

            if (IsFrozen(target))
                UnfreezeObject(target);
            else
                FreezeObject(target);
        }

        private void UpdateHoveredObject()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null)
            {
                _hoveredObject = null;
                return;
            }

            Ray ray = GetAimRay(activeCamera);
            _hoveredObject = Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask, QueryTriggerInteraction.Ignore)
                ? ResolveGrabbable(hit)
                : null;
        }

        private void TryGrab()
        {
            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null) return;

            Ray ray = GetAimRay(activeCamera);
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, raycastLayerMask, QueryTriggerInteraction.Ignore))
                return;

            GrabbableComponent grabbable = ResolveGrabbable(hit);
            if (grabbable == null || grabbable.IsGrabbed)
                return;

            GrabObject(grabbable);
        }

        public void GrabObject(GrabbableComponent grabbable)
        {
            if (grabbable == null || grabbable.IsGrabbed || _heldObject != null)
                return;

            if (IsFrozen(grabbable))
                UnfreezeObject(grabbable);

            _heldObject = grabbable;
            Camera cam = GetActiveGameplayCamera();
            Vector3 origin = GetHoldDistanceOrigin(cam);
            _currentHoldDistance = Vector3.Distance(origin, grabbable.transform.position);

            _heldObject.OnGrab();
        }

        private void ReleaseHeldObject()
        {
            if (_heldObject == null)
                return;

            if (!IsFrozen(_heldObject))
                _heldObject.OnRelease();

            _heldObject = null;
        }

        private void TryThrowHeldObject()
        {
            if (_heldObject == null || isFrozen || IsFrozen(_heldObject))
            {
                return;
            }

            Camera activeCamera = GetActiveGameplayCamera();
            if (activeCamera == null)
            {
                return;
            }

            GrabbableComponent thrownObject = _heldObject;
            Rigidbody rb = thrownObject.Rigidbody;
            if (rb == null)
            {
                ReleaseHeldObject();
                return;
            }

            Vector3 throwDirection = GetAimRay(activeCamera).direction + Vector3.up * throwUpwardBias;
            if (throwDirection.sqrMagnitude <= 0.001f)
            {
                throwDirection = activeCamera.transform.forward;
            }

            ReleaseHeldObject();

            if (rb.isKinematic)
            {
                return;
            }

            float throwVelocity = CalculateThrowSpeed(thrownObject);
            float effectiveMaxThrowSpeed = CalculateEffectiveMaxThrowSpeed(thrownObject);
            rb.linearVelocity = Vector3.ClampMagnitude(throwDirection.normalized * throwVelocity, effectiveMaxThrowSpeed);
        }

        private float CalculateThrowSpeed(GrabbableComponent thrownObject)
        {
            float throwPowerMultiplier = Mathf.Max(0f, thrownObject.throwPowerMultiplier);
            float closeBonusMultiplier = Mathf.Max(0f, thrownObject.closeThrowBonusMultiplier);
            float closeFactor = CalculateCloseThrowFactor(_currentHoldDistance);
            float speed = throwSpeed + closeFactor * closeThrowBonusSpeed * closeBonusMultiplier;
            return speed * throwPowerMultiplier;
        }

        private float CalculateEffectiveMaxThrowSpeed(GrabbableComponent thrownObject)
        {
            float throwPowerMultiplier = Mathf.Max(0f, thrownObject.throwPowerMultiplier);
            float closeBonusMultiplier = Mathf.Max(0f, thrownObject.closeThrowBonusMultiplier);
            float closeRangeSpeed = throwSpeed + closeThrowBonusSpeed * closeBonusMultiplier;
            return Mathf.Max(maxThrowSpeed, closeRangeSpeed) * throwPowerMultiplier;
        }

        private float CalculateCloseThrowFactor(float holdDistance)
        {
            if (basePowerThrowDistance <= fullPowerThrowDistance)
                return holdDistance <= fullPowerThrowDistance ? 1f : 0f;

            return Mathf.InverseLerp(basePowerThrowDistance, fullPowerThrowDistance, holdDistance);
        }

        private void RotateHeldObject(Camera activeCamera)
        {
            Vector2 look = _actions.Player.Look.ReadValue<Vector2>();
            if (look.sqrMagnitude <= 0.01f)
                return;

            Quaternion yawRotation = Quaternion.AngleAxis(look.x * rotationSensitivity, Vector3.up);
            Quaternion pitchRotation = Quaternion.AngleAxis(-look.y * rotationSensitivity, activeCamera.transform.right);
            _heldObject.transform.rotation = yawRotation * pitchRotation * _heldObject.transform.rotation;
        }

        private Vector3 GetHoldPosition(Camera activeCamera)
        {
            Ray ray = GetAimRay(activeCamera);
            if (holdDistanceOrigin == HoldDistanceOrigin.Transform && holdOrigin != null)
                return holdOrigin.position + ray.direction * _currentHoldDistance;

            return ray.GetPoint(_currentHoldDistance);
        }

        private Vector3 GetHoldDistanceOrigin(Camera activeCamera)
        {
            if (holdDistanceOrigin == HoldDistanceOrigin.Transform && holdOrigin != null)
                return holdOrigin.position;

            return activeCamera != null ? activeCamera.transform.position : Vector3.zero;
        }

        private Camera GetActiveGameplayCamera()
        {
            return gameplayCamera != null ? gameplayCamera : Camera.main;
        }

        private GrabbableComponent ResolveGrabbable(RaycastHit hit)
        {
            return hit.collider.GetComponent<GrabbableComponent>()
                ?? hit.collider.GetComponentInParent<GrabbableComponent>();
        }

        private bool IsFrozen(GrabbableComponent obj)
        {
            for (int i = 0; i < _frozenObjects.Count; i++)
            {
                if (_frozenObjects[i].component == obj)
                    return true;
            }

            return false;
        }

        private void FreezeObject(GrabbableComponent obj)
        {
            if (obj == null || IsFrozen(obj))
                return;

            Rigidbody rb = obj.Rigidbody;
            if (rb == null)
                return;

            if (obj == _heldObject)
                ReleaseHeldObject();

            // Save physics settings so unlocking restores authored behavior.
            _frozenObjects.Add(new FrozenObjectData
            {
                component = obj,
                rb = rb,
                wasKinematic = rb.isKinematic,
                hadGravity = rb.useGravity,
                constraints = rb.constraints
            });

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        private void UnfreezeObject(GrabbableComponent obj)
        {
            int index = -1;
            for (int i = 0; i < _frozenObjects.Count; i++)
            {
                if (_frozenObjects[i].component == obj)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return;

            FrozenObjectData frozenData = _frozenObjects[index];
            if (frozenData.rb != null)
            {
                frozenData.rb.isKinematic = frozenData.wasKinematic;
                frozenData.rb.useGravity = frozenData.hadGravity;
                frozenData.rb.constraints = frozenData.constraints;
            }

            _frozenObjects.RemoveAt(index);
        }

        private void FreezeAllObjects()
        {
            GrabbableComponent[] allGrabbables = FindObjectsByType<GrabbableComponent>(FindObjectsSortMode.None);
            foreach (GrabbableComponent grabbable in allGrabbables)
                FreezeObject(grabbable);
        }

        private void UnfreezeAllObjects()
        {
            for (int i = 0; i < _frozenObjects.Count; i++)
            {
                FrozenObjectData frozenData = _frozenObjects[i];
                if (frozenData.rb != null)
                {
                    frozenData.rb.isKinematic = frozenData.wasKinematic;
                    frozenData.rb.useGravity = frozenData.hadGravity;
                    frozenData.rb.constraints = frozenData.constraints;
                }
            }

            _frozenObjects.Clear();
        }
    }
}
