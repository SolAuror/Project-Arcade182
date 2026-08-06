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

    [Header("Overtime Contingency")]
    [Tooltip("Match length before the ball turns lethal. Optional in 2P, forced in 4P.")]
    [SerializeField, Min(10f)] private float overtimeTriggerSeconds = 300f;
    [Tooltip("Remaining time at which the jumbotrons switch to the amber run-in.")]
    [SerializeField, Min(1f)] private float clockAlertSeconds = 30f;
    [SerializeField, Min(0f)] private float respawnDelaySeconds = 1.5f;

    [Header("Feedback")]
    [SerializeField, Range(0f, 1f)] private float goalCameraTrauma = 0.42f;
    [SerializeField, Range(0f, 1f)] private float vaporiseCameraTrauma = 0.6f;

    [Header("Authored Feedback Assets")]
    [SerializeField] private GameObject goalBurstPrefab;
    [SerializeField] private GameObject worldPopupPrefab;
    [SerializeField] private GameObject pulseWavePrefab;
    [SerializeField] private GameObject ballHoverPrefab;
    [SerializeField] private GameObject vaporiseBurstPrefab;
    [SerializeField] private AudioClip impactClip;
    [SerializeField] private AudioClip goalClip;
    [SerializeField] private AudioClip countdownClip;
    [SerializeField] private AudioClip vaporiseClip;

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
    private readonly Dictionary<AirFootyTeam, int> goalsScored = new();
    private readonly HashSet<AirFootyTeam> activeTeams = new();
    private readonly HashSet<AirFootyTeam> eliminatedTeams = new();
    private readonly Dictionary<BallController3D, Action> stallHandlers = new();
    private readonly Dictionary<BallController3D, Action<AirFootyTeam, AirFootyTeam>>
        lethalHandlers = new();

    // Overtime respawns need to put a striker back exactly where it started and
    // keep the shared control toggle from reviving it early.
    private readonly Dictionary<AirFootyTeam, GameObject> strikersByTeam = new();
    private readonly Dictionary<AirFootyTeam, Vector3> strikerHomePositions = new();
    private readonly HashSet<AirFootyTeam> vaporisedTeams = new();

    private BallController3D[] balls = Array.Empty<BallController3D>();
    private GoalZone3D[] goals = Array.Empty<GoalZone3D>();
    private AirFootyMatchClock3D[] matchClocks = Array.Empty<AirFootyMatchClock3D>();
    private AirFootyCrowdDirector[] crowdDirectors = Array.Empty<AirFootyCrowdDirector>();
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
    private bool overtimeEnabled;
    private bool overtimeActive;
    private float matchSecondsRemaining;
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
    public bool IsOvertimeEnabled => overtimeEnabled;
    public bool IsOvertimeActive => overtimeActive;
    public float MatchSecondsRemaining => matchSecondsRemaining;

    private void Awake()
    {
        AirFootyPrefabLibrary.Configure(
            goalBurstPrefab,
            worldPopupPrefab,
            pulseWavePrefab,
            ballHoverPrefab);
        AirFootyFeedbackUtility.Configure(
            impactClip,
            goalClip,
            countdownClip,
            vaporiseClip,
            vaporiseBurstPrefab);
        ValidateAuthoredFeedbackAssets();
    }

    private void Start()
    {
        ResolveGameplayObjects();
        ConfigureTeamsAndControllers();
        EnsurePresentation();
        ResolveScoreCarrier();
        PlayerScoreCarrier.ScoreRecord scoreRecord = ReadScoreRecord();
        bestRecordedScore = scoreRecord.BestScore;
        totalTickets = scoreRecord.TotalTickets;

        // Four player elimination always ends in overtime; two player only does
        // when the menu asked for it.
        overtimeEnabled = fourPlayerMode || AirFootySessionConfig.OvertimeEnabled;
        matchSecondsRemaining = overtimeTriggerSeconds;

        UpdateScoreDisplay();
        UpdateMatchClocks();
        scoreUI?.HideGameOver();
        SubscribeToBalls();
        PrepareAllBalls();
        SetPlayerControl(false);
        kickoffRoutine = StartCoroutine(KickoffSequence(OpeningRuleBanner(), null));
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

        foreach (KeyValuePair<BallController3D, Action<AirFootyTeam, AirFootyTeam>> pair
                 in lethalHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.LethalContact -= pair.Value;
            }
        }
        lethalHandlers.Clear();
    }

    private void Update()
    {
        if (!gameOver)
        {
            TickMatchClock();
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

    public int GetGoalsScored(AirFootyTeam team)
    {
        return goalsScored.TryGetValue(team, out int score) ? score : 0;
    }

    private string OpeningRuleBanner()
    {
        string rule = fourPlayerMode
            ? $"{scoreNeededToWin} IN YOUR GOAL = ELIMINATED"
            : $"FIRST TO {scoreNeededToWin}";
        return overtimeEnabled
            ? $"{rule}\n{FormatClock(overtimeTriggerSeconds)} TO OVERTIME"
            : rule;
    }

    private static string FormatClock(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{total / 60}:{total % 60:D2}";
    }

    /// <summary>
    /// Runs the match clock down. Held during the kick-off count so the 3-2-1 does
    /// not eat the match, and frozen with the game because it uses scaled time.
    /// </summary>
    private void TickMatchClock()
    {
        if (!overtimeEnabled || overtimeActive || kickoffRoutine != null)
        {
            return;
        }

        matchSecondsRemaining = Mathf.Max(0f, matchSecondsRemaining - Time.deltaTime);
        UpdateMatchClocks();

        if (matchSecondsRemaining <= 0f)
        {
            BeginOvertime();
        }
    }

    private void UpdateMatchClocks()
    {
        for (int i = 0; i < matchClocks.Length; i++)
        {
            AirFootyMatchClock3D clock = matchClocks[i];
            if (clock == null || overtimeActive)
            {
                continue;
            }

            if (!overtimeEnabled)
            {
                clock.SetHidden();
                continue;
            }

            clock.SetTime(
                matchSecondsRemaining,
                matchSecondsRemaining <= clockAlertSeconds);
        }
    }

    /// <summary>
    /// The clock has run out. Every ball becomes able to vaporise a striker, but
    /// only once somebody claims it with a pulse, and the pitch turns pulse only.
    /// </summary>
    private void BeginOvertime()
    {
        if (overtimeActive)
        {
            return;
        }

        overtimeActive = true;
        matchSecondsRemaining = 0f;

        for (int i = 0; i < matchClocks.Length; i++)
        {
            matchClocks[i]?.SetOvertime();
        }
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i]?.SetOvertimeLethal(true);
        }
        for (int i = 0; i < sideAiControllers.Length; i++)
        {
            if (sideAiControllers[i] != null)
            {
                sideAiControllers[i].SetOvertime(true);
            }
        }

        cameraFx?.AddTrauma(0.55f);

        // Re-drop through the normal kick-off so nobody is killed by a ball that
        // was already in flight when the rules changed.
        if (kickoffRoutine != null)
        {
            StopCoroutine(kickoffRoutine);
        }
        kickoffRoutine = StartCoroutine(KickoffSequence(
            "OVERTIME\nTHE BALL IS LIVE",
            new Color(1f, 0.18f, 0.25f, 1f)));
    }

    public bool IsTeamEliminated(AirFootyTeam team)
    {
        return eliminatedTeams.Contains(team);
    }

    public bool GoalConceded(
        AirFootyTeam concedingTeam,
        BallController3D scoringBall)
    {
        return GoalConceded(concedingTeam, scoringBall, out _);
    }

    public bool GoalConceded(
        AirFootyTeam concedingTeam,
        BallController3D scoringBall,
        out AirFootyTeam scoringTeam)
    {
        scoringTeam = AirFootyTeam.None;
        if (gameOver ||
            concedingTeam == AirFootyTeam.None ||
            eliminatedTeams.Contains(concedingTeam) ||
            scoringBall == null)
        {
            return false;
        }

        // A ball that is already stopped has scored and is waiting to be
        // re-dropped, so it cannot score again. This is what stops one entry
        // being counted twice: two goal triggers firing in the same physics
        // step, or a goal and an overtime vaporise landing together.
        if (!scoringBall.CanMove)
        {
            return false;
        }

        scoringBall.StopBall();
        goalsConceded[concedingTeam] = GetGoalsConceded(concedingTeam) + 1;

        scoringTeam = ResolveScoringTeam(
            concedingTeam,
            scoringBall.LastTouchTeam);
        if (scoringTeam != AirFootyTeam.None)
        {
            goalsScored[scoringTeam] = GetGoalsScored(scoringTeam) + 1;
        }

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
        ReactCrowdsToGoal(scoringTeam, concedingTeam);
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

    private AirFootyTeam ResolveScoringTeam(
        AirFootyTeam concedingTeam,
        AirFootyTeam lastTouchTeam)
    {
        if (lastTouchTeam != AirFootyTeam.None &&
            lastTouchTeam != concedingTeam)
        {
            return lastTouchTeam;
        }

        // In head-to-head, a defender's own goal belongs to the only opponent.
        // In FFA no single opponent receives credit for a defender's own goal.
        if (fourPlayerMode)
        {
            return AirFootyTeam.None;
        }

        foreach (AirFootyTeam candidate in activeTeams)
        {
            if (candidate != AirFootyTeam.None && candidate != concedingTeam)
            {
                return candidate;
            }
        }

        return concedingTeam == AirFootyTeam.Blue
            ? AirFootyTeam.Red
            : AirFootyTeam.Blue;
    }

    private void ReactCrowdsToGoal(
        AirFootyTeam scoringTeam,
        AirFootyTeam concedingTeam)
    {
        foreach (AirFootyCrowdDirector director in crowdDirectors)
        {
            director?.ReactToGoal(scoringTeam, concedingTeam, fourPlayerMode);
        }
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

    private IEnumerator KickoffSequence(string bannerMessage, Color? bannerColor)
    {
        while (Mathf.Approximately(Time.timeScale, 0f))
        {
            yield return null;
        }

        SetPlayerControl(false);
        PrepareAllBalls();

        if (!string.IsNullOrEmpty(bannerMessage))
        {
            scoreUI?.ShowMatchStatus(bannerMessage, bannerColor, true);
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

            Action<AirFootyTeam, AirFootyTeam> lethalHandler =
                (victim, owner) => HandleLethalContact(capturedBall, victim, owner);
            lethalHandlers.Add(candidate, lethalHandler);
            candidate.LethalContact += lethalHandler;
        }
    }

    /// <summary>
    /// An armed ball reached a striker. The victim concedes exactly one goal,
    /// which credits the team that armed the ball, then respawns unless that
    /// concede knocked them out.
    /// </summary>
    private void HandleLethalContact(
        BallController3D ball,
        AirFootyTeam victimTeam,
        AirFootyTeam ownerTeam)
    {
        if (gameOver ||
            ball == null ||
            victimTeam == AirFootyTeam.None ||
            eliminatedTeams.Contains(victimTeam) ||
            vaporisedTeams.Contains(victimTeam))
        {
            return;
        }

        Vector3 burstPosition =
            strikersByTeam.TryGetValue(victimTeam, out GameObject striker) && striker != null
                ? striker.transform.position
                : ball.transform.position;
        Color victimColor = AirFootyTeamMember3D.ColorFor(victimTeam);

        AirFootyFeedbackUtility.SpawnVaporiseBurst(burstPosition, victimColor);
        AirFootyWorldPopup.Spawn(
            burstPosition + Vector3.up * 1.2f,
            $"{AirFootyTeamMember3D.DisplayName(victimTeam)} VAPORISED!",
            victimColor);
        PlayVaporise();
        cameraFx?.AddTrauma(vaporiseCameraTrauma);

        vaporisedTeams.Add(victimTeam);
        SetStrikerPresent(victimTeam, false);

        // Routed through the normal scoring path so elimination, the scoreboard
        // and the ball reset all behave exactly as they do for a scored goal.
        // LastTouchTeam is still the owner because a lethal contact deliberately
        // does not register as a touch.
        GoalConceded(victimTeam, ball);

        if (gameOver || eliminatedTeams.Contains(victimTeam))
        {
            vaporisedTeams.Remove(victimTeam);
            return;
        }

        StartCoroutine(RespawnStriker(victimTeam));
    }

    private IEnumerator RespawnStriker(AirFootyTeam team)
    {
        yield return new WaitForSecondsRealtime(respawnDelaySeconds);

        vaporisedTeams.Remove(team);
        if (gameOver || eliminatedTeams.Contains(team))
        {
            yield break;
        }

        if (strikersByTeam.TryGetValue(team, out GameObject striker) &&
            striker != null &&
            strikerHomePositions.TryGetValue(team, out Vector3 home))
        {
            Rigidbody body = striker.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = home;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            striker.transform.position = home;
        }

        SetStrikerPresent(team, true);
    }

    /// <summary>
    /// Hides and disarms a striker without deactivating it. Toggling the
    /// GameObject would re-run OnEnable on the Input System wiring, so a
    /// respawning striker is suppressed piece by piece instead. Permanent
    /// elimination still deactivates outright.
    /// </summary>
    private void SetStrikerPresent(AirFootyTeam team, bool present)
    {
        if (!strikersByTeam.TryGetValue(team, out GameObject striker) || striker == null)
        {
            return;
        }

        foreach (Renderer renderer in striker.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = present;
        }
        foreach (Collider collider in striker.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = present;
        }

        // Coming back mid kick-off must not hand control over early; the kick-off
        // itself re-enables everyone when it finishes.
        bool control = present && kickoffRoutine == null && !gameOver;

        PlayerMovement3D movement = striker.GetComponent<PlayerMovement3D>();
        if (movement != null && movement.enabled)
        {
            movement.SetMovementEnabled(control);
        }
        PlayerActions3D actions = striker.GetComponent<PlayerActions3D>();
        if (actions != null && actions.enabled)
        {
            AirFootyTeamMember3D member =
                striker.GetComponent<AirFootyTeamMember3D>();
            bool humanControlled = member != null &&
                                   member.Team == humanControlledTeam;
            actions.SetActionsEnabled(control && humanControlled);
        }
        AirFootySideAI3D sideAI = striker.GetComponent<AirFootySideAI3D>();
        if (sideAI != null && sideAI.enabled)
        {
            sideAI.SetMovementEnabled(control);
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
        // One clock per jumbotron, so a two goal arena finds two and a four goal
        // arena finds four with no branch here.
        matchClocks = GetComponentsInChildren<AirFootyMatchClock3D>(true);
        crowdDirectors = GetComponentsInChildren<AirFootyCrowdDirector>(true);
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

            // Captured before anything moves, so an overtime respawn can put the
            // striker back exactly where it started.
            strikersByTeam[team] = strikerObject;
            strikerHomePositions[team] = strikerObject.transform.position;

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
                    Debug.LogError(
                        $"AirFooty striker {strikerObject.name} is missing its authored {nameof(PlayerMovement3D)}.",
                        strikerObject);
                    continue;
                }
                ResolveTeamAreaDepths(
                    team,
                    out float apexDepth,
                    out float goalLineDepth);
                movement.ConfigureTeamArea(
                    fourPlayerMode,
                    transform.position,
                    apexDepth,
                    goalLineDepth);
                movement.enabled = true;
                if (actions == null)
                {
                    Debug.LogError(
                        $"AirFooty striker {strikerObject.name} is missing its authored {nameof(PlayerActions3D)}.",
                        strikerObject);
                    continue;
                }
                actions.enabled = true;

                AirFootyStrikeMotor3D strikeMotor =
                    strikerObject.GetComponent<AirFootyStrikeMotor3D>();
                strikeMotor?.ConfigureTeam(team);
            }
            else
            {
                // Every striker keeps PlayerActions alive as its charge-bank
                // presenter. AI input remains disabled below, but Update can
                // still show charges being spent and recharged.
                if (actions == null)
                {
                    Debug.LogError(
                        $"AirFooty striker {strikerObject.name} is missing its authored {nameof(PlayerActions3D)}.",
                        strikerObject);
                    continue;
                }
                if (movement != null)
                {
                    movement.SetMovementEnabled(false);
                    movement.enabled = false;
                }
                actions.enabled = true;
                actions.SetActionsEnabled(false);
                sideAI = EnsureSideAI(strikerObject, team);
                if (sideAI != null)
                {
                    sideAI.enabled = true;
                }
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
            Debug.LogError(
                $"AirFooty striker {target.name} is missing its authored {nameof(AirFootySideAI3D)}.",
                target);
            return null;
        }
        ResolveTeamAreaDepths(
            team,
            out float apexDepth,
            out float goalLineDepth);
        sideAI.Configure(
            team,
            balls,
            goals,
            this,
            fourPlayerMode,
            transform.position,
            apexDepth,
            goalLineDepth);
        return sideAI;
    }

    /// <summary>
    /// Keeps every four-player movement semicircle the same size and the same
    /// distance in front of its goal. The side goals are authored farther from
    /// the arena centre than the blue/red goals, so centre-relative values pull
    /// Green and Gold inward and make their areas look misplaced.
    /// </summary>
    private void ResolveTeamAreaDepths(
        AirFootyTeam team,
        out float apexDepth,
        out float goalLineDepth)
    {
        apexDepth = centreRingRadius;
        goalLineDepth = teamGoalLineDepth;
        if (!fourPlayerMode || !TryResolveGoalDepth(team, out float teamDepth))
        {
            return;
        }

        float referenceDepth = 0f;
        int referenceCount = 0;
        if (TryResolveGoalDepth(AirFootyTeam.Blue, out float blueDepth))
        {
            referenceDepth += blueDepth;
            referenceCount++;
        }
        if (TryResolveGoalDepth(AirFootyTeam.Red, out float redDepth))
        {
            referenceDepth += redDepth;
            referenceCount++;
        }
        if (referenceCount == 0)
        {
            return;
        }

        float goalOffset = teamDepth - referenceDepth / referenceCount;
        apexDepth += goalOffset;
        goalLineDepth += goalOffset;
    }

    private bool TryResolveGoalDepth(AirFootyTeam team, out float depth)
    {
        Vector3 homeDirection = AirFootyTeamMember3D.HomeDirection(team);
        for (int i = 0; i < goals.Length; i++)
        {
            GoalZone3D goal = goals[i];
            if (goal == null || goal.OwnerTeam != team)
            {
                continue;
            }

            depth = Vector3.Dot(
                goal.transform.position - transform.position,
                homeDirection);
            return depth > 0f;
        }

        depth = 0f;
        return false;
    }

    private static AirFootyTeam ConfigureTeamMember(
        GameObject target,
        AirFootyTeam fallback)
    {
        AirFootyTeam inferred = AirFootyTeamMember3D.InferFromHierarchy(target.transform);
        AirFootyTeamMember3D member = target.GetComponent<AirFootyTeamMember3D>();
        if (member == null)
        {
            Debug.LogError(
                $"AirFooty striker {target.name} is missing its authored {nameof(AirFootyTeamMember3D)}.",
                target);
            return fallback;
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
            if (controller != null && controller.enabled && !IsSuppressed(controller))
            {
                controller.SetMovementEnabled(enabled);
            }
        }
        foreach (PlayerActions3D controller in playerActionControllers)
        {
            if (controller != null && controller.enabled && !IsSuppressed(controller))
            {
                AirFootyTeamMember3D member =
                    controller.GetComponent<AirFootyTeamMember3D>();
                bool humanControlled = member != null &&
                                       member.Team == humanControlledTeam;
                controller.SetActionsEnabled(enabled && humanControlled);
            }
        }
        foreach (AIPlayer3D controller in legacyAiControllers)
        {
            if (controller != null && controller.enabled && !IsSuppressed(controller))
            {
                controller.SetMovementEnabled(enabled);
            }
        }
        foreach (AirFootySideAI3D controller in sideAiControllers)
        {
            if (controller != null && controller.enabled && !IsSuppressed(controller))
            {
                controller.SetMovementEnabled(enabled);
            }
        }
    }

    /// <summary>
    /// True while this striker is mid-respawn. Without it a goal reset would hand
    /// control straight back to a player who is still vaporised.
    /// </summary>
    private bool IsSuppressed(Component controller)
    {
        if (vaporisedTeams.Count == 0)
        {
            return false;
        }

        AirFootyTeamMember3D member =
            controller.GetComponent<AirFootyTeamMember3D>();
        return member != null && vaporisedTeams.Contains(member.Team);
    }

    private void PlayVaporise()
    {
        if (feedbackAudio == null)
        {
            return;
        }

        feedbackAudio.pitch = 0.85f;
        feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.VaporiseClip);
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
        AirFootyCinemachineCameraRig cameraRig =
            GetComponent<AirFootyCinemachineCameraRig>();
        Camera mainCamera = cameraRig != null
            ? cameraRig.OutputCamera
            : AirFootyCameraLookup.FindDisplayCamera();
        if (mainCamera != null)
        {
            AudioListener listener = mainCamera.GetComponent<AudioListener>();
            if (listener == null)
            {
                Debug.LogError("AirFooty display camera is missing its authored AudioListener.", mainCamera);
            }
            else
            {
                listener.enabled = true;
            }

            cameraFx = mainCamera.GetComponent<AirFootyCameraFx>();
            if (cameraFx == null)
            {
                Debug.LogError("AirFooty display camera is missing its authored AirFootyCameraFx.", mainCamera);
            }
        }

        feedbackAudio = GetComponent<AudioSource>();
        if (feedbackAudio == null)
        {
            Debug.LogError("AirFooty arena root is missing its authored feedback AudioSource.", this);
        }
        else
        {
            feedbackAudio.playOnAwake = false;
            feedbackAudio.spatialBlend = 0f;
            feedbackAudio.volume = 0.65f;
        }
    }

    private void ValidateAuthoredFeedbackAssets()
    {
        if (goalBurstPrefab == null || worldPopupPrefab == null ||
            pulseWavePrefab == null || ballHoverPrefab == null ||
            vaporiseBurstPrefab == null || impactClip == null ||
            goalClip == null || countdownClip == null || vaporiseClip == null)
        {
            Debug.LogError(
                $"{nameof(GameManager3D)} on {name} has missing authored AirFooty feedback assets. " +
                "Check the authored AirFooty game prefab and feedback assets.",
                this);
        }
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
        overtimeTriggerSeconds = Mathf.Max(10f, overtimeTriggerSeconds);
        clockAlertSeconds = Mathf.Clamp(
            clockAlertSeconds,
            1f,
            overtimeTriggerSeconds);
        respawnDelaySeconds = Mathf.Max(0f, respawnDelaySeconds);
        centreRingRadius = Mathf.Max(0.1f, centreRingRadius);
        teamGoalLineDepth = Mathf.Max(
            centreRingRadius + 0.5f,
            teamGoalLineDepth);
        ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
        returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
    }
}
