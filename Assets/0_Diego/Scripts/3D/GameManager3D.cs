using System.Collections;
using Sol.Minigames;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameManager3D : MonoBehaviour
{
    private const string GameTitle = "AirFooty";

    [Header("Game")]
    [SerializeField] private BallController3D ball;
    [SerializeField] private ScoreUI scoreUI;
    [SerializeField] private GoalZone3D playerGoal;
    [SerializeField] private GoalZone3D aiGoal;
    [SerializeField] private int scoreNeededToWin = 5;
    [SerializeField] private float resetDelay = 1.5f;

    [Header("Kick-Off")]
    [SerializeField, Min(0f)] private float openingBannerSeconds = 0.65f;
    [SerializeField, Range(1, 3)] private int countdownFrom = 3;
    [SerializeField, Min(0.1f)] private float countdownStepSeconds = 0.5f;
    [SerializeField, Min(0f)] private float kickoffBannerSeconds = 0.45f;
    [SerializeField] private PlayerMovement3D playerMovement;
    [SerializeField] private PlayerActions3D playerActions;
    [SerializeField] private AIPlayer3D aiMovement;

    [Header("Re-Drop")]
    [SerializeField, Min(0f)] private float reDropFreezeSeconds = 0.65f;
    [SerializeField, Min(0f)] private float reDropBannerSeconds = 0.35f;

    [Header("Feedback")]
    [SerializeField, Range(0f, 1f)] private float goalCameraTrauma = 0.42f;

    [Header("Arcade Progress")]
    [SerializeField] private string minigameId = "AirFooty";
    [SerializeField, Min(0f)] private float ticketsPerPoint = 1f;
    [SerializeField] private PlayerScoreCarrier scoreCarrier;

    [Header("Scene Flow")]
    [SerializeField] private bool returnToSceneOnFinish = true;
    [SerializeField] private string returnSceneName = "Sc_ArcadeHub";
    [SerializeField, Min(0f)] private float returnDelaySeconds = 4f;

    private int playerScore;
    private int aiScore;
    private int bestRecordedScore;
    private int ticketsAwarded;
    private int totalTickets;
    private float finishTime;
    private bool goalBeingProcessed;
    private bool gameOver;
    private bool scoreRecorded;
    private GoalZone3D.ScoringSide lastScoringSide;
    private AirFootyCameraFx cameraFx;
    private AudioSource feedbackAudio;
    private Coroutine kickoffRoutine;
    private bool missingScoreCarrierReported;

    public int PlayerScore => playerScore;
    public int AiScore => aiScore;
    public int ScoreNeededToWin => scoreNeededToWin;
    public bool IsGameOver => gameOver;
    public bool IsKickoffRunning => kickoffRoutine != null;

    private void Start()
    {
        ResolveGameplayObjects();
        EnsurePresentation();
        ResolveScoreCarrier();
        PlayerScoreCarrier.ScoreRecord scoreRecord = ReadScoreRecord();
        bestRecordedScore = scoreRecord.BestScore;
        totalTickets = scoreRecord.TotalTickets;

        scoreUI.UpdateScores(playerScore, aiScore);
        scoreUI.HideGameOver();
        scoreUI.ShowActionPrompts();
        ball.Stalled += HandleBallStalled;
        ball.PrepareKickoff();
        SetPlayerControl(false);
        kickoffRoutine = StartCoroutine(KickoffSequence(0f, true));
    }

    private void OnDestroy()
    {
        if (ball != null)
        {
            ball.Stalled -= HandleBallStalled;
        }
    }

    private void Update()
    {
        if (!gameOver)
        {
            return;
        }

        if (returnToSceneOnFinish)
        {
            TickReturnDelay();
        }
        else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
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
        SetPlayerControl(false);
        lastScoringSide = scoringSide;

        if (scoringSide == GoalZone3D.ScoringSide.Player) playerScore++;
        else aiScore++;

        scoreUI.UpdateScores(playerScore, aiScore);
        cameraFx?.AddTrauma(goalCameraTrauma);

        if (playerScore >= scoreNeededToWin || aiScore >= scoreNeededToWin)
        {
            gameOver = true;
            finishTime = Time.unscaledTime;
            RecordScore();

            string result = playerScore >= scoreNeededToWin ? "Player Wins!" : "AI Wins!";
            string flowMessage = returnToSceneOnFinish
                ? "Returning to the arcade..."
                : "Press Space to Restart";
            scoreUI.ShowGameOver(
                $"{result}\nScore {playerScore}  |  Best {bestRecordedScore}\n" +
                $"+{ticketsAwarded} Tickets  |  Total {totalTickets}\n{flowMessage}");
        }
        else
        {
            StartCoroutine(ResetAfterGoal());
        }

        return true;
    }

    private void ResolveScoreCarrier()
    {
        if (scoreCarrier == null)
        {
            scoreCarrier = PlayerScoreCarrier.FindForPlayer();
        }

        if (scoreCarrier == null)
        {
            if (!missingScoreCarrierReported)
            {
                missingScoreCarrierReported = true;
                Debug.Log(
                    $"{name} is running without a PlayerScoreCarrier. " +
                    $"{GameTitle} score persistence is disabled for this standalone session.",
                    this);
            }
        }
        else
        {
            missingScoreCarrierReported = false;
        }
    }

    private void RecordScore()
    {
        if (scoreRecorded)
        {
            return;
        }

        scoreRecorded = true;
        ResolveScoreCarrier();
        if (scoreCarrier == null)
        {
            bestRecordedScore = Mathf.Max(bestRecordedScore, playerScore);
            return;
        }

        PlayerScoreCarrier.ScoreRecord scoreRecord =
            scoreCarrier.RecordScore(minigameId, playerScore, ticketsPerPoint);
        bestRecordedScore = scoreRecord.BestScore;
        ticketsAwarded = scoreRecord.TicketsAwarded;
        totalTickets = scoreRecord.TotalTickets;
    }

    private PlayerScoreCarrier.ScoreRecord ReadScoreRecord()
    {
        return scoreCarrier != null
            ? scoreCarrier.ReadScore(minigameId)
            : new PlayerScoreCarrier.ScoreRecord(minigameId, 0, 0, 0, 0);
    }

    private void TickReturnDelay()
    {
        if (Time.unscaledTime - finishTime < returnDelaySeconds ||
            string.IsNullOrWhiteSpace(returnSceneName))
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
        {
            Debug.LogWarning(
                $"{name} cannot return to '{returnSceneName}'. Add the scene to Build Settings or update Return Scene Name.",
                this);
            returnToSceneOnFinish = false;
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
    }

    private IEnumerator ResetAfterGoal()
    {
        yield return new WaitForSecondsRealtime(resetDelay);
        float serveDirection = playerScore > 0 || aiScore > 0
            ? LastScoringSideDirection()
            : 0f;
        ball.PrepareKickoff();
        playerGoal.AllowGoal();
        aiGoal.AllowGoal();
        kickoffRoutine = StartCoroutine(KickoffSequence(serveDirection, false));
        yield return kickoffRoutine;
        goalBeingProcessed = false;
    }

    private IEnumerator KickoffSequence(float horizontalDirection, bool openingKickoff)
    {
        while (Mathf.Approximately(Time.timeScale, 0f))
        {
            yield return null;
        }

        SetPlayerControl(false);
        ball.PrepareKickoff();

        if (openingKickoff)
        {
            scoreUI.ShowMatchStatus($"FIRST TO {scoreNeededToWin}", null, true);
            yield return new WaitForSecondsRealtime(openingBannerSeconds);
        }

        for (int count = countdownFrom; count >= 1; count--)
        {
            scoreUI.ShowMatchStatus(count.ToString(), null, true);
            PlayCountdownTick(1f + (countdownFrom - count) * 0.08f);
            yield return new WaitForSecondsRealtime(countdownStepSeconds);
        }

        scoreUI.ShowMatchStatus("KICK-OFF!", new Color(0.3f, 0.95f, 1f, 1f), true);
        ball.LaunchBall(horizontalDirection);
        SetPlayerControl(true);
        PlayCountdownTick(1.35f);
        yield return new WaitForSecondsRealtime(kickoffBannerSeconds);
        scoreUI.HideMatchStatus();
        kickoffRoutine = null;
    }

    private void HandleBallStalled()
    {
        if (gameOver || goalBeingProcessed || kickoffRoutine != null)
        {
            return;
        }

        AirFootyTeam lastTouchTeam = ball.LastTouchTeam;
        kickoffRoutine = StartCoroutine(ReDropSequence(lastTouchTeam));
    }

    private IEnumerator ReDropSequence(AirFootyTeam lastTouchTeam)
    {
        SetPlayerControl(false);
        ball.StopBall();
        scoreUI.ShowMatchStatus("RE-DROP", new Color(1f, 0.82f, 0.28f, 1f), true);
        yield return new WaitForSecondsRealtime(reDropFreezeSeconds);

        float serveDirection = ReDropServeDirection(lastTouchTeam);
        ball.PrepareKickoff();
        playerGoal.AllowGoal();
        aiGoal.AllowGoal();
        ball.LaunchBall(serveDirection);
        SetPlayerControl(true);
        PlayCountdownTick(1.15f);

        yield return new WaitForSecondsRealtime(reDropBannerSeconds);
        scoreUI.HideMatchStatus();
        kickoffRoutine = null;
    }

    private static float ReDropServeDirection(AirFootyTeam lastTouchTeam)
    {
        return lastTouchTeam switch
        {
            AirFootyTeam.Player => 1f,
            AirFootyTeam.AI => -1f,
            _ => Random.value < 0.5f ? -1f : 1f
        };
    }

    private float LastScoringSideDirection()
    {
        // The conceding side receives the next automatic serve.
        return lastScoringSide == GoalZone3D.ScoringSide.Player ? 1f : -1f;
    }

    private void ResolveGameplayObjects()
    {
        if (ball == null)
        {
            ball = FindFirstObjectByType<BallController3D>();
        }
        if (scoreUI == null)
        {
            scoreUI = FindFirstObjectByType<ScoreUI>();
        }
        if (playerMovement == null)
        {
            playerMovement = FindFirstObjectByType<PlayerMovement3D>();
        }
        if (aiMovement == null)
        {
            aiMovement = FindFirstObjectByType<AIPlayer3D>();
        }
        if (playerActions == null)
        {
            playerActions = FindFirstObjectByType<PlayerActions3D>();
        }
    }

    private void EnsurePresentation()
    {
        if (GetComponent<AirFootyArenaPresentation>() == null)
        {
            gameObject.AddComponent<AirFootyArenaPresentation>();
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                AudioListener listener = mainCamera.GetComponent<AudioListener>();
                if (listener == null)
                {
                    listener = mainCamera.gameObject.AddComponent<AudioListener>();
                }

                listener.enabled = true;
            }

            cameraFx = mainCamera.GetComponent<AirFootyCameraFx>();
            if (cameraFx == null)
            {
                cameraFx = mainCamera.gameObject.AddComponent<AirFootyCameraFx>();
            }
        }

        feedbackAudio = GetComponent<AudioSource>();
        if (feedbackAudio == null)
        {
            feedbackAudio = gameObject.AddComponent<AudioSource>();
        }
        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0f;
        feedbackAudio.volume = 0.65f;
    }

    private void SetPlayerControl(bool enabled)
    {
        playerMovement?.SetMovementEnabled(enabled);
        playerActions?.SetActionsEnabled(enabled);
        aiMovement?.SetMovementEnabled(enabled);
    }

    private void PlayCountdownTick(float pitch)
    {
        if (feedbackAudio == null)
        {
            return;
        }

        feedbackAudio.pitch = pitch;
        feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.CountdownClip);
    }

    private void OnValidate()
    {
        scoreNeededToWin = Mathf.Max(1, scoreNeededToWin);
        resetDelay = Mathf.Max(0f, resetDelay);
        openingBannerSeconds = Mathf.Max(0f, openingBannerSeconds);
        countdownFrom = Mathf.Clamp(countdownFrom, 1, 3);
        countdownStepSeconds = Mathf.Max(0.1f, countdownStepSeconds);
        kickoffBannerSeconds = Mathf.Max(0f, kickoffBannerSeconds);
        reDropFreezeSeconds = Mathf.Max(0f, reDropFreezeSeconds);
        reDropBannerSeconds = Mathf.Max(0f, reDropBannerSeconds);
        ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
        returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
    }
}
