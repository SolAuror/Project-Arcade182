using System.Collections.Generic;
using Sol.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.EditorTools
{
    /// <summary>
    /// One-shot authoring for the Labyrinth Crawler combat pass: spell assets,
    /// projectile and enemy prefabs, player prefab combat components, and scene
    /// wiring. Safe to re-run; existing assets are updated in place.
    /// </summary>
    public static class LabyrinthCrawlerSetup
    {
        private const string SpellFolder = "Assets/0_Jd/Spells";
        private const string PrefabFolder = "Assets/0_Jd/Prefabs/Minigames/LabyrinthCrawler";
        private const string PlayerPrefabPath = "Assets/Prefabs/SharedPlayerController.prefab";
        private const string ScenePath = "Assets/0_Jd/Scenes/Sc_LabyrinthCrawler.unity";

        [MenuItem("Sol/Setup/Labyrinth Crawler Combat")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/0_Jd", "Spells");
            EnsureFolder("Assets/0_Jd/Prefabs/Minigames", "LabyrinthCrawler");

            Material fireballMaterial = EnsureMaterial("Mat_Fireball", new Color(1f, 0.55f, 0.1f));
            Material enemyBoltMaterial = EnsureMaterial("Mat_EnemyBolt", new Color(1f, 0.15f, 0.25f));
            Material enemyCasterMaterial = EnsureMaterial("Mat_EnemyCaster", new Color(0.55f, 0.2f, 0.85f));
            Material enemyStalkerMaterial = EnsureMaterial("Mat_EnemyStalker", new Color(0.85f, 0.15f, 0.15f));

            Projectile fireballProjectile = EnsureProjectilePrefab("Projectile_Fireball", 0.3f, fireballMaterial);
            Projectile enemyBoltProjectile = EnsureProjectilePrefab("Projectile_EnemyBolt", 0.25f, enemyBoltMaterial);

            SpellDefinition fireball = EnsureSpellAsset<ProjectileSpellDefinition>("Spell_Fireball", spell =>
            {
                SetBaseStats(spell, "Fireball", 25f, 15f, 0.5f);
                spell.FindProperty("projectilePrefab").objectReferenceValue = fireballProjectile;
                spell.FindProperty("speed").floatValue = 18f;
            });

            SpellDefinition laser = EnsureSpellAsset<HitscanSpellDefinition>("Spell_Laser", spell =>
            {
                SetBaseStats(spell, "Laser", 12f, 8f, 0.25f);
                spell.FindProperty("range").floatValue = 30f;
            });

            SpellDefinition pulse = EnsureSpellAsset<AoeSpellDefinition>("Spell_Pulse", spell =>
            {
                SetBaseStats(spell, "Pulse", 30f, 35f, 1.5f);
                spell.FindProperty("baseRadius").floatValue = 5f;
            });

            SpellDefinition enemyBolt = EnsureSpellAsset<ProjectileSpellDefinition>("Spell_EnemyBolt", spell =>
            {
                SetBaseStats(spell, "Shadow Bolt", 8f, 0f, 1.6f);
                spell.FindProperty("projectilePrefab").objectReferenceValue = enemyBoltProjectile;
                spell.FindProperty("speed").floatValue = 12f;
            });

            SpellDefinition enemyClaw = EnsureSpellAsset<HitscanSpellDefinition>("Spell_EnemyClaw", spell =>
            {
                SetBaseStats(spell, "Claw", 10f, 0f, 1f);
                spell.FindProperty("range").floatValue = 2.5f;
            });

            GameObject enemyCaster = EnsureEnemyPrefab(
                "Enemy_Caster", enemyCasterMaterial, enemyBolt,
                maxHealth: 40f, detectionRange: 14f, attackRange: 8f, moveSpeed: 3.2f);

            GameObject enemyStalker = EnsureEnemyPrefab(
                "Enemy_Stalker", enemyStalkerMaterial, enemyClaw,
                maxHealth: 55f, detectionRange: 16f, attackRange: 2.2f, moveSpeed: 4.2f);

            SetupPlayerPrefab(new List<SpellDefinition> { fireball, laser, pulse });
            SetupScene(new List<GameObject> { enemyCaster, enemyStalker }, new List<SpellDefinition> { fireball, laser, pulse });

            AssetDatabase.SaveAssets();
            Debug.Log("Labyrinth Crawler combat setup complete.");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material EnsureMaterial(string assetName, Color color)
        {
            string path = $"{PrefabFolder}/{assetName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Material defaultMaterial = probe.GetComponent<Renderer>().sharedMaterial;
                material = new Material(defaultMaterial);
                Object.DestroyImmediate(probe);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Projectile EnsureProjectilePrefab(string assetName, float scale, Material material)
        {
            string path = $"{PrefabFolder}/{assetName}.prefab";

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = assetName;
            root.transform.localScale = Vector3.one * scale;
            root.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            root.AddComponent<Projectile>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Projectile>();
        }

        private static SpellDefinition EnsureSpellAsset<T>(string assetName, System.Action<SerializedObject> configure) where T : SpellDefinition
        {
            string path = $"{SpellFolder}/{assetName}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject serialized = new SerializedObject(asset);
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetBaseStats(SerializedObject spell, string displayName, float damage, float manaCost, float cooldown)
        {
            spell.FindProperty("displayName").stringValue = displayName;
            spell.FindProperty("baseDamage").floatValue = damage;
            spell.FindProperty("manaCost").floatValue = manaCost;
            spell.FindProperty("cooldownSeconds").floatValue = cooldown;
        }

        private static GameObject EnsureEnemyPrefab(
            string assetName,
            Material material,
            SpellDefinition spell,
            float maxHealth,
            float detectionRange,
            float attackRange,
            float moveSpeed)
        {
            string path = $"{PrefabFolder}/{assetName}.prefab";

            GameObject root = new GameObject(assetName);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.GetComponent<Renderer>().sharedMaterial = material;

            GameObject eye = new GameObject("Eye");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0.55f, 0.35f);

            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.4f;
            characterController.center = Vector3.zero;

            EnemyController enemy = root.AddComponent<EnemyController>(); // auto-adds Health + SpellCaster

            SerializedObject healthSerialized = new SerializedObject(root.GetComponent<Health>());
            healthSerialized.FindProperty("maxHealth").floatValue = maxHealth;
            healthSerialized.FindProperty("faction").enumValueIndex = (int)Faction.Enemy;
            healthSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject casterSerialized = new SerializedObject(root.GetComponent<SpellCaster>());
            SerializedProperty slots = casterSerialized.FindProperty("slots");
            slots.arraySize = 1;
            slots.GetArrayElementAtIndex(0).FindPropertyRelative("definition").objectReferenceValue = spell;
            slots.GetArrayElementAtIndex(0).FindPropertyRelative("unlockedAtStart").boolValue = true;
            casterSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject enemySerialized = new SerializedObject(enemy);
            enemySerialized.FindProperty("detectionRange").floatValue = detectionRange;
            enemySerialized.FindProperty("attackRange").floatValue = attackRange;
            enemySerialized.FindProperty("moveSpeed").floatValue = moveSpeed;
            enemySerialized.FindProperty("eye").objectReferenceValue = eye.transform;
            enemySerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetupPlayerPrefab(List<SpellDefinition> spells)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"LabyrinthCrawlerSetup could not load {PlayerPrefabPath}; player combat components must be added manually.");
                return;
            }

            if (!contents.TryGetComponent(out Health health))
            {
                health = contents.AddComponent<Health>();
            }

            SerializedObject healthSerialized = new SerializedObject(health);
            healthSerialized.FindProperty("faction").enumValueIndex = (int)Faction.Player;
            healthSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!contents.TryGetComponent(out Mana _))
            {
                contents.AddComponent<Mana>();
            }

            if (!contents.TryGetComponent(out SpellCaster caster))
            {
                caster = contents.AddComponent<SpellCaster>();
            }

            SerializedObject casterSerialized = new SerializedObject(caster);
            SerializedProperty slots = casterSerialized.FindProperty("slots");
            slots.arraySize = spells.Count;
            for (int i = 0; i < spells.Count; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("definition").objectReferenceValue = spells[i];
                slot.FindPropertyRelative("unlockedAtStart").boolValue = i == 0; // Fireball only; the rest unlock via cards
            }

            casterSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!contents.TryGetComponent(out PlayerSpellInput _))
            {
                contents.AddComponent<PlayerSpellInput>();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void SetupScene(List<GameObject> enemyPrefabList, List<SpellDefinition> spells)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabyrinthCrawlerGame game = Object.FindFirstObjectByType<LabyrinthCrawlerGame>();
            if (game == null)
            {
                Debug.LogWarning($"LabyrinthCrawlerSetup found no LabyrinthCrawlerGame in {ScenePath}.");
                return;
            }

            if (!game.TryGetComponent(out MinigameTimer timer))
            {
                timer = game.gameObject.AddComponent<MinigameTimer>();
            }

            SerializedObject timerSerialized = new SerializedObject(timer);
            timerSerialized.FindProperty("mode").enumValueIndex = (int)MinigameTimer.TimerMode.Stopwatch;
            timerSerialized.FindProperty("startOnEnable").boolValue = false;
            timerSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (!game.TryGetComponent(out LabyrinthUpgradeScreen _))
            {
                game.gameObject.AddComponent<LabyrinthUpgradeScreen>();
            }

            SerializedObject gameSerialized = new SerializedObject(game);
            gameSerialized.FindProperty("runTimer").objectReferenceValue = timer;

            SerializedProperty enemyPrefabs = gameSerialized.FindProperty("enemyPrefabs");
            enemyPrefabs.arraySize = enemyPrefabList.Count;
            for (int i = 0; i < enemyPrefabList.Count; i++)
            {
                enemyPrefabs.GetArrayElementAtIndex(i).objectReferenceValue = enemyPrefabList[i];
            }

            SerializedProperty playerSpells = gameSerialized.FindProperty("playerSpells");
            playerSpells.arraySize = spells.Count;
            for (int i = 0; i < spells.Count; i++)
            {
                playerSpells.GetArrayElementAtIndex(i).objectReferenceValue = spells[i];
            }

            gameSerialized.FindProperty("playerSpellsUnlockedAtStart").intValue = 1;
            gameSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
