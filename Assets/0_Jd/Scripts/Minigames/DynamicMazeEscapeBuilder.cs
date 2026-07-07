using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Dynamic Maze Escape Builder")]
    public class DynamicMazeEscapeBuilder : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Transform mazeRoot;
        [SerializeField] private float cellSize = 4f;
        [SerializeField] private float wallThickness = 0.25f;
        [SerializeField] private float wallHeight = 3f;
        [SerializeField] private Vector2Int startCell = Vector2Int.zero;

        [Header("Player")]
        [SerializeField] private Transform player;
        [SerializeField] private float playerSpawnHeight = 0.2f;

        [Header("Materials")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material finishMaterial;

        private readonly Stack<Vector2Int> path = new Stack<Vector2Int>();
        private readonly List<Vector2Int> unvisitedNeighbors = new List<Vector2Int>(4);
        private bool[,] visited;
        private CellWalls[,] cells;
        private int width;
        private int depth;

        private struct CellWalls
        {
            public bool north;
            public bool east;
            public bool south;
            public bool west;
        }

        public void BuildMaze(int mazeWidth, int mazeDepth, TimedMazeEscapeGame game)
        {
            width = Mathf.Max(2, mazeWidth);
            depth = Mathf.Max(2, mazeDepth);
            startCell.x = Mathf.Clamp(startCell.x, 0, width - 1);
            startCell.y = Mathf.Clamp(startCell.y, 0, depth - 1);

            EnsureRoot();
            EnsureMaterials();
            ClearMaze();
            GenerateMaze();
            BuildGeometry(game);
            MovePlayerToStart();
        }

        private void OnValidate()
        {
            cellSize = Mathf.Max(1f, cellSize);
            wallThickness = Mathf.Max(0.05f, wallThickness);
            wallHeight = Mathf.Max(0.5f, wallHeight);
            playerSpawnHeight = Mathf.Max(0f, playerSpawnHeight);
        }

        private void EnsureRoot()
        {
            if (mazeRoot != null)
            {
                return;
            }

            Transform existingRoot = transform.Find("Generated Maze");
            if (existingRoot != null)
            {
                mazeRoot = existingRoot;
                return;
            }

            GameObject rootObject = new GameObject("Generated Maze");
            mazeRoot = rootObject.transform;
            mazeRoot.SetParent(transform, false);
        }

        private void EnsureMaterials()
        {
            floorMaterial ??= CreateRuntimeMaterial("Maze Floor Runtime", new Color(0.25f, 0.25f, 0.27f));
            wallMaterial ??= CreateRuntimeMaterial("Maze Wall Runtime", new Color(0.7f, 0.68f, 0.5f));
            finishMaterial ??= CreateRuntimeMaterial("Maze Finish Runtime", new Color(0.15f, 0.75f, 0.3f));
        }

        private Material CreateRuntimeMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private void ClearMaze()
        {
            for (int i = mazeRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = mazeRoot.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void GenerateMaze()
        {
            cells = new CellWalls[width, depth];
            visited = new bool[width, depth];
            path.Clear();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    cells[x, z] = new CellWalls
                    {
                        north = true,
                        east = true,
                        south = true,
                        west = true
                    };
                }
            }

            Vector2Int current = startCell;
            visited[current.x, current.y] = true;
            path.Push(current);

            while (path.Count > 0)
            {
                current = path.Peek();
                CollectUnvisitedNeighbors(current);

                if (unvisitedNeighbors.Count == 0)
                {
                    path.Pop();
                    continue;
                }

                Vector2Int next = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
                OpenWallBetween(current, next);
                visited[next.x, next.y] = true;
                path.Push(next);
            }
        }

        private void CollectUnvisitedNeighbors(Vector2Int cell)
        {
            unvisitedNeighbors.Clear();
            TryAddNeighbor(cell.x, cell.y + 1);
            TryAddNeighbor(cell.x + 1, cell.y);
            TryAddNeighbor(cell.x, cell.y - 1);
            TryAddNeighbor(cell.x - 1, cell.y);
        }

        private void TryAddNeighbor(int x, int z)
        {
            if (x < 0 || z < 0 || x >= width || z >= depth || visited[x, z])
            {
                return;
            }

            unvisitedNeighbors.Add(new Vector2Int(x, z));
        }

        private void OpenWallBetween(Vector2Int current, Vector2Int next)
        {
            int dx = next.x - current.x;
            int dz = next.y - current.y;

            CellWalls currentWalls = cells[current.x, current.y];
            CellWalls nextWalls = cells[next.x, next.y];

            if (dx > 0)
            {
                currentWalls.east = false;
                nextWalls.west = false;
            }
            else if (dx < 0)
            {
                currentWalls.west = false;
                nextWalls.east = false;
            }
            else if (dz > 0)
            {
                currentWalls.north = false;
                nextWalls.south = false;
            }
            else if (dz < 0)
            {
                currentWalls.south = false;
                nextWalls.north = false;
            }

            cells[current.x, current.y] = currentWalls;
            cells[next.x, next.y] = nextWalls;
        }

        private void BuildGeometry(TimedMazeEscapeGame game)
        {
            Vector3 mazeSize = new Vector3(width * cellSize, 0.2f, depth * cellSize);
            Vector3 floorCenter = CellToWorld((width - 1) * 0.5f, (depth - 1) * 0.5f);
            CreateCube("Floor", floorCenter + Vector3.down * 0.1f, mazeSize, floorMaterial, true);

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    CellWalls walls = cells[x, z];
                    Vector3 center = CellToWorld(x, z);

                    if (walls.north)
                    {
                        CreateWall($"Wall_N_{x}_{z}", center + Vector3.forward * (cellSize * 0.5f), true);
                    }

                    if (walls.east)
                    {
                        CreateWall($"Wall_E_{x}_{z}", center + Vector3.right * (cellSize * 0.5f), false);
                    }

                    if (z == 0 && walls.south)
                    {
                        CreateWall($"Wall_S_{x}_{z}", center + Vector3.back * (cellSize * 0.5f), true);
                    }

                    if (x == 0 && walls.west)
                    {
                        CreateWall($"Wall_W_{x}_{z}", center + Vector3.left * (cellSize * 0.5f), false);
                    }
                }
            }

            CreateFinish(game);
        }

        private void CreateWall(string wallName, Vector3 position, bool horizontal)
        {
            Vector3 scale = horizontal
                ? new Vector3(cellSize + wallThickness, wallHeight, wallThickness)
                : new Vector3(wallThickness, wallHeight, cellSize + wallThickness);

            CreateCube(wallName, position + Vector3.up * (wallHeight * 0.5f), scale, wallMaterial, true);
        }

        private void CreateFinish(TimedMazeEscapeGame game)
        {
            Vector2Int finishCell = new Vector2Int(width - 1, depth - 1);
            GameObject finish = CreateCube(
                "Finish Trigger",
                CellToWorld(finishCell.x, finishCell.y) + Vector3.up * 0.15f,
                new Vector3(cellSize * 0.45f, 0.3f, cellSize * 0.45f),
                finishMaterial,
                true);

            Collider trigger = finish.GetComponent<Collider>();
            trigger.isTrigger = true;

            TimedMazeEscapeFinish escapeFinish = finish.AddComponent<TimedMazeEscapeFinish>();
            escapeFinish.AssignGame(game);
        }

        private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(mazeRoot, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;

            if (material != null && cube.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider && cube.TryGetComponent(out Collider collider))
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            return cube;
        }

        private void MovePlayerToStart()
        {
            Transform targetPlayer = player;
            if (targetPlayer == null)
            {
                CharacterController characterController = FindFirstObjectByType<CharacterController>();
                if (characterController != null)
                {
                    targetPlayer = characterController.transform;
                }
            }

            if (targetPlayer == null)
            {
                return;
            }

            Vector3 spawnPosition = mazeRoot.TransformPoint(CellToWorld(startCell.x, startCell.y) + Vector3.up * playerSpawnHeight);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            CharacterController controller = targetPlayer.GetComponent<CharacterController>();
            bool wasControllerEnabled = controller != null && controller.enabled;
            if (wasControllerEnabled)
            {
                controller.enabled = false;
            }

            Rigidbody rb = targetPlayer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            targetPlayer.SetPositionAndRotation(spawnPosition, spawnRotation);

            if (wasControllerEnabled)
            {
                controller.enabled = true;
            }
        }

        private Vector3 CellToWorld(float x, float z)
        {
            return new Vector3(x * cellSize, 0f, z * cellSize);
        }
    }
}
