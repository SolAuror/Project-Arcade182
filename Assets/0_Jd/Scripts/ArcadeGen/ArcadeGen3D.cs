using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    [Serializable]
    public class ArcadeMazeRules
    {
        [Header("Room Prefabs")]
        [Tooltip("When false, the generator keeps its own room prefabs and placement mode; the rules only control size, openings, and player/exit flags.")]
        public bool overrideRoomPrefabs = true;
        public List<GameObject> possibleRoomPrefabs = new List<GameObject>();
        public GameObject firstRoomPrefab;
        public GameObject lastRoomPrefab;
        public GameObject centerRoomPrefab;
        public ArcadeGen3D.SpecialRoomPlacementMode specialRoomPlacementMode =
            ArcadeGen3D.SpecialRoomPlacementMode.GenerateFromCenter;

        [Header("Maze Size")]
        [Min(1)] public int numX = 10;
        [Min(1)] public int numZ = 10;

        [Header("Braiding")]
        [Tooltip("Fraction of dead-ends knocked open into loops after the perfect-maze carve. 0 = classic single-path maze (the hub default). Loops give the player a route around obstacles like pits.")]
        [Range(0f, 1f)] public float braidRate;

        [Header("Pits")]
        [Tooltip("Number of pit cells the maze carves AROUND (obstacle-first): the walkable graph excludes them, so the exit is always reachable and pits stretch the route rather than block it. 0 = no pits (the hub default).")]
        [Min(0)] public int pitCount;

        [Tooltip("Void apparatus spawned beneath a designated pit room (retaining shafts, corner pillars and fog). Required for pitCount to take effect.")]
        public GameObject pitVoidPrefab;

        [Header("Buildings")]
        [Tooltip("Number of PROCEDURAL buildings placed obstacle-first (the maze carves AROUND their footprints, same as pits/voids, so the exit is always reachable). Each is a walled mass the player enters through a single carved doorway onto one open interior hall. Buildings keep a 1-cell gap so streets always wrap them. 0 = none (the hub default).")]
        [Min(0)] public int proceduralBuildingCount;

        [Tooltip("Smallest procedural-building footprint edge, in cells (1 = a single-cell hut). Any size is allowed on any level; larger footprints simply only land where space and connectivity allow.")]
        [Min(1)] public int buildingMinSize = 1;

        [Tooltip("Largest procedural-building footprint edge, in cells. Keep it under the maze size so a building never swallows the level.")]
        [Min(1)] public int buildingMaxSize = 2;

        [Tooltip("Hand-authored building prefabs dropped in obstacle-first exactly like procedural buildings. Footprint is read from the prefab's bounds; entrances are found by scanning perimeter WallSockets flagged as authored openings, and the generator opens the adjacent street to each. Empty = procedural buildings only.")]
        public List<GameObject> authoredBuildings = new List<GameObject>();

        [Tooltip("How many authored buildings to place, drawn from the Authored Buildings list. 0 = none.")]
        [Min(0)] public int authoredBuildingCount;

        [Header("Plazas (open squares)")]
        [Tooltip("Number of rare open-air PLAZAS: rectangular multi-cell regions whose interior walls are removed to make a widened outdoor square among the narrow streets. Only removes walls, so the exit stays reachable. Keep low - narrow streets are the norm. 0 = all narrow streets (the hub default).")]
        [Min(0)] public int plazaCount;

        [Tooltip("Smallest plaza edge in cells.")]
        [Min(2)] public int plazaMinSize = 2;

        [Tooltip("Largest plaza edge in cells.")]
        [Min(2)] public int plazaMaxSize = 3;

        [Header("Footprint")]
        [Tooltip("Carve inside an organic, non-rectangular blob of the grid instead of the full rectangle. Rooms stay grid-aligned; the level outline becomes irregular. 0 = classic full rectangle (the hub default).")]
        public bool organicFootprint;

        [Tooltip("Fraction of the WxH grid kept as active cells when Organic Footprint is on. Lower = more eroded / more irregular. Start and exit are always kept.")]
        [Range(0.35f, 1f)] public float footprintFill = 0.7f;

        [Header("Outer Openings")]
        public bool openStartOuterWall;
        public Room3D.Directions startOuterWallDirection = Room3D.Directions.SOUTH;
        public bool openEndOuterWall;
        public Room3D.Directions endOuterWallDirection = Room3D.Directions.NORTH;

        [Header("Player And Exit")]
        public bool respawnPlayerAtStart = true;
        public bool activateEndRoomExit = true;
    }

    public class ArcadeGen3D : MonoBehaviour
    {
        public enum SpecialRoomPlacementMode
        {
            FixedCorners,
            RandomStartAndEnd,
            GenerateFromCenter
        }

        [Header("Room Prefabs")]
        [Tooltip("Weighted room prefabs used for regular maze cells.")]
        [SerializeField] private List<GameObject> possibleRoomPrefabs = new List<GameObject>();

        [Tooltip("Room prefab used for the player start.")]
        [SerializeField] private GameObject firstRoomPrefab;

        [Tooltip("Room prefab used for the maze exit.")]
        [SerializeField] private GameObject lastRoomPrefab;

        [Tooltip("Room prefab used for the center start when Generate From Center is selected.")]
        [SerializeField] private GameObject centerRoomPrefab;

        [Tooltip("Choose fixed corner, random, or center start/end placement.")]
        [SerializeField] private SpecialRoomPlacementMode specialRoomPlacementMode = SpecialRoomPlacementMode.GenerateFromCenter;

        [SerializeField, HideInInspector] private GameObject roomPrefab;

        [Header("Maze Size")]
        [Tooltip("Number of rooms along local X.")]
        [SerializeField] private int numX = 10;

        [Tooltip("Number of rooms along local Z.")]
        [SerializeField] private int numZ = 10;

        [Header("Generation")]
        [Tooltip("Generate and carve the maze automatically when the scene starts.")]
        [SerializeField] private bool autoGenerateOnStart = true;

        [Tooltip("Allow R to regenerate the maze during play. Disabled by default so R can rotate held objects.")]
        [SerializeField] private bool allowRuntimeKeyboardRegenerate = false;

        [Tooltip("Maze carving steps processed per frame while generating at runtime.")]
        [SerializeField, Min(1)] private int generationStepsPerFrame = 32;

        [Header("Outer Openings")]
        [Tooltip("Optional outside opening on the start room.")]
        [SerializeField] private bool openStartOuterWall = false;

        [Tooltip("Wall direction to open when Start Outer Wall is enabled.")]
        [SerializeField] private Room3D.Directions startOuterWallDirection = Room3D.Directions.SOUTH;

        [Tooltip("Optional outside opening on the end room.")]
        [SerializeField] private bool openEndOuterWall = false;

        [Tooltip("Wall direction to open when End Outer Wall is enabled.")]
        [SerializeField] private Room3D.Directions endOuterWallDirection = Room3D.Directions.NORTH;

        [Header("Player")]
        [Tooltip("Move the existing player back to the generated start room after regeneration.")]
        [SerializeField] private bool respawnPlayerAtStartOnRegenerate = true;

        [Header("Wall Dressing")]
        [Tooltip("After carving, let each wall's WallSocket swap in a themed part (archway/doorway for openings, window/arrowslit walls for solids, gable caps on outer walls). Off on the hub generator; leave off unless the room prefabs carry WallSocket components. Purely cosmetic - never changes layout or reachability.")]
        [SerializeField] private bool dressWallsAfterCarve = false;

        [Tooltip("Render each shared wall once (by one owning cell) instead of both cells drawing it. A window then looks THROUGH into the neighbouring space instead of into a back-to-back wall, and interior walls stop doubling up. Turn OFF if your wall meshes are single-sided and show through from the back. Only applies while Dress Walls After Carve is on.")]
        [SerializeField] private bool deDoubleSharedWalls = true;

        [Header("Upper Floors (lost-city buildings)")]
        [Tooltip("Stack real upper-floor cells on the sealed solid-block buildings to make multi-storey lost-city structures with windowed facades onto the streets, capped by authored roof cells. Purely cosmetic exterior massing - the player never traverses them and the walkable graph never changes. Only applies while Dress Walls After Carve is on; always off on the hub.")]
        [SerializeField] private bool buildUpperFloors = true;

        [Tooltip("Full-height walled upper-cell prefab (walls substituted from the full-wall kit). The 2nd storey of a building.")]
        [SerializeField] private GameObject upperCellPrefab;

        [Tooltip("Half-height walled upper-cell prefab (walls substituted from the horizontal-half-wall kit). Optional spacer slipped in below the roof.")]
        [SerializeField] private GameObject upperCellHalfPrefab;

        [Header("Roof kit (authored cells, placed + rotated, internals kept)")]
        [Tooltip("RoofCell_Sloped - a self-contained hipped/pyramid roof that caps any single cell at any yaw.")]
        [SerializeField] private GameObject roofHipPrefab;

        [Tooltip("RoofCell_Stepped - a self-contained stepped/ziggurat roof cap.")]
        [SerializeField] private GameObject roofSteppedPrefab;

        [Tooltip("RoofCell_L - a single straight slope. Pairs with RoofCell_R into a ridge, or leans its high edge into a taller neighbour.")]
        [SerializeField] private GameObject roofSlopeLeftPrefab;

        [Tooltip("RoofCell_R - the mirror of RoofCell_L.")]
        [SerializeField] private GameObject roofSlopeRightPrefab;

        [Tooltip("RoofCell_L_Curve - curved (ogee) version of the left slope.")]
        [SerializeField] private GameObject roofSlopeLeftCurvePrefab;

        [Tooltip("RoofCell_R_Curve - curved version of the right slope.")]
        [SerializeField] private GameObject roofSlopeRightCurvePrefab;

        [Tooltip("RoofCell_Block - a flat roof cap for a full-cell top (a flat-roofed building).")]
        [SerializeField] private GameObject roofFlatBlockPrefab;

        [Tooltip("RoofCell_HalfBlock - a flat roof cap used when the cell's top layer is a half storey.")]
        [SerializeField] private GameObject roofFlatHalfBlockPrefab;

        [Header("Massing")]
        [Tooltip("Full storey height - vertical spacing of a full walled cell. 5.95 overlaps the seam like the ground walls.")]
        [SerializeField] private float upperFloorHeight = 5.95f;

        [Tooltip("Half storey height - the HorizontalHalfWall band (2.98).")]
        [SerializeField] private float halfFloorHeight = 2.98f;

        [Tooltip("Chance a building gets a full walled 2nd storey (UpperCell). Otherwise the roof sits straight on the ground block.")]
        [SerializeField, Range(0f, 1f)] private float upperStoryChance = 0.5f;

        [Tooltip("Chance an individual cell within a building rises one extra storey (a tower), so lower neighbours lean their roofs into it.")]
        [SerializeField, Range(0f, 1f)] private float towerChance = 0.25f;

        [Tooltip("Chance a building slips a half storey (UpperCell_Half) in below its roof.")]
        [SerializeField, Range(0f, 1f)] private float halfStoryChance = 0.3f;

        [Tooltip("Chance a building is flat-roofed (a flat RoofCell_Block cap) instead of a pitched roof. Falls back to a pitched roof if no flat cap prefab is assigned.")]
        [SerializeField, Range(0f, 1f)] private float flatTopChance = 0.15f;

        [Tooltip("Chance a leaning/paired roof slope uses the curved (ogee) variant instead of the straight one.")]
        [SerializeField, Range(0f, 1f)] private float curvedRoofChance = 0.4f;

        [Tooltip("Extra 90-degree yaw applied to every placed roof cell. Use this to correct the whole roof kit's facing in one place if the slopes come out rotated.")]
        [SerializeField, Range(0, 3)] private int roofYawTrim = 0;

        [Header("Roof configurations (multi-cell)")]
        [Tooltip("Authored multi-cell roof prefabs placed as ONE unit over a matching rectangular region of same-height building cells (e.g. a 2x2 pitched roof, or bigger). Tried largest-area first, before the per-cell roofs; whatever cells are left fall back to the per-cell slopes / flat blocks. Author each prefab centred on its own footprint (origin at the middle of its cells).")]
        [SerializeField] private List<RoofConfiguration> roofConfigurations = new List<RoofConfiguration>();

        /// <summary>
        /// An authored roof spanning a rectangle of cells, dropped as one piece on
        /// a same-height region of a building. Author the prefab centred on its
        /// footprint so it rotates cleanly about its middle.
        /// </summary>
        [System.Serializable]
        public class RoofConfiguration
        {
            [Tooltip("The multi-cell roof prefab, authored centred on its footprint.")]
            public GameObject prefab;

            [Tooltip("Footprint width in cells (X).")]
            [Min(1)] public int width = 2;

            [Tooltip("Footprint depth in cells (Z).")]
            [Min(1)] public int depth = 2;

            [Tooltip("For square footprints, also try 90/180/270 placements for variety. Ignored for non-square footprints (rotating would change the footprint).")]
            public bool allowRotation = true;

            public int Area => Mathf.Max(1, width) * Mathf.Max(1, depth);
        }

        // Arcade grid and carving state.
        private Room3D[,] rooms;

        // Footprint + obstacle masks (rules lane only). active = this cell is
        // part of the level (a room is instantiated); pit = this active cell is
        // an obstacle the walkable graph carves around. On the hub lane every
        // cell is active and none is a pit, so behaviour is unchanged.
        private bool[,] active;
        private bool[,] pit;

        // Obstacle-first PROCEDURAL building cells (rules lane only): sealed cells
        // excluded from the walkable graph exactly like pits, so the maze routes
        // around them. Post-carve OpenProceduralBuildings opens each contiguous
        // group into one hall with a single carved doorway, and they keep their
        // facade + upper-floor massing. Adjacent placements are kept 1 cell apart
        // so each block group is exactly one building.
        private bool[,] block;

        // Obstacle-first AUTHORED building footprint cells (rules lane only):
        // reserved like blocks, but rendered by one hand-authored prefab instead of
        // per-cell rooms, so BuildRoomGrid instantiates no Room3D here. Their
        // entrances are the prefab's own WallSocket openings; the generator opens
        // the adjacent street to each. Null on the hub.
        private bool[,] authored;

        // Cells belonging to an opened outdoor PLAZA (rules lane only). Null on the
        // hub. Plaza interior edges are opened and left frameless like an open room,
        // but the region stays exterior - classified Plaza, no roof/building dress.
        private bool[,] plazaMask;

        // Per procedural-building cell (rules lane only): the vertical recipe of
        // the upper stack. buildingFull = number of full walled UpperCell storeys
        // (0 = roof sits on the ground block; higher = a tower); buildingHalf = a
        // half storey slipped in below the roof; buildingFlat = leave the top open
        // (no roof). 0/false everywhere but on buildings; all null on the hub.
        // Purely cosmetic exterior massing.
        private int[,] buildingFull;
        private bool[,] buildingHalf;
        private bool[,] buildingFlat;

        // Final per-cell space classification (see SpaceType), resolved by a
        // post-carve pass from the active/pit/block/authored/plaza masks so gameplay
        // systems can query narrow-street vs plaza vs indoors vs building without
        // re-deriving it. Null before the first generation; every active cell on the
        // hub lane is a NarrowStreet.
        private SpaceType[,] spaceType;

        // One placed authored building: its footprint rectangle (origin + size in
        // cells) and the prefab instantiated over it post-carve. Rules lane only;
        // cleared and refilled each generation by ChooseBuildings.
        private struct AuthoredPlacement
        {
            public int OriginX;
            public int OriginZ;
            public int Width;
            public int Depth;
            public GameObject Prefab;
        }
        private readonly List<AuthoredPlacement> authoredPlacements = new List<AuthoredPlacement>();

        private readonly Stack<Room3D> stack = new Stack<Room3D>();
        private readonly List<GameObject> validRoomPrefabs = new List<GameObject>();

        // Room W (x) and L (z).
        private float roomWidth;
        private float roomLength;

        private Transform generatedRoomsParent;
        private Vector2Int centerRoomIndex = Vector2Int.zero;
        private Vector2Int startRoomIndex = Vector2Int.zero;
        private Vector2Int endRoomIndex = Vector2Int.zero;
        private bool generating;
        private ArcadeMazeRules activeRules;
        private Action generationCompleteCallback;

        public Transform GeneratedRoomsParent => generatedRoomsParent;
        public Room3D[,] Rooms => rooms;
        public bool IsGenerating => generating;
        public Vector2Int StartRoomIndex => startRoomIndex;
        public Vector2Int EndRoomIndex => endRoomIndex;
        public float RoomWidth => roomWidth;
        public float RoomLength => roomLength;

        /// <summary>The generator's final per-cell space classification, indexed [x, z]. Null until a generation has completed.</summary>
        public SpaceType[,] SpaceTypes => spaceType;

        /// <summary>Space classification for one cell; <see cref="SpaceType.None"/> when out of bounds or before a generation has run.</summary>
        public SpaceType GetSpaceType(int x, int z) =>
            spaceType != null && InBounds(x, z) ? spaceType[x, z] : SpaceType.None;

        /// <summary>Space classification for one cell index (grid x in .x, grid z in .y).</summary>
        public SpaceType GetSpaceType(Vector2Int index) => GetSpaceType(index.x, index.y);

        private void Start()
        {
            if (autoGenerateOnStart)
            {
                CreateArcade();
            }
            else if (generatedRoomsParent == null)
            {
                // Adopt a pre-baked "Generated Rooms" (the designer preview) only
                // when nothing has generated yet. Guarding on null keeps Start
                // from clobbering a parent an earlier Awake-time generation
                // already built - and, on scene reload, from latching onto the
                // deferred-Destroy baked node before it is torn down.
                generatedRoomsParent = transform.Find("Generated Rooms");
            }
        }

        private void Update()
        {
            // Modern Input System approach.
            if (allowRuntimeKeyboardRegenerate &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame &&
                !generating)
            {
                CreateArcade();
            }
        }

        public void CreateArcade()
        {
            if (generating)
            {
                Debug.Log("Already generating arcade. Please wait.");
                return;
            }

            activeRules = null;
            generationCompleteCallback = null;

            if (!PrepareGeneration(respawnPlayerAtStartOnRegenerate))
            {
                return;
            }

            StartCoroutine(Coroutine_ArcadeGen());
        }

        public bool GenerateWithRules(ArcadeMazeRules rules, Action onComplete = null)
        {
            if (generating)
            {
                Debug.Log("Already generating arcade. Please wait.");
                return false;
            }

            if (rules == null)
            {
                Debug.LogWarning("ArcadeGen3D GenerateWithRules needs a rules object.", this);
                return false;
            }

            activeRules = rules;
            generationCompleteCallback = onComplete;

            if (!PrepareGeneration(rules.respawnPlayerAtStart))
            {
                ClearActiveGenerationRequest();
                return false;
            }

            if (!Application.isPlaying)
            {
                RunGenerationToCompletion();
                FinishGeneration();
                return true;
            }

            StartCoroutine(Coroutine_ArcadeGen());
            return true;
        }

        [ContextMenu("Regenerate Maze")]
        public bool RegenerateMazeFromInspector()
        {
            activeRules = null;
            generationCompleteCallback = null;

            if (Application.isPlaying)
            {
                CreateArcade();
                return true;
            }

            if (!PrepareGeneration(respawnPlayerAtStartOnRegenerate))
            {
                return false;
            }

            // Route through the same completion path as every other lane so the
            // post-carve passes run here too (inert on this rules-null lane).
            RunGenerationToCompletion();
            FinishGeneration();
            return true;
        }

        private bool PrepareGeneration(bool respawnPlayer)
        {
            if (!RebuildRooms())
            {
                return false;
            }

            ResetRoomsForGeneration();
            ApplyOptionalOuterOpenings();

            if (respawnPlayer)
            {
                RespawnPlayerAtStart();
            }

            rooms[startRoomIndex.x, startRoomIndex.y].visited = true;
            stack.Push(rooms[startRoomIndex.x, startRoomIndex.y]);
            return true;
        }

        private bool RebuildRooms()
        {
            stack.Clear();

            if (CurrentNumX <= 0 || CurrentNumZ <= 0)
            {
                Debug.LogWarning("ArcadeGen3D needs a maze size greater than 0.");
                DestroyGeneratedRooms();
                rooms = null;
                return false;
            }

            if (!RefreshValidRoomPrefabs())
            {
                DestroyGeneratedRooms();
                rooms = null;
                return false;
            }

            if (!GetRoomSize(validRoomPrefabs[0]))
            {
                DestroyGeneratedRooms();
                rooms = null;
                return false;
            }

            SelectSpecialRoomIndices();
            // Buildings are chosen after pits, but the pit connectivity guard reads
            // the block/authored masks - clear last generation's (which may be a
            // different size as the maze grows) so the guard sees "no buildings yet"
            // instead of indexing a stale array out of bounds.
            block = null;
            authored = null;
            BuildActiveMask();     // organic footprint (rules lane); hub -> all active, no RNG
            ChoosePitCells();      // obstacle-first pits (rules lane); hub -> none, no RNG
            ChooseBuildings();     // obstacle-first buildings (rules lane); hub -> none, no RNG
            CreateGeneratedRoomsParent();
            BuildRoomGrid();
            return true;
        }

        // ---- Footprint mask + obstacle-first pits -----------------------
        // Both run for every lane but stay inert on the hub: BuildActiveMask
        // fills a full-rectangle mask and returns before any RNG when
        // organicFootprint is off, and ChoosePitCells returns before any RNG at
        // pit count 0. So the hub consumes zero new UnityEngine.Random draws and
        // its maze is generated exactly as before.

        // Marks which cells are part of the level. Off the organic lane every
        // cell is active (the classic rectangle). On it, a connected blob is
        // grown outward from the centre so the outline is irregular; start is
        // the seed and exit is the farthest active cell from it.
        private void BuildActiveMask()
        {
            active = new bool[CurrentNumX, CurrentNumZ];

            if (!CurrentOrganicFootprint)
            {
                for (int x = 0; x < CurrentNumX; x++)
                {
                    for (int z = 0; z < CurrentNumZ; z++)
                    {
                        active[x, z] = true;
                    }
                }

                return;
            }

            Vector2Int seed = GetCenterRoomIndex();
            int total = CurrentNumX * CurrentNumZ;
            int targetActive = Mathf.Clamp(Mathf.RoundToInt(total * CurrentFootprintFill), 1, total);

            active[seed.x, seed.y] = true;
            int activeCount = 1;

            // Random growth from the seed: pull a random cell off the frontier,
            // activate it, push its inactive neighbours. Produces a connected,
            // organic blob rather than a rectangle.
            List<Vector2Int> frontier = new List<Vector2Int>();
            AddInactiveNeighborsToFrontier(seed, frontier);

            while (activeCount < targetActive && frontier.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, frontier.Count);
                Vector2Int cell = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);

                if (active[cell.x, cell.y])
                {
                    continue;
                }

                active[cell.x, cell.y] = true;
                activeCount++;
                AddInactiveNeighborsToFrontier(cell, frontier);
            }

            // Start at the seed; exit is the farthest active cell from it so the
            // walk spans the blob. Both are guaranteed active.
            startRoomIndex = seed;
            endRoomIndex = FarthestActiveCellFrom(seed);
        }

        private void AddInactiveNeighborsToFrontier(Vector2Int cell, List<Vector2Int> frontier)
        {
            foreach (Room3D.Directions dir in CardinalDirections)
            {
                if (TryGetNeighbor(cell.x, cell.y, dir, out int nx, out int nz) && !active[nx, nz])
                {
                    frontier.Add(new Vector2Int(nx, nz));
                }
            }
        }

        // Grid BFS over active cells (adjacency only, no walls exist yet).
        private Vector2Int FarthestActiveCellFrom(Vector2Int origin)
        {
            bool[,] seen = new bool[CurrentNumX, CurrentNumZ];
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            seen[origin.x, origin.y] = true;
            frontier.Enqueue(origin);
            Vector2Int farthest = origin;

            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                farthest = cell; // BFS dequeues in non-decreasing distance order.

                foreach (Room3D.Directions dir in CardinalDirections)
                {
                    if (TryGetNeighbor(cell.x, cell.y, dir, out int nx, out int nz) &&
                        active[nx, nz] && !seen[nx, nz])
                    {
                        seen[nx, nz] = true;
                        frontier.Enqueue(new Vector2Int(nx, nz));
                    }
                }
            }

            return farthest;
        }

        // Obstacle-first pit selection: pick pit cells BEFORE the carve so the
        // walkable graph is built around them and the exit is reachable by
        // construction. A candidate is kept only if the remaining walkable set
        // stays one connected region (a pit may never island cells off), so no
        // amount of RNG can block progression.
        private void ChoosePitCells()
        {
            pit = new bool[CurrentNumX, CurrentNumZ];

            int target = CurrentPitCount;
            if (target <= 0)
            {
                return;
            }

            if (CurrentPitVoidPrefab == null)
            {
                Debug.LogWarning("ArcadeGen3D: pit count is above 0 but no Pit Void Prefab is assigned; skipping pits.", this);
                return;
            }

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);
                    if (!active[x, z] || cell == startRoomIndex || cell == endRoomIndex)
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            ShuffleInPlace(candidates);

            int placed = 0;
            foreach (Vector2Int cell in candidates)
            {
                if (placed >= target)
                {
                    break;
                }

                pit[cell.x, cell.y] = true;
                if (WalkableRegionConnected())
                {
                    placed++;
                }
                else
                {
                    pit[cell.x, cell.y] = false;
                }
            }
        }

        // One building to place: its footprint size in cells, plus (for authored
        // buildings) the prefab that renders it. Procedural requests carry a null
        // prefab and are drawn straight onto the per-cell room grid.
        private struct BuildingRequest
        {
            public int Width;
            public int Depth;
            public GameObject Prefab;
            public bool IsAuthored;
        }

        // Obstacle-first BUILDING placement: reserve rectangular footprints the same
        // way pits/blocks are reserved (the carve routes around them; a footprint is
        // kept only if the remaining walkable set stays one connected region, so a
        // building can never seal the exit off). Runs AFTER pits so the guard sees
        // them. Both procedural and authored buildings share this pass; larger
        // footprints are tried first so they claim open space before small ones fill
        // the gaps - the "larger buildings where space allows" behaviour, with no
        // size ramp. Each building keeps a 1-cell separation ring so it stays its own
        // group with streets wrapping it. Inert on the hub: no requests -> returns
        // before any RNG.
        private void ChooseBuildings()
        {
            block = new bool[CurrentNumX, CurrentNumZ];
            authored = new bool[CurrentNumX, CurrentNumZ];
            authoredPlacements.Clear();

            List<BuildingRequest> requests = BuildBuildingRequests();
            if (requests.Count == 0)
            {
                return;
            }

            // Largest footprint first (stable enough - ties keep list order).
            requests.Sort((a, b) => (b.Width * b.Depth).CompareTo(a.Width * a.Depth));

            List<Vector2Int> origins = new List<Vector2Int>(CurrentNumX * CurrentNumZ);
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    origins.Add(new Vector2Int(x, z));
                }
            }

            ShuffleInPlace(origins);

            foreach (BuildingRequest request in requests)
            {
                TryPlaceBuilding(request, origins);
            }
        }

        // Fisher-Yates over the maze RNG stream (UnityEngine.Random), drawing exactly
        // as the inline loops it replaced so obstacle placement is byte-for-byte
        // unchanged. Only ever runs on the rules lane (the hub returns before any
        // shuffle), so it never perturbs the hub carve.
        private static void ShuffleInPlace<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Rolls the buildings to attempt this generation. Procedural footprints are
        // drawn from the configured size range; authored footprints are read from
        // each chosen prefab's bounds. Draws no RNG when both counts are 0 (the hub).
        private List<BuildingRequest> BuildBuildingRequests()
        {
            List<BuildingRequest> requests = new List<BuildingRequest>();

            int gridMin = Mathf.Max(1, Mathf.Min(CurrentNumX, CurrentNumZ));
            int procMin = Mathf.Clamp(CurrentBuildingMinSize, 1, gridMin);
            int procMax = Mathf.Clamp(CurrentBuildingMaxSize, procMin, gridMin);

            int procCount = CurrentProceduralBuildingCount;
            for (int i = 0; i < procCount; i++)
            {
                requests.Add(new BuildingRequest
                {
                    Width = UnityEngine.Random.Range(procMin, procMax + 1),
                    Depth = UnityEngine.Random.Range(procMin, procMax + 1),
                    Prefab = null,
                    IsAuthored = false,
                });
            }

            List<GameObject> pool = CurrentAuthoredBuildings;
            int authoredCount = CurrentAuthoredBuildingCount;
            if (pool != null && pool.Count > 0)
            {
                for (int i = 0; i < authoredCount; i++)
                {
                    GameObject prefab = PickAuthoredBuilding(pool);
                    if (prefab == null)
                    {
                        continue;
                    }

                    if (!TryGetPrefabCellFootprint(prefab, out int w, out int d))
                    {
                        Debug.LogWarning($"ArcadeGen3D: authored building {prefab.name} has no enabled renderers to size " +
                            "its footprint; skipping it. Give the prefab visible geometry sized to whole maze cells.", this);
                        continue;
                    }

                    requests.Add(new BuildingRequest { Width = w, Depth = d, Prefab = prefab, IsAuthored = true });
                }
            }

            return requests;
        }

        // Uniform pick from the authored pool, skipping empty slots.
        private GameObject PickAuthoredBuilding(List<GameObject> pool)
        {
            int valid = 0;
            foreach (GameObject prefab in pool)
            {
                if (prefab != null)
                {
                    valid++;
                }
            }

            if (valid == 0)
            {
                return null;
            }

            int target = UnityEngine.Random.Range(0, valid);
            foreach (GameObject prefab in pool)
            {
                if (prefab == null)
                {
                    continue;
                }

                if (target-- == 0)
                {
                    return prefab;
                }
            }

            return null;
        }

        // Footprint in whole cells from a prefab's renderer bounds (same bounds math
        // as GetRoomSize). Rounds to the nearest cell and clamps to at least 1x1.
        private bool TryGetPrefabCellFootprint(GameObject prefab, out int w, out int d)
        {
            w = 0;
            d = 0;
            if (roomWidth <= 0f || roomLength <= 0f
                || !TryGetPrefabBounds(prefab, out Vector3 minBounds, out Vector3 maxBounds))
            {
                return false;
            }

            w = Mathf.Max(1, Mathf.RoundToInt((maxBounds.x - minBounds.x) / roomWidth));
            d = Mathf.Max(1, Mathf.RoundToInt((maxBounds.z - minBounds.z) / roomLength));
            return true;
        }

        // Scans shuffled origins for the first spot this building fits + keeps the
        // walkable region connected, then reserves it. Authored placements are
        // recorded for the post-carve instantiation pass. No-op if nothing fits.
        private bool TryPlaceBuilding(BuildingRequest request, List<Vector2Int> origins)
        {
            foreach (Vector2Int origin in origins)
            {
                int ox = origin.x;
                int oz = origin.y;
                if (!BuildingFootprintFits(ox, oz, request.Width, request.Depth))
                {
                    continue;
                }

                SetBuildingCells(ox, oz, request.Width, request.Depth, request.IsAuthored, true);
                if (!WalkableRegionConnected())
                {
                    SetBuildingCells(ox, oz, request.Width, request.Depth, request.IsAuthored, false);
                    continue;
                }

                if (request.IsAuthored)
                {
                    authoredPlacements.Add(new AuthoredPlacement
                    {
                        OriginX = ox,
                        OriginZ = oz,
                        Width = request.Width,
                        Depth = request.Depth,
                        Prefab = request.Prefab,
                    });
                }

                return true;
            }

            return false;
        }

        // A footprint fits when every cell is clear buildable ground (active,
        // non-pit, non-building, not the start/exit) and a 1-cell ring around it
        // holds no OTHER building, so each building stays a distinct group with a
        // street frontage.
        private bool BuildingFootprintFits(int ox, int oz, int w, int d)
        {
            if (ox < 0 || oz < 0 || ox + w > CurrentNumX || oz + d > CurrentNumZ)
            {
                return false;
            }

            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + d; z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);
                    if (!active[x, z] || pit[x, z] || block[x, z] || authored[x, z]
                        || cell == startRoomIndex || cell == endRoomIndex)
                    {
                        return false;
                    }
                }
            }

            for (int x = ox - 1; x <= ox + w; x++)
            {
                for (int z = oz - 1; z <= oz + d; z++)
                {
                    if (x < 0 || z < 0 || x >= CurrentNumX || z >= CurrentNumZ)
                    {
                        continue;
                    }

                    bool insideFootprint = x >= ox && x < ox + w && z >= oz && z < oz + d;
                    if (insideFootprint)
                    {
                        continue;
                    }

                    if (block[x, z] || authored[x, z])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void SetBuildingCells(int ox, int oz, int w, int d, bool authoredBuilding, bool value)
        {
            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + d; z++)
                {
                    if (authoredBuilding)
                    {
                        authored[x, z] = value;
                    }
                    else
                    {
                        block[x, z] = value;
                    }
                }
            }
        }

        // Flood the walkable cells (active and not an obstacle) from the start over
        // grid adjacency and confirm the flood covers every walkable cell. If it
        // does, the pre-carve graph is one piece and a spanning tree will reach the
        // exit and every other room. Obstacles = pits AND solid blocks; block is
        // still null while pits are being chosen, so it is guarded.
        private bool WalkableRegionConnected()
        {
            int walkableTotal = 0;
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (WalkableForGuard(x, z))
                    {
                        walkableTotal++;
                    }
                }
            }

            if (walkableTotal == 0 || !WalkableForGuard(startRoomIndex.x, startRoomIndex.y))
            {
                return false;
            }

            bool[,] seen = new bool[CurrentNumX, CurrentNumZ];
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            seen[startRoomIndex.x, startRoomIndex.y] = true;
            frontier.Enqueue(startRoomIndex);
            int reached = 1;

            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                foreach (Room3D.Directions dir in CardinalDirections)
                {
                    if (TryGetNeighbor(cell.x, cell.y, dir, out int nx, out int nz) &&
                        WalkableForGuard(nx, nz) && !seen[nx, nz])
                    {
                        seen[nx, nz] = true;
                        reached++;
                        frontier.Enqueue(new Vector2Int(nx, nz));
                    }
                }
            }

            return reached == walkableTotal;
        }

        // Raw walkable test used by the pre-carve connectivity guard, before the
        // room grid exists. block/authored may still be null while earlier obstacle
        // passes run, so both are null-guarded.
        private bool WalkableForGuard(int x, int z)
        {
            return active[x, z] && !pit[x, z]
                && (block == null || !block[x, z])
                && (authored == null || !authored[x, z]);
        }

        private bool InBounds(int x, int z) => x >= 0 && x < CurrentNumX && z >= 0 && z < CurrentNumZ;
        private bool IsActiveCell(int x, int z) => InBounds(x, z) && active != null && active[x, z];
        private bool IsPitCell(int x, int z) => InBounds(x, z) && pit != null && pit[x, z];
        private bool IsSolidBlockCell(int x, int z) => InBounds(x, z) && block != null && block[x, z];
        private bool IsAuthoredCell(int x, int z) => InBounds(x, z) && authored != null && authored[x, z];

        // Walkable = part of the level and not an obstacle. Pits, procedural blocks
        // and authored footprints are all obstacles the carve routes around; they
        // differ only in how they are revealed (a hole, a sealed mass the player can
        // later enter through one door, or an authored prefab).
        private bool IsWalkable(int x, int z) =>
            IsActiveCell(x, z) && !IsPitCell(x, z) && !IsSolidBlockCell(x, z) && !IsAuthoredCell(x, z);

        private bool RefreshValidRoomPrefabs()
        {
            validRoomPrefabs.Clear();

            List<GameObject> roomPrefabs = CurrentPossibleRoomPrefabs;
            if (roomPrefabs != null)
            {
                foreach (GameObject possibleRoomPrefab in roomPrefabs)
                {
                    AddValidRoomPrefab(possibleRoomPrefab);
                }
            }

            if (activeRules == null && validRoomPrefabs.Count == 0)
            {
                AddValidRoomPrefab(roomPrefab);
            }

            if (validRoomPrefabs.Count == 0)
            {
                Debug.LogWarning("ArcadeGen3D needs at least one room prefab with a Room3D component.");
                return false;
            }

            return true;
        }

        private bool AddValidRoomPrefab(GameObject possibleRoomPrefab)
        {
            if (possibleRoomPrefab == null)
            {
                return false;
            }

            if (validRoomPrefabs.Contains(possibleRoomPrefab))
            {
                return true;
            }

            if (!possibleRoomPrefab.TryGetComponent(out Room3D _))
            {
                Debug.LogWarning($"{possibleRoomPrefab.name} was skipped because it does not have a Room3D component.");
                return false;
            }

            validRoomPrefabs.Add(possibleRoomPrefab);
            return true;
        }

        private bool GetRoomSize(GameObject sizeSourcePrefab)
        {
            if (!TryGetPrefabBounds(sizeSourcePrefab, out Vector3 minBounds, out Vector3 maxBounds))
            {
                Debug.LogWarning($"{sizeSourcePrefab.name} does not have any enabled renderers to calculate room size from.");
                return false;
            }

            roomWidth = maxBounds.x - minBounds.x;
            roomLength = maxBounds.z - minBounds.z;

            if (roomWidth <= 0f || roomLength <= 0f)
            {
                Debug.LogWarning($"{sizeSourcePrefab.name} produced an invalid room size.");
                return false;
            }

            return true;
        }

        // World-space AABB of a prefab's enabled renderers - the shared accumulation
        // behind both room sizing and authored-building footprints. False when the
        // prefab is null or has no enabled renderers.
        private static bool TryGetPrefabBounds(GameObject prefab, out Vector3 min, out Vector3 max)
        {
            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;
            if (prefab == null)
            {
                return false;
            }

            bool found = false;
            foreach (Renderer ren in prefab.GetComponentsInChildren<Renderer>())
            {
                if (!ren.enabled)
                {
                    continue;
                }

                found = true;
                min = Vector3.Min(min, ren.bounds.min);
                max = Vector3.Max(max, ren.bounds.max);
            }

            return found;
        }

        private void CreateGeneratedRoomsParent()
        {
            DestroyGeneratedRooms();
            generatedRoomsParent = new GameObject("Generated Rooms").transform;
            generatedRoomsParent.SetParent(transform, false);
        }

        private void BuildRoomGrid()
        {
            rooms = new Room3D[CurrentNumX, CurrentNumZ];

            for (int x = 0; x < CurrentNumX; ++x)
            {
                for (int z = 0; z < CurrentNumZ; ++z)
                {
                    // Cells outside the footprint mask are not part of the level.
                    if (active != null && !active[x, z])
                    {
                        continue;
                    }

                    // Authored-building footprint: the hand-authored prefab owns this
                    // space, so instantiate no per-cell room here (PlaceAuthoredBuildings
                    // drops the prefab post-carve). rooms[x,z] stays null - every pass
                    // already guards for that.
                    if (authored != null && authored[x, z])
                    {
                        continue;
                    }

                    GameObject selectedPrefab = GetRoomPrefabForCell(x, z);
                    Vector3 roomLocalPosition = GetRoomLocalPosition(x, z);
                    Vector3 roomWorldPosition = generatedRoomsParent.TransformPoint(roomLocalPosition);

                    GameObject room = Instantiate(
                        selectedPrefab,
                        roomWorldPosition,
                        generatedRoomsParent.rotation,
                        generatedRoomsParent);

                    room.transform.localPosition = roomLocalPosition;
                    room.transform.localRotation = Quaternion.identity;
                    room.transform.localScale = selectedPrefab.transform.localScale;

                    room.name = "Room_" + x.ToString() + "_" + z.ToString();
                    rooms[x, z] = room.GetComponent<Room3D>();
                    rooms[x, z].Index = new Vector3Int(x, 0, z);

                    if (new Vector2Int(x, z) == endRoomIndex &&
                        room.TryGetComponent(out EndRoomExitClerkActivator clerkActivator))
                    {
                        clerkActivator.PrepareClerksForGeneration();
                    }
                }
            }
        }

        private Vector3 GetRoomLocalPosition(int x, int z)
        {
            return new Vector3(
                (x - centerRoomIndex.x) * roomWidth,
                0f,
                (z - centerRoomIndex.y) * roomLength);
        }

        private GameObject GetRoomPrefabForCell(int x, int z)
        {
            Vector2Int cellIndex = new Vector2Int(x, z);

            if (cellIndex == startRoomIndex)
            {
                GameObject fixedRoomPrefab = GetStartRoomPrefab();
                if (fixedRoomPrefab != null)
                {
                    return fixedRoomPrefab;
                }
            }

            if (cellIndex == endRoomIndex)
            {
                GameObject fixedRoomPrefab = GetFixedRoomPrefab(CurrentLastRoomPrefab, "Last Room Prefab");
                if (fixedRoomPrefab != null)
                {
                    return fixedRoomPrefab;
                }
            }

            return GetRandomRoomPrefab();
        }

        private GameObject GetStartRoomPrefab()
        {
            if (CurrentSpecialRoomPlacementMode == SpecialRoomPlacementMode.GenerateFromCenter)
            {
                if (CurrentCenterRoomPrefab == null)
                {
                    Debug.LogWarning("Center Room Prefab is not assigned. Falling back to the first room prefab.", this);
                }
                else
                {
                    GameObject fixedRoomPrefab = GetFixedRoomPrefab(CurrentCenterRoomPrefab, "Center Room Prefab");
                    if (fixedRoomPrefab != null)
                    {
                        return fixedRoomPrefab;
                    }
                }
            }

            return GetFixedRoomPrefab(CurrentFirstRoomPrefab, "First Room Prefab");
        }

        private GameObject GetFixedRoomPrefab(GameObject fixedRoomPrefab, string slotName)
        {
            if (fixedRoomPrefab == null)
            {
                return null;
            }

            if (!fixedRoomPrefab.TryGetComponent(out Room3D _))
            {
                Debug.LogWarning($"{slotName} was skipped because {fixedRoomPrefab.name} does not have a Room3D component.");
                return null;
            }

            return fixedRoomPrefab;
        }

        private GameObject GetRandomRoomPrefab()
        {
            int totalSpawnWeight = 0;
            foreach (GameObject validRoomPrefab in validRoomPrefabs)
            {
                Room3D room = validRoomPrefab.GetComponent<Room3D>();
                totalSpawnWeight += room.SpawnWeight;
            }

            if (totalSpawnWeight <= 0)
            {
                Debug.LogWarning("All 3D room prefab spawn weights are 0. Falling back to the first valid room prefab.");
                return validRoomPrefabs[0];
            }

            // Convert weights into a single random roll.
            int selectedWeight = UnityEngine.Random.Range(0, totalSpawnWeight);
            foreach (GameObject validRoomPrefab in validRoomPrefabs)
            {
                Room3D room = validRoomPrefab.GetComponent<Room3D>();
                selectedWeight -= room.SpawnWeight;

                if (selectedWeight < 0)
                {
                    return validRoomPrefab;
                }
            }

            return validRoomPrefabs[0];
        }

        private void SelectSpecialRoomIndices()
        {
            centerRoomIndex = GetCenterRoomIndex();
            startRoomIndex = Vector2Int.zero;
            endRoomIndex = new Vector2Int(CurrentNumX - 1, CurrentNumZ - 1);

            if (CurrentSpecialRoomPlacementMode == SpecialRoomPlacementMode.GenerateFromCenter)
            {
                SelectCenterStartRoomIndices();
                return;
            }

            if (CurrentSpecialRoomPlacementMode != SpecialRoomPlacementMode.RandomStartAndEnd)
            {
                return;
            }

            int cellCount = CurrentNumX * CurrentNumZ;
            if (cellCount < 2)
            {
                Debug.LogWarning("Random start/end placement needs at least two maze cells. Falling back to fixed corners.");
                return;
            }

            // Pick two different cells without a retry loop.
            int startFlatIndex = UnityEngine.Random.Range(0, cellCount);
            int endFlatIndex = UnityEngine.Random.Range(0, cellCount - 1);
            if (endFlatIndex >= startFlatIndex)
            {
                endFlatIndex++;
            }

            startRoomIndex = FlatIndexToRoomIndex(startFlatIndex);
            endRoomIndex = FlatIndexToRoomIndex(endFlatIndex);
        }

        private void SelectCenterStartRoomIndices()
        {
            startRoomIndex = centerRoomIndex;
            endRoomIndex = GetFarthestCornerFrom(startRoomIndex);
        }

        private Vector2Int GetCenterRoomIndex()
        {
            return new Vector2Int((CurrentNumX - 1) / 2, (CurrentNumZ - 1) / 2);
        }

        private Vector2Int GetFarthestCornerFrom(Vector2Int origin)
        {
            Vector2Int[] corners =
            {
                new Vector2Int(CurrentNumX - 1, CurrentNumZ - 1),
                new Vector2Int(0, CurrentNumZ - 1),
                new Vector2Int(CurrentNumX - 1, 0),
                Vector2Int.zero
            };

            Vector2Int farthestCorner = corners[0];
            int farthestDistance = GetManhattanDistance(origin, farthestCorner);

            for (int i = 1; i < corners.Length; i++)
            {
                int distance = GetManhattanDistance(origin, corners[i]);
                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthestCorner = corners[i];
                }
            }

            return farthestCorner;
        }

        private static int GetManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private Vector2Int FlatIndexToRoomIndex(int flatIndex)
        {
            return new Vector2Int(flatIndex % CurrentNumX, flatIndex / CurrentNumX);
        }

        private void ApplyOptionalOuterOpenings()
        {
            if (CurrentOpenStartOuterWall)
            {
                TryRemoveOuterRoomWall(startRoomIndex, CurrentStartOuterWallDirection, "start");
            }

            if (CurrentOpenEndOuterWall)
            {
                TryRemoveOuterRoomWall(endRoomIndex, CurrentEndOuterWallDirection, "end");
            }
        }

        private void TryRemoveOuterRoomWall(Vector2Int roomIndex, Room3D.Directions direction, string roomName)
        {
            if (direction == Room3D.Directions.NONE)
            {
                return;
            }

            if (!IsOuterWallDirection(roomIndex, direction))
            {
                Debug.LogWarning(
                    $"ArcadeGen3D skipped the {roomName} outer opening because {direction} is not outside the maze at {roomIndex}.",
                    this);
                return;
            }

            RemoveRoomWall(roomIndex.x, roomIndex.y, direction);
        }

        private bool IsOuterWallDirection(Vector2Int roomIndex, Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.NORTH:
                    return roomIndex.y == CurrentNumZ - 1;

                case Room3D.Directions.EAST:
                    return roomIndex.x == CurrentNumX - 1;

                case Room3D.Directions.SOUTH:
                    return roomIndex.y == 0;

                case Room3D.Directions.WEST:
                    return roomIndex.x == 0;

                default:
                    return false;
            }
        }

        private void DestroyGeneratedRooms()
        {
            if (generatedRoomsParent == null)
            {
                Transform existingGeneratedRoomsParent = transform.Find("Generated Rooms");
                if (existingGeneratedRoomsParent != null)
                {
                    generatedRoomsParent = existingGeneratedRoomsParent;
                }
            }

            if (generatedRoomsParent == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedRoomsParent.gameObject);
            }
            else
            {
                DestroyImmediate(generatedRoomsParent.gameObject);
            }

            generatedRoomsParent = null;
        }

        private void RemoveRoomWall(int x, int z, Room3D.Directions dir)
        {
            if (dir != Room3D.Directions.NONE)
            {
                rooms[x, z].SetDirFlag(dir, false);
            }

            Room3D.Directions opp = Room3D.Directions.NONE;
            switch (dir)
            {
                case Room3D.Directions.NORTH:
                    if (z < CurrentNumZ - 1)
                    {
                        opp = Room3D.Directions.SOUTH;
                        ++z;
                    }
                    break;

                case Room3D.Directions.EAST:
                    if (x < CurrentNumX - 1)
                    {
                        opp = Room3D.Directions.WEST;
                        ++x;
                    }
                    break;

                case Room3D.Directions.SOUTH:
                    if (z > 0)
                    {
                        opp = Room3D.Directions.NORTH;
                        --z;
                    }
                    break;

                case Room3D.Directions.WEST:
                    if (x > 0)
                    {
                        opp = Room3D.Directions.EAST;
                        --x;
                    }
                    break;
            }

            if (opp != Room3D.Directions.NONE)
            {
                rooms[x, z].SetDirFlag(opp, false);
            }
        }

        // ---- Post-carve passes ------------------------------------------
        // All three self-disable on the hub lane: BraidMaze returns at rate 0
        // before consuming any RNG, DesignatePits returns at pit count 0, and
        // the conjoin pass returns the moment it finds no pit rooms. Order
        // matters - braid first so pit designation can exploit the new loops
        // when keeping a route to the exit, then conjoin adjacent pits.

        private static readonly Room3D.Directions[] CardinalDirections =
        {
            Room3D.Directions.NORTH,
            Room3D.Directions.SOUTH,
            Room3D.Directions.EAST,
            Room3D.Directions.WEST,
        };

        private void PostCarveProcessing()
        {
            if (rooms == null)
            {
                spaceType = null;
                return;
            }

            BraidMaze();
            OpenPlazas();
            RevealPits();
            OpenConjoinedPitShafts();
            MarkSolidBlocks();
            OpenProceduralBuildings();
            PlaceAuthoredBuildings();
            ClassifySpaces();
            DressWalls();
            BuildUpperFloors();
            VerifyExitReachable();
        }

        // Resolves every cell's SpaceType from the now-finalized masks and records
        // it on both the generator grid and each placed Room3D, so gameplay systems
        // can ask "narrow street, plaza, or indoors?" without touching the raw masks.
        // Runs after every mask-mutating pass (braid, plazas, pits, buildings) and
        // before the cosmetic-only dressing passes. A pure read of the masks: it
        // changes no walls and no reachability. Iterates the full grid so a reused
        // room whose role changed (or dropped out of the footprint) never keeps a
        // stale classification. On the hub lane every active cell resolves to
        // NarrowStreet.
        private void ClassifySpaces()
        {
            if (rooms == null)
            {
                spaceType = null;
                return;
            }

            spaceType = new SpaceType[CurrentNumX, CurrentNumZ];

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    SpaceType space = ClassifyCell(x, z);
                    spaceType[x, z] = space;

                    if (rooms[x, z] != null)
                    {
                        rooms[x, z].SetSpaceType(space);
                    }
                }
            }
        }

        // Priority resolves the mutually exclusive categories. Outside the footprint
        // is None. Buildings come first: an authored footprint is opaque massing the
        // prefab owns (SolidBuilding), while a procedural block cell was opened into
        // an enterable hall by OpenProceduralBuildings (BuildingInterior). Then pits
        // (a hole). The remaining walkable cells are exterior: a rare opened Plaza,
        // else the default NarrowStreet. The masks never overlap by construction, so
        // the order only fixes precedence for readability.
        private SpaceType ClassifyCell(int x, int z)
        {
            if (!IsActiveCell(x, z))
            {
                return SpaceType.None;
            }

            if (IsAuthoredCell(x, z))
            {
                return SpaceType.SolidBuilding;
            }

            if (IsSolidBlockCell(x, z))
            {
                return SpaceType.BuildingInterior;
            }

            if (IsPitCell(x, z))
            {
                return SpaceType.Pit;
            }

            if (IsPlazaCell(x, z))
            {
                return SpaceType.Plaza;
            }

            return SpaceType.NarrowStreet;
        }

        // Flags the pre-chosen block cells on their placed rooms. They are already
        // sealed (the carve never opened them), so this only records the state so
        // enemies never spawn trapped inside and the dressing pass can face the
        // facade outward. Inert when no blocks were chosen.
        private void MarkSolidBlocks()
        {
            if (block == null || rooms == null)
            {
                return;
            }

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (block[x, z] && rooms[x, z] != null)
                    {
                        rooms[x, z].MarkSolidBlock();
                    }
                }
            }
        }

        // Opens every procedural building into one enterable hall with a single
        // street doorway. For each contiguous block group: knock out all interior
        // shared walls (the open hall), then carve exactly one entrance from a
        // perimeter cell to an adjacent walkable street. The carve already routed
        // around the group, so the hall is a dead-end pocket off the main route -
        // reachability of the exit is untouched. Uses an isolated seeded Random so
        // entrance choice never perturbs the carve stream. Inert when no procedural
        // buildings were placed (the hub - block is null there).
        private void OpenProceduralBuildings()
        {
            if (block == null || rooms == null)
            {
                return;
            }

            System.Random rng = new System.Random(
                System.HashCode.Combine(CurrentNumX, CurrentNumZ, startRoomIndex, endRoomIndex, 0x8111D));

            bool[,] visited = new bool[CurrentNumX, CurrentNumZ];
            List<Vector2Int> group = new List<Vector2Int>();

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (visited[x, z] || !IsSolidBlockCell(x, z) || rooms[x, z] == null)
                    {
                        continue;
                    }

                    CollectGroup(x, z, visited, group, IsSolidBlockCell);
                    OpenBuildingHall(group);
                    CarveBuildingEntrance(group, rng);
                }
            }
        }

        // Removes every interior wall of one building group so it reads as a single
        // open hall. Each shared edge is touched once via the lower/left cell's
        // NORTH/EAST side; RemoveRoomWall opens both faces. Adjacent block cells are
        // always the same building (placements keep a 1-cell gap), so a block
        // neighbour is safe to open into.
        private void OpenBuildingHall(List<Vector2Int> group)
        {
            foreach (Vector2Int cell in group)
            {
                if (IsSolidBlockCell(cell.x, cell.y + 1))
                {
                    RemoveRoomWall(cell.x, cell.y, Room3D.Directions.NORTH);
                }

                if (IsSolidBlockCell(cell.x + 1, cell.y))
                {
                    RemoveRoomWall(cell.x, cell.y, Room3D.Directions.EAST);
                }
            }
        }

        // Carves the single entrance: from all perimeter edges that face a walkable
        // street (or plaza), pick one and open it both sides. If the group is fully
        // boxed in by pits/void/edge (rare - the separation ring normally guarantees
        // a street neighbour), it stays sealed, which is still a valid solid mass.
        private void CarveBuildingEntrance(List<Vector2Int> group, System.Random rng)
        {
            List<(Vector2Int cell, Room3D.Directions dir)> candidates =
                new List<(Vector2Int, Room3D.Directions)>();

            foreach (Vector2Int cell in group)
            {
                foreach (Room3D.Directions dir in CardinalDirections)
                {
                    if (TryGetNeighbor(cell.x, cell.y, dir, out int nx, out int nz)
                        && IsWalkable(nx, nz) && rooms[nx, nz] != null)
                    {
                        candidates.Add((cell, dir));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            var choice = candidates[rng.Next(candidates.Count)];
            RemoveRoomWall(choice.cell.x, choice.cell.y, choice.dir);
        }

        // Instantiates each authored building prefab over its reserved footprint and
        // connects its doorways to the street. The footprint was reserved obstacle-
        // first (so it is a dead-end pocket off the main route) and carries no per-
        // cell rooms, so only the street side of each opening is opened. Inert when
        // no authored buildings were placed.
        private void PlaceAuthoredBuildings()
        {
            if (authoredPlacements.Count == 0 || rooms == null || generatedRoomsParent == null)
            {
                return;
            }

            foreach (AuthoredPlacement placement in authoredPlacements)
            {
                InstantiateAuthoredBuilding(placement);
            }
        }

        private void InstantiateAuthoredBuilding(AuthoredPlacement placement)
        {
            if (placement.Prefab == null)
            {
                return;
            }

            // Centre the prefab on its footprint: the midpoint of the min- and
            // max-corner cell centres (handles even spans landing between cells),
            // reusing the same cell-positioning as the per-cell room grid.
            Vector3 localPos = 0.5f * (
                GetRoomLocalPosition(placement.OriginX, placement.OriginZ)
                + GetRoomLocalPosition(placement.OriginX + placement.Width - 1, placement.OriginZ + placement.Depth - 1));

            GameObject building = Instantiate(
                placement.Prefab,
                generatedRoomsParent.TransformPoint(localPos),
                generatedRoomsParent.rotation,
                generatedRoomsParent);
            building.transform.localPosition = localPos;
            building.transform.localRotation = Quaternion.identity;
            building.transform.localScale = placement.Prefab.transform.localScale;
            building.name = "Building_" + placement.OriginX + "_" + placement.OriginZ;

            // Scan the placed prefab's perimeter WallSockets flagged as authored
            // openings; open the adjacent street wall so the player can walk in.
            WallSocket[] sockets = building.GetComponentsInChildren<WallSocket>(true);
            foreach (WallSocket socket in sockets)
            {
                if (socket == null || !socket.AuthoredOpening)
                {
                    continue;
                }

                if (!TryMapSocketToFacadeEdge(placement, socket, out int edgeX, out int edgeZ, out Room3D.Directions outDir))
                {
                    continue;
                }

                if (!TryGetNeighbor(edgeX, edgeZ, outDir, out int nx, out int nz)
                    || !IsWalkable(nx, nz) || rooms[nx, nz] == null)
                {
                    continue; // opening faces the level edge, another building, or a pit - no street to open onto
                }

                // Building footprint cells have no Room3D, so open only the street
                // cell's wall that faces the building.
                rooms[nx, nz].SetDirFlag(Opposite(outDir), false);
            }
        }

        // Maps an authored opening socket to the footprint perimeter edge it sits on
        // by nearest edge-midpoint, returning that edge's cell and the OUTWARD
        // direction (footprint -> street). Works in the maze root's local space,
        // flattened to the ground plane, so the socket's height (and any parent
        // transform) never skews the match. Rejects sockets that are not close to
        // any facade edge (an interior wall the author flagged by mistake).
        private bool TryMapSocketToFacadeEdge(AuthoredPlacement placement, WallSocket socket,
            out int edgeX, out int edgeZ, out Room3D.Directions outDir)
        {
            edgeX = 0;
            edgeZ = 0;
            outDir = Room3D.Directions.NONE;

            Vector3 socketLocal = generatedRoomsParent.InverseTransformPoint(socket.transform.position);
            socketLocal.y = 0f;
            float best = float.PositiveInfinity;

            int ox = placement.OriginX;
            int oz = placement.OriginZ;
            int w = placement.Width;
            int d = placement.Depth;

            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + d; z++)
                {
                    foreach (Room3D.Directions dir in CardinalDirections)
                    {
                        if (!TryGetNeighbor(x, z, dir, out int nx, out int nz))
                        {
                            continue;
                        }

                        bool outsideFootprint = nx < ox || nx >= ox + w || nz < oz || nz >= oz + d;
                        if (!outsideFootprint)
                        {
                            continue; // interior edge, not a facade
                        }

                        // Edge midpoint in the same local, ground-plane space.
                        Vector3 edgeLocal = GetRoomLocalPosition(x, z) + FacadeEdgeOffset(dir);
                        float sqr = (edgeLocal - socketLocal).sqrMagnitude;
                        if (sqr < best)
                        {
                            best = sqr;
                            edgeX = x;
                            edgeZ = z;
                            outDir = dir;
                        }
                    }
                }
            }

            float tolerance = 0.5f * Mathf.Min(roomWidth, roomLength);
            return outDir != Room3D.Directions.NONE && best <= tolerance * tolerance;
        }

        // Half-cell offset from a cell centre to the midpoint of its wall on one side.
        private Vector3 FacadeEdgeOffset(Room3D.Directions dir)
        {
            switch (dir)
            {
                case Room3D.Directions.NORTH: return new Vector3(0f, 0f, roomLength * 0.5f);
                case Room3D.Directions.SOUTH: return new Vector3(0f, 0f, -roomLength * 0.5f);
                case Room3D.Directions.EAST: return new Vector3(roomWidth * 0.5f, 0f, 0f);
                case Room3D.Directions.WEST: return new Vector3(-roomWidth * 0.5f, 0f, 0f);
                default: return Vector3.zero;
            }
        }

        private static Room3D.Directions Opposite(Room3D.Directions dir)
        {
            switch (dir)
            {
                case Room3D.Directions.NORTH: return Room3D.Directions.SOUTH;
                case Room3D.Directions.SOUTH: return Room3D.Directions.NORTH;
                case Room3D.Directions.EAST: return Room3D.Directions.WEST;
                case Room3D.Directions.WEST: return Room3D.Directions.EAST;
                default: return Room3D.Directions.NONE;
            }
        }

        // Opens rare rectangular pockets of corridor into OUTDOOR PLAZAS: every
        // interior wall of the pocket is knocked out so it reads as one widened
        // open-air square, while the single-width corridors around it stay narrow
        // streets. Runs after braiding so it can open loops too. Because it only
        // REMOVES walls on cells the carve already connected, the exit stays
        // reachable by construction. Kept rare so narrow streets are the norm.
        // Inert on the hub: it clears the mask and returns before any RNG when
        // Plaza Count is 0.
        private void OpenPlazas()
        {
            plazaMask = null; // reset each generation; stays null when no plazas

            int target = CurrentPlazaCount;
            if (target <= 0 || rooms == null)
            {
                return;
            }

            plazaMask = new bool[CurrentNumX, CurrentNumZ];

            int minSize = Mathf.Clamp(CurrentPlazaMinSize, 2, Mathf.Max(2, Mathf.Min(CurrentNumX, CurrentNumZ)));
            int maxSize = Mathf.Clamp(CurrentPlazaMaxSize, minSize, Mathf.Max(minSize, Mathf.Min(CurrentNumX, CurrentNumZ)));

            int placed = 0;
            int maxAttempts = target * 12 + 8;
            for (int attempt = 0; attempt < maxAttempts && placed < target; attempt++)
            {
                int w = UnityEngine.Random.Range(minSize, maxSize + 1);
                int h = UnityEngine.Random.Range(minSize, maxSize + 1);
                int ox = UnityEngine.Random.Range(0, Mathf.Max(1, CurrentNumX - w + 1));
                int oz = UnityEngine.Random.Range(0, Mathf.Max(1, CurrentNumZ - h + 1));

                if (!IsPlazaRegionClear(ox, oz, w, h))
                {
                    continue;
                }

                CommitPlaza(ox, oz, w, h);
                placed++;
            }
        }

        // A region is usable only when every cell in it is walkable (active,
        // non-pit, non-building, instantiated) and not already part of another
        // plaza, so plazas are clean rectangles that never swallow a pit, a
        // building, the void, or each other.
        private bool IsPlazaRegionClear(int ox, int oz, int w, int h)
        {
            if (ox < 0 || oz < 0 || ox + w > CurrentNumX || oz + h > CurrentNumZ)
            {
                return false;
            }

            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + h; z++)
                {
                    if (!IsWalkable(x, z) || rooms[x, z] == null || plazaMask[x, z])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void CommitPlaza(int ox, int oz, int w, int h)
        {
            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + h; z++)
                {
                    plazaMask[x, z] = true;
                }
            }

            // Knock out every interior wall (each shared edge touched once via its
            // NORTH/EAST side). Removing walls only adds connectivity, so the plaza
            // is fully open and the maze stays solvable.
            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + h; z++)
                {
                    if (z + 1 < oz + h)
                    {
                        RemoveRoomWall(x, z, Room3D.Directions.NORTH);
                    }

                    if (x + 1 < ox + w)
                    {
                        RemoveRoomWall(x, z, Room3D.Directions.EAST);
                    }
                }
            }
        }

        private bool IsPlazaCell(int x, int z) => InBounds(x, z) && plazaMask != null && plazaMask[x, z];

        // Cosmetic-only reskin pass: after the walkable graph is fully carved,
        // braided and pitted, tell every wall's WallSocket which themed part to
        // show for its final OPEN/CLOSED + OUTER state. Inert on the hub - it
        // returns before any work when Dress Walls After Carve is off. Runs on a
        // dedicated System.Random so wall variety never touches UnityEngine.Random
        // and can never shift the carve stream (hub stays byte-identical).
        private void DressWalls()
        {
            if (!dressWallsAfterCarve || rooms == null)
            {
                return;
            }

            // Seed from the layout, not a live counter, so the same maze always
            // dresses the same way regardless of how many draws the carve spent.
            System.Random rng = new System.Random(
                System.HashCode.Combine(CurrentNumX, CurrentNumZ, startRoomIndex, endRoomIndex));

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null || room.IsPit)
                    {
                        continue; // leave pit rooms as authored - no arches over a drop
                    }

                    bool selfBlock = IsSolidBlockCell(x, z);

                    foreach (Room3D.Directions dir in CardinalDirections)
                    {
                        bool open = IsDoorOpen(x, z, dir);
                        bool hasNeighbor = TryGetNeighbor(x, z, dir, out int nx, out int nz);
                        bool outer = !hasNeighbor || !IsActiveCell(nx, nz);

                        // Where a solid block meets a walkable cell, the block owns
                        // the shared wall so its facade (with windows/arrowslits)
                        // faces out over the street. Otherwise this cell owns its
                        // NORTH/EAST shared edges (and any outer edge) and the
                        // neighbour owns SOUTH/WEST, so a shared wall/arch is built
                        // exactly once instead of doubled.
                        bool neighborBlock = hasNeighbor && IsSolidBlockCell(nx, nz);
                        bool owner = selfBlock != neighborBlock
                            ? selfBlock
                            : outer || dir == Room3D.Directions.NORTH || dir == Room3D.Directions.EAST;

                        // An interior edge of one continuous open space, hidden
                        // frameless on both sides: two cells of the same plaza, or
                        // two cells of the same procedural building (adjacent blocks
                        // are always the same building - placements keep a 1-cell
                        // gap). The single street doorway is block-vs-street, so it
                        // is NOT interior and still reads as a passage/arch.
                        bool interiorEdge =
                            (IsPlazaCell(x, z) && hasNeighbor && IsPlazaCell(nx, nz))
                            || (selfBlock && neighborBlock);

                        room.DressWall(dir, open, outer, owner, interiorEdge, deDoubleSharedWalls, rng);
                    }
                }
            }
        }

        // --- Upper floors: the lost-city buildings ----------------------
        // Solid blocks are sealed masses the carve routed around - ideal building
        // footprints. Here we stack authored cells on them - full UpperCells,
        // optional half UpperCells, capped by authored RoofCells - so they read as
        // multi-storey lost-city structures with windowed facades onto the
        // streets. Purely cosmetic EXTERIOR massing: the cells have no floor, the
        // player never enters them, and nothing here touches the walkable graph.
        // Runs post-carve on its own seeded Random (never UnityEngine.Random), so
        // the hub carve stays byte-identical. Inert on the hub and whenever upper
        // floors or wall dressing are off.
        private void BuildUpperFloors()
        {
            buildingFull = null;
            buildingHalf = null;
            buildingFlat = null;

            if (!buildUpperFloors || !dressWallsAfterCarve || rooms == null)
            {
                return; // feature off (or off on the hub) - stay silent
            }

            if (upperCellPrefab == null && roofHipPrefab == null && roofSteppedPrefab == null
                && roofSlopeLeftPrefab == null && roofSlopeRightPrefab == null)
            {
                Debug.LogWarning("ArcadeGen3D: 'Build Upper Floors' is on but no Upper Cell / Roof Cell prefabs are " +
                    "assigned - nothing to build. Assign the UpperCell and RoofCell kit under the Upper Floors header.", this);
                return;
            }

            System.Random rng = new System.Random(
                System.HashCode.Combine(CurrentNumX, CurrentNumZ, startRoomIndex, endRoomIndex, 0x5747));

            BuildBuildingMasks(rng);

            int spawnedBuildings = 0;
            if (buildingFull != null)
            {
                // Drop authored multi-cell roofs first; the cells they cover skip
                // their per-cell roof (and half storey) so the config reads as one
                // piece. Everything else gets its per-cell stack + roof.
                bool[,] consumed = new bool[CurrentNumX, CurrentNumZ];
                PlaceRoofConfigurations(consumed, rng);

                for (int x = 0; x < CurrentNumX; x++)
                {
                    for (int z = 0; z < CurrentNumZ; z++)
                    {
                        if (IsSolidBlockCell(x, z) && rooms[x, z] != null)
                        {
                            SpawnBuildingStack(x, z, consumed[x, z], rng);
                            spawnedBuildings++;
                        }
                    }
                }
            }

            if (spawnedBuildings == 0)
            {
                Debug.Log("ArcadeGen3D: Build Upper Floors ran but found no solid-block buildings to stack on. " +
                    "Solid blocks are what buildings sit on - check the rules' Solid Block Count is > 0, and note " +
                    "the edit-mode preview (rules-null) has no blocks, so buildings only appear in play.", this);
            }
        }

        // Rolls the vertical recipe of every solid-block building: flood-fill each
        // contiguous block, roll a base full-storey count and per-cell tower / half
        // / flat-top flags, so the skyline is mixed and some cells rise into towers
        // that neighbours lean their roofs into. Blocks are the only building
        // hosts for now; streets and plazas stay open. Draws only from the passed
        // Random, so the hub stays byte-identical.
        private void BuildBuildingMasks(System.Random rng)
        {
            if (block == null)
            {
                return; // no solid blocks -> masks stay null, no buildings
            }

            int numX = CurrentNumX;
            int numZ = CurrentNumZ;
            buildingFull = new int[numX, numZ];
            buildingHalf = new bool[numX, numZ];
            buildingFlat = new bool[numX, numZ];

            bool[,] visited = new bool[numX, numZ];
            List<Vector2Int> group = new List<Vector2Int>();
            for (int x = 0; x < numX; x++)
            {
                for (int z = 0; z < numZ; z++)
                {
                    if (visited[x, z] || !IsSolidBlockCell(x, z))
                    {
                        continue;
                    }

                    CollectGroup(x, z, visited, group, IsSolidBlockCell);
                    int baseFull = rng.NextDouble() < upperStoryChance ? 1 : 0;
                    foreach (Vector2Int cell in group)
                    {
                        // Cap at one walled upper storey so the 3rd level up is
                        // only ever a roof (per the kit's rule). A tower therefore
                        // only rises where the building's base is a single storey;
                        // raise this cap if you want taller towers.
                        int full = Mathf.Min(1, baseFull + (rng.NextDouble() < towerChance ? 1 : 0));
                        buildingFull[cell.x, cell.y] = full;
                        buildingHalf[cell.x, cell.y] = rng.NextDouble() < halfStoryChance;
                        buildingFlat[cell.x, cell.y] = rng.NextDouble() < flatTopChance;
                    }
                }
            }
        }

        // Realises one building cell's stack: the full UpperCell storeys, an
        // optional half storey, and (unless flat-topped) an authored roof cell,
        // each parented under the ground room so the whole stack tears down with
        // the maze on the next generation.
        private void SpawnBuildingStack(int x, int z, bool roofConsumed, System.Random rng)
        {
            int full = buildingFull[x, z];
            bool half = buildingHalf[x, z];
            bool flat = buildingFlat[x, z];
            float h = upperFloorHeight;

            for (int layer = 1; layer <= full; layer++)
            {
                SpawnWalledCell(upperCellPrefab, x, z, layer * h, layer, false, rng);
            }

            // A multi-cell config already caps this cell: build its walls but no
            // half storey and no per-cell roof, so the config sits flush on top.
            if (roofConsumed)
            {
                return;
            }

            float topY = (full + 1) * h; // top surface of the ground block + full storeys

            bool halfOnTop = false;
            if (half && upperCellHalfPrefab != null)
            {
                SpawnWalledCell(upperCellHalfPrefab, x, z, topY, full + 1, true, rng);
                topY += halfFloorHeight;
                halfOnTop = true;
            }

            if (flat)
            {
                SpawnFlatCap(x, z, topY, halfOnTop, rng);
            }
            else
            {
                SpawnRoof(x, z, topY, rng);
            }
        }

        // Tiles building footprints with the authored multi-cell roof configs,
        // largest area first so big roofs claim their regions before small ones
        // fill in. A region qualifies when every cell is a same-height,
        // non-flat-topped building cell not already consumed. Marks the covered
        // cells in `consumed` and drops the config prefab centred over them.
        private void PlaceRoofConfigurations(bool[,] consumed, System.Random rng)
        {
            if (roofConfigurations == null || roofConfigurations.Count == 0)
            {
                return;
            }

            List<RoofConfiguration> ordered = new List<RoofConfiguration>();
            foreach (RoofConfiguration config in roofConfigurations)
            {
                if (config != null && config.prefab != null && config.width >= 1 && config.depth >= 1)
                {
                    ordered.Add(config);
                }
            }

            ordered.Sort((a, b) => b.Area.CompareTo(a.Area));

            foreach (RoofConfiguration config in ordered)
            {
                for (int x = 0; x + config.width <= CurrentNumX; x++)
                {
                    for (int z = 0; z + config.depth <= CurrentNumZ; z++)
                    {
                        if (RegionFitsConfig(x, z, config.width, config.depth, consumed))
                        {
                            for (int cx = x; cx < x + config.width; cx++)
                            {
                                for (int cz = z; cz < z + config.depth; cz++)
                                {
                                    consumed[cx, cz] = true;
                                }
                            }

                            PlaceConfig(config, x, z, rng);
                        }
                    }
                }
            }
        }

        // A config region must be a solid rectangle of building cells at one
        // height, none flat-topped, none already consumed by another config.
        private bool RegionFitsConfig(int x0, int z0, int w, int d, bool[,] consumed)
        {
            int full = buildingFull[x0, z0];
            for (int x = x0; x < x0 + w; x++)
            {
                for (int z = z0; z < z0 + d; z++)
                {
                    if (consumed[x, z] || !IsSolidBlockCell(x, z) || rooms[x, z] == null
                        || buildingFull[x, z] != full || buildingFlat[x, z])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Drops the config prefab centred over the footprint at the region's roof
        // height, parented under the min-corner ground room. Square configs may
        // also take a 90/180/270 placement for variety.
        private void PlaceConfig(RoofConfiguration config, int x0, int z0, System.Random rng)
        {
            int full = buildingFull[x0, z0];
            int yaw = config.allowRotation && config.width == config.depth ? rng.Next(4) : 0;

            Vector3 centre = new Vector3(
                (config.width - 1) * 0.5f * roomWidth,
                (full + 1) * upperFloorHeight,
                (config.depth - 1) * 0.5f * roomLength);

            GameObject go = Instantiate(config.prefab, rooms[x0, z0].transform);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(0f, 90f * ((yaw + roofYawTrim) & 3), 0f);
            go.transform.localScale = config.prefab.transform.localScale;
            go.name = $"{config.prefab.name}_{x0}_{z0}";
        }

        // Places a flat roof cap on a flat-roofed building cell: the half-block
        // variant when the top layer is a half storey, otherwise the full block.
        // Falls back to a pitched roof if no flat cap prefab is assigned, so the
        // building is never left uncapped.
        private void SpawnFlatCap(int x, int z, float localY, bool halfOnTop, System.Random rng)
        {
            GameObject prefab = halfOnTop && roofFlatHalfBlockPrefab != null
                ? roofFlatHalfBlockPrefab
                : roofFlatBlockPrefab;
            prefab = prefab != null ? prefab : roofFlatHalfBlockPrefab;

            if (prefab == null)
            {
                SpawnRoof(x, z, localY, rng); // no flat caps assigned - pitch it instead
                return;
            }

            GameObject go = Instantiate(prefab, rooms[x, z].transform);
            go.transform.localPosition = Vector3.up * localY;
            go.transform.localRotation = Quaternion.Euler(0f, 90f * (roofYawTrim & 3), 0f);
            go.transform.localScale = prefab.transform.localScale;
            go.name = $"{prefab.name}_{x}_{z}";
        }

        // Instantiates one walled upper cell (full or half) at localY above the
        // ground cell and dresses its four walls: a wall is hidden interior where
        // the neighbouring building reaches this same layer, else a windowed
        // facade onto the street/sky. The cell substitutes its walls from whatever
        // its WallSockets carry (full walls on UpperCell, half walls on
        // UpperCell_Half). Passages never open in the sky.
        private void SpawnWalledCell(GameObject prefab, int x, int z, float localY, int layer, bool isHalf, System.Random rng)
        {
            if (prefab == null)
            {
                return;
            }

            Room3D ground = rooms[x, z];
            GameObject go = Instantiate(prefab, ground.transform);
            go.transform.localPosition = Vector3.up * localY;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = prefab.transform.localScale;
            go.name = $"{prefab.name}_{x}_{z}_L{layer}";

            Room3D cell = go.GetComponent<Room3D>();
            if (cell == null)
            {
                Debug.LogWarning($"{prefab.name} has no Room3D; upper cell left undressed.", prefab);
                return;
            }

            foreach (Room3D.Directions dir in CardinalDirections)
            {
                bool interior = false;
                if (TryGetNeighbor(x, z, dir, out int nx, out int nz) && IsSolidBlockCell(nx, nz))
                {
                    interior = isHalf
                        ? buildingFull[nx, nz] == buildingFull[x, z] && buildingHalf[nx, nz]
                        : buildingFull[nx, nz] >= layer;
                }

                cell.DressWall(dir, interior, !interior, true, interior, deDoubleSharedWalls, rng);
            }
        }

        // Places an authored roof cell on top of a building cell. Roof cells keep
        // their internals; we only choose which one and its yaw. A cell with a
        // taller building neighbour gets a single slope leaning its high edge into
        // that neighbour; a cell with exactly one same-height neighbour pairs a
        // Left/Right slope into a ridge with it; everything else takes a
        // self-contained hipped or stepped cap at any yaw.
        private void SpawnRoof(int x, int z, float localY, System.Random rng)
        {
            GameObject prefab = PlanRoof(x, z, rng, out int yawSteps);
            if (prefab == null)
            {
                return;
            }

            GameObject go = Instantiate(prefab, rooms[x, z].transform);
            go.transform.localPosition = Vector3.up * localY;
            go.transform.localRotation = Quaternion.Euler(0f, 90f * ((yawSteps + roofYawTrim) & 3), 0f);
            go.transform.localScale = prefab.transform.localScale;
            go.name = $"{prefab.name}_{x}_{z}";
        }

        private GameObject PlanRoof(int x, int z, System.Random rng, out int yawSteps)
        {
            int selfFull = buildingFull[x, z];

            // Lean-to into the tallest strictly-taller building neighbour.
            Room3D.Directions leanDir = Room3D.Directions.NONE;
            int tallest = selfFull;
            foreach (Room3D.Directions dir in CardinalDirections)
            {
                if (TryGetNeighbor(x, z, dir, out int nx, out int nz) && IsSolidBlockCell(nx, nz)
                    && buildingFull[nx, nz] > tallest)
                {
                    tallest = buildingFull[nx, nz];
                    leanDir = dir;
                }
            }

            if (leanDir != Room3D.Directions.NONE)
            {
                bool curve = rng.NextDouble() < curvedRoofChance;
                GameObject slope = curve ? (roofSlopeLeftCurvePrefab != null ? roofSlopeLeftCurvePrefab : roofSlopeLeftPrefab)
                                         : roofSlopeLeftPrefab;
                if (slope != null)
                {
                    yawSteps = YawForApex(true, leanDir); // a Left slope, apex into the taller neighbour
                    return slope;
                }
            }

            // Ridge pair with a lone same-height neighbour (a 2-cell building or the
            // end of a run): this cell and the partner face their high edges at the
            // shared edge, one Left and one Right so they mirror into a peak.
            Room3D.Directions partnerDir = Room3D.Directions.NONE;
            int sameHeight = 0;
            int partnerNx = 0, partnerNz = 0;
            foreach (Room3D.Directions dir in CardinalDirections)
            {
                if (TryGetNeighbor(x, z, dir, out int nx, out int nz) && IsSolidBlockCell(nx, nz)
                    && buildingFull[nx, nz] == selfFull)
                {
                    sameHeight++;
                    partnerDir = dir;
                    partnerNx = nx;
                    partnerNz = nz;
                }
            }

            if (sameHeight == 1)
            {
                bool useLeft = CellOrder(x, z) < CellOrder(partnerNx, partnerNz);
                bool curve = rng.NextDouble() < curvedRoofChance;
                GameObject slope =
                    useLeft ? (curve ? roofSlopeLeftCurvePrefab : roofSlopeLeftPrefab)
                            : (curve ? roofSlopeRightCurvePrefab : roofSlopeRightPrefab);
                slope = slope != null ? slope : (useLeft ? roofSlopeLeftPrefab : roofSlopeRightPrefab);
                if (slope != null)
                {
                    yawSteps = YawForApex(useLeft, partnerDir);
                    return slope;
                }
            }

            // Junction/interior cell of a bigger footprint (the corner of an L, the
            // middle of a 3-long run, the inside of a block): two or more
            // same-height neighbours mean no single clean ridge, so fill the gap
            // with a flat full block between the pitched extremities.
            if (sameHeight >= 2 && roofFlatBlockPrefab != null)
            {
                yawSteps = roofYawTrim & 3;
                return roofFlatBlockPrefab;
            }

            // Standalone-ish cap: a lone 1x1 house, or a tip whose only neighbours
            // are a different height - a little hipped or stepped roof.
            GameObject hip = PickHipOrStepped(rng);
            yawSteps = hip != null ? rng.Next(4) : 0;
            return hip;
        }

        // Grid direction as a 90-degree yaw index. Unity's +90 yaw turns +Z(N)
        // into +X(E), so the cycle is N,E,S,W = 0,1,2,3 and each step is +90.
        private static int DirIndex(Room3D.Directions dir)
        {
            switch (dir)
            {
                case Room3D.Directions.EAST: return 1;
                case Room3D.Directions.SOUTH: return 2;
                case Room3D.Directions.WEST: return 3;
                default: return 0; // NORTH (+Z)
            }
        }

        // Yaw (in 90-degree steps) that turns a roof slope so its authored apex
        // (high edge) faces targetDir. The kit's _L slopes are authored with the
        // apex toward +Z (NORTH); the _R slopes toward -Z (SOUTH). So a Left and
        // its mirror Right, each pointed at their shared edge, close into one
        // ridge. Adjust Roof Yaw Trim in the inspector if the whole kit still
        // reads rotated.
        private static int YawForApex(bool isLeft, Room3D.Directions targetDir)
        {
            int authored = isLeft ? DirIndex(Room3D.Directions.NORTH) : DirIndex(Room3D.Directions.SOUTH);
            return (DirIndex(targetDir) - authored + 4) & 3;
        }

        private GameObject PickHipOrStepped(System.Random rng)
        {
            if (roofHipPrefab != null && roofSteppedPrefab != null)
            {
                return rng.NextDouble() < 0.5 ? roofHipPrefab : roofSteppedPrefab;
            }

            return roofHipPrefab != null ? roofHipPrefab : roofSteppedPrefab;
        }

        private int CellOrder(int x, int z) => x * CurrentNumZ + z;

        // Flood-fills the connected group of cells around (x, z) for which the
        // predicate holds, marking them visited. The shared scratch list is
        // cleared first.
        private void CollectGroup(int x, int z, bool[,] visited,
            List<Vector2Int> group, System.Func<int, int, bool> member)
        {
            group.Clear();
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            frontier.Enqueue(new Vector2Int(x, z));
            visited[x, z] = true;

            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                group.Add(cell);

                foreach (Room3D.Directions dir in CardinalDirections)
                {
                    if (!TryGetNeighbor(cell.x, cell.y, dir, out int nx, out int nz)
                        || visited[nx, nz] || !member(nx, nz))
                    {
                        continue;
                    }

                    visited[nx, nz] = true;
                    frontier.Enqueue(new Vector2Int(nx, nz));
                }
            }
        }

        // Structural safety net. The walkable graph is carved to guarantee a
        // pit-free route to the exit, but if a future change ever broke that,
        // surface it loudly during development rather than ship an unwinnable
        // stage. Stripped from release player builds.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void VerifyExitReachable()
        {
            if (rooms == null || startRoomIndex == endRoomIndex)
            {
                return;
            }

            if (!IsReachable(startRoomIndex, endRoomIndex, allowPits: false))
            {
                Debug.LogError(
                    $"ArcadeGen3D: start {startRoomIndex} cannot reach exit {endRoomIndex} on foot after " +
                    "generation - the stage would be unwinnable. Check the footprint/pit passes.", this);
            }
        }

        // Knock open extra walls at dead-ends so the perfect maze grows loops.
        // Only dead-ends (0 or 1 open doorway) are braided, which is enough to
        // remove most single-path chokepoints while keeping the maze legible.
        private void BraidMaze()
        {
            float rate = CurrentBraidRate;
            if (rate <= 0f)
            {
                return;
            }

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (!IsWalkable(x, z) || rooms[x, z] == null)
                    {
                        continue; // never braid a pit or a masked-out cell
                    }

                    if (CountOpenDoors(x, z) > 1)
                    {
                        continue;
                    }

                    if (UnityEngine.Random.value > rate)
                    {
                        continue;
                    }

                    List<Room3D.Directions> closed = ClosedInteriorDirections(x, z);
                    if (closed.Count == 0)
                    {
                        continue;
                    }

                    Room3D.Directions dir = closed[UnityEngine.Random.Range(0, closed.Count)];
                    RemoveRoomWall(x, z, dir);
                }
            }
        }

        // Turns the cells chosen pre-carve by ChoosePitCells into real pits:
        // hide the floor, reveal the void underneath, and open the pit's walls
        // toward any active neighbour so the hole reads as an open drop in the
        // corridor rather than a sealed box. The carve already routed the
        // walkable graph around these cells, so the exit stays reachable; the
        // pit-free route just has to go the long way, stretching the journey.
        private void RevealPits()
        {
            if (pit == null)
            {
                return;
            }

            GameObject voidPrefab = CurrentPitVoidPrefab;
            if (voidPrefab == null)
            {
                return;
            }

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (!pit[x, z] || rooms[x, z] == null)
                    {
                        continue;
                    }

                    rooms[x, z].RevealPit(voidPrefab);

                    // Open the drop toward every active neighbour (walkable or a
                    // conjoined pit); sides facing the masked-out void, or a sealed
                    // solid block, keep their wall so the edge stays sealed.
                    foreach (Room3D.Directions dir in CardinalDirections)
                    {
                        if (TryGetNeighbor(x, z, dir, out int nx, out int nz) &&
                            IsActiveCell(nx, nz) && !IsSolidBlockCell(nx, nz) && rooms[nx, nz] != null)
                        {
                            RemoveRoomWall(x, z, dir);
                        }
                    }
                }
            }
        }

        // Merge neighbouring pits into one continuous shaft: a below-floor
        // retaining wall is dropped only where an open doorway joins two pits.
        // Every other pit side keeps its wall so the hole stays a contained
        // well instead of a floating grid of disconnected squares.
        private void OpenConjoinedPitShafts()
        {
            if (!AnyPitRooms())
            {
                return;
            }

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null || !room.IsPit)
                    {
                        continue;
                    }

                    foreach (Room3D.Directions dir in CardinalDirections)
                    {
                        bool conjoin =
                            TryGetNeighbor(x, z, dir, out int nx, out int nz) &&
                            rooms[nx, nz] != null &&
                            rooms[nx, nz].IsPit &&
                            IsDoorOpen(x, z, dir);

                        room.SetShaftOpen(dir, conjoin);
                    }
                }
            }
        }

        private bool AnyPitRooms()
        {
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (rooms[x, z] != null && rooms[x, z].IsPit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int CountOpenDoors(int x, int z)
        {
            int open = 0;
            foreach (Room3D.Directions dir in CardinalDirections)
            {
                if (IsDoorOpen(x, z, dir))
                {
                    open++;
                }
            }

            return open;
        }

        private List<Room3D.Directions> ClosedInteriorDirections(int x, int z)
        {
            List<Room3D.Directions> closed = new List<Room3D.Directions>(4);
            foreach (Room3D.Directions dir in CardinalDirections)
            {
                // Only braid into a walkable neighbour: opening a loop into a pit
                // or off the footprint would break the obstacle-around invariant.
                if (TryGetNeighbor(x, z, dir, out int nx, out int nz) &&
                    IsWalkable(nx, nz) && rooms[nx, nz] != null &&
                    !IsDoorOpen(x, z, dir))
                {
                    closed.Add(dir);
                }
            }

            return closed;
        }

        private bool IsDoorOpen(int x, int z, Room3D.Directions dir)
        {
            return rooms[x, z] != null && !rooms[x, z].IsWallClosed(dir);
        }

        private bool TryGetNeighbor(int x, int z, Room3D.Directions dir, out int nx, out int nz)
        {
            nx = x;
            nz = z;
            switch (dir)
            {
                case Room3D.Directions.NORTH: nz = z + 1; break;
                case Room3D.Directions.SOUTH: nz = z - 1; break;
                case Room3D.Directions.EAST: nx = x + 1; break;
                case Room3D.Directions.WEST: nx = x - 1; break;
                default: return false;
            }

            return nx >= 0 && nx < CurrentNumX && nz >= 0 && nz < CurrentNumZ;
        }

        private bool IsReachable(Vector2Int start, Vector2Int end, bool allowPits)
        {
            return TryFindPath(start, end, allowPits, out List<Vector2Int> _);
        }

        // Breadth-first over open doorways. A cell is enterable only when it is
        // in the grid and either not a pit or pits are allowed; the start cell
        // is never a pit. Returns the path start..end inclusive when found.
        private bool TryFindPath(Vector2Int start, Vector2Int end, bool allowPits, out List<Vector2Int> path)
        {
            path = null;
            if (rooms == null)
            {
                return false;
            }

            bool[,] visited = new bool[CurrentNumX, CurrentNumZ];
            Vector2Int[,] cameFrom = new Vector2Int[CurrentNumX, CurrentNumZ];
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();

            visited[start.x, start.y] = true;
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                if (cell == end)
                {
                    path = ReconstructPath(cameFrom, start, end);
                    return true;
                }

                foreach (Room3D.Directions dir in CardinalDirections)
                {
                    if (!IsDoorOpen(cell.x, cell.y, dir) ||
                        !TryGetNeighbor(cell.x, cell.y, dir, out int nx, out int nz) ||
                        visited[nx, nz])
                    {
                        continue;
                    }

                    if (!allowPits && rooms[nx, nz] != null && rooms[nx, nz].IsPit && new Vector2Int(nx, nz) != end)
                    {
                        continue;
                    }

                    visited[nx, nz] = true;
                    cameFrom[nx, nz] = cell;
                    frontier.Enqueue(new Vector2Int(nx, nz));
                }
            }

            return false;
        }

        private static List<Vector2Int> ReconstructPath(Vector2Int[,] cameFrom, Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int cell = end;
            path.Add(cell);
            while (cell != start)
            {
                cell = cameFrom[cell.x, cell.y];
                path.Add(cell);
            }

            path.Reverse();
            return path;
        }

        public List<Tuple<Room3D.Directions, Room3D>> GetUnvisitedNeighbors(int cx, int cz)
        {
            List<Tuple<Room3D.Directions, Room3D>> neighbours =
                new List<Tuple<Room3D.Directions, Room3D>>();

            foreach (Room3D.Directions dir in CardinalDirections)
            {
                if (!TryGetNeighbor(cx, cz, dir, out int nx, out int nz))
                {
                    continue;
                }

                // Only carve into walkable cells: skip masked-out (null) cells
                // and pit obstacles so the spanning tree spans the walkable set.
                if (!IsWalkable(nx, nz) || rooms[nx, nz] == null || rooms[nx, nz].visited)
                {
                    continue;
                }

                neighbours.Add(new Tuple<Room3D.Directions, Room3D>(dir, rooms[nx, nz]));
            }

            return neighbours;
        }

        private bool GenerateStep()
        {
            if (stack.Count == 0)
            {
                return true;
            }

            Room3D r = stack.Peek();
            var neighbours = GetUnvisitedNeighbors(r.Index.x, r.Index.z);

            if (neighbours.Count != 0)
            {
                int index = neighbours.Count > 1 ? UnityEngine.Random.Range(0, neighbours.Count) : 0;
                var item = neighbours[index];
                Room3D neighbour = item.Item2;

                neighbour.visited = true;
                RemoveRoomWall(r.Index.x, r.Index.z, item.Item1);
                stack.Push(neighbour);
            }
            else
            {
                stack.Pop();
            }

            return false;
        }

        /// <summary>Moves (or spawns) the player at the current start room's PlayerSpawn.</summary>
        public void RespawnPlayerAtStartRoom()
        {
            RespawnPlayerAtStart();
        }

        private void RespawnPlayerAtStart()
        {
            if (rooms == null ||
                startRoomIndex.x < 0 ||
                startRoomIndex.y < 0 ||
                startRoomIndex.x >= rooms.GetLength(0) ||
                startRoomIndex.y >= rooms.GetLength(1) ||
                rooms[startRoomIndex.x, startRoomIndex.y] == null)
            {
                return;
            }

            PlayerSpawn playerSpawn = rooms[startRoomIndex.x, startRoomIndex.y].GetComponentInChildren<PlayerSpawn>();
            if (playerSpawn == null)
            {
                Debug.LogWarning("The first maze room does not contain a PlayerSpawn.", this);
                return;
            }

            playerSpawn.RespawnExistingAtThisSpawn();
        }

        private IEnumerator Coroutine_ArcadeGen()
        {
            generating = true;
            bool flag = false;

            while (!flag)
            {
                int stepsThisFrame = Mathf.Max(1, generationStepsPerFrame);
                for (int i = 0; i < stepsThisFrame; i++)
                {
                    flag = GenerateStep();
                    if (flag)
                    {
                        break;
                    }
                }

                yield return null; // Wait one frame.
            }

            FinishGeneration();
        }

        private void ActivateEndRoomExitClerk()
        {
            if (rooms == null ||
                endRoomIndex.x < 0 ||
                endRoomIndex.y < 0 ||
                endRoomIndex.x >= rooms.GetLength(0) ||
                endRoomIndex.y >= rooms.GetLength(1) ||
                rooms[endRoomIndex.x, endRoomIndex.y] == null)
            {
                return;
            }

            EndRoomExitClerkActivator clerkActivator =
                rooms[endRoomIndex.x, endRoomIndex.y].GetComponent<EndRoomExitClerkActivator>();

            if (clerkActivator != null)
            {
                clerkActivator.ActivateClerkOnClosedWall();
            }
        }

        private void ResetRoomsForGeneration()
        {
            for (int i = 0; i < CurrentNumX; ++i)
            {
                for (int j = 0; j < CurrentNumZ; ++j)
                {
                    if (rooms[i, j] == null)
                    {
                        continue; // masked-out cell; no room instantiated here
                    }

                    rooms[i, j].visited = false;
                    rooms[i, j].SetDirFlag(Room3D.Directions.NORTH, true);
                    rooms[i, j].SetDirFlag(Room3D.Directions.SOUTH, true);
                    rooms[i, j].SetDirFlag(Room3D.Directions.EAST, true);
                    rooms[i, j].SetDirFlag(Room3D.Directions.WEST, true);
                    rooms[i, j].visited = false;
                }
            }
        }

        private int CurrentNumX => activeRules != null ? Mathf.Max(1, activeRules.numX) : numX;
        private int CurrentNumZ => activeRules != null ? Mathf.Max(1, activeRules.numZ) : numZ;
        // Rules replace the room setup only when they opt in; otherwise the
        // generator's authored prefabs and placement mode stay in charge.
        private bool UseRuleRooms => activeRules != null && activeRules.overrideRoomPrefabs;
        private List<GameObject> CurrentPossibleRoomPrefabs => UseRuleRooms ? activeRules.possibleRoomPrefabs : possibleRoomPrefabs;
        private GameObject CurrentFirstRoomPrefab => UseRuleRooms ? activeRules.firstRoomPrefab : firstRoomPrefab;
        private GameObject CurrentLastRoomPrefab => UseRuleRooms ? activeRules.lastRoomPrefab : lastRoomPrefab;
        private GameObject CurrentCenterRoomPrefab => UseRuleRooms ? activeRules.centerRoomPrefab : centerRoomPrefab;
        private SpecialRoomPlacementMode CurrentSpecialRoomPlacementMode =>
            UseRuleRooms ? activeRules.specialRoomPlacementMode : specialRoomPlacementMode;
        private bool CurrentOpenStartOuterWall => activeRules != null ? activeRules.openStartOuterWall : openStartOuterWall;
        private Room3D.Directions CurrentStartOuterWallDirection =>
            activeRules != null ? activeRules.startOuterWallDirection : startOuterWallDirection;
        private bool CurrentOpenEndOuterWall => activeRules != null ? activeRules.openEndOuterWall : openEndOuterWall;
        private Room3D.Directions CurrentEndOuterWallDirection =>
            activeRules != null ? activeRules.endOuterWallDirection : endOuterWallDirection;

        private bool ShouldActivateEndRoomExit()
        {
            return activeRules == null || activeRules.activateEndRoomExit;
        }

        // No serialized hub counterpart: the hub lane (activeRules == null)
        // always reads 0, so the braid pass early-outs before touching RNG and
        // the hub maze is generated byte-identically to before this feature.
        private float CurrentBraidRate => activeRules != null ? Mathf.Clamp01(activeRules.braidRate) : 0f;

        // Pits + footprint are rules-only features: the hub lane reads 0 pits, a
        // null void prefab, and a full-rectangle footprint, so the mask/pit
        // passes early-out and the hub is untouched.
        // Plazas are a rules-only feature: the hub lane reads 0 plazas, so
        // OpenPlazas early-outs before any RNG and the hub maze is untouched.
        private int CurrentPlazaCount => activeRules != null ? Mathf.Max(0, activeRules.plazaCount) : 0;
        private int CurrentPlazaMinSize => activeRules != null ? Mathf.Max(2, activeRules.plazaMinSize) : 2;
        private int CurrentPlazaMaxSize => activeRules != null ? Mathf.Max(2, activeRules.plazaMaxSize) : 2;

        private int CurrentPitCount => activeRules != null ? Mathf.Max(0, activeRules.pitCount) : 0;

        // Buildings are a rules-only feature: the hub lane reads 0, so
        // ChooseBuildings early-outs before any RNG and the hub is untouched.
        private int CurrentProceduralBuildingCount => activeRules != null ? Mathf.Max(0, activeRules.proceduralBuildingCount) : 0;
        private int CurrentBuildingMinSize => activeRules != null ? Mathf.Max(1, activeRules.buildingMinSize) : 1;
        private int CurrentBuildingMaxSize => activeRules != null ? Mathf.Max(1, activeRules.buildingMaxSize) : 1;
        private int CurrentAuthoredBuildingCount => activeRules != null ? Mathf.Max(0, activeRules.authoredBuildingCount) : 0;
        private List<GameObject> CurrentAuthoredBuildings => activeRules != null ? activeRules.authoredBuildings : null;
        private GameObject CurrentPitVoidPrefab => activeRules != null ? activeRules.pitVoidPrefab : null;
        private bool CurrentOrganicFootprint => activeRules != null && activeRules.organicFootprint;
        private float CurrentFootprintFill => activeRules != null ? Mathf.Clamp(activeRules.footprintFill, 0.2f, 1f) : 1f;

        private void RunGenerationToCompletion()
        {
            generating = true;
            int generationStepLimit = Mathf.Max(1, CurrentNumX * CurrentNumZ * 4);
            for (int i = 0; i < generationStepLimit; i++)
            {
                if (GenerateStep())
                {
                    return;
                }
            }

            Debug.LogWarning("ArcadeGen3D generation hit the safety step limit.", this);
        }

        private void FinishGeneration()
        {
            // Post-carve passes run for every lane but self-disable when their
            // inputs are inert: BraidMaze does nothing at rate 0 (the hub), and
            // the pit passes do nothing when no pit rooms were placed.
            PostCarveProcessing();

            if (ShouldActivateEndRoomExit())
            {
                ActivateEndRoomExitClerk();
            }

            generating = false;

            Action callback = generationCompleteCallback;
            ClearActiveGenerationRequest();
            callback?.Invoke();
        }

        private void ClearActiveGenerationRequest()
        {
            activeRules = null;
            generationCompleteCallback = null;
        }
    }
}
