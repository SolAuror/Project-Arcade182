using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;

    private int leftScore;
    private int rightScore;

    private void Start()
    {
        UpdateScoreText();
    }

    public void AddLeftScore()
    {
        leftScore++;
        UpdateScoreText();
    }

    public void AddRightScore()
    {
        rightScore++;
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        scoreText.text = leftScore + "  -  " + rightScore;
    }
}

