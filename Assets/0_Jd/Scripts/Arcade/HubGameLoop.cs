using UnityEngine;

namespace Sol.Arcade
{
    /// <summary>
    /// Marks and protects the handcrafted arcade hub. Maze generation belongs
    /// exclusively to Labyrinth Crawler and is never permitted in this scene.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Hub Game Loop")]
    public class HubGameLoop : MonoBehaviour
    {
        private void Awake()
        {
            ArcadeGen3D[] generators = FindObjectsByType<ArcadeGen3D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ArcadeGen3D generator in generators)
            {
                if (generator == null)
                {
                    continue;
                }

                generator.enabled = false;
                Debug.LogError(
                    $"Disabled '{generator.name}' in the handcrafted arcade hub. " +
                    "Maze generation is owned exclusively by Labyrinth Crawler.",
                    generator);
            }
        }
    }
}
