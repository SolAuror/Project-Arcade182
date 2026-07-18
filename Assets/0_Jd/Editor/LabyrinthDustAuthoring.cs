using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Batch authoring for the Labyrinth ambient dust: builds a fully
    /// configured ParticleSystem child ("DustMotes") inside
    /// LabyrinthCrawlerGame.prefab plus its material asset, replacing the old
    /// runtime-built system. All velocity axes use TwoConstants curves - Unity
    /// requires every axis of velocityOverLifetime to share one curve mode
    /// (mixing a constant with a range threw "Particle Velocity curves must
    /// all be in the same mode" at runtime).
    ///
    /// Run closed-editor:
    ///   Unity.exe -batchmode -quit -projectPath [project] -executeMethod
    ///   Sol.Minigames.EditorTools.LabyrinthDustAuthoring.Build
    /// </summary>
    public static class LabyrinthDustAuthoring
    {
        private const string PrefabPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/LabyrinthCrawlerGame.prefab";
        private const string MaterialPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/M_DustMote.mat";

        public static void Build()
        {
            Material moteMaterial = LoadOrCreateMaterial();

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Remove the old runtime-builder component and any stale child.
                foreach (DustMotes stale in root.GetComponentsInChildren<DustMotes>(true))
                {
                    Object.DestroyImmediate(stale);
                }

                Transform staleChild = root.transform.Find("DustMotes");
                if (staleChild != null)
                {
                    Object.DestroyImmediate(staleChild.gameObject);
                }

                GameObject dust = new GameObject("DustMotes");
                dust.transform.SetParent(root.transform, false);

                ParticleSystem motes = dust.AddComponent<ParticleSystem>();
                ConfigureMotes(motes);

                ParticleSystemRenderer moteRenderer = dust.GetComponent<ParticleSystemRenderer>();
                moteRenderer.sharedMaterial = moteMaterial;
                moteRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                moteRenderer.shadowCastingMode = ShadowCastingMode.Off;
                moteRenderer.receiveShadows = false;

                dust.AddComponent<DustMotes>();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("LabyrinthDustAuthoring: authored the DustMotes particle system into LabyrinthCrawlerGame.prefab.");
        }

        private static void ConfigureMotes(ParticleSystem motes)
        {
            ParticleSystem.MainModule main = motes.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 70;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            // Aged parchment dust; pure white reads too clean for the dungeon.
            main.startColor = new Color(0.85f, 0.82f, 0.72f, 1f);
            main.gravityModifier = 0f;
            main.prewarm = true;
            main.loop = true;

            ParticleSystem.EmissionModule emission = motes.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;

            ParticleSystem.ShapeModule shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 9f, 14f);

            // Fade in, hang, fade out - motes never pop into or out of view.
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
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.2f, 0.15f),
                    new GradientAlphaKey(0.2f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            // Gentle settle plus a whisper of lateral drift. Every axis is a
            // TwoConstants curve on purpose - the modes must match.
            ParticleSystem.VelocityOverLifetimeModule velocity = motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.045f, -0.015f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            ParticleSystem.NoiseModule noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.04f;
            noise.damping = true;
        }

        private static Material LoadOrCreateMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new System.InvalidOperationException("Sprites/Default shader not found for the dust mote material.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
