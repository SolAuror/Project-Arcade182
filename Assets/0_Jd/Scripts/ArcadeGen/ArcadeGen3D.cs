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

        // Arcade grid and carving state.
        private Room3D[,] rooms;

        // Footprint + obstacle masks (rules lane only). active = this cell is
        // part of the level (a room is instantiated); pit = this active cell is
        // an obstacle the walkable graph carves around. On the hub lane every
        // cell is active and none is a pit, so behaviour is unchanged.
        private bool[,] active;
        private bool[,] pit;

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
            BuildActiveMask();     // organic footprint (rules lane); hub -> all active, no RNG
            ChoosePitCells();      // obstacle-first pits (rules lane); hub -> none, no RNG
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

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

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

        // Flood the walkable cells (active and not a pit) from the start over
        // grid adjacency and confirm the flood covers every walkable cell. If it
        // does, the pre-carve graph is one piece and a spanning tree will reach
        // the exit and every other room.
        private bool WalkableRegionConnected()
        {
            int walkableTotal = 0;
            for (int x = 0; x < CurrentNumX; x++)
            {
                for (int z = 0; z < CurrentNumZ; z++)
                {
                    if (active[x, z] && !pit[x, z])
                    {
                        walkableTotal++;
                    }
                }
            }

            if (walkableTotal == 0 || pit[startRoomIndex.x, startRoomIndex.y] || !active[startRoomIndex.x, startRoomIndex.y])
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
                        active[nx, nz] && !pit[nx, nz] && !seen[nx, nz])
                    {
                        seen[nx, nz] = true;
                        reached++;
                        frontier.Enqueue(new Vector2Int(nx, nz));
                    }
                }
            }

            return reached == walkableTotal;
        }

        private bool InBounds(int x, int z) => x >= 0 && x < CurrentNumX && z >= 0 && z < CurrentNumZ;
        private bool IsActiveCell(int x, int z) => InBounds(x, z) && active != null && active[x, z];
        private bool IsPitCell(int x, int z) => InBounds(x, z) && pit != null && pit[x, z];
        private bool IsWalkable(int x, int z) => IsActiveCell(x, z) && !IsPitCell(x, z);

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
            Renderer[] renderers = sizeSourcePrefab.GetComponentsInChildren<Renderer>();

            bool foundRenderer = false;
            Vector3 minBounds = Vector3.positiveInfinity;
            Vector3 maxBounds = Vector3.negativeInfinity;

            foreach (Renderer ren in renderers)
            {
                if (!ren.enabled)
                {
                    continue;
                }

                foundRenderer = true;
                minBounds = Vector3.Min(minBounds, ren.bounds.min);
                maxBounds = Vector3.Max(maxBounds, ren.bounds.max);
            }

            if (!foundRenderer)
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
                return;
            }

            BraidMaze();
            RevealPits();
            OpenConjoinedPitShafts();
            VerifyExitReachable();
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
                    // conjoined pit); sides facing the masked-out void keep their
                    // wall so the level edge stays sealed.
                    foreach (Room3D.Directions dir in CardinalDirections)
                    {
                        if (TryGetNeighbor(x, z, dir, out int nx, out int nz) &&
                            IsActiveCell(nx, nz) && rooms[nx, nz] != null)
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
        private int CurrentPitCount => activeRules != null ? Mathf.Max(0, activeRules.pitCount) : 0;
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
