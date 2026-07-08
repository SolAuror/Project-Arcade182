using Sol.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UIOutline = UnityEngine.UI.Outline;

namespace Sol.EditorTools
{
    /// <summary>
    /// Bakes the Labyrinth Crawler runtime HUD into an authored uGUI prefab
    /// (Assets/0_Jd/Prefabs/UI/LabyrinthCrawlerHud.prefab) and swaps the scene
    /// over to it. The prefab is only built when missing — hand modifications
    /// survive re-runs; delete the prefab to regenerate from scratch.
    /// </summary>
    public static class LabyrinthHudBuilder
    {
        private const string UiFolder = "Assets/0_Jd/Prefabs/UI";
        private const string HudPrefabPath = UiFolder + "/LabyrinthCrawlerHud.prefab";
        private const string ScenePath = "Assets/0_Jd/Scenes/Sc_LabyrinthCrawler.unity";

        // Dungeon theme
        private static readonly Color Gold = new Color32(0xC9, 0xA2, 0x27, 0xFF);
        private static readonly Color Parchment = new Color32(0xEF, 0xE3, 0xC2, 0xFF);
        private static readonly Color DimText = new Color32(0xB0, 0xA0, 0x80, 0xFF);
        private static readonly Color PanelBg = new Color32(0x14, 0x0D, 0x08, 0xE8);
        private static readonly Color SlotBg = new Color32(0x1A, 0x11, 0x0A, 0xE8);
        private static readonly Color CardBg = new Color32(0x1C, 0x13, 0x0B, 0xF5);
        private static readonly Color HpFill = new Color32(0xC3, 0x3B, 0x3B, 0xFF);
        private static readonly Color HpBg = new Color32(0x33, 0x12, 0x12, 0xD8);
        private static readonly Color MpFill = new Color32(0x3B, 0x6B, 0xC3, 0xFF);
        private static readonly Color MpBg = new Color32(0x10, 0x1A, 0x33, 0xD8);
        private static readonly Color DwellGreen = new Color32(0x2F, 0xBF, 0x71, 0xFF);
        private static readonly Color DarkInk = new Color32(0x1A, 0x11, 0x0A, 0xFF);

        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Sprite PanelSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        [MenuItem("Sol/Setup/Labyrinth Crawler Hud")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder))
            {
                AssetDatabase.CreateFolder("Assets/0_Jd/Prefabs", "UI");
            }

            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (hudPrefab == null)
            {
                hudPrefab = BuildHudPrefab();
            }
            else
            {
                Debug.Log("LabyrinthCrawlerHud prefab already exists; keeping authored version.");
            }

            WireScene(hudPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("Labyrinth Crawler HUD setup complete.");
        }

        private static GameObject BuildHudPrefab()
        {
            GameObject root = new GameObject("LabyrinthCrawlerHud", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            LabyrinthHud hud = root.AddComponent<LabyrinthHud>();

            // ---- Run panel (top-left) ----
            RectTransform runPanel = Panel(root.transform, "RunPanel", PanelBg);
            Place(runPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(380f, 158f));
            Text title = Row(runPanel, "TitleText", -12f, 22f, "— LABYRINTH CRAWLER —", 15, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.gameObject.name = "TitleText";
            Text timerText = Row(runPanel, "TimerText", -34f, 42f, "0:00.0", 32, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text scoreText = Row(runPanel, "ScoreText", -80f, 22f, "Score 0   Stage 1   Maze 3x3", 16, Parchment, TextAnchor.MiddleCenter, FontStyle.Normal);
            Text enemiesText = Row(runPanel, "EnemiesText", -104f, 22f, "Enemies 0   Kills 0", 16, Parchment, TextAnchor.MiddleCenter, FontStyle.Normal);
            Text statusText = Row(runPanel, "StatusText", -128f, 24f, "Reach the exit pad.", 13, DimText, TextAnchor.MiddleCenter, FontStyle.Italic);

            // ---- Vitals (bottom-left) ----
            RectTransform vitals = Container(root.transform, "Vitals");
            Place(vitals, Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(320f, 72f));
            (Image healthFill, Text healthText) = Bar(vitals, "HealthBar", 40f, HpBg, HpFill, "HP 100/100");
            (Image manaFill, Text manaText) = Bar(vitals, "ManaBar", 0f, MpBg, MpFill, "MP 100/100");

            // ---- Spell slots (bottom-center) ----
            RectTransform spellBar = Container(root.transform, "SpellBar");
            Place(spellBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(430f, 86f));
            string[] hints = { "LMB", "RMB", "Q" };
            var slotWidgets = new LabyrinthHud.SpellSlotWidget[3];
            for (int i = 0; i < 3; i++)
            {
                slotWidgets[i] = Slot(spellBar, $"Slot{i}", (i - 1) * 145f, hints[i]);
            }

            // ---- Exit dwell (center-low) ----
            RectTransform dwellGroup = Container(root.transform, "DwellGroup");
            Place(dwellGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(320f, 56f));
            Row(dwellGroup, "DwellLabel", 0f, 20f, "Channeling exit...", 15, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform dwellBarBg = Panel(dwellGroup, "DwellBar", new Color(0f, 0f, 0f, 0.6f));
            Place(dwellBarBg, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(160f, 0f), new Vector2(320f, 26f));
            dwellBarBg.pivot = new Vector2(0.5f, 0f);
            dwellBarBg.anchoredPosition = new Vector2(0f, 0f);
            dwellBarBg.anchorMin = new Vector2(0.5f, 0f);
            dwellBarBg.anchorMax = new Vector2(0.5f, 0f);
            Image dwellFill = Fill(dwellBarBg, DwellGreen);
            dwellGroup.gameObject.SetActive(false);

            // ---- Crosshair ----
            RectTransform crosshair = Container(root.transform, "Crosshair");
            Place(crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            Image dot = crosshair.gameObject.AddComponent<Image>();
            dot.sprite = KnobSprite;
            dot.color = new Color(1f, 1f, 1f, 0.85f);
            dot.raycastTarget = false;

            // ---- Run over banner ----
            RectTransform runOverGroup = Panel(root.transform, "RunOverGroup", PanelBg);
            Place(runOverGroup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(560f, 96f));
            Text runOverText = StretchText(runOverGroup, "RunOverText", "YOU FELL", 24, Parchment, FontStyle.Bold);
            runOverGroup.gameObject.SetActive(false);

            // ---- Upgrade panel ----
            RectTransform upgradePanel = Container(root.transform, "UpgradePanel");
            Stretch(upgradePanel);
            Image dim = upgradePanel.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.66f);
            dim.raycastTarget = true; // block clicks into the world while choosing
            LabyrinthUpgradeScreen screen = upgradePanel.gameObject.AddComponent<LabyrinthUpgradeScreen>();

            RectTransform header = Container(upgradePanel, "HeaderText");
            Place(header, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(1000f, 44f));
            Text headerText = header.gameObject.AddComponent<Text>();
            StyleText(headerText, "STAGE CLEAR — CHOOSE A BOON", 30, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            var cardWidgets = new LabyrinthUpgradeScreen.UpgradeCardWidget[3];
            for (int i = 0; i < 3; i++)
            {
                cardWidgets[i] = Card(upgradePanel, $"Card{i}", (i - 1) * 284f, i + 1);
            }

            upgradePanel.gameObject.SetActive(false);

            WireHud(hud, timerText, scoreText, enemiesText, statusText, healthFill, healthText, manaFill, manaText, slotWidgets, dwellGroup.gameObject, dwellFill, runOverGroup.gameObject, runOverText);
            WireScreen(screen, cardWidgets);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ //

        private static LabyrinthHud.SpellSlotWidget Slot(RectTransform parent, string name, float x, string hint)
        {
            RectTransform slot = Panel(parent, name, SlotBg);
            Place(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(135f, 86f));

            Text nameText = Row(slot, "NameText", -10f, 24f, "-", 15, Parchment, TextAnchor.MiddleCenter, FontStyle.Bold);

            RectTransform level = Container(slot, "LevelText");
            Place(level, Vector2.zero, Vector2.zero, new Vector2(34f, 15f), new Vector2(56f, 18f));
            Text levelText = level.gameObject.AddComponent<Text>();
            StyleText(levelText, "Lv1", 12, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

            RectTransform hintRect = Container(slot, "HintText");
            Place(hintRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 15f), new Vector2(56f, 18f));
            Text hintText = hintRect.gameObject.AddComponent<Text>();
            StyleText(hintText, hint, 12, DimText, TextAnchor.MiddleRight, FontStyle.Normal);

            RectTransform cooldown = Container(slot, "CooldownOverlay");
            Stretch(cooldown);
            Image cooldownOverlay = cooldown.gameObject.AddComponent<Image>();
            cooldownOverlay.sprite = PanelSprite;
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.62f);
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Vertical;
            cooldownOverlay.fillOrigin = (int)Image.OriginVertical.Top;
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.raycastTarget = false;

            RectTransform locked = Container(slot, "LockedOverlay");
            Stretch(locked);
            Image lockedImage = locked.gameObject.AddComponent<Image>();
            lockedImage.sprite = PanelSprite;
            lockedImage.type = Image.Type.Sliced;
            lockedImage.color = new Color(0f, 0f, 0f, 0.78f);
            lockedImage.raycastTarget = false;
            Text lockedText = StretchText(locked, "LockedText", "LOCKED", 14, Gold, FontStyle.Bold);
            lockedText.raycastTarget = false;
            locked.gameObject.SetActive(false);

            return new LabyrinthHud.SpellSlotWidget
            {
                nameText = nameText,
                levelText = levelText,
                cooldownOverlay = cooldownOverlay,
                lockedOverlay = locked.gameObject
            };
        }

        private static LabyrinthUpgradeScreen.UpgradeCardWidget Card(RectTransform parent, string name, float x, int hotkey)
        {
            RectTransform card = Panel(parent, name, CardBg);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -10f), new Vector2(260f, 300f));

            Text titleText = Row(card, "TitleText", -16f, 56f, "Upgrade", 19, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text descriptionText = Row(card, "DescriptionText", -80f, 130f, "Description", 15, Parchment, TextAnchor.UpperCenter, FontStyle.Normal);

            RectTransform buttonRect = Container(card, "TakeButton");
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.offsetMin = new Vector2(40f, 14f);
            buttonRect.offsetMax = new Vector2(-40f, 54f);

            Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.sprite = PanelSprite;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = Gold;
            buttonImage.raycastTarget = true;

            Button takeButton = buttonRect.gameObject.AddComponent<Button>();
            takeButton.targetGraphic = buttonImage;

            Text buttonText = StretchText(buttonRect, "ButtonText", $"Take  [{hotkey}]", 16, DarkInk, FontStyle.Bold);
            buttonText.raycastTarget = false;

            return new LabyrinthUpgradeScreen.UpgradeCardWidget
            {
                root = card.gameObject,
                titleText = titleText,
                descriptionText = descriptionText,
                takeButton = takeButton
            };
        }

        private static (Image fill, Text label) Bar(RectTransform parent, string name, float y, Color background, Color fillColor, string text)
        {
            RectTransform bar = Panel(parent, name, background);
            Place(bar, Vector2.zero, Vector2.zero, new Vector2(160f, y), new Vector2(320f, 30f));
            bar.pivot = new Vector2(0f, 0f);
            bar.anchoredPosition = new Vector2(0f, y);

            Image fill = Fill(bar, fillColor);
            Text label = StretchText(bar, "Label", text, 14, Parchment, FontStyle.Bold);
            return (fill, label);
        }

        private static Image Fill(RectTransform parent, Color color)
        {
            RectTransform rect = Container(parent, "Fill");
            Stretch(rect, 3f);
            Image fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = PanelSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            fill.color = color;
            fill.raycastTarget = false;
            return fill;
        }

        private static RectTransform Panel(Transform parent, string name, Color background)
        {
            RectTransform rect = Container(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.color = background;
            image.raycastTarget = false;

            UIOutline outline = rect.gameObject.AddComponent<UIOutline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return rect;
        }

        private static RectTransform Container(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text Row(RectTransform parent, string name, float topY, float height, string content, int size, Color color, TextAnchor anchor, FontStyle style)
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

        private static Text StretchText(RectTransform parent, string name, string content, int size, Color color, FontStyle style)
        {
            RectTransform rect = Container(parent, name);
            Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            StyleText(text, content, size, color, TextAnchor.MiddleCenter, style);
            return text;
        }

        private static void StyleText(Text text, string content, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            text.font = UiFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        // ------------------------------------------------------------------ //

        private static void WireHud(
            LabyrinthHud hud,
            Text timerText, Text scoreText, Text enemiesText, Text statusText,
            Image healthFill, Text healthText, Image manaFill, Text manaText,
            LabyrinthHud.SpellSlotWidget[] slots,
            GameObject dwellGroup, Image dwellFill,
            GameObject runOverGroup, Text runOverText)
        {
            SerializedObject serialized = new SerializedObject(hud);
            serialized.FindProperty("timerText").objectReferenceValue = timerText;
            serialized.FindProperty("scoreText").objectReferenceValue = scoreText;
            serialized.FindProperty("enemiesText").objectReferenceValue = enemiesText;
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
            serialized.FindProperty("healthFill").objectReferenceValue = healthFill;
            serialized.FindProperty("healthText").objectReferenceValue = healthText;
            serialized.FindProperty("manaFill").objectReferenceValue = manaFill;
            serialized.FindProperty("manaText").objectReferenceValue = manaText;
            serialized.FindProperty("dwellGroup").objectReferenceValue = dwellGroup;
            serialized.FindProperty("dwellFill").objectReferenceValue = dwellFill;
            serialized.FindProperty("runOverGroup").objectReferenceValue = runOverGroup;
            serialized.FindProperty("runOverText").objectReferenceValue = runOverText;

            SerializedProperty slotsProperty = serialized.FindProperty("spellSlots");
            slotsProperty.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                SerializedProperty slot = slotsProperty.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("nameText").objectReferenceValue = slots[i].nameText;
                slot.FindPropertyRelative("levelText").objectReferenceValue = slots[i].levelText;
                slot.FindPropertyRelative("cooldownOverlay").objectReferenceValue = slots[i].cooldownOverlay;
                slot.FindPropertyRelative("lockedOverlay").objectReferenceValue = slots[i].lockedOverlay;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireScreen(LabyrinthUpgradeScreen screen, LabyrinthUpgradeScreen.UpgradeCardWidget[] cards)
        {
            SerializedObject serialized = new SerializedObject(screen);
            SerializedProperty cardsProperty = serialized.FindProperty("cards");
            cardsProperty.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                SerializedProperty card = cardsProperty.GetArrayElementAtIndex(i);
                card.FindPropertyRelative("root").objectReferenceValue = cards[i].root;
                card.FindPropertyRelative("titleText").objectReferenceValue = cards[i].titleText;
                card.FindPropertyRelative("descriptionText").objectReferenceValue = cards[i].descriptionText;
                card.FindPropertyRelative("takeButton").objectReferenceValue = cards[i].takeButton;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireScene(GameObject hudPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            LabyrinthCrawlerGame game = Object.FindFirstObjectByType<LabyrinthCrawlerGame>();
            if (game != null && game.TryGetComponent(out LabyrinthUpgradeScreen oldScreen))
            {
                // The IMGUI-era screen lived on the game object; the prefab owns it now.
                Object.DestroyImmediate(oldScreen, true);
            }

            LabyrinthHud existingHud = Object.FindFirstObjectByType<LabyrinthHud>(FindObjectsInactive.Include);
            if (existingHud == null)
            {
                PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            }

            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
