using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AirFootyTeamMember3D : MonoBehaviour
{
    [SerializeField] private AirFootyTeam team;

    public AirFootyTeam Team => team;

    public void Configure(AirFootyTeam value)
    {
        team = value;
    }

    public static AirFootyTeam InferFromHierarchy(Transform source)
    {
        Transform current = source;
        while (current != null)
        {
            string objectName = current.name;
            if (Contains(objectName, "Blue")) return AirFootyTeam.Blue;
            if (Contains(objectName, "Red")) return AirFootyTeam.Red;
            if (Contains(objectName, "Green")) return AirFootyTeam.Green;
            if (Contains(objectName, "Gold") || Contains(objectName, "Yellow"))
            {
                return AirFootyTeam.Gold;
            }

            current = current.parent;
        }

        return AirFootyTeam.None;
    }

    public static string DisplayName(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Blue => "BLUE",
            AirFootyTeam.Red => "RED",
            AirFootyTeam.Green => "GREEN",
            AirFootyTeam.Gold => "GOLD",
            _ => "UNKNOWN"
        };
    }

    public static Color ColorFor(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Blue => new Color(0.12f, 0.62f, 1f, 1f),
            AirFootyTeam.Red => new Color(1f, 0.18f, 0.25f, 1f),
            AirFootyTeam.Green => new Color(0.16f, 0.9f, 0.36f, 1f),
            AirFootyTeam.Gold => new Color(1f, 0.72f, 0.12f, 1f),
            _ => Color.white
        };
    }

    public static Vector3 HomeDirection(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Blue => Vector3.left,
            AirFootyTeam.Red => Vector3.right,
            AirFootyTeam.Green => Vector3.forward,
            AirFootyTeam.Gold => Vector3.back,
            _ => Vector3.zero
        };
    }

    private static bool Contains(string value, string token)
    {
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
