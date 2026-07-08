using System;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Instant raycast spell: damages the first opposite-faction <see cref="Health"/>
    /// along the aim ray and draws a short-lived beam.
    /// </summary>
    [CreateAssetMenu(fileName = "Spell_Hitscan", menuName = "Sol/Spells/Hitscan Spell")]
    public class HitscanSpellDefinition : SpellDefinition
    {
        [Header("Hitscan")]
        [Tooltip("Maximum beam distance.")]
        [SerializeField, Min(0.5f)] private float range = 30f;

        [Header("Beam Visual")]
        [SerializeField] private Color beamColor = new Color(0.4f, 0.9f, 1f, 1f);
        [SerializeField, Min(0.001f)] private float beamWidth = 0.05f;
        [SerializeField, Min(0.01f)] private float beamLifeSeconds = 0.08f;

        public float Range => range;

        public override void Cast(in SpellCastContext context)
        {
            Ray ray = context.AimRay;
            Vector3 beamEnd = ray.origin + ray.direction * range;

            RaycastHit[] hits = Physics.RaycastAll(ray, range, context.HitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (IsSelfHit(context, hit.collider))
                {
                    continue;
                }

                beamEnd = hit.point;

                Health health = FindHealth(hit.collider);
                if (health != null && health.Faction != context.Faction)
                {
                    health.TakeDamage(GetDamage(context), context.Faction);
                }

                break; // first non-self hit ends the beam, wall or target
            }

            SpawnBeam(context.Origin, beamEnd);
        }

        private void SpawnBeam(Vector3 start, Vector3 end)
        {
            GameObject beamObject = new GameObject($"{name} Beam");
            LineRenderer line = beamObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = beamWidth;
            line.endWidth = beamWidth;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
            }

            line.startColor = beamColor;
            line.endColor = beamColor;

            UnityEngine.Object.Destroy(beamObject, beamLifeSeconds);
        }
    }
}
