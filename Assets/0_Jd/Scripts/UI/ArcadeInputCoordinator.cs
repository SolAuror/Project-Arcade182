using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sol.UI
{
    /// <summary>
    /// Owns cursor capture, input-device switching, and UGUI focus across
    /// scene, pause, and in-game menus. Gameplay systems configure their own
    /// cursor policy; overlay menus temporarily suspend it.
    ///
    /// Prefab-authored at Assets/0_Jd/Resources/UI/ArcadeInput.prefab together
    /// with the session's one EventSystem, and instantiated once for the whole
    /// session — no input objects are generated at runtime. The prefab has to
    /// stay under a Resources folder for the same reason the pause menu does:
    /// the callers are static bootstraps with no scene presence, so there is
    /// nowhere to serialise a reference to it.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class ArcadeInputCoordinator : MonoBehaviour
    {
        private enum InputContext
        {
            Gameplay,
            Menu
        }

        private readonly struct ContextSnapshot
        {
            public ContextSnapshot(
                InputContext context,
                GameObject menuRoot,
                Selectable preferredSelection)
            {
                Context = context;
                MenuRoot = menuRoot;
                PreferredSelection = preferredSelection;
            }

            public InputContext Context { get; }
            public GameObject MenuRoot { get; }
            public Selectable PreferredSelection { get; }
        }

        private const string InputResourcePath = "UI/ArcadeInput";
        private const float DeviceStickThreshold = 0.35f;
        private const float PointerWakeThresholdSquared = 1f;

        private static ArcadeInputCoordinator instance;

        private readonly Stack<ContextSnapshot> contextStack = new();
        private InputContext context = InputContext.Gameplay;
        private GameObject menuRoot;
        private Selectable preferredSelection;
        private CursorLockMode gameplayLockMode = CursorLockMode.Locked;
        private bool showGameplayPointerForMouse;
        private bool usingController;

        public static bool UsingController =>
            instance != null && instance.usingController;
        public static bool IsMenuActive =>
            instance != null && instance.context == InputContext.Menu;

        public static void EnsureExists()
        {
            if (instance != null)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(InputResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"Arcade input prefab missing at Resources/{InputResourcePath}. " +
                    "It must live under a Resources folder to be loadable here; " +
                    "the authored copy is Assets/0_Jd/Resources/UI/ArcadeInput.prefab.");
                return;
            }

            GameObject coordinatorObject = Instantiate(prefab);
            coordinatorObject.name = "Arcade Input";
            instance = coordinatorObject.GetComponent<ArcadeInputCoordinator>();
            DontDestroyOnLoad(coordinatorObject);
        }

        public static void ResetForScene(
            CursorLockMode lockMode,
            bool showPointerForMouse)
        {
            EnsureExists();
            if (instance == null)
            {
                return;
            }

            instance.contextStack.Clear();
            instance.menuRoot = null;
            instance.preferredSelection = null;
            instance.gameplayLockMode = lockMode;
            instance.showGameplayPointerForMouse = showPointerForMouse;
            instance.context = InputContext.Gameplay;
            instance.ApplyCursorState();
        }

        public static void ConfigureGameplayCursor(
            CursorLockMode lockMode,
            bool showPointerForMouse)
        {
            EnsureExists();
            if (instance == null)
            {
                return;
            }

            instance.gameplayLockMode = lockMode;
            instance.showGameplayPointerForMouse = showPointerForMouse;
            instance.ApplyCursorState();
        }

        public static void EnterGameplay(
            CursorLockMode lockMode,
            bool showPointerForMouse)
        {
            EnsureExists();
            if (instance == null)
            {
                return;
            }

            instance.contextStack.Clear();
            instance.menuRoot = null;
            instance.preferredSelection = null;
            instance.gameplayLockMode = lockMode;
            instance.showGameplayPointerForMouse = showPointerForMouse;
            instance.context = InputContext.Gameplay;
            instance.ClearSelection();
            instance.ApplyCursorState();
        }

        public static void ShowMenu(
            GameObject root = null,
            Selectable preferred = null)
        {
            EnsureExists();
            if (instance == null)
            {
                return;
            }

            instance.contextStack.Clear();
            instance.context = InputContext.Menu;
            instance.SetMenuFocusInternal(root, preferred);
            instance.ApplyCursorState();
        }

        public static void SetMenuFocus(
            GameObject root,
            Selectable preferred = null)
        {
            EnsureExists();
            if (instance == null)
            {
                return;
            }

            instance.context = InputContext.Menu;
            instance.SetMenuFocusInternal(root, preferred);
            instance.ApplyCursorState();
        }

        public static void PushMenu(
            GameObject root,
            Selectable preferred = null)
        {
            EnsureExists();
            if (instance == null)
            {
                return;
            }

            instance.contextStack.Push(new ContextSnapshot(
                instance.context,
                instance.menuRoot,
                instance.preferredSelection));
            instance.context = InputContext.Menu;
            instance.SetMenuFocusInternal(root, preferred);
            instance.ApplyCursorState();
        }

        public static void PopContext()
        {
            // A pop is teardown/cleanup and must never recreate the persistent
            // coordinator after Unity has begun destroying scene objects.
            if (instance == null)
            {
                return;
            }

            if (instance.contextStack.Count > 0)
            {
                ContextSnapshot snapshot = instance.contextStack.Pop();
                instance.context = snapshot.Context;
                instance.menuRoot = snapshot.MenuRoot;
                instance.preferredSelection = snapshot.PreferredSelection;
            }
            else
            {
                instance.context = InputContext.Gameplay;
                instance.menuRoot = null;
                instance.preferredSelection = null;
            }

            if (instance.context == InputContext.Gameplay)
            {
                instance.ClearSelection();
            }
            else
            {
                instance.EnsureMenuSelection();
            }
            instance.ApplyCursorState();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            bool controllerUsed = ControllerUsedThisFrame();
            bool keyboardOrMouseUsed = KeyboardOrMouseUsedThisFrame();
            bool nextUsingController = keyboardOrMouseUsed
                ? false
                : controllerUsed || usingController;
            if (nextUsingController != usingController)
            {
                usingController = nextUsingController;
                ApplyCursorState();
            }

            if (context == InputContext.Menu && usingController)
            {
                EnsureMenuSelection();
            }
        }

        private void LateUpdate()
        {
            ApplyCursorState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyCursorState();
            }
        }

        private void SetMenuFocusInternal(
            GameObject root,
            Selectable preferred)
        {
            menuRoot = root;
            preferredSelection = preferred;
            if (usingController)
            {
                EnsureMenuSelection();
            }
        }

        private void EnsureMenuSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            Selectable selectedControl = selected != null
                ? selected.GetComponent<Selectable>()
                : null;
            bool belongsToMenu = selected != null &&
                                 (menuRoot == null ||
                                  selected == menuRoot ||
                                  selected.transform.IsChildOf(menuRoot.transform));
            if (belongsToMenu &&
                selected.activeInHierarchy &&
                selectedControl != null &&
                selectedControl.IsInteractable())
            {
                return;
            }

            if (IsUsable(preferredSelection))
            {
                eventSystem.SetSelectedGameObject(preferredSelection.gameObject);
                return;
            }

            if (menuRoot != null)
            {
                foreach (Selectable candidate in
                         menuRoot.GetComponentsInChildren<Selectable>(false))
                {
                    if (IsUsable(candidate))
                    {
                        eventSystem.SetSelectedGameObject(candidate.gameObject);
                        return;
                    }
                }
            }
        }

        private void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void ApplyCursorState()
        {
            CursorLockMode desiredLockMode = context == InputContext.Menu
                ? CursorLockMode.None
                : gameplayLockMode;
            bool desiredVisible = context == InputContext.Menu
                ? !usingController
                : desiredLockMode != CursorLockMode.Locked &&
                  showGameplayPointerForMouse &&
                  !usingController;

            if (Cursor.lockState != desiredLockMode)
            {
                Cursor.lockState = desiredLockMode;
            }
            if (Cursor.visible != desiredVisible)
            {
                Cursor.visible = desiredVisible;
            }
        }

        private static bool IsUsable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.gameObject.activeInHierarchy &&
                   selectable.IsInteractable();
        }

        private static bool KeyboardOrMouseUsedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            return mouse != null &&
                   (mouse.delta.ReadValue().sqrMagnitude >= PointerWakeThresholdSquared ||
                    mouse.scroll.ReadValue().sqrMagnitude > 0.01f ||
                    mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame ||
                    mouse.middleButton.wasPressedThisFrame);
        }

        private static bool ControllerUsedThisFrame()
        {
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.leftStick.ReadValue().sqrMagnitude >=
                        DeviceStickThreshold * DeviceStickThreshold ||
                    gamepad.rightStick.ReadValue().sqrMagnitude >=
                        DeviceStickThreshold * DeviceStickThreshold ||
                    gamepad.dpad.ReadValue().sqrMagnitude > 0.01f ||
                    gamepad.buttonSouth.wasPressedThisFrame ||
                    gamepad.buttonNorth.wasPressedThisFrame ||
                    gamepad.buttonEast.wasPressedThisFrame ||
                    gamepad.buttonWest.wasPressedThisFrame ||
                    gamepad.startButton.wasPressedThisFrame ||
                    gamepad.selectButton.wasPressedThisFrame ||
                    gamepad.leftShoulder.wasPressedThisFrame ||
                    gamepad.rightShoulder.wasPressedThisFrame ||
                    gamepad.leftTrigger.wasPressedThisFrame ||
                    gamepad.rightTrigger.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
