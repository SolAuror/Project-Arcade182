using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text aiScoreText;
    [SerializeField] private TMP_Text gameOverText;

    public void UpdateScores(int playerScore, int aiScore)
    {
        playerScoreText.text = "Player: " + playerScore;
        aiScoreText.text = "AI: " + aiScore;
    }

    public void ShowGameOver(string message)
    {
        gameOverText.text = message;
        gameOverText.gameObject.SetActive(true);
    }

    public void HideGameOver()
    {
        gameOverText.gameObject.SetActive(false);
    }
}
