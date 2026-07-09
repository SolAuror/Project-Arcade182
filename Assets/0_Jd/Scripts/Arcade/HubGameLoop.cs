using UnityEngine;

namespace Sol.Arcade
{
    /// <summary>
    /// Overarching hub loop, spawned automatically each time the hub scene
    /// loads (see <see cref="ArcadeMetaBootstrap"/>): regenerates the arcade
    /// maze so every return from a minigame gets a fresh layout. The golden
    /// exit door is authored in a room prefab; the exit clerk sells the coin
    /// it redeems.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Hub Game Loop")]
    public class HubGameLoop : MonoBehaviour
    {
        [Tooltip("Regenerate the hub maze every time the scene loads.")]
        [SerializeField] private bool regenerateMazeOnLoad = true;

        private void Start()
        {
            ArcadeGen3D generator = FindFirstObjectByType<ArcadeGen3D>();
            if (generator == null)
            {
                Debug.LogWarning($"{name} found no ArcadeGen3D in the hub scene.", this);
                return;
            }

            if (regenerateMazeOnLoad)
            {
                generator.CreateArcade();
            }
        }
    }
}
