using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Purely visual spark released when an atom is smashed: darts around the
    /// board plane like an electron, reflecting off walls and obstructions
    /// (never off atoms or balls) for a couple of bounces before decaying.
    /// Built procedurally — no prefab required.
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
        private Renderer sparkRenderer;
        private Color baseColor;

        public static AtomSmasherElectron Spawn(
            Vector3 position,
            Vector3 planarVelocity,
            Color color,
            float lifeSeconds,
            int maxBounces,
            float scale,
            float planeZ)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "AtomElectron";
            Collider sparkCollider = spark.GetComponent<Collider>();
            if (sparkCollider != null)
            {
                Destroy(sparkCollider);
            }

            spark.transform.position = new Vector3(position.x, position.y, planeZ);
            spark.transform.localScale = Vector3.one * scale;

            Renderer sparkRenderer = spark.GetComponent<Renderer>();
            if (sparkRenderer != null)
            {
                sparkRenderer.material.color = color;
            }

            TrailRenderer trail = spark.AddComponent<TrailRenderer>();
            Shader trailShader = Shader.Find("Sprites/Default");
            if (trailShader != null)
            {
                trail.material = new Material(trailShader);
            }

            trail.time = 0.18f;
            trail.startWidth = scale * 0.7f;
            trail.endWidth = 0f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);

            AtomSmasherElectron electron = spark.AddComponent<AtomSmasherElectron>();
            electron.velocity = new Vector3(planarVelocity.x, planarVelocity.y, 0f);
            electron.planeZ = planeZ;
            electron.radius = scale * 0.5f;
            electron.maxBounces = Mathf.Max(0, maxBounces);
            electron.dieTime = Time.time + Mathf.Max(0.2f, lifeSeconds);
            electron.baseScale = spark.transform.localScale;
            electron.sparkRenderer = sparkRenderer;
            electron.baseColor = color;
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
                sparkRenderer.material.color = new Color(baseColor.r, baseColor.g, baseColor.b, fade);
            }
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
