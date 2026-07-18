using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.Minigames
{
    /// <summary>
    /// Thin player-only glue: reads the LabyrinthCrawler action map and routes
    /// Attack/Cast/Pulse to <see cref="SpellCaster"/> slots 0/1/2. Spells leave
    /// the player's hand <c>CastingSource</c> transforms but converge on the
    /// camera crosshair, so hand-fired shots still land where the player aims.
    /// All casting rules live in the shared SpellCaster. Disables itself in
    /// scenes without a crawler game.
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

        [Header("Muzzle")]
        [Tooltip("Hand transforms spells launch from. Left empty, they are auto-found by name under the player at runtime (the rig adds combat components on the fly, so name lookup is the reliable path).")]
        [SerializeField] private List<Transform> castingSources = new List<Transform>();

        [Tooltip("Name of the hand child objects used as spell origins when the list above is empty.")]
        [SerializeField] private string castingSourceName = "CastingSource";

        [Tooltip("How far ahead the crosshair looks to converge hand-fired spells onto the aim point.")]
        [SerializeField, Min(1f)] private float aimConvergenceDistance = 100f;

        // Active hand transforms resolved from the wired list or by name lookup.
        private readonly List<Transform> resolvedSources = new List<Transform>();

        private readonly bool[] heldSlots = new bool[3];
        private readonly bool[] suppressManaFailUntilReleased = new bool[3];
        private SpellCaster caster;
        private Mana mana;
        private LabyrinthCrawlerGame game;
        private InputSystem_Actions inputActions;
        private InputActionMap crawlerMap;
        private InputAction attackAction;
        private InputAction castAction;
        private InputAction pulseAction;

        private void Awake()
        {
            caster = GetComponent<SpellCaster>();
            mana = GetComponent<Mana>();
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
                attackAction.canceled += OnAttackCanceled;
            }

            if (castAction != null)
            {
                castAction.started += OnCastStarted;
                castAction.canceled += OnCastCanceled;
            }

            if (pulseAction != null)
            {
                pulseAction.started += OnPulseStarted;
                pulseAction.canceled += OnPulseCanceled;
            }

            crawlerMap?.Enable();
        }

        private void OnDisable()
        {
            if (attackAction != null)
            {
                attackAction.started -= OnAttackStarted;
                attackAction.canceled -= OnAttackCanceled;
            }

            if (castAction != null)
            {
                castAction.started -= OnCastStarted;
                castAction.canceled -= OnCastCanceled;
            }

            if (pulseAction != null)
            {
                pulseAction.started -= OnPulseStarted;
                pulseAction.canceled -= OnPulseCanceled;
            }

            crawlerMap?.Disable();
            attackAction = null;
            castAction = null;
            pulseAction = null;
            crawlerMap = null;

            for (int i = 0; i < heldSlots.Length; i++)
            {
                heldSlots[i] = false;
                suppressManaFailUntilReleased[i] = false;
            }
        }

        private void OnDestroy()
        {
            inputActions?.Dispose();
            inputActions = null;
        }

        private void Update()
        {
            // Sustained spells (ContinuousWhileHeld) re-cast while the input is
            // held; the slot cooldown sets the tick rate.
            for (int i = 0; i < heldSlots.Length; i++)
            {
                if (heldSlots[i])
                {
                    TryCastSlot(i);
                }
            }

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

        private void OnAttackStarted(InputAction.CallbackContext context) => BeginCast(0);
        private void OnCastStarted(InputAction.CallbackContext context) => BeginCast(1);
        private void OnPulseStarted(InputAction.CallbackContext context) => BeginCast(2);
        private void OnAttackCanceled(InputAction.CallbackContext context) => EndCast(0);
        private void OnCastCanceled(InputAction.CallbackContext context) => EndCast(1);
        private void OnPulseCanceled(InputAction.CallbackContext context) => EndCast(2);

        private void BeginCast(int slot)
        {
            suppressManaFailUntilReleased[slot] = false;
            TryCastSlot(slot);

            SpellDefinition definition = caster.GetDefinition(slot);
            if (definition != null && definition.ContinuousWhileHeld)
            {
                heldSlots[slot] = true;
            }
        }

        private void EndCast(int slot)
        {
            heldSlots[slot] = false;
            suppressManaFailUntilReleased[slot] = false;
        }

        private void TryCastSlot(int slot)
        {
            mana ??= GetComponent<Mana>();

            if (game != null && !game.CanPlayerAct)
            {
                return;
            }

            Camera aimCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (aimCamera == null)
            {
                return;
            }

            if (suppressManaFailUntilReleased[slot])
            {
                return;
            }

            SpellDefinition definition = caster.GetDefinition(slot);
            // The crosshair ray from the camera decides WHERE the shot lands...
            Ray cameraRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aimPoint = Physics.Raycast(cameraRay, out RaycastHit hit, aimConvergenceDistance, hitMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : cameraRay.GetPoint(aimConvergenceDistance);

            // ...but the shot LEAVES the hand, angled toward that same point so
            // the muzzle offset never throws off the player's aim.
            Transform muzzle = ResolveMuzzle(slot);
            Vector3 origin = muzzle != null ? muzzle.position : cameraRay.origin;
            Vector3 direction = aimPoint - origin;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = cameraRay.direction;
            }

            SpellCastContext castContext = new SpellCastContext
            {
                Caster = transform,
                Faction = Faction.Player,
                AimRay = new Ray(origin, direction.normalized),
                Muzzle = muzzle != null ? muzzle : aimCamera.transform,
                HitMask = hitMask
            };

            float previousManaFailTime = mana != null ? mana.LastFailedSpendTime : float.MinValue;
            bool cast = caster.TryCast(slot, castContext);

            if (!cast &&
                definition != null &&
                definition.ContinuousWhileHeld &&
                mana != null &&
                mana.LastFailedSpendTime > previousManaFailTime)
            {
                suppressManaFailUntilReleased[slot] = true;
            }
        }

        /// <summary>
        /// Picks the hand a given slot fires from. Multiple hands round-robin by
        /// slot (e.g. two hands: slot 0 and 2 → first hand, slot 1 → second).
        /// Returns null when no active hand is found, so callers fall back to the
        /// camera muzzle.
        /// </summary>
        private Transform ResolveMuzzle(int slot)
        {
            RefreshResolvedSources();
            if (resolvedSources.Count == 0)
            {
                return null;
            }

            return resolvedSources[slot % resolvedSources.Count];
        }

        private void RefreshResolvedSources()
        {
            // Drop hands that were destroyed or hidden (e.g. camera-mode swaps).
            for (int i = resolvedSources.Count - 1; i >= 0; i--)
            {
                if (resolvedSources[i] == null || !resolvedSources[i].gameObject.activeInHierarchy)
                {
                    resolvedSources.RemoveAt(i);
                }
            }

            if (resolvedSources.Count > 0)
            {
                return;
            }

            // Explicit wiring wins when present and active...
            if (castingSources.Count > 0)
            {
                foreach (Transform source in castingSources)
                {
                    if (source != null && source.gameObject.activeInHierarchy)
                    {
                        resolvedSources.Add(source);
                    }
                }

                if (resolvedSources.Count > 0)
                {
                    return;
                }
            }

            // ...otherwise find the hand objects by name under the player.
            foreach (Transform candidate in GetComponentsInChildren<Transform>(false))
            {
                if (candidate.name == castingSourceName)
                {
                    resolvedSources.Add(candidate);
                }
            }
        }
    }
}
