using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Sol/Minigames/Hoops Throwable")]
    public class HoopsThrowable : MonoBehaviour
    {
        [SerializeField] private float scoreCooldownSeconds = 0.5f;
        [SerializeField] private float resetBelowY = -10f;
        [SerializeField] private bool resetWhenFallen = true;

        private Rigidbody rb;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float lastScoreTime = -999f;

        public bool CanScore => Time.time - lastScoreTime >= scoreCooldownSeconds;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (resetWhenFallen && transform.position.y < resetBelowY)
            {
                ResetToSpawn();
            }
        }

        private void OnValidate()
        {
            scoreCooldownSeconds = Mathf.Max(0f, scoreCooldownSeconds);
        }

        public void MarkScored()
        {
            lastScoreTime = Time.time;
        }

        public void ResetToSpawn()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
    }
}
