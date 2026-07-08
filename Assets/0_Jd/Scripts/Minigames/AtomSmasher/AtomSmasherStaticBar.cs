using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Static Bar")]
    public class AtomSmasherStaticBar : MonoBehaviour
    {
        [SerializeField] private float physicsPlaneZ;

        private void Awake()
        {
            LockToPlane();
        }

        public void Initialize(float planeZ)
        {
            physicsPlaneZ = planeZ;
            LockToPlane();
        }

        private void LockToPlane()
        {
            Vector3 position = transform.position;
            position.z = physicsPlaneZ;
            transform.position = position;
        }
    }
}
