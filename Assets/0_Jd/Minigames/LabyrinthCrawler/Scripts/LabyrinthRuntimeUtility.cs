using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sol.Minigames
{
    /// <summary>
    /// Small, allocation-free helpers shared by the crawler's runtime components.
    /// Keeping these mechanics here prevents trigger, tag, and shuffle behavior
    /// from drifting between pickups, exits, secrets, and reward drafts.
    /// </summary>
    internal static class LabyrinthRuntimeUtility
    {
        public static bool IsTaggedCollider(Collider collider, string tag)
        {
            return collider != null &&
                   (collider.CompareTag(tag) || collider.transform.root.CompareTag(tag));
        }

        public static void EnsureSphereTrigger(GameObject owner, float radius)
        {
            foreach (Collider collider in owner.GetComponents<Collider>())
            {
                if (collider != null && collider.isTrigger)
                {
                    return;
                }
            }

            SphereCollider trigger = owner.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Max(0.05f, radius);
            Debug.LogWarning(
                $"{owner.name} was missing its authored trigger; a recovery SphereCollider was added at runtime.",
                owner);
        }

        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
