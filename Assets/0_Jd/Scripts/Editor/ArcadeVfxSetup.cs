using Sol.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.EditorTools
{
    /// <summary>
    /// Authors the feedback VFX that code already expects but was never built:
    /// the Atom Smasher ball boost trail (<c>AtomSmasherBall.boostTrail</c>), the
    /// four Atom Smasher feedback particle bursts (<c>AtomSmasherGame.PlayFeedback</c>
    /// warns while they are unassigned), and matching trails + impact bursts on the
    /// Labyrinth Crawler projectiles. Safe to re-run; assets are rebuilt in place.
    /// </summary>
    public static class ArcadeVfxSetup
    {
        private const string AtomFolder = "Assets/0_Jd/Prefabs/Minigames/AtomSmasher";
        private const string LabyrinthFolder = "Assets/0_Jd/Prefabs/Minigames/LabyrinthCrawler";
        private const string BallPrefabPath = AtomFolder + "/AtomSmasherBall.prefab";
        private const string AtomScenePath = "Assets/0_Jd/Scenes/Sc_AtomSmasher.unity";

        [MenuItem("Sol/Setup/Arcade Feedback Vfx")]
        public static void BuildAll()
        {
            Material particleMaterial = EnsureParticleMaterial(AtomFolder, "Mat_AS_Particles");

            ParticleSystem hitBurst = EnsureBurstPrefab(AtomFolder, "VFX_AtomHit", new Color(1f, 0.7f, 0.2f));
            ParticleSystem quantumBurst = EnsureBurstPrefab(AtomFolder, "VFX_AtomQuantum", new Color(0.3f, 0.9f, 1f));
            ParticleSystem waveClearBurst = EnsureBurstPrefab(AtomFolder, "VFX_AtomWaveClear", new Color(0.35f, 1f, 0.45f));
            ParticleSystem failBurst = EnsureBurstPrefab(AtomFolder, "VFX_AtomFail", new Color(1f, 0.25f, 0.25f));

            AddBallBoostTrail(particleMaterial);
            WireAtomSmasherScene(hitBurst, quantumBurst, waveClearBurst, failBurst);

            GameObject fireballHit = EnsureBurstPrefab(LabyrinthFolder, "VFX_SpellHit_Fireball", new Color(1f, 0.55f, 0.1f)).gameObject;
            GameObject enemyBoltHit = EnsureBurstPrefab(LabyrinthFolder, "VFX_SpellHit_EnemyBolt", new Color(1f, 0.15f, 0.25f)).gameObject;

            AddProjectileVfx(LabyrinthFolder + "/Projectile_Fireball.prefab", fireballHit, new Color(1f, 0.55f, 0.1f), particleMaterial);
            AddProjectileVfx(LabyrinthFolder + "/Projectile_EnemyBolt.prefab", enemyBoltHit, new Color(1f, 0.15f, 0.25f), particleMaterial);

            AssetDatabase.SaveAssets();
            Debug.Log("Arcade feedback VFX setup complete.");
        }

        private static Material EnsureParticleMaterial(string folder, string assetName)
        {
            string path = $"{folder}/{assetName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ParticleSystem EnsureBurstPrefab(string folder, string assetName, Color color)
        {
            string path = $"{folder}/{assetName}.prefab";
            Material particleMaterial = EnsureParticleMaterial(AtomFolder, "Mat_AS_Particles");

            GameObject root = new GameObject(assetName);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = color;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            ParticleSystemRenderer particleRenderer = root.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ParticleSystem>();
        }

        private static void AddBallBoostTrail(Material trailMaterial)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(BallPrefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"ArcadeVfxSetup could not load {BallPrefabPath}.");
                return;
            }

            Transform existing = contents.transform.Find("BoostTrail");
            GameObject trailObject = existing != null ? existing.gameObject : new GameObject("BoostTrail");
            trailObject.transform.SetParent(contents.transform, false);
            trailObject.transform.localPosition = Vector3.zero;

            if (!trailObject.TryGetComponent(out TrailRenderer trail))
            {
                trail = trailObject.AddComponent<TrailRenderer>();
            }

            ConfigureTrail(trail, trailMaterial, new Color(0.3f, 0.9f, 1f), 0.25f, 0.16f);
            trail.emitting = false; // AtomSmasherBall toggles this during quantum boosts

            var ball = contents.GetComponent<AtomSmasherBall>();
            if (ball != null)
            {
                SerializedObject ballSerialized = new SerializedObject(ball);
                ballSerialized.FindProperty("boostTrail").objectReferenceValue = trail;
                ballSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, BallPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void WireAtomSmasherScene(
            ParticleSystem hitBurst,
            ParticleSystem quantumBurst,
            ParticleSystem waveClearBurst,
            ParticleSystem failBurst)
        {
            Scene scene = EditorSceneManager.OpenScene(AtomScenePath, OpenSceneMode.Single);
            AtomSmasherGame game = Object.FindFirstObjectByType<AtomSmasherGame>();
            if (game == null)
            {
                Debug.LogWarning($"ArcadeVfxSetup found no AtomSmasherGame in {AtomScenePath}.");
                return;
            }

            SerializedObject gameSerialized = new SerializedObject(game);
            gameSerialized.FindProperty("targetHitParticles").objectReferenceValue = hitBurst;
            gameSerialized.FindProperty("quantumTriggerParticles").objectReferenceValue = quantumBurst;
            gameSerialized.FindProperty("waveClearParticles").objectReferenceValue = waveClearBurst;
            gameSerialized.FindProperty("failedRoundParticles").objectReferenceValue = failBurst;
            gameSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AddProjectileVfx(string prefabPath, GameObject hitVfxPrefab, Color trailColor, Material trailMaterial)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"ArcadeVfxSetup could not load {prefabPath}.");
                return;
            }

            Transform existing = contents.transform.Find("Trail");
            GameObject trailObject = existing != null ? existing.gameObject : new GameObject("Trail");
            trailObject.transform.SetParent(contents.transform, false);
            trailObject.transform.localPosition = Vector3.zero;

            if (!trailObject.TryGetComponent(out TrailRenderer trail))
            {
                trail = trailObject.AddComponent<TrailRenderer>();
            }

            ConfigureTrail(trail, trailMaterial, trailColor, 0.2f, 0.12f);
            trail.emitting = true;

            var projectile = contents.GetComponent<Projectile>();
            if (projectile != null)
            {
                SerializedObject projectileSerialized = new SerializedObject(projectile);
                projectileSerialized.FindProperty("hitVfxPrefab").objectReferenceValue = hitVfxPrefab;
                projectileSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void ConfigureTrail(TrailRenderer trail, Material material, Color color, float time, float startWidth)
        {
            trail.sharedMaterial = material;
            trail.time = time;
            trail.startWidth = startWidth;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
