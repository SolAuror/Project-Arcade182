using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Regression guard for the shared maze generator. Generates the Hub maze
    /// (activeRules == null lane) under a fixed RNG seed and writes a
    /// deterministic wall-open signature to a text file. Run it before and
    /// after any ArcadeGen3D change: the signatures MUST match, proving the
    /// Labyrinth-only braid/pit passes never perturbed the Hub's generation.
    ///
    /// The proof only holds if the new passes consume zero UnityEngine.Random
    /// calls when their rate is 0 - otherwise the Hub's random stream shifts
    /// and every downstream Random.Range diverges.
    ///
    /// Run closed-editor:
    ///   Unity.exe -batchmode -quit -projectPath [project] -executeMethod
    ///   Sol.Minigames.EditorTools.MazeSeedRegression.Capture
    /// </summary>
    public static class MazeSeedRegression
    {
        private const string HubScenePath = "Assets/Shared/Scenes/Sc_ArcadeHub.unity";
        private const string OutputPath = "Assets/../MazeSeedSignature.txt";
        private const int Seed = 1337;

        public static void Capture()
        {
            Scene scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);

            ArcadeGen3D generator = Object.FindFirstObjectByType<ArcadeGen3D>(FindObjectsInactive.Include);
            if (generator == null)
            {
                Debug.LogError("MazeSeedRegression: no ArcadeGen3D in the hub scene.");
                return;
            }

            // Fixed seed makes the DFS carve, special-room picks and weighted
            // prefab selection all reproducible.
            Random.InitState(Seed);
            generator.RegenerateMazeFromInspector();

            string signature = BuildSignature(generator);
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(OutputPath), signature);
            Debug.Log($"MazeSeedRegression: wrote signature ({signature.Length} chars) to {OutputPath}.");

            // Never leave the regenerated Hub scene dirtied on disk.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static string BuildSignature(ArcadeGen3D generator)
        {
            Room3D[,] rooms = generator.Rooms;
            if (rooms == null)
            {
                return "NULL_GRID";
            }

            int width = rooms.GetLength(0);
            int depth = rooms.GetLength(1);

            StringBuilder builder = new StringBuilder();
            builder.Append($"size={width}x{depth} ");
            builder.Append($"start={generator.StartRoomIndex} end={generator.EndRoomIndex}\n");

            for (int z = depth - 1; z >= 0; z--)
            {
                for (int x = 0; x < width; x++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null)
                    {
                        builder.Append("....");
                        continue;
                    }

                    // One glyph per side: uppercase = closed, dot = open.
                    builder.Append(room.IsWallClosed(Room3D.Directions.NORTH) ? 'N' : '.');
                    builder.Append(room.IsWallClosed(Room3D.Directions.SOUTH) ? 'S' : '.');
                    builder.Append(room.IsWallClosed(Room3D.Directions.EAST) ? 'E' : '.');
                    builder.Append(room.IsWallClosed(Room3D.Directions.WEST) ? 'W' : '.');
                    builder.Append(' ');
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
