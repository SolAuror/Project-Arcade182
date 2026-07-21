using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Constant local rotation for authored dressing pieces (orbit rings,
    /// hazard bands). Purely cosmetic; runs on scaled time so slow-mo beats
    /// slow the ornaments with the rest of the board.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher/Spin")]
    public class AtomSmasherSpin : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 45f, 0f);

        public Vector3 DegreesPerSecond
        {
            get => degreesPerSecond;
            set => degreesPerSecond = value;
        }

        private void Update()
        {
            transform.localRotation *= Quaternion.Euler(degreesPerSecond * Time.deltaTime);
        }
    }
}
