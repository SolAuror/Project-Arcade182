using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text aiScoreText;
    [SerializeField] private TMP_Text gameOverText;

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

    public void SetGameplayHudVisible(bool visible)
    {
        SetTextVisible(playerScoreText, visible);
        SetTextVisible(aiScoreText, visible);

        resultVisible = false;
        SetTextVisible(gameOverText, false);
    }

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

    public void UpdateHeadToHeadScores(
        int humanScore,
        int opponentScore,
        AirFootyTeam humanTeam,
        AirFootyTeam opponentTeam)
    {
        string humanName = AirFootyTeamMember3D.DisplayName(humanTeam);
        string opponentName = AirFootyTeamMember3D.DisplayName(opponentTeam);
        if (playerScoreText != null)
        {
            playerScoreText.text = $"{humanName}  {humanScore:D2}";
            playerScoreText.color = AirFootyTeamMember3D.ColorFor(humanTeam);
        }
        if (aiScoreText != null)
        {
            aiScoreText.text = $"{opponentScore:D2}  {opponentName}";
            aiScoreText.color = AirFootyTeamMember3D.ColorFor(opponentTeam);
        }

        previousPlayerScore = humanScore;
        previousAiScore = opponentScore;
    }

    public void UpdateEliminationScores(
        int blue,
        int red,
        int green,
        int gold,
        int eliminationScore)
    {
        if (playerScoreText != null)
        {
            playerScoreText.color = Color.white;
            playerScoreText.text =
                TeamScoreLine(AirFootyTeam.Blue, blue, eliminationScore, false) + "\n" +
                TeamScoreLine(AirFootyTeam.Green, green, eliminationScore, false);
        }

        if (aiScoreText != null)
        {
            aiScoreText.color = Color.white;
            aiScoreText.text =
                TeamScoreLine(AirFootyTeam.Red, red, eliminationScore, true) + "\n" +
                TeamScoreLine(AirFootyTeam.Gold, gold, eliminationScore, true);
        }
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

    private static void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text != null && text.gameObject.activeSelf != visible)
        {
            text.gameObject.SetActive(visible);
        }
    }

    private static string TeamScoreLine(
        AirFootyTeam team,
        int score,
        int eliminationScore,
        bool scoreFirst)
    {
        string color = ColorUtility.ToHtmlStringRGB(
            AirFootyTeamMember3D.ColorFor(team));
        string teamName = AirFootyTeamMember3D.DisplayName(team);
        string value = $"{score:D2}/{eliminationScore:D2}";
        string line = scoreFirst
            ? $"{value}  {teamName}"
            : $"{teamName}  {value}";
        return $"<color=#{color}>{line}</color>";
    }
}
