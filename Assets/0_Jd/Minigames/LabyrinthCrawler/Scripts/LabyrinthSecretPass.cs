using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sol.Minigames
{
    /// <summary>
    /// Labyrinth-only post-generation pass: after ArcadeGen3D carves a stage,
    /// places <see cref="IllusoryWall"/> plugs over secret-room doors, useful
    /// closed-wall shortcuts, and rare blockers on the main route to the exit.
    /// Runs from LabyrinthCrawlerGame.OnMazeReady - never from the shared
    /// generator - so the arcade hub's maze is untouched by design.
    /// </summary>
    [Serializable]
    public class LabyrinthSecretPass
    {
        #region Configuration

        [Tooltip("Illusory wall plug spawned over selected room boundaries. Leave empty to disable secrets.")]
        [SerializeField] private IllusoryWall illusoryWallPrefab;

        [Header("Frequency")]
        [Tooltip("Chance each wall slot actually spawns.")]
        [SerializeField, Range(0f, 1f)] private float secretChance = 1f;

        [Tooltip("Base illusory walls per stage before stage-scaling bonuses.")]
        [SerializeField, Min(0)] private int maxSecretsPerStage = 1;

        [Tooltip("Hard ceiling after stage bonuses. Zero means no ceiling.")]
        [SerializeField, Min(0)] private int illusionHardCap = 3;

        [Tooltip("Adds one more illusory wall every N stages. With 3, stages 3/6/9 add +1/+2/+3.")]
        [SerializeField, Min(0)] private int bonusWallEveryStages = 3;

        [Tooltip("Minimum Manhattan cell distance between illusion sites. Prevents several secrets clustering around one room.")]
        [SerializeField, Min(0)] private int minimumIllusionSpacingCells = 2;

        [Header("Site Mix")]
        [Tooltip("Chance that a non-blocker slot becomes a shortcut instead of a dead-end room plug.")]
        [SerializeField, Range(0f, 1f)] private float shortcutChance = 0.35f;

        [Tooltip("Rare stage-level chance to place one illusory wall on the carved path to the exit.")]
        [SerializeField, Range(0f, 1f)] private float exitPathBlockerChance = 0.12f;

        [Tooltip("Minimum carved-route distance between two adjacent rooms before a closed wall is worth making into a shortcut.")]
        [SerializeField, Min(2)] private int minShortcutPathDistance = 4;

        [Tooltip("How many start/end path edges to avoid when placing the rare exit blocker.")]
        [SerializeField, Min(0)] private int exitPathBlockerEndpointBuffer = 1;

        [Tooltip("Plug center height above the room root (dungeon wall slabs are 6 tall standing on the floor).")]
        [SerializeField] private float plugHeight = 3f;

        [Header("Treasure Rooms")]
        [Tooltip("Hard cap on dead-end treasure rooms per stage. Illusory walls beyond this become shortcuts, so rewards stay rare while shortcuts keep scaling.")]
        [SerializeField, Min(0)] private int treasureRoomsPerStage = 1;

        [Tooltip("Reward cache placed inside a secret room. Leave empty for score-only secret rooms.")]
        [SerializeField] private LabyrinthSecretCache secretCachePrefab;

        [Tooltip("Cache spawn height above the room root.")]
        [SerializeField] private float cacheHeight = 0.6f;

        #endregion

        #region Runtime State and Domain Model

        private readonly List<IllusoryWall> spawned = new List<IllusoryWall>();
        private readonly List<LabyrinthSecretCache> spawnedCaches = new List<LabyrinthSecretCache>();
        private Action<LabyrinthSecretCache> cacheCollectedCallback;
        private int trackableSecretCount;

        private static readonly Room3D.Directions[] CardinalDirections =
        {
            Room3D.Directions.NORTH,
            Room3D.Directions.SOUTH,
            Room3D.Directions.EAST,
            Room3D.Directions.WEST
        };

        /// <summary>
        /// What a given illusory wall is hiding. The game scores these very
        /// differently: a treasure room is a real find, a shortcut pays for
        /// itself in saved time, and a blocker is an obstacle, not a prize.
        /// </summary>
        public enum SecretSiteKind
        {
            Room,
            Shortcut,
            ExitPathBlocker
        }

        private readonly struct SecretSite
        {
            public SecretSite(Room3D from, Room3D to, Room3D.Directions direction, SecretSiteKind kind, bool opensBoundary)
            {
                From = from;
                To = to;
                Direction = direction;
                Kind = kind;
                OpensBoundary = opensBoundary;
            }

            public Room3D From { get; }
            public Room3D To { get; }
            public Room3D.Directions Direction { get; }
            public SecretSiteKind Kind { get; }
            public bool OpensBoundary { get; }
        }

        /// <summary>
        /// Allocation-free, direction-independent identity for one shared room
        /// boundary. This replaces the former formatted-string edge keys.
        /// </summary>
        private readonly struct SecretEdge : IEquatable<SecretEdge>
        {
            public SecretEdge(Room3D a, Room3D b)
            {
                Vector2Int first = ToIndex(a);
                Vector2Int second = ToIndex(b);
                if (first.x > second.x || (first.x == second.x && first.y > second.y))
                {
                    (first, second) = (second, first);
                }

                First = first;
                Second = second;
            }

            private Vector2Int First { get; }
            private Vector2Int Second { get; }

            public bool Equals(SecretEdge other) => First == other.First && Second == other.Second;
            public override bool Equals(object obj) => obj is SecretEdge other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(First, Second);
        }

        public IReadOnlyList<IllusoryWall> Spawned => spawned;
        public int TrackableSecretCount => trackableSecretCount;

        #endregion

        #region Stage Lifecycle

        /// <summary>Destroys the previous stage's plugs and caches (call when the maze rebuilds).</summary>
        public void Clear()
        {
            foreach (IllusoryWall wall in spawned)
            {
                if (wall != null)
                {
                    UnityEngine.Object.Destroy(wall.gameObject);
                }
            }

            spawned.Clear();
            trackableSecretCount = 0;

            foreach (LabyrinthSecretCache cache in spawnedCaches)
            {
                if (cache != null)
                {
                    UnityEngine.Object.Destroy(cache.gameObject);
                }
            }

            spawnedCaches.Clear();
        }

        /// <summary>
        /// Places illusory walls for the current stage. Call after generation
        /// completes; <paramref name="onRevealed"/> fires per wall the player
        /// walks through.
        /// </summary>
        public void SpawnSecrets(
            ArcadeGen3D generator,
            Transform parent,
            int stage,
            Action<IllusoryWall, SecretSiteKind> onRevealed,
            Action<LabyrinthSecretCache> onCacheCollected = null)
        {
            Clear();
            cacheCollectedCallback = onCacheCollected;

            int targetWallCount = GetTargetWallCount(stage);
            if (illusoryWallPrefab == null || generator == null || targetWallCount <= 0)
            {
                return;
            }

            List<SecretSite> roomSites = CollectDeadEnds(generator);
            List<SecretSite> shortcutSites = CollectShortcuts(generator, minShortcutPathDistance);
            List<SecretSite> blockerSites = CollectExitPathBlockers(generator, exitPathBlockerEndpointBuffer);
            LabyrinthRuntimeUtility.Shuffle(roomSites);
            LabyrinthRuntimeUtility.Shuffle(shortcutSites);
            LabyrinthRuntimeUtility.Shuffle(blockerSites);

            HashSet<SecretEdge> usedEdges = new HashSet<SecretEdge>();
            List<Vector2Int> occupiedSiteRooms =
                new List<Vector2Int>();

            int placed = 0;

            if (Random.value <= exitPathBlockerChance &&
                TryPlaceNext(
                    blockerSites,
                    parent,
                    onRevealed,
                    usedEdges,
                    occupiedSiteRooms))
            {
                placed++;
            }

            int roomsPlaced = 0;

            while (placed < targetWallCount)
            {
                if (Random.value > secretChance)
                {
                    placed++;
                    continue;
                }

                // Treasure rooms are capped so a searching player meets roughly
                // one reward per stage; every extra wall this stage falls back
                // to a shortcut, which pays for itself in saved time.
                bool roomsAvailable = roomsPlaced < treasureRoomsPerStage;
                bool preferShortcut = !roomsAvailable || Random.value <= shortcutChance;
                bool didPlace = false;

                if (preferShortcut)
                {
                    didPlace = TryPlaceNext(
                        shortcutSites,
                        parent,
                        onRevealed,
                        usedEdges,
                        occupiedSiteRooms);
                    if (!didPlace && roomsAvailable &&
                        TryPlaceNext(
                            roomSites,
                            parent,
                            onRevealed,
                            usedEdges,
                            occupiedSiteRooms))
                    {
                        didPlace = true;
                        roomsPlaced++;
                    }
                }
                else
                {
                    didPlace = TryPlaceNext(
                        roomSites,
                        parent,
                        onRevealed,
                        usedEdges,
                        occupiedSiteRooms);
                    if (didPlace)
                    {
                        roomsPlaced++;
                    }
                    else
                    {
                        didPlace = TryPlaceNext(
                            shortcutSites,
                            parent,
                            onRevealed,
                            usedEdges,
                            occupiedSiteRooms);
                    }
                }

                if (!didPlace)
                {
                    break;
                }

                placed++;
            }
        }

        #endregion

        #region Candidate Discovery

        private int GetTargetWallCount(int stage)
        {
            int bonus = bonusWallEveryStages > 0 ? Mathf.Max(0, stage) / bonusWallEveryStages : 0;
            int target = Mathf.Max(0, maxSecretsPerStage) + bonus;
            return illusionHardCap > 0
                ? Mathf.Min(target, illusionHardCap)
                : target;
        }

        /// <summary>
        /// Dead ends are spanning-tree leaves: exactly one open side with an
        /// in-grid neighbor. Nothing routes THROUGH a leaf, so hiding its
        /// doorway can never cut off the start-to-exit path. The start room
        /// (player spawn) and end room (exit pad) are excluded.
        /// </summary>
        private static List<SecretSite> CollectDeadEnds(ArcadeGen3D generator)
        {
            List<SecretSite> candidates = new List<SecretSite>();
            Room3D[,] rooms = generator.Rooms;
            if (rooms == null)
            {
                return candidates;
            }

            int numX = rooms.GetLength(0);
            int numZ = rooms.GetLength(1);
            Vector2Int start = generator.StartRoomIndex;
            Vector2Int end = generator.EndRoomIndex;

            for (int x = 0; x < numX; x++)
            {
                for (int z = 0; z < numZ; z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null ||
                        room.IsPit ||
                        (x == start.x && z == start.y) ||
                        (x == end.x && z == end.y))
                    {
                        continue;
                    }

                    int openSides = 0;
                    Room3D doorNeighbor = null;
                    Room3D.Directions doorDirection = Room3D.Directions.NONE;

                    if (!room.IsWallClosed(Room3D.Directions.NORTH))
                    {
                        openSides++;
                        if (TryGetNeighbor(rooms, x, z, Room3D.Directions.NORTH, out Room3D northNeighbor))
                        {
                            doorNeighbor = northNeighbor;
                            doorDirection = Room3D.Directions.NORTH;
                        }
                    }

                    if (!room.IsWallClosed(Room3D.Directions.SOUTH))
                    {
                        openSides++;
                        if (TryGetNeighbor(rooms, x, z, Room3D.Directions.SOUTH, out Room3D southNeighbor))
                        {
                            doorNeighbor = southNeighbor;
                            doorDirection = Room3D.Directions.SOUTH;
                        }
                    }

                    if (!room.IsWallClosed(Room3D.Directions.EAST))
                    {
                        openSides++;
                        if (TryGetNeighbor(rooms, x, z, Room3D.Directions.EAST, out Room3D eastNeighbor))
                        {
                            doorNeighbor = eastNeighbor;
                            doorDirection = Room3D.Directions.EAST;
                        }
                    }

                    if (!room.IsWallClosed(Room3D.Directions.WEST))
                    {
                        openSides++;
                        if (TryGetNeighbor(rooms, x, z, Room3D.Directions.WEST, out Room3D westNeighbor))
                        {
                            doorNeighbor = westNeighbor;
                            doorDirection = Room3D.Directions.WEST;
                        }
                    }

                    // A leaf whose sole opening pierces the outer wall (rules
                    // can open start/end outer walls) has no neighbor; skip it.
                    if (openSides == 1 && doorNeighbor != null)
                    {
                        candidates.Add(new SecretSite(room, doorNeighbor, doorDirection, SecretSiteKind.Room, false));
                    }
                }
            }

            return candidates;
        }

        private static List<SecretSite> CollectShortcuts(ArcadeGen3D generator, int minGraphDistance)
        {
            List<SecretSite> candidates = new List<SecretSite>();
            Room3D[,] rooms = generator.Rooms;
            if (rooms == null)
            {
                return candidates;
            }

            int numX = rooms.GetLength(0);
            int numZ = rooms.GetLength(1);
            Vector2Int start = generator.StartRoomIndex;
            Vector2Int end = generator.EndRoomIndex;

            for (int x = 0; x < numX; x++)
            {
                for (int z = 0; z < numZ; z++)
                {
                    Room3D room = rooms[x, z];
                    if (room == null || room.IsPit || room.IsSolidBlock)
                    {
                        continue;
                    }

                    AddShortcutCandidate(generator, candidates, room, x, z, Room3D.Directions.NORTH, start, end, minGraphDistance);
                    AddShortcutCandidate(generator, candidates, room, x, z, Room3D.Directions.EAST, start, end, minGraphDistance);
                }
            }

            return candidates;
        }

        private static void AddShortcutCandidate(
            ArcadeGen3D generator,
            List<SecretSite> candidates,
            Room3D room,
            int x,
            int z,
            Room3D.Directions direction,
            Vector2Int start,
            Vector2Int end,
            int minGraphDistance)
        {
            Room3D[,] rooms = generator.Rooms;
            if (!TryGetNeighbor(rooms, x, z, direction, out Room3D neighbor) ||
                neighbor == null)
            {
                return;
            }

            Vector2Int a = ToIndex(room);
            Vector2Int b = ToIndex(neighbor);
            if (a == start || a == end || b == start || b == end)
            {
                return;
            }

            Room3D.Directions opposite = Opposite(direction);
            if (!room.IsWallClosed(direction) || !neighbor.IsWallClosed(opposite))
            {
                return;
            }

            int graphDistance = GetOpenGraphDistance(generator, a, b);
            if (graphDistance >= minGraphDistance)
            {
                candidates.Add(new SecretSite(room, neighbor, direction, SecretSiteKind.Shortcut, true));
            }
        }

        private static List<SecretSite> CollectExitPathBlockers(ArcadeGen3D generator, int endpointBuffer)
        {
            List<SecretSite> candidates = new List<SecretSite>();
            if (!TryFindOpenPath(generator, generator.StartRoomIndex, generator.EndRoomIndex, out List<Vector2Int> path) ||
                path.Count < 2)
            {
                return candidates;
            }

            Room3D[,] rooms = generator.Rooms;
            int firstEdge = Mathf.Clamp(endpointBuffer, 0, path.Count - 2);
            int lastEdge = Mathf.Clamp(path.Count - 2 - endpointBuffer, 0, path.Count - 2);
            if (firstEdge > lastEdge)
            {
                firstEdge = 0;
                lastEdge = path.Count - 2;
            }

            for (int i = firstEdge; i <= lastEdge; i++)
            {
                Vector2Int fromIndex = path[i];
                Vector2Int toIndex = path[i + 1];
                Room3D from = rooms[fromIndex.x, fromIndex.y];
                Room3D to = rooms[toIndex.x, toIndex.y];
                Room3D.Directions direction = DirectionBetween(fromIndex, toIndex);
                if (from != null && to != null && direction != Room3D.Directions.NONE)
                {
                    candidates.Add(new SecretSite(from, to, direction, SecretSiteKind.ExitPathBlocker, false));
                }
            }

            return candidates;
        }

        #endregion

        #region Placement and Disguises

        private bool TryPlaceNext(
            List<SecretSite> sites,
            Transform parent,
            Action<IllusoryWall, SecretSiteKind> onRevealed,
            HashSet<SecretEdge> usedEdges,
            List<Vector2Int> occupiedSiteRooms)
        {
            for (int i = sites.Count - 1; i >= 0; i--)
            {
                SecretSite site = sites[i];
                sites.RemoveAt(i);

                if (!CanUseSite(
                        site,
                        usedEdges,
                        occupiedSiteRooms))
                {
                    continue;
                }

                if (site.OpensBoundary)
                {
                    OpenBoundary(site);
                }

                PlacePlug(site, parent, onRevealed);
                if (site.Kind != SecretSiteKind.ExitPathBlocker)
                {
                    trackableSecretCount++;
                }

                usedEdges.Add(new SecretEdge(site.From, site.To));
                occupiedSiteRooms.Add(ToIndex(site.From));
                occupiedSiteRooms.Add(ToIndex(site.To));
                return true;
            }

            return false;
        }

        private bool CanUseSite(
            SecretSite site,
            HashSet<SecretEdge> usedEdges,
            List<Vector2Int> occupiedSiteRooms)
        {
            if (site.From == null || site.To == null || site.Direction == Room3D.Directions.NONE)
            {
                return false;
            }

            SecretEdge edgeKey = new SecretEdge(site.From, site.To);
            if (usedEdges.Contains(edgeKey))
            {
                return false;
            }

            if (minimumIllusionSpacingCells > 0)
            {
                Vector2Int from = ToIndex(site.From);
                Vector2Int to = ToIndex(site.To);
                foreach (Vector2Int occupied in occupiedSiteRooms)
                {
                    int fromDistance =
                        Mathf.Abs(from.x - occupied.x)
                        + Mathf.Abs(from.y - occupied.y);
                    int toDistance =
                        Mathf.Abs(to.x - occupied.x)
                        + Mathf.Abs(to.y - occupied.y);
                    if (fromDistance < minimumIllusionSpacingCells
                        || toDistance < minimumIllusionSpacingCells)
                    {
                        return false;
                    }
                }
            }

            if (site.Kind == SecretSiteKind.Shortcut)
            {
                return site.From.IsWallClosed(site.Direction) &&
                       site.To.IsWallClosed(Opposite(site.Direction));
            }

            return !site.From.IsWallClosed(site.Direction) &&
                   !site.To.IsWallClosed(Opposite(site.Direction));
        }

        private static void OpenBoundary(SecretSite site)
        {
            site.From.SetDirFlag(site.Direction, false);
            site.To.SetDirFlag(Opposite(site.Direction), false);
        }

        private void PlacePlug(SecretSite site, Transform parent, Action<IllusoryWall, SecretSiteKind> onRevealed)
        {
            Vector3 leafCenter = site.From.transform.position;
            Vector3 neighborCenter = site.To.transform.position;

            // Doorway cavity center sits midway between the two room roots;
            // the plug's local Z (thin axis) points through the doorway.
            Vector3 position = (leafCenter + neighborCenter) * 0.5f;
            position.y = leafCenter.y + plugHeight;

            Vector3 through = neighborCenter - leafCenter;
            through.y = 0f;
            Quaternion rotation = through.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(through.normalized, Vector3.up)
                : Quaternion.identity;

            IllusoryWall wall = UnityEngine.Object.Instantiate(illusoryWallPrefab, position, rotation, parent);
            wall.name = $"IllusoryWall_{site.Kind}_{site.From.Index.x}_{site.From.Index.z}";
            if (TryResolveDisguise(
                    site,
                    out GameObject disguisePrefab,
                    out Vector3 disguisePosition,
                    out Quaternion disguiseRotation))
            {
                wall.ConfigureDisguise(
                    disguisePrefab,
                    disguisePosition,
                    disguiseRotation);
            }

            spawned.Add(wall);

            if (onRevealed != null)
            {
                SecretSiteKind kind = site.Kind;
                wall.OnRevealed.AddListener(() => onRevealed(wall, kind));
            }

            // Only a dead-end room has space worth filling; shortcuts and
            // blockers are passages, not prizes.
            if (site.Kind == SecretSiteKind.Room)
            {
                PlaceCache(site.From, parent);
            }
        }

        private static bool TryResolveDisguise(
            SecretSite site,
            out GameObject prefab,
            out Vector3 position,
            out Quaternion rotation)
        {
            if (TryResolveDisguiseFromRoom(
                    site.From,
                    site.Direction,
                    out prefab,
                    out position,
                    out rotation))
            {
                return true;
            }

            return TryResolveDisguiseFromRoom(
                site.To,
                Opposite(site.Direction),
                out prefab,
                out position,
                out rotation);
        }

        private static bool TryResolveDisguiseFromRoom(
            Room3D room,
            Room3D.Directions direction,
            out GameObject prefab,
            out Vector3 position,
            out Quaternion rotation)
        {
            prefab = null;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (room == null
                || !room.TryGetWallTransform(
                    direction,
                    out Transform wallTransform))
            {
                return false;
            }

            WallSocket socket =
                wallTransform.GetComponent<WallSocket>();
            if (socket == null)
            {
                return false;
            }

            System.Random rng = new System.Random(
                Random.Range(-1000000000, 1000000000));
            return socket.TryGetIllusoryDisguise(
                direction,
                rng,
                out prefab,
                out position,
                out rotation);
        }

        private void PlaceCache(Room3D room, Transform parent)
        {
            if (secretCachePrefab == null || room == null)
            {
                return;
            }

            Vector3 position = room.transform.position + Vector3.up * cacheHeight;
            LabyrinthSecretCache cache = UnityEngine.Object.Instantiate(
                secretCachePrefab, position, Quaternion.identity, parent);
            cache.Initialize(cacheCollectedCallback);
            spawnedCaches.Add(cache);
        }

        #endregion

        #region Grid Graph Helpers

        private static bool TryGetNeighbor(
            Room3D[,] rooms,
            int x,
            int z,
            Room3D.Directions direction,
            out Room3D neighbor,
            bool allowBuildingInterior = false)
        {
            neighbor = null;
            switch (direction)
            {
                case Room3D.Directions.NORTH:
                    z++;
                    break;

                case Room3D.Directions.SOUTH:
                    z--;
                    break;

                case Room3D.Directions.EAST:
                    x++;
                    break;

                case Room3D.Directions.WEST:
                    x--;
                    break;

                default:
                    return false;
            }

            if (x < 0 || z < 0 || x >= rooms.GetLength(0) || z >= rooms.GetLength(1))
            {
                return false;
            }

            neighbor = rooms[x, z];
            // Site discovery rejects both pits and building mass by default, so a
            // shortcut never tunnels into a facade. Open-graph pathfinding opts
            // into procedural interiors because those ground-floor halls are
            // traversable and may contain the exit.
            return neighbor != null &&
                   !neighbor.IsPit &&
                   (allowBuildingInterior || !neighbor.IsSolidBlock);
        }

        private static int GetOpenGraphDistance(ArcadeGen3D generator, Vector2Int start, Vector2Int end)
        {
            return TryFindOpenPath(generator, start, end, out List<Vector2Int> path) ? path.Count - 1 : -1;
        }

        private static bool TryFindOpenPath(ArcadeGen3D generator, Vector2Int start, Vector2Int end, out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();
            Room3D[,] rooms = generator.Rooms;
            if (rooms == null)
            {
                return false;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> previous = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(start);
            previous[start] = start;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (current == end)
                {
                    BuildPath(previous, start, end, path);
                    return true;
                }

                foreach (Vector2Int next in GetOpenNeighborIndices(rooms, current))
                {
                    if (previous.ContainsKey(next))
                    {
                        continue;
                    }

                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static IEnumerable<Vector2Int> GetOpenNeighborIndices(Room3D[,] rooms, Vector2Int index)
        {
            Room3D room = rooms[index.x, index.y];
            if (room == null)
            {
                yield break;
            }

            foreach (Room3D.Directions direction in CardinalDirections)
            {
                if (room.IsWallClosed(direction) ||
                    !TryGetNeighbor(
                        rooms,
                        index.x,
                        index.y,
                        direction,
                        out Room3D neighbor,
                        allowBuildingInterior: true))
                {
                    continue;
                }

                Vector2Int next = ToIndex(neighbor);
                if (!neighbor.IsWallClosed(Opposite(direction)))
                {
                    yield return next;
                }
            }
        }

        private static void BuildPath(
            Dictionary<Vector2Int, Vector2Int> previous,
            Vector2Int start,
            Vector2Int end,
            List<Vector2Int> path)
        {
            path.Clear();
            Vector2Int current = end;
            path.Add(current);

            while (current != start)
            {
                current = previous[current];
                path.Add(current);
            }

            path.Reverse();
        }

        private static Room3D.Directions DirectionBetween(Vector2Int from, Vector2Int to)
        {
            Vector2Int delta = to - from;
            if (delta == Vector2Int.up)
            {
                return Room3D.Directions.NORTH;
            }

            if (delta == Vector2Int.down)
            {
                return Room3D.Directions.SOUTH;
            }

            if (delta == Vector2Int.right)
            {
                return Room3D.Directions.EAST;
            }

            if (delta == Vector2Int.left)
            {
                return Room3D.Directions.WEST;
            }

            return Room3D.Directions.NONE;
        }

        private static Room3D.Directions Opposite(Room3D.Directions direction)
        {
            return direction switch
            {
                Room3D.Directions.NORTH => Room3D.Directions.SOUTH,
                Room3D.Directions.SOUTH => Room3D.Directions.NORTH,
                Room3D.Directions.EAST => Room3D.Directions.WEST,
                Room3D.Directions.WEST => Room3D.Directions.EAST,
                _ => Room3D.Directions.NONE
            };
        }

        private static Vector2Int ToIndex(Room3D room)
        {
            Vector3Int index = room.Index;
            return new Vector2Int(index.x, index.z);
        }

        #endregion

    }
}
