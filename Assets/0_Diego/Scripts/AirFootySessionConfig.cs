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

    public static void Configure(
        AirFootyGameMode mode,
        AirFootyTeam humanTeam)
    {
        Mode = mode;
        HumanTeam = IsTeamAvailable(mode, humanTeam)
            ? humanTeam
            : AirFootyTeam.Blue;
        HasSelection = true;
    }

    public static void Clear()
    {
        Mode = AirFootyGameMode.TwoPlayer;
        HumanTeam = AirFootyTeam.Blue;
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
