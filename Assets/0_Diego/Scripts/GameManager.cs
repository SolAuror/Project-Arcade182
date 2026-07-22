using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private BallController ball;
    [SerializeField] private ScoreUI scoreUI;
    [SerializeField] private GoalZone playerGoal;
    [SerializeField] private GoalZone aiGoal;
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
        if (gameOver && Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public bool GoalScored(GoalZone.ScoringSide scoringSide)
    {
        if (goalBeingProcessed || gameOver) return false;

        goalBeingProcessed = true;
        ball.StopBall();

        if (scoringSide == GoalZone.ScoringSide.Player) playerScore++;
        else aiScore++;

        scoreUI.UpdateScores(playerScore, aiScore);

        if (playerScore >= scoreNeededToWin || aiScore >= scoreNeededToWin)
        {
            FinishGame();
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

    private void FinishGame()
    {
        gameOver = true;
        string result = playerScore >= scoreNeededToWin ? "Player Wins!" : "AI Wins!";
        scoreUI.ShowGameOver(result + "\nPress Space to Restart");
    }
}
