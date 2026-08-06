using UnityEngine;

/// <summary>
/// Which physical side of the stadium bowl a grandstand section sits on.
/// Four-player supporters sit opposite their players: Blue East, Red West,
/// Green South, and Gold North.
/// </summary>
public enum AirFootyStandSide
{
    North,
    East,
    South,
    West
}

/// <summary>
/// Crowd allegiance. Blue and Red are today's player/AI pairing; Gold and
/// Green exist so the planned square arena with four goals can be dressed
/// without authoring new crowd assets.
/// </summary>
public enum AirFootyCrowdTeam
{
    Neutral,
    Blue,
    Red,
    Gold,
    Green
}

/// <summary>
/// Team colours shared by the crowd, the neon trim, and the goal-end lighting
/// so a team reads the same everywhere in the bowl. Blue, Red, and Neutral
/// match the colours already used by the strikers and the pitch markings.
/// </summary>
public static class AirFootyCrowdPalette
{
    public static Color Of(AirFootyCrowdTeam team)
    {
        switch (team)
        {
            case AirFootyCrowdTeam.Blue:
                return new Color(0.1f, 0.55f, 1f);
            case AirFootyCrowdTeam.Red:
                return new Color(1f, 0.18f, 0.25f);
            case AirFootyCrowdTeam.Gold:
                return new Color(1f, 0.72f, 0.16f);
            case AirFootyCrowdTeam.Green:
                return new Color(0.22f, 1f, 0.55f);
            default:
                return new Color(0.18f, 0.9f, 1f);
        }
    }
}

/// <summary>
/// One authored bay of crowd clones. The block deliberately has no Update of
/// its own: it only publishes its seats, and the owning
/// <see cref="AirFootyCrowdStand"/> animates them in a single loop. A full
/// bowl therefore costs one Update per section rather than one per clone.
/// </summary>
[DisallowMultipleComponent]
public class AirFootyCrowdBlock : MonoBehaviour
{
    [SerializeField] private Transform[] seats = new Transform[0];
    [SerializeField] private AirFootyCrowdTeam team = AirFootyCrowdTeam.Neutral;

    public Transform[] Seats => seats;

    public AirFootyCrowdTeam Team => team;
}
