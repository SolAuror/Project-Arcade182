using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Rolls a spawn-time variant for an orbital cluster: ring size, atom
    /// count, spin speed, and simple patterns. The authored prefab (3 atoms
    /// at radius 1.3) stays the common case; bigger rings, fast spinners,
    /// and patterns are the rare rolls. Spin scales gently with the wave.
    /// Must run before the game registers child targets, so every atom the
    /// variant adds counts toward the wave.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AtomSmasherRotator))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Orbital Cluster")]
    public class AtomSmasherOrbitalCluster : MonoBehaviour
    {
        private enum Variant
        {
            Classic,   // authored ring of 3
            BigRing,   // longer arms, more atoms, bigger circle
            FastSpin,  // classic ring at high rpm
            Twin,      // two atoms on long opposite arms
            Cross      // four atoms at right angles
        }

        [Header("Variant odds (weights)")]
        [SerializeField, Min(0)] private int classicWeight = 62;
        [SerializeField, Min(0)] private int bigRingWeight = 14;
        [SerializeField, Min(0)] private int fastSpinWeight = 12;
        [SerializeField, Min(0)] private int twinWeight = 7;
        [SerializeField, Min(0)] private int crossWeight = 5;

        [Header("Shape")]
        [SerializeField, Min(0.5f)] private float classicRadius = 1.3f;
        [SerializeField, Min(0.5f)] private float bigRingRadiusMin = 1.55f;
        [SerializeField, Min(0.5f)] private float bigRingRadiusMax = 1.75f;
        [SerializeField, Min(0.5f)] private float twinRadius = 1.75f;

        [Header("Spin")]
        [Tooltip("Fast-spin variants multiply the authored spin by this range.")]
        [SerializeField, Min(1f)] private float fastSpinMultiplierMin = 1.7f;
        [SerializeField, Min(1f)] private float fastSpinMultiplierMax = 2.2f;

        [Tooltip("Every cluster spins this much faster per wave past its unlock, on top of variant rolls.")]
        [SerializeField, Min(0f)] private float spinGainPerWave = 0.04f;

        [Tooltip("Cap on the per-wave spin scaling (multiplier, not deg/s).")]
        [SerializeField, Min(1f)] private float maxWaveSpinMultiplier = 1.6f;

        [Tooltip("Wave the per-wave spin ramp starts counting from (cluster unlock wave).")]
        [SerializeField, Min(1)] private int spinRampStartWave = 5;

        [Header("Atom avoidance")]
        [Tooltip("Foreign atoms inside this range of an orbital push its arm in or out — the ring breathes around them. Balls, blockers, and this cluster's own atoms are ignored.")]
        [SerializeField, Min(0f)] private float orbitalAvoidRadius = 0.9f;

        [Tooltip("How far an arm may stretch or shrink from its rolled radius.")]
        [SerializeField, Min(0f)] private float maxRadiusFlex = 0.5f;

        [Tooltip("Seconds to ease into a dodge as a foreign atom nears.")]
        [SerializeField, Min(0.02f)] private float flexEaseSeconds = 0.35f;

        [Tooltip("Seconds to relax back to the rolled radius once clear.")]
        [SerializeField, Min(0.02f)] private float flexRelaxSeconds = 1.1f;

        /// <summary>Ring radius plus orbiting atom extent — the game sizes the placement footprint from this.</summary>
        public float OuterRadius { get; private set; }

        private struct OrbitalArm
        {
            public Transform Transform;
            public Vector3 LocalDirection;
            public float BaseRadius;
            public float LocalZ;
            public float Flex;
        }

        private static readonly Collider[] flexHits = new Collider[10];

        private OrbitalArm[] arms;
        private bool configured;

        private void Awake()
        {
            if (!configured)
            {
                OuterRadius = MeasureAuthoredRadius();
            }
        }

        public void ConfigureForWave(int waveNumber)
        {
            if (configured)
            {
                return;
            }

            configured = true;

            List<AtomSmasherTarget> orbitals = CollectOrbitalAtoms();
            if (orbitals.Count == 0)
            {
                OuterRadius = MeasureAuthoredRadius();
                return;
            }

            Variant variant = RollVariant();
            float radius = classicRadius;
            int atomCount = orbitals.Count;

            switch (variant)
            {
                case Variant.BigRing:
                    radius = Random.Range(bigRingRadiusMin, bigRingRadiusMax);
                    atomCount = Random.value < 0.5f ? 5 : 6;
                    break;
                case Variant.Twin:
                    radius = twinRadius;
                    atomCount = 2;
                    break;
                case Variant.Cross:
                    atomCount = 4;
                    break;
            }

            LayOutRing(orbitals, atomCount, radius);

            AtomSmasherRotator rotator = GetComponent<AtomSmasherRotator>();
            if (rotator != null)
            {
                float waveScale = Mathf.Min(1f + Mathf.Max(0, waveNumber - spinRampStartWave) * spinGainPerWave, maxWaveSpinMultiplier);
                float variantScale = variant == Variant.FastSpin ? Random.Range(fastSpinMultiplierMin, fastSpinMultiplierMax)
                    : variant == Variant.Twin ? 1.3f
                    : 1f;
                rotator.DegreesPerSecond *= waveScale * variantScale;
            }

            float atomExtent = 0.3f;
            foreach (AtomSmasherTarget orbital in orbitals)
            {
                if (orbital != null)
                {
                    atomExtent = Mathf.Max(atomExtent, orbital.transform.localScale.x * 0.5f);
                    break;
                }
            }

            OuterRadius = radius + atomExtent;
            arms = null; // rebuilt from the new layout on the next physics step
        }

        // Arms flex radially so orbitals breathe around foreign atoms instead
        // of sweeping through them. Only atoms push; balls must still connect,
        // and passing over blockers is fine by design.
        private void FixedUpdate()
        {
            EnsureArms();

            for (int i = 0; i < arms.Length; i++)
            {
                Transform arm = arms[i].Transform;
                if (arm == null || !arm.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 worldPosition = arm.position;
                Vector3 radial = worldPosition - transform.position;
                radial.z = 0f;
                if (radial.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                radial.Normalize();

                float desiredFlex = 0f;
                int hitCount = Physics.OverlapSphereNonAlloc(worldPosition, orbitalAvoidRadius, flexHits, ~0, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < hitCount; h++)
                {
                    Collider hit = flexHits[h];
                    if (hit == null || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<AtomSmasherTarget>() == null)
                    {
                        continue;
                    }

                    Vector3 away = worldPosition - hit.ClosestPoint(worldPosition);
                    away.z = 0f;
                    float distance = away.magnitude;
                    if (distance >= orbitalAvoidRadius)
                    {
                        continue;
                    }

                    Vector3 pushDirection = distance > 0.001f ? away / distance : radial;
                    desiredFlex += Vector3.Dot(pushDirection, radial) * (1f - distance / orbitalAvoidRadius);
                }

                float flexFloor = -Mathf.Min(maxRadiusFlex, Mathf.Max(0f, arms[i].BaseRadius - 0.6f));
                desiredFlex = Mathf.Clamp(Mathf.Clamp(desiredFlex, -1f, 1f) * maxRadiusFlex, flexFloor, maxRadiusFlex);

                float ease = Mathf.Abs(desiredFlex) > Mathf.Abs(arms[i].Flex) ? flexEaseSeconds : flexRelaxSeconds;
                arms[i].Flex = Mathf.Lerp(arms[i].Flex, desiredFlex, 1f - Mathf.Exp(-Time.fixedDeltaTime / ease));

                Vector3 flexed = arms[i].LocalDirection * (arms[i].BaseRadius + arms[i].Flex);
                flexed.z = arms[i].LocalZ;
                arm.localPosition = flexed;
            }
        }

        private void EnsureArms()
        {
            if (arms != null)
            {
                return;
            }

            List<AtomSmasherTarget> orbitals = CollectOrbitalAtoms();
            arms = new OrbitalArm[orbitals.Count];
            for (int i = 0; i < orbitals.Count; i++)
            {
                Vector3 local = orbitals[i].transform.localPosition;
                Vector3 planar = new Vector3(local.x, local.y, 0f);
                arms[i] = new OrbitalArm
                {
                    Transform = orbitals[i].transform,
                    LocalDirection = planar.normalized,
                    BaseRadius = planar.magnitude,
                    LocalZ = local.z,
                    Flex = 0f
                };
            }
        }

        private Variant RollVariant()
        {
            int total = classicWeight + bigRingWeight + fastSpinWeight + twinWeight + crossWeight;
            if (total <= 0)
            {
                return Variant.Classic;
            }

            int roll = Random.Range(0, total);
            if ((roll -= classicWeight) < 0) return Variant.Classic;
            if ((roll -= bigRingWeight) < 0) return Variant.BigRing;
            if ((roll -= fastSpinWeight) < 0) return Variant.FastSpin;
            if ((roll -= twinWeight) < 0) return Variant.Twin;
            return Variant.Cross;
        }

        // Repositions the authored atoms evenly around the ring, cloning the
        // first one for extra slots. Surplus atoms deactivate before their
        // deferred Destroy so the game's same-frame target registration
        // (which skips inactive children) never counts them.
        private void LayOutRing(List<AtomSmasherTarget> orbitals, int atomCount, float radius)
        {
            AtomSmasherTarget template = orbitals[0];
            float phase = Random.Range(0f, 360f);

            for (int i = 0; i < atomCount; i++)
            {
                AtomSmasherTarget atom;
                if (i < orbitals.Count)
                {
                    atom = orbitals[i];
                }
                else
                {
                    atom = Instantiate(template, transform);
                    atom.name = template.name + " +" + i;
                }

                float angle = (phase + 360f * i / atomCount) * Mathf.Deg2Rad;
                atom.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, template.transform.localPosition.z);
            }

            for (int i = atomCount; i < orbitals.Count; i++)
            {
                if (orbitals[i] != null)
                {
                    orbitals[i].gameObject.SetActive(false);
                    Destroy(orbitals[i].gameObject);
                }
            }
        }

        // Orbital atoms are the direct-child targets sitting off-center;
        // the nucleus (if it ever gains a target) stays untouched.
        private List<AtomSmasherTarget> CollectOrbitalAtoms()
        {
            List<AtomSmasherTarget> orbitals = new List<AtomSmasherTarget>();
            foreach (AtomSmasherTarget target in GetComponentsInChildren<AtomSmasherTarget>())
            {
                if (target != null && target.transform.localPosition.sqrMagnitude > 0.04f)
                {
                    orbitals.Add(target);
                }
            }

            return orbitals;
        }

        private float MeasureAuthoredRadius()
        {
            float radius = classicRadius;
            foreach (AtomSmasherTarget target in CollectOrbitalAtoms())
            {
                radius = Mathf.Max(radius, target.transform.localPosition.magnitude + target.transform.localScale.x * 0.5f);
            }

            return radius;
        }
    }
}
