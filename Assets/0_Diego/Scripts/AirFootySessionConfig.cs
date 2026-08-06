public enum AirFootyGameMode
{
    TwoPlayer,
    FourPlayer
}

public static class AirFootySessionConfig
{
    public static AirFootyGameMode Mode { get; private set; } =
        AirFootyGameMode.TwoPlayer;
    public static AirFootyTeam HumanTeam { get; private set; } =
        AirFootyTeam.Blue;
    public static bool HasSelection { get; private set; }

    /// <summary>
    /// Whether the match runs a clock that ends in the overtime contingency.
    /// Optional in two player, mandatory in four player.
    /// </summary>
    public static bool OvertimeEnabled { get; private set; } = true;

    public static void Configure(
        AirFootyGameMode mode,
        AirFootyTeam humanTeam,
        bool overtimeRequested = true)
    {
        Mode = mode;
        HumanTeam = IsTeamAvailable(mode, humanTeam)
            ? humanTeam
            : AirFootyTeam.Blue;
        // Four player elimination always ends in overtime, so the rule cannot be
        // turned off by a stale menu state or a caller that skipped the toggle.
        OvertimeEnabled = mode == AirFootyGameMode.FourPlayer || overtimeRequested;
        HasSelection = true;
    }

    public static void Clear()
    {
        Mode = AirFootyGameMode.TwoPlayer;
        HumanTeam = AirFootyTeam.Blue;
        OvertimeEnabled = true;
        HasSelection = false;
    }

    public static bool IsTeamAvailable(
        AirFootyGameMode mode,
        AirFootyTeam team)
    {
        if (team == AirFootyTeam.Blue || team == AirFootyTeam.Red)
        {
            return true;
        }

        return mode == AirFootyGameMode.FourPlayer &&
               (team == AirFootyTeam.Green || team == AirFootyTeam.Gold);
    }
}
