using Sol.Arcade;
using UnityEngine;
using UnityEngine.InputSystem;
using Player;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Launcher")]
    public class AtomSmasherLauncher : MonoBehaviour
    {
        [SerializeField] private AtomSmasherGame game;
        [SerializeField] private AtomSmasherBall ballPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform playerAnchor;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LineRenderer aimArc;
        [SerializeField] private bool usePlayerAnchor = true;
        [SerializeField] private bool followPlayerAnchor = true;
        [SerializeField] private Vector3 launcherLocalOffsetFromPlayer = new Vector3(0f, 0.05f, 0f);
        [SerializeField] private Vector3 firePointLocalOffsetFromPlayer = new Vector3(0f, 1.15f, 0f);
        [SerializeField] private float launchSpeed = 16f;
        [SerializeField] private float minAimAngle = 20f;
        [SerializeField] private float maxAimAngle = 160f;
        [SerializeField] private int arcSegments = 24;
        [SerializeField] private float arcTimeStep = 0.08f;

        [Header("Stick Aim")]
        [Tooltip("Degrees per second the barrel sweeps at full stick deflection.")]
        [SerializeField, Min(1f)] private float aimDegreesPerSecond = 150f;

        [Tooltip("Stick deflection below this is treated as rest, so the barrel holds its angle.")]
        [SerializeField, Range(0.01f, 0.9f)] private float aimStickDeadzone = 0.2f;

        [Tooltip("Response curve on stick deflection. Above 1 gives fine control near centre and a fast sweep at full tilt.")]
        [SerializeField, Min(1f)] private float aimStickResponseExponent = 1.7f;

        [Tooltip("Pointer travel (pixels) that hands aiming back to the mouse after the stick has been used.")]
        [SerializeField, Min(0f)] private float pointerWakeThreshold = 6f;

        private InputSystem_Actions actions;
        private InputAction launchAction;
        private InputAction aimPointAction;
        private InputAction aimStickAction;
        private InputActionMap atomSmasherMap;
        private bool isAiming;
        private int lastLaunchFrame = -1;
        private Color arcBaseStartColor = Color.white;
        private Color arcBaseEndColor = Color.white;
        private float aimAngle = 90f;
        private bool stickAiming;
        private Vector2 lastPointerPosition;
        private bool hasPointerBaseline;

        /// <summary>True while the barrel is being steered by a stick, so prompts can name the right buttons.</summary>
        public bool IsStickAiming => stickAiming;

        private void Awake()
        {
            actions = new InputSystem_Actions();

            if (firePoint == null)
            {
                firePoint = transform;
            }

            ResolvePlayerAnchor();

            if (aimArc == null)
            {
                aimArc = GetComponentInChildren<LineRenderer>();
            }

            if (aimArc == null)
            {
                Debug.LogWarning($"{name} needs an authored LineRenderer child for the Atom Smasher aim arc.", this);
            }
            else
            {
                arcBaseStartColor = aimArc.startColor;
                arcBaseEndColor = aimArc.endColor;
            }
        }

        private void OnEnable()
        {
            if (actions == null)
            {
                actions = new InputSystem_Actions();
            }

            launchAction = actions.AtomSmasher.Launch;
            aimPointAction = actions.AtomSmasher.AimPoint;
            aimStickAction = actions.AtomSmasher.Aim;
            atomSmasherMap = actions.AtomSmasher.Get();
            launchAction.started += OnLaunchStarted;
            launchAction.canceled += OnLaunchCanceled;
            atomSmasherMap.Enable();
        }

        private void OnDisable()
        {
            if (launchAction != null)
            {
                launchAction.started -= OnLaunchStarted;
                launchAction.canceled -= OnLaunchCanceled;
            }

            atomSmasherMap?.Disable();
            launchAction = null;
            aimPointAction = null;
            aimStickAction = null;
            atomSmasherMap = null;
        }

        private void OnDestroy()
        {
            actions?.Dispose();
            actions = null;
        }

        private void Update()
        {
            SyncToPlayerAnchor();
            UpdateAimAngle();
            DrawAimArc(GetAimDirection());
        }

        private void OnValidate()
        {
            launchSpeed = Mathf.Max(0.1f, launchSpeed);
            arcSegments = Mathf.Max(2, arcSegments);
            arcTimeStep = Mathf.Max(0.01f, arcTimeStep);
            minAimAngle = Mathf.Clamp(minAimAngle, 0f, 180f);
            maxAimAngle = Mathf.Clamp(maxAimAngle, minAimAngle, 180f);
            aimAngle = Mathf.Clamp(aimAngle, minAimAngle, maxAimAngle);
        }

        public void AssignGame(AtomSmasherGame owningGame)
        {
            game = owningGame;
            ResolvePlayerAnchor();

            if (ballPrefab == null && game != null)
            {
                ballPrefab = game.BallPrefab;
            }
        }

        private void OnLaunchStarted(InputAction.CallbackContext context)
        {
            if (PauseMenuController.IsPaused)
            {
                return;
            }

            isAiming = true;
        }

        private void OnLaunchCanceled(InputAction.CallbackContext context)
        {
            LaunchFromInput();
        }

        private void LaunchFromInput()
        {
            // Launch shares its button with UI submit (A / left click), so a
            // press aimed at the pause menu must not also fire a probe.
            if (PauseMenuController.IsPaused)
            {
                isAiming = false;
                return;
            }

            if (Time.frameCount == lastLaunchFrame)
            {
                return;
            }

            lastLaunchFrame = Time.frameCount;
            isAiming = false;

            if (game == null || !game.CanLaunch)
            {
                return;
            }

            if (ballPrefab != null && game.BallPrefab == null)
            {
                Debug.LogWarning($"{name} has a local ball prefab, but the game has no ball prefab assigned.", this);
            }

            game.TryLaunchBall(GetFirePosition(), GetAimDirection(), launchSpeed);
        }

        private Vector3 GetFirePosition()
        {
            if (usePlayerAnchor && playerAnchor != null)
            {
                Vector3 anchoredPosition = playerAnchor.TransformPoint(firePointLocalOffsetFromPlayer);
                anchoredPosition.z = game != null ? game.PhysicsPlaneZ : anchoredPosition.z;
                return anchoredPosition;
            }

            Transform source = firePoint != null ? firePoint : transform;
            Vector3 position = source.position;
            position.z = game != null ? game.PhysicsPlaneZ : position.z;
            return position;
        }

        private void ResolvePlayerAnchor()
        {
            if (!usePlayerAnchor || playerAnchor != null)
            {
                return;
            }

            Controller playerController = FindFirstObjectByType<Controller>();
            if (playerController != null)
            {
                playerAnchor = playerController.transform;
            }
        }

        private void SyncToPlayerAnchor()
        {
            if (!usePlayerAnchor || !followPlayerAnchor || playerAnchor == null)
            {
                return;
            }

            Vector3 anchoredPosition = playerAnchor.TransformPoint(launcherLocalOffsetFromPlayer);
            anchoredPosition.z = game != null ? game.PhysicsPlaneZ : anchoredPosition.z;
            transform.position = anchoredPosition;
            transform.rotation = Quaternion.identity;
        }

        // Mouse and stick both steer the same barrel angle, and whichever the
        // player touched last owns it: the stick sweeps the angle at a rate,
        // the pointer snaps it to wherever it is on the board.
        private void UpdateAimAngle()
        {
            Vector2 stick = aimStickAction != null ? aimStickAction.ReadValue<Vector2>() : Vector2.zero;
            float deflection = Mathf.Abs(stick.x);

            if (deflection > aimStickDeadzone)
            {
                stickAiming = true;
                float scaled = Mathf.Pow(
                    Mathf.InverseLerp(aimStickDeadzone, 1f, deflection),
                    aimStickResponseExponent);

                // Screen-space convention: pushing right walks the barrel toward
                // the low angles on the right of the board.
                aimAngle -= Mathf.Sign(stick.x) * scaled * aimDegreesPerSecond * Time.unscaledDeltaTime;
                aimAngle = Mathf.Clamp(aimAngle, minAimAngle, maxAimAngle);
                return;
            }

            if (PointerMoved() || !stickAiming)
            {
                stickAiming = false;
                if (TryReadPointerAngle(out float pointerAngle))
                {
                    aimAngle = pointerAngle;
                }
            }
        }

        private bool PointerMoved()
        {
            if (aimPointAction == null)
            {
                return false;
            }

            Vector2 pointer = aimPointAction.ReadValue<Vector2>();
            if (!hasPointerBaseline)
            {
                hasPointerBaseline = true;
                lastPointerPosition = pointer;
                return false;
            }

            if ((pointer - lastPointerPosition).sqrMagnitude < pointerWakeThreshold * pointerWakeThreshold)
            {
                return false;
            }

            lastPointerPosition = pointer;
            return true;
        }

        private bool TryReadPointerAngle(out float angle)
        {
            angle = aimAngle;
            Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;

            if (cameraToUse == null || aimPointAction == null)
            {
                return false;
            }

            Vector3 firePosition = GetFirePosition();
            Ray ray = cameraToUse.ScreenPointToRay(aimPointAction.ReadValue<Vector2>());
            Plane boardPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, game != null ? game.PhysicsPlaneZ : firePosition.z));

            if (!boardPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 direction = ray.GetPoint(enter) - firePosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return false;
            }

            angle = Mathf.Clamp(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, minAimAngle, maxAimAngle);
            return true;
        }

        private Vector3 GetAimDirection()
        {
            float radians = Mathf.Clamp(aimAngle, minAimAngle, maxAimAngle) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;
        }

        private void DrawAimArc(Vector3 direction)
        {
            if (aimArc == null)
            {
                return;
            }

            // The arc doubles as the reload gauge: it regrows from the muzzle
            // and brightens back to its authored color as the shot recharges.
            float reloadProgress = game != null ? game.ShotReloadProgress : 1f;
            bool reloading = game != null && game.IsRunning && game.ShotsRemaining > 0 && reloadProgress < 1f;

            aimArc.enabled = game == null || game.CanLaunch || isAiming || reloading;

            int visibleSegments = Mathf.Max(2, Mathf.CeilToInt(arcSegments * reloadProgress));
            aimArc.positionCount = visibleSegments;

            float chargeDim = 0.35f + 0.65f * reloadProgress;
            aimArc.startColor = arcBaseStartColor * chargeDim;
            aimArc.endColor = arcBaseEndColor * chargeDim;

            Vector3 start = GetFirePosition();
            Vector3 velocity = direction * launchSpeed;
            Vector3 gravity = Physics.gravity;

            for (int i = 0; i < visibleSegments; i++)
            {
                float time = i * arcTimeStep;
                Vector3 point = start + velocity * time + 0.5f * gravity * time * time;
                point.z = start.z;
                aimArc.SetPosition(i, point);
            }
        }
    }
}
