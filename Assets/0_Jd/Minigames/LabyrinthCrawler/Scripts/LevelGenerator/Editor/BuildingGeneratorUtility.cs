using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sol.Editor
{
    internal static class BuildingGeneratorUtility
    {
        internal readonly struct Entrance
        {
            public readonly Vector3Int Coordinate;
            public readonly Room3D.Directions Face;

            public Entrance(Vector3Int coordinate, Room3D.Directions face)
            {
                Coordinate = coordinate;
                Face = face;
            }
        }

        internal readonly struct Result
        {
            public readonly int CellCount;
            public readonly IReadOnlyList<Entrance> Entrances;

            public Result(int cellCount, IReadOnlyList<Entrance> entrances)
            {
                CellCount = cellCount;
                Entrances = entrances;
            }
        }

        private static readonly Room3D.Directions[] HorizontalDirections =
        {
            Room3D.Directions.NORTH,
            Room3D.Directions.SOUTH,
            Room3D.Directions.EAST,
            Room3D.Directions.WEST,
        };

        private static readonly Vector2Int[] HorizontalOffsets =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.right,
            Vector2Int.left,
        };

        private enum BuildingArchetype
        {
            Organic,
            LShape,
            TShape,
            TowerHouse,
        }

        private sealed class MassingPlan
        {
            public readonly bool[,] Footprint;
            public readonly int[,] FullHeights;
            public readonly bool[,] HalfTops;
            public readonly List<Vector2Int> Columns;

            public MassingPlan(
                bool[,] footprint,
                int[,] fullHeights,
                bool[,] halfTops,
                List<Vector2Int> columns)
            {
                Footprint = footprint;
                FullHeights = fullHeights;
                HalfTops = halfTops;
                Columns = columns;
            }
        }

        private readonly struct RoofSurface
        {
            public readonly Vector2Int Column;
            public readonly Vector3Int TopCoordinate;
            public readonly float TopHeight;
            public readonly BuildingComponent.CellLayerType TopLayer;

            public RoofSurface(
                Vector2Int column,
                Vector3Int topCoordinate,
                float topHeight,
                BuildingComponent.CellLayerType topLayer)
            {
                Column = column;
                TopCoordinate = topCoordinate;
                TopHeight = topHeight;
                TopLayer = topLayer;
            }
        }

        private readonly struct RoofPlacement
        {
            public readonly BuildingComponent.RoofCellType Type;
            public readonly int YawSteps;

            public RoofPlacement(
                BuildingComponent.RoofCellType type,
                int yawSteps)
            {
                Type = type;
                YawSteps = yawSteps;
            }
        }

        internal static Result Generate(
            BuildingComponent building,
            bool advanceSeed)
        {
            Undo.SetCurrentGroupName("Generate Random Building");
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RegisterFullObjectHierarchyUndo(
                building.gameObject,
                "Generate Random Building");
            Undo.RecordObject(building, "Generate Random Building");

            if (advanceSeed)
            {
                building.AdvanceGenerationSeed();
            }

            foreach (Room3D room in GetAuthorableRooms(building))
            {
                if (room != null)
                {
                    Undo.DestroyObjectImmediate(room.gameObject);
                }
            }
            building.ClearCellRegistrations();

            int width = building.GenerationWidth;
            int length = building.GenerationLength;
            int heightLimit = building.GenerationHeightLimit;
            BuildingPlanUtility.Plan plan = BuildingPlanUtility.Create(
                width,
                length,
                heightLimit,
                building.GenerationEntranceCount,
                building.GenerationHalfLayerChance,
                building.GenerationSeed,
                building.HasRoofPrefab);

            int cellCount = 0;
            foreach (Vector2Int column in plan.Columns)
            {
                for (int y = 0;
                     y < plan.FullHeights[column.x, column.y];
                     y++)
                {
                    if (InstantiateCell(
                            building,
                            new Vector3Int(column.x, y, column.y),
                            BuildingComponent.CellLayerType.Full) != null)
                    {
                        cellCount++;
                    }
                }

                if (plan.HalfTops[column.x, column.y]
                    && InstantiateCell(
                        building,
                        new Vector3Int(
                            column.x,
                            plan.FullHeights[column.x, column.y],
                            column.y),
                        BuildingComponent.CellLayerType.Half) != null)
                {
                    cellCount++;
                }
            }

            building.RefreshStructure();
            SetWallRoles(building);
            List<Entrance> entrances = new List<Entrance>();
            foreach (BuildingPlanUtility.Entrance entrance in plan.Entrances)
            {
                Vector3Int coordinate =
                    new Vector3Int(entrance.Column.x, 0, entrance.Column.y);
                if (entrance.Vertical)
                {
                    building.ApplyVerticalEntranceColumn(
                        coordinate,
                        entrance.Face);
                }
                else
                {
                    MarkEntrance(building, coordinate, entrance.Face);
                }
                entrances.Add(new Entrance(coordinate, entrance.Face));
            }

            foreach (KeyValuePair<
                     Vector2Int,
                     BuildingPlanUtility.RoofPlacement> roof in plan.Roofs)
            {
                building.ApplyRoofType(
                    new Vector3Int(roof.Key.x, 0, roof.Key.y),
                    roof.Value.Type,
                    roof.Value.YawSteps);
            }

            building.SetDressingSeed(building.GenerationSeed);
            building.DressWalls();

            EditorUtility.SetDirty(building);
            foreach (WallSocket socket in
                     building.GetComponentsInChildren<WallSocket>(true))
            {
                EditorUtility.SetDirty(socket);
            }

            Undo.CollapseUndoOperations(undoGroup);
            return new Result(cellCount, entrances);
        }

        internal static GameObject SaveAsPrefab(BuildingComponent building)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Generated Building Prefab",
                building.name + "_Generated",
                "prefab",
                "Choose where to save the generated building prefab.");
            return string.IsNullOrEmpty(path)
                ? null
                : PrefabUtility.SaveAsPrefabAsset(building.gameObject, path);
        }

        private static Room3D InstantiateCell(
            BuildingComponent building,
            Vector3Int coordinate,
            BuildingComponent.CellLayerType layerType)
        {
            GameObject prefab =
                building.CellPrefabForCoordinate(coordinate, layerType);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    prefab,
                    building.transform) as GameObject;
            if (instance == null)
            {
                instance = Object.Instantiate(prefab, building.transform);
            }

            instance.transform.localPosition =
                building.CellLocalPosition(coordinate, layerType);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = prefab.transform.localScale;
            instance.name =
                $"Cell_{coordinate.x}_{coordinate.y}_{coordinate.z}";

            Room3D room = instance.GetComponent<Room3D>();
            if (room == null)
            {
                room = instance.GetComponentInChildren<Room3D>(true);
            }

            if (room == null)
            {
                Object.DestroyImmediate(instance);
                Debug.LogError(
                    $"{building.name}: '{prefab.name}' has no Room3D.",
                    building);
                return null;
            }

            building.RegisterCell(
                coordinate,
                room,
                layerType,
                false);
            Undo.RegisterCreatedObjectUndo(
                instance,
                "Add Generated Building Cell");
            return room;
        }

        private static MassingPlan PlanMassing(
            int width,
            int length,
            int heightLimit,
            float halfLayerChance,
            System.Random rng)
        {
            List<BuildingArchetype> available =
                new List<BuildingArchetype>
                {
                    BuildingArchetype.Organic,
                };
            if (width >= 2 && length >= 2)
            {
                available.Add(BuildingArchetype.LShape);
            }
            if ((width >= 3 && length >= 2)
                || (length >= 3 && width >= 2))
            {
                available.Add(BuildingArchetype.TShape);
            }
            if (heightLimit >= 2)
            {
                available.Add(BuildingArchetype.TowerHouse);
                available.Add(BuildingArchetype.TowerHouse);
            }

            BuildingArchetype archetype =
                available[rng.Next(available.Count)];
            bool[,] footprint = new bool[width, length];
            switch (archetype)
            {
                case BuildingArchetype.LShape:
                    GrowLFootprint(footprint, rng);
                    break;
                case BuildingArchetype.TShape:
                    GrowTFootprint(footprint, rng);
                    break;
                case BuildingArchetype.TowerHouse:
                    GrowOrganicFootprint(footprint, rng, 0.58f, 0.86f);
                    break;
                default:
                    GrowOrganicFootprint(footprint, rng, 0.45f, 0.76f);
                    break;
            }

            List<Vector2Int> columns = CollectColumns(footprint);
            if (columns.Count == 0)
            {
                footprint[0, 0] = true;
                columns.Add(Vector2Int.zero);
            }

            int[,] fullHeights = new int[width, length];
            int baseHeight =
                archetype == BuildingArchetype.TowerHouse
                    ? 1
                    : rng.Next(1, Mathf.Min(2, heightLimit) + 1);
            foreach (Vector2Int column in columns)
            {
                fullHeights[column.x, column.y] = baseHeight;
            }

            if (heightLimit > baseHeight
                && (archetype == BuildingArchetype.TowerHouse
                    || rng.NextDouble() < 0.62))
            {
                RaiseTower(
                    footprint,
                    fullHeights,
                    columns,
                    baseHeight,
                    heightLimit,
                    archetype == BuildingArchetype.TowerHouse,
                    rng);
            }

            bool[,] halfTops = new bool[width, length];
            if (halfLayerChance > 0f
                && rng.NextDouble() < halfLayerChance)
            {
                AddHalfTopCluster(
                    footprint,
                    fullHeights,
                    halfTops,
                    columns,
                    heightLimit,
                    rng);
            }

            return new MassingPlan(
                footprint,
                fullHeights,
                halfTops,
                columns);
        }

        private static void GrowOrganicFootprint(
            bool[,] footprint,
            System.Random rng,
            float minimumFill,
            float maximumFill)
        {
            int width = footprint.GetLength(0);
            int length = footprint.GetLength(1);
            int capacity = width * length;
            int minimum = Mathf.Clamp(
                Mathf.CeilToInt(capacity * minimumFill),
                1,
                capacity);
            int maximum = Mathf.Clamp(
                Mathf.CeilToInt(capacity * maximumFill),
                minimum,
                capacity);
            if (capacity >= 4)
            {
                maximum = Mathf.Min(maximum, capacity - 1);
                minimum = Mathf.Min(minimum, maximum);
            }
            int target = rng.Next(minimum, maximum + 1);
            Vector2Int seed = new Vector2Int(
                Mathf.Clamp(width / 2 + rng.Next(-1, 2), 0, width - 1),
                Mathf.Clamp(length / 2 + rng.Next(-1, 2), 0, length - 1));
            footprint[seed.x, seed.y] = true;

            List<Vector2Int> columns = new List<Vector2Int> { seed };
            while (columns.Count < target)
            {
                List<Vector2Int> frontier = new List<Vector2Int>();
                foreach (Vector2Int column in columns)
                {
                    foreach (Vector2Int offset in CardinalOffsets())
                    {
                        Vector2Int candidate = column + offset;
                        if (!InBounds(footprint, candidate)
                            || footprint[candidate.x, candidate.y]
                            || frontier.Contains(candidate))
                        {
                            continue;
                        }

                        frontier.Add(candidate);
                    }
                }

                if (frontier.Count == 0)
                {
                    break;
                }

                // Candidates touching two existing cells close small gaps and
                // produce architectural wings; candidates touching only one cell
                // extend an arm. Mix both instead of filling a rectangle.
                bool closeGaps = rng.NextDouble() < 0.58;
                frontier.Sort((a, b) =>
                {
                    int aScore = CountOccupiedNeighbours(footprint, a);
                    int bScore = CountOccupiedNeighbours(footprint, b);
                    return closeGaps
                        ? bScore.CompareTo(aScore)
                        : aScore.CompareTo(bScore);
                });
                int pickRange = Mathf.Min(3, frontier.Count);
                Vector2Int picked = frontier[rng.Next(pickRange)];
                footprint[picked.x, picked.y] = true;
                columns.Add(picked);
            }
        }

        private static void GrowLFootprint(
            bool[,] footprint,
            System.Random rng)
        {
            int width = footprint.GetLength(0);
            int length = footprint.GetLength(1);
            int usedWidth = rng.Next(Mathf.Min(2, width), width + 1);
            int usedLength = rng.Next(Mathf.Min(2, length), length + 1);
            int horizontalThickness =
                usedLength >= 4 && rng.NextDouble() < 0.3 ? 2 : 1;
            int verticalThickness =
                usedWidth >= 4 && rng.NextDouble() < 0.3 ? 2 : 1;
            bool flipX = rng.NextDouble() < 0.5;
            bool flipZ = rng.NextDouble() < 0.5;

            for (int localX = 0; localX < usedWidth; localX++)
            {
                for (int localZ = 0; localZ < usedLength; localZ++)
                {
                    if (localX >= verticalThickness
                        && localZ >= horizontalThickness)
                    {
                        continue;
                    }

                    int x = flipX ? usedWidth - 1 - localX : localX;
                    int z = flipZ ? usedLength - 1 - localZ : localZ;
                    footprint[x, z] = true;
                }
            }
        }

        private static void GrowTFootprint(
            bool[,] footprint,
            System.Random rng)
        {
            int width = footprint.GetLength(0);
            int length = footprint.GetLength(1);
            bool vertical =
                width >= 3
                && (length < 3 || rng.NextDouble() < 0.5);

            if (vertical)
            {
                int crossWidth = rng.Next(3, width + 1);
                int crossStart = rng.Next(0, width - crossWidth + 1);
                bool crossAtNorth = rng.NextDouble() < 0.5;
                int crossZ = crossAtNorth ? length - 1 : 0;
                int stemX = rng.Next(
                    crossStart + 1,
                    crossStart + crossWidth - 1);
                int stemLength = rng.Next(2, length + 1);

                for (int x = crossStart; x < crossStart + crossWidth; x++)
                {
                    footprint[x, crossZ] = true;
                }
                for (int step = 0; step < stemLength; step++)
                {
                    int z = crossAtNorth ? crossZ - step : crossZ + step;
                    footprint[stemX, z] = true;
                }
            }
            else
            {
                int crossLength = rng.Next(3, length + 1);
                int crossStart = rng.Next(0, length - crossLength + 1);
                bool crossAtEast = rng.NextDouble() < 0.5;
                int crossX = crossAtEast ? width - 1 : 0;
                int stemZ = rng.Next(
                    crossStart + 1,
                    crossStart + crossLength - 1);
                int stemLength = rng.Next(2, width + 1);

                for (int z = crossStart; z < crossStart + crossLength; z++)
                {
                    footprint[crossX, z] = true;
                }
                for (int step = 0; step < stemLength; step++)
                {
                    int x = crossAtEast ? crossX - step : crossX + step;
                    footprint[x, stemZ] = true;
                }
            }
        }

        private static void RaiseTower(
            bool[,] footprint,
            int[,] heights,
            List<Vector2Int> columns,
            int baseHeight,
            int heightLimit,
            bool substantial,
            System.Random rng)
        {
            List<Vector2Int> candidates = new List<Vector2Int>(columns);
            Shuffle(candidates, rng);
            candidates.Sort((a, b) =>
                CountOccupiedNeighbours(footprint, b)
                    .CompareTo(CountOccupiedNeighbours(footprint, a)));
            Vector2Int tower = candidates[0];
            int towerHeight = substantial
                ? rng.Next(baseHeight + 1, heightLimit + 1)
                : baseHeight + 1;
            heights[tower.x, tower.y] = towerHeight;

            int clusterLimit =
                substantial && columns.Count >= 6
                    ? rng.Next(1, Mathf.Min(4, columns.Count) + 1)
                    : 1;
            List<Vector2Int> frontier = new List<Vector2Int> { tower };
            HashSet<Vector2Int> raised =
                new HashSet<Vector2Int> { tower };
            while (raised.Count < clusterLimit && frontier.Count > 0)
            {
                Vector2Int source = frontier[rng.Next(frontier.Count)];
                List<Vector2Int> neighbours =
                    OccupiedNeighbours(footprint, source);
                Shuffle(neighbours, rng);
                bool extended = false;
                foreach (Vector2Int neighbour in neighbours)
                {
                    if (!raised.Add(neighbour))
                    {
                        continue;
                    }

                    heights[neighbour.x, neighbour.y] =
                        rng.NextDouble() < 0.7
                            ? towerHeight
                            : Mathf.Max(baseHeight + 1, towerHeight - 1);
                    frontier.Add(neighbour);
                    extended = true;
                    break;
                }

                if (!extended)
                {
                    frontier.Remove(source);
                }
            }
        }

        private static void AddHalfTopCluster(
            bool[,] footprint,
            int[,] heights,
            bool[,] halfTops,
            List<Vector2Int> columns,
            int heightLimit,
            System.Random rng)
        {
            List<Vector2Int> eligible = columns.FindAll(column =>
                heights[column.x, column.y] < heightLimit);
            if (eligible.Count == 0)
            {
                return;
            }

            Vector2Int seed = eligible[rng.Next(eligible.Count)];
            halfTops[seed.x, seed.y] = true;
            if (eligible.Count < 5 || rng.NextDouble() >= 0.48)
            {
                return;
            }

            List<Vector2Int> neighbours =
                OccupiedNeighbours(footprint, seed);
            Shuffle(neighbours, rng);
            foreach (Vector2Int neighbour in neighbours)
            {
                if (heights[neighbour.x, neighbour.y]
                        != heights[seed.x, seed.y]
                    || heights[neighbour.x, neighbour.y] >= heightLimit)
                {
                    continue;
                }

                halfTops[neighbour.x, neighbour.y] = true;
                break;
            }
        }

        private static List<Vector2Int> CollectColumns(bool[,] footprint)
        {
            List<Vector2Int> columns = new List<Vector2Int>();
            for (int x = 0; x < footprint.GetLength(0); x++)
            {
                for (int z = 0; z < footprint.GetLength(1); z++)
                {
                    if (footprint[x, z])
                    {
                        columns.Add(new Vector2Int(x, z));
                    }
                }
            }
            return columns;
        }

        private static int CountOccupiedNeighbours(
            bool[,] footprint,
            Vector2Int column)
        {
            return OccupiedNeighbours(footprint, column).Count;
        }

        private static List<Vector2Int> OccupiedNeighbours(
            bool[,] footprint,
            Vector2Int column)
        {
            List<Vector2Int> neighbours = new List<Vector2Int>();
            foreach (Vector2Int offset in CardinalOffsets())
            {
                Vector2Int neighbour = column + offset;
                if (InBounds(footprint, neighbour)
                    && footprint[neighbour.x, neighbour.y])
                {
                    neighbours.Add(neighbour);
                }
            }
            return neighbours;
        }

        private static Vector2Int[] CardinalOffsets()
        {
            return HorizontalOffsets;
        }

        private static bool InBounds(
            bool[,] footprint,
            Vector2Int column)
        {
            return column.x >= 0
                && column.y >= 0
                && column.x < footprint.GetLength(0)
                && column.y < footprint.GetLength(1);
        }

        private static void SetWallRoles(BuildingComponent building)
        {
            foreach (BuildingComponent.AuthoredCell cell in
                     building.AuthoredCells)
            {
                if (cell == null || cell.Room == null)
                {
                    continue;
                }

                foreach (Room3D.Directions direction in HorizontalDirections)
                {
                    if (building.TryGetWallSocket(
                            cell.Coordinate,
                            direction,
                            out WallSocket socket))
                    {
                        socket.SetAuthoredType(
                            WallSocket.AuthoredWallType.Solid);
                    }
                }
            }

            building.OpenAllSharedEdges(false);
        }

        private static List<Entrance> PlaceEntrances(
            BuildingComponent building,
            MassingPlan plan,
            System.Random rng)
        {
            List<Entrance> candidates = new List<Entrance>();
            foreach (Vector2Int column in plan.Columns)
            {
                foreach (Room3D.Directions direction in HorizontalDirections)
                {
                    Vector2Int neighbour =
                        column + DirectionOffset2D(direction);
                    if (InBounds(plan.Footprint, neighbour)
                        && plan.Footprint[neighbour.x, neighbour.y])
                    {
                        continue;
                    }

                    candidates.Add(new Entrance(
                        new Vector3Int(column.x, 0, column.y),
                        direction));
                }
            }

            Shuffle(candidates, rng);
            int count = Mathf.Min(
                building.GenerationEntranceCount,
                candidates.Count);
            List<Entrance> placed = candidates.GetRange(0, count);

            foreach (Entrance entrance in placed)
            {
                List<BuildingComponent.AuthoredCell> column =
                    ColumnCells(building, entrance.Coordinate);
                bool makeVertical =
                    column.Count >= 2
                    && column[column.Count - 1].LayerType
                        == BuildingComponent.CellLayerType.Full
                    && rng.NextDouble() < 0.35;

                if (makeVertical)
                {
                    foreach (BuildingComponent.AuthoredCell cell in column)
                    {
                        MarkEntrance(
                            building,
                            cell.Coordinate,
                            entrance.Face);
                    }
                }
                else
                {
                    MarkEntrance(
                        building,
                        entrance.Coordinate,
                        entrance.Face);
                }
            }

            return placed;
        }

        private static void PlaceRoofs(
            BuildingComponent building,
            MassingPlan plan,
            System.Random rng)
        {
            Dictionary<Vector2Int, RoofSurface> surfaces =
                CollectRoofSurfaces(building, plan);
            Dictionary<Vector2Int, RoofPlacement> placements =
                new Dictionary<Vector2Int, RoofPlacement>();
            HashSet<Vector2Int> flatSupports =
                new HashSet<Vector2Int>();

            List<Vector2Int> ordered = new List<Vector2Int>(plan.Columns);
            Shuffle(ordered, rng);
            PlanMatchedRoofPairs(
                building,
                surfaces,
                placements,
                ordered,
                rng);

            // Any still-unpaired roof below a taller neighbour may become a
            // lean-to. Its high vertical edge meets an unclaimed full-height wall
            // rather than another directional roof or a floating gable.
            foreach (Vector2Int column in ordered)
            {
                if (!surfaces.TryGetValue(column, out RoofSurface surface)
                    || placements.ContainsKey(column)
                    || HasAdjacentDirectionalRoof(placements, column)
                    || !TryFindTallerNeighbour(
                        surfaces,
                        placements,
                        surface,
                        rng,
                        out Room3D.Directions tallerDirection,
                        out Vector2Int supportColumn))
                {
                    continue;
                }

                if (!TryPickLeanRoof(
                        building,
                        rng,
                        out BuildingComponent.RoofCellType type,
                        out bool useLeft))
                {
                    continue;
                }
                placements[column] = new RoofPlacement(
                    type,
                    YawForApex(useLeft, tallerDirection));
                flatSupports.Add(supportColumn);
            }

            // The uphill side of a directional slope must terminate against a
            // complete flat cap. Pair planning ran first, so this never breaks an
            // existing L/R match to manufacture a support.
            foreach (Vector2Int support in flatSupports)
            {
                placements[support] = new RoofPlacement(
                    BuildingComponent.RoofCellType.Block,
                    rng.Next(4));
            }

            // Tips, tower tops, and any unpaired junction get a complete
            // self-contained cap. Sloped and stepped caps make single rises read
            // as deliberate tower roofs rather than arbitrary flat tiles.
            foreach (Vector2Int column in plan.Columns)
            {
                if (!surfaces.TryGetValue(column, out RoofSurface surface))
                {
                    continue;
                }

                if (!placements.TryGetValue(column, out RoofPlacement placement))
                {
                    BuildingComponent.RoofCellType type =
                        PickStandaloneRoof(building, surface.TopLayer, rng);
                    placement = new RoofPlacement(type, rng.Next(4));
                }

                building.ApplyRoofType(
                    new Vector3Int(column.x, 0, column.y),
                    placement.Type,
                    placement.YawSteps);
            }
        }

        private static bool HasAdjacentDirectionalRoof(
            Dictionary<Vector2Int, RoofPlacement> placements,
            Vector2Int column)
        {
            foreach (Vector2Int offset in CardinalOffsets())
            {
                if (placements.TryGetValue(
                        column + offset,
                        out RoofPlacement placement)
                    && IsDirectionalRoof(placement.Type))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsDirectionalRoof(
            BuildingComponent.RoofCellType type)
        {
            return type == BuildingComponent.RoofCellType.SlopeLeft
                || type == BuildingComponent.RoofCellType.SlopeRight
                || type == BuildingComponent.RoofCellType.SlopeLeftCurve
                || type == BuildingComponent.RoofCellType.SlopeRightCurve;
        }

        private static void PlanMatchedRoofPairs(
            BuildingComponent building,
            Dictionary<Vector2Int, RoofSurface> surfaces,
            Dictionary<Vector2Int, RoofPlacement> placements,
            List<Vector2Int> ordered,
            System.Random rng)
        {
            // Pair equal-height neighbours before planning lean-tos. This applies
            // equally to full and half-height hosts: both pieces stay on top of
            // their cells, share one straight/curved roll, and face complementary
            // vertical edges into the same ridge.
            ordered.Sort((a, b) =>
                CountSameHeightNeighbours(surfaces, a)
                    .CompareTo(CountSameHeightNeighbours(surfaces, b)));
            foreach (Vector2Int column in ordered)
            {
                if (!surfaces.TryGetValue(column, out RoofSurface surface)
                    || placements.ContainsKey(column))
                {
                    continue;
                }

                List<Vector2Int> partners =
                    SameHeightNeighbours(surfaces, surface);
                partners.RemoveAll(placements.ContainsKey);
                if (partners.Count == 0)
                {
                    continue;
                }

                Shuffle(partners, rng);
                partners.Sort((a, b) =>
                    CountSameHeightNeighbours(surfaces, a)
                        .CompareTo(CountSameHeightNeighbours(surfaces, b)));
                Vector2Int partner = partners[0];
                Room3D.Directions towardPartner =
                    DirectionBetween(column, partner);
                bool canCurve =
                    building.HasRoofPrefab(
                        BuildingComponent.RoofCellType.SlopeLeftCurve)
                    && building.HasRoofPrefab(
                        BuildingComponent.RoofCellType.SlopeRightCurve);
                bool canStraight =
                    building.HasRoofPrefab(
                        BuildingComponent.RoofCellType.SlopeLeft)
                    && building.HasRoofPrefab(
                        BuildingComponent.RoofCellType.SlopeRight);
                if (!canCurve && !canStraight)
                {
                    return;
                }

                bool curved =
                    canCurve
                    && (!canStraight || rng.NextDouble() < 0.42);
                BuildingComponent.RoofCellType left =
                    curved
                        ? BuildingComponent.RoofCellType.SlopeLeftCurve
                        : BuildingComponent.RoofCellType.SlopeLeft;
                BuildingComponent.RoofCellType right =
                    curved
                        ? BuildingComponent.RoofCellType.SlopeRightCurve
                        : BuildingComponent.RoofCellType.SlopeRight;
                placements[column] = new RoofPlacement(
                    left,
                    YawForApex(true, towardPartner));
                placements[partner] = new RoofPlacement(
                    right,
                    YawForApex(false, Opposite(towardPartner)));
            }
        }

        private static bool TryPickLeanRoof(
            BuildingComponent building,
            System.Random rng,
            out BuildingComponent.RoofCellType type,
            out bool useLeft)
        {
            List<BuildingComponent.RoofCellType> available =
                new List<BuildingComponent.RoofCellType>();
            BuildingComponent.RoofCellType[] candidates =
            {
                BuildingComponent.RoofCellType.SlopeLeft,
                BuildingComponent.RoofCellType.SlopeRight,
                BuildingComponent.RoofCellType.SlopeLeftCurve,
                BuildingComponent.RoofCellType.SlopeRightCurve,
            };
            foreach (BuildingComponent.RoofCellType candidate in candidates)
            {
                if (building.HasRoofPrefab(candidate))
                {
                    available.Add(candidate);
                }
            }

            if (available.Count == 0)
            {
                type = BuildingComponent.RoofCellType.None;
                useLeft = true;
                return false;
            }

            bool preferCurve = rng.NextDouble() < 0.42;
            List<BuildingComponent.RoofCellType> preferred =
                available.FindAll(candidate =>
                    preferCurve
                        ? candidate
                            == BuildingComponent.RoofCellType.SlopeLeftCurve
                            || candidate
                            == BuildingComponent.RoofCellType.SlopeRightCurve
                        : candidate
                            == BuildingComponent.RoofCellType.SlopeLeft
                            || candidate
                            == BuildingComponent.RoofCellType.SlopeRight);
            List<BuildingComponent.RoofCellType> pool =
                preferred.Count > 0 ? preferred : available;
            type = pool[rng.Next(pool.Count)];
            useLeft =
                type == BuildingComponent.RoofCellType.SlopeLeft
                || type == BuildingComponent.RoofCellType.SlopeLeftCurve;
            return true;
        }

        private static Dictionary<Vector2Int, RoofSurface>
            CollectRoofSurfaces(
                BuildingComponent building,
                MassingPlan plan)
        {
            Dictionary<Vector2Int, RoofSurface> surfaces =
                new Dictionary<Vector2Int, RoofSurface>();
            foreach (Vector2Int column in plan.Columns)
            {
                Vector3Int coordinate =
                    new Vector3Int(column.x, 0, column.y);
                if (!building.TryGetTopCoordinate(
                        coordinate,
                        out Vector3Int topCoordinate)
                    || !building.TryGetCell(topCoordinate, out Room3D room)
                    || room == null
                    || !building.TryGetCellLayer(
                        topCoordinate,
                        out BuildingComponent.CellLayerType topLayer))
                {
                    continue;
                }

                float topHeight =
                    room.transform.localPosition.y
                    + building.GetCellHeight(topLayer);
                surfaces[column] = new RoofSurface(
                    column,
                    topCoordinate,
                    topHeight,
                    topLayer);
            }
            return surfaces;
        }

        private static bool TryFindTallerNeighbour(
            Dictionary<Vector2Int, RoofSurface> surfaces,
            Dictionary<Vector2Int, RoofPlacement> placements,
            RoofSurface surface,
            System.Random rng,
            out Room3D.Directions direction,
            out Vector2Int supportColumn)
        {
            List<Room3D.Directions> taller =
                new List<Room3D.Directions>();
            float tallestHeight = surface.TopHeight + 0.01f;
            foreach (Room3D.Directions candidate in HorizontalDirections)
            {
                Vector2Int neighbour =
                    surface.Column + DirectionOffset2D(candidate);
                if (!surfaces.TryGetValue(
                        neighbour,
                        out RoofSurface neighbourSurface)
                    || placements.ContainsKey(neighbour)
                    || neighbourSurface.TopLayer
                        != BuildingComponent.CellLayerType.Full)
                {
                    continue;
                }

                if (neighbourSurface.TopHeight > tallestHeight + 0.01f)
                {
                    taller.Clear();
                    tallestHeight = neighbourSurface.TopHeight;
                }
                if (Mathf.Abs(
                        neighbourSurface.TopHeight - tallestHeight) < 0.01f)
                {
                    taller.Add(candidate);
                }
            }

            if (taller.Count > 0)
            {
                direction = taller[rng.Next(taller.Count)];
                supportColumn =
                    surface.Column + DirectionOffset2D(direction);
                return true;
            }

            direction = Room3D.Directions.NONE;
            supportColumn = surface.Column;
            return false;
        }

        private static List<Vector2Int> SameHeightNeighbours(
            Dictionary<Vector2Int, RoofSurface> surfaces,
            RoofSurface surface)
        {
            List<Vector2Int> neighbours = new List<Vector2Int>();
            foreach (Vector2Int offset in CardinalOffsets())
            {
                Vector2Int neighbour = surface.Column + offset;
                if (surfaces.TryGetValue(
                        neighbour,
                        out RoofSurface neighbourSurface)
                    && Mathf.Abs(
                        neighbourSurface.TopHeight - surface.TopHeight) < 0.01f)
                {
                    neighbours.Add(neighbour);
                }
            }
            return neighbours;
        }

        private static int CountSameHeightNeighbours(
            Dictionary<Vector2Int, RoofSurface> surfaces,
            Vector2Int column)
        {
            return surfaces.TryGetValue(column, out RoofSurface surface)
                ? SameHeightNeighbours(surfaces, surface).Count
                : 0;
        }

        private static BuildingComponent.RoofCellType PickStandaloneRoof(
            BuildingComponent building,
            BuildingComponent.CellLayerType topLayer,
            System.Random rng)
        {
            bool hasSloped = building.HasRoofPrefab(
                BuildingComponent.RoofCellType.Sloped);
            bool hasStepped = building.HasRoofPrefab(
                BuildingComponent.RoofCellType.Stepped);
            if (hasSloped && hasStepped)
            {
                return rng.NextDouble() < 0.5
                    ? BuildingComponent.RoofCellType.Sloped
                    : BuildingComponent.RoofCellType.Stepped;
            }
            if (hasSloped || hasStepped)
            {
                return hasSloped
                    ? BuildingComponent.RoofCellType.Sloped
                    : BuildingComponent.RoofCellType.Stepped;
            }

            return topLayer == BuildingComponent.CellLayerType.Half
                ? BuildingComponent.RoofCellType.HalfBlock
                : BuildingComponent.RoofCellType.Block;
        }

        private static int YawForApex(
            bool isLeft,
            Room3D.Directions targetDirection)
        {
            int authored =
                isLeft
                    ? DirectionIndex(Room3D.Directions.NORTH)
                    : DirectionIndex(Room3D.Directions.SOUTH);
            return (DirectionIndex(targetDirection) - authored + 4) & 3;
        }

        private static int DirectionIndex(Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.EAST: return 1;
                case Room3D.Directions.SOUTH: return 2;
                case Room3D.Directions.WEST: return 3;
                default: return 0;
            }
        }

        private static Room3D.Directions DirectionBetween(
            Vector2Int from,
            Vector2Int to)
        {
            Vector2Int difference = to - from;
            if (difference == Vector2Int.up)
            {
                return Room3D.Directions.NORTH;
            }
            if (difference == Vector2Int.down)
            {
                return Room3D.Directions.SOUTH;
            }
            if (difference == Vector2Int.right)
            {
                return Room3D.Directions.EAST;
            }
            return Room3D.Directions.WEST;
        }

        private static Vector2Int DirectionOffset2D(
            Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH: return Vector2Int.up;
                case Room3D.Directions.SOUTH: return Vector2Int.down;
                case Room3D.Directions.EAST: return Vector2Int.right;
                case Room3D.Directions.WEST: return Vector2Int.left;
                default: return Vector2Int.zero;
            }
        }

        private static Room3D.Directions Opposite(
            Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH:
                    return Room3D.Directions.SOUTH;
                case Room3D.Directions.SOUTH:
                    return Room3D.Directions.NORTH;
                case Room3D.Directions.EAST:
                    return Room3D.Directions.WEST;
                case Room3D.Directions.WEST:
                    return Room3D.Directions.EAST;
                default:
                    return Room3D.Directions.NONE;
            }
        }

        private static List<BuildingComponent.AuthoredCell> ColumnCells(
            BuildingComponent building,
            Vector3Int coordinate)
        {
            List<BuildingComponent.AuthoredCell> column =
                new List<BuildingComponent.AuthoredCell>();
            foreach (BuildingComponent.AuthoredCell cell in
                     building.AuthoredCells)
            {
                if (cell != null
                    && cell.Room != null
                    && cell.Coordinate.x == coordinate.x
                    && cell.Coordinate.z == coordinate.z)
                {
                    column.Add(cell);
                }
            }
            column.Sort((a, b) =>
                a.Coordinate.y.CompareTo(b.Coordinate.y));
            return column;
        }

        private static void MarkEntrance(
            BuildingComponent building,
            Vector3Int coordinate,
            Room3D.Directions face)
        {
            if (building.TryGetWallSocket(
                    coordinate,
                    face,
                    out WallSocket socket))
            {
                socket.SetAuthoredType(
                    WallSocket.AuthoredWallType.Entrance);
            }
        }

        private static void Shuffle<T>(
            IList<T> items,
            System.Random rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                T temporary = items[i];
                items[i] = items[swap];
                items[swap] = temporary;
            }
        }

        internal static List<Room3D> GetAuthorableRooms(
            BuildingComponent building)
        {
            List<Room3D> rooms = new List<Room3D>();
            foreach (Room3D room in
                     building.GetComponentsInChildren<Room3D>(true))
            {
                if (room == null)
                {
                    continue;
                }

                Room3D parentRoom =
                    room.transform.parent != null
                        ? room.transform.parent.GetComponentInParent<Room3D>()
                        : null;
                if (parentRoom == null)
                {
                    rooms.Add(room);
                }
            }
            return rooms;
        }
    }
}
