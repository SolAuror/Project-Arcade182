using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Batch authoring for the last runtime-built shared FX (authored-assets
    /// policy: only the maze is generated at runtime):
    ///
    /// 1. Resources/DamagePopup.prefab - TextMesh popup with its dark
    ///    readability drop-shadow child, LegacyRuntime font wired.
    /// 2. Resources/SpellBurstVisual.prefab - translucent shockwave sphere
    ///    with its material asset (M_SpellBurst).
    ///
    /// Both live in a Resources folder because their spawners are static
    /// methods with no scene context; the scripts Resources.Load once and
    /// instantiate from then on.
    ///
    /// Run closed-editor:
    ///   Unity.exe -batchmode -quit -projectPath [project] -executeMethod
    ///   Sol.Minigames.EditorTools.SharedFxAuthoring.Build
    /// </summary>
    public static class SharedFxAuthoring
    {
        private const string ResourcesFolder = "Assets/0_Jd/Resources";
        private const string PopupPrefabPath = ResourcesFolder + "/DamagePopup.prefab";
        private const string BurstPrefabPath = ResourcesFolder + "/SpellBurstVisual.prefab";
        private const string BurstMaterialPath = "Assets/0_Jd/Minigames/LabyrinthCrawler/GameMaterials/M_SpellBurst.mat";

        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/0_Jd", "Resources");
            }

            BuildPopupPrefab();
            BuildBurstPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("SharedFxAuthoring: authored DamagePopup.prefab + SpellBurstVisual.prefab into Resources.");
        }

        private static void BuildPopupPrefab()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject popupRoot = new GameObject("DamagePopup");
            try
            {
                TextMesh text = CreateText(popupRoot, font, new Color(1f, 0.85f, 0.3f, 1f));

                GameObject shadowObject = new GameObject("Shadow");
                shadowObject.transform.SetParent(popupRoot.transform, false);
                shadowObject.transform.localPosition = new Vector3(0.03f, -0.03f, 0.02f);
                TextMesh shadow = CreateText(shadowObject, font, new Color(0f, 0f, 0f, 0.85f));

                DamagePopup popup = popupRoot.AddComponent<DamagePopup>();
                SerializedObject serializedPopup = new SerializedObject(popup);
                serializedPopup.FindProperty("textMesh").objectReferenceValue = text;
                serializedPopup.FindProperty("shadowMesh").objectReferenceValue = shadow;
                serializedPopup.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(popupRoot, PopupPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(popupRoot);
            }
        }

        private static TextMesh CreateText(GameObject target, Font font, Color color)
        {
            TextMesh text = target.GetComponent<TextMesh>();
            if (text == null)
            {
                text = target.AddComponent<TextMesh>();
            }

            text.text = "0";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 46;
            text.characterSize = 0.035f;
            text.fontStyle = FontStyle.Bold;
            text.color = color;

            if (font != null)
            {
                text.font = font;
                MeshRenderer textRenderer = target.GetComponent<MeshRenderer>();
                if (textRenderer != null)
                {
                    textRenderer.sharedMaterial = font.material;
                    textRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    textRenderer.receiveShadows = false;
                }
            }

            return text;
        }

        private static void BuildBurstPrefab()
        {
            Material burstMaterial = AssetDatabase.LoadAssetAtPath<Material>(BurstMaterialPath);
            if (burstMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    throw new System.InvalidOperationException("Sprites/Default shader not found for the burst material.");
                }

                burstMaterial = new Material(shader);
                AssetDatabase.CreateAsset(burstMaterial, BurstMaterialPath);
            }

            GameObject burstRoot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                burstRoot.name = "SpellBurstVisual";

                Collider burstCollider = burstRoot.GetComponent<Collider>();
                if (burstCollider != null)
                {
                    Object.DestroyImmediate(burstCollider);
                }

                Renderer burstRenderer = burstRoot.GetComponent<Renderer>();
                burstRenderer.sharedMaterial = burstMaterial;
                burstRenderer.shadowCastingMode = ShadowCastingMode.Off;
                burstRenderer.receiveShadows = false;

                SpellBurstVisual burst = burstRoot.AddComponent<SpellBurstVisual>();
                SerializedObject serializedBurst = new SerializedObject(burst);
                serializedBurst.FindProperty("burstRenderer").objectReferenceValue = burstRenderer;
                serializedBurst.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(burstRoot, BurstPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(burstRoot);
            }
        }
    }
}
