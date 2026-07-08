using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UIOutline = UnityEngine.UI.Outline;

namespace Sol.EditorTools
{
    /// <summary>
    /// Shared primitives for baking minigame HUD prefabs: themed panels, text
    /// rows, and fill bars built from RectTransforms (no layout groups), so the
    /// results stay easy to rearrange by hand afterwards.
    /// </summary>
    public static class HudBuilderKit
    {
        public static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Sprite PanelSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        public static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        public static GameObject CreateCanvasRoot(string name, int sortingOrder = 10)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            return root;
        }

        public static RectTransform Container(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Panel(Transform parent, string name, Color background, Color outlineColor)
        {
            RectTransform rect = Container(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.color = background;
            image.raycastTarget = false;

            UIOutline outline = rect.gameObject.AddComponent<UIOutline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return rect;
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        public static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        /// <summary>Top-anchored full-width text row inside a panel.</summary>
        public static Text Row(RectTransform parent, string name, float topY, float height, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Container(parent, name);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(14f, topY - height);
            rect.offsetMax = new Vector2(-14f, topY);

            Text text = rect.gameObject.AddComponent<Text>();
            StyleText(text, content, size, color, anchor, style);
            return text;
        }

        public static Text StretchText(RectTransform parent, string name, string content, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Container(parent, name);
            Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            StyleText(text, content, size, color, TextAnchor.MiddleCenter, style);
            return text;
        }

        public static void StyleText(Text text, string content, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            text.font = UiFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.raycastTarget = false;
        }

        /// <summary>Horizontal filled bar inside a pre-placed background rect.</summary>
        public static Image Fill(RectTransform parent, Color color, float margin = 3f)
        {
            RectTransform rect = Container(parent, "Fill");
            Stretch(rect, margin);
            Image fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = PanelSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            fill.color = color;
            fill.raycastTarget = false;
            return fill;
        }
    }
}
