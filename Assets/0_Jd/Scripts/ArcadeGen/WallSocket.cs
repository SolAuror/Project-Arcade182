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

        [Header("Street Arch (pier tops)")]
        [Tooltip("Arch pieces sprung ON TOP of a pier. When the solid variant chosen for a CLOSED edge is a pier (its name contains the Pier Name Token), its MATCHING arch is spawned over it - the arch whose name is the pier's name with the pier token swapped for the arch token (e.g. Wall_Pier_L -> Wall_Arch_L). Each pier variant (double / center / _L / _R) pairs 1:1 with its own arch. Empty = no street arches (the default).")]
        [SerializeField] private List<GameObject> streetArchVariants = new List<GameObject>();

        [Tooltip("A chosen solid variant counts as a pier when its name contains this text, case-insensitive.")]
        [SerializeField] private string pierNameToken = "pier";

        [Tooltip("Its matching arch is found by swapping the pier token for this one in the pier's name (Wall_Pier_L -> Wall_Arch_L).")]
        [SerializeField] private string archNameToken = "arch";

        [Tooltip("Marks a pier as a HALF variant. Ignored when matching to an arch, so a half pier pairs with the SAME arch as its full pier (each arch = 1 full + 1 half pier). Blank = no half handling.")]
        [SerializeField] private string halfPierToken = "half";

        [Tooltip("Chance a pier gets its arch. 1 = every pier (and draws no RNG). Uses the dressing pass's own RNG, so it never affects the maze carve.")]
        [Range(0f, 1f)] [SerializeField] private float streetArchChance = 1f;

        // Instances spawned by the last ApplyStyle, cleared before the next so a
        // regenerate (or an edit-time rebuild) never leaves doubled geometry.
        private GameObject spawnedVariant;
        private GameObject spawnedArch;

        /// <summary>
        /// Renders this edge for its final carved state. Called once per generation
        /// by ArcadeGen3D's DressWalls post-carve pass. <paramref name="rng"/> is
        /// that pass's isolated System.Random - never pass UnityEngine.Random here.
        /// </summary>
        public void ApplyStyle(Room3D.Directions facing, bool open, bool outer, System.Random rng)
        {
            ClearSpawned();

            GameObject chosen = open ? Pick(passageVariants, rng) : Pick(solidVariants, rng);

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

            GetTemplatePlacement(facing, out Vector3 pos, out Quaternion rot);

            if (chosen != null)
            {
                spawnedVariant = SpawnPart(chosen, pos + Vector3.up * partYOffset, rot);

                if (!open)
                {
                    TrySpawnStreetArch(chosen, pos, rot, rng);
                }
            }
        }

        // When the chosen solid variant is a pier (its name contains the Pier Name
        // Token), spawn its MATCHING arch on top - the arch whose name is the pier's
        // with the pier token swapped for the arch token (Wall_Pier_L -> Wall_Arch_L),
        // so each pier variant (double / center / _L / _R) gets its own paired arch.
        // Same placement as the pier (kit parts share a cell-centre origin), so the
        // arch - authored to sit at the pier top - lands right on it. Uses the
        // dressing pass's System.Random only, never the maze carve RNG. Inert until
        // Street Arch Variants are assigned.
        private void TrySpawnStreetArch(GameObject chosenSolid, Vector3 pos, Quaternion rot, System.Random rng)
        {
            if (streetArchVariants == null || streetArchVariants.Count == 0
                || string.IsNullOrEmpty(pierNameToken) || string.IsNullOrEmpty(archNameToken))
            {
                return;
            }

            if (chosenSolid.name.IndexOf(pierNameToken, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return; // not a pier
            }

            if (streetArchChance < 1f && rng.NextDouble() > streetArchChance)
            {
                return;
            }

            GameObject archPrefab = FindMatchingArch(chosenSolid.name);
            if (archPrefab == null)
            {
                Debug.LogWarning($"{name}: pier '{chosenSolid.name}' has no matching arch in Street Arch Variants " +
                    $"(match ignores the '{pierNameToken}', '{archNameToken}' and '{halfPierToken}' tokens).", this);
                return;
            }

            spawnedArch = SpawnPart(archPrefab, pos + Vector3.up * partYOffset, rot);
        }

        // The arch paired with a pier - the SAME arch for a full pier and its half
        // pier. Matched on the variant key: the name with the pier, arch and half
        // tokens plus all non-alphanumerics stripped, so Wall_Pier_L, Wall_Pier_L_Half
        // and Wall_Arch_L all reduce to the same key.
        private GameObject FindMatchingArch(string pierName)
        {
            string pierKey = VariantKey(pierName);
            if (string.IsNullOrEmpty(pierKey))
            {
                return null;
            }

            foreach (GameObject arch in streetArchVariants)
            {
                if (arch != null && VariantKey(arch.name) == pierKey)
                {
                    return arch;
                }
            }

            return null;
        }

        // Reduces a pier/arch name to its bare variant key (see FindMatchingArch).
        private string VariantKey(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return string.Empty;
            }

            string stripped = rawName.ToLowerInvariant();
            stripped = RemoveToken(stripped, pierNameToken);
            stripped = RemoveToken(stripped, archNameToken);
            stripped = RemoveToken(stripped, halfPierToken);

            System.Text.StringBuilder key = new System.Text.StringBuilder(stripped.Length);
            foreach (char c in stripped)
            {
                if (char.IsLetterOrDigit(c))
                {
                    key.Append(c);
                }
            }

            return key.ToString();
        }

        private static string RemoveToken(string lowerName, string token)
        {
            return string.IsNullOrEmpty(token)
                ? lowerName
                : lowerName.Replace(token.ToLowerInvariant(), string.Empty);
        }

        // Kit parts have cell-centre origins, so the wall the designer already
        // placed (Default Solid) is the exact template: reuse its local position
        // and rotation and a variant drops into the same slot, correctly oriented,
        // whatever direction the wall faces. Only when Default Solid is missing do
        // we fall back to a plain per-direction yaw about the cell centre.
        private void GetTemplatePlacement(Room3D.Directions facing, out Vector3 pos, out Quaternion rot)
        {
            if (defaultSolid != null)
            {
                pos = defaultSolid.transform.localPosition;
                rot = defaultSolid.transform.localRotation;
                return;
            }

            pos = Vector3.zero;
            rot = Quaternion.Euler(0f, DirectionYaw(facing), 0f);
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
            instance.name = prefab.name;
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

        private void ClearSpawned()
        {
            DestroySpawned(ref spawnedVariant);
            DestroySpawned(ref spawnedArch);
        }

        private void DestroySpawned(ref GameObject instance)
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

            instance = null;
        }
    }
}
