using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Headless validation of the pit-aware maze generation. Drives the
    /// Labyrinth scene's ArcadeGen3D through GenerateWithRules (which runs
    /// synchronously in edit mode) across many seeds and, with an independent
    /// breadth-first oracle, asserts the invariant that matters: a pit-free
    /// walking route from start to exit always exists. AuditGridAlignment also
    /// asserts every room stays on the grid at a single height - the check that
    /// would have caught the old demote-a-pit float bug.
    ///
    /// Pits and procedural buildings are reserved before the carve, then
    /// materialized/opened after it. The validation requests both and forces the
    /// indoor-exit roll so the independent BFS also covers enterable buildings.
    ///
    /// Run closed-editor:
    ///   Unity.exe -batchmode -quit -projectPath [project] -executeMethod
    ///   Sol.Minigames.EditorTools.LabyrinthPitVerification.Run
    /// </summary>
    public static class LabyrinthPitVerification
    {
        private const string LabyrinthScenePath = "Assets/0_Jd/Scenes/Sc_LabyrinthCrawler.unity";
        private const string PitVoidPrefabPath =
            "Assets/0_Jd/Minigames/LabyrinthCrawler/DungeonRooms/PitVoid.prefab";
        private const int SeedCount = 40;
        private const int PitsPerMaze = 3;

        public static void Run()
        {
            EditorSceneManager.OpenScene(LabyrinthScenePath, OpenSceneMode.Single);

            ArcadeGen3D generator = Object.FindFirstObjectByType<ArcadeGen3D>(FindObjectsInactive.Include);
            LabyrinthCrawlerGame game = Object.FindFirstObjectByType<LabyrinthCrawlerGame>(FindObjectsInactive.Include);
            if (generator == null || game == null)
            {
                Debug.LogError("LabyrinthPitVerification: the scene needs both LabyrinthCrawlerGame and its maze generator.");
                return;
            }

            GameObject pitVoidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PitVoidPrefabPath);
            if (pitVoidPrefab == null)
            {
                Debug.LogWarning(
                    $"LabyrinthPitVerification: {PitVoidPrefabPath} not found. Run " +
                    "Sol/Labyrinth/Build Pit Void Prefab first; verifying solvability/alignment with no pits.");
            }

            int failures = 0;
            int totalPits = 0;
            int mazesWithPits = 0;
            int mazesWithBuildings = 0;
            int indoorExits = 0;

            for (int seed = 1; seed <= SeedCount; seed++)
            {
                Random.InitState(seed * 7919);

                ArcadeMazeRules rules = new ArcadeMazeRules
                {
                    overrideRoomPrefabs = false, // use the scene generator's room list
                    numX = 6,
                    numZ = 6,
                    braidRate = 0.35f,
                    pitCount = pitVoidPrefab != null ? PitsPerMaze : 0,
                    pitVoidPrefab = pitVoidPrefab,
                    proceduralBuildingCount = 2,
                    buildingMinSize = 1,
                    buildingMaxSize = 2,
                    buildingHeightLimit = 3,
                    buildingEntranceCount = 1,
                    buildingExitChance = 1f,
                    organicFootprint = true,
                    footprintFill = 0.7f,
                    respawnPlayerAtStart = false,
                    activateEndRoomExit = false
                };

                if (!generator.GenerateWithRules(game, rules))
                {
                    Debug.LogError($"LabyrinthPitVerification: seed {seed} failed to generate.");
                    failures++;
                    continue;
                }

                Room3D[,] rooms = generator.Rooms;
                int pits = CountPits(rooms);
                totalPits += pits;
                if (pits > 0)
                {
                    mazesWithPits++;
                }

                int buildingCells = CountBuildingCells(rooms);
                if (buildingCells > 0)
                {
                    mazesWithBuildings++;
                    Room3D endRoom = rooms[generator.EndRoomIndex.x, generator.EndRoomIndex.y];
                    if (endRoom != null && endRoom.IsSolidBlock)
                    {
                        indoorExits++;
                    }
                    else
                    {
                        Debug.LogError(
                            $"LabyrinthPitVerification: seed {seed} placed {buildingCells} building cells " +
                            "but the forced building exit remained outdoors.");
                        failures++;
                    }
                }

                if (!PitFreePathExists(rooms, generator.StartRoomIndex, generator.EndRoomIndex))
                {
                    Debug.LogError($"LabyrinthPitVerification: seed {seed} has NO pit-free route (pits={pits}). INVARIANT BROKEN.");
                    failures++;
                }

                failures += AuditGridAlignment(rooms, generator, seed);
            }

            Debug.Log(
                $"LabyrinthPitVerification: {SeedCount} seeds, {mazesWithPits} had pits, " +
                $"{mazesWithBuildings} had buildings, {indoorExits} had indoor exits, " +
                $"avg {(float)totalPits / SeedCount:0.0} pits/maze, failures={failures}.");

            if (failures == 0)
            {
                Debug.Log(
                    "LabyrinthPitVerification: PASS - every maze is solvable on foot around its pits " +
                    "and every successfully placed building accepted the forced indoor exit.");
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // Every room must sit on a regular grid: spacing RoomWidth in x,
        // RoomLength in z, and IDENTICAL y for all cells. A room that lands
        // off-grid or at a wrong height (the old float bug) shows up here as a
        // non-zero offset, independent of camera or perspective.
        private static int AuditGridAlignment(Room3D[,] rooms, ArcadeGen3D generator, int seed)
        {
            int width = rooms.GetLength(0);
            int depth = rooms.GetLength(1);
            float stepX = generator.RoomWidth;
            float stepZ = generator.RoomLength;

            // With an organic footprint many cells are masked out (null), so
            // anchor on the FIRST present room and back out the grid origin from
            // its index; every other present cell's local position is predicted
            // from that. A room that floats off-grid shows up as a delta.
            Room3D anchor = null;
            foreach (Room3D room in rooms)
            {
                if (room != null)
                {
                    anchor = room;
                    break;
                }
            }

            if (anchor == null)
            {
                return 0;
            }

            Vector3 origin = anchor.transform.localPosition -
                new Vector3(anchor.Index.x * stepX, 0f, anchor.Index.z * stepZ);
            int problems = 0;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null)
                    {
                        continue;
                    }

                    Vector3 expected = origin + new Vector3(x * stepX, 0f, z * stepZ);
                    Vector3 actual = room.transform.localPosition;
                    Vector3 delta = actual - expected;

                    if (delta.sqrMagnitude > 0.0004f) // >2cm off in any axis
                    {
                        Debug.LogError(
                            $"LabyrinthPitVerification: seed {seed} room ({x},{z}) MISALIGNED " +
                            $"by ({delta.x:0.###},{delta.y:0.###},{delta.z:0.###}) " +
                            $"isPit={room.IsPit} name={room.name}.");
                        problems++;
                    }
                }
            }

            return problems;
        }

        private static int CountPits(Room3D[,] rooms)
        {
            int count = 0;
            foreach (Room3D room in rooms)
            {
                if (room != null && room.IsPit)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountBuildingCells(Room3D[,] rooms)
        {
            int count = 0;
            foreach (Room3D room in rooms)
            {
                if (room != null && room.IsSolidBlock)
                {
                    count++;
                }
            }

            return count;
        }

        // Independent BFS oracle: walk only through open doorways and never
        // step onto a pit cell (the exit is allowed even if it were a pit).
        private static bool PitFreePathExists(Room3D[,] rooms, Vector2Int start, Vector2Int end)
        {
            int width = rooms.GetLength(0);
            int depth = rooms.GetLength(1);
            bool[,] seen = new bool[width, depth];
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();

            seen[start.x, start.y] = true;
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                Vector2Int c = frontier.Dequeue();
                if (c == end)
                {
                    return true;
                }

                TryStep(rooms, seen, frontier, c, Room3D.Directions.NORTH, c.x, c.y + 1, end);
                TryStep(rooms, seen, frontier, c, Room3D.Directions.SOUTH, c.x, c.y - 1, end);
                TryStep(rooms, seen, frontier, c, Room3D.Directions.EAST, c.x + 1, c.y, end);
                TryStep(rooms, seen, frontier, c, Room3D.Directions.WEST, c.x - 1, c.y, end);
            }

            return false;
        }

        private static void TryStep(
            Room3D[,] rooms,
            bool[,] seen,
            Queue<Vector2Int> frontier,
            Vector2Int from,
            Room3D.Directions dir,
            int nx,
            int nz,
            Vector2Int end)
        {
            int width = rooms.GetLength(0);
            int depth = rooms.GetLength(1);
            if (nx < 0 || nz < 0 || nx >= width || nz >= depth || seen[nx, nz])
            {
                return;
            }

            Room3D current = rooms[from.x, from.y];
            if (current == null || current.IsWallClosed(dir))
            {
                return;
            }

            Room3D neighbor = rooms[nx, nz];
            if (neighbor != null && neighbor.IsPit && new Vector2Int(nx, nz) != end)
            {
                return;
            }

            seen[nx, nz] = true;
            frontier.Enqueue(new Vector2Int(nx, nz));
        }
    }
}
