using System;
using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private int scoreNeededToWin = 5;
    [SerializeField] private float resetDelay = 1.5f;

    [Header("Kick-Off")]
    [SerializeField, Min(0f)] private float openingBannerSeconds = 0.65f;
    [SerializeField, Range(1, 3)] private int countdownFrom = 3;
    [SerializeField, Min(0.1f)] private float countdownStepSeconds = 0.5f;
    [SerializeField, Min(0f)] private float kickoffBannerSeconds = 0.45f;

    [Header("Re-Drop")]
    [SerializeField, Min(0f)] private float reDropFreezeSeconds = 0.65f;
    [SerializeField, Min(0f)] private float reDropBannerSeconds = 0.35f;

    [Header("Feedback")]
    [SerializeField, Range(0f, 1f)] private float goalCameraTrauma = 0.42f;

    [Header("Four Player Team Areas")]
    [SerializeField, Min(0.1f)] private float centreRingRadius = 1.1f;
    [SerializeField, Min(0.6f)] private float teamGoalLineDepth = 7.75f;

    [Header("Arcade Progress")]
    [SerializeField] private string minigameId = "AirFooty";
    [SerializeField, Min(0f)] private float ticketsPerPoint = 1f;
    [SerializeField] private PlayerScoreCarrier scoreCarrier;

    [Header("Scene Flow")]
    [SerializeField] private bool returnToSceneOnFinish = true;
    [SerializeField] private string returnSceneName = "Sc_ArcadeHub";
    [SerializeField, Min(0f)] private float returnDelaySeconds = 4f;

    private readonly Dictionary<AirFootyTeam, int> goalsConceded = new();
    private readonly HashSet<AirFootyTeam> activeTeams = new();
    private readonly HashSet<AirFootyTeam> eliminatedTeams = new();
    private readonly Dictionary<BallController3D, Action> stallHandlers = new();

    private BallController3D[] balls = Array.Empty<BallController3D>();
    private GoalZone3D[] goals = Array.Empty<GoalZone3D>();
    private PlayerMovement3D[] playerMovements = Array.Empty<PlayerMovement3D>();
    private PlayerActions3D[] playerActionControllers = Array.Empty<PlayerActions3D>();
    private AIPlayer3D[] legacyAiControllers = Array.Empty<AIPlayer3D>();
    private AirFootySideAI3D[] sideAiControllers = Array.Empty<AirFootySideAI3D>();
    private PlayerMovement3D playerMovement;

    private int playerScore;
    private int aiScore;
    private int bestRecordedScore;
    private int ticketsAwarded;
    private int totalTickets;
    private float finishTime;
    private bool gameOver;
    private bool scoreRecorded;
    private bool fourPlayerMode;
    private AirFootyTeam humanControlledTeam = AirFootyTeam.Blue;
    private AirFootyCameraFx cameraFx;
    private AudioSource feedbackAudio;
    private Coroutine kickoffRoutine;
    private bool missingScoreCarrierReported;

    public int PlayerScore => playerScore;
    public int AiScore => aiScore;
    public int ScoreNeededToWin => scoreNeededToWin;
    public bool IsGameOver => gameOver;
    public bool IsKickoffRunning => kickoffRoutine != null;
    public bool IsFourPlayerMode => fourPlayerMode;

    private void Start()
    {
        ResolveGameplayObjects();
        ConfigureTeamsAndControllers();
        EnsurePresentation();
        ResolveScoreCarrier();
        PlayerScoreCarrier.ScoreRecord scoreRecord = ReadScoreRecord();
        bestRecordedScore = scoreRecord.BestScore;
        totalTickets = scoreRecord.TotalTickets;

        UpdateScoreDisplay();
        scoreUI?.HideGameOver();
        SubscribeToBalls();
        PrepareAllBalls();
        SetPlayerControl(false);
        kickoffRoutine = StartCoroutine(KickoffSequence(true));
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<BallController3D, Action> pair in stallHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.Stalled -= pair.Value;
            }
        }
        stallHandlers.Clear();
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

    public int GetGoalsConceded(AirFootyTeam team)
    {
        return goalsConceded.TryGetValue(team, out int score) ? score : 0;
    }

    public bool IsTeamEliminated(AirFootyTeam team)
    {
        return eliminatedTeams.Contains(team);
    }

    public bool GoalConceded(
        AirFootyTeam concedingTeam,
        BallController3D scoringBall)
    {
        if (gameOver ||
            concedingTeam == AirFootyTeam.None ||
            eliminatedTeams.Contains(concedingTeam) ||
            scoringBall == null)
        {
            return false;
        }

        scoringBall.StopBall();
        goalsConceded[concedingTeam] = GetGoalsConceded(concedingTeam) + 1;

        AirFootyTeam scoringTeam = scoringBall.LastTouchTeam;
        if (!fourPlayerMode)
        {
            if (concedingTeam == humanControlledTeam)
            {
                aiScore++;
            }
            else
            {
                playerScore++;
            }
        }
        else if (scoringTeam == humanControlledTeam &&
                 concedingTeam != humanControlledTeam)
        {
            playerScore++;
        }
        else if (concedingTeam == humanControlledTeam)
        {
            aiScore++;
        }

        UpdateScoreDisplay();
        cameraFx?.AddTrauma(goalCameraTrauma);

        bool eliminated = GetGoalsConceded(concedingTeam) >= scoreNeededToWin;
        if (eliminated)
        {
            EliminateTeam(concedingTeam);
        }

        if (!gameOver)
        {
            if (!fourPlayerMode)
            {
                SetPlayerControl(false);
            }
            StartCoroutine(ResetBallAfterGoal(scoringBall, concedingTeam));
        }

        return true;
    }

    // Compatibility entry point for the original two-goal prefab and older tests.
    public bool GoalScored(GoalZone3D.ScoringSide scoringSide)
    {
        AirFootyTeam concedingTeam = scoringSide == GoalZone3D.ScoringSide.Player
            ? AirFootyTeam.Red
            : AirFootyTeam.Blue;
        BallController3D scoringBall = ball != null
            ? ball
            : balls.Length > 0 ? balls[0] : null;
        return GoalConceded(concedingTeam, scoringBall);
    }

    private void EliminateTeam(AirFootyTeam team)
    {
        if (!eliminatedTeams.Add(team))
        {
            return;
        }

        foreach (AirFootyTeamMember3D member in
                 GetComponentsInChildren<AirFootyTeamMember3D>(true))
        {
            if (member.Team == team)
            {
                member.gameObject.SetActive(false);
            }
        }

        string teamName = AirFootyTeamMember3D.DisplayName(team);
        Color teamColor = AirFootyTeamMember3D.ColorFor(team);
        scoreUI?.ShowMatchStatus($"{teamName} ELIMINATED", teamColor, true);
        AirFootyWorldPopup.Spawn(
            transform.position + Vector3.up * 1.5f,
            $"{teamName} OUT!",
            teamColor);

        int remainingTeams = 0;
        AirFootyTeam winner = AirFootyTeam.None;
        foreach (AirFootyTeam candidate in activeTeams)
        {
            if (!eliminatedTeams.Contains(candidate))
            {
                remainingTeams++;
                winner = candidate;
            }
        }

        if (remainingTeams <= 1)
        {
            FinishMatch(winner);
        }
    }

    private void FinishMatch(AirFootyTeam winner)
    {
        gameOver = true;
        finishTime = Time.unscaledTime;
        SetPlayerControl(false);
        StopAllBalls();
        RecordScore();

        string winnerName = AirFootyTeamMember3D.DisplayName(winner);
        string flowMessage = returnToSceneOnFinish
            ? "Returning to the arcade..."
            : "Press Space to Restart";
        string humanTeamName =
            AirFootyTeamMember3D.DisplayName(humanControlledTeam);
        scoreUI?.ShowGameOver(
            $"{winnerName} WINS!\n" +
            $"{humanTeamName} Score {playerScore}  |  Best {bestRecordedScore}\n" +
            $"+{ticketsAwarded} Tickets  |  Total {totalTickets}\n{flowMessage}");
    }

    private IEnumerator ResetBallAfterGoal(
        BallController3D scoringBall,
        AirFootyTeam concedingTeam)
    {
        yield return new WaitForSecondsRealtime(resetDelay);
        if (gameOver || scoringBall == null)
        {
            yield break;
        }

        foreach (GoalZone3D goal in goals)
        {
            goal?.AllowGoal();
        }

        scoringBall.PrepareKickoff();
        float horizontalDirection = concedingTeam switch
        {
            AirFootyTeam.Blue => -1f,
            AirFootyTeam.Red => 1f,
            _ => UnityEngine.Random.value < 0.5f ? -1f : 1f
        };
        scoringBall.LaunchBall(horizontalDirection);
        if (!fourPlayerMode)
        {
            SetPlayerControl(true);
        }

        yield return new WaitForSecondsRealtime(reDropBannerSeconds);
        scoreUI?.HideMatchStatus();
    }

    private IEnumerator KickoffSequence(bool openingKickoff)
    {
        while (Mathf.Approximately(Time.timeScale, 0f))
        {
            yield return null;
        }

        SetPlayerControl(false);
        PrepareAllBalls();

        if (openingKickoff)
        {
            string rule = fourPlayerMode
                ? $"{scoreNeededToWin} IN YOUR GOAL = ELIMINATED"
                : $"FIRST TO {scoreNeededToWin}";
            scoreUI?.ShowMatchStatus(rule, null, true);
            yield return new WaitForSecondsRealtime(openingBannerSeconds);
        }

        for (int count = countdownFrom; count >= 1; count--)
        {
            scoreUI?.ShowMatchStatus(count.ToString(), null, true);
            PlayCountdownTick(1f + (countdownFrom - count) * 0.08f);
            yield return new WaitForSecondsRealtime(countdownStepSeconds);
        }

        scoreUI?.ShowMatchStatus("KICK-OFF!", new Color(0.3f, 0.95f, 1f, 1f), true);
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i]?.LaunchBall(i % 2 == 0 ? 1f : -1f);
        }
        SetPlayerControl(true);
        PlayCountdownTick(1.35f);
        yield return new WaitForSecondsRealtime(kickoffBannerSeconds);
        scoreUI?.HideMatchStatus();
        kickoffRoutine = null;
    }

    private void SubscribeToBalls()
    {
        foreach (BallController3D candidate in balls)
        {
            if (candidate == null || stallHandlers.ContainsKey(candidate))
            {
                continue;
            }

            BallController3D capturedBall = candidate;
            Action handler = () => HandleBallStalled(capturedBall);
            stallHandlers.Add(candidate, handler);
            candidate.Stalled += handler;
        }
    }

    private void HandleBallStalled(BallController3D stalledBall)
    {
        if (gameOver || kickoffRoutine != null || stalledBall == null)
        {
            return;
        }

        StartCoroutine(ReDropSequence(stalledBall));
    }

    private IEnumerator ReDropSequence(BallController3D stalledBall)
    {
        stalledBall.StopBall();
        scoreUI?.ShowMatchStatus("RE-DROP", new Color(1f, 0.82f, 0.28f, 1f), true);
        yield return new WaitForSecondsRealtime(reDropFreezeSeconds);
        if (gameOver || stalledBall == null)
        {
            yield break;
        }

        stalledBall.PrepareKickoff();
        foreach (GoalZone3D goal in goals)
        {
            goal?.AllowGoal();
        }
        stalledBall.LaunchBall(UnityEngine.Random.value < 0.5f ? -1f : 1f);
        PlayCountdownTick(1.15f);
        yield return new WaitForSecondsRealtime(reDropBannerSeconds);
        scoreUI?.HideMatchStatus();
    }

    private void ResolveGameplayObjects()
    {
        balls = GetComponentsInChildren<BallController3D>(true);
        if (balls.Length == 0 && ball != null)
        {
            balls = new[] { ball };
        }
        if (ball == null && balls.Length > 0)
        {
            ball = balls[0];
        }

        goals = GetComponentsInChildren<GoalZone3D>(true);
        playerMovements = GetComponentsInChildren<PlayerMovement3D>(true);
        playerActionControllers = GetComponentsInChildren<PlayerActions3D>(true);
        legacyAiControllers = GetComponentsInChildren<AIPlayer3D>(true);

        if (scoreUI == null) scoreUI = FindFirstObjectByType<ScoreUI>();
    }

    private void ConfigureTeamsAndControllers()
    {
        activeTeams.Clear();
        foreach (GoalZone3D goal in goals)
        {
            if (goal == null) continue;
            AirFootyTeam owner = goal.OwnerTeam;
            if (owner != AirFootyTeam.None)
            {
                activeTeams.Add(owner);
                goalsConceded.TryAdd(owner, 0);
            }
        }

        if (activeTeams.Count == 0)
        {
            activeTeams.Add(AirFootyTeam.Blue);
            activeTeams.Add(AirFootyTeam.Red);
            goalsConceded[AirFootyTeam.Blue] = 0;
            goalsConceded[AirFootyTeam.Red] = 0;
        }

        fourPlayerMode = activeTeams.Count > 2 || balls.Length > 1;
        humanControlledTeam = AirFootySessionConfig.HasSelection &&
                              activeTeams.Contains(AirFootySessionConfig.HumanTeam)
            ? AirFootySessionConfig.HumanTeam
            : AirFootyTeam.Blue;

        HashSet<GameObject> strikerObjects = new();
        foreach (PlayerMovement3D controller in playerMovements)
        {
            strikerObjects.Add(controller.gameObject);
            ConfigureTeamMember(controller.gameObject, AirFootyTeam.Blue);
        }

        foreach (AIPlayer3D controller in legacyAiControllers)
        {
            AirFootyTeam inferred = AirFootyTeamMember3D.InferFromHierarchy(
                controller.transform);
            if (inferred == AirFootyTeam.None) inferred = AirFootyTeam.Red;
            ConfigureTeamMember(controller.gameObject, inferred);
            strikerObjects.Add(controller.gameObject);
        }

        foreach (GameObject strikerObject in strikerObjects)
        {
            AirFootyTeamMember3D member =
                strikerObject.GetComponent<AirFootyTeamMember3D>();
            AirFootyTeam team = member != null
                ? member.Team
                : AirFootyTeam.Blue;
            bool humanControlled = team == humanControlledTeam;

            AIPlayer3D legacyAI = strikerObject.GetComponent<AIPlayer3D>();
            if (legacyAI != null)
            {
                legacyAI.enabled = false;
            }

            PlayerMovement3D movement =
                strikerObject.GetComponent<PlayerMovement3D>();
            PlayerActions3D actions =
                strikerObject.GetComponent<PlayerActions3D>();
            AirFootySideAI3D sideAI =
                strikerObject.GetComponent<AirFootySideAI3D>();

            if (humanControlled)
            {
                if (sideAI != null)
                {
                    sideAI.SetMovementEnabled(false);
                    sideAI.enabled = false;
                }
                if (movement == null)
                {
                    movement = strikerObject.AddComponent<PlayerMovement3D>();
                }
                movement.ConfigureTeamArea(
                    fourPlayerMode,
                    transform.position,
                    centreRingRadius,
                    teamGoalLineDepth);
                movement.enabled = true;
                if (actions == null)
                {
                    actions = strikerObject.AddComponent<PlayerActions3D>();
                }
                actions.enabled = true;

                AirFootyStrikeMotor3D strikeMotor =
                    strikerObject.GetComponent<AirFootyStrikeMotor3D>();
                strikeMotor?.ConfigureTeam(team);
            }
            else
            {
                if (movement != null)
                {
                    movement.SetMovementEnabled(false);
                    movement.enabled = false;
                }
                if (actions != null)
                {
                    actions.SetActionsEnabled(false);
                    actions.enabled = false;
                }
                sideAI = EnsureSideAI(strikerObject, team);
                sideAI.enabled = true;
            }
        }

        playerMovements = GetComponentsInChildren<PlayerMovement3D>(true);
        playerActionControllers = GetComponentsInChildren<PlayerActions3D>(true);
        sideAiControllers = GetComponentsInChildren<AirFootySideAI3D>(true);

        playerMovement = null;
        foreach (PlayerMovement3D controller in playerMovements)
        {
            if (controller != null && controller.enabled)
            {
                playerMovement = controller;
                break;
            }
        }
        GetComponent<AirFootyCinemachineCameraRig>()?.SetPlayer(
            playerMovement,
            humanControlledTeam);
    }

    private AirFootySideAI3D EnsureSideAI(GameObject target, AirFootyTeam team)
    {
        AirFootySideAI3D sideAI = target.GetComponent<AirFootySideAI3D>();
        if (sideAI == null)
        {
            sideAI = target.AddComponent<AirFootySideAI3D>();
        }
        sideAI.Configure(
            team,
            balls,
            goals,
            this,
            fourPlayerMode,
            transform.position,
            centreRingRadius,
            teamGoalLineDepth);
        return sideAI;
    }

    private static AirFootyTeam ConfigureTeamMember(
        GameObject target,
        AirFootyTeam fallback)
    {
        AirFootyTeam inferred = AirFootyTeamMember3D.InferFromHierarchy(target.transform);
        AirFootyTeamMember3D member = target.GetComponent<AirFootyTeamMember3D>();
        if (member == null)
        {
            member = target.AddComponent<AirFootyTeamMember3D>();
        }
        AirFootyTeam team = inferred != AirFootyTeam.None ? inferred : fallback;
        member.Configure(team);
        return team;
    }

    private void UpdateScoreDisplay()
    {
        if (scoreUI == null)
        {
            return;
        }

        if (fourPlayerMode)
        {
            scoreUI.UpdateEliminationScores(
                GetGoalsConceded(AirFootyTeam.Blue),
                GetGoalsConceded(AirFootyTeam.Red),
                GetGoalsConceded(AirFootyTeam.Green),
                GetGoalsConceded(AirFootyTeam.Gold),
                scoreNeededToWin);
        }
        else
        {
            AirFootyTeam opponent = AirFootyTeam.None;
            foreach (AirFootyTeam candidate in activeTeams)
            {
                if (candidate != humanControlledTeam)
                {
                    opponent = candidate;
                    break;
                }
            }
            scoreUI.UpdateHeadToHeadScores(
                playerScore,
                aiScore,
                humanControlledTeam,
                opponent);
        }
    }

    private void PrepareAllBalls()
    {
        foreach (BallController3D candidate in balls)
        {
            candidate?.PrepareKickoff();
        }
    }

    private void StopAllBalls()
    {
        foreach (BallController3D candidate in balls)
        {
            candidate?.StopBall();
        }
    }

    private void SetPlayerControl(bool enabled)
    {
        foreach (PlayerMovement3D controller in playerMovements)
        {
            if (controller != null && controller.enabled)
            {
                controller.SetMovementEnabled(enabled);
            }
        }
        foreach (PlayerActions3D controller in playerActionControllers)
        {
            if (controller != null && controller.enabled)
            {
                controller.SetActionsEnabled(enabled);
            }
        }
        foreach (AIPlayer3D controller in legacyAiControllers)
        {
            if (controller != null && controller.enabled)
            {
                controller.SetMovementEnabled(enabled);
            }
        }
        foreach (AirFootySideAI3D controller in sideAiControllers)
        {
            if (controller != null && controller.enabled)
            {
                controller.SetMovementEnabled(enabled);
            }
        }
    }

    private void ResolveScoreCarrier()
    {
        if (scoreCarrier == null) scoreCarrier = PlayerScoreCarrier.FindForPlayer();
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
        if (scoreRecorded) return;
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

    private void EnsurePresentation()
    {
        if (GetComponent<AirFootyArenaPresentation>() == null)
        {
            gameObject.AddComponent<AirFootyArenaPresentation>();
        }

        AirFootyCinemachineCameraRig cameraRig =
            GetComponent<AirFootyCinemachineCameraRig>();
        Camera mainCamera = cameraRig != null
            ? cameraRig.OutputCamera
            : AirFootyCameraLookup.FindDisplayCamera();
        if (mainCamera != null)
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                AudioListener listener = mainCamera.GetComponent<AudioListener>();
                if (listener == null) listener = mainCamera.gameObject.AddComponent<AudioListener>();
                listener.enabled = true;
            }

            cameraFx = mainCamera.GetComponent<AirFootyCameraFx>();
            if (cameraFx == null) cameraFx = mainCamera.gameObject.AddComponent<AirFootyCameraFx>();
        }

        feedbackAudio = GetComponent<AudioSource>();
        if (feedbackAudio == null) feedbackAudio = gameObject.AddComponent<AudioSource>();
        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0f;
        feedbackAudio.volume = 0.65f;
    }

    private void PlayCountdownTick(float pitch)
    {
        if (feedbackAudio == null) return;
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
        centreRingRadius = Mathf.Max(0.1f, centreRingRadius);
        teamGoalLineDepth = Mathf.Max(
            centreRingRadius + 0.5f,
            teamGoalLineDepth);
        ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
        returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
    }
}
