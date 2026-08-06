using UnityEngine;

/// <summary>
/// Turns match events into crowd behaviour.
///
/// By default it watches <see cref="GameManager3D"/>'s public score properties
/// rather than hooking the goal path, so the existing gameplay scripts and the
/// arena itself need no changes to get a celebrating crowd.
///
/// The match manager drives goals directly so FFA can celebrate every crowd
/// except the team that conceded. Score polling remains as a 1v1 fallback.
/// </summary>
[DisallowMultipleComponent]
public class AirFootyCrowdDirector : MonoBehaviour
{
    [Header("Match Source")]
    [Tooltip("Leave empty to drive the crowd entirely from script calls.")]
    [SerializeField] private GameManager3D gameManager;
    [SerializeField] private bool findGameManagerOnStart = true;

    [Header("Bowl")]
    [SerializeField] private AirFootyCrowdStand[] stands = new AirFootyCrowdStand[0];
    [SerializeField] private AirFootyNeonPulse[] arenaNeon = new AirFootyNeonPulse[0];

    [Header("Team Mapping")]
    [SerializeField] private AirFootyCrowdTeam playerTeam = AirFootyCrowdTeam.Blue;
    [SerializeField] private AirFootyCrowdTeam aiTeam = AirFootyCrowdTeam.Red;
    [Tooltip("Neutral sections applaud any goal at reduced intensity.")]
    [SerializeField] private bool neutralStandsJoinIn = true;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float goalCelebrationSeconds = 3.5f;
    [SerializeField, Min(0f)] private float winCelebrationSeconds = 8f;
    [SerializeField, Range(0f, 1f)] private float neutralIntensity = 0.6f;

    private int lastPlayerScore;
    private int lastAiScore;
    private bool winCelebrated;

    private void Start()
    {
        if (gameManager == null && findGameManagerOnStart)
        {
            gameManager = FindFirstObjectByType<GameManager3D>();
        }

        if (gameManager != null)
        {
            // Seed from the live score so a mid-match spawn does not fire.
            lastPlayerScore = gameManager.PlayerScore;
            lastAiScore = gameManager.AiScore;
        }
    }

    /// <summary>
    /// Celebrate in every section that belongs to <paramref name="scoringTeam"/>,
    /// slump the sections that support anyone else.
    /// </summary>
    public void CelebrateForTeam(AirFootyCrowdTeam scoringTeam)
    {
        CelebrateForTeam(scoringTeam, goalCelebrationSeconds);
    }

    /// <summary>
    /// Celebrate for <paramref name="seconds"/> in every section that belongs to
    /// <paramref name="scoringTeam"/>, slump the sections that support anyone
    /// else.
    /// </summary>
    public void CelebrateForTeam(AirFootyCrowdTeam scoringTeam, float seconds)
    {
        for (int index = 0; index < stands.Length; index++)
        {
            AirFootyCrowdStand stand = stands[index];
            if (stand == null)
            {
                continue;
            }

            if (stand.Team == scoringTeam)
            {
                stand.Celebrate(seconds);
            }
            else if (stand.Team == AirFootyCrowdTeam.Neutral)
            {
                if (neutralStandsJoinIn)
                {
                    stand.Celebrate(seconds * neutralIntensity);
                }
            }
            else
            {
                stand.Slump();
            }
        }

        PulseArenaNeon(AirFootyCrowdPalette.Of(scoringTeam));
    }

    /// <summary>
    /// A goal went in at <paramref name="concededSide"/>: everyone but that
    /// side's supporters celebrates. Intended for the four-goal arena, where a
    /// side belongs to exactly one team. On the current two-goal layout each
    /// long side is shared, so prefer
    /// <see cref="CelebrateForTeam(AirFootyCrowdTeam)"/> there.
    /// </summary>
    public void CelebrateAgainstSide(AirFootyStandSide concededSide)
    {
        CelebrateAgainstTeam(TeamOnSide(concededSide));
    }

    /// <summary>
    /// Every team except the one whose goal was scored celebrates. This is the
    /// FFA rule and does not depend on where a team's supporters are seated.
    /// </summary>
    public void CelebrateAgainstTeam(AirFootyCrowdTeam concedingTeam)
    {

        for (int index = 0; index < stands.Length; index++)
        {
            AirFootyCrowdStand stand = stands[index];
            if (stand == null)
            {
                continue;
            }

            if (stand.Team == concedingTeam && concedingTeam != AirFootyCrowdTeam.Neutral)
            {
                stand.Slump();
            }
            else if (stand.Team == AirFootyCrowdTeam.Neutral)
            {
                if (neutralStandsJoinIn)
                {
                    stand.Celebrate(goalCelebrationSeconds * neutralIntensity);
                }
            }
            else
            {
                stand.Celebrate(goalCelebrationSeconds);
            }
        }

        PulseArenaNeon(AirFootyCrowdPalette.Of(AirFootyCrowdTeam.Neutral));
    }

    public void ReactToGoal(
        AirFootyTeam scoringTeam,
        AirFootyTeam concedingTeam,
        bool fourPlayerMode)
    {
        if (fourPlayerMode)
        {
            CelebrateAgainstTeam(ToCrowdTeam(concedingTeam));
        }
        else
        {
            AirFootyCrowdTeam crowdTeam = ToCrowdTeam(scoringTeam);
            if (crowdTeam != AirFootyCrowdTeam.Neutral)
            {
                CelebrateForTeam(crowdTeam);
            }
        }

        // Goal routing is immediate. Synchronise the polling fallback so it
        // does not fire the same celebration again on the next Update.
        if (gameManager != null)
        {
            lastPlayerScore = gameManager.PlayerScore;
            lastAiScore = gameManager.AiScore;
        }
    }

    private static AirFootyCrowdTeam ToCrowdTeam(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Blue => AirFootyCrowdTeam.Blue,
            AirFootyTeam.Red => AirFootyCrowdTeam.Red,
            AirFootyTeam.Green => AirFootyCrowdTeam.Green,
            AirFootyTeam.Gold => AirFootyCrowdTeam.Gold,
            _ => AirFootyCrowdTeam.Neutral
        };
    }

    /// <summary>Settle the whole bowl back to its idle breath.</summary>
    public void CalmDown()
    {
        for (int index = 0; index < stands.Length; index++)
        {
            stands[index]?.CalmDown();
        }
    }

    /// <summary>The team whose sections sit on <paramref name="side"/>.</summary>
    public AirFootyCrowdTeam TeamOnSide(AirFootyStandSide side)
    {
        for (int index = 0; index < stands.Length; index++)
        {
            AirFootyCrowdStand stand = stands[index];
            if (stand != null && stand.Side == side && stand.Team != AirFootyCrowdTeam.Neutral)
            {
                return stand.Team;
            }
        }

        return AirFootyCrowdTeam.Neutral;
    }

    private void Update()
    {
        if (gameManager == null)
        {
            return;
        }

        int playerScore = gameManager.PlayerScore;
        int aiScore = gameManager.AiScore;

        if (playerScore > lastPlayerScore)
        {
            CelebrateForTeam(playerTeam);
        }
        else if (aiScore > lastAiScore)
        {
            CelebrateForTeam(aiTeam);
        }

        lastPlayerScore = playerScore;
        lastAiScore = aiScore;

        if (!winCelebrated && gameManager.IsGameOver)
        {
            winCelebrated = true;
            CelebrateForTeam(
                playerScore > aiScore ? playerTeam : aiTeam,
                winCelebrationSeconds);
        }
    }

    private void PulseArenaNeon(Color color)
    {
        for (int index = 0; index < arenaNeon.Length; index++)
        {
            arenaNeon[index]?.Pulse(color);
        }
    }
}
