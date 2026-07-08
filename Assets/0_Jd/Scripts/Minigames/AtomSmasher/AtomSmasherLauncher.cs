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

        private InputSystem_Actions actions;
        private InputAction launchAction;
        private InputAction aimPointAction;
        private InputActionMap atomSmasherMap;
        private bool isAiming;
        private int lastLaunchFrame = -1;

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
        }

        private void OnEnable()
        {
            if (actions == null)
            {
                actions = new InputSystem_Actions();
            }

            launchAction = actions.AtomSmasher.Launch;
            aimPointAction = actions.AtomSmasher.AimPoint;
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
            DrawAimArc(GetAimDirection());
        }

        private void OnValidate()
        {
            launchSpeed = Mathf.Max(0.1f, launchSpeed);
            arcSegments = Mathf.Max(2, arcSegments);
            arcTimeStep = Mathf.Max(0.01f, arcTimeStep);
            minAimAngle = Mathf.Clamp(minAimAngle, 0f, 180f);
            maxAimAngle = Mathf.Clamp(maxAimAngle, minAimAngle, 180f);
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
            isAiming = true;
        }

        private void OnLaunchCanceled(InputAction.CallbackContext context)
        {
            LaunchFromInput();
        }

        private void LaunchFromInput()
        {
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

        private Vector3 GetAimDirection()
        {
            Vector3 firePosition = GetFirePosition();
            Vector3 targetPosition = firePosition + Vector3.up;
            Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;

            if (cameraToUse != null && aimPointAction != null)
            {
                Ray ray = cameraToUse.ScreenPointToRay(aimPointAction.ReadValue<Vector2>());
                Plane boardPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, game != null ? game.PhysicsPlaneZ : firePosition.z));

                if (boardPlane.Raycast(ray, out float enter))
                {
                    targetPosition = ray.GetPoint(enter);
                }
            }

            Vector3 direction = targetPosition - firePosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector3.up;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, minAimAngle, maxAimAngle);
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;
        }

        private void DrawAimArc(Vector3 direction)
        {
            if (aimArc == null)
            {
                return;
            }

            aimArc.enabled = game == null || game.CanLaunch || isAiming;
            aimArc.positionCount = arcSegments;

            Vector3 start = GetFirePosition();
            Vector3 velocity = direction * launchSpeed;
            Vector3 gravity = Physics.gravity;

            for (int i = 0; i < arcSegments; i++)
            {
                float time = i * arcTimeStep;
                Vector3 point = start + velocity * time + 0.5f * gravity * time * time;
                point.z = start.z;
                aimArc.SetPosition(i, point);
            }
        }
    }
}
