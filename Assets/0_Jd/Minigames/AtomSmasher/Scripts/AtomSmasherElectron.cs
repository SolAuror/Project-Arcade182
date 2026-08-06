using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Purely visual spark released when an atom is smashed: darts around the
    /// board plane like an electron, reflecting off walls and obstructions
    /// (never off atoms or balls) for a couple of bounces before decaying.
    /// Its renderer and trail are authored on a reusable prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Electron")]
    public class AtomSmasherElectron : MonoBehaviour
    {
        private static readonly List<AtomSmasherElectron> ActiveElectrons = new List<AtomSmasherElectron>();

        /// <summary>All live electron sparks, for board-wide effects like black holes.</summary>
        public static IReadOnlyList<AtomSmasherElectron> Active => ActiveElectrons;

        private Vector3 velocity;
        private float planeZ;
        private float radius = 0.05f;
        private int maxBounces = 2;
        private int bounceCount;
        private float dieTime;
        private float fadeSeconds = 0.3f;
        private Vector3 baseScale;
        [SerializeField] private Renderer sparkRenderer;
        [SerializeField] private TrailRenderer sparkTrail;

        private MaterialPropertyBlock propertyBlock;
        private Color baseColor;

        public static AtomSmasherElectron Spawn(
            AtomSmasherElectron prefab,
            Vector3 position,
            Vector3 planarVelocity,
            Color color,
            float lifeSeconds,
            int maxBounces,
            float scale,
            float planeZ)
        {
            if (prefab == null)
            {
                Debug.LogError(
                    $"{nameof(AtomSmasherElectron)} requires an authored prefab. " +
                    "Check the authored Atom Smasher electron prefab reference.");
                return null;
            }

            AtomSmasherElectron electron = Instantiate(
                prefab,
                new Vector3(position.x, position.y, planeZ),
                Quaternion.identity);
            electron.transform.localScale = Vector3.one * scale;
            electron.velocity = new Vector3(planarVelocity.x, planarVelocity.y, 0f);
            electron.planeZ = planeZ;
            electron.radius = scale * 0.5f;
            electron.maxBounces = Mathf.Max(0, maxBounces);
            electron.dieTime = Time.time + Mathf.Max(0.2f, lifeSeconds);
            electron.baseScale = electron.transform.localScale;
            electron.baseColor = color;
            electron.ApplySparkColor(color);

            if (electron.sparkTrail != null)
            {
                electron.sparkTrail.time = 0.18f;
                electron.sparkTrail.startWidth = scale * 0.7f;
                electron.sparkTrail.endWidth = 0f;
                electron.sparkTrail.startColor = color;
                electron.sparkTrail.endColor = new Color(color.r, color.g, color.b, 0f);
                electron.sparkTrail.Clear();
                electron.sparkTrail.emitting = true;
            }

            return electron;
        }

        private void OnEnable()
        {
            ActiveElectrons.Add(this);
        }

        private void OnDisable()
        {
            ActiveElectrons.Remove(this);
        }

        /// <summary>Bends this spark's velocity toward a point (black hole pull).</summary>
        public void Attract(Vector3 point, float acceleration, float deltaTime)
        {
            Vector3 toPoint = point - transform.position;
            toPoint.z = 0f;
            if (toPoint.sqrMagnitude < 0.0001f)
            {
                return;
            }

            velocity += toPoint.normalized * (acceleration * deltaTime);
        }

        /// <summary>Removes this spark immediately (swallowed by a hazard).</summary>
        public void Consume()
        {
            Destroy(gameObject);
        }

        private void Update()
        {
            float remaining = dieTime - Time.time;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            MoveWithBounces(Time.deltaTime);

            // Shrink and fade over the final moments.
            float fade = Mathf.Clamp01(remaining / fadeSeconds);
            transform.localScale = baseScale * Mathf.Lerp(0.25f, 1f, fade);
            if (sparkRenderer != null)
            {
                ApplySparkColor(new Color(baseColor.r, baseColor.g, baseColor.b, fade));
            }
        }

        private void ApplySparkColor(Color color)
        {
            if (sparkRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            sparkRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            sparkRenderer.SetPropertyBlock(propertyBlock);
        }

        private void MoveWithBounces(float deltaTime)
        {
            Vector3 position = transform.position;
            float travel = velocity.magnitude * deltaTime;
            Vector3 direction = velocity.normalized;

            RaycastHit[] hits = Physics.RaycastAll(position, direction, travel + radius, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                // Electrons pass through atoms and balls; only walls and
                // obstructions reflect them.
                if (hit.collider.GetComponentInParent<AtomSmasherTarget>() != null ||
                    hit.collider.GetComponentInParent<AtomSmasherBall>() != null)
                {
                    continue;
                }

                bounceCount++;
                if (bounceCount > maxBounces)
                {
                    dieTime = Mathf.Min(dieTime, Time.time + fadeSeconds);
                    break;
                }

                Vector3 normal = hit.normal;
                normal.z = 0f;
                if (normal.sqrMagnitude < 0.001f)
                {
                    break;
                }

                velocity = Vector3.Reflect(velocity, normal.normalized);
                position = hit.point + normal.normalized * (radius + 0.01f);
                direction = velocity.normalized;
                break;
            }

            position += velocity * deltaTime;
            position.z = planeZ;
            transform.position = position;
        }
    }
}
