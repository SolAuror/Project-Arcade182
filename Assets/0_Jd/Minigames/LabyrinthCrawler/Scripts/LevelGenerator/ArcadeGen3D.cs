using System;
using System.Collections;
using System.Collections.Generic;
using Sol.Minigames;
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
        [Tooltip("Fraction of dead-ends knocked open into loops after the perfect-maze carve. 0 = classic single-path maze. Loops give the player a route around obstacles like pits.")]
        [Range(0f, 1f)] public float braidRate;

        [Header("Pits")]
        [Tooltip("Number of pit cells the maze carves AROUND (obstacle-first): the walkable graph excludes them, so the exit is always reachable and pits stretch the route rather than block it. 0 = no pits (the hub default).")]
        [Min(0)] public int pitCount;

        [Tooltip("Void apparatus spawned beneath a designated pit room (retaining shafts, corner pillars and fog). Required for pitCount to take effect.")]
        public GameObject pitVoidPrefab;

        [Header("Buildings")]
        [Tooltip("Number of PROCEDURAL buildings placed obstacle-first. Each uses the shared Building Component planner for organic/L/T/tower-house massing, entrances, roofs and structural supports. The maze reserves the resulting footprint before carving so the exit remains reachable.")]
        [Min(0)] public int proceduralBuildingCount;

        [Tooltip("Smallest bounding box offered to the organic building planner, in cells.")]
        [Min(1)] public int buildingMinSize = 1;

        [Tooltip("Largest bounding box offered to the organic building planner. It is a limit, not a guaranteed filled rectangle.")]
        [Min(1)] public int buildingMaxSize = 2;

        [Tooltip("Maximum number of full-height cells in a procedural building column, including its ground floor.")]
        [Range(1, 8)] public int buildingHeightLimit = 3;

        [Tooltip("Requested entrances per procedural building. Entrances that do not face a reachable street are skipped safely.")]
        [Min(1)] public int buildingEntranceCount = 1;

        [Tooltip("Chance the stage exit is moved into one of the successfully placed procedural buildings.")]
        [Range(0f, 1f)] public float buildingExitChance = 0.35f;

        [Tooltip("Select the final exit after carving from the real walkable graph. This changes only the destination, never the generated maze topology. Intended for games that spawn their own exit marker into any room.")]
        public bool optimizeExitPlacement;

        [Tooltip("An indoor exit must be at least this many room steps beyond the nearest building entrance. 1 prevents the exit from occupying the entrance cell.")]
        [Min(1)] public int minimumBuildingExitDepth = 1;

        [Tooltip("An indoor exit must retain at least this fraction of the farthest outdoor route distance. If no deep building cell qualifies, the exit remains outdoors.")]
        [Range(0f, 1f)] public float minimumBuildingExitDistanceRatio = 0.75f;

        [Tooltip("Select the final player start after the exit from the completed walkable graph. Requires a movable PlayerSpawn marker in the generated room set.")]
        public bool optimizePlayerSpawnPlacement;

        [Tooltip("Chance the optimized player start is placed inside a qualifying procedural building.")]
        [Range(0f, 1f)] public float buildingPlayerSpawnChance = 0.35f;

        [Tooltip("An indoor player start must be at least this many room steps beyond the nearest building entrance.")]
        [Min(1)] public int minimumBuildingPlayerSpawnDepth = 1;

        [Tooltip("An indoor player start must retain at least this fraction of the farthest outdoor route from the exit.")]
        [Range(0f, 1f)] public float minimumBuildingPlayerSpawnDistanceRatio = 0.75f;

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

    /// <summary>
    /// Labyrinth Crawler's maze generator. The legacy type name is retained so
    /// existing prefabs keep their serialized references. At runtime it is a
    /// passive service: only its owning <see cref="LabyrinthCrawlerGame"/> may
    /// submit generation rules.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Labyrinth Crawler/Maze Generator")]
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

        [Tooltip("Optional exact X/Z distance between room origins. Leave at zero to infer the size from the room prefab renderers. Use this for cell-centred modular kits so posters, machines, wall thickness, or other decor cannot perturb grid alignment.")]
        [SerializeField] private Vector2 roomSizeOverride;

        [Header("Legacy Generation Settings")]
        [Tooltip("Retained for prefab compatibility. Runtime auto-generation is disabled; LabyrinthCrawlerGame owns generation.")]
        [SerializeField, HideInInspector] private bool autoGenerateOnStart;

        [Tooltip("Retained for prefab compatibility. Runtime keyboard regeneration is disabled; LabyrinthCrawlerGame owns generation.")]
        [SerializeField, HideInInspector] private bool allowRuntimeKeyboardRegenerate;

        [Tooltip("Maze carving steps processed per frame while generating at runtime.")]
        [SerializeField, Min(1)] private int generationStepsPerFrame = 32;

        [Header("Maze Loops")]
        [Tooltip("Fraction of dead ends opened into loops after the initial perfect-maze carve. The dungeon uses 0.35.")]
        [SerializeField, Range(0f, 1f)] private float braidRate;

        [Header("Outer Openings")]
        [Tooltip("Optional outside opening on the start room.")]
        [SerializeField] private bool openStartOuterWall = false;

        [Tooltip("Wall direction to open when Start Outer Wall is enabled.")]
        [SerializeField] private Room3D.Directions startOuterWallDirection = Room3D.Directions.SOUTH;

        [Tooltip("Optional outside opening on the end room.")]
        [SerializeField] private bool openEndOuterWall = false;

        [Tooltip("Wall direction to open when End Outer Wall is enabled.")]
        [SerializeField] private Room3D.Directions endOuterWallDirection = Room3D.Directions.NORTH;

        [Header("Wall Dressing")]
        [Tooltip("After carving, let each wall's WallSocket swap in a themed part (archway/doorway for openings, window/arrowslit walls for solids, gable caps on outer walls). Off on the hub generator; leave off unless the room prefabs carry WallSocket components. Purely cosmetic - never changes layout or reachability.")]
        [SerializeField] private bool dressWallsAfterCarve = false;

        [Tooltip("Render each shared wall once (by one owning cell) instead of both cells drawing it. A window then looks THROUGH into the neighbouring space instead of into a back-to-back wall, and interior walls stop doubling up. Turn OFF if your wall meshes are single-sided and show through from the back. Only applies while Dress Walls After Carve is on.")]
        [SerializeField] private bool deDoubleSharedWalls = true;

        [Tooltip("Optional lightweight passage kit for rooms that do not already carry WallSockets. Closed edges keep their authored solid wall and poster/flyer children; open edges choose one of these parts. Empty leaves existing room behaviour unchanged.")]
        [SerializeField] private List<GameObject> minimalPassageVariants = new List<GameObject>();

        [Tooltip("Scale multiplier applied once to poster roots discovered by the lightweight wall treatment. Flyers are left at their authored size.")]
        [SerializeField, Min(0.01f)] private float minimalPosterScaleMultiplier = 1f;

        [Header("Arcade Machine Scatter")]
        [Tooltip("After carving, redistribute arcade machines already authored in each room onto closed-wall bays. Entire open walls remain clear for doorways and openings. Intended for the Hub; leave off on generators without room-authored cabinets.")]
        [SerializeField] private bool scatterArcadeMachinesAfterCarve;

        [Tooltip("Playable cabinet prefabs used to fill rooms that contain fewer than Target Arcade Machines Per Room. Add future machines here to include them in the deterministic scatter pool.")]
        [SerializeField] private List<GameObject> arcadeMachinePrefabs = new List<GameObject>();

        [Tooltip("Minimum number of playable cabinets requested in every walkable room. Existing authored cabinets count toward this target.")]
        [SerializeField, Min(0)] private int targetArcadeMachinesPerRoom = 2;

        [Tooltip("Uniform local scale applied to scattered cabinet roots before their footprints are measured. Keep at 1 to preserve the authored size.")]
        [SerializeField, Min(0.01f)] private float arcadeMachineUniformScale = 1f;

        [Tooltip("Small clearance maintained between each cabinet's measured lowest rendered point and the room floor.")]
        [SerializeField, Min(0f)] private float arcadeMachineGroundClearance;

        [Tooltip("Distance from the room edge to the centre of a cabinet. The Hub's authored machines use approximately 1.38.")]
        [SerializeField, Min(0f)] private float arcadeMachineWallInset = 1.38f;

        [Tooltip("Distance to either side of the wall centre for the two cabinet bays. Keeps the centre line and adjacent corners clear.")]
        [SerializeField, Min(0f)] private float arcadeMachineBayOffset = 2.15f;

        [Tooltip("Extra horizontal gap maintained between the measured footprints of neighbouring cabinets.")]
        [SerializeField, Min(0f)] private float arcadeMachineClearance = 0.15f;

        [Header("Arcade Poster Scatter")]
        [Tooltip("After carving, redistribute authored posters and fill safe closed-wall poster bays from Arcade Poster Prefabs. Openings and doorway walls never receive posters.")]
        [SerializeField] private bool scatterArcadePostersAfterCarve;

        [Tooltip("Poster prefabs used to fill closed walls. Add future posters here to include them in the deterministic scatter pool.")]
        [SerializeField] private List<GameObject> arcadePosterPrefabs = new List<GameObject>();

        [Tooltip("Number of posters placed across each rendered closed wall. Three creates a deliberately poster-heavy arcade.")]
        [SerializeField, Min(0)] private int postersPerClosedWall = 3;

        [Tooltip("Distance from the room edge to the Hub wall's interior poster plane. The current wall kit's visible inner face is approximately 0.55 units inside the grid boundary.")]
        [SerializeField, Min(0f)] private float posterWallInset = 0.55f;

        [Tooltip("Height of poster roots above the room floor.")]
        [SerializeField, Min(0f)] private float posterHeight = 3.35f;

        [Tooltip("Random vertical variation added to poster placement.")]
        [SerializeField, Min(0f)] private float posterHeightJitter = 0.35f;

        [Tooltip("Horizontal margin kept between the outer poster bays and room corners.")]
        [SerializeField, Min(0f)] private float posterCornerMargin = 1.05f;

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

        [Tooltip("Chance the shared building planner adds a clustered half-storey top.")]
        [SerializeField, Range(0f, 1f)] private float halfStoryChance = 0.3f;

        [Tooltip("Extra 90-degree yaw applied to every placed roof cell. Use this to correct the whole roof kit's facing in one place if the slopes come out rotated.")]
        [SerializeField, Range(0, 3)] private int roofYawTrim = 0;

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

        private struct ProceduralPlacement
        {
            public int OriginX;
            public int OriginZ;
            public BuildingPlanUtility.Plan Plan;
        }
        private readonly List<ProceduralPlacement> proceduralPlacements =
            new List<ProceduralPlacement>();

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
            if (generatedRoomsParent == null)
            {
                // Adopt a pre-baked "Generated Rooms" (the designer preview) only
                // when nothing has generated yet. Guarding on null keeps Start
                // from clobbering a parent an earlier Awake-time generation
                // already built - and, on scene reload, from latching onto the
                // deferred-Destroy baked node before it is torn down.
                generatedRoomsParent = transform.Find("Generated Rooms");
            }

            if (autoGenerateOnStart || allowRuntimeKeyboardRegenerate)
            {
                Debug.LogWarning(
                    $"{name} contains obsolete autonomous-generation settings. " +
                    "They are ignored because LabyrinthCrawlerGame has exclusive runtime maze authority.",
                    this);
            }
        }

        [Obsolete("Runtime maze generation is owned by LabyrinthCrawlerGame. Use GenerateWithRules(owner, rules, callback).")]
        public void CreateArcade()
        {
            Debug.LogError(
                "Autonomous maze generation is disabled. LabyrinthCrawlerGame must submit the generation request.",
                this);
        }

        public bool GenerateWithRules(
            LabyrinthCrawlerGame owner,
            ArcadeMazeRules rules,
            Action onComplete = null)
        {
            if (owner == null ||
                owner.Maze != this ||
                !owner.isActiveAndEnabled ||
                owner.gameObject.scene != gameObject.scene)
            {
                Debug.LogError(
                    $"{name} rejected an unauthorized maze-generation request. " +
                    "Only the active LabyrinthCrawlerGame that owns this generator may generate a maze.",
                    this);
                return false;
            }

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
            Debug.LogError(
                "Standalone Inspector regeneration is disabled. " +
                "Maze generation is dedicated to Labyrinth Crawler and must use its game-owned rules.",
                this);
            return false;
        }

        private bool PrepareGeneration(bool respawnPlayer)
        {
            if (!RebuildRooms())
            {
                return false;
            }

            ResetRoomsForGeneration();
            ApplyOptionalOuterOpenings();

            if (respawnPlayer && !CurrentOptimizePlayerSpawnPlacement)
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
            if (!CurrentOptimizeExitPlacement)
            {
                // Compatibility lane for generators whose last-room prefab owns
                // the exit and therefore must be selected before instantiation.
                ChooseBuildingExit();
            }
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
            public BuildingPlanUtility.Plan Plan;
        }

        // Obstacle-first BUILDING placement: reserve planned footprints the same
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
            proceduralPlacements.Clear();

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
                int width = UnityEngine.Random.Range(procMin, procMax + 1);
                int depth = UnityEngine.Random.Range(procMin, procMax + 1);
                int seed = UnityEngine.Random.Range(
                    -1000000000,
                    1000000000);
                requests.Add(new BuildingRequest
                {
                    Width = width,
                    Depth = depth,
                    Prefab = null,
                    IsAuthored = false,
                    Plan = BuildingPlanUtility.Create(
                        width,
                        depth,
                        buildUpperFloors && upperCellPrefab != null
                            ? CurrentBuildingHeightLimit
                            : 1,
                        CurrentBuildingEntranceCount,
                        buildUpperFloors && upperCellHalfPrefab != null
                            ? halfStoryChance
                            : 0f,
                        seed,
                        HasMazeRoofPrefab),
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
                if (!BuildingFootprintFits(
                        ox,
                        oz,
                        request.Width,
                        request.Depth,
                        request.Plan))
                {
                    continue;
                }

                SetBuildingCells(
                    ox,
                    oz,
                    request.Width,
                    request.Depth,
                    request.IsAuthored,
                    true,
                    request.Plan);
                if (!WalkableRegionConnected())
                {
                    SetBuildingCells(
                        ox,
                        oz,
                        request.Width,
                        request.Depth,
                        request.IsAuthored,
                        false,
                        request.Plan);
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
                else
                {
                    proceduralPlacements.Add(new ProceduralPlacement
                    {
                        OriginX = ox,
                        OriginZ = oz,
                        Plan = request.Plan,
                    });
                }

                return true;
            }

            return false;
        }

        private void ChooseBuildingExit()
        {
            if (proceduralPlacements.Count == 0
                || CurrentBuildingExitChance <= 0f
                || UnityEngine.Random.value > CurrentBuildingExitChance)
            {
                return;
            }

            ProceduralPlacement placement =
                proceduralPlacements[
                    UnityEngine.Random.Range(
                        0,
                        proceduralPlacements.Count)];
            if (placement.Plan == null
                || placement.Plan.Columns.Count == 0)
            {
                return;
            }

            int bestScore = int.MinValue;
            List<Vector2Int> best = new List<Vector2Int>();
            foreach (Vector2Int column in placement.Plan.Columns)
            {
                int entranceDistance = int.MaxValue;
                foreach (BuildingPlanUtility.Entrance entrance in
                         placement.Plan.Entrances)
                {
                    int distance =
                        Mathf.Abs(column.x - entrance.Column.x)
                        + Mathf.Abs(column.y - entrance.Column.y);
                    entranceDistance =
                        Mathf.Min(entranceDistance, distance);
                }
                if (entranceDistance == int.MaxValue)
                {
                    entranceDistance = 0;
                }

                Vector2Int absolute = new Vector2Int(
                    placement.OriginX + column.x,
                    placement.OriginZ + column.y);
                int startDistance =
                    Mathf.Abs(absolute.x - startRoomIndex.x)
                    + Mathf.Abs(absolute.y - startRoomIndex.y);
                int score = entranceDistance * 100 + startDistance;
                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                }
                if (score == bestScore)
                {
                    best.Add(absolute);
                }
            }

            if (best.Count > 0)
            {
                endRoomIndex =
                    best[UnityEngine.Random.Range(0, best.Count)];
            }
        }

        // A footprint fits when every cell is clear buildable ground (active,
        // non-pit, non-building, not the start/exit) and a 1-cell ring around it
        // holds no OTHER building, so each building stays a distinct group with a
        // street frontage.
        private bool BuildingFootprintFits(
            int ox,
            int oz,
            int w,
            int d,
            BuildingPlanUtility.Plan plan)
        {
            if (ox < 0 || oz < 0 || ox + w > CurrentNumX || oz + d > CurrentNumZ)
            {
                return false;
            }

            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + d; z++)
                {
                    if (plan != null
                        && !plan.Footprint[x - ox, z - oz])
                    {
                        continue;
                    }
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

        private void SetBuildingCells(
            int ox,
            int oz,
            int w,
            int d,
            bool authoredBuilding,
            bool value,
            BuildingPlanUtility.Plan plan)
        {
            for (int x = ox; x < ox + w; x++)
            {
                for (int z = oz; z < oz + d; z++)
                {
                    if (plan != null
                        && !plan.Footprint[x - ox, z - oz])
                    {
                        continue;
                    }
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
            if (roomSizeOverride.x > 0f && roomSizeOverride.y > 0f)
            {
                roomWidth = roomSizeOverride.x;
                roomLength = roomSizeOverride.y;
                return true;
            }

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
        // Rules-only passes self-disable on the hub lane before consuming RNG.
        // Order matters: loops and plazas open the carved graph first; pits and
        // buildings then materialize their reserved masks; classification records
        // the final topology before the cosmetic wall/upper-floor dressing runs.

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
            ScatterArcadePosters();
            ScatterArcadeMachines();
            BuildUpperFloors();
            OptimizeEndpointPlacements();
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

            foreach (ProceduralPlacement placement in proceduralPlacements)
            {
                if (placement.Plan == null)
                {
                    continue;
                }

                List<Vector2Int> group = new List<Vector2Int>();
                foreach (Vector2Int local in placement.Plan.Columns)
                {
                    Vector2Int cell = new Vector2Int(
                        placement.OriginX + local.x,
                        placement.OriginZ + local.y);
                    if (InBounds(cell.x, cell.y)
                        && rooms[cell.x, cell.y] != null)
                    {
                        group.Add(cell);
                    }
                }

                OpenBuildingHall(group);
                if (CarvePlannedEntrances(placement) == 0)
                {
                    CarveBuildingEntrance(group, rng);
                }
            }
        }

        private int CarvePlannedEntrances(
            ProceduralPlacement placement)
        {
            int opened = 0;
            foreach (BuildingPlanUtility.Entrance entrance in
                     placement.Plan.Entrances)
            {
                int x = placement.OriginX + entrance.Column.x;
                int z = placement.OriginZ + entrance.Column.y;
                if (!InBounds(x, z)
                    || rooms[x, z] == null
                    || !TryGetNeighbor(
                        x,
                        z,
                        entrance.Face,
                        out int nx,
                        out int nz)
                    || !IsWalkable(nx, nz)
                    || rooms[nx, nz] == null)
                {
                    continue;
                }

                RemoveRoomWall(x, z, entrance.Face);
                opened++;
            }
            return opened;
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

                    // Arcade's room prefabs intentionally stay much simpler than
                    // the crawler kit. When a small passage list is supplied, add
                    // runtime sockets around their existing solid walls and keep
                    // poster/flyer children as closed-wall-only decoration.
                    room.EnsureMinimalWallSockets(
                        minimalPassageVariants,
                        minimalPosterScaleMultiplier);

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

        private readonly struct ArcadePosterSlot
        {
            public ArcadePosterSlot(
                Room3D.Directions wall,
                Vector3 localPosition)
            {
                Wall = wall;
                LocalPosition = localPosition;
            }

            public Room3D.Directions Wall { get; }
            public Vector3 LocalPosition { get; }
        }

        // Dense Hub poster pass. Existing room-authored posters are recycled
        // first, then the serialized pool fills any remaining bays. Because bays
        // are generated only for closed edges, poster art never leaks into a
        // doorway or wide opening even when the maze layout changes.
        private void ScatterArcadePosters()
        {
            if (!scatterArcadePostersAfterCarve || rooms == null)
            {
                return;
            }

            System.Random rng = new System.Random(
                System.HashCode.Combine(
                    CurrentNumX,
                    CurrentNumZ,
                    startRoomIndex,
                    endRoomIndex,
                    0x504F5354));

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null || room.IsPit || room.IsSolidBlock)
                    {
                        continue;
                    }

                    ScatterRoomArcadePosters(room, x, z, rng);
                }
            }
        }

        private void ScatterRoomArcadePosters(
            Room3D room,
            int x,
            int z,
            System.Random rng)
        {
            List<ArcadePosterSlot> slots =
                BuildArcadePosterSlots(x, z);
            List<Transform> posters =
                FindArcadePosterRoots(room);

            int requestedCount = slots.Count;
            while (posters.Count < requestedCount
                && TryPickPooledPrefab(
                    arcadePosterPrefabs,
                    rng,
                    out GameObject prefab))
            {
                GameObject instance =
                    Instantiate(prefab, room.transform);
                instance.name =
                    $"__ArcadePoster__{prefab.name}";
                instance.transform.localScale =
                    Vector3.Scale(
                        prefab.transform.localScale,
                        Vector3.one
                            * Mathf.Max(
                                0.01f,
                                minimalPosterScaleMultiplier));
                posters.Add(instance.transform);
            }

            for (int i = posters.Count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                (posters[i], posters[swap]) =
                    (posters[swap], posters[i]);
            }

            for (int i = 0; i < posters.Count; i++)
            {
                Transform poster = posters[i];
                if (i >= slots.Count)
                {
                    poster.gameObject.SetActive(false);
                    continue;
                }

                ArcadePosterSlot slot = slots[i];
                Vector3 position = slot.LocalPosition;
                position.y +=
                    ((float)rng.NextDouble() * 2f - 1f)
                    * Mathf.Max(0f, posterHeightJitter);

                // Detaching from an authored wall prevents a poster moved away
                // from an open/non-owning edge being hidden with its old parent.
                poster.SetParent(room.transform, true);
                poster.SetPositionAndRotation(
                    room.transform.TransformPoint(position),
                    room.transform.rotation
                        * Quaternion.Euler(
                            0f,
                            DirectionYaw(slot.Wall),
                            0f));
                poster.gameObject.SetActive(true);
            }
        }

        private List<ArcadePosterSlot> BuildArcadePosterSlots(
            int x,
            int z)
        {
            int count = Mathf.Max(0, postersPerClosedWall);
            List<ArcadePosterSlot> slots =
                new List<ArcadePosterSlot>(count * 4);
            if (count == 0)
            {
                return slots;
            }

            float halfWidth = roomWidth * 0.5f;
            float halfLength = roomLength * 0.5f;
            float insetX = Mathf.Clamp(
                posterWallInset,
                0f,
                halfWidth);
            float insetZ = Mathf.Clamp(
                posterWallInset,
                0f,
                halfLength);
            float horizontalX = Mathf.Max(
                0f,
                halfWidth - posterCornerMargin);
            float horizontalZ = Mathf.Max(
                0f,
                halfLength - posterCornerMargin);

            AddPosterWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.NORTH,
                count,
                horizontalX,
                offset =>
                    new Vector3(
                        offset,
                        posterHeight,
                        halfLength - insetZ));
            AddPosterWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.SOUTH,
                count,
                horizontalX,
                offset =>
                    new Vector3(
                        -offset,
                        posterHeight,
                        -halfLength + insetZ));
            AddPosterWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.EAST,
                count,
                horizontalZ,
                offset =>
                    new Vector3(
                        halfWidth - insetX,
                        posterHeight,
                        offset));
            AddPosterWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.WEST,
                count,
                horizontalZ,
                offset =>
                    new Vector3(
                        -halfWidth + insetX,
                        posterHeight,
                        -offset));

            return slots;
        }

        private void AddPosterWallSlots(
            List<ArcadePosterSlot> slots,
            int x,
            int z,
            Room3D.Directions wall,
            int count,
            float horizontalExtent,
            Func<float, Vector3> positionFactory)
        {
            if (IsDoorOpen(x, z, wall)
                || !DoesRoomOwnRenderedWall(x, z, wall))
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float t =
                    count == 1
                        ? 0.5f
                        : i / (float)(count - 1);
                float offset =
                    Mathf.Lerp(
                        -horizontalExtent,
                        horizontalExtent,
                        t);
                slots.Add(
                    new ArcadePosterSlot(
                        wall,
                        positionFactory(offset)));
            }
        }

        private bool DoesRoomOwnRenderedWall(
            int x,
            int z,
            Room3D.Directions wall)
        {
            if (!deDoubleSharedWalls)
            {
                return true;
            }

            bool selfBlock = IsSolidBlockCell(x, z);
            bool hasNeighbor =
                TryGetNeighbor(
                    x,
                    z,
                    wall,
                    out int neighborX,
                    out int neighborZ);
            bool outer =
                !hasNeighbor
                || !IsActiveCell(neighborX, neighborZ);
            bool neighborBlock =
                hasNeighbor
                && IsSolidBlockCell(neighborX, neighborZ);

            return selfBlock != neighborBlock
                ? selfBlock
                : outer
                    || wall == Room3D.Directions.NORTH
                    || wall == Room3D.Directions.EAST;
        }

        private static List<Transform> FindArcadePosterRoots(
            Room3D room)
        {
            List<Transform> posters = new List<Transform>();
            foreach (Transform candidate
                in room.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == room.transform
                    || candidate.name.IndexOf(
                        "Poster",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                bool nestedBelowPoster = false;
                Transform parent = candidate.parent;
                while (parent != null && parent != room.transform)
                {
                    if (parent.name.IndexOf(
                            "Poster",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        nestedBelowPoster = true;
                        break;
                    }

                    parent = parent.parent;
                }

                if (!nestedBelowPoster)
                {
                    posters.Add(candidate);
                }
            }

            return posters;
        }

        private static bool TryPickPooledPrefab(
            IReadOnlyList<GameObject> prefabs,
            System.Random rng,
            out GameObject prefab)
        {
            prefab = null;
            if (prefabs == null || prefabs.Count == 0)
            {
                return false;
            }

            int start = rng.Next(prefabs.Count);
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject candidate =
                    prefabs[(start + i) % prefabs.Count];
                if (candidate != null)
                {
                    prefab = candidate;
                    return true;
                }
            }

            return false;
        }

        private readonly struct ArcadeMachineSlot
        {
            public ArcadeMachineSlot(Room3D.Directions wall, Vector3 localPosition)
            {
                Wall = wall;
                LocalPosition = localPosition;
            }

            public Room3D.Directions Wall { get; }
            public Vector3 LocalPosition { get; }
        }

        private readonly struct PlacedArcadeMachine
        {
            public PlacedArcadeMachine(Vector2 localPosition, float radius)
            {
                LocalPosition = localPosition;
                Radius = radius;
            }

            public Vector2 LocalPosition { get; }
            public float Radius { get; }
        }

        // Hub-only furnishing pass. A room prefab remains the source of which
        // playable cabinets it contains; generation only chooses safe locations
        // after the maze knows which edges became passages. Each closed wall has
        // two side bays, while an open wall has none, so a cabinet can never sit
        // inside either the narrow doorway or the wide opening model.
        private void ScatterArcadeMachines()
        {
            if (!scatterArcadeMachinesAfterCarve || rooms == null)
            {
                return;
            }

            System.Random rng = new System.Random(
                System.HashCode.Combine(
                    CurrentNumX,
                    CurrentNumZ,
                    startRoomIndex,
                    endRoomIndex,
                    0x41524344));

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null || room.IsPit || room.IsSolidBlock)
                    {
                        continue;
                    }

                    ScatterRoomArcadeMachines(room, x, z, rng);
                }
            }
        }

        private void ScatterRoomArcadeMachines(
            Room3D room,
            int x,
            int z,
            System.Random rng)
        {
            List<Transform> machines = FindArcadeMachineRoots(room);
            int requestedCount =
                Mathf.Max(0, targetArcadeMachinesPerRoom);
            while (machines.Count < requestedCount
                && TryPickPooledPrefab(
                    arcadeMachinePrefabs,
                    rng,
                    out GameObject prefab))
            {
                GameObject instance =
                    Instantiate(prefab, room.transform);
                instance.name =
                    $"__ArcadeMachine__{prefab.name}";
                machines.Add(instance.transform);
            }

            if (machines.Count == 0)
            {
                return;
            }

            List<ArcadeMachineSlot> slots = new List<ArcadeMachineSlot>(8);
            float halfWidth = roomWidth * 0.5f;
            float halfLength = roomLength * 0.5f;
            float insetX = Mathf.Clamp(arcadeMachineWallInset, 0f, halfWidth);
            float insetZ = Mathf.Clamp(arcadeMachineWallInset, 0f, halfLength);
            float bayX = Mathf.Min(
                arcadeMachineBayOffset,
                Mathf.Max(0f, halfWidth - insetX));
            float bayZ = Mathf.Min(
                arcadeMachineBayOffset,
                Mathf.Max(0f, halfLength - insetZ));

            AddMachineWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.NORTH,
                new Vector3(-bayX, 0f, halfLength - insetZ),
                new Vector3(bayX, 0f, halfLength - insetZ));
            AddMachineWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.SOUTH,
                new Vector3(-bayX, 0f, -halfLength + insetZ),
                new Vector3(bayX, 0f, -halfLength + insetZ));
            AddMachineWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.EAST,
                new Vector3(halfWidth - insetX, 0f, -bayZ),
                new Vector3(halfWidth - insetX, 0f, bayZ));
            AddMachineWallSlots(
                slots,
                x,
                z,
                Room3D.Directions.WEST,
                new Vector3(-halfWidth + insetX, 0f, -bayZ),
                new Vector3(-halfWidth + insetX, 0f, bayZ));

            // Hierarchy order is stable for prefab instances. A seeded shuffle
            // prevents every generated copy from selecting the same fallback wall
            // while keeping regeneration deterministic for the same layout.
            for (int i = machines.Count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                (machines[i], machines[swap]) = (machines[swap], machines[i]);
            }

            List<PlacedArcadeMachine> placedMachines =
                new List<PlacedArcadeMachine>(machines.Count);

            foreach (Transform machine in machines)
            {
                float uniformScale =
                    Mathf.Max(0.01f, arcadeMachineUniformScale);
                machine.localScale =
                    Vector3.one * uniformScale;

                Vector3 currentLocalPosition =
                    room.transform.InverseTransformPoint(machine.position);
                Quaternion currentLocalRotation =
                    Quaternion.Inverse(room.transform.rotation) * machine.rotation;
                Room3D.Directions sourceWall =
                    ClosestWall(currentLocalPosition);
                float footprintRadius =
                    GetArcadeMachineFootprintRadius(machine);

                // Some authored Hub cabinets currently live below a wall parent.
                // Detach them before placement so hiding an open/non-owning wall
                // cannot also hide a cabinet that was moved to a safe closed bay.
                machine.SetParent(room.transform, true);

                int slotIndex = FindPreferredMachineSlot(
                    slots,
                    sourceWall,
                    currentLocalPosition,
                    footprintRadius,
                    placedMachines,
                    rng);
                if (slotIndex < 0)
                {
                    machine.gameObject.SetActive(false);
                    continue;
                }

                ArcadeMachineSlot slot = slots[slotIndex];
                slots.RemoveAt(slotIndex);

                Vector3 placed = slot.LocalPosition;
                placed.y = currentLocalPosition.y;
                float yawDelta =
                    DirectionYaw(slot.Wall) - DirectionYaw(sourceWall);

                machine.SetPositionAndRotation(
                    room.transform.TransformPoint(placed),
                    room.transform.rotation
                        * Quaternion.Euler(0f, yawDelta, 0f)
                        * currentLocalRotation);
                machine.gameObject.SetActive(true);
                GroundArcadeMachine(machine, room);
                placedMachines.Add(
                    new PlacedArcadeMachine(
                        new Vector2(placed.x, placed.z),
                        footprintRadius));
            }
        }

        private void GroundArcadeMachine(
            Transform machine,
            Room3D room)
        {
            Renderer[] renderers =
                machine.GetComponentsInChildren<Renderer>(false);
            bool foundBounds = false;
            Bounds combined = default;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null
                    || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!foundBounds)
                {
                    combined = renderer.bounds;
                    foundBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            if (!foundBounds)
            {
                return;
            }

            float floorY =
                room.transform.position.y
                + Mathf.Max(0f, arcadeMachineGroundClearance);
            machine.position +=
                Vector3.up * (floorY - combined.min.y);
        }

        private void AddMachineWallSlots(
            List<ArcadeMachineSlot> slots,
            int x,
            int z,
            Room3D.Directions wall,
            Vector3 first,
            Vector3 second)
        {
            if (IsDoorOpen(x, z, wall))
            {
                return;
            }

            slots.Add(new ArcadeMachineSlot(wall, first));
            slots.Add(new ArcadeMachineSlot(wall, second));
        }

        private static List<Transform> FindArcadeMachineRoots(Room3D room)
        {
            List<Transform> machines = new List<Transform>();
            HashSet<Transform> unique = new HashSet<Transform>();

            foreach (Sol.Arcade.ArcadeMachineLauncher launcher
                in room.GetComponentsInChildren<Sol.Arcade.ArcadeMachineLauncher>(true))
            {
                Transform machineRoot = null;
                Transform current = launcher.transform;
                while (current != null && current != room.transform)
                {
                    if (current.name.IndexOf(
                            "ArcadeMachine",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        machineRoot = current;
                    }

                    current = current.parent;
                }

                if (machineRoot != null && unique.Add(machineRoot))
                {
                    machines.Add(machineRoot);
                }
            }

            return machines;
        }

        private int FindPreferredMachineSlot(
            List<ArcadeMachineSlot> slots,
            Room3D.Directions sourceWall,
            Vector3 sourcePosition,
            float footprintRadius,
            List<PlacedArcadeMachine> placedMachines,
            System.Random rng)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Wall != sourceWall
                    || !IsMachineSlotClear(
                        slots[i],
                        footprintRadius,
                        placedMachines))
                {
                    continue;
                }

                float distance =
                    (slots[i].LocalPosition - sourcePosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            if (best >= 0)
            {
                return best;
            }

            // Reservoir selection gives every compatible fallback bay an equal,
            // deterministic chance without allocating a second candidate list.
            int compatibleCount = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!IsMachineSlotClear(
                        slots[i],
                        footprintRadius,
                        placedMachines))
                {
                    continue;
                }

                compatibleCount++;
                if (rng.Next(compatibleCount) == 0)
                {
                    best = i;
                }
            }

            return best;
        }

        private bool IsMachineSlotClear(
            ArcadeMachineSlot slot,
            float footprintRadius,
            List<PlacedArcadeMachine> placedMachines)
        {
            Vector2 candidate = new Vector2(
                slot.LocalPosition.x,
                slot.LocalPosition.z);

            foreach (PlacedArcadeMachine placed in placedMachines)
            {
                float minimumDistance =
                    footprintRadius
                    + placed.Radius
                    + Mathf.Max(0f, arcadeMachineClearance);
                if ((candidate - placed.LocalPosition).sqrMagnitude
                    < minimumDistance * minimumDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private static float GetArcadeMachineFootprintRadius(Transform machine)
        {
            Renderer[] renderers =
                machine.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 0.75f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Max(
                0.25f,
                new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
        }

        private static Room3D.Directions ClosestWall(Vector3 localPosition)
        {
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

        private static float DirectionYaw(Room3D.Directions direction)
        {
            switch (direction)
            {
                case Room3D.Directions.EAST: return 90f;
                case Room3D.Directions.SOUTH: return 180f;
                case Room3D.Directions.WEST: return 270f;
                default: return 0f; // NORTH
            }
        }

        // --- Upper floors: the lost-city buildings ----------------------
        // Procedural footprints are reserved before the carve, then their ground
        // cells are opened into enterable halls. BuildingComponent materializes
        // the planned upper storeys, half-storeys, roofs, entrances, and facade
        // roles above those halls. Only the ground floor affects traversal; the
        // upper stack is cosmetic. The plan owns a local seed, so this dressing
        // never consumes the hub's UnityEngine.Random carve sequence.
        private void BuildUpperFloors()
        {
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

            int spawnedBuildings = BuildSharedProceduralBuildings();

            if (spawnedBuildings == 0)
            {
                Debug.Log("ArcadeGen3D: Build Upper Floors ran but found no solid-block buildings to stack on. " +
                    "Solid blocks are what buildings sit on - check the rules' Solid Block Count is > 0, and note " +
                    "the edit-mode preview (rules-null) has no blocks, so buildings only appear in play.", this);
            }
        }

        private int BuildSharedProceduralBuildings()
        {
            int spawned = 0;
            foreach (ProceduralPlacement placement in proceduralPlacements)
            {
                if (placement.Plan == null)
                {
                    continue;
                }

                GameObject root = new GameObject(
                    $"ProceduralBuilding_{placement.OriginX}_{placement.OriginZ}");
                root.transform.SetParent(generatedRoomsParent, false);
                BuildingComponent building =
                    root.AddComponent<BuildingComponent>();
                building.ConfigureRuntimeKit(
                    null,
                    upperCellPrefab,
                    upperCellHalfPrefab,
                    new Vector3(
                        roomWidth,
                        upperFloorHeight,
                        roomLength),
                    halfFloorHeight,
                    GetRoomLocalPosition(
                        placement.OriginX,
                        placement.OriginZ),
                    roofHipPrefab,
                    roofSteppedPrefab,
                    roofSlopeLeftPrefab,
                    roofSlopeRightPrefab,
                    roofSlopeLeftCurvePrefab,
                    roofSlopeRightCurvePrefab,
                    roofFlatBlockPrefab,
                    roofFlatHalfBlockPrefab,
                    placement.Plan.Seed);

                foreach (Vector2Int column in placement.Plan.Columns)
                {
                    int x = placement.OriginX + column.x;
                    int z = placement.OriginZ + column.y;
                    Room3D ground = rooms[x, z];
                    if (ground == null)
                    {
                        continue;
                    }

                    ground.transform.SetParent(root.transform, true);
                    building.RegisterCell(
                        new Vector3Int(column.x, 0, column.y),
                        ground,
                        BuildingComponent.CellLayerType.Full,
                        false);

                    int fullHeight =
                        placement.Plan.FullHeights[column.x, column.y];
                    for (int y = 1; y < fullHeight; y++)
                    {
                        Room3D upper = SpawnRuntimeBuildingCell(
                            upperCellPrefab,
                            root.transform,
                            $"UpperCell_{column.x}_{column.y}_L{y}");
                        if (upper != null)
                        {
                            building.RegisterCell(
                                new Vector3Int(column.x, y, column.y),
                                upper,
                                BuildingComponent.CellLayerType.Full,
                                false);
                        }
                    }

                    if (placement.Plan.HalfTops[column.x, column.y])
                    {
                        Room3D half = SpawnRuntimeBuildingCell(
                            upperCellHalfPrefab,
                            root.transform,
                            $"HalfCell_{column.x}_{column.y}");
                        if (half != null)
                        {
                            building.RegisterCell(
                                new Vector3Int(
                                    column.x,
                                    fullHeight,
                                    column.y),
                                half,
                                BuildingComponent.CellLayerType.Half,
                                false);
                        }
                    }
                }

                building.RefreshStructure();
                SetRuntimeBuildingWallRoles(building);
                ApplyRuntimeBuildingEntrances(building, placement);
                foreach (KeyValuePair<
                         Vector2Int,
                         BuildingPlanUtility.RoofPlacement> roof in
                         placement.Plan.Roofs)
                {
                    building.ApplyRoofType(
                        new Vector3Int(roof.Key.x, 0, roof.Key.y),
                        roof.Value.Type,
                        (roof.Value.YawSteps + roofYawTrim) & 3);
                }
                building.SetDressingSeed(placement.Plan.Seed);
                building.DressWalls();
                spawned++;
            }
            return spawned;
        }

        private static Room3D SpawnRuntimeBuildingCell(
            GameObject prefab,
            Transform parent,
            string instanceName)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject instance = Instantiate(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = prefab.transform.localScale;
            instance.name = instanceName;
            Room3D room = instance.GetComponent<Room3D>();
            if (room == null)
            {
                Debug.LogWarning(
                    $"{prefab.name} has no Room3D; skipped runtime building cell.",
                    prefab);
                Destroy(instance);
            }
            return room;
        }

        private static void SetRuntimeBuildingWallRoles(
            BuildingComponent building)
        {
            foreach (BuildingComponent.AuthoredCell cell in
                     building.AuthoredCells)
            {
                if (cell == null || cell.Room == null)
                {
                    continue;
                }
                foreach (Room3D.Directions direction in CardinalDirections)
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

        private void ApplyRuntimeBuildingEntrances(
            BuildingComponent building,
            ProceduralPlacement placement)
        {
            foreach (Vector2Int column in placement.Plan.Columns)
            {
                int x = placement.OriginX + column.x;
                int z = placement.OriginZ + column.y;
                foreach (Room3D.Directions face in CardinalDirections)
                {
                    if (!IsDoorOpen(x, z, face)
                        || !TryGetNeighbor(
                            x,
                            z,
                            face,
                            out int nx,
                            out int nz)
                        || !IsWalkable(nx, nz)
                        || rooms[nx, nz] == null)
                    {
                        continue;
                    }

                    bool vertical = placement.Plan.Entrances.Exists(
                        entrance =>
                            entrance.Column == column
                            && entrance.Face == face
                            && entrance.Vertical);
                    Vector3Int coordinate = new Vector3Int(
                        column.x,
                        0,
                        column.y);
                    if (vertical)
                    {
                        building.ApplyVerticalEntranceColumn(
                            coordinate,
                            face);
                    }
                    else
                    {
                        building.ApplyWallType(
                            coordinate,
                            face,
                            WallSocket.AuthoredWallType.Entrance);
                    }
                }
            }
        }

        // Archived non-destructively for comparison while the shared
        // BuildingComponent pipeline settles. This predecessor has no callers and
        // is excluded from the player/editor assemblies.
#if false
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

#endif

        private bool HasMazeRoofPrefab(
            BuildingComponent.RoofCellType type)
        {
            switch (type)
            {
                case BuildingComponent.RoofCellType.None:
                    return true;
                case BuildingComponent.RoofCellType.Sloped:
                    return roofHipPrefab != null;
                case BuildingComponent.RoofCellType.Stepped:
                    return roofSteppedPrefab != null;
                case BuildingComponent.RoofCellType.SlopeLeft:
                    return roofSlopeLeftPrefab != null;
                case BuildingComponent.RoofCellType.SlopeRight:
                    return roofSlopeRightPrefab != null;
                case BuildingComponent.RoofCellType.SlopeLeftCurve:
                    return roofSlopeLeftCurvePrefab != null;
                case BuildingComponent.RoofCellType.SlopeRightCurve:
                    return roofSlopeRightCurvePrefab != null;
                case BuildingComponent.RoofCellType.Block:
                    return roofFlatBlockPrefab != null;
                case BuildingComponent.RoofCellType.HalfBlock:
                    return roofFlatHalfBlockPrefab != null;
                default:
                    return false;
            }
        }

#if false
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

#endif

        private readonly struct ExitCandidate
        {
            public readonly Vector2Int Cell;
            public readonly int PathDistance;
            public readonly int BuildingDepth;
            public readonly int OpenDoorCount;
            public readonly int PathTurns;
            public readonly int ManhattanDistance;

            public ExitCandidate(
                Vector2Int cell,
                int pathDistance,
                int buildingDepth,
                int openDoorCount,
                int pathTurns,
                int manhattanDistance)
            {
                Cell = cell;
                PathDistance = pathDistance;
                BuildingDepth = buildingDepth;
                OpenDoorCount = openDoorCount;
                PathTurns = pathTurns;
                ManhattanDistance = manhattanDistance;
            }
        }

        // Selects endpoints only after every topology-changing pass is done. The
        // exit is optimized from the preliminary start first; the player start is
        // then optimized from that final exit. This maximizes their real walkable
        // separation without changing a single carved wall or obstacle.
        private void OptimizeEndpointPlacements()
        {
            if (rooms == null)
            {
                return;
            }

            if (CurrentOptimizeExitPlacement
                && TryChooseOptimizedEndpoint(
                    startRoomIndex,
                    CurrentBuildingExitChance,
                    CurrentMinimumBuildingExitDepth,
                    CurrentMinimumBuildingExitDistanceRatio,
                    0x45584954,
                    out Vector2Int optimizedExit))
            {
                endRoomIndex = optimizedExit;
            }

            if (CurrentOptimizePlayerSpawnPlacement
                && TryChooseOptimizedEndpoint(
                    endRoomIndex,
                    CurrentBuildingPlayerSpawnChance,
                    CurrentMinimumBuildingPlayerSpawnDepth,
                    CurrentMinimumBuildingPlayerSpawnDistanceRatio,
                    0x53504157,
                    out Vector2Int optimizedStart))
            {
                startRoomIndex = optimizedStart;
            }
        }

        private bool TryChooseOptimizedEndpoint(
            Vector2Int origin,
            float indoorChance,
            int minimumBuildingDepth,
            float minimumBuildingDistanceRatio,
            int randomSalt,
            out Vector2Int selected)
        {
            selected = origin;
            if (!CanTraverseForExit(origin.x, origin.y))
            {
                return false;
            }

            BuildExitDistanceMap(
                origin,
                out int[,] pathDistance,
                out Vector2Int[,] previous);
            int[,] buildingDepth = BuildBuildingDepthMap();
            System.Random rng = new System.Random(
                ComputeExitTopologySeed() ^ randomSalt);

            List<ExitCandidate> outdoor = new List<ExitCandidate>();
            List<ExitCandidate> indoor = new List<ExitCandidate>();
            int farthestOutdoorDistance = -1;

            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);
                    if (cell == origin
                        || pathDistance[x, z] < 0
                        || !CanTraverseForExit(x, z))
                    {
                        continue;
                    }

                    ExitCandidate candidate = new ExitCandidate(
                        cell,
                        pathDistance[x, z],
                        buildingDepth[x, z],
                        CountTraversableOpenDoors(x, z),
                        CountPathTurns(cell, previous, pathDistance),
                        GetManhattanDistance(origin, cell));

                    if (IsSolidBlockCell(x, z))
                    {
                        if (buildingDepth[x, z] >= minimumBuildingDepth)
                        {
                            indoor.Add(candidate);
                        }
                    }
                    else
                    {
                        outdoor.Add(candidate);
                        farthestOutdoorDistance = Mathf.Max(
                            farthestOutdoorDistance,
                            candidate.PathDistance);
                    }
                }
            }

            int minimumIndoorDistance = farthestOutdoorDistance >= 0
                ? Mathf.CeilToInt(
                    farthestOutdoorDistance
                    * Mathf.Clamp01(minimumBuildingDistanceRatio))
                : 0;
            indoor.RemoveAll(candidate =>
                candidate.PathDistance < minimumIndoorDistance);

            ExitCandidate? chosen = null;
            bool chooseIndoor =
                indoor.Count > 0
                && (outdoor.Count == 0
                    || rng.NextDouble() < Mathf.Clamp01(indoorChance));
            if (chooseIndoor)
            {
                // Deep is a hard priority within the indoor lane. Distance and
                // branch quality decide between equally deep cells.
                int deepest = -1;
                foreach (ExitCandidate candidate in indoor)
                {
                    deepest = Mathf.Max(deepest, candidate.BuildingDepth);
                }
                indoor.RemoveAll(candidate =>
                    candidate.BuildingDepth < deepest);
                chosen = ChooseBestExitCandidate(indoor, rng);
            }
            else if (outdoor.Count > 0)
            {
                chosen = ChooseBestExitCandidate(outdoor, rng);
            }
            else if (indoor.Count > 0)
            {
                chosen = ChooseBestExitCandidate(indoor, rng);
            }

            if (!chosen.HasValue)
            {
                return false;
            }

            selected = chosen.Value.Cell;
            return true;
        }

        private void BuildExitDistanceMap(
            Vector2Int origin,
            out int[,] distance,
            out Vector2Int[,] previous)
        {
            distance = new int[CurrentNumX, CurrentNumZ];
            previous = new Vector2Int[CurrentNumX, CurrentNumZ];
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    distance[x, z] = -1;
                }
            }

            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            distance[origin.x, origin.y] = 0;
            frontier.Enqueue(origin);
            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                foreach (Room3D.Directions direction in CardinalDirections)
                {
                    if (!IsDoorOpen(cell.x, cell.y, direction)
                        || !TryGetNeighbor(
                            cell.x,
                            cell.y,
                            direction,
                            out int nx,
                            out int nz)
                        || distance[nx, nz] >= 0
                        || !CanTraverseForExit(nx, nz))
                    {
                        continue;
                    }

                    distance[nx, nz] =
                        distance[cell.x, cell.y] + 1;
                    previous[nx, nz] = cell;
                    frontier.Enqueue(new Vector2Int(nx, nz));
                }
            }
        }

        // Multi-source BFS within procedural-building cells. Every cell with a
        // real open doorway to the street starts at depth 0; deeper cells count
        // interior room steps from their nearest actual entrance.
        private int[,] BuildBuildingDepthMap()
        {
            int[,] depth = new int[CurrentNumX, CurrentNumZ];
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    depth[x, z] = -1;
                    if (!IsSolidBlockCell(x, z)
                        || !CanTraverseForExit(x, z))
                    {
                        continue;
                    }

                    foreach (Room3D.Directions direction in CardinalDirections)
                    {
                        if (IsDoorOpen(x, z, direction)
                            && TryGetNeighbor(
                                x,
                                z,
                                direction,
                                out int nx,
                                out int nz)
                            && !IsSolidBlockCell(nx, nz)
                            && CanTraverseForExit(nx, nz))
                        {
                            depth[x, z] = 0;
                            frontier.Enqueue(new Vector2Int(x, z));
                            break;
                        }
                    }
                }
            }

            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                foreach (Room3D.Directions direction in CardinalDirections)
                {
                    if (!IsDoorOpen(cell.x, cell.y, direction)
                        || !TryGetNeighbor(
                            cell.x,
                            cell.y,
                            direction,
                            out int nx,
                            out int nz)
                        || !IsSolidBlockCell(nx, nz)
                        || depth[nx, nz] >= 0
                        || !CanTraverseForExit(nx, nz))
                    {
                        continue;
                    }

                    depth[nx, nz] = depth[cell.x, cell.y] + 1;
                    frontier.Enqueue(new Vector2Int(nx, nz));
                }
            }

            return depth;
        }

        private ExitCandidate ChooseBestExitCandidate(
            List<ExitCandidate> candidates,
            System.Random rng)
        {
            ExitCandidate best = candidates[0];
            List<ExitCandidate> ties = new List<ExitCandidate> { best };
            for (int i = 1; i < candidates.Count; i++)
            {
                ExitCandidate candidate = candidates[i];
                int comparison = CompareExitCandidates(candidate, best);
                if (comparison > 0)
                {
                    best = candidate;
                    ties.Clear();
                    ties.Add(candidate);
                }
                else if (comparison == 0)
                {
                    ties.Add(candidate);
                }
            }

            return ties[rng.Next(ties.Count)];
        }

        private static int CompareExitCandidates(
            ExitCandidate a,
            ExitCandidate b)
        {
            int comparison = a.PathDistance.CompareTo(b.PathDistance);
            if (comparison != 0)
            {
                return comparison;
            }

            // A low-degree destination is more likely to sit at the end of a
            // branch instead of beside a broad plaza or shortcut.
            comparison = b.OpenDoorCount.CompareTo(a.OpenDoorCount);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = a.PathTurns.CompareTo(b.PathTurns);
            return comparison != 0
                ? comparison
                : a.ManhattanDistance.CompareTo(b.ManhattanDistance);
        }

        private int CountTraversableOpenDoors(int x, int z)
        {
            int count = 0;
            foreach (Room3D.Directions direction in CardinalDirections)
            {
                if (IsDoorOpen(x, z, direction)
                    && TryGetNeighbor(
                        x,
                        z,
                        direction,
                        out int nx,
                        out int nz)
                    && CanTraverseForExit(nx, nz))
                {
                    count++;
                }
            }
            return count;
        }

        private int CountPathTurns(
            Vector2Int end,
            Vector2Int[,] previous,
            int[,] distance)
        {
            int turns = 0;
            Vector2Int cell = end;
            Vector2Int priorDirection = Vector2Int.zero;
            while (distance[cell.x, cell.y] > 0)
            {
                Vector2Int parent = previous[cell.x, cell.y];
                Vector2Int direction = parent - cell;
                if (priorDirection != Vector2Int.zero
                    && direction != priorDirection)
                {
                    turns++;
                }
                priorDirection = direction;
                cell = parent;
            }
            return turns;
        }

        private bool CanTraverseForExit(int x, int z)
        {
            return InBounds(x, z)
                && rooms[x, z] != null
                && !rooms[x, z].IsPit;
        }

        private int ComputeExitTopologySeed()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + CurrentNumX;
                hash = hash * 31 + CurrentNumZ;
                hash = hash * 31 + startRoomIndex.x;
                hash = hash * 31 + startRoomIndex.y;
                for (int x = 0; x < CurrentNumX; x++)
                {
                    for (int z = 0; z < CurrentNumZ; z++)
                    {
                        Room3D room = rooms[x, z];
                        hash = hash * 31 + (room != null ? 1 : 0);
                        hash = hash * 31
                            + (IsSolidBlockCell(x, z) ? 1 : 0);
                        if (room == null)
                        {
                            continue;
                        }
                        foreach (Room3D.Directions direction
                                 in CardinalDirections)
                        {
                            hash = hash * 31
                                + (room.IsWallClosed(direction) ? 1 : 0);
                        }
                    }
                }
                return hash;
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

            Room3D startRoom =
                rooms[startRoomIndex.x, startRoomIndex.y];
            PlayerSpawn playerSpawn =
                startRoom.GetComponentInChildren<PlayerSpawn>(true);
            if (playerSpawn == null)
            {
                playerSpawn = FindGeneratedPlayerSpawn();
                if (playerSpawn == null)
                {
                    Debug.LogWarning(
                        "The generated maze does not contain a PlayerSpawn.",
                        this);
                    return;
                }

                MovePlayerSpawnMarker(playerSpawn, startRoom);
            }

            playerSpawn.RespawnExistingAtThisSpawn();
        }

        private PlayerSpawn FindGeneratedPlayerSpawn()
        {
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (rooms[x, z] == null)
                    {
                        continue;
                    }

                    PlayerSpawn marker =
                        rooms[x, z]
                            .GetComponentInChildren<PlayerSpawn>(true);
                    if (marker != null)
                    {
                        return marker;
                    }
                }
            }

            return null;
        }

        private static void MovePlayerSpawnMarker(
            PlayerSpawn marker,
            Room3D destinationRoom)
        {
            Room3D sourceRoom = marker.GetComponentInParent<Room3D>();
            Vector3 roomLocalPosition = sourceRoom != null
                ? sourceRoom.transform.InverseTransformPoint(
                    marker.transform.position)
                : new Vector3(0f, 1.5f, 0f);
            Quaternion roomLocalRotation = sourceRoom != null
                ? Quaternion.Inverse(sourceRoom.transform.rotation)
                    * marker.transform.rotation
                : Quaternion.identity;

            marker.transform.SetParent(
                destinationRoom.transform,
                false);
            marker.transform.localPosition = roomLocalPosition;
            marker.transform.localRotation = roomLocalRotation;
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

        private float CurrentBraidRate =>
            activeRules != null
                ? Mathf.Clamp01(activeRules.braidRate)
                : Mathf.Clamp01(braidRate);

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
        private int CurrentBuildingHeightLimit => activeRules != null ? Mathf.Clamp(activeRules.buildingHeightLimit, 1, 8) : 1;
        private int CurrentBuildingEntranceCount => activeRules != null ? Mathf.Max(1, activeRules.buildingEntranceCount) : 1;
        private float CurrentBuildingExitChance => activeRules != null ? Mathf.Clamp01(activeRules.buildingExitChance) : 0f;
        private bool CurrentOptimizeExitPlacement =>
            activeRules != null && activeRules.optimizeExitPlacement;
        private int CurrentMinimumBuildingExitDepth =>
            activeRules != null
                ? Mathf.Max(1, activeRules.minimumBuildingExitDepth)
                : 1;
        private float CurrentMinimumBuildingExitDistanceRatio =>
            activeRules != null
                ? Mathf.Clamp01(activeRules.minimumBuildingExitDistanceRatio)
                : 0.75f;
        private bool CurrentOptimizePlayerSpawnPlacement =>
            activeRules != null
            && activeRules.optimizePlayerSpawnPlacement;
        private float CurrentBuildingPlayerSpawnChance =>
            activeRules != null
                ? Mathf.Clamp01(activeRules.buildingPlayerSpawnChance)
                : 0f;
        private int CurrentMinimumBuildingPlayerSpawnDepth =>
            activeRules != null
                ? Mathf.Max(
                    1,
                    activeRules.minimumBuildingPlayerSpawnDepth)
                : 1;
        private float CurrentMinimumBuildingPlayerSpawnDistanceRatio =>
            activeRules != null
                ? Mathf.Clamp01(
                    activeRules.minimumBuildingPlayerSpawnDistanceRatio)
                : 0.75f;
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
            // inputs are inert: BraidMaze does nothing when its configured rate
            // is 0, and the pit passes do nothing when no pit rooms were placed.
            PostCarveProcessing();

            if (activeRules != null
                && activeRules.respawnPlayerAtStart
                && CurrentOptimizePlayerSpawnPlacement)
            {
                RespawnPlayerAtStart();
            }

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
