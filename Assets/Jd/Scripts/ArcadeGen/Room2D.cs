using System.Collections.Generic;
using UnityEngine;

namespace Sol // Controls room walls for generated maze paths.
{
    public class Room2D : MonoBehaviour
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
        [Tooltip("Wall object on the +Y side.")]
        [SerializeField] private GameObject NorthWall;

        [Tooltip("Wall object on the -Y side.")]
        [SerializeField] private GameObject SouthWall;

        [Tooltip("Wall object on the +X side.")]
        [SerializeField] private GameObject EastWall;

        [Tooltip("Wall object on the -X side.")]
        [SerializeField] private GameObject WestWall;

        private readonly Dictionary<Directions, GameObject> walls =
            new Dictionary<Directions, GameObject>();

        private readonly Dictionary<Directions, bool> dirFlags =
            new Dictionary<Directions, bool>();

        public Vector2Int Index { get; set; }
        public bool visited { get; set; } = false;

        private void Start()
        {
            walls[Directions.NORTH] = NorthWall;
            walls[Directions.SOUTH] = SouthWall;
            walls[Directions.EAST] = EastWall;
            walls[Directions.WEST] = WestWall;
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

        private void SetActive(Directions dir, bool flag)
        {
            if (!walls.TryGetValue(dir, out GameObject wall) || wall == null)
            {
                Debug.LogWarning($"{name} is missing a wall reference for {dir}.");
                return;
            }

            wall.SetActive(flag);
        }
    }
}
