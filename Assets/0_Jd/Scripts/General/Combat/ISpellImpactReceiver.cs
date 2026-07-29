using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Implemented by surfaces that react to spell impacts without taking
    /// damage (e.g. the illusory wall's ripple). The shared spell kit notifies
    /// the receiver found on the struck collider's parents regardless of
    /// faction, so testing a wall works for player and enemy fire alike.
    /// </summary>
    public interface ISpellImpactReceiver
    {
        /// <param name="point">World-space impact point.</param>
        /// <param name="normal">Surface normal at the impact (best effort).</param>
        /// <param name="faction">Faction the spell belonged to.</param>
        void OnSpellImpact(Vector3 point, Vector3 normal, Faction faction);
    }

    internal static class SpellImpactReceiverUtility
    {
        private const float NearbyReceiverRadius = 0.45f;

        public static bool TryNotify(Component hit, Vector3 point, Vector3 normal, Faction faction)
        {
            ISpellImpactReceiver receiver = Find(hit);
            if (receiver == null)
            {
                receiver = FindNearImpact(point);
            }
            if (receiver == null)
            {
                return false;
            }

            receiver.OnSpellImpact(point, normal, faction);
            return true;
        }

        public static ISpellImpactReceiver Find(Component hit)
        {
            if (hit == null)
            {
                return null;
            }

            ISpellImpactReceiver receiver = hit.GetComponentInParent<ISpellImpactReceiver>();
            if (receiver != null)
            {
                return receiver;
            }

            foreach (MonoBehaviour behaviour in hit.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is ISpellImpactReceiver impactReceiver)
                {
                    return impactReceiver;
                }
            }

            return null;
        }

        private static ISpellImpactReceiver FindNearImpact(Vector3 point)
        {
            Collider[] nearby = Physics.OverlapSphere(
                point,
                NearbyReceiverRadius,
                ~0,
                QueryTriggerInteraction.Collide);
            ISpellImpactReceiver nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider candidate in nearby)
            {
                ISpellImpactReceiver receiver = Find(candidate);
                if (receiver == null)
                {
                    continue;
                }

                Vector3 closest = candidate.ClosestPoint(point);
                float distance = (closest - point).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = receiver;
                }
            }
            return nearest;
        }
    }
}
