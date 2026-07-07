using System.Collections.Generic;
using Sol.Arcade;
using Sol.Grab;
using Sol.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.EditorTools
{
    public static class MiniGamePrototypeBuilder
    {
        private const string MazeScenePath = "Assets/Jd/Scenes/Sc_TimedMazeEscape.unity";
        private const string HoopScenePath = "Assets/Jd/Scenes/Sc_HoopThrow.unity";
        private const string BounceScenePath = "Assets/Jd/Scenes/Sc_BounceTargets.unity";
        private const string ArcadeExteriorScenePath = "Assets/Shared/Scenes/Sc_ArcadeExterior.unity";
        private const string SharedPlayerPrefabPath = "Assets/Shared/Prefabs/SharedPlayerController.prefab";
        private const string ArcadeCabinetPrefabPath = "Assets/Jd/Prefabs/ArcadeCabinet.prefab";
        private const string BouncePrefabFolder = "Assets/Jd/Prefabs/Minigames/BounceTargets";
        private const string BounceBallPrefabPath = BouncePrefabFolder + "/BounceBall.prefab";
        private const string BounceTargetPrefabPath = BouncePrefabFolder + "/BounceTarget.prefab";
        private const string BounceLauncherPrefabPath = BouncePrefabFolder + "/BounceLauncher.prefab";
        private const string BounceBoardWallPrefabPath = BouncePrefabFolder + "/BounceBoardWall.prefab";
        private const string BouncyPhysicsMaterialPath = BouncePrefabFolder + "/PM_Bouncy.physicMaterial";

        [MenuItem("Sol/Minigames/Rebuild Prototype Scenes")]
        public static void RebuildPrototypeScenes()
        {
            BuildMazeEscapeScene();
            BuildHoopThrowScene();
            BuildBounceTargetsScene();
            AddScenesToBuildSettings(MazeScenePath, HoopScenePath, BounceScenePath);
            WireBounceTargetsCabinet();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt JD minigame prototype scenes.");
        }

        [MenuItem("Sol/Minigames/Rebuild Bounce Targets Prototype")]
        public static void RebuildBounceTargetsPrototype()
        {
            BuildBounceTargetsScene();
            AddScenesToBuildSettings(BounceScenePath);
            WireBounceTargetsCabinet();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt Bounce Targets prototype scene and prefabs.");
        }

        public static void BuildMazeEscapeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Sc_TimedMazeEscape";

            Material floor = CreateMaterial("Maze Floor", new Color(0.25f, 0.25f, 0.27f));
            Material wall = CreateMaterial("Maze Wall", new Color(0.7f, 0.68f, 0.5f));
            Material finish = CreateMaterial("Maze Finish", new Color(0.15f, 0.75f, 0.3f));

            CreateLighting();

            GameObject gameRoot = new GameObject("Timed Maze Escape Game");
            TimedMazeEscapeGame escapeGame = gameRoot.AddComponent<TimedMazeEscapeGame>();

            GameObject player = CreatePlayer(new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            GameObject builderRoot = new GameObject("Dynamic Maze Builder");
            DynamicMazeEscapeBuilder mazeBuilder = builderRoot.AddComponent<DynamicMazeEscapeBuilder>();
            ConfigureDynamicMazeBuilder(mazeBuilder, player != null ? player.transform : null, floor, wall, finish);

            SerializedObject serializedGame = new SerializedObject(escapeGame);
            serializedGame.FindProperty("mazeBuilder").objectReferenceValue = mazeBuilder;
            serializedGame.ApplyModifiedPropertiesWithoutUndo();

            CreateFallbackCamera(new Vector3(-8f, 8f, -8f), Quaternion.Euler(55f, 45f, 0f));

            EditorSceneManager.SaveScene(scene, MazeScenePath);
        }

        public static void BuildHoopThrowScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Sc_HoopThrow";

            Material floor = CreateMaterial("Hoop Floor", new Color(0.18f, 0.2f, 0.2f));
            Material rim = CreateMaterial("Hoop Rim", new Color(0.95f, 0.35f, 0.1f));
            Material ball = CreateMaterial("Throw Ball", new Color(0.2f, 0.55f, 1f));
            Material backstop = CreateMaterial("Backstop", new Color(0.22f, 0.22f, 0.24f));

            CreateLighting();
            CreateCube("Floor", new Vector3(0f, -0.1f, 6f), new Vector3(20f, 0.2f, 24f), floor, true);
            CreateCube("Back Wall", new Vector3(0f, 3f, 15f), new Vector3(20f, 6f, 0.5f), backstop, true);
            CreateCube("Left Rail", new Vector3(-10f, 1f, 6f), new Vector3(0.5f, 2f, 20f), backstop, true);
            CreateCube("Right Rail", new Vector3(10f, 1f, 6f), new Vector3(0.5f, 2f, 20f), backstop, true);

            GameObject gameRoot = new GameObject("Hoop Throw Game");
            gameRoot.AddComponent<HoopThrowGame>();

            CreateGameManagerWithGrab();
            CreateHoop("Large Hoop", new Vector3(-4.5f, 2.5f, 12f), 3.2f, 1, rim);
            CreateHoop("Medium Hoop", new Vector3(0f, 3f, 12f), 2.3f, 2, rim);
            CreateHoop("Small Hoop", new Vector3(4.5f, 3.4f, 12f), 1.5f, 3, rim);

            for (int i = 0; i < 5; i++)
            {
                CreateThrowableBall(new Vector3(-2f + i, 1.2f, 2f), ball);
            }

            CreatePlayer(new Vector3(0f, 0.2f, -2f), Quaternion.identity);
            CreateFallbackCamera(new Vector3(0f, 5f, -8f), Quaternion.Euler(25f, 0f, 0f));

            EditorSceneManager.SaveScene(scene, HoopScenePath);
        }

        public static void BuildBounceTargetsScene()
        {
            BuildBounceTargetsPrefabs();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Sc_BounceTargets";

            Material wall = LoadMaterial("Assets/Jd/Materials/M_Wall.mat", "Bounce Wall Fallback", new Color(0.7f, 0.68f, 0.5f));
            Material grey = LoadMaterial("Assets/Jd/Materials/M_Grey.mat", "Bounce Grey Fallback", new Color(0.35f, 0.35f, 0.35f));
            Material black = LoadMaterial("Assets/Jd/Materials/M_Black.mat", "Bounce Black Fallback", new Color(0.05f, 0.05f, 0.05f));

            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BounceBallPrefabPath);
            GameObject targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BounceTargetPrefabPath);
            GameObject launcherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BounceLauncherPrefabPath);
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BounceBoardWallPrefabPath);

            CreateLighting();
            Camera camera = CreateBounceCamera();
            CreateCube("Board Back Panel", new Vector3(0f, 0f, 0.35f), new Vector3(17f, 12.5f, 0.15f), black, false);
            CreateCube("Board Floor", new Vector3(0f, -6.25f, 0f), new Vector3(17f, 0.2f, 1f), grey, true);

            BounceTargetsGame game = new GameObject("Bounce Targets Game").AddComponent<BounceTargetsGame>();
            BounceLauncher launcher = InstantiatePrefab<BounceLauncher>(launcherPrefab, new Vector3(0f, -5.25f, 0f), Quaternion.identity);
            List<BounceTarget> targets = CreateBounceTargets(targetPrefab);

            InstantiateWall(wallPrefab, "Left Board Wall", new Vector3(-8f, 0f, 0f), new Vector3(0.45f, 11.5f, 1f));
            InstantiateWall(wallPrefab, "Right Board Wall", new Vector3(8f, 0f, 0f), new Vector3(0.45f, 11.5f, 1f));
            InstantiateWall(wallPrefab, "Top Board Wall", new Vector3(0f, 5.75f, 0f), new Vector3(16.45f, 0.45f, 1f));

            ConfigureBounceGame(game, launcher, ballPrefab != null ? ballPrefab.GetComponent<BounceBall>() : null, targets);
            ConfigureBounceLauncher(launcher, game, ballPrefab != null ? ballPrefab.GetComponent<BounceBall>() : null, camera);

            EditorSceneManager.SaveScene(scene, BounceScenePath);
        }

        private static void BuildBounceTargetsPrefabs()
        {
            EnsureFolder("Assets/Jd/Prefabs");
            EnsureFolder("Assets/Jd/Prefabs/Minigames");
            EnsureFolder(BouncePrefabFolder);

            PhysicsMaterial bouncyMaterial = CreateOrLoadBouncyPhysicsMaterial();
            Material blue = LoadMaterial("Assets/Jd/Materials/M_Blue.mat", "Bounce Ball Fallback", new Color(0.2f, 0.55f, 1f));
            Material gold = LoadMaterial("Assets/Jd/Materials/M_Gold.mat", "Bounce Target Fallback", new Color(1f, 0.75f, 0.15f));
            Material red = LoadMaterial("Assets/Jd/Materials/M_Red.mat", "Bounce Launcher Fallback", new Color(0.9f, 0.1f, 0.08f));
            Material wall = LoadMaterial("Assets/Jd/Materials/M_Wall.mat", "Bounce Wall Fallback", new Color(0.7f, 0.68f, 0.5f));

            CreateBounceBallPrefab(blue, bouncyMaterial);
            CreateBounceTargetPrefab(gold, bouncyMaterial);
            CreateBounceLauncherPrefab(red, gold);
            CreateBounceBoardWallPrefab(wall, bouncyMaterial);
        }

        private static void CreateBounceBallPrefab(Material material, PhysicsMaterial physicsMaterial)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "BounceBall";
            sphere.transform.localScale = Vector3.one * 0.45f;

            if (sphere.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            if (sphere.TryGetComponent(out SphereCollider collider))
            {
                collider.material = physicsMaterial;
            }

            Rigidbody rb = sphere.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.02f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezePositionZ;

            sphere.AddComponent<BounceBall>();
            SavePrefabAndDestroy(sphere, BounceBallPrefabPath);
        }

        private static void CreateBounceTargetPrefab(Material material, PhysicsMaterial physicsMaterial)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "BounceTarget";
            sphere.transform.localScale = Vector3.one * 0.65f;

            if (sphere.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            if (sphere.TryGetComponent(out SphereCollider collider))
            {
                collider.material = physicsMaterial;
            }

            BounceTarget target = sphere.AddComponent<BounceTarget>();
            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.FindProperty("scoreValue").intValue = 100;
            serializedTarget.FindProperty("requiredTarget").boolValue = true;
            serializedTarget.FindProperty("deactivateOnHit").boolValue = true;
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();

            SavePrefabAndDestroy(sphere, BounceTargetPrefabPath);
        }

        private static void CreateBounceLauncherPrefab(Material baseMaterial, Material arcMaterial)
        {
            GameObject root = new GameObject("BounceLauncher");
            BounceLauncher launcher = root.AddComponent<BounceLauncher>();

            GameObject baseObject = CreateCube("Base", Vector3.zero, new Vector3(1.3f, 0.35f, 0.8f), baseMaterial, true);
            baseObject.transform.SetParent(root.transform, false);

            GameObject barrel = CreateCube("Barrel", new Vector3(0f, 0.35f, 0f), new Vector3(0.32f, 0.85f, 0.32f), baseMaterial, true);
            barrel.transform.SetParent(root.transform, false);

            GameObject firePoint = new GameObject("Fire Point");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            GameObject arcObject = new GameObject("Aim Arc");
            arcObject.transform.SetParent(root.transform, false);
            LineRenderer lineRenderer = arcObject.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = arcMaterial;
            lineRenderer.widthMultiplier = 0.06f;
            lineRenderer.positionCount = 0;
            lineRenderer.useWorldSpace = true;

            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BounceBallPrefabPath);
            SerializedObject serializedLauncher = new SerializedObject(launcher);
            serializedLauncher.FindProperty("ballPrefab").objectReferenceValue = ballPrefab != null ? ballPrefab.GetComponent<BounceBall>() : null;
            serializedLauncher.FindProperty("firePoint").objectReferenceValue = firePoint.transform;
            serializedLauncher.FindProperty("aimArc").objectReferenceValue = lineRenderer;
            serializedLauncher.FindProperty("launchSpeed").floatValue = 16f;
            serializedLauncher.ApplyModifiedPropertiesWithoutUndo();

            SavePrefabAndDestroy(root, BounceLauncherPrefabPath);
        }

        private static void CreateBounceBoardWallPrefab(Material material, PhysicsMaterial physicsMaterial)
        {
            GameObject wall = CreateCube("BounceBoardWall", Vector3.zero, Vector3.one, material, true);

            if (wall.TryGetComponent(out BoxCollider collider))
            {
                collider.material = physicsMaterial;
            }

            SavePrefabAndDestroy(wall, BounceBoardWallPrefabPath);
        }

        private static Camera CreateBounceCamera()
        {
            GameObject cameraObject = new GameObject("Bounce Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -14f), Quaternion.identity);
            camera.orthographic = true;
            camera.orthographicSize = 6.6f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            return camera;
        }

        private static List<BounceTarget> CreateBounceTargets(GameObject targetPrefab)
        {
            List<BounceTarget> targets = new List<BounceTarget>();
            Vector3[] positions =
            {
                new Vector3(-4.8f, 3.9f, 0f),
                new Vector3(-2.4f, 4.45f, 0f),
                new Vector3(0f, 4.1f, 0f),
                new Vector3(2.4f, 4.45f, 0f),
                new Vector3(4.8f, 3.9f, 0f),
                new Vector3(-3.6f, 2.55f, 0f),
                new Vector3(-1.2f, 2.9f, 0f),
                new Vector3(1.2f, 2.9f, 0f),
                new Vector3(3.6f, 2.55f, 0f),
                new Vector3(-4.8f, 1.15f, 0f),
                new Vector3(-2.4f, 1.45f, 0f),
                new Vector3(0f, 1.15f, 0f),
                new Vector3(2.4f, 1.45f, 0f),
                new Vector3(4.8f, 1.15f, 0f),
                new Vector3(-3.2f, -0.35f, 0f),
                new Vector3(0f, -0.05f, 0f),
                new Vector3(3.2f, -0.35f, 0f)
            };

            foreach (Vector3 position in positions)
            {
                BounceTarget target = InstantiatePrefab<BounceTarget>(targetPrefab, position, Quaternion.identity);
                if (target != null)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private static void InstantiateWall(GameObject wallPrefab, string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = InstantiatePrefab<GameObject>(wallPrefab, position, Quaternion.identity);
            if (wall == null)
            {
                return;
            }

            wall.name = name;
            wall.transform.localScale = scale;
        }

        private static void ConfigureBounceGame(BounceTargetsGame game, BounceLauncher launcher, BounceBall ballPrefab, List<BounceTarget> targets)
        {
            SerializedObject serializedGame = new SerializedObject(game);
            serializedGame.FindProperty("roundShots").intValue = 10;
            serializedGame.FindProperty("launcher").objectReferenceValue = launcher;
            serializedGame.FindProperty("ballPrefab").objectReferenceValue = ballPrefab;
            serializedGame.FindProperty("physicsPlaneZ").floatValue = 0f;
            serializedGame.FindProperty("drainY").floatValue = -6.45f;
            serializedGame.FindProperty("returnSceneName").stringValue = "Sc_ArcadeExterior";

            SerializedProperty targetList = serializedGame.FindProperty("targets");
            targetList.arraySize = targets.Count;
            for (int i = 0; i < targets.Count; i++)
            {
                targetList.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            }

            serializedGame.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBounceLauncher(BounceLauncher launcher, BounceTargetsGame game, BounceBall ballPrefab, Camera camera)
        {
            SerializedObject serializedLauncher = new SerializedObject(launcher);
            serializedLauncher.FindProperty("game").objectReferenceValue = game;
            serializedLauncher.FindProperty("ballPrefab").objectReferenceValue = ballPrefab;
            serializedLauncher.FindProperty("aimCamera").objectReferenceValue = camera;
            serializedLauncher.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBounceTargetsCabinet()
        {
            Scene scene = EditorSceneManager.OpenScene(ArcadeExteriorScenePath, OpenSceneMode.Single);
            ArcadeMachineLauncher[] launchers = Object.FindObjectsByType<ArcadeMachineLauncher>(FindObjectsSortMode.None);

            foreach (ArcadeMachineLauncher launcher in launchers)
            {
                SerializedObject serializedLauncher = new SerializedObject(launcher);
                string targetSceneName = serializedLauncher.FindProperty("targetSceneName").stringValue;
                if (targetSceneName == BounceScenePath || targetSceneName == "Sc_BounceTargets")
                {
                    EditorSceneManager.SaveScene(scene);
                    return;
                }
            }

            GameObject cabinetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcadeCabinetPrefabPath);
            if (cabinetPrefab == null)
            {
                Debug.LogWarning($"Could not find arcade cabinet prefab at {ArcadeCabinetPrefabPath}.");
                return;
            }

            GameObject cabinet = (GameObject)PrefabUtility.InstantiatePrefab(cabinetPrefab, scene);
            cabinet.name = "ArcadeCabinet_BounceTargets";
            cabinet.transform.SetPositionAndRotation(new Vector3(1.16f, 1.2f, 15.25f), Quaternion.identity);

            ArcadeMachineLauncher launcherComponent = cabinet.GetComponentInChildren<ArcadeMachineLauncher>();
            if (launcherComponent != null)
            {
                SerializedObject serializedLauncher = new SerializedObject(launcherComponent);
                serializedLauncher.FindProperty("targetSceneName").stringValue = BounceScenePath;
                serializedLauncher.FindProperty("interactDistance").floatValue = 5f;
                serializedLauncher.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureDynamicMazeBuilder(
            DynamicMazeEscapeBuilder mazeBuilder,
            Transform player,
            Material floor,
            Material wall,
            Material finish)
        {
            SerializedObject serializedBuilder = new SerializedObject(mazeBuilder);
            serializedBuilder.FindProperty("player").objectReferenceValue = player;
            serializedBuilder.FindProperty("floorMaterial").objectReferenceValue = floor;
            serializedBuilder.FindProperty("wallMaterial").objectReferenceValue = wall;
            serializedBuilder.FindProperty("finishMaterial").objectReferenceValue = finish;
            serializedBuilder.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateGameManagerWithGrab()
        {
            GameObject gameManager = new GameObject("GameManager");
            GrabManager grabManager = gameManager.AddComponent<GrabManager>();

            SerializedObject serializedGrab = new SerializedObject(grabManager);
            serializedGrab.FindProperty("grabMode").enumValueIndex = (int)GrabMode.Crosshair;
            serializedGrab.FindProperty("grabInput").enumValueIndex = (int)GrabInputBinding.Attack;
            serializedGrab.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePlayer(Vector3 position, Quaternion rotation)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedPlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogWarning($"Could not find shared player prefab at {SharedPlayerPrefabPath}.");
                return null;
            }

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "SharedPlayerController";
            player.transform.SetPositionAndRotation(position, rotation);
            return player;
        }

        private static void CreateFallbackCamera(Vector3 position, Quaternion rotation)
        {
            if (Camera.main != null)
            {
                return;
            }

            GameObject cameraObject = new GameObject("Fallback Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            camera.nearClipPlane = 0.01f;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void AddWall(string name, float x, float z, float sizeX, float sizeZ, Material material)
        {
            CreateCube(name, new Vector3(x, 1.5f, z), new Vector3(sizeX, 3f, sizeZ), material, true);
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;

            if (material != null && cube.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider && cube.TryGetComponent(out Collider collider))
            {
                Object.DestroyImmediate(collider);
            }

            return cube;
        }

        private static void CreateHoop(string name, Vector3 position, float openingSize, int points, Material material)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(openingSize, openingSize, 0.7f);

            HoopScoreZone scoreZone = root.AddComponent<HoopScoreZone>();
            SerializedObject serializedZone = new SerializedObject(scoreZone);
            serializedZone.FindProperty("points").intValue = points;
            serializedZone.ApplyModifiedPropertiesWithoutUndo();

            float thickness = 0.18f;
            float rimSpan = openingSize + thickness;
            CreateHoopPart(root.transform, "Top Rim", new Vector3(0f, openingSize * 0.5f, 0f), new Vector3(rimSpan, thickness, thickness), material);
            CreateHoopPart(root.transform, "Bottom Rim", new Vector3(0f, -openingSize * 0.5f, 0f), new Vector3(rimSpan, thickness, thickness), material);
            CreateHoopPart(root.transform, "Left Rim", new Vector3(-openingSize * 0.5f, 0f, 0f), new Vector3(thickness, rimSpan, thickness), material);
            CreateHoopPart(root.transform, "Right Rim", new Vector3(openingSize * 0.5f, 0f, 0f), new Vector3(thickness, rimSpan, thickness), material);
        }

        private static void CreateHoopPart(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject part = CreateCube(name, Vector3.zero, scale, material, true);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
        }

        private static void CreateThrowableBall(Vector3 position, Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Throwable Ball";
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * 0.55f;

            if (sphere.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            Rigidbody rb = sphere.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;

            sphere.AddComponent<ThrowableScoreObject>();
            sphere.AddComponent<GrabbableComponent>();
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = name,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static Material LoadMaterial(string path, string fallbackName, Color fallbackColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            return material != null ? material : CreateMaterial(fallbackName, fallbackColor);
        }

        private static PhysicsMaterial CreateOrLoadBouncyPhysicsMaterial()
        {
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BouncyPhysicsMaterialPath);
            if (material == null)
            {
                material = new PhysicsMaterial("PM_Bouncy");
                AssetDatabase.CreateAsset(material, BouncyPhysicsMaterialPath);
            }

            material.dynamicFriction = 0f;
            material.staticFriction = 0f;
            material.bounciness = 0.95f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static T InstantiatePrefab<T>(GameObject prefab, Vector3 position, Quaternion rotation) where T : Object
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);

            if (typeof(T) == typeof(GameObject))
            {
                return instance as T;
            }

            return instance.GetComponent<T>();
        }

        private static void SavePrefabAndDestroy(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void AddScenesToBuildSettings(params string[] scenePaths)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (string scenePath in scenePaths)
            {
                if (ContainsScene(scenes, scenePath))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool ContainsScene(List<EditorBuildSettingsScene> scenes, string scenePath)
        {
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == scenePath)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
