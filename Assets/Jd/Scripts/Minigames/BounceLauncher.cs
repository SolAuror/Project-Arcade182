using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Bounce Launcher")]
    public class BounceLauncher : MonoBehaviour
    {
        [SerializeField] private BounceTargetsGame game;
        [SerializeField] private BounceBall ballPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LineRenderer aimArc;
        [SerializeField] private float launchSpeed = 16f;
        [SerializeField] private float minAimAngle = 20f;
        [SerializeField] private float maxAimAngle = 160f;
        [SerializeField] private int arcSegments = 24;
        [SerializeField] private float arcTimeStep = 0.08f;

        private InputSystem_Actions actions;
        private bool isAiming;
        private int lastLaunchFrame = -1;

        private void Awake()
        {
            actions = new InputSystem_Actions();

            if (firePoint == null)
            {
                firePoint = transform;
            }

            if (aimArc == null)
            {
                aimArc = GetComponentInChildren<LineRenderer>();
            }

            if (aimArc == null)
            {
                GameObject arcObject = new GameObject("Aim Arc");
                arcObject.transform.SetParent(transform, false);
                aimArc = arcObject.AddComponent<LineRenderer>();
                aimArc.useWorldSpace = true;
                aimArc.widthMultiplier = 0.06f;
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    aimArc.material = new Material(shader);
                }
            }
        }

        private void OnEnable()
        {
            if (actions == null)
            {
                actions = new InputSystem_Actions();
            }

            actions.Player.Attack.started += OnAttackStarted;
            actions.Player.Attack.canceled += OnAttackCanceled;
            actions.Player.Enable();
        }

        private void OnDisable()
        {
            if (actions != null)
            {
                actions.Player.Attack.started -= OnAttackStarted;
                actions.Player.Attack.canceled -= OnAttackCanceled;
                actions.Player.Disable();
            }
        }

        private void OnDestroy()
        {
            actions?.Dispose();
            actions = null;
        }

        private void Update()
        {
            DrawAimArc(GetAimDirection());

            if (actions != null && actions.Player.Attack.WasReleasedThisFrame())
            {
                LaunchFromInput();
            }
        }

        private void OnValidate()
        {
            launchSpeed = Mathf.Max(0.1f, launchSpeed);
            arcSegments = Mathf.Max(2, arcSegments);
            arcTimeStep = Mathf.Max(0.01f, arcTimeStep);
            minAimAngle = Mathf.Clamp(minAimAngle, 0f, 180f);
            maxAimAngle = Mathf.Clamp(maxAimAngle, minAimAngle, 180f);
        }

        public void AssignGame(BounceTargetsGame owningGame)
        {
            game = owningGame;

            if (ballPrefab == null && game != null)
            {
                ballPrefab = game.BallPrefab;
            }
        }

        private void OnAttackStarted(InputAction.CallbackContext context)
        {
            isAiming = true;
        }

        private void OnAttackCanceled(InputAction.CallbackContext context)
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
            Transform source = firePoint != null ? firePoint : transform;
            Vector3 position = source.position;
            position.z = game != null ? game.PhysicsPlaneZ : position.z;
            return position;
        }

        private Vector3 GetAimDirection()
        {
            Vector3 firePosition = GetFirePosition();
            Vector3 targetPosition = firePosition + Vector3.up;
            Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;

            if (cameraToUse != null && Mouse.current != null)
            {
                Ray ray = cameraToUse.ScreenPointToRay(Mouse.current.position.ReadValue());
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
