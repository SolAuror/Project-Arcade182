using Sol.Minigames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Sol.EditorTools.HudBuilderKit;

namespace Sol.EditorTools
{
    /// <summary>
    /// Bakes the Atom Smasher runtime HUD into an authored uGUI prefab
    /// (Assets/0_Jd/Prefabs/UI/AtomSmasherHud.prefab) and swaps the scene over
    /// to it. The prefab is only built when missing — hand modifications
    /// survive re-runs; delete the prefab to regenerate from scratch.
    /// </summary>
    public static class AtomSmasherHudBuilder
    {
        private const string HudPrefabPath = "Assets/0_Jd/Prefabs/UI/AtomSmasherHud.prefab";
        private const string ScenePath = "Assets/0_Jd/Scenes/Sc_AtomSmasher.unity";

        // Retro-atomic theme
        private static readonly Color Navy = new Color32(0x0A, 0x12, 0x20, 0xE8);
        private static readonly Color Cyan = new Color32(0x35, 0xD8, 0xE8, 0xFF);
        private static readonly Color AtomOrange = new Color32(0xF0, 0x8A, 0x2A, 0xFF);
        private static readonly Color Frost = new Color32(0xDF, 0xE9, 0xF2, 0xFF);
        private static readonly Color DimFrost = new Color32(0x8F, 0xA2, 0xB5, 0xFF);

        [MenuItem("Sol/Setup/Atom Smasher Hud")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/0_Jd/Prefabs/UI"))
            {
                AssetDatabase.CreateFolder("Assets/0_Jd/Prefabs", "UI");
            }

            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (hudPrefab == null)
            {
                hudPrefab = BuildHudPrefab();
            }
            else
            {
                Debug.Log("AtomSmasherHud prefab already exists; keeping authored version.");
            }

            WireScene(hudPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("Atom Smasher HUD setup complete.");
        }

        private static GameObject BuildHudPrefab()
        {
            GameObject root = CreateCanvasRoot("AtomSmasherHud");
            AtomSmasherHud hud = root.AddComponent<AtomSmasherHud>();

            // ---- Info panel (top-left) ----
            RectTransform infoPanel = Panel(root.transform, "InfoPanel", Navy, Cyan);
            Place(infoPanel, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(340f, 208f));

            Row(infoPanel, "TitleText", -12f, 22f, "— ATOM SMASHER —", 15, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text scoreText = Row(infoPanel, "ScoreText", -34f, 40f, "Score 0", 30, Frost, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text waveText = Row(infoPanel, "WaveText", -78f, 22f, "Wave 1", 16, Frost);
            Text shotsText = Row(infoPanel, "ShotsText", -102f, 22f, "Shots 10/10", 16, Frost);
            Text targetsText = Row(infoPanel, "TargetsText", -126f, 22f, "Targets left 0", 16, Frost);
            Text multiplierText = Row(infoPanel, "MultiplierText", -150f, 22f, "Chain x1", 16, AtomOrange, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text timerText = Row(infoPanel, "TimerRow", -174f, 22f, "Timer 60s", 16, AtomOrange);
            GameObject timerRow = timerText.gameObject;

            // ---- Status hint (bottom-center) ----
            RectTransform statusRect = Container(root.transform, "StatusText");
            Place(statusRect, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(680f, 28f));
            Text statusText = statusRect.gameObject.AddComponent<Text>();
            StyleText(statusText, "Aim and release to launch.", 17, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);

            // ---- Result banner (center-high) ----
            RectTransform resultGroup = Panel(root.transform, "ResultGroup", Navy, AtomOrange);
            Place(resultGroup, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(600f, 96f));
            Text resultText = StretchText(resultGroup, "ResultText", "BOARD CLEARED", 24, Frost, FontStyle.Bold);
            resultGroup.gameObject.SetActive(false);

            // ---- Wire ----
            SerializedObject serialized = new SerializedObject(hud);
            serialized.FindProperty("scoreText").objectReferenceValue = scoreText;
            serialized.FindProperty("waveText").objectReferenceValue = waveText;
            serialized.FindProperty("shotsText").objectReferenceValue = shotsText;
            serialized.FindProperty("targetsText").objectReferenceValue = targetsText;
            serialized.FindProperty("multiplierText").objectReferenceValue = multiplierText;
            serialized.FindProperty("timerRow").objectReferenceValue = timerRow;
            serialized.FindProperty("timerText").objectReferenceValue = timerText;
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
            serialized.FindProperty("resultGroup").objectReferenceValue = resultGroup.gameObject;
            serialized.FindProperty("resultText").objectReferenceValue = resultText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void WireScene(GameObject hudPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<AtomSmasherHud>(FindObjectsInactive.Include) == null)
            {
                PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
