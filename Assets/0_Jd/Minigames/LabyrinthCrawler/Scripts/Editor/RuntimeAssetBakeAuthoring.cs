using System;
using System.Collections.Generic;
using System.Reflection;
using Sol.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot, idempotent authoring pass for fixed presentation objects that
/// used to be assembled in Awake. Dynamic maze, projectile and one-shot FX
/// spawning deliberately remains runtime-owned.
/// </summary>
public static class RuntimeAssetBakeAuthoring
{
    private const string AirFootyMaterialDirectory =
        "Assets/0_Diego/Resources/Materials/AirFooty/BakedRuntime";
    private const string AirFootyLineMaterialPath =
        AirFootyMaterialDirectory + "/M_AirFooty_LineFx.mat";
    private const string AirFootyShadowMaterialPath =
        AirFootyMaterialDirectory + "/M_AirFooty_HoverShadow.mat";
    private const string LabyrinthWingMaterialPath =
        "Assets/0_Jd/Minigames/LabyrinthCrawler/Enemies/M_Enemies/M_EnemyFlyerWings.mat";
    private const string AirFootyScenePath =
        "Assets/0_Diego/Scenes/AirFootyFinal.unity";

    private static readonly string[] AirFootyPrefabPaths =
    {
        "Assets/0_Diego/AirFooty_2Player.prefab",
        "Assets/0_Diego/AirFooty_4Player.prefab"
    };

    private static readonly string[] LabyrinthEnemyPrefabPaths =
    {
        "Assets/0_Jd/Minigames/LabyrinthCrawler/Enemies/Enemy_Caster.prefab",
        "Assets/0_Jd/Minigames/LabyrinthCrawler/Enemies/Enemy_Flyer.prefab",
        "Assets/0_Jd/Minigames/LabyrinthCrawler/Enemies/Enemy_Stalker.prefab"
    };

    [MenuItem("Sol/Project Maintenance/Bake Fixed Runtime Assets")]
    public static void BakeAll()
    {
        Material lineMaterial = LoadOrCreateMaterial(
            AirFootyLineMaterialPath,
            "Sprites/Default",
            Color.white);
        Material shadowMaterial = LoadOrCreateMaterial(
            AirFootyShadowMaterialPath,
            "Sprites/Default",
            new Color(0f, 0.06f, 0.12f, 0.28f));
        Material wingMaterial = LoadOrCreateMaterial(
            LabyrinthWingMaterialPath,
            "Universal Render Pipeline/Lit",
            new Color(0.15f, 0.95f, 1f, 1f));
        if (wingMaterial.HasProperty("_EmissionColor"))
        {
            wingMaterial.EnableKeyword("_EMISSION");
            wingMaterial.SetColor(
                "_EmissionColor",
                new Color(0.04f, 0.7f, 0.85f, 1f));
            EditorUtility.SetDirty(wingMaterial);
        }

        foreach (string path in AirFootyPrefabPaths)
        {
            EditPrefab(path, root => BakeAirFooty(root, lineMaterial, shadowMaterial));
        }

        foreach (string path in LabyrinthEnemyPrefabPaths)
        {
            EditPrefab(path, root => BakeLabyrinthEnemy(root, wingMaterial));
        }

        BakeAirFootyScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateBakes();
        Debug.Log("Fixed runtime asset bake completed and validated.");
    }

    public static void BakeAllFromCommandLine()
    {
        BakeAll();
    }

    private static void BakeAirFooty(
        GameObject root,
        Material lineMaterial,
        Material shadowMaterial)
    {
        foreach (AirFootyArenaPresentation presentation in
                 root.GetComponentsInChildren<AirFootyArenaPresentation>(true))
        {
            BakePitchMarkings(presentation, lineMaterial);
        }

        foreach (PlayerActions3D actions in root.GetComponentsInChildren<PlayerActions3D>(true))
        {
            BakePlayerPresentation(actions.transform, lineMaterial);
        }

        foreach (AIPlayer3D ai in root.GetComponentsInChildren<AIPlayer3D>(true))
        {
            BakeAiPresentation(ai.transform, lineMaterial);
        }

        foreach (BallController3D ball in root.GetComponentsInChildren<BallController3D>(true))
        {
            BakeBallPresentation(ball.transform, lineMaterial, shadowMaterial);
        }

        foreach (AirFootyRallyDirector rally in
                 root.GetComponentsInChildren<AirFootyRallyDirector>(true))
        {
            Light glow = GetOrAddComponent<Light>(GetOrCreateChild(rally.transform, "Rally Heat Glow"));
            glow.type = LightType.Point;
            glow.shadows = LightShadows.None;
            glow.enabled = false;
        }
    }

    private static void BakePitchMarkings(
        AirFootyArenaPresentation presentation,
        Material material)
    {
        Transform owner = presentation.transform;
        SerializedObject serialized = new SerializedObject(presentation);
        Color pitchColor = serialized.FindProperty("pitchLineColor").colorValue;
        Color playerColor = serialized.FindProperty("playerColor").colorValue;
        Color aiColor = serialized.FindProperty("aiColor").colorValue;
        Transform markings = GetOrCreateChild(owner, "AirFooty Pitch Markings").transform;
        CreateLine(
            markings,
            "Halfway Line",
            new[] { new Vector3(0f, 0.035f, -5.05f), new Vector3(0f, 0.035f, 5.05f) },
            false,
            0.055f,
            material,
            pitchColor);

        const int segments = 56;
        Vector3[] circle = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            circle[i] = new Vector3(Mathf.Cos(angle) * 1.15f, 0.037f, Mathf.Sin(angle) * 1.15f);
        }
        CreateLine(
            markings,
            "Centre Circle",
            circle,
            true,
            0.05f,
            material,
            pitchColor);
        CreateLine(
            markings,
            "Player Goal Accent",
            new[] { new Vector3(-8.62f, 0.04f, -1.4f), new Vector3(-8.62f, 0.04f, 1.4f) },
            false,
            0.12f,
            material,
            playerColor);
        CreateLine(
            markings,
            "AI Goal Accent",
            new[] { new Vector3(8.62f, 0.04f, -1.4f), new Vector3(8.62f, 0.04f, 1.4f) },
            false,
            0.12f,
            material,
            aiColor);
    }

    private static void BakePlayerPresentation(Transform owner, Material material)
    {
        ConfigureRing(GetOrCreateChild(owner, "Hover Pulse Charge"), 44, 0.06f, material);

        LineRenderer aim = ConfigureLine(GetOrCreateChild(owner, "Dash Aim Indicator"), material);
        aim.useWorldSpace = true;
        aim.loop = false;
        aim.positionCount = 3;
        aim.startWidth = 0.075f;
        aim.endWidth = 0.075f;

        for (int i = 0; i < 3; i++)
        {
            GameObject pip = GetOrCreateChild(owner, $"Ability Charge {i + 1}");
            pip.transform.localPosition = new Vector3((i - 1) * 0.3f, 0.07f, -0.86f);
            ConfigureRing(pip, 14, 0.05f, material);
        }

        LineRenderer stabilizer = ConfigureRing(
            GetOrCreateChild(owner, "Turbo Stabilizers"),
            12,
            0.09f,
            material);
        stabilizer.enabled = false;

        for (int i = 0; i < 2; i++)
        {
            TrailRenderer trail = GetOrAddComponent<TrailRenderer>(
                GetOrCreateChild(owner, $"Turbo Thruster {i + 1}"));
            ConfigureTrail(trail, material, 0.3f, 0.24f);
        }

        Light glow = GetOrAddComponent<Light>(GetOrCreateChild(owner, "Turbo Reactor Glow"));
        glow.transform.localPosition = Vector3.up * 0.38f;
        glow.type = LightType.Point;
        glow.range = 3.3f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    private static void BakeAiPresentation(Transform owner, Material material)
    {
        GameObject telegraphObject = GetOrCreateChild(owner, "AI Shot Telegraph");
        LineRenderer telegraph = ConfigureLine(telegraphObject, material);
        telegraph.useWorldSpace = true;
        telegraph.loop = false;
        telegraph.positionCount = 2;
        telegraph.enabled = false;

        Light glow = GetOrAddComponent<Light>(telegraphObject);
        glow.type = LightType.Point;
        glow.range = 3f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;

        TrailRenderer trail = GetOrAddComponent<TrailRenderer>(owner.gameObject);
        ConfigureTrail(trail, material, 0.26f, 0.46f);
        EnsureAudioSource(owner.gameObject, 0.25f);
    }

    private static void BakeBallPresentation(
        Transform owner,
        Material lineMaterial,
        Material shadowMaterial)
    {
        TrailRenderer trail = GetOrAddComponent<TrailRenderer>(owner.gameObject);
        ConfigureTrail(trail, lineMaterial, 0.2f, 0.18f);
        EnsureAudioSource(owner.gameObject, 0.35f);

        GameObject hover = GetOrCreateChild(owner, "AirFooty Ball Hover");
        GetOrAddComponent<AirFootyHoverVisual>(hover);
        LineRenderer hoverRing = ConfigureRing(
            GetOrCreateChild(hover.transform, "Hover Ring"),
            48,
            0.045f,
            lineMaterial);
        ScaleRingPositions(hoverRing, 0.48f);

        GameObject shadow = GetOrCreateChild(hover.transform, "Hover Shadow");
        if (shadow.GetComponent<MeshFilter>() == null || shadow.GetComponent<MeshRenderer>() == null)
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(primitive);
            GetOrAddComponent<MeshFilter>(shadow).sharedMesh = mesh;
            GetOrAddComponent<MeshRenderer>(shadow);
        }
        Collider collider = shadow.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shadow.transform.localScale = Vector3.one * 0.62f;
        MeshRenderer shadowRenderer = shadow.GetComponent<MeshRenderer>();
        shadowRenderer.sharedMaterial = shadowMaterial;
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
    }

    private static void BakeLabyrinthEnemy(GameObject root, Material wingMaterial)
    {
        EnemyController controller = root.GetComponentInChildren<EnemyController>(true);
        if (controller == null)
        {
            return;
        }

        EnsureAudioSource(controller.gameObject, 1f);
        GetOrAddComponent<HitFlash>(controller.gameObject);

        SerializedObject serialized = new SerializedObject(controller);
        bool isFlyer = serialized.FindProperty("locomotionMode").enumValueIndex ==
                       (int)EnemyController.LocomotionMode.Flying;
        if (!isFlyer)
        {
            return;
        }

        GetOrAddComponent<FlyingEnemyVisual>(controller.gameObject);
        Transform visual = controller.transform.Find("Visual") ?? controller.transform;
        BakeWing(visual, "Left Flight Wing", -1f, wingMaterial);
        BakeWing(visual, "Right Flight Wing", 1f, wingMaterial);
    }

    private static void BakeAirFootyScene()
    {
        Scene scene = SceneManager.GetSceneByPath(AirFootyScenePath);
        bool closeAfterBake = !scene.IsValid() || !scene.isLoaded;
        if (!closeAfterBake && scene.isDirty)
        {
            throw new InvalidOperationException(
                "AirFootyFinal has unsaved scene changes. Save or revert them before running the fixed runtime asset bake.");
        }
        if (closeAfterBake)
        {
            scene = EditorSceneManager.OpenScene(
                AirFootyScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            List<MainMenuUI> menus = new List<MainMenuUI>();
            List<Camera> sceneCameras = new List<Camera>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                menus.AddRange(root.GetComponentsInChildren<MainMenuUI>(true));
                sceneCameras.AddRange(root.GetComponentsInChildren<Camera>(true));
            }

            if (menus.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{AirFootyScenePath} must contain exactly one MainMenuUI; found {menus.Count}.");
            }

            MainMenuUI menu = menus[0];
            SerializedObject serialized = new SerializedObject(menu);
            SerializedProperty displayCameraProperty =
                serialized.FindProperty("displayCamera");
            Camera keptDisplayCamera =
                displayCameraProperty.objectReferenceValue as Camera;
            if (keptDisplayCamera != null && keptDisplayCamera.gameObject.scene != scene)
            {
                keptDisplayCamera = null;
            }

            foreach (Camera sceneCamera in sceneCameras)
            {
                if (sceneCamera.name != "AirFooty Display Camera")
                {
                    continue;
                }

                if (keptDisplayCamera == null)
                {
                    keptDisplayCamera = sceneCamera;
                }
                else if (sceneCamera != keptDisplayCamera)
                {
                    UnityEngine.Object.DestroyImmediate(sceneCamera.gameObject);
                }
            }

            if (keptDisplayCamera == null)
            {
                GameObject cameraObject = new GameObject("AirFooty Display Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                keptDisplayCamera = cameraObject.AddComponent<Camera>();
            }

            ConfigureDisplayCamera(keptDisplayCamera);
            displayCameraProperty.objectReferenceValue = keptDisplayCamera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            InvokePrivate(menu, "BuildSelectionPanels");
            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"Could not save authored Air Footy scene at {AirFootyScenePath}.");
            }

            serialized.Update();
            Require(
                serialized.FindProperty("modeSelectionPanel").objectReferenceValue != null,
                AirFootyScenePath,
                "mode selection panel");
            Require(
                serialized.FindProperty("teamSelectionPanel").objectReferenceValue != null,
                AirFootyScenePath,
                "team selection panel");
            Require(
                keptDisplayCamera != null,
                AirFootyScenePath,
                "display camera");
        }
        finally
        {
            if (closeAfterBake && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ConfigureDisplayCamera(Camera displayCamera)
    {
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = new Color(0.015f, 0.02f, 0.08f, 1f);
        displayCamera.fieldOfView = 42f;
        displayCamera.nearClipPlane = 0.3f;
        displayCamera.farClipPlane = 1000f;
        displayCamera.targetTexture = null;
        displayCamera.targetDisplay = 0;
        displayCamera.tag = "MainCamera";
        displayCamera.transform.SetPositionAndRotation(
            new Vector3(-13.34f, 10.5f, -12.37f),
            Quaternion.Euler(30f, 47.146f, 0f));

        UniversalAdditionalCameraData cameraData =
            displayCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = true;
        GetOrAddComponent<AudioListener>(displayCamera.gameObject);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new MissingMethodException(target.GetType().Name, methodName);
        }

        method.Invoke(target, null);
    }

    private static void BakeWing(
        Transform visual,
        string name,
        float side,
        Material material)
    {
        GameObject wing = GetOrCreateChild(visual, name);
        wing.transform.localPosition = new Vector3(side * 0.72f, 0.48f, -0.05f);
        wing.transform.localScale = new Vector3(0.82f, 0.055f, 0.34f);
        if (wing.GetComponent<MeshFilter>() == null || wing.GetComponent<MeshRenderer>() == null)
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(primitive);
            GetOrAddComponent<MeshFilter>(wing).sharedMesh = mesh;
            GetOrAddComponent<MeshRenderer>(wing);
        }
        Collider collider = wing.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        wing.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void EnsureAudioSource(GameObject owner, float spatialBlend)
    {
        AudioSource source = GetOrAddComponent<AudioSource>(owner);
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
    }

    private static LineRenderer ConfigureRing(
        GameObject owner,
        int segments,
        float width,
        Material material)
    {
        LineRenderer line = ConfigureLine(owner, material);
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = width;
        line.endWidth = width;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }
        return line;
    }

    private static LineRenderer CreateLine(
        Transform parent,
        string name,
        Vector3[] positions,
        bool loop,
        float width,
        Material material,
        Color color)
    {
        LineRenderer line = ConfigureLine(GetOrCreateChild(parent, name), material);
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = positions.Length;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.SetPositions(positions);
        return line;
    }

    private static void ScaleRingPositions(LineRenderer line, float radius)
    {
        for (int i = 0; i < line.positionCount; i++)
        {
            line.SetPosition(i, line.GetPosition(i) * radius);
        }
    }

    private static LineRenderer ConfigureLine(GameObject owner, Material material)
    {
        LineRenderer line = GetOrAddComponent<LineRenderer>(owner);
        line.sharedMaterial = material;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        return line;
    }

    private static void ConfigureTrail(
        TrailRenderer trail,
        Material material,
        float time,
        float startWidth)
    {
        trail.sharedMaterial = material;
        trail.time = time;
        trail.minVertexDistance = 0.04f;
        trail.startWidth = startWidth;
        trail.endWidth = 0f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static T GetOrAddComponent<T>(GameObject owner) where T : Component
    {
        T component = owner.GetComponent<T>();
        return component != null ? component : owner.AddComponent<T>();
    }

    private static Material LoadOrCreateMaterial(
        string path,
        string shaderName,
        Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            string directory = path.Substring(0, path.LastIndexOf('/'));
            EnsureAssetDirectory(directory);
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureAssetDirectory(string directory)
    {
        string[] parts = directory.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static void EditPrefab(string path, Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateBakes()
    {
        foreach (string path in AirFootyPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (BallController3D ball in prefab.GetComponentsInChildren<BallController3D>(true))
            {
                Require(ball.GetComponent<TrailRenderer>() != null, path, "ball trail");
                Require(ball.transform.Find("AirFooty Ball Hover/Hover Ring") != null, path, "ball hover ring");
            }
            foreach (AIPlayer3D ai in prefab.GetComponentsInChildren<AIPlayer3D>(true))
            {
                Require(ai.transform.Find("AI Shot Telegraph") != null, path, "AI telegraph");
            }
        }

        GameObject flyer = AssetDatabase.LoadAssetAtPath<GameObject>(LabyrinthEnemyPrefabPaths[1]);
        Require(flyer.GetComponentInChildren<FlyingEnemyVisual>(true) != null,
            LabyrinthEnemyPrefabPaths[1], "flying visual component");
        Require(FindDeepChild(flyer.transform, "Left Flight Wing") != null,
            LabyrinthEnemyPrefabPaths[1], "left flying wing");
        Require(FindDeepChild(flyer.transform, "Right Flight Wing") != null,
            LabyrinthEnemyPrefabPaths[1], "right flying wing");
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
            Transform nested = FindDeepChild(child, name);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }

    private static void Require(bool condition, string path, string item)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"{path} is missing baked {item}.");
        }
    }
}
