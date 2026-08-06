using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Regression guard for the shared maze generator. Generates the authored
    /// Labyrinth prefab under a fixed RNG seed and writes a
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
        private const string GeneratorPrefabPath =
            "Assets/0_Jd/Minigames/LabyrinthCrawler/LabyrinthCrawlerGame.prefab";
        private const string OutputPath = "Assets/../MazeSeedSignature.txt";
        private const int Seed = 1337;

        public static void Capture()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GeneratorPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"MazeSeedRegression: could not load {GeneratorPrefabPath}.");
            }

            try
            {
                ArcadeGen3D generator = root.GetComponentInChildren<ArcadeGen3D>(true);
                LabyrinthCrawlerGame game = root.GetComponent<LabyrinthCrawlerGame>();
                if (generator == null)
                {
                    throw new InvalidOperationException(
                        $"MazeSeedRegression: no ArcadeGen3D in {GeneratorPrefabPath}.");
                }

                if (game == null)
                {
                    throw new InvalidOperationException(
                        $"MazeSeedRegression: no LabyrinthCrawlerGame in {GeneratorPrefabPath}.");
                }

                // Fixed seed makes the DFS carve, special-room picks and weighted
                // prefab selection all reproducible.
                UnityEngine.Random.InitState(Seed);
                ArcadeMazeRules rules = CreateStageOneRules(game);
                if (!generator.GenerateWithRules(game, rules))
                {
                    throw new InvalidOperationException(
                        "MazeSeedRegression: the game-owned stage-one generation request was rejected.");
                }

                string signature = BuildSignature(generator);
                if (signature == "NULL_GRID")
                {
                    throw new InvalidOperationException(
                        "MazeSeedRegression: generation completed without a room grid.");
                }
                File.WriteAllText(Path.GetFullPath(OutputPath), signature);
                Debug.Log($"MazeSeedRegression: wrote signature ({signature.Length} chars) to {OutputPath}.");
            }
            finally
            {
                // The generated hierarchy exists only in the isolated prefab stage.
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ArcadeMazeRules CreateStageOneRules(LabyrinthCrawlerGame game)
        {
            FieldInfo field = typeof(LabyrinthCrawlerGame).GetField(
                "labyrinthMazeRules",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object config = field?.GetValue(game);
            if (config == null)
            {
                throw new InvalidOperationException(
                    "MazeSeedRegression: Labyrinth maze rules are unavailable.");
            }

            Type type = config.GetType();
            int width = ReadIntProperty(config, type, "StartingMazeWidth");
            int depth = ReadIntProperty(config, type, "StartingMazeDepth");
            int stage = 1;
            int pits = InvokeInt(config, type, "GetPitCount", stage);
            int plazas = InvokeInt(config, type, "GetRoomCount", stage);
            int blocks = InvokeInt(config, type, "GetSolidBlockCount", stage);
            int authoredBuildings = InvokeInt(config, type, "GetAuthoredBuildingCount", stage);
            MethodInfo create = type.GetMethod(
                "CreateArcadeRules",
                BindingFlags.Instance | BindingFlags.Public);
            object result = create?.Invoke(
                config,
                new object[] { width, depth, pits, plazas, blocks, authoredBuildings });
            if (result is not ArcadeMazeRules rules)
            {
                throw new InvalidOperationException(
                    "MazeSeedRegression: could not create stage-one ArcadeMazeRules.");
            }

            rules.activateEndRoomExit = false;
            return rules;
        }

        private static int ReadIntProperty(object target, Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(target) is int value)
            {
                return value;
            }

            throw new InvalidOperationException($"MazeSeedRegression: missing rule property {name}.");
        }

        private static int InvokeInt(object target, Type type, string name, int stage)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            if (method?.Invoke(target, new object[] { stage }) is int value)
            {
                return value;
            }

            throw new InvalidOperationException($"MazeSeedRegression: missing rule method {name}.");
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
