using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class ScoreUI : MonoBehaviour
{
    [Header("Team Scores")]
    [FormerlySerializedAs("playerScoreText")]
    [SerializeField] private TMP_Text blueScoreText;
    [FormerlySerializedAs("aiScoreText")]
    [SerializeField] private TMP_Text redScoreText;
    [SerializeField] private TMP_Text greenScoreText;
    [SerializeField] private TMP_Text goldScoreText;
    [SerializeField] private TMP_Text gameOverText;

    [Header("AirFooty Presentation")]
    [SerializeField] private Color neutralColor = new Color(0.86f, 0.96f, 1f, 1f);
    [SerializeField, Min(1f)] private float scorePunchScale = 1.3f;
    [SerializeField, Min(0.1f)] private float punchDecay = 8f;

    private Vector3 blueBaseScale = Vector3.one;
    private Vector3 redBaseScale = Vector3.one;
    private Vector3 greenBaseScale = Vector3.one;
    private Vector3 goldBaseScale = Vector3.one;
    private Vector3 statusBaseScale = Vector3.one;
    private int previousBlueScore = -1;
    private int previousRedScore = -1;
    private int previousGreenScore = -1;
    private int previousGoldScore = -1;
    private bool gameplayHudVisible;
    private bool fourTeamLayout;
    private bool resultVisible;

    private void Awake()
    {
        ConfigureScoreText(blueScoreText, AirFootyTeam.Blue, ref blueBaseScale);
        ConfigureScoreText(redScoreText, AirFootyTeam.Red, ref redBaseScale);
        ConfigureScoreText(greenScoreText, AirFootyTeam.Green, ref greenBaseScale);
        ConfigureScoreText(goldScoreText, AirFootyTeam.Gold, ref goldBaseScale);

        if (gameOverText != null)
        {
            statusBaseScale = gameOverText.transform.localScale;
        }
    }

    private void Update()
    {
        float interpolation = 1f - Mathf.Exp(-punchDecay * Time.unscaledDeltaTime);
        AnimateScoreText(blueScoreText, blueBaseScale, interpolation);
        AnimateScoreText(redScoreText, redBaseScale, interpolation);
        AnimateScoreText(greenScoreText, greenBaseScale, interpolation);
        AnimateScoreText(goldScoreText, goldBaseScale, interpolation);

        if (gameOverText != null)
        {
            float breathe = resultVisible
                ? 1f
                : 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.025f;
            gameOverText.transform.localScale = Vector3.Lerp(
                gameOverText.transform.localScale,
                statusBaseScale * breathe,
                interpolation);
        }
    }

    public void SetGameplayHudVisible(bool visible)
    {
        gameplayHudVisible = visible;
        ApplyScoreVisibility();

        resultVisible = false;
        SetTextVisible(gameOverText, false);
    }

    public void UpdateScores(int playerScore, int aiScore)
    {
        UpdateHeadToHeadScores(
            playerScore,
            aiScore,
            AirFootyTeam.Blue,
            AirFootyTeam.Red);
    }

    public void UpdateHeadToHeadScores(
        int humanScore,
        int opponentScore,
        AirFootyTeam humanTeam,
        AirFootyTeam opponentTeam)
    {
        fourTeamLayout = false;
        ApplyScoreVisibility();
        SetTeamScore(humanTeam, humanScore, -1);
        SetTeamScore(opponentTeam, opponentScore, -1);
    }

    public void UpdateEliminationScores(
        int blue,
        int red,
        int green,
        int gold,
        int eliminationScore)
    {
        fourTeamLayout = true;
        ApplyScoreVisibility();
        SetTeamScore(AirFootyTeam.Blue, blue, eliminationScore);
        SetTeamScore(AirFootyTeam.Red, red, eliminationScore);
        SetTeamScore(AirFootyTeam.Green, green, eliminationScore);
        SetTeamScore(AirFootyTeam.Gold, gold, eliminationScore);
    }

    public void ShowGameOver(string message)
    {
        if (gameOverText == null)
        {
            return;
        }

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

    public void ShowMatchStatus(
        string message,
        Color? color = null,
        bool punch = false)
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

    private void SetTeamScore(
        AirFootyTeam team,
        int score,
        int eliminationScore)
    {
        TMP_Text text = ScoreTextFor(team);
        if (text == null)
        {
            return;
        }

        string teamName = AirFootyTeamMember3D.DisplayName(team);
        string value = eliminationScore > 0
            ? $"{score:D2}/{eliminationScore:D2}"
            : $"{score:D2}";
        bool scoreFirst =
            team == AirFootyTeam.Red || team == AirFootyTeam.Gold;
        text.text = scoreFirst
            ? $"{value}  {teamName}"
            : $"{teamName}  {value}";
        text.color = AirFootyTeamMember3D.ColorFor(team);

        int previous = PreviousScoreFor(team);
        if (previous >= 0 && score > previous)
        {
            text.transform.localScale =
                BaseScaleFor(team) * scorePunchScale;
        }

        SetPreviousScore(team, score);
    }

    private void ApplyScoreVisibility()
    {
        SetTextVisible(blueScoreText, gameplayHudVisible);
        SetTextVisible(redScoreText, gameplayHudVisible);
        SetTextVisible(
            greenScoreText,
            gameplayHudVisible && fourTeamLayout);
        SetTextVisible(
            goldScoreText,
            gameplayHudVisible && fourTeamLayout);
    }

    private TMP_Text ScoreTextFor(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Blue => blueScoreText,
            AirFootyTeam.Red => redScoreText,
            AirFootyTeam.Green => greenScoreText,
            AirFootyTeam.Gold => goldScoreText,
            _ => null
        };
    }

    private Vector3 BaseScaleFor(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Blue => blueBaseScale,
            AirFootyTeam.Red => redBaseScale,
            AirFootyTeam.Green => greenBaseScale,
            AirFootyTeam.Gold => goldBaseScale,
            _ => Vector3.one
        };
    }

    private int PreviousScoreFor(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Blue => previousBlueScore,
            AirFootyTeam.Red => previousRedScore,
            AirFootyTeam.Green => previousGreenScore,
            AirFootyTeam.Gold => previousGoldScore,
            _ => -1
        };
    }

    private void SetPreviousScore(AirFootyTeam team, int score)
    {
        switch (team)
        {
            case AirFootyTeam.Blue:
                previousBlueScore = score;
                break;
            case AirFootyTeam.Red:
                previousRedScore = score;
                break;
            case AirFootyTeam.Green:
                previousGreenScore = score;
                break;
            case AirFootyTeam.Gold:
                previousGoldScore = score;
                break;
        }
    }

    private static void ConfigureScoreText(
        TMP_Text text,
        AirFootyTeam team,
        ref Vector3 baseScale)
    {
        if (text == null)
        {
            return;
        }

        baseScale = text.transform.localScale;
        text.color = AirFootyTeamMember3D.ColorFor(team);
        text.raycastTarget = false;
    }

    private static void AnimateScoreText(
        TMP_Text text,
        Vector3 baseScale,
        float interpolation)
    {
        if (text == null)
        {
            return;
        }

        text.transform.localScale = Vector3.Lerp(
            text.transform.localScale,
            baseScale,
            interpolation);
    }

    private static void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text != null && text.gameObject.activeSelf != visible)
        {
            text.gameObject.SetActive(visible);
        }
    }
}
