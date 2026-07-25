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

        // Optional lost-city dressing on each wall; null on undressed rooms (the
        // hub), where DressWall falls back to the legacy show-when-closed toggle.
        private readonly Dictionary<Directions, WallSocket> sockets =
            new Dictionary<Directions, WallSocket>();

        private readonly Dictionary<Directions, GameObject> shafts =
            new Dictionary<Directions, GameObject>();

        private readonly Dictionary<Directions, bool> dirFlags =
            new Dictionary<Directions, bool>();

        // Optional lost-city crown decoration (roof + corner caps) on the room
        // root; null on undressed rooms, where DressCrown is a no-op.
        private RoomDecorSocket crown;

        private bool wallsInitialized;

        public Vector3Int Index { get; set; }
        public int SpawnWeight => Mathf.Max(0, spawnWeight);

        /// <summary>True when this room is an impassable pit; the generator routes around it.</summary>
        public bool IsPit { get; private set; }

        /// <summary>True when this room is a sealed solid block (a building the player cannot enter); the generator routes around it like a pit, but it stays a walled mass instead of a hole.</summary>
        public bool IsSolidBlock { get; private set; }

        /// <summary>How the generator classified this cell (street, indoors, solid building, pit). <see cref="SpaceType.None"/> until the generator's classification pass assigns it. Query metadata only - it never drives walls or reachability.</summary>
        public SpaceType Space { get; private set; } = SpaceType.None;

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
            // conjoin pass can drop the one between two neighbouring pits. Prefer
            // explicit NShaft/SShaft/EShaft/WShaft children; for a kit-built void
            // whose retaining walls are named generically ("Wall (3)"...), fall back
            // to matching each wall to this room's own wall for that direction by
            // rotation (same model + per-direction rotation, so the match is exact).
            RegisterShaft(voidInstance.transform, "NShaft", Directions.NORTH);
            RegisterShaft(voidInstance.transform, "SShaft", Directions.SOUTH);
            RegisterShaft(voidInstance.transform, "EShaft", Directions.EAST);
            RegisterShaft(voidInstance.transform, "WShaft", Directions.WEST);

            // Kit-built void: retaining walls grouped under directional parents
            // (NorthWall/SouthWall/EastWall/WestWall), like the room cells. Hiding
            // the whole group drops every segment of that side's deep shaft at once.
            RegisterShaft(voidInstance.transform, "NorthWall", Directions.NORTH);
            RegisterShaft(voidInstance.transform, "SouthWall", Directions.SOUTH);
            RegisterShaft(voidInstance.transform, "EastWall", Directions.EAST);
            RegisterShaft(voidInstance.transform, "WestWall", Directions.WEST);

            AutoRegisterShaftsByRotation(voidInstance.transform);
        }

        private void RegisterShaft(Transform voidRoot, string childName, Directions dir)
        {
            Transform shaft = voidRoot.Find(childName);
            if (shaft != null)
            {
                shafts[dir] = shaft.gameObject;
            }
        }

        private static readonly Directions[] Cardinals =
        {
            Directions.NORTH, Directions.SOUTH, Directions.EAST, Directions.WEST
        };

        // Maps each of the void's retaining walls to the maze direction it seals, by
        // matching its rotation to this room's own wall for that direction. Only
        // fills directions an explicit NShaft/etc. did not already claim, and only
        // considers children named "Wall" so corner pillars and fog are left alone.
        // A direction with no close match keeps its wall (graceful no-op).
        private void AutoRegisterShaftsByRotation(Transform voidRoot)
        {
            HashSet<GameObject> used = new HashSet<GameObject>();

            foreach (Directions dir in Cardinals)
            {
                if (shafts.TryGetValue(dir, out GameObject existing) && existing != null)
                {
                    used.Add(existing); // an explicit named shaft already owns this side
                    continue;
                }

                if (!TryGetWallReferenceRotation(dir, out Quaternion reference))
                {
                    continue;
                }

                GameObject best = null;
                float bestAngle = 45f; // accept only a clear same-facing match
                foreach (Transform child in voidRoot)
                {
                    GameObject candidate = child.gameObject;
                    if (used.Contains(candidate) || !candidate.name.Contains("Wall"))
                    {
                        continue; // corners, body, fog, or an already-claimed wall
                    }

                    float angle = Quaternion.Angle(child.rotation, reference);
                    if (angle < bestAngle)
                    {
                        bestAngle = angle;
                        best = candidate;
                    }
                }

                if (best != null)
                {
                    shafts[dir] = best;
                    used.Add(best);
                }
            }
        }

        // This room's wall for a direction is an identity-rotation container whose
        // child model carries the per-direction rotation; return that model's world
        // rotation as the reference to match void walls against.
        private bool TryGetWallReferenceRotation(Directions dir, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!walls.TryGetValue(dir, out GameObject wall) || wall == null)
            {
                return false;
            }

            MeshFilter model = wall.GetComponentInChildren<MeshFilter>(true);
            if (model == null)
            {
                return false;
            }

            rotation = model.transform.rotation;
            return true;
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

            // Cache the (optional) dressing socket per wall once. GetComponent is
            // null on rooms without the lost-city kit, so DressWall stays legacy.
            sockets.Clear();
            sockets[Directions.NORTH] = NorthWall != null ? NorthWall.GetComponent<WallSocket>() : null;
            sockets[Directions.SOUTH] = SouthWall != null ? SouthWall.GetComponent<WallSocket>() : null;
            sockets[Directions.EAST] = EastWall != null ? EastWall.GetComponent<WallSocket>() : null;
            sockets[Directions.WEST] = WestWall != null ? WestWall.GetComponent<WallSocket>() : null;

            crown = GetComponent<RoomDecorSocket>();

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
        /// <summary>
        /// Flags this already-placed room as a sealed solid block. The carve never
        /// opens a non-walkable cell, so its walls are already closed on every side;
        /// this just records the state so enemies/secrets never spawn trapped inside
        /// and the dressing pass can face the block's facade outward.
        /// </summary>
        public void MarkSolidBlock()
        {
            IsSolidBlock = true;
        }

        /// <summary>
        /// Records the generator's final space classification for this cell so
        /// gameplay systems can query it off a room reference. Set once per
        /// generation by the classification pass from the finalized masks; pure
        /// metadata that never touches walls, floors, or reachability.
        /// </summary>
        public void SetSpaceType(SpaceType space)
        {
            Space = space;
        }

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

        /// <summary>
        /// Final cosmetic pass for one wall after carving. Delegates to a
        /// <see cref="WallSocket"/> when the wall has one (the lost-city kit);
        /// otherwise falls back to the legacy show-when-closed toggle so undressed
        /// rooms (the hub) render exactly as before.
        ///
        /// <para><paramref name="owner"/> is false on the non-owning side of a
        /// shared edge. That side always hides its OPEN passage frame (so an arch
        /// is built once), and - when <paramref name="deDoubleClosed"/> is set -
        /// hides its CLOSED wall too, so each boundary carries a single wall the
        /// player looks THROUGH into the neighbouring space (fixing windows that
        /// used to stare into a back-to-back wall) and the grid stops drawing every
        /// interior wall twice.</para>
        ///
        /// <para><paramref name="roomInterior"/> marks an open edge between two
        /// cells of the same open room; both sides hide entirely so the room reads
        /// as one continuous space instead of a grid of archways.</para>
        /// </summary>
        public void DressWall(Directions dir, bool open, bool outer, bool owner, bool roomInterior, bool deDoubleClosed, System.Random rng)
        {
            if (dir == Directions.NONE)
            {
                return;
            }

            InitializeWalls();

            if (!walls.TryGetValue(dir, out GameObject wall) || wall == null)
            {
                return;
            }

            bool hasSocket = sockets.TryGetValue(dir, out WallSocket socket) && socket != null;

            // Inside an open room (or the interior of a stacked building floor):
            // no wall, no frame, on both sides.
            if (open && roomInterior)
            {
                wall.SetActive(false);
                return;
            }

            // Non-owner side of a shared edge: hide so the owner renders it once.
            // Open edges always de-double (a single archway); closed edges only
            // when de-doubling is on (a single wall to see through).
            if (!owner && (open || deDoubleClosed))
            {
                wall.SetActive(false);
                return;
            }

            if (hasSocket)
            {
                wall.SetActive(true);
                socket.ApplyStyle(dir, open, outer, rng);
                return;
            }

            // No socket: legacy behaviour - a closed wall is visible, an open gap hides.
            wall.SetActive(!open);
        }

        /// <summary>
        /// Final cosmetic pass for this cell's crown (flat/pitched roof + corner
        /// caps). Delegates to a <see cref="RoomDecorSocket"/> when the room has
        /// one; no-op otherwise. Called for every dressed cell each generation -
        /// including with everything off - so reused rooms never keep a stale
        /// roof or cap from the previous layout.
        /// </summary>
        public void DressCrown(bool flatRoof, int roofYawSteps, int gableRolesPacked, int capMask, System.Random rng)
        {
            InitializeWalls();

            if (crown != null)
            {
                crown.ApplyCrown(flatRoof, roofYawSteps, gableRolesPacked, capMask, rng);
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
