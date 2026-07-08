using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.Minigames
{
    /// <summary>
    /// Thin player-only glue: reads the LabyrinthCrawler action map and routes
    /// Attack/Cast/Pulse to <see cref="SpellCaster"/> slots 0/1/2 with a context
    /// aimed from the gameplay camera center. All casting rules live in the
    /// shared SpellCaster. Disables itself in scenes without a crawler game.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpellCaster))]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Player Spell Input")]
    public class PlayerSpellInput : MonoBehaviour
    {
        private const int PlayerLayer = 3;

        [Header("Aim")]
        [Tooltip("Camera used for center-screen aim. Falls back to Camera.main.")]
        [SerializeField] private Camera gameplayCamera;

        [Tooltip("Layers spells may hit. Player layer is excluded by default.")]
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers & ~(1 << PlayerLayer);

        private SpellCaster caster;
        private LabyrinthCrawlerGame game;
        private InputSystem_Actions inputActions;
        private InputActionMap crawlerMap;
        private InputAction attackAction;
        private InputAction castAction;
        private InputAction pulseAction;

        private void Awake()
        {
            caster = GetComponent<SpellCaster>();
        }

        private void Start()
        {
            game = FindFirstObjectByType<LabyrinthCrawlerGame>();
            if (game == null)
            {
                // Shared player prefab in a non-crawler scene: stay dormant.
                enabled = false;
            }
        }

        private void OnEnable()
        {
            inputActions ??= new InputSystem_Actions();

            attackAction = inputActions.FindAction("LabyrinthCrawler/Attack", false);
            castAction = inputActions.FindAction("LabyrinthCrawler/Cast", false);
            pulseAction = inputActions.FindAction("LabyrinthCrawler/Pulse", false);
            crawlerMap = attackAction?.actionMap ?? castAction?.actionMap;

            if (attackAction != null)
            {
                attackAction.started += OnAttackStarted;
            }

            if (castAction != null)
            {
                castAction.started += OnCastStarted;
            }

            if (pulseAction != null)
            {
                pulseAction.started += OnPulseStarted;
            }

            crawlerMap?.Enable();
        }

        private void OnDisable()
        {
            if (attackAction != null)
            {
                attackAction.started -= OnAttackStarted;
            }

            if (castAction != null)
            {
                castAction.started -= OnCastStarted;
            }

            if (pulseAction != null)
            {
                pulseAction.started -= OnPulseStarted;
            }

            crawlerMap?.Disable();
            attackAction = null;
            castAction = null;
            pulseAction = null;
            crawlerMap = null;
        }

        private void OnDestroy()
        {
            inputActions?.Dispose();
            inputActions = null;
        }

        private void Update()
        {
            // Fallback while the generated input wrapper predates the Pulse action.
            if (pulseAction != null)
            {
                return;
            }

            bool pulsePressed =
                (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame);

            if (pulsePressed)
            {
                TryCastSlot(2);
            }
        }

        private void OnAttackStarted(InputAction.CallbackContext context) => TryCastSlot(0);
        private void OnCastStarted(InputAction.CallbackContext context) => TryCastSlot(1);
        private void OnPulseStarted(InputAction.CallbackContext context) => TryCastSlot(2);

        private void TryCastSlot(int slot)
        {
            if (game != null && !game.CanPlayerAct)
            {
                return;
            }

            Camera aimCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (aimCamera == null)
            {
                return;
            }

            SpellCastContext castContext = new SpellCastContext
            {
                Caster = transform,
                Faction = Faction.Player,
                AimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)),
                Muzzle = aimCamera.transform,
                HitMask = hitMask
            };

            caster.TryCast(slot, castContext);
        }
    }
}
