using System;
using Sol.Grab;
using Sol.Minigames;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sol.Arcade
{
    /// <summary>
    /// The way out of the arcade. Sealed until the player redeems the golden
    /// coin bought from the exit clerk; using it with the coin beats the game
    /// and returns to the main menu. Builds its own doorframe visuals — spawn
    /// it anywhere (the hub drops one in the maze start room).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Golden Exit Door")]
    public class GoldenExitDoor : MonoBehaviour
    {
        [Header("Escape")]
        [Tooltip("Scene loaded after beating the game.")]
        [SerializeField] private string menuSceneName = "Sc_MainMenu";

        [SerializeField, Min(0.5f)] private float escapeDelaySeconds = 2.5f;

        [Header("Interaction")]
        [SerializeField] private float interactDistance = 6f;
        [SerializeField] private LayerMask interactLayerMask = Physics.DefaultRaycastLayers & ~(1 << 3);

        [Header("Look")]
        [SerializeField] private Color sealedColor = new Color(0.12f, 0.1f, 0.16f, 1f);
        [SerializeField] private Color unlockedColor = new Color(1f, 0.8f, 0.2f, 1f);

        private InputSystem_Actions actions;
        private Renderer portalRenderer;
        private PlayerScoreCarrier carrier;
        private float escapeAtTime = -1f;
        private float nextCarrierSearchTime;
        private bool escapeTriggered;

        private void Awake()
        {
            actions = new InputSystem_Actions();
            BuildVisuals();
        }

        private void OnEnable()
        {
            actions ??= new InputSystem_Actions();
            actions.Player.Interact.started += OnInteractStarted;
            actions.Player.Attack.started += OnAttackStarted;
            actions.Player.Enable();
        }

        private void OnDisable()
        {
            actions.Player.Interact.started -= OnInteractStarted;
            actions.Player.Attack.started -= OnAttackStarted;
            actions.Player.Disable();
        }

        private void OnDestroy()
        {
            actions?.Dispose();
            actions = null;
        }

        private void Update()
        {
            UpdatePortalLook();

            if (escapeTriggered && Time.time >= escapeAtTime)
            {
                escapeTriggered = false;
                LoadMenuScene();
            }
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            TryUseDoor();
        }

        private void OnAttackStarted(InputAction.CallbackContext context)
        {
            if (GrabManager.Instance != null && GrabManager.Instance.IsAimingAtGrabbable())
                return;

            TryUseDoor();
        }

        private void TryUseDoor()
        {
            if (escapeTriggered || !IsPlayerAimingAtDoor())
            {
                return;
            }

            PlayerScoreCarrier scoreCarrier = ResolveCarrier();
            Vector3 popupPosition = transform.position + Vector3.up * 2.6f;

            if (scoreCarrier == null || !scoreCarrier.HasGoldenCoin)
            {
                DamagePopup.SpawnText(popupPosition, "SEALED.\nONLY A GOLDEN COIN OPENS THIS DOOR.", new Color(0.7f, 0.6f, 1f), 2.5f);
                return;
            }

            scoreCarrier.RedeemGoldenCoin();
            DamagePopup.SpawnText(popupPosition, "YOU ESCAPED THE ARCADE!", new Color(1f, 0.85f, 0.25f), escapeDelaySeconds);
            escapeTriggered = true;
            escapeAtTime = Time.time + escapeDelaySeconds;
        }

        private void LoadMenuScene()
        {
            if (Application.CanStreamedLevelBeLoaded(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
                return;
            }

            Debug.LogWarning($"{name} cannot load '{menuSceneName}'. Run Sol/Setup/Menus And UI Prefabs and add it to Build Settings.", this);
        }

        private PlayerScoreCarrier ResolveCarrier()
        {
            if (carrier == null && Time.unscaledTime >= nextCarrierSearchTime)
            {
                nextCarrierSearchTime = Time.unscaledTime + 0.5f;
                carrier = PlayerScoreCarrier.FindForPlayer();
            }

            return carrier;
        }

        private void UpdatePortalLook()
        {
            if (portalRenderer == null)
            {
                return;
            }

            PlayerScoreCarrier scoreCarrier = ResolveCarrier();
            bool unlocked = scoreCarrier != null && scoreCarrier.HasGoldenCoin;
            float pulse = (Mathf.Sin(Time.time * (unlocked ? 5f : 1.5f)) + 1f) * 0.5f;
            Color baseColor = unlocked ? unlockedColor : sealedColor;
            portalRenderer.material.color = Color.Lerp(baseColor, Color.white, pulse * (unlocked ? 0.35f : 0.08f));
        }

        // Simple procedural doorframe: two pillars, a lintel, and a glowing portal slab.
        private void BuildVisuals()
        {
            CreateFramePiece("Left Pillar", new Vector3(-0.85f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 0.3f));
            CreateFramePiece("Right Pillar", new Vector3(0.85f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 0.3f));
            CreateFramePiece("Lintel", new Vector3(0f, 2.6f, 0f), new Vector3(2f, 0.3f, 0.3f));

            GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            portal.name = "Portal";
            portal.transform.SetParent(transform, false);
            portal.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            portal.transform.localScale = new Vector3(1.4f, 2.5f, 0.08f);
            portalRenderer = portal.GetComponent<Renderer>();
        }

        private void CreateFramePiece(string pieceName, Vector3 localPosition, Vector3 localScale)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = pieceName;
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;

            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            if (pieceRenderer != null)
            {
                pieceRenderer.material.color = new Color(0.85f, 0.7f, 0.3f, 1f);
            }
        }

        private bool IsPlayerAimingAtDoor()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null)
                return false;

            Ray ray = GrabManager.Instance != null
                ? GrabManager.Instance.GetAimRay(activeCamera)
                : activeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            float distance = GrabManager.Instance != null
                ? Mathf.Max(interactDistance, GrabManager.Instance.raycastDistance)
                : interactDistance;

            RaycastHit[] hits = Physics.RaycastAll(ray, distance, interactLayerMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    return true;
            }

            return false;
        }
    }
}
