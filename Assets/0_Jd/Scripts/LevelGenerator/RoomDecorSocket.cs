using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Cosmetic per-CELL crown for the lost-city reskin, driven by ArcadeGen3D's
    /// dressing pass. Caps a cell with either a flat roof (multi-cell buildings),
    /// a pitched roof assembled from matched gable slopes (standalone houses), or
    /// corner-post ornaments. Lives on the room prefab root next to Room3D; rooms
    /// without one keep the legacy look.
    ///
    /// It sits on every floor prefab - the ground cells and the stacked
    /// upper-floor cells alike - and always caps THIS cell's own wall top, so the
    /// generator only ever asks the topmost floor of a building to show a roof.
    /// Because the roof is a per-cell feature (not tied to a shared, de-doubled
    /// wall) the gable slopes always close into one coherent shape instead of the
    /// jagged half-roofs a lone wall-mounted gable produced.
    ///
    /// Placement follows the kit convention: every FBX has its origin at the CELL
    /// CENTRE with geometry baked in place (Blender Z-up), so a part spawns at
    /// local position zero (lifted to the wall top) with the Z-up conversion
    /// rotation plus a per-direction yaw. Corner caps are authored on one specific
    /// corner (Corner_Cap1_L/R and Cap2_L south-east, Cap2_R south-west); each
    /// variant records its authored corner so the yaw to any target corner is the
    /// step difference around the SE-SW-NW-NE cycle.
    ///
    /// Selection uses the caller-supplied System.Random of the dressing pass -
    /// never UnityEngine.Random - so decoration can never perturb the carve stream
    /// (the hub stays byte-identical).
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomDecorSocket : MonoBehaviour
    {
        private const string SpawnedNamePrefix = "__RoomDecor__";

        /// <summary>
        /// Cell corners in +90-degree yaw order starting at the corner the caps
        /// are authored on. Rotating a part by k steps moves geometry authored on
        /// SouthEast to the corner k places later in this cycle.
        /// </summary>
        public enum Corner
        {
            SouthEast = 0,
            SouthWest = 1,
            NorthWest = 2,
            NorthEast = 3,
        }

        /// <summary>
        /// Which gable piece a wall of a pitched roof shows. Left and Right are
        /// the two mirror-image roof slopes that pair up to close a peak; End is a
        /// symmetric gable-end (Gable_Sloped / Gable_Stepped) that caps the short
        /// side of a ridge. Packed 2 bits per direction by the generator.
        /// </summary>
        public enum GableRole
        {
            None = 0,
            Left = 1,
            Right = 2,
            End = 3,
        }

        public enum AuthoredRoofType
        {
            None,
            Flat,
            RidgeNorthSouth,
            RidgeEastWest,
        }

        [System.Serializable]
        public class CornerCapVariant
        {
            [Tooltip("Corner cap model (Corner_Cap1_L/R, Corner_Cap2_L/R).")]
            public GameObject prefab;

            [Tooltip("The corner this model's geometry is authored on at yaw 0. Corner_Cap1_L/R and Cap2_L are authored south-east; Cap2_R is authored south-west.")]
            public Corner authoredCorner = Corner.SouthEast;
        }

        [Tooltip("Full-cell flat roof pieces (Roof_SingleFull). One is picked when the generator flat-roofs this cell - the top of a multi-cell building.")]
        [SerializeField] private List<GameObject> roofVariants = new List<GameObject>();

        [Header("Pitched roof (gable slopes)")]
        [Tooltip("The Gable_Left roof slope. Paired with Gable_Right on the opposite or adjacent wall to close a ridge or hip.")]
        [SerializeField] private GameObject gableLeft;

        [Tooltip("The Gable_Right roof slope, mirror of Gable_Left.")]
        [SerializeField] private GameObject gableRight;

        [Tooltip("Symmetric gable-END pieces (Gable_Sloped, Gable_Stepped) that close the short side of a ridge roof. One is picked at random.")]
        [SerializeField] private List<GameObject> gableEndVariants = new List<GameObject>();

        [Header("Corner posts")]
        [Tooltip("Small ornaments capping the corner posts. The generator picks corners so a post shared by neighbouring cells is capped at most once.")]
        [SerializeField] private List<CornerCapVariant> cornerCaps = new List<CornerCapVariant>();

        [Header("Placement")]
        [Tooltip("Wall-top height of THIS cell (the kit wall spans 0..5.97; 5.95 overlaps slightly to kill the seam). Roofs, gables and caps are authored at ground level and lifted to here so they sit on top of the cell.")]
        [SerializeField] private float crownYOffset = 5.95f;

        [Tooltip("Extra vertical nudge for the flat roof piece only (its eaves are authored to drape below its own origin).")]
        [SerializeField] private float roofYOffset;

        [Tooltip("Extra vertical nudge for gable slope/end pieces only.")]
        [SerializeField] private float gableYOffset;

        [Tooltip("Extra vertical nudge for corner caps only.")]
        [SerializeField] private float capYOffset;

        [Tooltip("Rotation converting the kit's Blender Z-up authoring space into the cell's local space; matches how the wall/corner models are placed in the room prefabs (euler 90,0,0). Yaw is applied on top of this.")]
        [SerializeField] private Vector3 modelBaseEuler = new Vector3(90f, 0f, 0f);

        [Header("Authored Building")]
        [Tooltip("Roof selected for this cell by the Building Component Inspector.")]
        [SerializeField] private AuthoredRoofType authoredRoof;

        public AuthoredRoofType AuthoredRoof => authoredRoof;

        // Instances spawned by the last ApplyCrown, cleared before the next so a
        // regenerate never leaves doubled geometry. Serialized so roofs baked into
        // an authored building remain replaceable after a domain reload.
        [SerializeField, HideInInspector] private List<GameObject> spawned = new List<GameObject>();

        /// <summary>Packs a per-direction gable role set into one int (2 bits each).</summary>
        public static int PackGable(GableRole north, GableRole south, GableRole east, GableRole west)
        {
            return ((int)north & 3)
                | (((int)south & 3) << 2)
                | (((int)east & 3) << 4)
                | (((int)west & 3) << 6);
        }

        /// <summary>
        /// Renders this cell's crown for the current generation. Called for every
        /// dressed cell - including with everything off - so a reused room never
        /// keeps a stale roof or cap from the previous layout.
        /// </summary>
        /// <param name="flatRoof">Spawn a flat roof (top of a multi-cell building).</param>
        /// <param name="roofYawSteps">Flat-roof orientation in 90-degree steps.</param>
        /// <param name="gableRolesPacked">Per-direction gable slopes (see PackGable); 0 = none.</param>
        /// <param name="capMask">Bit per <see cref="Corner"/> value to cap.</param>
        public void ApplyCrown(bool flatRoof, int roofYawSteps, int gableRolesPacked, int capMask, System.Random rng)
        {
            ClearSpawned();

            Quaternion baseRot = Quaternion.Euler(modelBaseEuler);

            if (flatRoof)
            {
                GameObject roof = Pick(roofVariants, rng);
                if (roof != null)
                {
                    Spawn(roof, crownYOffset + roofYOffset,
                        Quaternion.Euler(0f, 90f * roofYawSteps, 0f) * baseRot);
                }
            }

            if (gableRolesPacked != 0)
            {
                SpawnGable(Room3D.Directions.NORTH, Unpack(gableRolesPacked, 0), baseRot, rng);
                SpawnGable(Room3D.Directions.SOUTH, Unpack(gableRolesPacked, 1), baseRot, rng);
                SpawnGable(Room3D.Directions.EAST, Unpack(gableRolesPacked, 2), baseRot, rng);
                SpawnGable(Room3D.Directions.WEST, Unpack(gableRolesPacked, 3), baseRot, rng);
            }

            for (int corner = 0; corner < 4 && capMask != 0; corner++)
            {
                if ((capMask & (1 << corner)) == 0)
                {
                    continue;
                }

                CornerCapVariant cap = PickCap(rng);
                if (cap == null)
                {
                    continue;
                }

                int yawSteps = (corner - (int)cap.authoredCorner + 4) % 4;
                Spawn(cap.prefab, crownYOffset + capYOffset,
                    Quaternion.Euler(0f, 90f * yawSteps, 0f) * baseRot);
            }
        }

        /// <summary>Stores and applies the roof chosen by the building authoring tool.</summary>
        public void ApplyAuthoredRoof(AuthoredRoofType roofType, System.Random rng)
        {
            authoredRoof = roofType;

            switch (roofType)
            {
                case AuthoredRoofType.Flat:
                    ApplyCrown(true, 0, 0, 0, rng);
                    break;

                case AuthoredRoofType.RidgeNorthSouth:
                    ApplyCrown(
                        false,
                        0,
                        PackGable(
                            GableRole.End,
                            GableRole.End,
                            GableRole.Left,
                            GableRole.Right),
                        0,
                        rng);
                    break;

                case AuthoredRoofType.RidgeEastWest:
                    ApplyCrown(
                        false,
                        0,
                        PackGable(
                            GableRole.Left,
                            GableRole.Right,
                            GableRole.End,
                            GableRole.End),
                        0,
                        rng);
                    break;

                default:
                    ApplyCrown(false, 0, 0, 0, rng);
                    break;
            }
        }

        private static GableRole Unpack(int packed, int dirSlot)
        {
            return (GableRole)((packed >> (dirSlot * 2)) & 3);
        }

        private void SpawnGable(Room3D.Directions dir, GableRole role, Quaternion baseRot, System.Random rng)
        {
            GameObject piece =
                role == GableRole.Left ? gableLeft :
                role == GableRole.Right ? gableRight :
                role == GableRole.End ? Pick(gableEndVariants, rng) : null;

            if (piece == null)
            {
                return;
            }

            Spawn(piece, crownYOffset + gableYOffset,
                Quaternion.Euler(0f, DirectionYaw(dir), 0f) * baseRot);
        }

        // Per-direction yaw that rotates an east-authored wall-slot piece onto the
        // named wall, matching the room prefabs (E=0, S=90, W=180, N=270).
        private static float DirectionYaw(Room3D.Directions dir)
        {
            switch (dir)
            {
                case Room3D.Directions.EAST: return 0f;
                case Room3D.Directions.SOUTH: return 90f;
                case Room3D.Directions.WEST: return 180f;
                default: return 270f; // NORTH
            }
        }

        private void Spawn(GameObject prefab, float lift, Quaternion localRotation)
        {
            GameObject instance = Instantiate(prefab, transform);
            instance.transform.localPosition = Vector3.up * lift;
            instance.transform.localRotation = localRotation;
            instance.name = SpawnedNamePrefix + prefab.name;
            spawned.Add(instance);
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

        private CornerCapVariant PickCap(System.Random rng)
        {
            if (cornerCaps == null || cornerCaps.Count == 0)
            {
                return null;
            }

            int valid = 0;
            foreach (CornerCapVariant cap in cornerCaps)
            {
                if (cap != null && cap.prefab != null)
                {
                    valid++;
                }
            }

            if (valid == 0)
            {
                return null;
            }

            int target = rng.Next(valid);
            foreach (CornerCapVariant cap in cornerCaps)
            {
                if (cap == null || cap.prefab == null)
                {
                    continue;
                }

                if (target-- == 0)
                {
                    return cap;
                }
            }

            return null;
        }

        private void ClearSpawned()
        {
            if (spawned == null)
            {
                spawned = new List<GameObject>();
            }

            HashSet<GameObject> instances = new HashSet<GameObject>();
            foreach (GameObject go in spawned)
            {
                if (go != null)
                {
                    instances.Add(go);
                }
            }

            // Also recover roofs baked before spawned references were serialized,
            // so the roof controls remain an idempotent hot-swap.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (IsGeneratedOrKnownDecor(child))
                {
                    instances.Add(child);
                }
            }

            spawned.Clear();

            foreach (GameObject go in instances)
            {
                if (Application.isPlaying)
                {
                    Destroy(go);
                }
                else
                {
                    DestroyImmediate(go);
                }
            }
        }

        private bool IsGeneratedOrKnownDecor(GameObject child)
        {
            if (child == null)
            {
                return false;
            }

            string childName = child.name;
            if (childName.StartsWith(SpawnedNamePrefix, System.StringComparison.Ordinal)
                || Matches(childName, roofVariants)
                || Matches(childName, gableEndVariants)
                || Matches(childName, gableLeft)
                || Matches(childName, gableRight))
            {
                return true;
            }

            if (cornerCaps != null)
            {
                foreach (CornerCapVariant cap in cornerCaps)
                {
                    if (cap != null && Matches(childName, cap.prefab))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Matches(string instanceName, List<GameObject> prefabs)
        {
            if (prefabs == null)
            {
                return false;
            }

            foreach (GameObject prefab in prefabs)
            {
                if (Matches(instanceName, prefab))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Matches(string instanceName, GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            string cleanName = instanceName.StartsWith(
                SpawnedNamePrefix,
                System.StringComparison.Ordinal)
                    ? instanceName.Substring(SpawnedNamePrefix.Length)
                    : instanceName;
            cleanName = cleanName.Replace("(Clone)", string.Empty).Trim();
            return string.Equals(
                cleanName,
                prefab.name,
                System.StringComparison.Ordinal);
        }
    }
}
