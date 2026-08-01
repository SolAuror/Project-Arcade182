#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AirFootyPlaytestTelemetry : MonoBehaviour
{
    public enum Team
    {
        Player,
        AI
    }

    public enum DeliberateStrikeType
    {
        TapKick,
        ChargedKick,
        DashKick
    }

    private const float DefaultGoalDefenseBoundaryX = -5.5f;
    private const float CentralGoalDefenseHalfWidth = 1f;
    private const string CsvFileName = "AirFooty_PlaytestTelemetry.csv";

    private static AirFootyPlaytestTelemetry instance;

    private GameManager3D gameManager;
    private PlayerMovement3D player;
    private AIPlayer3D ai;
    private BallController3D ballController;
    private Rigidbody ballBody;
    private float activeMatchSeconds;
    private float playerGoalDefenseSeconds;
    private float aiGoalDefenseSeconds;
    private float playerCentralGoalDefenseSeconds;
    private float aiCentralGoalDefenseSeconds;
    private int previousPlayerScore;
    private int previousAiScore;
    private int playerDeliberateStrikes;
    private int aiDeliberateStrikes;
    private int passivePlayerContacts;
    private int passiveAiContacts;
    private int wallRebounds;
    private int currentRallyContacts;
    private int longestRallyContacts;
    private int goalsPrecededByDeliberateStrike;
    private readonly int[] aiStateEntries = new int[6];
    private int aiNearPostPlans;
    private int aiFarPostPlans;
    private int aiBankPlans;
    private int aiStrikeAttempts;
    private int aiStrikeHits;
    private int aiStrikeMisses;
    private int directShotGoals;
    private int oneBankShotGoals;
    private int multiBankShotGoals;
    private int playerSaves;
    private int aiSaves;
    private int ownGoalsAfterStrike;
    private int longestAlternatingRally;
    private int hotRallyStrikes;
    private int criticalRallyStrikes;
    private bool lastStrikerContactWasDeliberate;
    private bool reportWritten;
    private Team lastDeliberateTeam;
    private float suppressMatchingPassiveContactUntil;
    private float lastObservedTouchTime = float.NegativeInfinity;

    public static AirFootyPlaytestTelemetry Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameManager3D manager = FindFirstObjectByType<GameManager3D>();
        if (manager == null || FindFirstObjectByType<AirFootyPlaytestTelemetry>() != null)
        {
            return;
        }

        GameObject telemetryObject = new GameObject("AirFooty Playtest Telemetry (Development)");
        telemetryObject.transform.SetParent(manager.transform, false);
        telemetryObject.AddComponent<AirFootyPlaytestTelemetry>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ResolveSceneObjects();
    }

    private void Start()
    {
        if (gameManager == null)
        {
            enabled = false;
            return;
        }

        previousPlayerScore = gameManager.PlayerScore;
        previousAiScore = gameManager.AiScore;
    }

    private void Update()
    {
        if (gameManager == null)
        {
            return;
        }

        CaptureDeliberateStrike();
        CaptureScoreChanges();

        if (IsActivePlay())
        {
            activeMatchSeconds += Time.unscaledDeltaTime;
            if (player != null && player.transform.position.x <= DefaultGoalDefenseBoundaryX)
            {
                playerGoalDefenseSeconds += Time.unscaledDeltaTime;
                if (Mathf.Abs(player.transform.position.z) <= CentralGoalDefenseHalfWidth)
                {
                    playerCentralGoalDefenseSeconds += Time.unscaledDeltaTime;
                }
            }
            if (ai != null && ai.transform.position.x >= -DefaultGoalDefenseBoundaryX)
            {
                aiGoalDefenseSeconds += Time.unscaledDeltaTime;
                if (Mathf.Abs(ai.transform.position.z) <= CentralGoalDefenseHalfWidth)
                {
                    aiCentralGoalDefenseSeconds += Time.unscaledDeltaTime;
                }
            }
        }

        if (gameManager.IsGameOver && !reportWritten)
        {
            WriteMatchReport();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void RecordDeliberateStrike(Team team, DeliberateStrikeType strikeType)
    {
        if (team == Team.Player)
        {
            playerDeliberateStrikes++;
        }
        else
        {
            aiDeliberateStrikes++;
        }

        lastStrikerContactWasDeliberate = true;
        lastDeliberateTeam = team;
        suppressMatchingPassiveContactUntil =
            Time.unscaledTime + Mathf.Max(0.05f, Time.fixedDeltaTime * 1.5f);
        RegisterRallyContact();
    }

    public void RecordAIStateTransition(AirFootyAIState state)
    {
        int stateIndex = (int)state;
        if (stateIndex >= 0 && stateIndex < aiStateEntries.Length)
        {
            aiStateEntries[stateIndex]++;
        }
    }

    public void RecordAIShotPlan(AirFootyAIShotType shotType)
    {
        switch (shotType)
        {
            case AirFootyAIShotType.NearPost:
                aiNearPostPlans++;
                break;
            case AirFootyAIShotType.FarPost:
                aiFarPostPlans++;
                break;
            case AirFootyAIShotType.Bank:
                aiBankPlans++;
                break;
        }
    }

    public void RecordAIStrikeResult(AirFootyStrikeResult result)
    {
        aiStrikeAttempts++;
        if (result == AirFootyStrikeResult.Hit ||
            result == AirFootyStrikeResult.Perfect)
        {
            aiStrikeHits++;
        }
        else
        {
            aiStrikeMisses++;
        }
    }

    public void RecordShotOutcome(
        AirFootyShotClassification classification,
        AirFootyTeam shootingTeam,
        AirFootyTeam scoringOrDefendingTeam)
    {
        switch (classification)
        {
            case AirFootyShotClassification.Direct:
                directShotGoals++;
                break;
            case AirFootyShotClassification.OneBank:
                oneBankShotGoals++;
                break;
            case AirFootyShotClassification.MultiBank:
                multiBankShotGoals++;
                break;
            case AirFootyShotClassification.Save:
                if (scoringOrDefendingTeam == AirFootyTeam.Player)
                {
                    playerSaves++;
                }
                else if (scoringOrDefendingTeam == AirFootyTeam.AI)
                {
                    aiSaves++;
                }
                return;
        }

        if (shootingTeam != AirFootyTeam.None &&
            scoringOrDefendingTeam != AirFootyTeam.None &&
            shootingTeam != scoringOrDefendingTeam)
        {
            ownGoalsAfterStrike++;
        }
    }

    public void RecordRallyProgress(
        AirFootyRallyTier tier,
        int alternatingStrikes)
    {
        longestAlternatingRally =
            Mathf.Max(longestAlternatingRally, alternatingStrikes);
        if (tier == AirFootyRallyTier.Hot)
        {
            hotRallyStrikes++;
        }
        else if (tier == AirFootyRallyTier.Critical)
        {
            criticalRallyStrikes++;
        }
    }

    internal void RecordBallCollision(Collision collision)
    {
        PlayerMovement3D playerContact = collision.collider.GetComponentInParent<PlayerMovement3D>();
        if (playerContact != null)
        {
            if (IsRecentDeliberateTouch(AirFootyTeam.Player))
            {
                return;
            }
            if (ShouldSuppressPassiveContact(Team.Player))
            {
                return;
            }

            passivePlayerContacts++;
            lastStrikerContactWasDeliberate = false;
            RegisterRallyContact();
            return;
        }

        AIPlayer3D aiContact = collision.collider.GetComponentInParent<AIPlayer3D>();
        if (aiContact != null)
        {
            if (IsRecentDeliberateTouch(AirFootyTeam.AI))
            {
                return;
            }
            if (ShouldSuppressPassiveContact(Team.AI))
            {
                return;
            }

            passiveAiContacts++;
            lastStrikerContactWasDeliberate = false;
            RegisterRallyContact();
            return;
        }

        if (collision.contactCount > 0 &&
            Mathf.Abs(collision.GetContact(0).normal.y) < 0.5f)
        {
            wallRebounds++;
        }
    }

    private void ResolveSceneObjects()
    {
        gameManager = FindFirstObjectByType<GameManager3D>();
        player = FindFirstObjectByType<PlayerMovement3D>();
        ai = FindFirstObjectByType<AIPlayer3D>();

        ballController = FindFirstObjectByType<BallController3D>();
        if (ballController == null)
        {
            return;
        }

        ballBody = ballController.GetComponent<Rigidbody>();
        if (ballController.GetComponent<AirFootyTelemetryCollisionProbe>() == null)
        {
            ballController.gameObject.AddComponent<AirFootyTelemetryCollisionProbe>();
        }
    }

    private bool IsActivePlay()
    {
        return !gameManager.IsGameOver &&
               !gameManager.IsKickoffRunning &&
               ballBody != null &&
               ballBody.linearVelocity.sqrMagnitude > 0.0025f;
    }

    private void CaptureScoreChanges()
    {
        int playerScore = gameManager.PlayerScore;
        int aiScore = gameManager.AiScore;

        while (previousPlayerScore < playerScore)
        {
            RecordGoal();
            previousPlayerScore++;
        }

        while (previousAiScore < aiScore)
        {
            RecordGoal();
            previousAiScore++;
        }
    }

    private void CaptureDeliberateStrike()
    {
        if (ballController == null ||
            float.IsNegativeInfinity(ballController.LastTouchTime) ||
            Mathf.Approximately(ballController.LastTouchTime, lastObservedTouchTime))
        {
            return;
        }

        lastObservedTouchTime = ballController.LastTouchTime;
        if (ballController.LastTouchType == AirFootyTouchType.None ||
            ballController.LastTouchType == AirFootyTouchType.Passive)
        {
            return;
        }

        Team team = ballController.LastTouchTeam == AirFootyTeam.Player
            ? Team.Player
            : Team.AI;
        DeliberateStrikeType strikeType = ballController.LastTouchType switch
        {
            AirFootyTouchType.ChargedKick => DeliberateStrikeType.ChargedKick,
            AirFootyTouchType.DashKick => DeliberateStrikeType.DashKick,
            _ => DeliberateStrikeType.TapKick
        };
        RecordDeliberateStrike(team, strikeType);
    }

    private void RecordGoal()
    {
        if (lastStrikerContactWasDeliberate)
        {
            goalsPrecededByDeliberateStrike++;
        }

        currentRallyContacts = 0;
        lastStrikerContactWasDeliberate = false;
    }

    private void RegisterRallyContact()
    {
        currentRallyContacts++;
        longestRallyContacts = Mathf.Max(longestRallyContacts, currentRallyContacts);
    }

    private bool ShouldSuppressPassiveContact(Team team)
    {
        return team == lastDeliberateTeam &&
               Time.unscaledTime <= suppressMatchingPassiveContactUntil;
    }

    private bool IsRecentDeliberateTouch(AirFootyTeam team)
    {
        return ballController != null &&
               ballController.LastTouchTeam == team &&
               ballController.LastTouchType != AirFootyTouchType.None &&
               ballController.LastTouchType != AirFootyTouchType.Passive &&
               Time.time - ballController.LastTouchTime <= 0.1f;
    }

    private void WriteMatchReport()
    {
        reportWritten = true;

        float defensePercent = activeMatchSeconds > 0f
            ? playerGoalDefenseSeconds / activeMatchSeconds * 100f
            : 0f;
        float aiDefensePercent = activeMatchSeconds > 0f
            ? aiGoalDefenseSeconds / activeMatchSeconds * 100f
            : 0f;
        float centralDefensePercent = activeMatchSeconds > 0f
            ? playerCentralGoalDefenseSeconds / activeMatchSeconds * 100f
            : 0f;
        float aiCentralDefensePercent = activeMatchSeconds > 0f
            ? aiCentralGoalDefenseSeconds / activeMatchSeconds * 100f
            : 0f;
        int totalGoals = gameManager.PlayerScore + gameManager.AiScore;

        string summary =
            $"AirFooty telemetry | duration={activeMatchSeconds:0.00}s, " +
            $"score={gameManager.PlayerScore}-{gameManager.AiScore}, " +
            $"deliberate={playerDeliberateStrikes}/{aiDeliberateStrikes} (player/AI), " +
            $"passive={passivePlayerContacts}/{passiveAiContacts} (player/AI), " +
            $"wallRebounds={wallRebounds}, longestRallyContacts={longestRallyContacts}, " +
            $"goalDefense={defensePercent:0.0}/{aiDefensePercent:0.0}% (player/AI), " +
            $"centralDefense={centralDefensePercent:0.0}/{aiCentralDefensePercent:0.0}% (player/AI), " +
            $"goalsAfterDeliberate={goalsPrecededByDeliberateStrike}/{totalGoals}, " +
            $"shotGoals={directShotGoals}/{oneBankShotGoals}/{multiBankShotGoals} (direct/1-bank/multi), " +
            $"saves={playerSaves}/{aiSaves} (player/AI), ownGoals={ownGoalsAfterStrike}, " +
            $"alternatingRally={longestAlternatingRally} longest, " +
            $"heatStrikes={hotRallyStrikes}/{criticalRallyStrikes} (hot/critical), " +
            $"aiPlans={aiNearPostPlans}/{aiFarPostPlans}/{aiBankPlans} (near/far/bank), " +
            $"aiStrikes={aiStrikeHits}/{aiStrikeAttempts} hits, " +
            $"aiStates={string.Join("/", aiStateEntries)}";
        Debug.Log(summary, this);

        string path = Path.Combine(Application.persistentDataPath, CsvFileName);
        try
        {
            bool writeHeader = !File.Exists(path);
            using StreamWriter writer = new StreamWriter(path, true);
            if (writeHeader)
            {
                writer.WriteLine(
                    "timestamp_utc,duration_seconds,player_goals,ai_goals," +
                    "player_deliberate_strikes,ai_deliberate_strikes," +
                    "player_passive_contacts,ai_passive_contacts,wall_rebounds," +
                    "longest_rally_contacts,player_goal_defense_percent," +
                    "goals_preceded_by_deliberate_strike," +
                    "ai_near_post_plans,ai_far_post_plans,ai_bank_plans," +
                    "ai_strike_attempts,ai_strike_hits,ai_strike_misses," +
                    "ai_recover_entries,ai_intercept_entries,ai_acquire_entries," +
                    "ai_charge_entries,ai_strike_entries,ai_cooldown_entries," +
                    "ai_goal_defense_percent,player_central_goal_defense_percent," +
                    "ai_central_goal_defense_percent,direct_shot_goals," +
                    "one_bank_shot_goals,multi_bank_shot_goals," +
                    "player_saves,ai_saves,own_goals_after_strike," +
                    "longest_alternating_rally,hot_rally_strikes," +
                    "critical_rally_strikes");
            }

            writer.WriteLine(string.Join(",",
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                activeMatchSeconds.ToString("0.00", CultureInfo.InvariantCulture),
                gameManager.PlayerScore,
                gameManager.AiScore,
                playerDeliberateStrikes,
                aiDeliberateStrikes,
                passivePlayerContacts,
                passiveAiContacts,
                wallRebounds,
                longestRallyContacts,
                defensePercent.ToString("0.0", CultureInfo.InvariantCulture),
                goalsPrecededByDeliberateStrike,
                aiNearPostPlans,
                aiFarPostPlans,
                aiBankPlans,
                aiStrikeAttempts,
                aiStrikeHits,
                aiStrikeMisses,
                aiStateEntries[(int)AirFootyAIState.Recover],
                aiStateEntries[(int)AirFootyAIState.PredictIntercept],
                aiStateEntries[(int)AirFootyAIState.AcquireShotLane],
                aiStateEntries[(int)AirFootyAIState.Charge],
                aiStateEntries[(int)AirFootyAIState.Strike],
                aiStateEntries[(int)AirFootyAIState.Cooldown],
                aiDefensePercent.ToString("0.0", CultureInfo.InvariantCulture),
                centralDefensePercent.ToString("0.0", CultureInfo.InvariantCulture),
                aiCentralDefensePercent.ToString("0.0", CultureInfo.InvariantCulture),
                directShotGoals,
                oneBankShotGoals,
                multiBankShotGoals,
                playerSaves,
                aiSaves,
                ownGoalsAfterStrike,
                longestAlternatingRally,
                hotRallyStrikes,
                criticalRallyStrikes));

            Debug.Log($"AirFooty telemetry appended to '{path}'.", this);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"AirFooty telemetry could not write '{path}': {exception.Message}",
                this);
        }
    }
}

[DisallowMultipleComponent]
internal sealed class AirFootyTelemetryCollisionProbe : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        AirFootyPlaytestTelemetry.Instance?.RecordBallCollision(collision);
    }
}
#endif
