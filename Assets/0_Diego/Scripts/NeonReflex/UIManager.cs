using Sol.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NeonReflex
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text livesText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject instructionsPanel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button instructionsButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameManager gameManager;

        private void Start()
        {
            SimpleUiBuilder.EnsureEventSystem();
            startButton.onClick.AddListener(StartGame);
            instructionsButton.onClick.AddListener(ShowInstructions);
            backButton.onClick.AddListener(ShowStartMenu);
            ShowStartMenu();
        }

        private void Update()
        {
            if (instructionsPanel.activeInHierarchy &&
                (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                 Gamepad.current?.buttonEast.wasPressedThisFrame == true))
            {
                ShowStartMenu();
            }
        }

        private void StartGame()
        {
            startPanel.SetActive(false);
            instructionsPanel.SetActive(false);
            ArcadeInputCoordinator.EnterGameplay(
                CursorLockMode.Confined,
                true);
            gameManager.StartGame();
        }

        public void ShowStartMenu()
        {
            startPanel.SetActive(true);
            instructionsPanel.SetActive(false);
            ArcadeInputCoordinator.ShowMenu(startPanel, startButton);
        }

        public void ShowInstructions()
        {
            startPanel.SetActive(false);
            instructionsPanel.SetActive(true);
            ArcadeInputCoordinator.SetMenuFocus(
                instructionsPanel,
                backButton);
        }

        public void UpdateScore(int score)
        {
            scoreText.text = "SCORE: " + score;
        }

        public void UpdateLevel(int level)
        {
            levelText.text = "LEVEL: " + level;
        }

        public void UpdateLives(int lives)
        {
            livesText.text = "LIVES: " + lives;
        }

        public void HideMessages()
        {
            messageText.gameObject.SetActive(false);
        }

        public void ShowLevelComplete()
        {
            ShowMessage("LEVEL COMPLETE");
        }

        public void ShowGameOver()
        {
            ShowMessage("GAME OVER\nPRESS SPACE TO RESTART");
        }

        public void ShowGameComplete()
        {
            ShowMessage("ALL LEVELS COMPLETE!\nPRESS SPACE TO RESTART");
        }

        private void ShowMessage(string message)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
        }
    }
}
