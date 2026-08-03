using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Fills the WallSocket variant lists on the Labyrinth room prefabs from the
    /// imported kit, by model name. Every wall socket gets the same sets: the
    /// socket copies each wall's own default transform when it spawns a part, so a
    /// full-edge variant orients correctly on all four walls without per-direction
    /// authoring.
    ///
    /// Two wall sets: the ground cells and the full-height UpperCell substitute
    /// from the full-WIDTH wall pieces; UpperCell_Half substitutes from the
    /// half-HEIGHT HorizontalHalfWall pieces. RoofCell prefabs are NOT touched -
    /// they keep their authored internals and the generator only places + rotates
    /// them. Skipped kit pieces: VerticalHalfWall* (half width, no slot), the
    /// gables/roofs (now authored roof cells), corner posts and FloorTIle.
    /// Re-runnable: it overwrites the lists each time. Menu: Sol > Labyrinth >
    /// Populate Wall Socket Lists.
    /// </summary>
    public static class PopulateWallSockets
    {
        private const string ModelsDir = "Assets/0_Jd/Minigames/LabyrinthCrawler/Models";
        private const string RoomsDir = "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms";

        // Ground cells + the full-height upper cell: substitute full walls.
        private static readonly string[] FullWallPrefabPaths =
        {
            RoomsDir + "/DungeonCell.prefab",
            RoomsDir + "/DungeonCellLit.prefab",
            RoomsDir + "/DungeonSpawn.prefab",
            RoomsDir + "/DungeonExit.prefab",
            RoomsDir + "/UpperCell.prefab",
        };

        // The half-height upper cell: substitute horizontal-half walls.
        private static readonly string[] HalfWallPrefabPaths =
        {
            RoomsDir + "/UpperCell_Half.prefab",
        };

        // Full-width CLOSED walls: plain, window and arrowslit variants (incl. the
        // "dual" 2-opening and _L/_R offset versions - all full walls, so a closed
        // edge, never a gap). Upper-floor facades draw from this same set.
        private static readonly string[] FullSolidNames =
        {
            "Wall",
            "Wall_Window", "Wall_Window_L", "Wall_Window_R", "Wall_DualWindow",
            "Wall_Arrowslit", "Wall_Arrowslit_L", "Wall_Arrowslit_R", "Wall_DoubleArrowslit",
        };

        // Full-width PASSAGES: every piece with a walkable doorway or archway. Only
        // used on the ground maze (upper floors never open a passage), but assigned
        // everywhere for simplicity.
        private static readonly string[] FullPassageNames =
        {
            "Wall_Doorway", "Wall_Doorway_L", "Wall_Doorway_R",
            "Wall_DoubleDoorway", "Wall_DoubleDoorway_L", "Wall_DoubleDoorway_R",
            "Wall_DualDoorway",
            "Wall_Arch", "Wall_Arch_L", "Wall_Arch_R",
            "Wall_Arch_Pier", "Wall_Arch_L_Pier", "Wall_Arch_R_Pier",
            "Wall_DoubleArchway", "Wall_DoubleArchway_Pier",
            "Wall_Doorway+Arrowslit", "Wall_Arrowslit+Door",
        };

        // Half-height CLOSED walls for UpperCell_Half facades.
        private static readonly string[] HalfSolidNames =
        {
            "HorizontalHalfWall",
            "HorizontalHalfWall_Window", "HorizontalHalfWall_Window_L", "HorizontalHalfWall_Window_R",
            "HorizontalHalfWall_DualWindow",
            "HorizontalHalfWall_Arrowslit", "HorizontalHalfWall_Arrowslit_L", "HorizontalHalfWall_Arrowslit_R",
            "HorizontalHalfWall_DualArrowslit",
        };

        // Half-height passages (unused in the sky, assigned for completeness).
        private static readonly string[] HalfPassageNames =
        {
            "HorizontalHalfWall_Archway_Pier",
            "HorizontalHalfWall_Archway_L_Pier", "HorizontalHalfWall_Archway_R_Pier",
            "HorizontalHalfWall_DoubleArchway_Pier",
        };

        [MenuItem("Sol/Labyrinth/Populate Wall Socket Lists")]
        public static void Populate()
        {
            List<GameObject> fullSolids = LoadModels(FullSolidNames);
            List<GameObject> fullPassages = LoadModels(FullPassageNames);
            List<GameObject> halfSolids = LoadModels(HalfSolidNames);
            List<GameObject> halfPassages = LoadModels(HalfPassageNames);

            if (fullSolids.Count == 0 && fullPassages.Count == 0)
            {
                Debug.LogError($"PopulateWallSockets: found no models under {ModelsDir}; nothing assigned.");
                return;
            }

            int socketsFilled = 0;
            int prefabsChanged = 0;

            foreach (string path in FullWallPrefabPaths)
            {
                if (FillPrefab(path, fullSolids, fullPassages, ref socketsFilled))
                {
                    prefabsChanged++;
                }
            }

            foreach (string path in HalfWallPrefabPaths)
            {
                if (FillPrefab(path, halfSolids, halfPassages, ref socketsFilled))
                {
                    prefabsChanged++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"PopulateWallSockets: done. {socketsFilled} socket(s) across {prefabsChanged} prefab(s). " +
                      $"Full walls: {fullSolids.Count} solid + {fullPassages.Count} passage; " +
                      $"half walls: {halfSolids.Count} solid + {halfPassages.Count} passage.");
        }

        // Fills every WallSocket on one prefab with the given solid/passage sets.
        // Returns true if the prefab existed and was written. Optional prefabs
        // (UpperCell / UpperCell_Half) are silently skipped until authored.
        private static bool FillPrefab(string path, List<GameObject> solids, List<GameObject> passages, ref int socketsFilled)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                return false; // not authored yet
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"PopulateWallSockets: could not load {path}; skipped.");
                return false;
            }

            try
            {
                WallSocket[] sockets = root.GetComponentsInChildren<WallSocket>(true);
                if (sockets.Length == 0)
                {
                    Debug.LogWarning($"PopulateWallSockets: {System.IO.Path.GetFileName(path)} has no WallSockets; run 'Wire Wall Sockets' first.");
                    return false;
                }

                foreach (WallSocket socket in sockets)
                {
                    SerializedObject so = new SerializedObject(socket);
                    SetList(so.FindProperty("solidVariants"), solids);
                    SetList(so.FindProperty("passageVariants"), passages);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    socketsFilled++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"PopulateWallSockets: {System.IO.Path.GetFileName(path)} - {sockets.Length} socket(s) filled.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetList(SerializedProperty listProp, List<GameObject> items)
        {
            listProp.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static List<GameObject> LoadModels(string[] names)
        {
            List<GameObject> list = new List<GameObject>();
            foreach (string name in names)
            {
                GameObject go = LoadModel(name);
                if (go != null)
                {
                    list.Add(go);
                }
            }

            return list;
        }

        private static GameObject LoadModel(string name)
        {
            string path = $"{ModelsDir}/{name}.fbx";
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogWarning($"PopulateWallSockets: model not found at {path}; skipped.");
            }

            return go;
        }
    }
}
