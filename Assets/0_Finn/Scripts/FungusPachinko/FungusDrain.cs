using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// Trigger volume along the bottom of the board that retires any ball reaching it.
    /// A physical drain keeps the machine self-contained: the rig keeps working at any
    /// world position or orientation, unlike a world-space height check would.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Drain")]
    [RequireComponent(typeof(Collider))]
    public class FungusDrain : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            FungusBall ball = other.GetComponentInParent<FungusBall>();
            if (ball != null)
            {
                ball.Finish();
            }
        }
    }
}
