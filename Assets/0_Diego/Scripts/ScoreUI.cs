using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text aiScoreText;
    [SerializeField] private TMP_Text gameOverText;
    [SerializeField] private TMP_Text actionPromptText;

    [Header("AirFooty Presentation")]
    [SerializeField] private Color playerColor = new Color(0.12f, 0.62f, 1f, 1f);
    [SerializeField] private Color aiColor = new Color(1f, 0.18f, 0.25f, 1f);
    [SerializeField] private Color neutralColor = new Color(0.86f, 0.96f, 1f, 1f);
    [SerializeField, Min(1f)] private float scorePunchScale = 1.3f;
    [SerializeField, Min(0.1f)] private float punchDecay = 8f;

    private Vector3 playerBaseScale = Vector3.one;
    private Vector3 aiBaseScale = Vector3.one;
    private Vector3 statusBaseScale = Vector3.one;
    private int previousPlayerScore = -1;
    private int previousAiScore = -1;
    private bool resultVisible;

    private void Awake()
    {
        if (playerScoreText != null)
        {
            playerBaseScale = playerScoreText.transform.localScale;
            playerScoreText.color = playerColor;
        }
        if (aiScoreText != null)
        {
            aiBaseScale = aiScoreText.transform.localScale;
            aiScoreText.color = aiColor;
        }
        if (gameOverText != null)
        {
            statusBaseScale = gameOverText.transform.localScale;
        }
    }

    private void Update()
    {
        float interpolation = 1f - Mathf.Exp(-punchDecay * Time.unscaledDeltaTime);
        if (playerScoreText != null)
        {
            playerScoreText.transform.localScale = Vector3.Lerp(
                playerScoreText.transform.localScale,
                playerBaseScale,
                interpolation);
        }
        if (aiScoreText != null)
        {
            aiScoreText.transform.localScale = Vector3.Lerp(
                aiScoreText.transform.localScale,
                aiBaseScale,
                interpolation);
        }
        if (gameOverText != null)
        {
            float breathe = resultVisible ? 1f : 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.025f;
            gameOverText.transform.localScale = Vector3.Lerp(
                gameOverText.transform.localScale,
                statusBaseScale * breathe,
                interpolation);
        }
    }

    public void UpdateScores(int playerScore, int aiScore)
    {
        if (playerScoreText != null)
        {
            playerScoreText.text = $"BLUE  {playerScore:D2}";
            if (previousPlayerScore >= 0 && playerScore > previousPlayerScore)
            {
                playerScoreText.transform.localScale = playerBaseScale * scorePunchScale;
            }
        }

        if (aiScoreText != null)
        {
            aiScoreText.text = $"{aiScore:D2}  RED";
            if (previousAiScore >= 0 && aiScore > previousAiScore)
            {
                aiScoreText.transform.localScale = aiBaseScale * scorePunchScale;
            }
        }

        previousPlayerScore = playerScore;
        previousAiScore = aiScore;
    }

    public void ShowGameOver(string message)
    {
        if (gameOverText == null) return;

        resultVisible = true;
        gameOverText.text = message;
        gameOverText.color = neutralColor;
        gameOverText.transform.localScale = statusBaseScale * 1.12f;
        gameOverText.gameObject.SetActive(true);
    }

    public void HideGameOver()
    {
        resultVisible = false;
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }
    }

    public void ShowMatchStatus(string message, Color? color = null, bool punch = false)
    {
        if (gameOverText == null || resultVisible)
        {
            return;
        }

        gameOverText.text = message;
        gameOverText.color = color ?? neutralColor;
        gameOverText.gameObject.SetActive(true);
        if (punch)
        {
            gameOverText.transform.localScale = statusBaseScale * 1.2f;
        }
    }

    public void HideMatchStatus()
    {
        if (!resultVisible && gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }
    }

    public void ShowActionPrompts()
    {
        ResolveActionPromptText();
        if (actionPromptText == null)
        {
            return;
        }

        actionPromptText.text =
            "MOVE — WASD / LEFT STICK\n" +
            "HOLD + RELEASE KICK — SPACE / A\n" +
            "FIRST TO 5 • STAY ON YOUR HALF";
    }

    private void ResolveActionPromptText()
    {
        if (actionPromptText != null)
        {
            return;
        }

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (text.name == "Instructions Text")
            {
                actionPromptText = text;
                return;
            }
        }
    }
}
