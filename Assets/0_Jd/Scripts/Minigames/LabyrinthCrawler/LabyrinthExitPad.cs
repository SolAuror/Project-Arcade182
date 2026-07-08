using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Stand-on exit zone in the end room. Clears the stage instantly when every
    /// enemy is dead, otherwise after an interruptible dwell. Uses a plain
    /// bounds check against the player, so it works regardless of collider or
    /// CharacterController trigger quirks.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Exit Pad")]
    public class LabyrinthExitPad : MonoBehaviour
    {
        [Header("Zone")]
        [Tooltip("Half extents of the stand-on zone in local space.")]
        [SerializeField] private Vector3 halfExtents = new Vector3(2f, 2f, 2f);

        [Header("Clear")]
        [Tooltip("Seconds the player must stand here while enemies are alive.")]
        [SerializeField, Min(0f)] private float clearDwellSeconds = 1.5f;

        private LabyrinthCrawlerGame game;
        private Transform player;
        private float dwell;
        private bool cleared;

        public bool PlayerInside { get; private set; }
        public float DwellProgress => clearDwellSeconds > 0f ? Mathf.Clamp01(dwell / clearDwellSeconds) : 1f;

        public void Initialize(LabyrinthCrawlerGame owningGame, Vector3 zoneHalfExtents, float dwellSeconds)
        {
            game = owningGame;
            halfExtents = zoneHalfExtents;
            clearDwellSeconds = Mathf.Max(0f, dwellSeconds);
            dwell = 0f;
            cleared = false;
        }

        private void Update()
        {
            if (cleared || game == null || !game.IsRunning || game.IsChoosingUpgrade)
            {
                return;
            }

            PlayerInside = IsPlayerInside();
            if (!PlayerInside)
            {
                dwell = 0f; // stepping off resets the channel
                return;
            }

            if (game.EnemiesRemaining == 0)
            {
                Clear();
                return;
            }

            dwell += Time.deltaTime;
            if (dwell >= clearDwellSeconds)
            {
                Clear();
            }
        }

        private bool IsPlayerInside()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                player = playerObject != null ? playerObject.transform : null;
            }

            if (player == null)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(player.position);
            return Mathf.Abs(local.x) <= halfExtents.x &&
                   local.y >= -0.5f && local.y <= halfExtents.y * 2f &&
                   Mathf.Abs(local.z) <= halfExtents.z;
        }

        private void Clear()
        {
            cleared = true;
            PlayerInside = false;
            dwell = 0f;
            game.CompleteEscape();
        }
    }
}
