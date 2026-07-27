using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CreateAirFootyScene
{
    private const string SceneFolder = "Assets/0_Diego/Scenes";
    private const string MaterialFolder = "Assets/0_Diego/Resources/Material";
    private const string TextureFolder = "Assets/0_Diego/Resources/Texture";

    [MenuItem("Tools/Create Air Footy Scene")]
    public static void Build()
    {
        EnsureFolder(SceneFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(TextureFolder);

        PhysicsMaterial2D ballMaterial = CreateMaterial("BallBounce", 0f, 0.9f);
        PhysicsMaterial2D wallMaterial = CreateMaterial("WallBounce", 0f, 0.9f);
        Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite footballSprite = LoadSprite(TextureFolder + "/Football.png", circle);
        Sprite playerSprite = LoadSprite(TextureFolder + "/PlayerMarker.png", circle);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        CreateSpriteObject("Field", Vector2.zero, new Vector2(16f, 8f), new Color(0.12f, 0.48f, 0.2f), square, -2);
        CreateSpriteObject("Centre Line", Vector2.zero, new Vector2(0.08f, 8f), Color.white, square, -1);

        CreateWall("Top Wall", new Vector2(0f, 4.25f), new Vector2(17f, 0.5f), square, wallMaterial);
        CreateWall("Bottom Wall", new Vector2(0f, -4.25f), new Vector2(17f, 0.5f), square, wallMaterial);
        CreateWall("Left Wall Top", new Vector2(-8.25f, 2.85f), new Vector2(0.5f, 2.3f), square, wallMaterial);
        CreateWall("Left Wall Bottom", new Vector2(-8.25f, -2.85f), new Vector2(0.5f, 2.3f), square, wallMaterial);
        CreateWall("Right Wall Top", new Vector2(8.25f, 2.85f), new Vector2(0.5f, 2.3f), square, wallMaterial);
        CreateWall("Right Wall Bottom", new Vector2(8.25f, -2.85f), new Vector2(0.5f, 2.3f), square, wallMaterial);

        GameObject player = CreatePaddle("Player", new Vector2(-4.5f, 0f), new Color(0.2f, 0.55f, 1f), playerSprite);
        player.AddComponent<PlayerMovement>();

        GameObject ai = CreatePaddle("AI Player", new Vector2(4.5f, 0f), new Color(1f, 0.3f, 0.3f), playerSprite);
        AIPlayer aiScript = ai.AddComponent<AIPlayer>();

        GameObject ball = CreateSpriteObject("Ball", Vector2.zero, Vector2.one * 0.65f, Color.white, footballSprite, 1);
        Rigidbody2D ballBody = ball.AddComponent<Rigidbody2D>();
        ballBody.bodyType = RigidbodyType2D.Dynamic;
        ballBody.gravityScale = 0f;
        ballBody.linearDamping = 0f;
        ballBody.angularDamping = 0.05f;
        ballBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        ballBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        CircleCollider2D ballCollider = ball.AddComponent<CircleCollider2D>();
        ballCollider.sharedMaterial = ballMaterial;
        BallController ballScript = ball.AddComponent<BallController>();

        GameObject managerObject = new GameObject("Game Manager");
        GameManager manager = managerObject.AddComponent<GameManager>();

        GoalZone playerGoal = CreateGoal("AI Goal", new Vector2(8.5f, 0f), GoalZone.ScoringSide.Player, manager, square);
        GoalZone aiGoal = CreateGoal("Player Goal", new Vector2(-8.5f, 0f), GoalZone.ScoringSide.AI, manager, square);
        ScoreUI scoreUI = CreateUI();

        SetReference(aiScript, "ball", ball.transform);
        SetReference(manager, "ball", ballScript);
        SetReference(manager, "scoreUI", scoreUI);
        SetReference(manager, "playerGoal", playerGoal);
        SetReference(manager, "aiGoal", aiGoal);

        EditorSceneManager.SaveScene(scene, SceneFolder + "/AirFooty.unity");
        AddSceneToBuildSettings(SceneFolder + "/AirFooty.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Air Footy scene created successfully.");
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 5.2f;
        camera.backgroundColor = new Color(0.04f, 0.08f, 0.04f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static GameObject CreatePaddle(string name, Vector2 position, Color colour, Sprite sprite)
    {
        GameObject paddle = CreateSpriteObject(name, position, Vector2.one * 1.1f, colour, sprite, 1);
        Rigidbody2D body = paddle.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        paddle.AddComponent<CircleCollider2D>();
        return paddle;
    }

    private static void CreateWall(string name, Vector2 position, Vector2 size, Sprite sprite, PhysicsMaterial2D material)
    {
        GameObject wall = CreateSpriteObject(name, position, size, new Color(0.08f, 0.15f, 0.08f), sprite, 0);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.sharedMaterial = material;
    }

    private static GoalZone CreateGoal(string name, Vector2 position, GoalZone.ScoringSide side, GameManager manager, Sprite sprite)
    {
        GameObject goal = CreateSpriteObject(name, position, new Vector2(0.5f, 2.5f), new Color(1f, 0.85f, 0.1f, 0.45f), sprite, 0);
        BoxCollider2D collider = goal.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        GoalZone goalZone = goal.AddComponent<GoalZone>();
        SetReference(goalZone, "pointGoesTo", side);
        SetReference(goalZone, "gameManager", manager);
        return goalZone;
    }

    private static ScoreUI CreateUI()
    {
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        ScoreUI scoreUI = canvasObject.AddComponent<ScoreUI>();

        TMP_Text playerText = CreateText("Player Score", canvasObject.transform, "Player: 0", new Vector2(-250f, -35f), 30, TextAlignmentOptions.Center);
        TMP_Text aiText = CreateText("AI Score", canvasObject.transform, "AI: 0", new Vector2(250f, -35f), 30, TextAlignmentOptions.Center);
        TMP_Text gameOverText = CreateText("Game Over", canvasObject.transform, "", Vector2.zero, 42, TextAlignmentOptions.Center);
        gameOverText.rectTransform.sizeDelta = new Vector2(600f, 180f);

        SetReference(scoreUI, "playerScoreText", playerText);
        SetReference(scoreUI, "aiScoreText", aiText);
        SetReference(scoreUI, "gameOverText", gameOverText);
        CreateMenu(canvasObject.transform);
        return scoreUI;
    }

    private static void CreateMenu(Transform canvas)
    {
        GameObject menuController = new GameObject("Main Menu UI");
        menuController.transform.SetParent(canvas, false);
        MainMenuUI menu = menuController.AddComponent<MainMenuUI>();

        GameObject mainPanel = CreatePanel("Main Menu Panel", canvas, new Color(0.03f, 0.09f, 0.06f, 0.94f));
        TMP_Text title = CreateText("Title", mainPanel.transform, "AIR FOOTY", new Vector2(0f, -120f), 64, TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(700f, 100f);
        Button startButton = CreateButton("Start Button", mainPanel.transform, "START GAME", new Vector2(0f, -260f));
        Button instructionsButton = CreateButton("Instructions Button", mainPanel.transform, "INSTRUCTIONS", new Vector2(0f, -350f));

        GameObject instructionsPanel = CreatePanel("Instructions Panel", canvas, new Color(0.03f, 0.09f, 0.06f, 0.97f));
        TMP_Text instructions = CreateText("Instructions Text", instructionsPanel.transform,
            "HOW TO PLAY\n\nUse W A S D to move.\nStay on your half of the field.\nHit the football into the AI goal.\nFirst team to score 5 goals wins!",
            new Vector2(0f, -210f), 32, TextAlignmentOptions.Center);
        instructions.rectTransform.sizeDelta = new Vector2(760f, 380f);
        Button backButton = CreateButton("Back Button", instructionsPanel.transform, "BACK", new Vector2(0f, -450f));

        GameObject rulesPanel = new GameObject("Rule Banner", typeof(RectTransform), typeof(Image));
        rulesPanel.transform.SetParent(canvas, false);
        RectTransform rulesRect = rulesPanel.GetComponent<RectTransform>();
        rulesRect.anchorMin = new Vector2(0.5f, 0f);
        rulesRect.anchorMax = new Vector2(0.5f, 0f);
        rulesRect.anchoredPosition = new Vector2(0f, 45f);
        rulesRect.sizeDelta = new Vector2(620f, 60f);
        rulesPanel.GetComponent<Image>().color = new Color(0.02f, 0.07f, 0.04f, 0.82f);
        TMP_Text ruleText = CreateText("Rule Text", rulesPanel.transform, "FIRST TEAM TO SCORE 5 GOALS WINS!", Vector2.zero, 25, TextAlignmentOptions.Center);
        ruleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        ruleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        SetReference(menu, "mainMenuPanel", mainPanel);
        SetReference(menu, "instructionsPanel", instructionsPanel);
        SetReference(menu, "rulesPanel", rulesPanel);
        SetReference(menu, "startButton", startButton);
        SetReference(menu, "instructionsButton", instructionsButton);
        SetReference(menu, "backButton", backButton);
    }

    private static GameObject CreatePanel(string name, Transform parent, Color colour)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = colour;
        return panel;
    }

    private static Button CreateButton(string name, Transform parent, string labelText, Vector2 position)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(330f, 68f);
        buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.62f, 0.3f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        TMP_Text label = CreateText("Text", buttonObject.transform, labelText, Vector2.zero, 28, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, Vector2 position, float size, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = alignment;
        label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = new Vector2(300f, 80f);
        return label;
    }

    private static GameObject CreateSpriteObject(string name, Vector2 position, Vector2 size, Color colour, Sprite sprite, int order)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        gameObject.transform.localScale = size;
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = colour;
        renderer.sortingOrder = order;
        return gameObject;
    }

    private static Sprite LoadSprite(string path, Sprite fallback)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return fallback;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return sprite != null ? sprite : fallback;
    }

    private static PhysicsMaterial2D CreateMaterial(string name, float friction, float bounce)
    {
        string path = MaterialFolder + "/" + name + ".physicsMaterial2D";
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (material == null)
        {
            material = new PhysicsMaterial2D(name);
            AssetDatabase.CreateAsset(material, path);
        }
        material.friction = friction;
        material.bounciness = bounce;
        return material;
    }

    private static void SetReference(Object target, string propertyName, object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (value is Object objectValue) property.objectReferenceValue = objectValue;
        else if (value is System.Enum enumValue) property.enumValueIndex = System.Convert.ToInt32(enumValue);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        System.Collections.Generic.List<EditorBuildSettingsScene> scenes =
            new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        if (!scenes.Exists(scene => scene.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
