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
            else
            {
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

            int generationStepLimit = Mathf.Max(1, CurrentNumX * CurrentNumZ * 4);
            for (int i = 0; i < generationStepLimit; i++)
            {
                if (GenerateStep())
                {
                    if (ShouldActivateEndRoomExit())
                    {
                        ActivateEndRoomExitClerk();
                    }

                    return true;
                }
            }

            Debug.LogWarning("ArcadeGen3D regeneration hit the safety step limit.", this);
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
            CreateGeneratedRoomsParent();
            BuildRoomGrid();
            return true;
        }

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

        public List<Tuple<Room3D.Directions, Room3D>> GetUnvisitedNeighbors(int cx, int cz)
        {
            List<Tuple<Room3D.Directions, Room3D>> neighbours =
                new List<Tuple<Room3D.Directions, Room3D>>();

            foreach (Room3D.Directions dir in Enum.GetValues(typeof(Room3D.Directions)))
            {
                int x = cx;
                int z = cz;

                switch (dir)
                {
                    case Room3D.Directions.NORTH:
                        if (z < CurrentNumZ - 1)
                        {
                            ++z;
                            if (!rooms[x, z].visited)
                            {
                                neighbours.Add(new Tuple<Room3D.Directions, Room3D>(
                                    Room3D.Directions.NORTH,
                                    rooms[x, z]));
                            }
                        }
                        break;

                    case Room3D.Directions.EAST:
                        if (x < CurrentNumX - 1)
                        {
                            ++x;
                            if (!rooms[x, z].visited)
                            {
                                neighbours.Add(new Tuple<Room3D.Directions, Room3D>(
                                    Room3D.Directions.EAST,
                                    rooms[x, z]));
                            }
                        }
                        break;

                    case Room3D.Directions.SOUTH:
                        if (z > 0)
                        {
                            --z;
                            if (!rooms[x, z].visited)
                            {
                                neighbours.Add(new Tuple<Room3D.Directions, Room3D>(
                                    Room3D.Directions.SOUTH,
                                    rooms[x, z]));
                            }
                        }
                        break;

                    case Room3D.Directions.WEST:
                        if (x > 0)
                        {
                            --x;
                            if (!rooms[x, z].visited)
                            {
                                neighbours.Add(new Tuple<Room3D.Directions, Room3D>(
                                    Room3D.Directions.WEST,
                                    rooms[x, z]));
                            }
                        }
                        break;
                }
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
