using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Spins an obstruction around the board plane's Z axis. Put it on the root
    /// of any obstruction prefab to make a rotating hazard.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Rotator")]
    public class AtomSmasherRotator : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 45f;

        [Tooltip("Randomly flip spin direction when spawned.")]
        [SerializeField] private bool randomizeDirection = true;

        private float direction = 1f;

        /// <summary>Spin rate in deg/s; cluster variants retune this at spawn.</summary>
        public float DegreesPerSecond
        {
            get => degreesPerSecond;
            set => degreesPerSecond = value;
        }

        private void OnEnable()
        {
            direction = randomizeDirection && Random.value < 0.5f ? -1f : 1f;
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * direction * Time.deltaTime, Space.Self);
        }
    }
}
