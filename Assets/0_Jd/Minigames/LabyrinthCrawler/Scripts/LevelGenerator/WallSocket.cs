using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Cosmetic dressing for a single maze-cell edge. The generator decides only
    /// OPEN vs CLOSED; this socket turns that decision into an authored Blender
    /// part - an archway/doorway for a passage, a plain/window/arrowslit wall for
    /// a solid edge.
    ///
    /// The same socket dresses both the ground maze and the stacked upper-floor
    /// cells of the lost-city buildings: an upper floor is just another row of
    /// rooms whose edges read OPEN (interior of a building, hidden) or CLOSED
    /// (a windowed facade onto the street), so no second-storey-specific code is
    /// needed here. Roofs and gables that cap a building live on the per-cell
    /// <see cref="RoomDecorSocket"/> instead.
    ///
    /// Selection takes a caller-supplied <see cref="System.Random"/> so wall
    /// variety never draws from UnityEngine.Random and can never perturb the maze
    /// carve RNG stream (ArcadeGen3D stays byte-identical on the hub). With every
    /// list left empty the socket reproduces the legacy look exactly: a solid edge
    /// shows the default child, an open edge hides the whole wall into a bare gap.
    ///
    /// Add one to each of a room's four wall objects (the NWall/SWall/EWall/WWall
    /// parents, not their mesh child) and point <see cref="defaultSolid"/> at that
    /// wall's original mesh child.
    /// </summary>
    [DisallowMultipleComponent]
    public class WallSocket : MonoBehaviour
    {
        private const string SpawnedNamePrefix = "__WallDress__";

        public enum AuthoredWallType
        {
            Solid,
            Entrance,
            InteriorOpening,
        }

        public enum PierFamily
        {
            Center,
            Left,
            Right,
            Double,
        }

        [Tooltip("The wall's original mesh child (the grey cube). Hidden the moment a variant is chosen; shown as the fallback solid when no Solid Variants are assigned. Leave empty only if this wall has no default geometry.")]
        [SerializeField] private GameObject defaultSolid;

        [Tooltip("Solid, non-passable wall parts (plain / window / arrowslit and variations). One is picked at random for a CLOSED edge. Empty = fall back to Default Solid.")]
        [SerializeField] private List<GameObject> solidVariants = new List<GameObject>();

        [Tooltip("Passable frames (single/double doorway or archway). One is picked at random for an OPEN edge. Empty = the opening is a plain gap, as before the kit.")]
        [SerializeField] private List<GameObject> passageVariants = new List<GameObject>();

        [Header("Placement")]
        [Tooltip("Kit parts are authored with their origin at the CELL CENTRE (geometry baked to the correct edge), so a spawned variant simply reuses the Default Solid's local position and rotation - it lands exactly where the wall it replaces sits, oriented the same way. No offset or yaw math needed.")]
        [SerializeField] private float partYOffset;

        [Header("Authored Building")]
        [Tooltip("Mark this wall as a doorway on a hand-authored building prefab. When the generator drops the building into a level it scans the perimeter for sockets flagged here and opens the adjacent street so the player can walk in. Ignored on ordinary generated maze cells (the carve decides open/closed there).")]
        [SerializeField] private bool authoredOpening;

        /// <summary>True when this socket is an authored building's entrance (see the Authored Building header). The generator reads this off a placed building prefab's perimeter to connect its doorways to the street; it plays no part in ordinary maze dressing.</summary>
        public bool AuthoredOpening => authoredOpening;

        [Tooltip("Building-authoring state for a shared edge between two cells. Hidden on both sides so the cells form one continuous interior. Use the Building Component Inspector to set this.")]
        [SerializeField, HideInInspector] private bool authoredInteriorOpening;

        /// <summary>The hand-authored role used when a BuildingComponent dresses this socket.</summary>
        public AuthoredWallType AuthoredType =>
            authoredInteriorOpening
                ? AuthoredWallType.InteriorOpening
                : authoredOpening
                    ? AuthoredWallType.Entrance
                    : AuthoredWallType.Solid;

        [Header("Pier Arch Pairing")]
        [Tooltip("Optional arches used to cap a vertical entrance stack. The arch is placed in the top socket above its matching pier family, never overlaid on the pier. When empty, matching arches are found among Passage Variants.")]
        [SerializeField] private List<GameObject> streetArchVariants = new List<GameObject>();

        [Tooltip("A passage variant counts as a stacker pier when its name contains this text, case-insensitive.")]
        [SerializeField] private string pierNameToken = "pier";

        [Tooltip("Matching top pieces must contain this text and share the pier's Center, Left, Right, or Double family.")]
        [SerializeField] private string archNameToken = "arch";

        // Instances spawned by the last ApplyStyle, cleared before the next so a
        // regenerate (or an edit-time rebuild) never leaves doubled geometry.
        // Serialized so a style baked into an authored building prefab can still
        // be replaced cleanly after a domain reload or when the prefab is spawned
        // and dressed again at runtime.
        [SerializeField, HideInInspector] private GameObject spawnedVariant;
        [SerializeField, HideInInspector] private GameObject spawnedArch;
        [SerializeField, HideInInspector] private bool hasCachedPlacement;
        [SerializeField, HideInInspector] private Vector3 cachedLocalPosition;
        [SerializeField, HideInInspector] private Quaternion cachedLocalRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private List<GameObject> closedDecor = new List<GameObject>();

        /// <summary>
        /// Configures a socket created at runtime for the Arcade hub's minimal
        /// solid-or-passage treatment. Existing authored crawler sockets never
        /// call this path and retain their complete variant configuration.
        /// </summary>
        public void ConfigureMinimal(
            GameObject solid,
            IReadOnlyList<GameObject> passages,
            IReadOnlyList<GameObject> decor)
        {
            defaultSolid = solid;
            solidVariants.Clear();
            passageVariants.Clear();
            closedDecor.Clear();

            if (passages != null)
            {
                for (int i = 0; i < passages.Count; i++)
                {
                    if (passages[i] != null)
                    {
                        passageVariants.Add(passages[i]);
                    }
                }
            }

            if (decor != null)
            {
                for (int i = 0; i < decor.Count; i++)
                {
                    if (decor[i] != null && !closedDecor.Contains(decor[i]))
                    {
                        closedDecor.Add(decor[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Renders this edge for its final carved state. Called once per generation
        /// by ArcadeGen3D's DressWalls post-carve pass. <paramref name="rng"/> is
        /// that pass's isolated System.Random - never pass UnityEngine.Random here.
        /// </summary>
        public void ApplyStyle(Room3D.Directions facing, bool open, bool outer, System.Random rng)
        {
            ApplyStyleInternal(facing, open, rng, null);
        }

        /// <summary>
        /// Dresses one level of a vertically stacked authored entrance. Lower
        /// levels use the requested pier family; the top level uses its matching
        /// arch. Pier and arch are separate storey-height kit pieces and must
        /// never be spawned at the same socket transform.
        /// </summary>
        public void ApplyStackedEntranceStyle(
            Room3D.Directions facing,
            PierFamily family,
            bool capWithArch,
            System.Random rng)
        {
            ApplyStyleInternal(facing, true, rng, family, capWithArch);
        }

        private void ApplyStyleInternal(
            Room3D.Directions facing,
            bool open,
            System.Random rng,
            PierFamily? requiredPierFamily,
            bool useFamilyArch = false)
        {
            // Capture placement before cleanup. This recovers older authored
            // prefabs whose generated-child reference was not serialized, and
            // ensures rerolling never compounds a direction rotation.
            GetTemplatePlacement(facing, out Vector3 pos, out Quaternion rot);
            ClearSpawned();

            GameObject chosen =
                open && requiredPierFamily.HasValue
                    ? useFamilyArch
                        ? FindFamilyArch(requiredPierFamily.Value)
                        : PickPier(passageVariants, requiredPierFamily.Value, rng)
                    : open
                        ? PickNonPier(passageVariants, rng)
                        : Pick(solidVariants, rng);

            SetClosedDecorVisible(!open);

            if (open && requiredPierFamily.HasValue && chosen == null)
            {
                Debug.LogWarning(
                    $"{name}: stacked entrance has no " +
                    $"{(useFamilyArch ? "arch" : "pier")} variant for " +
                    $"{requiredPierFamily.Value}.",
                    this);
            }

            // Open edge with no authored passage part: collapse to a bare gap (the
            // pre-kit behaviour). Deactivating the wall object hides the default
            // child with it.
            if (open && chosen == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // The default cube is only the closed-edge fallback; hide it as soon as
            // a real part is spawned so the two never double up.
            if (defaultSolid != null)
            {
                defaultSolid.SetActive(chosen == null && !open);
            }
            else if (chosen == null && solidVariants.Count > 0)
            {
                Debug.LogWarning($"{name}: Solid Variants are assigned but Default Solid is empty; the room's original wall mesh may show through the variant.", this);
            }

            if (chosen != null)
            {
                spawnedVariant = SpawnPart(chosen, pos + Vector3.up * partYOffset, rot);
            }
        }

        /// <summary>
        /// Sets the role of this wall inside a hand-authored building. Entrances
        /// are visible passage frames and are detected by ArcadeGen3D; interior
        /// openings are frameless gaps between two authored cells.
        /// </summary>
        public void SetAuthoredType(AuthoredWallType type)
        {
            authoredOpening = type == AuthoredWallType.Entrance;
            authoredInteriorOpening = type == AuthoredWallType.InteriorOpening;
        }

        /// <summary>Applies this socket's authored-building role.</summary>
        public void ApplyAuthoredStyle(Room3D.Directions facing, System.Random rng)
        {
            if (authoredInteriorOpening)
            {
                ClearSpawned();
                gameObject.SetActive(false);
                return;
            }

            ApplyStyle(facing, authoredOpening, true, rng);
        }

        /// <summary>
        /// Removes generated dressing and restores the socket's authored mesh.
        /// Used when a socket belongs to a nested decorative Room3D (for example
        /// a RoofCell) rather than to an authorable building cell.
        /// </summary>
        public void RestoreDefaultStyle()
        {
            ClearSpawned();
            gameObject.SetActive(true);

            if (defaultSolid != null)
            {
                defaultSolid.SetActive(true);
            }

            SetClosedDecorVisible(true);
        }

        /// <summary>
        /// Supplies an opaque full-wall model and the exact world transform this
        /// socket uses for kit pieces. Illusory walls use it to blend into the
        /// generated facade instead of approximating the opening with a cube.
        /// </summary>
        public bool TryGetIllusoryDisguise(
            Room3D.Directions facing,
            System.Random rng,
            out GameObject prefab,
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            prefab = null;
            worldPosition = transform.position;
            worldRotation = transform.rotation;

            List<GameObject> opaque = new List<GameObject>();
            foreach (GameObject candidate in solidVariants)
            {
                if (candidate == null || IsOpenFeature(candidate.name))
                {
                    continue;
                }
                opaque.Add(candidate);
            }

            List<GameObject> pool = opaque.Count > 0 ? opaque : solidVariants;
            prefab = Pick(pool, rng);
            if (prefab == null)
            {
                return false;
            }

            GetTemplatePlacement(
                facing,
                out Vector3 localPosition,
                out Quaternion localRotation);
            worldPosition = transform.TransformPoint(
                localPosition + Vector3.up * partYOffset);
            worldRotation = transform.rotation * localRotation;
            return true;
        }

        private static bool IsOpenFeature(string candidateName)
        {
            if (string.IsNullOrEmpty(candidateName))
            {
                return false;
            }
            string lower = candidateName.ToLowerInvariant();
            return lower.Contains("window")
                || lower.Contains("arrow")
                || lower.Contains("door")
                || lower.Contains("arch")
                || lower.Contains("pier");
        }

        // The top of a vertical entrance stack uses the arch matching the lower
        // piers. Explicit Street Arch Variants win; otherwise the normal passage
        // pool supplies Wall_Arch / Wall_Arch_L / Wall_Arch_R /
        // Wall_DoubleArchway.
        private GameObject FindFamilyArch(PierFamily family)
        {
            if (string.IsNullOrEmpty(pierNameToken) || string.IsNullOrEmpty(archNameToken))
            {
                return null;
            }

            string familyKey = PierFamilyKey(family);
            GameObject match = FindMatchingArchIn(streetArchVariants, familyKey);
            if (match != null)
            {
                return match;
            }

            match = FindMatchingArchIn(passageVariants, familyKey);
            return match != null
                ? match
                : FindMatchingArchIn(solidVariants, familyKey);
        }

        private GameObject FindMatchingArchIn(List<GameObject> variants, string pierKey)
        {
            if (variants == null)
            {
                return null;
            }

            foreach (GameObject candidate in variants)
            {
                if (candidate == null
                    || candidate.name.IndexOf(
                        archNameToken,
                        System.StringComparison.OrdinalIgnoreCase) < 0
                    || candidate.name.IndexOf(
                        pierNameToken,
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (ArchPairKey(candidate.name) == pierKey)
                {
                    return candidate;
                }
            }

            return null;
        }

        // Reduces full-height and horizontal-half pieces to the same semantic
        // family. This is why HorizontalHalfWall_Archway_L_Pier correctly pairs
        // with Wall_Arch_L even though their literal name stems differ.
        private static string ArchPairKey(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return string.Empty;
            }

            string lower = rawName.ToLowerInvariant();
            if (lower.Contains("double"))
            {
                return "double";
            }

            string[] tokens = lower.Split(
                new[] { ' ', '_', '-', '.', '(', ')' },
                System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (token == "l" || token == "left")
                {
                    return "left";
                }

                if (token == "r" || token == "right")
                {
                    return "right";
                }
            }

            return "center";
        }

        // Kit parts have cell-centre origins, so the wall the designer already
        // placed (Default Solid) is the exact template. Cache that transform so a
        // hot-swap never derives rotation from the previously spawned variant.
        // Older building prefabs may contain an untracked variant child; recover
        // its placement once before ClearSpawned removes it.
        private void GetTemplatePlacement(Room3D.Directions facing, out Vector3 pos, out Quaternion rot)
        {
            if (defaultSolid != null)
            {
                pos = defaultSolid.transform.localPosition;
                rot = defaultSolid.transform.localRotation;
                CachePlacement(pos, rot);
                return;
            }

            GameObject existing = FindExistingVariantChild();
            if (existing != null)
            {
                pos = existing.transform.localPosition;
                rot = existing.transform.localRotation;
                CachePlacement(pos, rot);
                return;
            }

            if (hasCachedPlacement)
            {
                pos = cachedLocalPosition;
                rot = cachedLocalRotation;
                return;
            }

            pos = Vector3.zero;
            rot = Quaternion.Euler(0f, DirectionYaw(facing), 0f);
            CachePlacement(pos, rot);
        }

        private void CachePlacement(Vector3 pos, Quaternion rot)
        {
            cachedLocalPosition = pos;
            cachedLocalRotation = rot;
            hasCachedPlacement = true;
        }

        private static float DirectionYaw(Room3D.Directions facing)
        {
            switch (facing)
            {
                case Room3D.Directions.EAST: return 90f;
                case Room3D.Directions.SOUTH: return 180f;
                case Room3D.Directions.WEST: return 270f;
                default: return 0f; // NORTH / NONE
            }
        }

        private GameObject SpawnPart(GameObject prefab, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject instance = Instantiate(prefab, transform);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.name = SpawnedNamePrefix + prefab.name;
            return instance;
        }

        private static GameObject Pick(List<GameObject> list, System.Random rng)
        {
            if (list == null || list.Count == 0)
            {
                return null;
            }

            int valid = 0;
            foreach (GameObject go in list)
            {
                if (go != null)
                {
                    valid++;
                }
            }

            if (valid == 0)
            {
                return null;
            }

            int target = rng.Next(valid);
            foreach (GameObject go in list)
            {
                if (go == null)
                {
                    continue;
                }

                if (target-- == 0)
                {
                    return go;
                }
            }

            return null;
        }

        private GameObject PickNonPier(List<GameObject> list, System.Random rng)
        {
            if (list == null)
            {
                return null;
            }

            int valid = 0;
            foreach (GameObject go in list)
            {
                if (go != null && !IsPier(go))
                {
                    valid++;
                }
            }

            if (valid == 0)
            {
                return null;
            }

            int target = rng.Next(valid);
            foreach (GameObject go in list)
            {
                if (go == null || IsPier(go))
                {
                    continue;
                }

                if (target-- == 0)
                {
                    return go;
                }
            }

            return null;
        }

        private GameObject PickPier(
            List<GameObject> list,
            PierFamily family,
            System.Random rng)
        {
            if (list == null || string.IsNullOrEmpty(pierNameToken))
            {
                return null;
            }

            string requiredKey = PierFamilyKey(family);
            int valid = 0;
            foreach (GameObject go in list)
            {
                if (IsPier(go) && ArchPairKey(go.name) == requiredKey)
                {
                    valid++;
                }
            }

            if (valid == 0)
            {
                return null;
            }

            int target = rng.Next(valid);
            foreach (GameObject go in list)
            {
                if (!IsPier(go) || ArchPairKey(go.name) != requiredKey)
                {
                    continue;
                }

                if (target-- == 0)
                {
                    return go;
                }
            }

            return null;
        }

        private static string PierFamilyKey(PierFamily family)
        {
            switch (family)
            {
                case PierFamily.Left: return "left";
                case PierFamily.Right: return "right";
                case PierFamily.Double: return "double";
                default: return "center";
            }
        }

        private bool IsPier(GameObject candidate)
        {
            return candidate != null
                && candidate.name.IndexOf(
                    pierNameToken,
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ClearSpawned()
        {
            // The serialized references cover new prefabs. The child scan also
            // cleans variants baked before those references existed, preventing a
            // reroll from stacking another wall over an old one.
            HashSet<GameObject> instances = new HashSet<GameObject>();
            AddIfPresent(instances, spawnedVariant);
            AddIfPresent(instances, spawnedArch);

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != defaultSolid && IsGeneratedOrKnownVariant(child))
                {
                    instances.Add(child);
                }
            }

            spawnedVariant = null;
            spawnedArch = null;

            foreach (GameObject instance in instances)
            {
                DestroySpawned(instance);
            }
        }

        private GameObject FindExistingVariantChild()
        {
            if (spawnedVariant != null)
            {
                return spawnedVariant;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child != defaultSolid && IsGeneratedOrKnownVariant(child)
                    && !MatchesAnyVariant(child.name, streetArchVariants))
                {
                    return child;
                }
            }

            return null;
        }

        private bool IsGeneratedOrKnownVariant(GameObject child)
        {
            if (child == null)
            {
                return false;
            }

            string childName = child.name;
            return childName.StartsWith(SpawnedNamePrefix, System.StringComparison.Ordinal)
                || MatchesAnyVariant(childName, solidVariants)
                || MatchesAnyVariant(childName, passageVariants)
                || MatchesAnyVariant(childName, streetArchVariants);
        }

        private static bool MatchesAnyVariant(string instanceName, List<GameObject> variants)
        {
            if (variants == null)
            {
                return false;
            }

            string cleanName = instanceName.StartsWith(
                SpawnedNamePrefix,
                System.StringComparison.Ordinal)
                    ? instanceName.Substring(SpawnedNamePrefix.Length)
                    : instanceName;
            cleanName = cleanName.Replace("(Clone)", string.Empty).Trim();

            foreach (GameObject variant in variants)
            {
                if (variant != null && string.Equals(
                        cleanName,
                        variant.name,
                        System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddIfPresent(HashSet<GameObject> instances, GameObject instance)
        {
            if (instance != null)
            {
                instances.Add(instance);
            }
        }

        private void DestroySpawned(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        private void SetClosedDecorVisible(bool visible)
        {
            if (closedDecor == null)
            {
                return;
            }

            foreach (GameObject decor in closedDecor)
            {
                if (decor != null)
                {
                    decor.SetActive(visible);
                }
            }
        }
    }
}
