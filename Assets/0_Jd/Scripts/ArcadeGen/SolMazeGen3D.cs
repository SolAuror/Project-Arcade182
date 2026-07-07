using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    public class ArcadeGen3D : MonoBehaviour
    {
        private enum SpecialRoomPlacementMode
        {
            FixedCorners,
            RandomStartAndEnd
        }

        [Header("Room Prefabs")]
        [Tooltip("Weighted room prefabs used for regular maze cells.")]
        [SerializeField] private List<GameObject> possibleRoomPrefabs = new List<GameObject>();

        [Tooltip("Room prefab used for the player start.")]
        [SerializeField] private GameObject firstRoomPrefab;

        [Tooltip("Room prefab used for the maze exit.")]
        [SerializeField] private GameObject lastRoomPrefab;

        [Tooltip("Choose fixed corner start/end rooms or random positions.")]
        [SerializeField] private SpecialRoomPlacementMode specialRoomPlacementMode = SpecialRoomPlacementMode.FixedCorners;

        [SerializeField, HideInInspector] private GameObject roomPrefab;

        [Header("Maze Size")]
        [Tooltip("Number of rooms along local X.")]
        [SerializeField] private int numX = 10;

        [Tooltip("Number of rooms along local Z.")]
        [SerializeField] private int numZ = 10;

        [Header("Generation")]
        [Tooltip("Generate and carve the maze automatically when the scene starts.")]
        [SerializeField] private bool autoGenerateOnStart = true;

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
        private Vector2Int startRoomIndex = Vector2Int.zero;
        private Vector2Int endRoomIndex = Vector2Int.zero;
        private bool generating;

        private void Start()
        {
            if (autoGenerateOnStart)
            {
                CreateArcade();
            }
            else
            {
                RebuildRooms();
            }
        }

        private void Update()
        {
            // Modern Input System approach.
            if (UnityEngine.InputSystem.Keyboard.current != null &&
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

            if (!RebuildRooms())
            {
                return;
            }

            Reset();
            ApplyOptionalOuterOpenings();

            if (respawnPlayerAtStartOnRegenerate)
            {
                RespawnPlayerAtStart();
            }

            rooms[startRoomIndex.x, startRoomIndex.y].visited = true;
            stack.Push(rooms[startRoomIndex.x, startRoomIndex.y]);

            StartCoroutine(Coroutine_ArcadeGen());
        }

        private bool RebuildRooms()
        {
            stack.Clear();

            if (numX <= 0 || numZ <= 0)
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

            if (possibleRoomPrefabs != null)
            {
                foreach (GameObject possibleRoomPrefab in possibleRoomPrefabs)
                {
                    AddValidRoomPrefab(possibleRoomPrefab);
                }
            }

            if (validRoomPrefabs.Count == 0)
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
            rooms = new Room3D[numX, numZ];

            for (int x = 0; x < numX; ++x)
            {
                for (int z = 0; z < numZ; ++z)
                {
                    GameObject selectedPrefab = GetRoomPrefabForCell(x, z);
                    Vector3 roomLocalPosition = new Vector3(x * roomWidth, 0f, z * roomLength);
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

        private GameObject GetRoomPrefabForCell(int x, int z)
        {
            Vector2Int cellIndex = new Vector2Int(x, z);

            if (cellIndex == startRoomIndex)
            {
                GameObject fixedRoomPrefab = GetFixedRoomPrefab(firstRoomPrefab, "First Room Prefab");
                if (fixedRoomPrefab != null)
                {
                    return fixedRoomPrefab;
                }
            }

            if (cellIndex == endRoomIndex)
            {
                GameObject fixedRoomPrefab = GetFixedRoomPrefab(lastRoomPrefab, "Last Room Prefab");
                if (fixedRoomPrefab != null)
                {
                    return fixedRoomPrefab;
                }
            }

            return GetRandomRoomPrefab();
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
            startRoomIndex = Vector2Int.zero;
            endRoomIndex = new Vector2Int(numX - 1, numZ - 1);

            if (specialRoomPlacementMode != SpecialRoomPlacementMode.RandomStartAndEnd)
            {
                return;
            }

            int cellCount = numX * numZ;
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

        private Vector2Int FlatIndexToRoomIndex(int flatIndex)
        {
            return new Vector2Int(flatIndex % numX, flatIndex / numX);
        }

        private void ApplyOptionalOuterOpenings()
        {
            if (openStartOuterWall)
            {
                RemoveRoomWall(startRoomIndex.x, startRoomIndex.y, startOuterWallDirection);
            }

            if (openEndOuterWall)
            {
                RemoveRoomWall(endRoomIndex.x, endRoomIndex.y, endOuterWallDirection);
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
                    if (z < numZ - 1)
                    {
                        opp = Room3D.Directions.SOUTH;
                        ++z;
                    }
                    break;

                case Room3D.Directions.EAST:
                    if (x < numX - 1)
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
                        if (z < numZ - 1)
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
                        if (x < numX - 1)
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
                for (int i = 0; i < 10; i++) // Process 10 steps per frame.
                {
                    flag = GenerateStep();
                    if (flag)
                    {
                        break;
                    }
                }

                yield return null; // Wait one frame.
            }

            ActivateEndRoomExitClerk();
            generating = false;
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

        private void Reset()
        {
            for (int i = 0; i < numX; ++i)
            {
                for (int j = 0; j < numZ; ++j)
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
    }
}
