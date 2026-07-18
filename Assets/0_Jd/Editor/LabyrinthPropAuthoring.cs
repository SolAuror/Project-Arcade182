using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Batch authoring for the Labyrinth props that used to be runtime-built
    /// (see the authored-assets policy: only the maze is generated at runtime).
    ///
    /// 1. ExitPad.prefab - extracted from the user-authored "ExitPad" object
    ///    inside DungeonExit.prefab (the one carrying LabyrinthExitPad), so
    ///    the game's fallback pad is the real authored pad, not a primitive.
    /// 2. Beam_Laser.prefab - two-layer beacon beam (bright core line inside a
    ///    wide translucent glow shell, Minecraft-beacon style) plus a subtle
    ///    edge-emitter mote system laid along the beam, with material assets.
    /// 3. Wires the ExitPad prefab into LabyrinthCrawlerGame.prefab
    ///    (exitPadPrefab) and the beam prefab into Spell_Laser.asset
    ///    (beamPrefab).
    ///
    /// Run closed-editor:
    ///   Unity.exe -batchmode -quit -projectPath [project] -executeMethod
    ///   Sol.Minigames.EditorTools.LabyrinthPropAuthoring.Build
    /// </summary>
    public static class LabyrinthPropAuthoring
    {
        private const string DungeonExitPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/DungeonExit.prefab";
        private const string ExitPadPrefabPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/GamePrefabs/ExitPad.prefab";
        private const string BeamPrefabPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/GamePrefabs/Beam_Laser.prefab";
        private const string GamePrefabPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/LabyrinthCrawlerGame.prefab";
        private const string LaserSpellPath = "Assets/0_Jd/SO_Spells/Spell_Laser.asset";
        private const string CoreMaterialPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/M_LaserCore.mat";
        private const string GlowMaterialPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/M_LaserGlow.mat";
        private const string MoteMaterialPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/M_LaserMote.mat";

        public static void Build()
        {
            GameObject exitPadAsset = ExtractExitPad();
            GameObject beamAsset = BuildLaserBeam();
            WireGamePrefab(exitPadAsset);
            WireLaserSpell(beamAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("LabyrinthPropAuthoring: authored ExitPad.prefab + Beam_Laser.prefab and wired both references.");
        }

        private static GameObject ExtractExitPad()
        {
            GameObject roomRoot = PrefabUtility.LoadPrefabContents(DungeonExitPath);
            GameObject padCopy = null;
            try
            {
                LabyrinthExitPad sourcePad = roomRoot.GetComponentInChildren<LabyrinthExitPad>(true);
                if (sourcePad == null)
                {
                    throw new System.InvalidOperationException(
                        $"No LabyrinthExitPad found inside {DungeonExitPath}; cannot extract the exit pad.");
                }

                padCopy = Object.Instantiate(sourcePad.gameObject);
                padCopy.name = "ExitPad";
                return PrefabUtility.SaveAsPrefabAsset(padCopy, ExitPadPrefabPath);
            }
            finally
            {
                if (padCopy != null)
                {
                    Object.DestroyImmediate(padCopy);
                }

                PrefabUtility.UnloadPrefabContents(roomRoot);
            }
        }

        private static GameObject BuildLaserBeam()
        {
            Material coreMaterial = LoadOrCreateSpriteMaterial(CoreMaterialPath);
            Material glowMaterial = LoadOrCreateSpriteMaterial(GlowMaterialPath);
            Material moteMaterial = LoadOrCreateSpriteMaterial(MoteMaterialPath);

            GameObject beamRoot = new GameObject("Beam_Laser");
            try
            {
                LineRenderer core = CreateBeamLine(
                    beamRoot.transform, "Core", coreMaterial, 0.05f,
                    new Color(0.85f, 0.97f, 1f, 1f), sortingOrder: 1);
                LineRenderer glow = CreateBeamLine(
                    beamRoot.transform, "Glow", glowMaterial, 0.22f,
                    new Color(0.4f, 0.9f, 1f, 0.35f), sortingOrder: 0);
                ParticleSystem motes = CreateBeamMotes(beamRoot.transform, moteMaterial);

                HitscanBeam beam = beamRoot.AddComponent<HitscanBeam>();
                SerializedObject serializedBeam = new SerializedObject(beam);
                serializedBeam.FindProperty("coreLine").objectReferenceValue = core;
                serializedBeam.FindProperty("glowLine").objectReferenceValue = glow;
                serializedBeam.FindProperty("motes").objectReferenceValue = motes;
                serializedBeam.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(beamRoot, BeamPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(beamRoot);
            }
        }

        private static LineRenderer CreateBeamLine(
            Transform parent, string childName, Material material, float width, Color color, int sortingOrder)
        {
            GameObject lineObject = new GameObject(childName);
            lineObject.transform.SetParent(parent, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sharedMaterial = material;
            line.numCapVertices = 2; // rounded ends, no square-cut beam tips
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private static ParticleSystem CreateBeamMotes(Transform parent, Material material)
        {
            GameObject moteObject = new GameObject("Motes");
            moteObject.transform.SetParent(parent, false);

            ParticleSystem motes = moteObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = motes.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.04f);
            main.startColor = new Color(0.4f, 0.9f, 1f, 0.6f);
            main.gravityModifier = 0f;
            main.loop = true;

            // HitscanBeam lays this edge along the beam each Flash and toggles
            // emission with visibility.
            ParticleSystem.EmissionModule emission = motes.emission;
            emission.enabled = true;
            emission.rateOverTime = 26f;

            ParticleSystem.ShapeModule shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = motes.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer moteRenderer = moteObject.GetComponent<ParticleSystemRenderer>();
            moteRenderer.sharedMaterial = material;
            moteRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            moteRenderer.shadowCastingMode = ShadowCastingMode.Off;
            moteRenderer.receiveShadows = false;
            return motes;
        }

        private static void WireGamePrefab(GameObject exitPadAsset)
        {
            LabyrinthExitPad padComponent = exitPadAsset.GetComponent<LabyrinthExitPad>();
            GameObject gameRoot = PrefabUtility.LoadPrefabContents(GamePrefabPath);
            try
            {
                LabyrinthCrawlerGame game = gameRoot.GetComponent<LabyrinthCrawlerGame>();
                SerializedObject serializedGame = new SerializedObject(game);
                serializedGame.FindProperty("exitPadPrefab").objectReferenceValue = padComponent;
                serializedGame.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(gameRoot, GamePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(gameRoot);
            }
        }

        private static void WireLaserSpell(GameObject beamAsset)
        {
            HitscanBeam beamComponent = beamAsset.GetComponent<HitscanBeam>();
            HitscanSpellDefinition laser = AssetDatabase.LoadAssetAtPath<HitscanSpellDefinition>(LaserSpellPath);
            if (laser == null)
            {
                throw new System.InvalidOperationException($"Laser spell asset not found at {LaserSpellPath}.");
            }

            SerializedObject serializedSpell = new SerializedObject(laser);
            serializedSpell.FindProperty("beamPrefab").objectReferenceValue = beamComponent;
            serializedSpell.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(laser);
        }

        private static Material LoadOrCreateSpriteMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new System.InvalidOperationException("Sprites/Default shader not found.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
