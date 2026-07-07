using System.Collections.Generic;
using UnityEngine;

namespace Sol // Controls room walls for generated maze paths.
{
    public class Room3D : MonoBehaviour
    {
        public enum Directions
        {
            NORTH,
            SOUTH,
            EAST,
            WEST,
            NONE,
        }

        [Header("Walls")]
        [Tooltip("Wall object on the +Z side.")]
        [SerializeField] private GameObject NorthWall;

        [Tooltip("Wall object on the -Z side.")]
        [SerializeField] private GameObject SouthWall;

        [Tooltip("Wall object on the +X side.")]
        [SerializeField] private GameObject EastWall;

        [Tooltip("Wall object on the -X side.")]
        [SerializeField] private GameObject WestWall;

        [Header("Optional Parts")]
        [Tooltip("Optional roof object enabled when the room initializes.")]
        [SerializeField] private GameObject roofObject;

        [Header("Generation")]
        [Tooltip("Chance weight used when this room is in a random room list.")]
        [SerializeField, Min(0)] private int spawnWeight = 1;

        private readonly Dictionary<Directions, GameObject> walls =
            new Dictionary<Directions, GameObject>();

        private readonly Dictionary<Directions, bool> dirFlags =
            new Dictionary<Directions, bool>();

        private bool wallsInitialized;

        public Vector3Int Index { get; set; }
        public int SpawnWeight => Mathf.Max(0, spawnWeight);
        public bool visited { get; set; } = false;

        private void Awake()
        {
            InitializeWalls();
        }

        public void SetDirFlag(Directions dir, bool flag)
        {
            if (dir == Directions.NONE)
            {
                return;
            }

            dirFlags[dir] = flag;
            SetActive(dir, flag);
        }

        public bool IsWallClosed(Directions dir)
        {
            if (dir == Directions.NONE)
            {
                return false;
            }

            InitializeWalls();

            if (dirFlags.TryGetValue(dir, out bool isClosed))
            {
                return isClosed;
            }

            return walls.TryGetValue(dir, out GameObject wall) && wall != null && wall.activeSelf;
        }

        public bool TryGetWallTransform(Directions dir, out Transform wallTransform)
        {
            InitializeWalls();

            if (dir != Directions.NONE &&
                walls.TryGetValue(dir, out GameObject wall) &&
                wall != null)
            {
                wallTransform = wall.transform;
                return true;
            }

            wallTransform = null;
            return false;
        }

        private void InitializeWalls()
        {
            if (wallsInitialized)
            {
                return;
            }

            walls.Clear();
            walls[Directions.NORTH] = NorthWall;
            walls[Directions.SOUTH] = SouthWall;
            walls[Directions.EAST] = EastWall;
            walls[Directions.WEST] = WestWall;

            if (roofObject != null)
            {
                roofObject.SetActive(true);
            }

            wallsInitialized = true;
        }

        private void SetActive(Directions dir, bool flag)
        {
            InitializeWalls();

            if (!walls.TryGetValue(dir, out GameObject wall) || wall == null)
            {
                Debug.LogWarning($"{name} is missing a wall reference for {dir}.");
                return;
            }

            wall.SetActive(flag);
        }
    }
}
