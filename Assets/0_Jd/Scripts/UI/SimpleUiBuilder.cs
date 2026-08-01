using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Sol.UI
{
    /// <summary>
    /// Tiny code-built UGUI helpers for the main and pause menus, so no menu
    /// prefabs need authoring. Everything uses the built-in legacy font.
    /// </summary>
    public static class SimpleUiBuilder
    {
        public static readonly Color TextColor = new Color(0.95f, 0.92f, 0.85f, 1f);
        public static readonly Color AccentColor = new Color(1f, 0.8f, 0.2f, 1f);
        public static readonly Color ButtonColor = new Color(0.13f, 0.13f, 0.18f, 0.92f);
        public static readonly Color SelectedTextColor = new Color(0.035f, 0.04f, 0.065f, 1f);

        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject canvasObject = new GameObject(name);
            if (parent != null)
            {
                canvasObject.transform.SetParent(parent, false);
            }

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            ArcadeInputCoordinator.EnsureExists();

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                foreach (EventSystem candidate in Object.FindObjectsByType<EventSystem>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    if (candidate != null && candidate.gameObject.activeInHierarchy)
                    {
                        eventSystem = candidate;
                        break;
                    }
                }
            }

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem (Menus)");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            eventSystem.sendNavigationEvents = true;
            InputSystemUIInputModule inputModule =
                eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }

            foreach (BaseInputModule candidate in
                     eventSystem.GetComponents<BaseInputModule>())
            {
                if (candidate != inputModule)
                {
                    candidate.enabled = false;
                }
            }
        }

        public static Image CreateFullScreenTint(Transform parent, string name, Color color)
        {
            GameObject tintObject = new GameObject(name);
            tintObject.transform.SetParent(parent, false);

            Image image = tintObject.AddComponent<Image>();
            image.color = color;

            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>Centered vertical column that lays out whatever gets added to it.</summary>
        public static RectTransform CreateButtonColumn(Transform parent, string name, float width, float spacing = 14f)
        {
            GameObject columnObject = new GameObject(name);
            columnObject.transform.SetParent(parent, false);

            RectTransform rect = columnObject.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);

            VerticalLayoutGroup layout = columnObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = columnObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        public static Text CreateText(Transform parent, string name, string value, int fontSize, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize * 1.4f;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, int fontSize, UnityAction onClick, out Text labelText)
        {
            GameObject buttonObject = new GameObject($"Button {label}");
            buttonObject.transform.SetParent(parent, false);

            Image background = buttonObject.AddComponent<Image>();
            background.color = ButtonColor;

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.32f, 0.3f, 0.42f, 1f);
            colors.selectedColor = AccentColor;
            colors.pressedColor = new Color(0.55f, 0.45f, 0.15f, 1f);
            colors.disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize * 2.2f;

            labelText = CreateText(buttonObject.transform, "Label", label, fontSize, TextColor);
            Stretch(labelText.rectTransform);
            EnsureButtonSelectionFeedback(buttonObject);
            return button;
        }

        public static Button CreateButton(Transform parent, string label, int fontSize, UnityAction onClick)
        {
            return CreateButton(parent, label, fontSize, onClick, out _);
        }

        public static void EnsureButtonSelectionFeedback(GameObject root)
        {
            Button[] buttons = root != null
                ? root.GetComponentsInChildren<Button>(true)
                : Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(0.38f, 0.35f, 0.48f, 1f);
                colors.selectedColor = AccentColor;
                colors.pressedColor = new Color(1f, 0.55f, 0.08f, 1f);
                colors.fadeDuration = 0.06f;
                button.colors = colors;

                ArcadeButtonSelectionFeedback feedback =
                    button.GetComponent<ArcadeButtonSelectionFeedback>();
                if (feedback == null)
                {
                    feedback = button.gameObject.AddComponent<ArcadeButtonSelectionFeedback>();
                }
                feedback.Configure(button, AccentColor, SelectedTextColor);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    [DisallowMultipleComponent]
    public sealed class ArcadeButtonSelectionFeedback : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler
    {
        private const float SelectedScale = 1.045f;

        private Button button;
        private UnityEngine.UI.Outline selectionOutline;
        private Text[] legacyLabels;
        private Color[] legacyLabelColors;
        private TMP_Text[] tmpLabels;
        private Color[] tmpLabelColors;
        private Color selectedTextColor;
        private Vector3 restingScale;
        private bool configured;

        public void Configure(
            Button configuredButton,
            Color outlineColor,
            Color focusedTextColor)
        {
            button = configuredButton;
            selectedTextColor = focusedTextColor;
            if (!configured)
            {
                configured = true;
                restingScale = transform.localScale;
                CacheLabels();

                Graphic target = button != null ? button.targetGraphic : null;
                if (target != null)
                {
                    selectionOutline =
                        target.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    selectionOutline.effectDistance = new Vector2(5f, -5f);
                    selectionOutline.useGraphicAlpha = false;
                }
            }

            if (selectionOutline != null)
            {
                selectionOutline.effectColor = outlineColor;
            }

            bool currentlySelected =
                EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject;
            SetSelected(currentlySelected);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetSelected(button == null || button.IsInteractable());
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetSelected(false);
        }

        private void OnDisable()
        {
            SetSelected(false);
        }

        private void CacheLabels()
        {
            legacyLabels = GetComponentsInChildren<Text>(true);
            legacyLabelColors = new Color[legacyLabels.Length];
            for (int i = 0; i < legacyLabels.Length; i++)
            {
                legacyLabelColors[i] = legacyLabels[i].color;
            }

            tmpLabels = GetComponentsInChildren<TMP_Text>(true);
            tmpLabelColors = new Color[tmpLabels.Length];
            for (int i = 0; i < tmpLabels.Length; i++)
            {
                tmpLabelColors[i] = tmpLabels[i].color;
            }
        }

        private void SetSelected(bool value)
        {
            transform.localScale = restingScale *
                                   (value ? SelectedScale : 1f);
            if (selectionOutline != null)
            {
                selectionOutline.enabled = value;
            }

            for (int i = 0; i < legacyLabels.Length; i++)
            {
                if (legacyLabels[i] != null)
                {
                    legacyLabels[i].color = value
                        ? selectedTextColor
                        : legacyLabelColors[i];
                }
            }

            for (int i = 0; i < tmpLabels.Length; i++)
            {
                if (tmpLabels[i] != null)
                {
                    tmpLabels[i].color = value
                        ? selectedTextColor
                        : tmpLabelColors[i];
                }
            }
        }
    }

    /// <summary>
    /// Owns cursor capture, input-device switching, and UGUI focus across
    /// scene, pause, and in-game menus. Gameplay systems configure their own
    /// cursor policy; overlay menus temporarily suspend it.
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

            GameObject coordinatorObject = new GameObject("Arcade Input Coordinator");
            instance = coordinatorObject.AddComponent<ArcadeInputCoordinator>();
            DontDestroyOnLoad(coordinatorObject);
        }

        public static void ResetForScene(
            CursorLockMode lockMode,
            bool showPointerForMouse)
        {
            EnsureExists();
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
            instance.gameplayLockMode = lockMode;
            instance.showGameplayPointerForMouse = showPointerForMouse;
            instance.ApplyCursorState();
        }

        public static void EnterGameplay(
            CursorLockMode lockMode,
            bool showPointerForMouse)
        {
            EnsureExists();
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
            instance.context = InputContext.Menu;
            instance.SetMenuFocusInternal(root, preferred);
            instance.ApplyCursorState();
        }

        public static void PushMenu(
            GameObject root,
            Selectable preferred = null)
        {
            EnsureExists();
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
            EnsureExists();
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
            SimpleUiBuilder.EnsureButtonSelectionFeedback(root);
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
                SimpleUiBuilder.EnsureEventSystem();
                eventSystem = EventSystem.current;
            }
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
