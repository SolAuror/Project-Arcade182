using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Lightweight authoring helper for hand-built and generated maze building
    /// prefabs. Its custom Inspector creates socket-configured cells on a local
    /// grid and edits individual facade roles. It drives every child
    /// <see cref="WallSocket"/> through the same styling path as
    /// <see cref="ArcadeGen3D"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingComponent : MonoBehaviour
    {
        private const string SpawnedRoofCellPrefix = "__BuildingRoofCell__";

        private static readonly Room3D.Directions[] WallDirections =
        {
            Room3D.Directions.NORTH,
            Room3D.Directions.SOUTH,
            Room3D.Directions.EAST,
            Room3D.Directions.WEST,
        };

        public enum RoofCellType
        {
            None,
            Sloped,
            Stepped,
            SlopeLeft,
            SlopeRight,
            SlopeLeftCurve,
            SlopeRightCurve,
            Block,
            HalfBlock,
        }

        public enum CellLayerType
        {
            Full,
            Half,
        }

        private sealed class StructuralCornerPiece
        {
            public readonly GameObject CornerObject;
            public readonly Vector3Int CellCoordinate;
            public readonly float BaseHeight;
            public readonly float Height;
            public readonly bool TouchesStructuralWall;

            public StructuralCornerPiece(
                GameObject cornerObject,
                Vector3Int cellCoordinate,
                float baseHeight,
                float height,
                bool touchesStructuralWall)
            {
                CornerObject = cornerObject;
                CellCoordinate = cellCoordinate;
                BaseHeight = baseHeight;
                Height = height;
                TouchesStructuralWall = touchesStructuralWall;
            }
        }

        [Serializable]
        public sealed class AuthoredCell
        {
            [SerializeField] private Vector3Int coordinate;
            [SerializeField] private Room3D room;
            [SerializeField] private CellLayerType layerType;
            [SerializeField] private RoofCellType roofCell;
            [SerializeField, Range(0, 3)] private int roofYawSteps;
            [SerializeField, HideInInspector] private GameObject spawnedRoofCell;

            public Vector3Int Coordinate => coordinate;
            public Room3D Room => room;
            public CellLayerType LayerType => layerType;
            public RoofCellType RoofCell => roofCell;
            public int RoofYawSteps => roofYawSteps;
            public GameObject SpawnedRoofCell
            {
                get => spawnedRoofCell;
                set => spawnedRoofCell = value;
            }

            public AuthoredCell(
                Vector3Int coordinate,
                Room3D room,
                CellLayerType layerType = CellLayerType.Full)
            {
                this.coordinate = coordinate;
                this.room = room;
                this.layerType = layerType;
            }

            public void SetRoof(RoofCellType type, int yawSteps)
            {
                roofCell = type;
                roofYawSteps = ((yawSteps % 4) + 4) % 4;
            }

            public void SetCoordinate(Vector3Int value)
            {
                coordinate = value;
            }
        }

        [Header("Cell Authoring")]
        [Tooltip("The socket-configured maze cell prefab used by the Add Cell buttons.")]
        [SerializeField] private GameObject cellPrefab;

        [Tooltip("Optional socket-configured cell used above or below the ground floor (normally UpperCell). Falls back to Cell Prefab when empty.")]
        [SerializeField] private GameObject upperCellPrefab;

        [Tooltip("Horizontal half-wall cell used for half-height layers (normally UpperCell_Half).")]
        [SerializeField] private GameObject halfCellPrefab;

        [Tooltip("Distance between cell origins on X, between storeys on Y, and on Z. Labyrinth defaults are approximately 8.51, 5.95, 8.51.")]
        [SerializeField] private Vector3 cellSpacing = new Vector3(8.51f, 5.95f, 8.51f);

        [Tooltip("Height contributed by a horizontal half-wall layer.")]
        [SerializeField] private float halfCellHeight = 2.98f;

        [Header("RoofCell Kit")]
        [Tooltip("RoofCell_Sloped prefab.")]
        [SerializeField] private GameObject roofCellSloped;

        [Tooltip("RoofCell_Stepped prefab.")]
        [SerializeField] private GameObject roofCellStepped;

        [Tooltip("RoofCell_L prefab.")]
        [SerializeField] private GameObject roofCellLeft;

        [Tooltip("RoofCell_R prefab.")]
        [SerializeField] private GameObject roofCellRight;

        [Tooltip("RoofCell_L_Curve prefab.")]
        [SerializeField] private GameObject roofCellLeftCurve;

        [Tooltip("RoofCell_R_Curve prefab.")]
        [SerializeField] private GameObject roofCellRightCurve;

        [Tooltip("RoofCell_Block prefab.")]
        [SerializeField] private GameObject roofCellBlock;

        [Tooltip("RoofCell_HalfBlock prefab.")]
        [SerializeField] private GameObject roofCellHalfBlock;

        [SerializeField, HideInInspector] private Vector3 gridOriginLocal;
        [SerializeField, HideInInspector] private List<AuthoredCell> authoredCells = new List<AuthoredCell>();

        [Header("Random Building Generator")]
        [SerializeField, Range(1, 12)] private int generationWidth = 2;
        [SerializeField, Range(1, 12)] private int generationLength = 2;
        [SerializeField, Range(1, 8)] private int generationHeightLimit = 3;
        [SerializeField, Min(0)] private int generationEntranceCount = 1;
        [SerializeField] private int generationSeed = 1;
        [SerializeField, Range(0f, 1f)] private float generationHalfLayerChance = 0.25f;

        [Header("Wall Dressing")]
        [Tooltip("Controls the deterministic wall choices. Change this value, or press Dress / Reroll Walls, to get another variation.")]
        [SerializeField] private int dressingSeed = 1;

        [Tooltip("Dress the building again when it is instantiated. Leave off to keep the variation baked into the prefab by the Inspector button.")]
        [SerializeField] private bool dressOnAwake;

        /// <summary>The seed used for the next deterministic dressing pass.</summary>
        public int DressingSeed => dressingSeed;
        public GameObject CellPrefab => cellPrefab;
        public GameObject UpperCellPrefab => upperCellPrefab;
        public GameObject HalfCellPrefab => halfCellPrefab;
        public Vector3 CellSpacing => cellSpacing;
        public IReadOnlyList<AuthoredCell> AuthoredCells => authoredCells;
        public int AuthoredCellCount => authoredCells != null ? authoredCells.Count : 0;
        public bool HasCompleteRoofCellKit =>
            roofCellSloped != null
            && roofCellStepped != null
            && roofCellLeft != null
            && roofCellRight != null
            && roofCellLeftCurve != null
            && roofCellRightCurve != null
            && roofCellBlock != null
            && roofCellHalfBlock != null;
        public int GenerationWidth => Mathf.Clamp(generationWidth, 1, 12);
        public int GenerationLength => Mathf.Clamp(generationLength, 1, 12);
        public int GenerationHeightLimit =>
            Mathf.Clamp(generationHeightLimit, 1, 8);
        public int GenerationEntranceCount => Mathf.Max(0, generationEntranceCount);
        public int GenerationSeed => generationSeed;
        public float GenerationHalfLayerChance =>
            Mathf.Clamp01(generationHalfLayerChance);
        public int AuthorableWallSocketCount
        {
            get
            {
                int count = 0;
                foreach (WallSocket socket in
                         GetComponentsInChildren<WallSocket>(true))
                {
                    if (socket != null && !IsNestedDecorRoomSocket(socket))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            if (dressOnAwake)
            {
                DressWalls();
                DressRoofs();
            }
        }

        /// <summary>
        /// Applies the current seed to every child WallSocket. Authored openings
        /// receive passage variants; every other socket receives a solid variant.
        /// Returns the number of sockets dressed.
        /// </summary>
        public int DressWalls()
        {
            WallSocket[] sockets = GetComponentsInChildren<WallSocket>(true);
            HashSet<WallSocket> dressed = new HashSet<WallSocket>();
            PruneMissingCells();

            // Registered authored cells know their grid coordinates, which lets
            // vertically aligned entrances dress as one coherent pier stack.
            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell == null || cell.Room == null)
                {
                    continue;
                }

                foreach (Room3D.Directions direction in WallDirections)
                {
                    if (!TryGetWallSocket(cell.Coordinate, direction, out WallSocket socket)
                        || dressed.Contains(socket))
                    {
                        continue;
                    }

                    if (socket.AuthoredType == WallSocket.AuthoredWallType.Entrance
                        && ApplyVerticalEntranceStack(
                            cell.Coordinate,
                            direction,
                            dressed) >= 2)
                    {
                        continue;
                    }

                    socket.ApplyAuthoredStyle(
                        direction,
                        CreateWallRandom(cell.Coordinate, direction));
                    dressed.Add(socket);
                }
            }

            System.Random fallbackRng = new System.Random(dressingSeed);
            foreach (WallSocket socket in sockets)
            {
                if (socket == null || dressed.Contains(socket))
                {
                    continue;
                }

                // RoofCell prefabs contain a Room3D and four WallSockets of their
                // own. They are decoration nested below an authored cell, not
                // extra building cells. Dressing them produced another facade
                // inside the roof and inherited the RoofCell's yaw, which looked
                // like doubled walls with reset rotations. Also repair variants
                // generated by older dressing passes.
                if (IsNestedDecorRoomSocket(socket))
                {
                    socket.RestoreDefaultStyle();
                    continue;
                }

                Room3D.Directions facing = InferFacing(socket.transform);
                socket.ApplyAuthoredStyle(facing, fallbackRng);
                dressed.Add(socket);
            }

            DressStructuralPillars();
            return dressed.Count;
        }

        /// <summary>
        /// De-duplicates cell-corner columns and thins fully interior junctions.
        /// Perimeter corners remain closed, vertical chains remain beneath taller
        /// massing, and a sparse deterministic grid of interior posts suggests
        /// structural bays and room divisions.
        /// </summary>
        public int DressStructuralPillars()
        {
            PruneMissingCells();
            Dictionary<Vector3Int, List<StructuralCornerPiece>> junctions =
                new Dictionary<Vector3Int, List<StructuralCornerPiece>>();
            Dictionary<Vector2Int, float> columnTops =
                new Dictionary<Vector2Int, float>();

            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell == null || cell.Room == null)
                {
                    continue;
                }

                float baseHeight =
                    transform.InverseTransformPoint(
                        cell.Room.transform.position).y;
                float height = CellHeight(cell.LayerType);
                Vector2Int column =
                    new Vector2Int(cell.Coordinate.x, cell.Coordinate.z);
                float top = baseHeight + height;
                if (!columnTops.TryGetValue(column, out float existingTop)
                    || top > existingTop)
                {
                    columnTops[column] = top;
                }

                AddStructuralCorner(
                    junctions,
                    cell,
                    "southwest",
                    new Vector2Int(0, 0),
                    baseHeight,
                    height);
                AddStructuralCorner(
                    junctions,
                    cell,
                    "southeast",
                    new Vector2Int(1, 0),
                    baseHeight,
                    height);
                AddStructuralCorner(
                    junctions,
                    cell,
                    "northwest",
                    new Vector2Int(0, 1),
                    baseHeight,
                    height);
                AddStructuralCorner(
                    junctions,
                    cell,
                    "northeast",
                    new Vector2Int(1, 1),
                    baseHeight,
                    height);
            }

            float tallMassThreshold =
                CalculateMedianColumnTop(columnTops);
            int visible = 0;
            foreach (KeyValuePair<Vector3Int, List<StructuralCornerPiece>>
                     junction in junctions)
            {
                List<StructuralCornerPiece> pieces = junction.Value;
                foreach (StructuralCornerPiece piece in pieces)
                {
                    piece.CornerObject.SetActive(false);
                }

                float segmentTop = float.MinValue;
                bool touchesStructuralWall = false;
                foreach (StructuralCornerPiece piece in pieces)
                {
                    segmentTop = Mathf.Max(
                        segmentTop,
                        piece.BaseHeight + piece.Height);
                    touchesStructuralWall |= piece.TouchesStructuralWall;
                }

                int surroundingCells = CountUniqueCornerCells(pieces);
                bool perimeter = surroundingCells < 4;
                bool supportsTallerMass =
                    JunctionSupportsTallerColumn(
                        junction.Key,
                        segmentTop,
                        tallMassThreshold,
                        columnTops);
                bool roomMarker =
                    ShouldKeepInteriorRoomMarker(junction.Key);
                if (perimeter
                    || touchesStructuralWall
                    || supportsTallerMass
                    || roomMarker)
                {
                    // Corner meshes are complementary quarters/halves, not
                    // duplicate full pillars. A retained junction needs every
                    // contributing piece to close its walls and column.
                    foreach (StructuralCornerPiece piece in pieces)
                    {
                        piece.CornerObject.SetActive(true);
                    }
                    visible++;
                }
            }

            return visible;
        }

        /// <summary>Advances the seed and immediately applies a fresh variation.</summary>
        public int RerollWalls()
        {
            AdvanceDressingSeed();
            return DressWalls();
        }

        /// <summary>Reapplies the selected RoofCell prefab on each registered cell.</summary>
        public int DressRoofs()
        {
            PruneMissingCells();
            HashSet<Vector2Int> columns = new HashSet<Vector2Int>();
            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell == null || cell.Room == null)
                {
                    continue;
                }

                columns.Add(new Vector2Int(
                    cell.Coordinate.x,
                    cell.Coordinate.z));
            }

            foreach (Vector2Int column in columns)
            {
                NormalizeRoofColumn(
                    new Vector3Int(column.x, 0, column.y));
            }

            return columns.Count;
        }

        public int AdvanceDressingSeed()
        {
            dressingSeed = dressingSeed == int.MaxValue ? int.MinValue : dressingSeed + 1;
            return dressingSeed;
        }

        public int AdvanceGenerationSeed()
        {
            generationSeed =
                generationSeed == int.MaxValue
                    ? int.MinValue
                    : generationSeed + 1;
            return generationSeed;
        }

        public void SetDressingSeed(int value)
        {
            dressingSeed = value;
        }

        /// <summary>
        /// Supplies the same cell and roof kit when a procedural building is
        /// materialised at runtime by <see cref="ArcadeGen3D"/>.
        /// </summary>
        public void ConfigureRuntimeKit(
            GameObject groundCell,
            GameObject upperCell,
            GameObject halfCell,
            Vector3 spacing,
            float halfHeight,
            Vector3 localGridOrigin,
            GameObject slopedRoof,
            GameObject steppedRoof,
            GameObject leftRoof,
            GameObject rightRoof,
            GameObject leftCurveRoof,
            GameObject rightCurveRoof,
            GameObject blockRoof,
            GameObject halfBlockRoof,
            int seed)
        {
            cellPrefab = groundCell;
            upperCellPrefab = upperCell;
            halfCellPrefab = halfCell;
            cellSpacing = spacing;
            halfCellHeight = halfHeight;
            gridOriginLocal = localGridOrigin;
            roofCellSloped = slopedRoof;
            roofCellStepped = steppedRoof;
            roofCellLeft = leftRoof;
            roofCellRight = rightRoof;
            roofCellLeftCurve = leftCurveRoof;
            roofCellRightCurve = rightCurveRoof;
            roofCellBlock = blockRoof;
            roofCellHalfBlock = halfBlockRoof;
            generationSeed = seed;
            dressingSeed = seed;
        }

        /// <summary>Returns the registered cell at a grid coordinate.</summary>
        public bool TryGetCell(Vector3Int coordinate, out Room3D room)
        {
            PruneMissingCells();

            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell.Coordinate == coordinate && cell.Room != null)
                {
                    room = cell.Room;
                    return true;
                }
            }

            room = null;
            return false;
        }

        /// <summary>Registers a newly-created or existing Room3D as an authored cell.</summary>
        public void RegisterCell(
            Vector3Int coordinate,
            Room3D room,
            CellLayerType layerType = CellLayerType.Full,
            bool refreshColumn = true)
        {
            if (room == null)
            {
                return;
            }

            PruneMissingCells();
            authoredCells.RemoveAll(cell =>
                cell == null || cell.Room == room || cell.Coordinate == coordinate);
            authoredCells.Add(new AuthoredCell(coordinate, room, layerType));
            if (refreshColumn)
            {
                ReflowColumn(coordinate);
                NormalizeRoofColumn(coordinate);
                DressStructuralPillars();
            }
        }

        public void ClearCellRegistrations()
        {
            if (authoredCells == null)
            {
                authoredCells = new List<AuthoredCell>();
            }
            else
            {
                authoredCells.Clear();
            }
        }

        public void RefreshStructure()
        {
            PruneMissingCells();
            ReflowAllColumns();
            DressRoofs();
            DressStructuralPillars();
        }

        public void UnregisterCell(Room3D room)
        {
            if (authoredCells == null)
            {
                return;
            }

            AuthoredCell removed = authoredCells.Find(cell =>
                cell != null && cell.Room == room);
            if (removed == null)
            {
                authoredCells.RemoveAll(cell => cell == null || cell.Room == null);
                return;
            }

            Vector3Int coordinate = removed.Coordinate;
            RoofCellType removedRoof = removed.RoofCell;
            int removedYaw = removed.RoofYawSteps;
            authoredCells.RemoveAll(cell =>
                cell == null || cell.Room == null || cell.Room == room);

            CompactColumnCoordinates(coordinate);
            ReflowColumn(coordinate);
            if (removedRoof != RoofCellType.None
                && TryGetTopCellInColumn(coordinate, out AuthoredCell newTop))
            {
                newTop.SetRoof(removedRoof, removedYaw);
            }
            NormalizeRoofColumn(coordinate);
            DressStructuralPillars();
        }

        /// <summary>
        /// Registers Room3D children that pre-date this component. The first room
        /// becomes grid coordinate zero; the rest are mapped from their offsets.
        /// </summary>
        public int RegisterExistingCells()
        {
            PruneMissingCells();
            Room3D[] rooms = GetComponentsInChildren<Room3D>(true);
            Room3D firstAuthorableRoom = null;
            foreach (Room3D room in rooms)
            {
                if (room != null
                    && (room.transform.parent == null
                        || room.transform.parent.GetComponentInParent<Room3D>()
                            == null))
                {
                    firstAuthorableRoom = room;
                    break;
                }
            }

            if (firstAuthorableRoom == null)
            {
                return 0;
            }

            if (authoredCells.Count == 0)
            {
                gridOriginLocal = transform.InverseTransformPoint(
                    firstAuthorableRoom.transform.position);
            }

            int registered = 0;
            foreach (Room3D room in rooms)
            {
                Room3D parentRoom =
                    room != null && room.transform.parent != null
                        ? room.transform.parent.GetComponentInParent<Room3D>()
                        : null;
                if (room == null || parentRoom != null || ContainsRoom(room))
                {
                    continue;
                }

                Vector3 local = transform.InverseTransformPoint(room.transform.position) - gridOriginLocal;
                Vector3Int coordinate = new Vector3Int(
                    SafeRound(local.x, cellSpacing.x),
                    SafeRound(local.y, cellSpacing.y),
                    SafeRound(local.z, cellSpacing.z));

                if (TryGetCell(coordinate, out _))
                {
                    continue;
                }

                CellLayerType layerType =
                    room.name.IndexOf("half", StringComparison.OrdinalIgnoreCase) >= 0
                        ? CellLayerType.Half
                        : CellLayerType.Full;
                authoredCells.Add(new AuthoredCell(coordinate, room, layerType));
                registered++;
            }

            ReflowAllColumns();
            DressStructuralPillars();
            return registered;
        }

        public Vector3 CellLocalPosition(
            Vector3Int coordinate,
            CellLayerType prospectiveType)
        {
            return gridOriginLocal + new Vector3(
                coordinate.x * Mathf.Max(0.01f, Mathf.Abs(cellSpacing.x)),
                ColumnLocalY(coordinate, prospectiveType),
                coordinate.z * Mathf.Max(0.01f, Mathf.Abs(cellSpacing.z)));
        }

        public GameObject CellPrefabForCoordinate(
            Vector3Int coordinate,
            CellLayerType layerType)
        {
            if (layerType == CellLayerType.Half)
            {
                return halfCellPrefab;
            }

            return coordinate.y != 0 && upperCellPrefab != null
                ? upperCellPrefab
                : cellPrefab;
        }

        public bool TryGetCellLayer(
            Vector3Int coordinate,
            out CellLayerType layerType)
        {
            if (TryGetAuthoredCell(coordinate, out AuthoredCell cell))
            {
                layerType = cell.LayerType;
                return true;
            }

            layerType = CellLayerType.Full;
            return false;
        }

        public float GetCellHeight(CellLayerType type)
        {
            return CellHeight(type);
        }

        public bool CanShareInteriorEdge(
            Vector3Int first,
            Vector3Int second)
        {
            return TryGetAuthoredCell(first, out AuthoredCell a)
                && TryGetAuthoredCell(second, out AuthoredCell b)
                && a.LayerType == b.LayerType
                && Mathf.Abs(
                    a.Room.transform.localPosition.y
                    - b.Room.transform.localPosition.y) < 0.01f;
        }

        public int OpenAllSharedEdges(bool dressAfter = true)
        {
            int opened = 0;
            Room3D.Directions[] owners =
            {
                Room3D.Directions.NORTH,
                Room3D.Directions.EAST,
            };

            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell == null || cell.Room == null)
                {
                    continue;
                }

                foreach (Room3D.Directions direction in owners)
                {
                    Vector3Int neighbour =
                        cell.Coordinate + DirectionOffset(direction);
                    if (!CanShareInteriorEdge(cell.Coordinate, neighbour))
                    {
                        continue;
                    }

                    if (TryGetWallSocket(
                            cell.Coordinate,
                            direction,
                            out WallSocket owner))
                    {
                        owner.SetAuthoredType(
                            WallSocket.AuthoredWallType.InteriorOpening);
                    }

                    if (TryGetWallSocket(
                            neighbour,
                            Opposite(direction),
                            out WallSocket duplicate))
                    {
                        duplicate.SetAuthoredType(
                            WallSocket.AuthoredWallType.InteriorOpening);
                    }

                    opened++;
                }
            }

            if (dressAfter && opened > 0)
            {
                DressWalls();
            }

            return opened;
        }

        public bool TryGetWallSocket(Vector3Int coordinate, Room3D.Directions direction, out WallSocket socket)
        {
            socket = null;
            if (!TryGetCell(coordinate, out Room3D room)
                || !room.TryGetWallTransform(direction, out Transform wallTransform))
            {
                return false;
            }

            socket = wallTransform.GetComponent<WallSocket>();
            return socket != null;
        }

        public bool ApplyWallType(
            Vector3Int coordinate,
            Room3D.Directions direction,
            WallSocket.AuthoredWallType type)
        {
            if (!TryGetWallSocket(coordinate, direction, out WallSocket socket))
            {
                return false;
            }

            socket.SetAuthoredType(type);

            if (type == WallSocket.AuthoredWallType.Entrance
                && ApplyVerticalEntranceStack(coordinate, direction, null) >= 2)
            {
                return true;
            }

            socket.ApplyAuthoredStyle(
                direction,
                CreateWallRandom(coordinate, direction));

            if (type != WallSocket.AuthoredWallType.Entrance)
            {
                RefreshVerticalEntranceAt(
                    coordinate + Vector3Int.up,
                    direction);
                RefreshVerticalEntranceAt(
                    coordinate + Vector3Int.down,
                    direction);
            }

            return true;
        }

        public bool TryGetRoofState(
            Vector3Int coordinate,
            out RoofCellType roofType,
            out int yawSteps)
        {
            if (TryGetTopCellInColumn(coordinate, out AuthoredCell cell))
            {
                roofType = cell.RoofCell;
                yawSteps = cell.RoofYawSteps;
                return true;
            }

            roofType = RoofCellType.None;
            yawSteps = 0;
            return false;
        }

        public bool TryGetTopCoordinate(
            Vector3Int coordinate,
            out Vector3Int topCoordinate)
        {
            if (TryGetTopCellInColumn(coordinate, out AuthoredCell topCell))
            {
                topCoordinate = topCell.Coordinate;
                return true;
            }

            topCoordinate = coordinate;
            return false;
        }

        public bool HasRoofPrefab(RoofCellType type)
        {
            return type == RoofCellType.None || RoofPrefab(type) != null;
        }

        public bool ApplyRoofType(
            Vector3Int coordinate,
            RoofCellType type,
            int yawSteps = 0)
        {
            if (!TryGetTopCellInColumn(coordinate, out AuthoredCell topCell))
            {
                return false;
            }

            foreach (AuthoredCell cell in ColumnCells(coordinate))
            {
                if (cell != topCell)
                {
                    cell.SetRoof(RoofCellType.None, 0);
                    ApplyRoofCell(cell);
                }
            }

            type = RoofTypeForTopLayer(type, topCell.LayerType);
            topCell.SetRoof(type, yawSteps);
            return ApplyRoofCell(topCell);
        }

        /// <summary>
        /// Marks the selected face as an entrance on every contiguous vertical
        /// cell at this X/Z coordinate, then dresses the column as matching
        /// stacker piers capped by one arch.
        /// </summary>
        public int ApplyVerticalEntranceColumn(
            Vector3Int coordinate,
            Room3D.Directions direction)
        {
            int lowestY = coordinate.y;
            while (TryGetCell(
                       new Vector3Int(coordinate.x, lowestY - 1, coordinate.z),
                       out _))
            {
                lowestY--;
            }

            int highestY = coordinate.y;
            while (TryGetCell(
                       new Vector3Int(coordinate.x, highestY + 1, coordinate.z),
                       out _))
            {
                highestY++;
            }

            Vector3Int topCoordinate =
                new Vector3Int(coordinate.x, highestY, coordinate.z);
            if (TryGetCellLayer(topCoordinate, out CellLayerType topLayer)
                && topLayer == CellLayerType.Half)
            {
                Debug.LogWarning(
                    $"{name}: a vertical entrance cannot be capped inside a half-height layer. " +
                    "Add a full cell above the half stack, then create the entrance.",
                    this);
                return 0;
            }

            int marked = 0;
            Vector3Int outward = DirectionOffset(direction);
            for (int y = lowestY; y <= highestY; y++)
            {
                Vector3Int level = new Vector3Int(coordinate.x, y, coordinate.z);
                if ((TryGetCell(level + outward, out _)
                     && CanShareInteriorEdge(level, level + outward))
                    || !TryGetWallSocket(level, direction, out WallSocket socket))
                {
                    continue;
                }

                socket.SetAuthoredType(WallSocket.AuthoredWallType.Entrance);
                marked++;
            }

            if (marked > 0)
            {
                DressWalls();
            }

            return marked;
        }

        public System.Random CreateWallRandom(Vector3Int coordinate, Room3D.Directions direction)
        {
            int wallSeed = dressingSeed;
            unchecked
            {
                wallSeed = wallSeed * 397 ^ coordinate.x;
                wallSeed = wallSeed * 397 ^ coordinate.y;
                wallSeed = wallSeed * 397 ^ coordinate.z;
                wallSeed = wallSeed * 397 ^ (int)direction;
            }

            return new System.Random(wallSeed);
        }

        private System.Random CreateEntranceStackRandom(
            Vector3Int coordinate,
            Room3D.Directions direction)
        {
            int stackSeed = dressingSeed;
            unchecked
            {
                stackSeed = stackSeed * 397 ^ coordinate.x;
                stackSeed = stackSeed * 397 ^ coordinate.z;
                stackSeed = stackSeed * 397 ^ (int)direction;
                stackSeed = stackSeed * 397 ^ 0x53544143; // "STAC"
            }

            return new System.Random(stackSeed);
        }

        private int ApplyVerticalEntranceStack(
            Vector3Int coordinate,
            Room3D.Directions direction,
            HashSet<WallSocket> dressed)
        {
            if (!IsEntranceAt(coordinate, direction))
            {
                return 0;
            }

            int lowestY = coordinate.y;
            while (IsEntranceAt(
                       new Vector3Int(coordinate.x, lowestY - 1, coordinate.z),
                       direction))
            {
                lowestY--;
            }

            int highestY = coordinate.y;
            while (IsEntranceAt(
                       new Vector3Int(coordinate.x, highestY + 1, coordinate.z),
                       direction))
            {
                highestY++;
            }

            int count = highestY - lowestY + 1;
            if (count < 2)
            {
                return count;
            }

            Vector3Int stackCoordinate =
                new Vector3Int(coordinate.x, lowestY, coordinate.z);
            WallSocket.PierFamily family = (WallSocket.PierFamily)
                CreateEntranceStackRandom(stackCoordinate, direction).Next(0, 4);
            for (int y = lowestY; y <= highestY; y++)
            {
                Vector3Int level = new Vector3Int(coordinate.x, y, coordinate.z);
                if (!TryGetWallSocket(level, direction, out WallSocket socket)
                    || socket.AuthoredType != WallSocket.AuthoredWallType.Entrance)
                {
                    continue;
                }

                // Lower levels extend the opening with stacker piers; only the
                // highest socket receives the matching arch. The explicit family
                // keeps full and half cell prefabs aligned regardless of ordering.
                socket.ApplyStackedEntranceStyle(
                    direction,
                    family,
                    y == highestY,
                    CreateEntranceStackRandom(stackCoordinate, direction));
                dressed?.Add(socket);
            }

            return count;
        }

        private void RefreshVerticalEntranceAt(
            Vector3Int coordinate,
            Room3D.Directions direction)
        {
            if (!TryGetWallSocket(coordinate, direction, out WallSocket socket)
                || socket.AuthoredType != WallSocket.AuthoredWallType.Entrance)
            {
                return;
            }

            if (ApplyVerticalEntranceStack(coordinate, direction, null) < 2)
            {
                socket.ApplyAuthoredStyle(
                    direction,
                    CreateWallRandom(coordinate, direction));
            }
        }

        private bool IsEntranceAt(
            Vector3Int coordinate,
            Room3D.Directions direction)
        {
            return TryGetWallSocket(coordinate, direction, out WallSocket socket)
                && socket.AuthoredType == WallSocket.AuthoredWallType.Entrance;
        }

        private bool ApplyRoofCell(AuthoredCell cell)
        {
            if (cell == null || cell.Room == null)
            {
                return false;
            }

            ClearRoofCell(cell);

            // Building authoring now uses the same complete RoofCell prefabs as
            // ArcadeGen3D. Clear any crown pieces previously produced by the old
            // RoomDecorSocket roof selector so the two systems cannot overlap.
            RoomDecorSocket oldCrown = cell.Room.GetComponent<RoomDecorSocket>();
            if (oldCrown != null)
            {
                oldCrown.ApplyAuthoredRoof(
                    RoomDecorSocket.AuthoredRoofType.None,
                    CreateRoofRandom(cell.Coordinate));
            }

            GameObject prefab = RoofPrefab(cell.RoofCell);
            if (prefab == null)
            {
                return cell.RoofCell == RoofCellType.None;
            }

            GameObject instance = Instantiate(prefab, cell.Room.transform);
            instance.transform.localPosition =
                Vector3.up * CellHeight(cell.LayerType);
            instance.transform.localRotation =
                Quaternion.Euler(0f, 90f * cell.RoofYawSteps, 0f);
            instance.transform.localScale = prefab.transform.localScale;
            instance.name = SpawnedRoofCellPrefix + prefab.name;
            cell.SpawnedRoofCell = instance;
            return true;
        }

        private void ClearRoofCell(AuthoredCell cell)
        {
            HashSet<GameObject> stale = new HashSet<GameObject>();
            if (cell.SpawnedRoofCell != null)
            {
                stale.Add(cell.SpawnedRoofCell);
            }

            Transform parent = cell.Room.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (child.name.StartsWith(
                        SpawnedRoofCellPrefix,
                        StringComparison.Ordinal))
                {
                    stale.Add(child);
                }
            }

            cell.SpawnedRoofCell = null;
            foreach (GameObject instance in stale)
            {
                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }
            }
        }

        private GameObject RoofPrefab(RoofCellType type)
        {
            switch (type)
            {
                case RoofCellType.Sloped: return roofCellSloped;
                case RoofCellType.Stepped: return roofCellStepped;
                case RoofCellType.SlopeLeft: return roofCellLeft;
                case RoofCellType.SlopeRight: return roofCellRight;
                case RoofCellType.SlopeLeftCurve: return roofCellLeftCurve;
                case RoofCellType.SlopeRightCurve: return roofCellRightCurve;
                case RoofCellType.Block: return roofCellBlock;
                case RoofCellType.HalfBlock: return roofCellHalfBlock;
                default: return null;
            }
        }

        private RoofCellType RoofTypeForTopLayer(
            RoofCellType type,
            CellLayerType layerType)
        {
            if (layerType == CellLayerType.Half
                && type == RoofCellType.Block
                && roofCellHalfBlock != null)
            {
                return RoofCellType.HalfBlock;
            }

            if (layerType == CellLayerType.Full
                && type == RoofCellType.HalfBlock
                && roofCellBlock != null)
            {
                return RoofCellType.Block;
            }

            return type;
        }

        private bool TryGetAuthoredCell(
            Vector3Int coordinate,
            out AuthoredCell authoredCell)
        {
            PruneMissingCells();
            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell != null
                    && cell.Room != null
                    && cell.Coordinate == coordinate)
                {
                    authoredCell = cell;
                    return true;
                }
            }

            authoredCell = null;
            return false;
        }

        private List<AuthoredCell> ColumnCells(Vector3Int coordinate)
        {
            PruneMissingCells();
            List<AuthoredCell> column = authoredCells.FindAll(cell =>
                cell != null
                && cell.Room != null
                && cell.Coordinate.x == coordinate.x
                && cell.Coordinate.z == coordinate.z);
            column.Sort((a, b) => a.Coordinate.y.CompareTo(b.Coordinate.y));
            return column;
        }

        private bool TryGetTopCellInColumn(
            Vector3Int coordinate,
            out AuthoredCell topCell)
        {
            List<AuthoredCell> column = ColumnCells(coordinate);
            topCell = column.Count > 0 ? column[column.Count - 1] : null;
            return topCell != null;
        }

        private void NormalizeRoofColumn(Vector3Int coordinate)
        {
            List<AuthoredCell> column = ColumnCells(coordinate);
            if (column.Count == 0)
            {
                return;
            }

            AuthoredCell donor = null;
            for (int i = column.Count - 1; i >= 0; i--)
            {
                if (column[i].RoofCell != RoofCellType.None)
                {
                    donor = column[i];
                    break;
                }
            }

            RoofCellType type = donor != null
                ? donor.RoofCell
                : RoofCellType.None;
            int yaw = donor != null ? donor.RoofYawSteps : 0;
            AuthoredCell top = column[column.Count - 1];
            type = RoofTypeForTopLayer(type, top.LayerType);

            foreach (AuthoredCell cell in column)
            {
                cell.SetRoof(
                    cell == top ? type : RoofCellType.None,
                    cell == top ? yaw : 0);
                ApplyRoofCell(cell);
            }
        }

        private void ReflowAllColumns()
        {
            HashSet<Vector2Int> columns = new HashSet<Vector2Int>();
            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell != null && cell.Room != null)
                {
                    columns.Add(new Vector2Int(
                        cell.Coordinate.x,
                        cell.Coordinate.z));
                }
            }

            foreach (Vector2Int column in columns)
            {
                ReflowColumn(new Vector3Int(column.x, 0, column.y));
            }
        }

        private void ReflowColumn(Vector3Int coordinate)
        {
            foreach (AuthoredCell cell in ColumnCells(coordinate))
            {
                cell.Room.transform.localPosition =
                    CellLocalPosition(cell.Coordinate, cell.LayerType);
            }
        }

        private void CompactColumnCoordinates(Vector3Int coordinate)
        {
            List<AuthoredCell> column = ColumnCells(coordinate);
            if (column.Count == 0)
            {
                return;
            }

            int pivot = column.FindIndex(cell => cell.Coordinate.y == 0);
            if (pivot < 0)
            {
                pivot = column.FindIndex(cell => cell.Coordinate.y > 0);
                if (pivot < 0)
                {
                    pivot = column.Count - 1;
                }
            }

            for (int i = 0; i < column.Count; i++)
            {
                Vector3Int old = column[i].Coordinate;
                column[i].SetCoordinate(
                    new Vector3Int(old.x, i - pivot, old.z));
            }
        }

        private float ColumnLocalY(
            Vector3Int coordinate,
            CellLayerType prospectiveType)
        {
            float y = 0f;
            if (coordinate.y > 0)
            {
                for (int layer = 0; layer < coordinate.y; layer++)
                {
                    Vector3Int below =
                        new Vector3Int(coordinate.x, layer, coordinate.z);
                    y += TryGetAuthoredCell(below, out AuthoredCell cell)
                        ? CellHeight(cell.LayerType)
                        : CellHeight(CellLayerType.Full);
                }
            }
            else if (coordinate.y < 0)
            {
                for (int layer = -1; layer >= coordinate.y; layer--)
                {
                    Vector3Int below =
                        new Vector3Int(coordinate.x, layer, coordinate.z);
                    CellLayerType type =
                        TryGetAuthoredCell(below, out AuthoredCell cell)
                            ? cell.LayerType
                            : layer == coordinate.y
                                ? prospectiveType
                                : CellLayerType.Full;
                    y -= CellHeight(type);
                }
            }

            return y;
        }

        private float CellHeight(CellLayerType type)
        {
            return type == CellLayerType.Half
                ? Mathf.Max(0.01f, Mathf.Abs(halfCellHeight))
                : Mathf.Max(0.01f, Mathf.Abs(cellSpacing.y));
        }

        public System.Random CreateRoofRandom(Vector3Int coordinate)
        {
            int roofSeed = dressingSeed;
            unchecked
            {
                roofSeed = roofSeed * 397 ^ coordinate.x;
                roofSeed = roofSeed * 397 ^ coordinate.y;
                roofSeed = roofSeed * 397 ^ coordinate.z;
                roofSeed = roofSeed * 397 ^ 0x524F4F46; // "ROOF"
            }

            return new System.Random(roofSeed);
        }

        [ContextMenu("Dress Walls")]
        private void DressWallsFromContextMenu()
        {
            DressWalls();
        }

        [ContextMenu("Dress / Reroll Walls")]
        private void RerollWallsFromContextMenu()
        {
            RerollWalls();
        }

        // Direction only matters for sockets without a Default Solid placement
        // template. Prefer the standard room names, then fall back to the socket's
        // position relative to the building root.
        private Room3D.Directions InferFacing(Transform socketTransform)
        {
            for (Transform current = socketTransform;
                 current != null && current != transform;
                 current = current.parent)
            {
                if (TryDirectionFromName(current.name, out Room3D.Directions namedDirection))
                {
                    return namedDirection;
                }
            }

            Vector3 localPosition = transform.InverseTransformPoint(socketTransform.position);
            if (Mathf.Abs(localPosition.x) > Mathf.Abs(localPosition.z))
            {
                return localPosition.x >= 0f
                    ? Room3D.Directions.EAST
                    : Room3D.Directions.WEST;
            }

            return localPosition.z >= 0f
                ? Room3D.Directions.NORTH
                : Room3D.Directions.SOUTH;
        }

        private static void AddStructuralCorner(
            Dictionary<Vector3Int, List<StructuralCornerPiece>> junctions,
            AuthoredCell cell,
            string corner,
            Vector2Int nodeOffset,
            float baseHeight,
            float height)
        {
            Transform cornerTransform =
                FindDirectCorner(cell.Room.transform, corner);
            if (cornerTransform == null)
            {
                return;
            }

            Vector2Int node = new Vector2Int(
                cell.Coordinate.x + nodeOffset.x,
                cell.Coordinate.z + nodeOffset.y);
            Vector3Int key = new Vector3Int(
                node.x,
                Mathf.RoundToInt(baseHeight * 100f),
                node.y);
            if (!junctions.TryGetValue(
                    key,
                    out List<StructuralCornerPiece> pieces))
            {
                pieces = new List<StructuralCornerPiece>();
                junctions.Add(key, pieces);
            }

            pieces.Add(new StructuralCornerPiece(
                cornerTransform.gameObject,
                cell.Coordinate,
                baseHeight,
                height,
                CornerTouchesStructuralWall(cell, corner)));
        }

        private static bool CornerTouchesStructuralWall(
            AuthoredCell cell,
            string semanticCorner)
        {
            Room3D.Directions first;
            Room3D.Directions second;
            switch (semanticCorner)
            {
                case "southwest":
                    first = Room3D.Directions.SOUTH;
                    second = Room3D.Directions.WEST;
                    break;
                case "southeast":
                    first = Room3D.Directions.SOUTH;
                    second = Room3D.Directions.EAST;
                    break;
                case "northwest":
                    first = Room3D.Directions.NORTH;
                    second = Room3D.Directions.WEST;
                    break;
                default:
                    first = Room3D.Directions.NORTH;
                    second = Room3D.Directions.EAST;
                    break;
            }

            return WallIsStructural(cell.Room, first)
                || WallIsStructural(cell.Room, second);
        }

        private static bool WallIsStructural(
            Room3D room,
            Room3D.Directions direction)
        {
            if (!room.TryGetWallTransform(
                    direction,
                    out Transform wallTransform))
            {
                return true;
            }

            WallSocket socket = wallTransform.GetComponent<WallSocket>();
            return socket == null
                || socket.AuthoredType
                    != WallSocket.AuthoredWallType.InteriorOpening;
        }

        private static Transform FindDirectCorner(
            Transform room,
            string semanticCorner)
        {
            for (int i = 0; i < room.childCount; i++)
            {
                Transform child = room.GetChild(i);
                string normalized = child.name
                    .Replace(" ", string.Empty)
                    .Replace("_", string.Empty)
                    .ToLowerInvariant();
                switch (semanticCorner)
                {
                    case "southwest":
                        if (normalized.StartsWith("swcorner")
                            || normalized.StartsWith("swcrnr")
                            || normalized.StartsWith("southwest"))
                        {
                            return child;
                        }
                        break;
                    case "southeast":
                        if (normalized.StartsWith("secorner")
                            || normalized.StartsWith("secrnr")
                            || normalized.StartsWith("southeast"))
                        {
                            return child;
                        }
                        break;
                    case "northwest":
                        if (normalized.StartsWith("nwcorner")
                            || normalized.StartsWith("nwcrnr")
                            || normalized.StartsWith("northwest"))
                        {
                            return child;
                        }
                        break;
                    case "northeast":
                        if (normalized.StartsWith("necorner")
                            || normalized.StartsWith("necrnr")
                            || normalized.StartsWith("northeast"))
                        {
                            return child;
                        }
                        break;
                }
            }
            return null;
        }

        private static int CountUniqueCornerCells(
            List<StructuralCornerPiece> pieces)
        {
            HashSet<Vector3Int> coordinates = new HashSet<Vector3Int>();
            foreach (StructuralCornerPiece piece in pieces)
            {
                if (piece != null)
                {
                    coordinates.Add(piece.CellCoordinate);
                }
            }
            return coordinates.Count;
        }

        private static bool JunctionSupportsTallerColumn(
            Vector3Int junction,
            float segmentTop,
            float tallMassThreshold,
            Dictionary<Vector2Int, float> columnTops)
        {
            Vector2Int[] surrounding =
            {
                new Vector2Int(junction.x - 1, junction.z - 1),
                new Vector2Int(junction.x, junction.z - 1),
                new Vector2Int(junction.x - 1, junction.z),
                new Vector2Int(junction.x, junction.z),
            };
            foreach (Vector2Int column in surrounding)
            {
                if (columnTops.TryGetValue(column, out float top)
                    && top > tallMassThreshold + 0.01f
                    && top > segmentTop + 0.01f)
                {
                    return true;
                }
            }
            return false;
        }

        private static float CalculateMedianColumnTop(
            Dictionary<Vector2Int, float> columnTops)
        {
            if (columnTops.Count == 0)
            {
                return 0f;
            }

            List<float> heights =
                new List<float>(columnTops.Values);
            heights.Sort();
            int middle = heights.Count / 2;
            return heights.Count % 2 == 0
                ? (heights[middle - 1] + heights[middle]) * 0.5f
                : heights[middle];
        }

        private bool ShouldKeepInteriorRoomMarker(Vector3Int junction)
        {
            int floorSeed;
            unchecked
            {
                floorSeed = generationSeed * 397 ^ junction.y;
                floorSeed = floorSeed * 397 ^ 0x504F5354; // "POST"
            }

            int xPhase = PositiveModulo(floorSeed, 2);
            int zPhase = PositiveModulo(floorSeed >> 3, 2);
            return PositiveModulo(junction.x - xPhase, 2) == 0
                && PositiveModulo(junction.z - zPhase, 2) == 0;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private static bool IsNestedDecorRoomSocket(WallSocket socket)
        {
            Room3D nearestRoom = socket.GetComponentInParent<Room3D>(true);
            if (nearestRoom == null || nearestRoom.transform.parent == null)
            {
                return false;
            }

            return nearestRoom.transform.parent.GetComponentInParent<Room3D>(true)
                != null;
        }

        private static Vector3Int DirectionOffset(Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH: return new Vector3Int(0, 0, 1);
                case Room3D.Directions.SOUTH: return new Vector3Int(0, 0, -1);
                case Room3D.Directions.EAST: return new Vector3Int(1, 0, 0);
                case Room3D.Directions.WEST: return new Vector3Int(-1, 0, 0);
                default: return Vector3Int.zero;
            }
        }

        private static Room3D.Directions Opposite(Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH: return Room3D.Directions.SOUTH;
                case Room3D.Directions.SOUTH: return Room3D.Directions.NORTH;
                case Room3D.Directions.EAST: return Room3D.Directions.WEST;
                case Room3D.Directions.WEST: return Room3D.Directions.EAST;
                default: return Room3D.Directions.NONE;
            }
        }

        private static bool TryDirectionFromName(string objectName, out Room3D.Directions direction)
        {
            string normalized = objectName
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .ToLowerInvariant();

            if (normalized.StartsWith("northwall") || normalized.StartsWith("nwall"))
            {
                direction = Room3D.Directions.NORTH;
                return true;
            }

            if (normalized.StartsWith("southwall") || normalized.StartsWith("swall"))
            {
                direction = Room3D.Directions.SOUTH;
                return true;
            }

            if (normalized.StartsWith("eastwall") || normalized.StartsWith("ewall"))
            {
                direction = Room3D.Directions.EAST;
                return true;
            }

            if (normalized.StartsWith("westwall") || normalized.StartsWith("wwall"))
            {
                direction = Room3D.Directions.WEST;
                return true;
            }

            direction = Room3D.Directions.NONE;
            return false;
        }

        private bool ContainsRoom(Room3D room)
        {
            foreach (AuthoredCell cell in authoredCells)
            {
                if (cell != null && cell.Room == room)
                {
                    return true;
                }
            }

            return false;
        }

        private void PruneMissingCells()
        {
            if (authoredCells == null)
            {
                authoredCells = new List<AuthoredCell>();
                return;
            }

            authoredCells.RemoveAll(cell => cell == null || cell.Room == null);
        }

        private static int SafeRound(float value, float spacing)
        {
            float safeSpacing = Mathf.Max(0.01f, Mathf.Abs(spacing));
            return Mathf.RoundToInt(value / safeSpacing);
        }
    }

    /// <summary>
    /// Runtime-safe source of truth for procedural building massing, entrances,
    /// and roofs. Both the prefab authoring Inspector and ArcadeGen3D consume the
    /// resulting immutable plan.
    /// </summary>
    public static class BuildingPlanUtility
    {
        public readonly struct Entrance
        {
            public readonly Vector2Int Column;
            public readonly Room3D.Directions Face;
            public readonly bool Vertical;

            public Entrance(
                Vector2Int column,
                Room3D.Directions face,
                bool vertical)
            {
                Column = column;
                Face = face;
                Vertical = vertical;
            }
        }

        public readonly struct RoofPlacement
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

        public sealed class Plan
        {
            public readonly int Width;
            public readonly int Length;
            public readonly bool[,] Footprint;
            public readonly int[,] FullHeights;
            public readonly bool[,] HalfTops;
            public readonly List<Vector2Int> Columns;
            public readonly List<Entrance> Entrances;
            public readonly Dictionary<Vector2Int, RoofPlacement> Roofs;
            public readonly int Seed;

            internal Plan(
                int width,
                int length,
                bool[,] footprint,
                int[,] fullHeights,
                bool[,] halfTops,
                List<Vector2Int> columns,
                List<Entrance> entrances,
                Dictionary<Vector2Int, RoofPlacement> roofs,
                int seed)
            {
                Width = width;
                Length = length;
                Footprint = footprint;
                FullHeights = fullHeights;
                HalfTops = halfTops;
                Columns = columns;
                Entrances = entrances;
                Roofs = roofs;
                Seed = seed;
            }
        }

        private enum Archetype
        {
            Organic,
            LShape,
            TShape,
            TowerHouse,
        }

        private readonly struct RoofSurface
        {
            public readonly Vector2Int Column;
            public readonly int TopUnits;
            public readonly BuildingComponent.CellLayerType TopLayer;

            public RoofSurface(
                Vector2Int column,
                int topUnits,
                BuildingComponent.CellLayerType topLayer)
            {
                Column = column;
                TopUnits = topUnits;
                TopLayer = topLayer;
            }
        }

        private static readonly Vector2Int[] Offsets =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.right,
            Vector2Int.left,
        };

        private static readonly Room3D.Directions[] Directions =
        {
            Room3D.Directions.NORTH,
            Room3D.Directions.SOUTH,
            Room3D.Directions.EAST,
            Room3D.Directions.WEST,
        };

        public static Plan Create(
            int width,
            int length,
            int heightLimit,
            int entranceCount,
            float halfLayerChance,
            int seed,
            Func<BuildingComponent.RoofCellType, bool> hasRoofPrefab)
        {
            width = Mathf.Max(1, width);
            length = Mathf.Max(1, length);
            heightLimit = Mathf.Max(1, heightLimit);
            System.Random rng = new System.Random(seed);

            bool[,] footprint = new bool[width, length];
            Archetype archetype = PickArchetype(
                width,
                length,
                heightLimit,
                rng);
            switch (archetype)
            {
                case Archetype.LShape:
                    GrowLFootprint(footprint, rng);
                    break;
                case Archetype.TShape:
                    GrowTFootprint(footprint, rng);
                    break;
                case Archetype.TowerHouse:
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

            int[,] heights = new int[width, length];
            int baseHeight =
                archetype == Archetype.TowerHouse
                    ? 1
                    : rng.Next(1, Mathf.Min(2, heightLimit) + 1);
            foreach (Vector2Int column in columns)
            {
                heights[column.x, column.y] = baseHeight;
            }

            if (heightLimit > baseHeight
                && (archetype == Archetype.TowerHouse
                    || rng.NextDouble() < 0.62))
            {
                RaiseTower(
                    footprint,
                    heights,
                    columns,
                    baseHeight,
                    heightLimit,
                    archetype == Archetype.TowerHouse,
                    rng);
            }

            bool[,] halfTops = new bool[width, length];
            if (halfLayerChance > 0f
                && rng.NextDouble() < Mathf.Clamp01(halfLayerChance))
            {
                AddHalfTopCluster(
                    footprint,
                    heights,
                    halfTops,
                    columns,
                    heightLimit,
                    rng);
            }

            List<Entrance> entrances = PlanEntrances(
                footprint,
                heights,
                halfTops,
                columns,
                Mathf.Max(0, entranceCount),
                rng);
            Dictionary<Vector2Int, RoofPlacement> roofs = PlanRoofs(
                heights,
                halfTops,
                columns,
                hasRoofPrefab ?? (_ => true),
                rng);
            return new Plan(
                width,
                length,
                footprint,
                heights,
                halfTops,
                columns,
                entrances,
                roofs,
                seed);
        }

        private static Archetype PickArchetype(
            int width,
            int length,
            int heightLimit,
            System.Random rng)
        {
            List<Archetype> available =
                new List<Archetype> { Archetype.Organic };
            if (width >= 2 && length >= 2)
            {
                available.Add(Archetype.LShape);
            }
            if ((width >= 3 && length >= 2)
                || (length >= 3 && width >= 2))
            {
                available.Add(Archetype.TShape);
            }
            if (heightLimit >= 2)
            {
                available.Add(Archetype.TowerHouse);
                available.Add(Archetype.TowerHouse);
            }
            return available[rng.Next(available.Count)];
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
                    foreach (Vector2Int offset in Offsets)
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

                bool closeGaps = rng.NextDouble() < 0.58;
                frontier.Sort((a, b) =>
                {
                    int aScore = CountOccupiedNeighbours(footprint, a);
                    int bScore = CountOccupiedNeighbours(footprint, b);
                    return closeGaps
                        ? bScore.CompareTo(aScore)
                        : aScore.CompareTo(bScore);
                });
                Vector2Int picked =
                    frontier[rng.Next(Mathf.Min(3, frontier.Count))];
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
                width >= 3 && (length < 3 || rng.NextDouble() < 0.5);
            if (vertical)
            {
                int crossWidth = rng.Next(3, width + 1);
                int crossStart = rng.Next(0, width - crossWidth + 1);
                bool north = rng.NextDouble() < 0.5;
                int crossZ = north ? length - 1 : 0;
                int stemX =
                    rng.Next(crossStart + 1, crossStart + crossWidth - 1);
                int stemLength = rng.Next(2, length + 1);
                for (int x = crossStart; x < crossStart + crossWidth; x++)
                {
                    footprint[x, crossZ] = true;
                }
                for (int step = 0; step < stemLength; step++)
                {
                    footprint[
                        stemX,
                        north ? crossZ - step : crossZ + step] = true;
                }
                return;
            }

            int crossLength = rng.Next(3, length + 1);
            int crossStartZ = rng.Next(0, length - crossLength + 1);
            bool east = rng.NextDouble() < 0.5;
            int crossX = east ? width - 1 : 0;
            int stemZ =
                rng.Next(crossStartZ + 1, crossStartZ + crossLength - 1);
            int horizontalLength = rng.Next(2, width + 1);
            for (int z = crossStartZ; z < crossStartZ + crossLength; z++)
            {
                footprint[crossX, z] = true;
            }
            for (int step = 0; step < horizontalLength; step++)
            {
                footprint[
                    east ? crossX - step : crossX + step,
                    stemZ] = true;
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
                        == heights[seed.x, seed.y]
                    && heights[neighbour.x, neighbour.y] < heightLimit)
                {
                    halfTops[neighbour.x, neighbour.y] = true;
                    break;
                }
            }
        }

        private static List<Entrance> PlanEntrances(
            bool[,] footprint,
            int[,] heights,
            bool[,] halfTops,
            List<Vector2Int> columns,
            int entranceCount,
            System.Random rng)
        {
            List<(Vector2Int column, Room3D.Directions face)> candidates =
                new List<(Vector2Int, Room3D.Directions)>();
            foreach (Vector2Int column in columns)
            {
                foreach (Room3D.Directions direction in Directions)
                {
                    Vector2Int neighbour =
                        column + DirectionOffset(direction);
                    if (!InBounds(footprint, neighbour)
                        || !footprint[neighbour.x, neighbour.y])
                    {
                        candidates.Add((column, direction));
                    }
                }
            }

            Shuffle(candidates, rng);
            int count = Mathf.Min(entranceCount, candidates.Count);
            List<Entrance> entrances = new List<Entrance>(count);
            for (int i = 0; i < count; i++)
            {
                Vector2Int column = candidates[i].column;
                bool vertical =
                    heights[column.x, column.y] >= 2
                    && !halfTops[column.x, column.y]
                    && rng.NextDouble() < 0.35;
                entrances.Add(new Entrance(
                    column,
                    candidates[i].face,
                    vertical));
            }
            return entrances;
        }

        private static Dictionary<Vector2Int, RoofPlacement> PlanRoofs(
            int[,] heights,
            bool[,] halfTops,
            List<Vector2Int> columns,
            Func<BuildingComponent.RoofCellType, bool> hasRoof,
            System.Random rng)
        {
            Dictionary<Vector2Int, RoofSurface> surfaces =
                new Dictionary<Vector2Int, RoofSurface>();
            foreach (Vector2Int column in columns)
            {
                bool half = halfTops[column.x, column.y];
                surfaces[column] = new RoofSurface(
                    column,
                    heights[column.x, column.y] * 2 + (half ? 1 : 0),
                    half
                        ? BuildingComponent.CellLayerType.Half
                        : BuildingComponent.CellLayerType.Full);
            }

            Dictionary<Vector2Int, RoofPlacement> placements =
                new Dictionary<Vector2Int, RoofPlacement>();
            List<Vector2Int> ordered = new List<Vector2Int>(columns);
            Shuffle(ordered, rng);
            ordered.Sort((a, b) =>
                CountSameHeightNeighbours(surfaces, a)
                    .CompareTo(CountSameHeightNeighbours(surfaces, b)));

            foreach (Vector2Int column in ordered)
            {
                if (placements.ContainsKey(column))
                {
                    continue;
                }
                List<Vector2Int> partners =
                    SameHeightNeighbours(surfaces, surfaces[column]);
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
                bool canCurve =
                    hasRoof(BuildingComponent.RoofCellType.SlopeLeftCurve)
                    && hasRoof(BuildingComponent.RoofCellType.SlopeRightCurve);
                bool canStraight =
                    hasRoof(BuildingComponent.RoofCellType.SlopeLeft)
                    && hasRoof(BuildingComponent.RoofCellType.SlopeRight);
                if (!canCurve && !canStraight)
                {
                    break;
                }
                bool curved =
                    canCurve && (!canStraight || rng.NextDouble() < 0.42);
                Room3D.Directions toward =
                    DirectionBetween(column, partner);
                placements[column] = new RoofPlacement(
                    curved
                        ? BuildingComponent.RoofCellType.SlopeLeftCurve
                        : BuildingComponent.RoofCellType.SlopeLeft,
                    YawForApex(true, toward));
                placements[partner] = new RoofPlacement(
                    curved
                        ? BuildingComponent.RoofCellType.SlopeRightCurve
                        : BuildingComponent.RoofCellType.SlopeRight,
                    YawForApex(false, Opposite(toward)));
            }

            HashSet<Vector2Int> flatSupports = new HashSet<Vector2Int>();
            foreach (Vector2Int column in ordered)
            {
                if (placements.ContainsKey(column)
                    || HasAdjacentDirectionalRoof(placements, column)
                    || !TryFindTallerFullNeighbour(
                        surfaces,
                        placements,
                        surfaces[column],
                        rng,
                        out Room3D.Directions direction,
                        out Vector2Int support)
                    || !TryPickLeanRoof(
                        hasRoof,
                        rng,
                        out BuildingComponent.RoofCellType type,
                        out bool useLeft))
                {
                    continue;
                }
                placements[column] =
                    new RoofPlacement(type, YawForApex(useLeft, direction));
                flatSupports.Add(support);
            }
            foreach (Vector2Int support in flatSupports)
            {
                placements[support] = new RoofPlacement(
                    BuildingComponent.RoofCellType.Block,
                    rng.Next(4));
            }

            foreach (Vector2Int column in columns)
            {
                if (placements.ContainsKey(column))
                {
                    continue;
                }
                RoofSurface surface = surfaces[column];
                placements[column] = new RoofPlacement(
                    PickStandaloneRoof(hasRoof, surface.TopLayer, rng),
                    rng.Next(4));
            }
            return placements;
        }

        private static bool TryFindTallerFullNeighbour(
            Dictionary<Vector2Int, RoofSurface> surfaces,
            Dictionary<Vector2Int, RoofPlacement> placements,
            RoofSurface surface,
            System.Random rng,
            out Room3D.Directions direction,
            out Vector2Int support)
        {
            List<Room3D.Directions> taller =
                new List<Room3D.Directions>();
            int tallest = surface.TopUnits;
            foreach (Room3D.Directions candidate in Directions)
            {
                Vector2Int neighbour =
                    surface.Column + DirectionOffset(candidate);
                if (!surfaces.TryGetValue(neighbour, out RoofSurface other)
                    || placements.ContainsKey(neighbour)
                    || other.TopLayer != BuildingComponent.CellLayerType.Full
                    || other.TopUnits <= surface.TopUnits)
                {
                    continue;
                }
                if (other.TopUnits > tallest)
                {
                    tallest = other.TopUnits;
                    taller.Clear();
                }
                if (other.TopUnits == tallest)
                {
                    taller.Add(candidate);
                }
            }
            if (taller.Count == 0)
            {
                direction = Room3D.Directions.NONE;
                support = surface.Column;
                return false;
            }
            direction = taller[rng.Next(taller.Count)];
            support = surface.Column + DirectionOffset(direction);
            return true;
        }

        private static bool TryPickLeanRoof(
            Func<BuildingComponent.RoofCellType, bool> hasRoof,
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
                if (hasRoof(candidate))
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
            bool curve = rng.NextDouble() < 0.42;
            List<BuildingComponent.RoofCellType> preferred =
                available.FindAll(candidate =>
                    curve
                        ? candidate
                            == BuildingComponent.RoofCellType.SlopeLeftCurve
                            || candidate
                            == BuildingComponent.RoofCellType.SlopeRightCurve
                        : candidate == BuildingComponent.RoofCellType.SlopeLeft
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

        private static BuildingComponent.RoofCellType PickStandaloneRoof(
            Func<BuildingComponent.RoofCellType, bool> hasRoof,
            BuildingComponent.CellLayerType topLayer,
            System.Random rng)
        {
            bool sloped =
                hasRoof(BuildingComponent.RoofCellType.Sloped);
            bool stepped =
                hasRoof(BuildingComponent.RoofCellType.Stepped);
            if (sloped && stepped)
            {
                return rng.NextDouble() < 0.5
                    ? BuildingComponent.RoofCellType.Sloped
                    : BuildingComponent.RoofCellType.Stepped;
            }
            if (sloped || stepped)
            {
                return sloped
                    ? BuildingComponent.RoofCellType.Sloped
                    : BuildingComponent.RoofCellType.Stepped;
            }
            BuildingComponent.RoofCellType fallback =
                topLayer == BuildingComponent.CellLayerType.Half
                    ? BuildingComponent.RoofCellType.HalfBlock
                    : BuildingComponent.RoofCellType.Block;
            return hasRoof(fallback)
                ? fallback
                : BuildingComponent.RoofCellType.None;
        }

        private static bool HasAdjacentDirectionalRoof(
            Dictionary<Vector2Int, RoofPlacement> placements,
            Vector2Int column)
        {
            foreach (Vector2Int offset in Offsets)
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

        private static List<Vector2Int> SameHeightNeighbours(
            Dictionary<Vector2Int, RoofSurface> surfaces,
            RoofSurface surface)
        {
            List<Vector2Int> neighbours = new List<Vector2Int>();
            foreach (Vector2Int offset in Offsets)
            {
                Vector2Int neighbour = surface.Column + offset;
                if (surfaces.TryGetValue(neighbour, out RoofSurface other)
                    && other.TopUnits == surface.TopUnits)
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
            foreach (Vector2Int offset in Offsets)
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

        private static bool InBounds(
            bool[,] footprint,
            Vector2Int column)
        {
            return column.x >= 0
                && column.y >= 0
                && column.x < footprint.GetLength(0)
                && column.y < footprint.GetLength(1);
        }

        private static Vector2Int DirectionOffset(
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

        private static int YawForApex(
            bool isLeft,
            Room3D.Directions target)
        {
            int authored = isLeft
                ? DirectionIndex(Room3D.Directions.NORTH)
                : DirectionIndex(Room3D.Directions.SOUTH);
            return (DirectionIndex(target) - authored + 4) & 3;
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
    }
}
