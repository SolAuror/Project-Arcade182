using System;
using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// Aggregates every FungusLight under the machine so the rest of the game talks to
    /// one component instead of dozens of individual lights.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Light Bank")]
    public class FungusLightBank : MonoBehaviour
    {
        private FungusLight[] lights = Array.Empty<FungusLight>();

        public int TotalLights => lights.Length;
        public int LightsRemaining { get; private set; }
        public bool AllOut => TotalLights > 0 && LightsRemaining <= 0;

        /// <summary>Re-broadcast of any child light turning off.</summary>
        public event Action<FungusLight> AnyLightTurnedOff;

        /// <summary>Raised once when the last lit light goes out.</summary>
        public event Action AllLightsOut;

        private void Awake()
        {
            lights = GetComponentsInChildren<FungusLight>(true);
            LightsRemaining = 0;
            foreach (FungusLight boardLight in lights)
            {
                boardLight.TurnedOff += HandleLightTurnedOff;
                if (boardLight.IsLit)
                {
                    LightsRemaining++;
                }
            }
        }

        private void OnDestroy()
        {
            foreach (FungusLight boardLight in lights)
            {
                if (boardLight != null)
                {
                    boardLight.TurnedOff -= HandleLightTurnedOff;
                }
            }
        }

        /// <summary>Relights the whole board (replay / attract mode).</summary>
        public void ResetAll()
        {
            foreach (FungusLight boardLight in lights)
            {
                boardLight.ResetLight();
            }

            LightsRemaining = lights.Length;
        }

        private void HandleLightTurnedOff(FungusLight boardLight)
        {
            LightsRemaining = Mathf.Max(0, LightsRemaining - 1);
            AnyLightTurnedOff?.Invoke(boardLight);
            if (AllOut)
            {
                AllLightsOut?.Invoke();
            }
        }
    }
}
