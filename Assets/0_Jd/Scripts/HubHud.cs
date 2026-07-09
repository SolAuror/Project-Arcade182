using Sol.Minigames;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.Arcade
{
    /// <summary>
    /// Prefab-authored hub overlay: live ticket total plus a static controls
    /// panel (authored in the HubHud prefab). The crosshair stays on the
    /// existing UICanvas, where the player Controller manages its visibility.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Hub Hud")]
    public class HubHud : MonoBehaviour
    {
        [Header("Tickets")]
        [SerializeField] private Text ticketsText;

        [Tooltip("Found automatically from the tagged player when left empty.")]
        [SerializeField] private PlayerScoreCarrier scoreCarrier;

        private float nextCarrierSearchTime;

        private void Update()
        {
            if (scoreCarrier == null)
            {
                // The player can spawn a frame or two after the HUD; retry politely.
                if (Time.unscaledTime < nextCarrierSearchTime)
                {
                    return;
                }

                nextCarrierSearchTime = Time.unscaledTime + 0.5f;
                scoreCarrier = PlayerScoreCarrier.FindForPlayer();
                if (scoreCarrier == null)
                {
                    return;
                }
            }

            string value = $"TICKETS  {scoreCarrier.TotalTickets}";
            if (ticketsText != null && ticketsText.text != value)
            {
                ticketsText.text = value;
            }
        }
    }
}
