using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
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
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem (Menus)");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
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
            colors.pressedColor = new Color(0.55f, 0.45f, 0.15f, 1f);
            colors.disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.6f);
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = fontSize * 2.2f;

            labelText = CreateText(buttonObject.transform, "Label", label, fontSize, TextColor);
            Stretch(labelText.rectTransform);
            return button;
        }

        public static Button CreateButton(Transform parent, string label, int fontSize, UnityAction onClick)
        {
            return CreateButton(parent, label, fontSize, onClick, out _);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
