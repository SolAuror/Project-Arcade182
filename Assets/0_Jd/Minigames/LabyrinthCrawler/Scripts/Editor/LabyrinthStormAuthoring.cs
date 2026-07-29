using System;
using System.IO;
using Sol.Minigames;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Bakes deterministic placeholder art and installs the storm driver.
    /// The generated assets are intentionally authored on disk; the runtime
    /// components never construct textures, materials or audio.
    /// </summary>
    public static class LabyrinthStormAuthoring
    {
        private const string PrefabPath =
            "Assets/0_Jd/Minigames/LabyrinthCrawler/LabyrinthCrawlerGame.prefab";
        private const string MaterialPath =
            "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/M_StormSky.mat";
        private const string BoltMaterialPath =
            "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/M_StormBolt.mat";
        private const string TextureFolder =
            "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/Textures";
        private const string CloudPath = TextureFolder + "/T_StormClouds.png";
        private const string EntityPath = TextureFolder + "/T_EntitySilhouette.png";
        private const string SkylinePath = TextureFolder + "/T_StormSkyline.png";
        private const string ValidationImageFileName = "StormSkyValidation.png";

        [MenuItem("Sol/Labyrinth Crawler/Author Storm Sky Scaffold")]
        public static void Build()
        {
            EnsureFolders();

            BakeTextureIfMissing(
                CloudPath,
                BakeCloudNoise,
                TextureWrapMode.Repeat,
                TextureWrapMode.Repeat);
            BakeTextureIfMissing(
                EntityPath,
                BakeEntityPlaceholder,
                TextureWrapMode.Clamp,
                TextureWrapMode.Clamp);
            BakeTextureIfMissingOrWrongSize(
                SkylinePath,
                BakeSkyline,
                TextureWrapMode.Repeat,
                TextureWrapMode.Clamp,
                1024,
                160);

            Material skyMaterial = LoadOrCreateSkyMaterial(out bool createdSkyMaterial);
            Material boltMaterial = LoadOrCreateBoltMaterial(out bool createdBoltMaterial);
            AssignTextureIfMissing(
                skyMaterial,
                "_CloudTex",
                CloudPath,
                createdSkyMaterial);
            AssignTextureIfMissing(
                skyMaterial,
                "_EntityMask",
                EntityPath,
                createdSkyMaterial);
            AssignTextureIfMissing(
                skyMaterial,
                "_SkylineMask",
                SkylinePath,
                createdSkyMaterial);
            if (createdSkyMaterial)
            {
                ApplyStormSkyPalette(skyMaterial);
                EditorUtility.SetDirty(skyMaterial);
            }
            if (createdBoltMaterial)
            {
                boltMaterial.SetColor("_BaseColor", new Color(0.78f, 1f, 0.68f, 1f));
                EditorUtility.SetDirty(boltMaterial);
            }

            InstallIntoPrefab(skyMaterial, boltMaterial);

            AssetDatabase.SaveAssets();
            Debug.Log(
                "LabyrinthStormAuthoring: ensured storm assets and updated the existing runtime wiring. " +
                "Existing art and tuning were preserved except for outdated generated skyline masks.");
        }

        [MenuItem("Sol/Labyrinth Crawler/Validate Storm Sky Render")]
        public static void ValidateStormSkyRender()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                throw new InvalidOperationException("M_StormSky.mat is missing.");
            }

            const int size = 128;
            GameObject cameraObject = new GameObject("StormSkyValidationCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = 0;
            camera.fieldOfView = 90f;
            camera.aspect = 1f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.rotation = Quaternion.Euler(-25f, 0f, 0f);

            RenderTexture target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
            {
                name = "StormSkyValidationRT",
                antiAliasing = 1
            };
            Material previousSkybox = RenderSettings.skybox;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D capture = null;

            try
            {
                RenderSettings.skybox = material;
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                capture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                capture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                capture.Apply(false, false);

                Color[] pixels = capture.GetPixels();
                float minimum = 1f;
                float maximum = 0f;
                float total = 0f;
                foreach (Color pixel in pixels)
                {
                    float luma = pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f;
                    minimum = Mathf.Min(minimum, luma);
                    maximum = Mathf.Max(maximum, luma);
                    total += luma;
                }

                string outputPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "..", ValidationImageFileName));
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());
                Debug.Log(
                    $"LabyrinthStormAuthoring: validation sky render " +
                    $"supported={material.shader.isSupported}, shaderErrors={ShaderUtil.ShaderHasError(material.shader)}, " +
                    $"luma min={minimum:F4}, avg={total / pixels.Length:F4}, max={maximum:F4}. " +
                    $"Saved {outputPath}");
            }
            finally
            {
                RenderSettings.skybox = previousSkybox;
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                if (capture != null)
                {
                    UnityEngine.Object.DestroyImmediate(capture);
                }
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void EnsureFolders()
        {
            const string labyrinthFolder = "Assets/0_Jd/Minigames/LabyrinthCrawler";
            const string materialsFolder = labyrinthFolder + "/GameMaterials";

            if (!AssetDatabase.IsValidFolder(materialsFolder))
            {
                AssetDatabase.CreateFolder(labyrinthFolder, "GameMaterials");
            }
            if (!AssetDatabase.IsValidFolder(TextureFolder))
            {
                AssetDatabase.CreateFolder(materialsFolder, "Textures");
            }
        }

        private static void BakeTextureIfMissing(
            string path,
            Func<Texture2D> bake,
            TextureWrapMode wrapU,
            TextureWrapMode wrapV)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
            {
                return;
            }

            SaveTexture(bake(), path, wrapU, wrapV);
        }

        private static void BakeTextureIfMissingOrWrongSize(
            string path,
            Func<Texture2D> bake,
            TextureWrapMode wrapU,
            TextureWrapMode wrapV,
            int expectedWidth,
            int expectedHeight)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null
                && existing.width == expectedWidth
                && existing.height == expectedHeight)
            {
                return;
            }

            SaveTexture(bake(), path, wrapU, wrapV);
        }

        private static void AssignTextureIfMissing(
            Material material,
            string propertyName,
            string texturePath,
            bool forceAssignment)
        {
            if (!forceAssignment && material.GetTexture(propertyName) != null)
            {
                return;
            }

            material.SetTexture(
                propertyName,
                AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(material);
        }

        private static Material LoadOrCreateSkyMaterial(out bool created)
        {
            Shader shader = Shader.Find("Arcade/PS1/Storm Sky");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Arcade/PS1/Storm Sky was not imported. Resolve shader compile errors before authoring.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                created = true;
                material = new Material(shader)
                {
                    name = "M_StormSky"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                created = false;
                material.shader = shader;
            }

            return material;
        }

        private static Material LoadOrCreateBoltMaterial(out bool created)
        {
            Shader shader = Shader.Find("Arcade/PS1/Lightning Bolt");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Arcade/PS1/Lightning Bolt was not imported. Resolve shader compile errors before authoring.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(BoltMaterialPath);
            if (material == null)
            {
                created = true;
                material = new Material(shader)
                {
                    name = "M_StormBolt"
                };
                AssetDatabase.CreateAsset(material, BoltMaterialPath);
            }
            else
            {
                created = false;
                material.shader = shader;
            }

            return material;
        }

        private static void InstallIntoPrefab(
            Material skyMaterial,
            Material boltMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                bool prefabChanged = false;
                RetroPresenter presenter = root.GetComponentInChildren<RetroPresenter>(true);
                if (presenter == null)
                {
                    throw new InvalidOperationException(
                        "LabyrinthCrawlerGame.prefab does not contain RetroPresenter.");
                }

                SerializedObject presenterObject = new SerializedObject(presenter);
                SerializedProperty skyProperty =
                    presenterObject.FindProperty("stormSkyMaterial");
                bool firstInstallation = skyProperty.objectReferenceValue == null;
                if (firstInstallation)
                {
                    skyProperty.objectReferenceValue = skyMaterial;
                    presenterObject.FindProperty("fogColor").colorValue =
                        new Color(0.035f, 0.045f, 0.03f, 1f);
                    presenterObject.FindProperty("fogDensity").floatValue = 0.11f;
                    presenterObject.ApplyModifiedPropertiesWithoutUndo();
                    prefabChanged = true;
                }

                // Runtime selects only the nearest point lights for shadows.
                // Serialize every candidate with shadows off so Edit mode and
                // prefab previews do not allocate six shadow faces per light.
                foreach (Light localLight in root.GetComponentsInChildren<Light>(true))
                {
                    if (localLight.type == LightType.Point)
                    {
                        bool lightChanged = false;
                        if (localLight.shadows != LightShadows.None)
                        {
                            localLight.shadows = LightShadows.None;
                            lightChanged = true;
                        }
                        UniversalAdditionalLightData lightData =
                            localLight.GetUniversalAdditionalLightData();
                        SerializedObject lightDataObject = new SerializedObject(lightData);
                        SerializedProperty tierProperty = lightDataObject.FindProperty(
                            "m_AdditionalLightsShadowResolutionTier");
                        int lowTier =
                            UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
                        if (tierProperty.intValue != lowTier)
                        {
                            tierProperty.intValue = lowTier;
                            lightDataObject.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(lightData);
                            lightChanged = true;
                        }
                        if (lightChanged)
                        {
                            EditorUtility.SetDirty(localLight);
                            prefabChanged = true;
                        }
                    }
                }

                Transform stormTransform = root.transform.Find("StormDirector");
                bool createdStormObject = stormTransform == null;
                GameObject stormObject;
                if (createdStormObject)
                {
                    stormObject = new GameObject("StormDirector");
                    stormObject.transform.SetParent(root.transform, false);
                    prefabChanged = true;
                }
                else
                {
                    stormObject = stormTransform.gameObject;
                }

                AudioSource thunderSource = GetOrAddComponent<AudioSource>(
                    stormObject,
                    out bool createdThunderSource);
                if (createdThunderSource)
                {
                    thunderSource.playOnAwake = false;
                    thunderSource.loop = false;
                    thunderSource.spatialBlend = 0f;
                    thunderSource.volume = 0.8f;
                    prefabChanged = true;
                }

                Light lightningLight = GetOrAddComponent<Light>(
                    stormObject,
                    out bool createdLightningLight);
                if (createdLightningLight)
                {
                    lightningLight.type = LightType.Directional;
                    lightningLight.color = new Color(0.72f, 1f, 0.62f, 1f);
                    lightningLight.intensity = 0f;
                    // This is a distant directional illumination effect, not
                    // a physical strike that needs another shadow render.
                    lightningLight.shadows = LightShadows.None;
                    lightningLight.renderMode = LightRenderMode.ForcePixel;
                    lightningLight.enabled = false;
                    prefabChanged = true;
                }

                LineRenderer boltGlow = FindOrCreateBoltLine(
                    stormObject.transform,
                    "BoltGlow",
                    boltMaterial,
                    0.62f,
                    out bool changedBoltGlow);
                LineRenderer boltCore = FindOrCreateBoltLine(
                    stormObject.transform,
                    "BoltCore",
                    boltMaterial,
                    0.16f,
                    out bool changedBoltCore);
                prefabChanged |= changedBoltGlow || changedBoltCore;

                StormDirector director = GetOrAddComponent<StormDirector>(
                    stormObject,
                    out bool createdDirector);
                prefabChanged |= createdDirector;
                SerializedObject directorObject = new SerializedObject(director);
                bool referencesChanged =
                    AssignReferenceIfDifferent(directorObject, "presenter", presenter)
                    | AssignReferenceIfDifferent(directorObject, "thunderSource", thunderSource)
                    | AssignReferenceIfDifferent(directorObject, "lightningLight", lightningLight)
                    | AssignReferenceIfDifferent(directorObject, "boltCore", boltCore)
                    | AssignReferenceIfDifferent(directorObject, "boltGlow", boltGlow);
                if (referencesChanged)
                {
                    directorObject.ApplyModifiedPropertiesWithoutUndo();
                    prefabChanged = true;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static LineRenderer FindOrCreateBoltLine(
            Transform parent,
            string name,
            Material material,
            float width,
            out bool changed)
        {
            Transform existingTransform = parent.Find(name);
            GameObject lineObject;
            bool createdObject = existingTransform == null;
            if (createdObject)
            {
                lineObject = new GameObject(name);
                lineObject.transform.SetParent(parent, false);
            }
            else
            {
                lineObject = existingTransform.gameObject;
            }

            LineRenderer line = GetOrAddComponent<LineRenderer>(
                lineObject,
                out bool createdLine);
            changed = createdObject || createdLine;
            if (createdObject || createdLine)
            {
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.widthMultiplier = width;
                line.positionCount = 0;
                line.numCapVertices = 0;
                line.numCornerVertices = 0;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                line.enabled = false;
            }
            else if (line.sharedMaterial == null)
            {
                line.sharedMaterial = material;
                changed = true;
            }

            return line;
        }

        private static bool AssignReferenceIfDifferent(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property.objectReferenceValue == value)
            {
                return false;
            }

            property.objectReferenceValue = value;
            return true;
        }

        private static T GetOrAddComponent<T>(
            GameObject gameObject,
            out bool created)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            created = component == null;
            if (created)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static void ApplyStormSkyPalette(Material material)
        {
            material.SetColor("_HorizonColor", new Color(0.13f, 0.18f, 0.085f, 1f));
            material.SetColor("_ZenithColor", new Color(0.02f, 0.034f, 0.024f, 1f));
            material.SetColor("_CloudDarkColor", new Color(0.027f, 0.042f, 0.027f, 1f));
            material.SetColor("_CloudLightColor", new Color(0.25f, 0.35f, 0.14f, 1f));
            material.SetColor("_SkylineColor", new Color(0.01f, 0.014f, 0.01f, 1f));
            material.SetColor("_HazeColor", new Color(0.18f, 0.24f, 0.10f, 1f));
            material.SetFloat("_CloudPlaneScale", 0.20f);
            material.SetFloat("_CloudScaleA", 1f);
            material.SetFloat("_CloudScaleB", 2.5f);
            material.SetFloat("_CloudContrast", 1.7f);
            material.SetFloat("_WarpScale", 1.5f);
            material.SetFloat("_WarpStrength", 0.48f);
            material.SetFloat("_SwirlAmount", 1.5f);
            material.SetFloat("_EntitySize", 0.62f);
            material.SetFloat("_EntityDarkness", 0.22f);
            material.SetFloat("_SkylineHeight", 0.13f);
            material.SetFloat("_SkylineBelowHorizon", 0.10f);
            material.SetFloat("_SkylineRepeatFar", 1f);
            material.SetFloat("_SkylineRepeatMid", 1.7f);
            material.SetFloat("_SkylineRepeatNear", 2.9f);
            material.SetFloat("_SkylineAirlightFar", 0.8f);
            material.SetFloat("_SkylineAirlightMid", 0.6f);
            material.SetFloat("_SkylineAirlightNear", 0.35f);
            material.SetFloat("_SkyFogBlend", 0.16f);
        }

        private static Texture2D BakeCloudNoise()
        {
            const int size = 256;
            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float primary = FractalPeriodicNoise(u, v, 17);
                    float warp = FractalPeriodicNoise(u, v, 89);
                    byte r = ToByte(primary);
                    byte g = ToByte(warp);
                    pixels[y * size + x] = new Color32(r, g, r, 255);
                }
            }

            texture.SetPixels32(pixels);
            return texture;
        }

        private static Texture2D BakeEntityPlaceholder()
        {
            const int size = 256;
            Texture2D texture = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float px = u - 0.5f;
                    float py = v - 0.5f;

                    float mask = 0f;
                    mask = Mathf.Max(mask, EllipseMask(px, py - 0.04f, 0.23f, 0.25f));
                    mask = Mathf.Max(mask, EllipseMask(px, py - 0.25f, 0.12f, 0.13f));
                    mask = Mathf.Max(mask, EllipseMask(px - 0.18f, py - 0.18f, 0.11f, 0.08f));
                    mask = Mathf.Max(mask, EllipseMask(px + 0.18f, py - 0.18f, 0.11f, 0.08f));

                    // Crown/horns. These are deliberately broad graphic
                    // shapes: a painted or rendered alpha should replace them.
                    mask = Mathf.Max(mask, TaperedSpike(px, py, -0.12f, 0.22f, 0.36f, 0.055f));
                    mask = Mathf.Max(mask, TaperedSpike(px, py, 0f, 0.25f, 0.43f, 0.06f));
                    mask = Mathf.Max(mask, TaperedSpike(px, py, 0.12f, 0.22f, 0.36f, 0.055f));
                    mask = Mathf.Max(mask, CurvedHorn(px, py, -1f));
                    mask = Mathf.Max(mask, CurvedHorn(px, py, 1f));

                    // Seven hanging tentacles with differing phase/length.
                    for (int tentacle = 0; tentacle < 7; tentacle++)
                    {
                        float t = tentacle / 6f;
                        float baseX = Mathf.Lerp(-0.2f, 0.2f, t);
                        float length = Mathf.Lerp(0.28f, 0.44f, Hash01(tentacle, 71));
                        float bottom = 0.03f - length;
                        if (py <= 0.04f && py >= bottom)
                        {
                            float along = Mathf.InverseLerp(0.04f, bottom, py);
                            float curve = Mathf.Sin(along * 4.2f + tentacle * 1.7f)
                                * Mathf.Lerp(0.018f, 0.055f, along);
                            float width = Mathf.Lerp(0.035f, 0.009f, along);
                            float distance = Mathf.Abs(px - baseX - curve);
                            mask = Mathf.Max(
                                mask,
                                1f - SmoothThreshold(width * 0.7f, width, distance));
                        }
                    }

                    byte value = ToByte(Mathf.Clamp01(mask));
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            texture.SetPixels32(pixels);
            return texture;
        }

        private static Texture2D BakeSkyline()
        {
            const int width = 1024;
            const int height = 160;
            Texture2D texture = NewTexture(width, height);
            Color32[] pixels = new Color32[width * height];
            byte[] far = BakeSkylineLayer(width, height, 40319, 8, 27, 20, 79);
            byte[] mid = BakeSkylineLayer(width, height, 51047, 10, 35, 26, 103);
            byte[] near = BakeSkylineLayer(width, height, 71861, 14, 45, 32, 131);

            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(far[index], mid[index], near[index], 255);
            }

            texture.SetPixels32(pixels);
            return texture;
        }

        private static byte[] BakeSkylineLayer(
            int width,
            int height,
            int seed,
            int minimumTowerWidth,
            int maximumTowerWidth,
            int minimumTowerHeight,
            int maximumTowerHeight)
        {
            byte[] pixels = new byte[width * height];
            System.Random random = new System.Random(seed);

            // Leave matching empty edge columns so point-sampled repetition
            // crosses a clean gap instead of bisecting a tower at the seam.
            int cursor = random.Next(3, 9);
            int rightLimit = width - 8;
            while (cursor < rightLimit)
            {
                int towerWidth = random.Next(minimumTowerWidth, maximumTowerWidth);
                int towerHeight = random.Next(minimumTowerHeight, maximumTowerHeight);
                int right = Mathf.Min(rightLimit, cursor + towerWidth);

                FillTower(pixels, width, height, cursor, right, towerHeight);
                int crownStyle = random.Next(0, 4);
                int center = (cursor + right) / 2;
                if (crownStyle == 0)
                {
                    DrawSpire(pixels, width, height, center, towerHeight, random.Next(10, 31));
                }
                else if (crownStyle == 1)
                {
                    DrawBattlements(pixels, width, height, cursor, right, towerHeight);
                }
                else if (crownStyle == 2)
                {
                    DrawDome(pixels, width, height, center, towerHeight, towerWidth / 2);
                }

                cursor = right + random.Next(1, 6);
            }

            return pixels;
        }

        private static void FillTower(
            byte[] pixels,
            int width,
            int height,
            int left,
            int right,
            int towerHeight)
        {
            for (int x = left; x < right; x++)
            {
                for (int y = 0; y < towerHeight; y++)
                {
                    SetMaskPixel(pixels, width, height, x, y);
                }
            }
        }

        private static void DrawSpire(
            byte[] pixels,
            int width,
            int height,
            int center,
            int baseY,
            int spireHeight)
        {
            for (int y = 0; y < spireHeight && baseY + y < height; y++)
            {
                int halfWidth = Mathf.Max(1, Mathf.RoundToInt((1f - y / (float)spireHeight) * 5f));
                for (int x = center - halfWidth; x <= center + halfWidth; x++)
                {
                    SetMaskPixel(pixels, width, height, x, baseY + y);
                }
            }
        }

        private static void DrawBattlements(
            byte[] pixels,
            int width,
            int height,
            int left,
            int right,
            int baseY)
        {
            for (int x = left; x < right; x++)
            {
                if (((x - left) / 4) % 2 == 0)
                {
                    for (int y = baseY; y < baseY + 6; y++)
                    {
                        SetMaskPixel(pixels, width, height, x, y);
                    }
                }
            }
        }

        private static void DrawDome(
            byte[] pixels,
            int width,
            int height,
            int center,
            int baseY,
            int radius)
        {
            radius = Mathf.Max(3, radius);
            for (int y = 0; y <= radius; y++)
            {
                float normalized = y / (float)radius;
                int halfWidth = Mathf.RoundToInt(Mathf.Sqrt(1f - normalized * normalized) * radius);
                for (int x = center - halfWidth; x <= center + halfWidth; x++)
                {
                    SetMaskPixel(pixels, width, height, x, baseY + y);
                }
            }
        }

        private static void SetMaskPixel(
            byte[] pixels,
            int width,
            int height,
            int x,
            int y)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                pixels[y * width + x] = 255;
            }
        }

        private static float FractalPeriodicNoise(float u, float v, int seed)
        {
            float total = 0f;
            float amplitude = 0.55f;
            float amplitudeSum = 0f;
            int period = 4;

            for (int octave = 0; octave < 5; octave++)
            {
                total += PeriodicValueNoise(u * period, v * period, period, seed + octave * 31)
                    * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return total / amplitudeSum;
        }

        private static float PeriodicValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            float tx = Smooth01(x - x0);
            float ty = Smooth01(y - y0);

            float a = Hash01(PositiveModulo(x0, period), PositiveModulo(y0, period), seed);
            float b = Hash01(PositiveModulo(x1, period), PositiveModulo(y0, period), seed);
            float c = Hash01(PositiveModulo(x0, period), PositiveModulo(y1, period), seed);
            float d = Hash01(PositiveModulo(x1, period), PositiveModulo(y1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float EllipseMask(float x, float y, float radiusX, float radiusY)
        {
            float distance = Mathf.Sqrt(
                x * x / (radiusX * radiusX) +
                y * y / (radiusY * radiusY));
            return 1f - SmoothThreshold(0.92f, 1f, distance);
        }

        private static float TaperedSpike(
            float x,
            float y,
            float centerX,
            float baseY,
            float tipY,
            float baseHalfWidth)
        {
            if (y < baseY || y > tipY)
            {
                return 0f;
            }

            float along = Mathf.InverseLerp(baseY, tipY, y);
            float halfWidth = Mathf.Lerp(baseHalfWidth, 0.002f, along);
            return 1f - SmoothThreshold(
                halfWidth * 0.75f,
                halfWidth,
                Mathf.Abs(x - centerX));
        }

        private static float CurvedHorn(float x, float y, float side)
        {
            if (y < 0.18f || y > 0.42f || Mathf.Sign(x) != Mathf.Sign(side))
            {
                return 0f;
            }

            float along = Mathf.InverseLerp(0.18f, 0.42f, y);
            float centerX = side * Mathf.Lerp(0.18f, 0.34f, along);
            float width = Mathf.Lerp(0.045f, 0.012f, along);
            return 1f - SmoothThreshold(
                width * 0.7f,
                width,
                Mathf.Abs(x - centerX));
        }

        private static float Smooth01(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float SmoothThreshold(float edge0, float edge1, float value)
        {
            float normalized = Mathf.InverseLerp(edge0, edge1, value);
            return Smooth01(normalized);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static float Hash01(int value, int seed)
        {
            return Hash01(value, value * 17 + 11, seed);
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point
            };
        }

        private static void SaveTexture(
            Texture2D texture,
            string path,
            TextureWrapMode wrapU,
            TextureWrapMode wrapV)
        {
            texture.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapModeU = wrapU;
            importer.wrapModeV = wrapV;
            importer.SaveAndReimport();
        }
    }
}
