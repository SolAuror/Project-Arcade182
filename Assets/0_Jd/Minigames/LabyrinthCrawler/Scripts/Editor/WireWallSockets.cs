using UnityEditor;
using UnityEngine;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// One-shot wiring for the lost-city wall reskin: adds a <see cref="WallSocket"/>
    /// to each of the four wall objects on the Labyrinth room prefabs and points its
    /// Default Solid at that wall's existing mesh child (the grey cube). The variant
    /// lists are left empty on purpose, so the rooms look exactly as before until art
    /// is dragged in - this only pre-builds the slots.
    ///
    /// Idempotent: re-running skips walls that already have a socket with a Default
    /// Solid, so it is safe to run again after adding more room prefabs to the list.
    /// Run it from the Unity menu: Sol > Labyrinth > Wire Wall Sockets.
    /// </summary>
    public static class WireWallSockets
    {
        private static readonly string[] RoomPrefabPaths =
        {
            "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/DungeonCell.prefab",
            "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/DungeonCellLit.prefab",
            "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/DungeonSpawn.prefab",
            "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/DungeonExit.prefab",
            "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/UpperCell.prefab",
            "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/UpperCell_Half.prefab",
        };

        [MenuItem("Sol/Labyrinth/Wire Wall Sockets")]
        public static void Wire()
        {
            int prefabsChanged = 0;
            int socketsWired = 0;

            foreach (string path in RoomPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    continue; // optional prefab (e.g. UpperCell) not authored yet
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogWarning($"WireWallSockets: could not load prefab at {path}; skipped.");
                    continue;
                }

                try
                {
                    Room3D room = root.GetComponent<Room3D>();
                    if (room == null)
                    {
                        Debug.LogWarning($"WireWallSockets: {path} has no Room3D component; skipped.");
                        continue;
                    }

                    SerializedObject roomSo = new SerializedObject(room);
                    int wired = 0;
                    wired += WireWall(roomSo.FindProperty("NorthWall"));
                    wired += WireWall(roomSo.FindProperty("SouthWall"));
                    wired += WireWall(roomSo.FindProperty("EastWall"));
                    wired += WireWall(roomSo.FindProperty("WestWall"));

                    if (wired > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabsChanged++;
                        socketsWired += wired;
                        Debug.Log($"WireWallSockets: {System.IO.Path.GetFileName(path)} - wired {wired} wall socket(s).");
                    }
                    else
                    {
                        Debug.Log($"WireWallSockets: {System.IO.Path.GetFileName(path)} - already wired, no change.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"WireWallSockets: done. {socketsWired} socket(s) across {prefabsChanged} prefab(s). Fill the variant lists to see parts appear.");
        }

        // Adds/ensures a WallSocket on the referenced wall object and points its
        // Default Solid at the wall's mesh child. Returns 1 if anything changed.
        private static int WireWall(SerializedProperty wallProp)
        {
            if (wallProp == null || !(wallProp.objectReferenceValue is GameObject wall))
            {
                return 0;
            }

            bool changed = false;

            WallSocket socket = wall.GetComponent<WallSocket>();
            if (socket == null)
            {
                socket = wall.AddComponent<WallSocket>();
                changed = true;
            }

            SerializedObject socketSo = new SerializedObject(socket);
            SerializedProperty defaultSolid = socketSo.FindProperty("defaultSolid");
            if (defaultSolid.objectReferenceValue == null)
            {
                MeshRenderer mesh = wall.GetComponentInChildren<MeshRenderer>(true);
                if (mesh != null)
                {
                    defaultSolid.objectReferenceValue = mesh.gameObject;
                    socketSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                else
                {
                    Debug.LogWarning($"WireWallSockets: '{wall.name}' has no MeshRenderer child; Default Solid left empty.");
                }
            }

            return changed ? 1 : 0;
        }
    }
}
