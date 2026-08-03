using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Activates the maze exit clerk attached to a still-closed end-room wall.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Room3D))]
    [AddComponentMenu("Sol/Maze/End Room Exit Clerk Activator")]
    public class EndRoomExitClerkActivator : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("Name of the direct child target under each wall.")]
        [SerializeField] private string exitSpawnTargetName = "exitSpawnTarget";

        [Header("Placement")]
        [Tooltip("First wall to try when it is still closed after maze generation.")]
        [SerializeField] private Room3D.Directions preferredExitWall = Room3D.Directions.NORTH;

        private static readonly Room3D.Directions[] FallbackDirections =
        {
            Room3D.Directions.NORTH,
            Room3D.Directions.EAST,
            Room3D.Directions.SOUTH,
            Room3D.Directions.WEST,
        };

        private const string ClerkRootName = "clerksDesk";

        private Room3D room;

        public void PrepareClerksForGeneration()
        {
            SetAllClerksActive(false);
        }

        public void ActivateClerkOnClosedWall()
        {
            SetAllClerksActive(false);

            if (TryActivateClerk(preferredExitWall))
            {
                return;
            }

            foreach (Room3D.Directions direction in FallbackDirections)
            {
                if (direction == preferredExitWall)
                {
                    continue;
                }

                if (TryActivateClerk(direction))
                {
                    return;
                }
            }

            Debug.LogWarning($"{name} could not find a closed wall with an active-ready clerk.", this);
        }

        private void Awake()
        {
            room = GetComponent<Room3D>();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(exitSpawnTargetName))
            {
                exitSpawnTargetName = "exitSpawnTarget";
            }
        }

        private bool TryActivateClerk(Room3D.Directions direction)
        {
            if (direction == Room3D.Directions.NONE ||
                !TryGetClerkForDirection(direction, out Transform clerkRoot) ||
                !room.IsWallClosed(direction))
            {
                return false;
            }

            clerkRoot.gameObject.SetActive(true);
            return true;
        }

        private void SetAllClerksActive(bool active)
        {
            foreach (Room3D.Directions direction in FallbackDirections)
            {
                if (TryGetClerkForDirection(direction, out Transform clerkRoot))
                {
                    clerkRoot.gameObject.SetActive(active);
                }
            }
        }

        private bool TryGetClerkForDirection(Room3D.Directions direction, out Transform clerkRoot)
        {
            clerkRoot = null;

            if (direction == Room3D.Directions.NONE)
            {
                return false;
            }

            if (room == null)
            {
                room = GetComponent<Room3D>();
            }

            if (room == null)
            {
                return false;
            }

            if (!room.TryGetWallTransform(direction, out Transform wallTransform))
            {
                return false;
            }

            Transform exitSpawnTarget = wallTransform.Find(exitSpawnTargetName);
            if (exitSpawnTarget == null)
            {
                return false;
            }

            clerkRoot = exitSpawnTarget.Find(ClerkRootName);
            return clerkRoot != null;
        }
    }
}
