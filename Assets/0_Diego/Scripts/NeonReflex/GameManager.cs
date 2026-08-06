using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace NeonReflex
{
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private TargetSpawner targetSpawner;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private int startingLives = 3;

        private int score;
        private int currentLevel = 1;
        private int lives;
        private int resolvedTargets;
        private int targetsInLevel;
        private bool levelRunning;
        private bool gameOver;
        private bool gameStarted;

        private void Awake()
        {
            if (targetSpawner == null || !targetSpawner.HasRequiredReferences ||
                uiManager == null || !uiManager.HasRequiredReferences)
            {
                Debug.LogError(
                    $"{name} requires authored TargetSpawner and UIManager references " +
                    "whose own production references are complete. Check the authored " +
                    "Neon Reflex scene and target prefab.",
                    this);
                enabled = false;
            }
        }

        private void Start()
        {
            lives = startingLives;
            uiManager.UpdateScore(score);
            uiManager.UpdateLives(lives);
            uiManager.UpdateLevel(currentLevel);
            uiManager.ShowStartMenu();
        }

        public void StartGame()
        {
            if (!isActiveAndEnabled || gameStarted) return;
            gameStarted = true;
            StartLevel();
        }

        private void Update()
        {
            bool restartPressed =
                Keyboard.current?.spaceKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
            if (gameOver && restartPressed)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void StartLevel()
        {
            resolvedTargets = 0;
            targetsInLevel = 10;
            levelRunning = true;
            uiManager.HideMessages();
            uiManager.UpdateLevel(currentLevel);
            targetSpawner.StartLevel(currentLevel, targetsInLevel);
        }

        public void TargetHit(bool isFake)
        {
            if (!levelRunning) return;

            if (isFake)
            {
                LoseLife();
            }
            else
            {
                score++;
                uiManager.UpdateScore(score);
            }

            TargetFinished();
        }

        public void TargetExpired(bool isFake)
        {
            if (!levelRunning) return;

            // Fake targets are meant to be ignored.
            if (!isFake) LoseLife();
            TargetFinished();
        }

        private void TargetFinished()
        {
            resolvedTargets++;

            if (gameOver) return;
            if (resolvedTargets >= targetsInLevel) StartCoroutine(CompleteLevel());
        }

        private void LoseLife()
        {
            lives--;
            uiManager.UpdateLives(lives);

            if (lives <= 0) EndGame();
        }

        private IEnumerator CompleteLevel()
        {
            levelRunning = false;
            targetSpawner.StopSpawning();
            uiManager.ShowLevelComplete();
            yield return new WaitForSeconds(2f);

            if (currentLevel >= 5)
            {
                uiManager.ShowGameComplete();
                gameOver = true;
            }
            else
            {
                currentLevel++;
                StartLevel();
            }
        }

        private void EndGame()
        {
            gameOver = true;
            levelRunning = false;
            targetSpawner.StopSpawning();
            uiManager.ShowGameOver();
        }
    }
}
