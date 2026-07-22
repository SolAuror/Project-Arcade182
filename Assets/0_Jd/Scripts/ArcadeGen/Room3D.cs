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

        [Header("Pit")]
        [Tooltip("Authoring hint only. Pits are designated at runtime by the maze generator, which removes a placed room's floor and reveals a void beneath it; leave this off on ordinary rooms.")]
        [SerializeField] private bool isPit;

        [Tooltip("Floor object hidden when this room is turned into a pit. Falls back to a child named \"RoomFloor\" when left empty.")]
        [SerializeField] private GameObject roomFloor;

        [Tooltip("Below-floor retaining wall on the +Z side. Stays closed by default; the generator only opens it between two adjacent pits so they conjoin into one shaft. Leave empty on non-pit rooms.")]
        [SerializeField] private GameObject NorthShaft;

        [Tooltip("Below-floor retaining wall on the -Z side.")]
        [SerializeField] private GameObject SouthShaft;

        [Tooltip("Below-floor retaining wall on the +X side.")]
        [SerializeField] private GameObject EastShaft;

        [Tooltip("Below-floor retaining wall on the -X side.")]
        [SerializeField] private GameObject WestShaft;

        [Header("Generation")]
        [Tooltip("Chance weight used when this room is in a random room list.")]
        [SerializeField, Min(0)] private int spawnWeight = 1;

        private readonly Dictionary<Directions, GameObject> walls =
            new Dictionary<Directions, GameObject>();

        private readonly Dictionary<Directions, GameObject> shafts =
            new Dictionary<Directions, GameObject>();

        private readonly Dictionary<Directions, bool> dirFlags =
            new Dictionary<Directions, bool>();

        private bool wallsInitialized;

        public Vector3Int Index { get; set; }
        public int SpawnWeight => Mathf.Max(0, spawnWeight);

        /// <summary>True when this room is an impassable pit; the generator routes around it.</summary>
        public bool IsPit { get; private set; }

        public bool visited { get; set; } = false;

        private void Awake()
        {
            IsPit = isPit;
            InitializeWalls();
        }

        /// <summary>
        /// Turns this already-placed room into a pit: hides the floor and spawns
        /// the void apparatus (retaining shafts, corner pillars, fog) as a child
        /// so the player and enemies fall through into the global kill/respawn
        /// plane. The void is parented to this room, never to the maze root, so
        /// generation bookkeeping can never orphan it.
        /// </summary>
        public void RevealPit(GameObject voidPrefab)
        {
            IsPit = true;
            InitializeWalls();

            GameObject floor = roomFloor != null ? roomFloor : FindChild("RoomFloor");
            if (floor != null)
            {
                floor.SetActive(false);
            }

            if (voidPrefab == null)
            {
                return;
            }

            GameObject voidInstance = Instantiate(voidPrefab, transform);
            voidInstance.transform.localPosition = Vector3.zero;
            voidInstance.transform.localRotation = Quaternion.identity;
            voidInstance.name = voidPrefab.name;

            // Retaining walls live inside the spawned void; adopt them so the
            // conjoin pass can open a side between two neighbouring pits.
            RegisterShaft(voidInstance.transform, "NShaft", Directions.NORTH);
            RegisterShaft(voidInstance.transform, "SShaft", Directions.SOUTH);
            RegisterShaft(voidInstance.transform, "EShaft", Directions.EAST);
            RegisterShaft(voidInstance.transform, "WShaft", Directions.WEST);
        }

        private void RegisterShaft(Transform voidRoot, string childName, Directions dir)
        {
            Transform shaft = voidRoot.Find(childName);
            if (shaft != null)
            {
                shafts[dir] = shaft.gameObject;
            }
        }

        private GameObject FindChild(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.gameObject : null;
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

            // Shaft segments are optional and only wired on pit rooms; a null
            // entry makes SetShaftOpen a safe no-op everywhere else.
            shafts.Clear();
            shafts[Directions.NORTH] = NorthShaft;
            shafts[Directions.SOUTH] = SouthShaft;
            shafts[Directions.EAST] = EastShaft;
            shafts[Directions.WEST] = WestShaft;

            if (roofObject != null)
            {
                roofObject.SetActive(true);
            }

            wallsInitialized = true;
        }

        /// <summary>
        /// Shows or hides the below-floor retaining wall on one side. Pit
        /// rooms keep every shaft closed so the hole is a contained well;
        /// the generator opens a side only when the neighbour is also a pit,
        /// merging the two wells into one continuous shaft. No-op on rooms
        /// that have no shaft wired for the direction.
        /// </summary>
        public void SetShaftOpen(Directions dir, bool open)
        {
            if (dir == Directions.NONE)
            {
                return;
            }

            InitializeWalls();

            if (shafts.TryGetValue(dir, out GameObject shaft) && shaft != null)
            {
                shaft.SetActive(!open);
            }
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
