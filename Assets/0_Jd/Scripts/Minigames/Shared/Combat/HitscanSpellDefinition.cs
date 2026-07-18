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
        [Tooltip("Authored layered beam prefab (core + glow + motes). Falls back to a runtime LineRenderer when empty.")]
        [SerializeField] private HitscanBeam beamPrefab;

        [SerializeField] private Color beamColor = new Color(0.4f, 0.9f, 1f, 1f);
        [SerializeField, Min(0.001f)] private float beamWidth = 0.05f;
        [SerializeField, Min(0.01f)] private float beamLifeSeconds = 0.08f;

        public float Range => range;

        // Reused across casts so rapid fire (the player's held Laser) does not
        // allocate a GameObject + Material every shot. Recreated when a scene
        // unload destroys the beam object.
        private HitscanBeam beam;
        private Material beamMaterial;

        public override void Cast(in SpellCastContext context)
        {
            Ray ray = context.AimRay;
            Vector3 beamEnd = ray.origin + ray.direction * range;

            RaycastHit[] hits = Physics.RaycastAll(ray, range, context.HitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            bool struck = false;
            foreach (RaycastHit hit in hits)
            {
                if (IsSelfHit(context, hit.collider))
                {
                    continue;
                }

                // The beam ignores the caster's own projectiles entirely, but
                // shoots enemy projectiles out of the air.
                Projectile projectile = hit.collider.GetComponentInParent<Projectile>();
                if (projectile != null)
                {
                    if (projectile.Owner == context.Faction)
                    {
                        continue;
                    }

                    beamEnd = hit.point;
                    projectile.Detonate();
                    struck = true;
                    break;
                }

                beamEnd = hit.point;

                Health health = FindHealth(hit.collider);
                if (health != null && health.Faction != context.Faction)
                {
                    health.TakeDamage(GetDamage(context), context.Faction);
                }

                SpellImpactReceiverUtility.TryNotify(hit.collider, hit.point, hit.normal, context.Faction);

                struck = true;
                break; // first non-self hit ends the beam, wall or target
            }

            SpawnBeam(context.Origin, beamEnd);
            PlayCastSound(context);
            if (struck)
            {
                PlayHitSound(beamEnd);
            }
        }

        private void SpawnBeam(Vector3 start, Vector3 end)
        {
            if (beam == null && !TryCreateBeam())
            {
                return;
            }

            beam.Flash(start, end, beamLifeSeconds);
        }

        private bool TryCreateBeam()
        {
            if (beamPrefab != null)
            {
                beam = Instantiate(beamPrefab);
                beam.name = $"{name} Beam";
                beam.SetTint(beamColor);
                return true;
            }

            if (beamMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    return false;
                }

                beamMaterial = new Material(shader); // one shared material per spell asset
            }

            GameObject beamObject = new GameObject($"{name} Beam");
            LineRenderer line = beamObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = beamWidth;
            line.endWidth = beamWidth;
            line.startColor = beamColor;
            line.endColor = beamColor;
            line.sharedMaterial = beamMaterial;
            line.enabled = false;

            beam = beamObject.AddComponent<HitscanBeam>();
            return true;
        }
    }
}
