using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CreateAirFooty3DScene
{
    private const string ScenePath = "Assets/0_Diego/Scenes/AirFooty3D.unity";
    private const string TexturePath = "Assets/0_Diego/Resources/Texture/CosmicPitch.png";
    private const string MaterialFolder = "Assets/0_Diego/Resources/Material/3D";

    [MenuItem("Tools/Create Air Footy 3D Scene")]
    public static void Build()
    {
        EnsureFolder("Assets/0_Diego/Scenes");
        EnsureFolder(MaterialFolder);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateLighting();
        CreateCamera();
        CreatePitch();

        PhysicsMaterial bounceMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialFolder + "/AirFootyBounce.physicMaterial");
        if (bounceMaterial == null)
        {
            bounceMaterial = new PhysicsMaterial("Air Footy Bounce");
            AssetDatabase.CreateAsset(bounceMaterial, MaterialFolder + "/AirFootyBounce.physicMaterial");
        }
        bounceMaterial.dynamicFriction = 0f;
        bounceMaterial.staticFriction = 0f;
        bounceMaterial.bounciness = 0.9f;
        bounceMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        bounceMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;

        CreateWall("Top Wall", new Vector3(0f, 0.5f, 4.25f), new Vector3(17.6f, 1f, 0.8f), bounceMaterial);
        CreateWall("Bottom Wall", new Vector3(0f, 0.5f, -4.25f), new Vector3(17.6f, 1f, 0.8f), bounceMaterial);
        CreateWall("Left Wall Top", new Vector3(-8.25f, 0.5f, 2.8f), new Vector3(0.8f, 1f, 2.9f), bounceMaterial);
        CreateWall("Left Wall Bottom", new Vector3(-8.25f, 0.5f, -2.8f), new Vector3(0.8f, 1f, 2.9f), bounceMaterial);
        CreateWall("Right Wall Top", new Vector3(8.25f, 0.5f, 2.8f), new Vector3(0.8f, 1f, 2.9f), bounceMaterial);
        CreateWall("Right Wall Bottom", new Vector3(8.25f, 0.5f, -2.8f), new Vector3(0.8f, 1f, 2.9f), bounceMaterial);

        // These walls stop the ball escaping after it enters a goal.
        CreateWall("Left Goal Back", new Vector3(-9.25f, 0.5f, 0f), new Vector3(0.5f, 1f, 3.4f), bounceMaterial);
        CreateWall("Right Goal Back", new Vector3(9.25f, 0.5f, 0f), new Vector3(0.5f, 1f, 3.4f), bounceMaterial);
        CreateWall("Left Goal Top", new Vector3(-8.75f, 0.5f, 1.65f), new Vector3(1.4f, 1f, 0.35f), bounceMaterial);
        CreateWall("Left Goal Bottom", new Vector3(-8.75f, 0.5f, -1.65f), new Vector3(1.4f, 1f, 0.35f), bounceMaterial);
        CreateWall("Right Goal Top", new Vector3(8.75f, 0.5f, 1.65f), new Vector3(1.4f, 1f, 0.35f), bounceMaterial);
        CreateWall("Right Goal Bottom", new Vector3(8.75f, 0.5f, -1.65f), new Vector3(1.4f, 1f, 0.35f), bounceMaterial);

        GameObject player = CreatePlayer("Player", new Vector3(-4.5f, 0.35f, 0f), new Color(0.1f, 0.45f, 1f), bounceMaterial);
        player.AddComponent<PlayerMovement3D>();

        GameObject ai = CreatePlayer("AI Goalkeeper", new Vector3(4.5f, 0.35f, 0f), new Color(1f, 0.15f, 0.2f), bounceMaterial);
        AIPlayer3D aiScript = ai.AddComponent<AIPlayer3D>();

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.position = new Vector3(0f, 0.4f, 0f);
        ball.transform.localScale = Vector3.one * 0.7f;
        Material ballMaterial = GetMaterial("Football3D", Color.white);
        ballMaterial.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/0_Diego/Resources/Texture/Football.png");
        ball.GetComponent<Renderer>().sharedMaterial = ballMaterial;
        SphereCollider ballCollider = ball.GetComponent<SphereCollider>();
        ballCollider.material = bounceMaterial;
        Rigidbody ballBody = ball.AddComponent<Rigidbody>();
        ballBody.useGravity = false;
        ballBody.linearDamping = 0f;
        ballBody.angularDamping = 0.05f;
        ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ballBody.interpolation = RigidbodyInterpolation.Interpolate;
        ballBody.solverIterations = 12;
        ballBody.solverVelocityIterations = 12;
        ballBody.maxDepenetrationVelocity = 20f;
        ballBody.constraints = RigidbodyConstraints.FreezePositionY;
        BallController3D ballScript = ball.AddComponent<BallController3D>();

        GameObject managerObject = new GameObject("Game Manager");
        GameManager3D manager = managerObject.AddComponent<GameManager3D>();
        GoalZone3D playerGoal = CreateGoal("AI Goal", new Vector3(8.6f, 0.6f, 0f), GoalZone3D.ScoringSide.Player, manager, 1f);
        GoalZone3D aiGoal = CreateGoal("Player Goal", new Vector3(-8.6f, 0.6f, 0f), GoalZone3D.ScoringSide.AI, manager, -1f);
        ScoreUI scoreUI = CreateUI();

        SetReference(aiScript, "ball", ball.transform);
        SetReference(manager, "ball", ballScript);
        SetReference(manager, "scoreUI", scoreUI);
        SetReference(manager, "playerGoal", playerGoal);
        SetReference(manager, "aiGoal", aiGoal);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Air Footy 3D scene created successfully.");
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.55f);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0f, 10f, -7f);
        cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.02f, 0.08f);
    }

    private static void CreatePitch()
    {
        GameObject pitch = GameObject.CreatePrimitive(PrimitiveType.Plane);
        pitch.name = "Cosmic Pitch";
        // A Unity Plane is 10 by 10 units, so this matches the arena footprint.
        pitch.transform.localScale = new Vector3(1.76f, 1f, 0.9f);

        Material material = GetMaterial("CosmicPitch3D", Color.white);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        material.mainTexture = texture;
        pitch.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static GameObject CreatePlayer(string name, Vector3 position, Color colour, PhysicsMaterial material)
    {
        GameObject player = new GameObject(name);
        player.name = name;
        player.transform.position = position;

        GameObject bodyModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bodyModel.name = "Body Model";
        bodyModel.transform.SetParent(player.transform, false);
        bodyModel.transform.localPosition = Vector3.zero;
        bodyModel.transform.localScale = new Vector3(0.6f, 0.32f, 0.6f);
        Object.DestroyImmediate(bodyModel.GetComponent<Collider>());
        SetColour(bodyModel, colour);

        GameObject headModel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headModel.name = "Head Model";
        headModel.transform.SetParent(player.transform, false);
        headModel.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        headModel.transform.localScale = Vector3.one * 0.42f;
        Object.DestroyImmediate(headModel.GetComponent<Collider>());
        SetColour(headModel, new Color(1f, 0.72f, 0.52f));

        SphereCollider collider = player.AddComponent<SphereCollider>();
        collider.radius = 0.6f;
        collider.material = material;
        Rigidbody body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        return player;
    }

    private static void CreateWall(string name, Vector3 position, Vector3 scale, PhysicsMaterial material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<BoxCollider>().material = material;
        SetColour(wall, new Color(0.08f, 0.1f, 0.22f));
    }

    private static GoalZone3D CreateGoal(string name, Vector3 position, GoalZone3D.ScoringSide side, GameManager3D manager, float direction)
    {
        GameObject goal = new GameObject(name);
        goal.transform.position = position;
        BoxCollider collider = goal.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.2f, 1.2f, 3.2f);
        collider.isTrigger = true;
        GoalZone3D goalZone = goal.AddComponent<GoalZone3D>();
        SetReference(goalZone, "pointGoesTo", side);
        SetReference(goalZone, "gameManager", manager);
        CreateGoalFrame(goal.transform, direction);
        return goalZone;
    }

    private static void CreateGoalFrame(Transform goal, float direction)
    {
        Material frameMaterial = GetMaterial("GoalFrame", Color.white);
        Material netMaterial = GetMaterial("GoalNet", new Color(0.5f, 0.75f, 1f, 0.32f));

        CreateGoalPart("Top Post", goal, new Vector3(direction * 0.35f, 0.65f, 1.35f), new Vector3(0.12f, 1.3f, 0.12f), frameMaterial);
        CreateGoalPart("Bottom Post", goal, new Vector3(direction * 0.35f, 0.65f, -1.35f), new Vector3(0.12f, 1.3f, 0.12f), frameMaterial);
        CreateGoalPart("Crossbar", goal, new Vector3(direction * 0.35f, 1.3f, 0f), new Vector3(0.12f, 0.12f, 2.8f), frameMaterial);
        CreateGoalPart("Net", goal, new Vector3(direction * 0.65f, 0.65f, 0f), new Vector3(0.06f, 1.2f, 2.6f), netMaterial);
    }

    private static void CreateGoalPart(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    private static ScoreUI CreateUI()
    {
        GameObject canvasObject = new GameObject("Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        ScoreUI scoreUI = canvasObject.AddComponent<ScoreUI>();

        GameObject eventObject = new GameObject("EventSystem");
        eventObject.AddComponent<EventSystem>();
        eventObject.AddComponent<InputSystemUIInputModule>();

        TMP_Text playerText = CreateText("Player Score", canvasObject.transform, "Player: 0", new Vector2(-350f, -50f), 38);
        TMP_Text aiText = CreateText("AI Score", canvasObject.transform, "AI: 0", new Vector2(350f, -50f), 38);
        TMP_Text gameOverText = CreateText("Game Over", canvasObject.transform, "", new Vector2(0f, -420f), 52);
        gameOverText.rectTransform.sizeDelta = new Vector2(900f, 180f);
        SetReference(scoreUI, "playerScoreText", playerText);
        SetReference(scoreUI, "aiScoreText", aiText);
        SetReference(scoreUI, "gameOverText", gameOverText);
        CreateMenu(canvasObject.transform);
        return scoreUI;
    }

    private static void CreateMenu(Transform canvas)
    {
        GameObject controller = new GameObject("Main Menu UI");
        controller.transform.SetParent(canvas, false);
        MainMenuUI menu = controller.AddComponent<MainMenuUI>();
        GameObject main = CreatePanel("Main Menu Panel", canvas);
        CreateText("Title", main.transform, "AIR FOOTY 3D", new Vector2(0f, -200f), 76);
        Button start = CreateButton("Start Button", main.transform, "START GAME", new Vector2(0f, -390f));
        Button instructionsButton = CreateButton("Instructions Button", main.transform, "INSTRUCTIONS", new Vector2(0f, -500f));

        GameObject instructions = CreatePanel("Instructions Panel", canvas);
        TMP_Text instructionsText = CreateText("Instructions Text", instructions.transform,
            "HOW TO PLAY\n\nUse W A S D to move.\nStay on your half of the pitch.\nHit the football into the AI goal.\nFirst team to score 5 goals wins!",
            new Vector2(0f, -300f), 40);
        instructionsText.rectTransform.sizeDelta = new Vector2(1000f, 500f);
        Button back = CreateButton("Back Button", instructions.transform, "BACK", new Vector2(0f, -720f));

        GameObject rules = new GameObject("Rule Banner", typeof(RectTransform), typeof(Image));
        rules.transform.SetParent(canvas, false);
        RectTransform rulesRect = rules.GetComponent<RectTransform>();
        rulesRect.anchorMin = new Vector2(0.5f, 0f);
        rulesRect.anchorMax = new Vector2(0.5f, 0f);
        rulesRect.anchoredPosition = new Vector2(0f, 60f);
        rulesRect.sizeDelta = new Vector2(800f, 75f);
        rules.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.12f, 0.9f);
        TMP_Text ruleText = CreateText("Rule Text", rules.transform, "FIRST TEAM TO SCORE 5 GOALS WINS!", Vector2.zero, 30);
        ruleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        ruleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        SetReference(menu, "mainMenuPanel", main);
        SetReference(menu, "instructionsPanel", instructions);
        SetReference(menu, "rulesPanel", rules);
        SetReference(menu, "startButton", start);
        SetReference(menu, "instructionsButton", instructionsButton);
        SetReference(menu, "backButton", back);
        main.SetActive(true);
        instructions.SetActive(false);
        rules.SetActive(false);
    }

    private static GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.09f, 1f);
        return panel;
    }

    private static Button CreateButton(string name, Transform parent, string text, Vector2 position)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(420f, 85f);
        buttonObject.GetComponent<Image>().color = new Color(0.45f, 0.12f, 0.75f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        TMP_Text label = CreateText("Text", buttonObject.transform, text, Vector2.zero, 34);
        label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, Vector2 position, float size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        label.rectTransform.anchoredPosition = position;
        label.rectTransform.sizeDelta = new Vector2(500f, 100f);
        return label;
    }

    private static void SetColour(GameObject target, Color colour)
    {
        string safeName = target.name.Replace(" ", "");
        target.GetComponent<Renderer>().sharedMaterial = GetMaterial(safeName, colour);
    }

    private static Material GetMaterial(string name, Color colour)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = colour;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetReference(Object target, string name, object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(name);
        if (value is Object objectValue) property.objectReferenceValue = objectValue;
        else if (value is System.Enum enumValue) property.enumValueIndex = System.Convert.ToInt32(enumValue);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(item => item.path == scenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
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
}
