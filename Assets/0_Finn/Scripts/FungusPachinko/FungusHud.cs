using UnityEngine;
using UnityEngine.UI;

namespace Finn.Minigames
{
    /// <summary>
    /// Display-only HUD for Fungus Pachinko. Subscribes to controller and light-bank
    /// events and rewrites its labels when told to — nothing here polls per frame.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Hud")]
    public class FungusHud : MonoBehaviour
    {
        [SerializeField] private FungusGameController controller;
        [SerializeField] private FungusLightBank lightBank;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text ballsText;
        [SerializeField] private Text lightsText;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.ScoreChanged += HandleScoreChanged;
                controller.BallsChanged += HandleBallsChanged;
                controller.GameEnded += HandleGameEnded;
            }

            if (lightBank != null)
            {
                lightBank.AnyLightTurnedOff += HandleLightTurnedOff;
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            RefreshAll();
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.ScoreChanged -= HandleScoreChanged;
                controller.BallsChanged -= HandleBallsChanged;
                controller.GameEnded -= HandleGameEnded;
            }

            if (lightBank != null)
            {
                lightBank.AnyLightTurnedOff -= HandleLightTurnedOff;
            }
        }

        private void RefreshAll()
        {
            if (controller != null)
            {
                HandleScoreChanged(controller.Score);
                HandleBallsChanged(controller.BallsRemaining);
            }

            RefreshLights();

            if (statusText != null)
            {
                statusText.text = "A/D to aim - SPACE to drop";
            }
        }

        private void HandleScoreChanged(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {score}";
            }
        }

        private void HandleBallsChanged(int balls)
        {
            if (ballsText != null)
            {
                ballsText.text = $"Balls: {balls}";
            }
        }

        private void HandleLightTurnedOff(FungusLight boardLight)
        {
            RefreshLights();
        }

        private void RefreshLights()
        {
            if (lightsText != null && lightBank != null)
            {
                lightsText.text = $"Lights: {lightBank.LightsRemaining}/{lightBank.TotalLights}";
            }
        }

        private void HandleGameEnded(FungusGameResult result)
        {
            if (statusText != null)
            {
                statusText.text = "Game over!";
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            if (resultText != null)
            {
                string bonusLine = result.AllLightsOut ? "\nALL LIGHTS OUT! +50 BONUS" : string.Empty;
                resultText.text = $"Score: {result.FinalScore}\nTickets: {result.TicketsAwarded}{bonusLine}";
            }
        }
    }
}
