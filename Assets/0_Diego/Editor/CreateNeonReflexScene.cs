using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CreateNeonReflexScene
{
    private const string ScenePath = "Assets/0_Diego/Scenes/NeonReflex.unity";
    private const string MaterialFolder = "Assets/0_Diego/Resources/Material/NeonReflex";
    private const string PrefabFolder = "Assets/0_Diego/Prefabs/NeonReflex";

    [MenuItem("Tools/Create Neon Reflex Scene")]
    public static void Build()
    {
        EnsureFolder("Assets/0_Diego/Scenes");
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera camera = CreateCamera();
        CreateArena();
        Transform[] spawnPoints = CreateSpawnPoints();
        NeonReflex.ReactionTarget prefab = CreateTargetPrefab();
        NeonReflex.UIManager ui = CreateUI();

        GameObject managerObject = new GameObject("Neon Reflex Game Manager");
        NeonReflex.GameManager manager = managerObject.AddComponent<NeonReflex.GameManager>();
        NeonReflex.TargetSpawner spawner = managerObject.AddComponent<NeonReflex.TargetSpawner>();

        NeonReflex.PlayerInput input = camera.gameObject.AddComponent<NeonReflex.PlayerInput>();
        SetObject(input, "playerCamera", camera);
        SetObject(manager, "targetSpawner", spawner);
        SetObject(manager, "uiManager", ui);
        SetObject(ui, "gameManager", manager);
        SetObject(spawner, "gameManager", manager);
        SetObject(spawner, "targetPrefab", prefab);
        SetObjectArray(spawner, "spawnPoints", spawnPoints);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Neon Reflex scene created successfully.");
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.008f, 0.035f);
        return camera;
    }

    private static void CreateArena()
    {
        GameObject lightObject = new GameObject("Arena Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 25f;
        light.intensity = 3f;
        light.color = new Color(0.15f, 0.65f, 1f);
        lightObject.transform.position = new Vector3(0f, 3f, -4f);
        RenderSettings.ambientLight = new Color(0.08f, 0.1f, 0.22f);

        CreateCube("Arena Back", new Vector3(0f, 0f, 2f), new Vector3(18f, 10f, 0.3f), new Color(0.01f, 0.025f, 0.08f));
        CreateCube("Top Neon Rail", new Vector3(0f, 4.5f, 1.5f), new Vector3(18f, 0.15f, 0.2f), new Color(0f, 0.9f, 1f));
        CreateCube("Bottom Neon Rail", new Vector3(0f, -4.5f, 1.5f), new Vector3(18f, 0.15f, 0.2f), new Color(0.75f, 0.05f, 1f));
        CreateCube("Left Neon Rail", new Vector3(-8f, 0f, 1.5f), new Vector3(0.15f, 9f, 0.2f), new Color(0f, 1f, 0.45f));
        CreateCube("Right Neon Rail", new Vector3(8f, 0f, 1.5f), new Vector3(0.15f, 9f, 0.2f), new Color(1f, 0.05f, 0.75f));

        for (int index = -3; index <= 3; index++)
        {
            CreateCube("Grid Line " + index, new Vector3(index * 2f, 0f, 1.78f), new Vector3(0.025f, 8f, 0.02f), new Color(0.02f, 0.2f, 0.35f));
        }
    }

    private static Transform[] CreateSpawnPoints()
    {
        Vector3[] positions =
        {
            new Vector3(-4.5f, 2.3f, 0f), new Vector3(0f, 2.5f, 0f), new Vector3(4.5f, 2.3f, 0f),
            new Vector3(-5.5f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(5.5f, 0f, 0f),
            new Vector3(-4.5f, -2.3f, 0f), new Vector3(0f, -2.5f, 0f), new Vector3(4.5f, -2.3f, 0f),
            new Vector3(-6.4f, 3.2f, 0.3f), new Vector3(6.4f, 3.2f, 0.3f),
            new Vector3(-6.4f, -3.2f, 0.3f), new Vector3(6.4f, -3.2f, 0.3f)
        };

        GameObject parent = new GameObject("Target Spawn Points");
        Transform[] points = new Transform[positions.Length];
        for (int index = 0; index < positions.Length; index++)
        {
            GameObject point = new GameObject("Spawn Point " + (index + 1));
            point.transform.SetParent(parent.transform);
            point.transform.position = positions[index];
            points[index] = point.transform;
        }
        return points;
    }

    private static NeonReflex.ReactionTarget CreateTargetPrefab()
    {
        string path = PrefabFolder + "/Energy Sphere.prefab";
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        target.name = "Energy Sphere";
        target.GetComponent<Renderer>().sharedMaterial = GetMaterial("EnergySphere", new Color(0f, 1f, 0.85f));
        target.AddComponent<NeonReflex.ReactionTarget>();
        GameObject prefabObject = PrefabUtility.SaveAsPrefabAsset(target, path);
        Object.DestroyImmediate(target);
        return prefabObject.GetComponent<NeonReflex.ReactionTarget>();
    }

    private static NeonReflex.UIManager CreateUI()
    {
        GameObject canvasObject = new GameObject("Neon Reflex UI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();
        NeonReflex.UIManager ui = canvasObject.AddComponent<NeonReflex.UIManager>();

        GameObject eventObject = new GameObject("EventSystem");
        eventObject.AddComponent<EventSystem>();
        eventObject.AddComponent<InputSystemUIInputModule>();

        TMP_Text score = CreateText("Score", canvasObject.transform, "SCORE: 0", new Vector2(-650f, -60f), 38f);
        TMP_Text level = CreateText("Level", canvasObject.transform, "LEVEL: 1", new Vector2(0f, -60f), 38f);
        TMP_Text lives = CreateText("Lives", canvasObject.transform, "LIVES: 3", new Vector2(650f, -60f), 38f);
        TMP_Text message = CreateText("Message", canvasObject.transform, "", new Vector2(0f, -430f), 72f);
        message.rectTransform.sizeDelta = new Vector2(1200f, 250f);

        GameObject startPanel = CreatePanel("Start Panel", canvasObject.transform, new Color(0.005f, 0.01f, 0.045f, 0.97f));
        TMP_Text title = CreateText("Title", startPanel.transform, "NEON\nREFLEX", new Vector2(0f, -150f), 105f);
        title.color = new Color(0.25f, 0.95f, 1f);
        title.rectTransform.sizeDelta = new Vector2(1100f, 270f);
        TMP_Text tagline = CreateText("Tagline", startPanel.transform, "TEST YOUR SPEED.  TRUST YOUR REFLEXES.", new Vector2(0f, -405f), 32f);
        tagline.color = new Color(0.95f, 0.2f, 0.85f);
        Button startButton = CreateButton("Start Button", startPanel.transform, "START", new Vector2(0f, -570f), new Color(0f, 0.75f, 1f));
        Button instructionsButton = CreateButton("Instructions Button", startPanel.transform, "HOW TO PLAY", new Vector2(0f, -690f), new Color(0.55f, 1f, 0.05f));

        GameObject instructionsPanel = CreatePanel("Instructions Panel", canvasObject.transform, new Color(0.005f, 0.01f, 0.045f, 0.98f));
        TMP_Text instructionsTitle = CreateText("Instructions Title", instructionsPanel.transform, "HOW TO PLAY", new Vector2(0f, -140f), 72f);
        instructionsTitle.color = new Color(0.95f, 0.2f, 0.85f);
        TMP_Text instructions = CreateText("Instructions", instructionsPanel.transform,
            "MOVE YOUR MOUSE\n\nAIM AT THE ENERGY SPHERE\n\nCLICK BEFORE IT DISAPPEARS\n\nAVOID THE RED FAKE TARGETS\n\nYOU HAVE 3 LIVES",
            new Vector2(0f, -440f), 34f);
        instructions.rectTransform.sizeDelta = new Vector2(1100f, 550f);
        Button backButton = CreateButton("Back Button", instructionsPanel.transform, "BACK", new Vector2(0f, -850f), new Color(0.75f, 0.1f, 1f));
        instructionsPanel.SetActive(false);

        SetObject(ui, "scoreText", score);
        SetObject(ui, "levelText", level);
        SetObject(ui, "livesText", lives);
        SetObject(ui, "messageText", message);
        SetObject(ui, "startPanel", startPanel);
        SetObject(ui, "instructionsPanel", instructionsPanel);
        SetObject(ui, "startButton", startButton);
        SetObject(ui, "instructionsButton", instructionsButton);
        SetObject(ui, "backButton", backButton);
        return ui;
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

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Color colour)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(480f, 90f);
        buttonObject.GetComponent<Image>().color = new Color(colour.r * 0.3f, colour.g * 0.3f, colour.b * 0.3f, 0.95f);
        ColorBlock colours = buttonObject.GetComponent<Button>().colors;
        colours.highlightedColor = colour;
        colours.pressedColor = Color.white;
        buttonObject.GetComponent<Button>().colors = colours;
        TMP_Text text = CreateText("Text", buttonObject.transform, label, Vector2.zero, 34f);
        text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.color = Color.white;
        return buttonObject.GetComponent<Button>();
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, Vector2 position, float size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.3f, 0.95f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = new Vector2(500f, 120f);
        return text;
    }

    private static void CreateCube(string name, Vector3 position, Vector3 scale, Color colour)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = GetMaterial(name.Replace(" ", ""), colour);
        Object.DestroyImmediate(cube.GetComponent<Collider>());
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
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", colour * 2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetObject(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray(Object target, string field, Transform[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddSceneToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(item => item.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
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
