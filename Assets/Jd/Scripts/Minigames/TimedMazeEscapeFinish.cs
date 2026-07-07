using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Timed Maze Escape Finish")]
    public class TimedMazeEscapeFinish : MonoBehaviour
    {
        [SerializeField] private TimedMazeEscapeGame game;
        [SerializeField] private LayerMask playerLayers = Physics.DefaultRaycastLayers;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<TimedMazeEscapeGame>();
            }
        }

        public void AssignGame(TimedMazeEscapeGame assignedGame)
        {
            game = assignedGame;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAcceptedPlayer(other))
            {
                return;
            }

            game?.CompleteEscape();
        }

        private bool IsAcceptedPlayer(Collider other)
        {
            if ((playerLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return false;
            }

            return other.GetComponentInParent<CharacterController>() != null ||
                   other.attachedRigidbody != null ||
                   other.CompareTag("Player");
        }
    }
}
