using Sol;
using UnityEditor;
using UnityEngine;

namespace Sol.EditorTools
{
    /// <summary>
    /// Ensures the thematic dungeon rooms carry the pieces maze generation
    /// relies on: the start room needs a nested PlayerSpawn (same setup as the
    /// hub SpawnRoom) so per-stage respawn keeps working. Safe to re-run.
    /// </summary>
    public static class DungeonRoomSetup
    {
        private const string DungeonSpawnPath = "Assets/0_Jd/Prefabs/3DRooms/DungeonRooms/DungeonSpawn.prefab";
        private const string PlayerSpawnPrefabPath = "Assets/0_Jd/Prefabs/PlayerSpawn.prefab";

        [MenuItem("Sol/Setup/Dungeon Rooms")]
        public static void BuildAll()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(DungeonSpawnPath);
            if (contents == null)
            {
                Debug.LogWarning($"DungeonRoomSetup could not load {DungeonSpawnPath}.");
                return;
            }

            if (contents.GetComponentInChildren<PlayerSpawn>(true) == null)
            {
                GameObject spawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSpawnPrefabPath);
                if (spawnPrefab == null)
                {
                    Debug.LogWarning($"DungeonRoomSetup could not load {PlayerSpawnPrefabPath}.");
                    PrefabUtility.UnloadPrefabContents(contents);
                    return;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(spawnPrefab, contents.transform);
                instance.name = "PlayerSpawn";
                instance.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                instance.transform.localRotation = Quaternion.identity;

                PrefabUtility.SaveAsPrefabAsset(contents, DungeonSpawnPath);
            }

            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
            Debug.Log("Dungeon room setup complete.");
        }
    }
}
