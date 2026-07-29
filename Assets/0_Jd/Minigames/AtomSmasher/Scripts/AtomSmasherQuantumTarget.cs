using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AtomSmasherTarget))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Quantum Target")]
    public class AtomSmasherQuantumTarget : MonoBehaviour
    {
        [SerializeField] private string displayName = "Quantum";

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Quantum" : displayName;
    }
}
