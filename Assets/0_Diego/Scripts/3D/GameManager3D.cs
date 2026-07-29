using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager3D : MonoBehaviour
{
    [SerializeField] private BallController3D ball;
    [SerializeField] private ScoreUI scoreUI;
    [SerializeField] private GoalZone3D playerGoal;
    [SerializeField] private GoalZone3D aiGoal;
    [SerializeField] private int scoreNeededToWin = 5;
    [SerializeField] private float resetDelay = 1.5f;

    private int playerScore;
    private int aiScore;
    private bool goalBeingProcessed;
    private bool gameOver;

    private void Start()
    {
        scoreUI.UpdateScores(playerScore, aiScore);
        scoreUI.HideGameOver();
    }

    private void Update()
    {
        if (gameOver && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public bool GoalScored(GoalZone3D.ScoringSide scoringSide)
    {
        if (goalBeingProcessed || gameOver) return false;

        goalBeingProcessed = true;
        ball.StopBall();

        if (scoringSide == GoalZone3D.ScoringSide.Player) playerScore++;
        else aiScore++;

        scoreUI.UpdateScores(playerScore, aiScore);

        if (playerScore >= scoreNeededToWin || aiScore >= scoreNeededToWin)
        {
            gameOver = true;
            string result = playerScore >= scoreNeededToWin ? "Player Wins!" : "AI Wins!";
            scoreUI.ShowGameOver(result + "\nPress Space to Restart");
        }
        else
        {
            StartCoroutine(ResetAfterGoal());
        }

        return true;
    }

    private IEnumerator ResetAfterGoal()
    {
        yield return new WaitForSeconds(resetDelay);
        ball.ResetBall();
        playerGoal.AllowGoal();
        aiGoal.AllowGoal();
        goalBeingProcessed = false;
    }
}
