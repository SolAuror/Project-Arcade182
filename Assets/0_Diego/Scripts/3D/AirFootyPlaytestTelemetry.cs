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
    private const string CsvFileName = "AirFooty_PlaytestTelemetry.csv";

    private static AirFootyPlaytestTelemetry instance;

    private GameManager3D gameManager;
    private PlayerMovement3D player;
    private BallController3D ballController;
    private Rigidbody ballBody;
    private float activeMatchSeconds;
    private float playerGoalDefenseSeconds;
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
        int totalGoals = gameManager.PlayerScore + gameManager.AiScore;

        string summary =
            $"AirFooty telemetry | duration={activeMatchSeconds:0.00}s, " +
            $"score={gameManager.PlayerScore}-{gameManager.AiScore}, " +
            $"deliberate={playerDeliberateStrikes}/{aiDeliberateStrikes} (player/AI), " +
            $"passive={passivePlayerContacts}/{passiveAiContacts} (player/AI), " +
            $"wallRebounds={wallRebounds}, longestRallyContacts={longestRallyContacts}, " +
            $"goalDefense={defensePercent:0.0}%, " +
            $"goalsAfterDeliberate={goalsPrecededByDeliberateStrike}/{totalGoals}";
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
                    "goals_preceded_by_deliberate_strike");
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
                goalsPrecededByDeliberateStrike));

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
