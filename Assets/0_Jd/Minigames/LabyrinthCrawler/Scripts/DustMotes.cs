using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Carries the authored ambient dust ParticleSystem along with the camera
    /// so its world-space motes always surround the player. The system itself
    /// is authored on this GameObject inside LabyrinthCrawlerGame.prefab (see
    /// Editor/LabyrinthDustAuthoring.cs) - nothing is generated at runtime;
    /// this component only moves the emitter.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Dust Motes")]
    public class DustMotes : MonoBehaviour
    {
        [Tooltip("Authored dust system to carry along. Auto-found on this object when unset.")]
        [SerializeField] private ParticleSystem motes;

        private Transform followCamera;

        private void Awake()
        {
            if (motes == null)
            {
                motes = GetComponentInChildren<ParticleSystem>(true);
            }
        }

        private void LateUpdate()
        {
            if (followCamera == null)
            {
                Camera mainCamera = Camera.main; // player rig spawns late; resolve lazily
                followCamera = mainCamera != null ? mainCamera.transform : null;
            }

            if (followCamera != null)
            {
                transform.position = followCamera.position;
            }
        }
    }
}
