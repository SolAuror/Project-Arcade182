using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Marks any rigidbody prop as throwable-for-points: its value replaces the
    /// hoop's base points when it sails through a score zone. Attach to cans,
    /// cubes, or anything else lying around the court.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Hoops Scorable")]
    public class HoopsScorable : MonoBehaviour
    {
        [Tooltip("Points scored when this object goes through a hoop (replaces the hoop's base points).")]
        [SerializeField, Min(1)] private int points = 2;

        [SerializeField, Min(0f)] private float scoreCooldownSeconds = 0.5f;

        private Rigidbody rb;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float lastScoreTime = -999f;

        public int Points => points;
        public bool CanScore => Time.time - lastScoreTime >= scoreCooldownSeconds;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        public void MarkScored()
        {
            lastScoreTime = Time.time;
        }

        public void ResetToSpawn()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
    }
}
