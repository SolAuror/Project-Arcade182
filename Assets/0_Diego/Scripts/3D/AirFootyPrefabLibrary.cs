using UnityEngine;

/// <summary>
/// Runtime access to serialized Air Footy UI/FX source prefabs. The active
/// arena configures this library from authored Inspector references.
/// </summary>
public static class AirFootyPrefabLibrary
{
    private static GameObject goalBurst;
    private static GameObject worldPopup;
    private static GameObject pulseWave;
    private static GameObject ballHover;

    public static void Configure(
        GameObject goalBurstPrefab,
        GameObject worldPopupPrefab,
        GameObject pulseWavePrefab,
        GameObject ballHoverPrefab)
    {
        goalBurst = goalBurstPrefab;
        worldPopup = worldPopupPrefab;
        pulseWave = pulseWavePrefab;
        ballHover = ballHoverPrefab;
    }

    public static GameObject InstantiateGoalBurst(Vector3 position) =>
        Instantiate(goalBurst, position, Quaternion.identity);

    public static GameObject InstantiateWorldPopup(Vector3 position) =>
        Instantiate(worldPopup, position, Quaternion.identity);

    public static GameObject InstantiatePulseWave(Transform parent, string instanceName)
    {
        GameObject instance = Instantiate(
            pulseWave,
            Vector3.zero,
            Quaternion.identity);
        if (instance == null)
        {
            return null;
        }

        instance.name = instanceName;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.up * 0.045f;
        return instance;
    }

    public static GameObject InstantiateBallHover(Transform parent)
    {
        GameObject instance = Instantiate(
            ballHover,
            Vector3.zero,
            Quaternion.identity);
        if (instance == null)
        {
            return null;
        }

        instance.name = "AirFooty Ball Hover";
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static GameObject Instantiate(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        return prefab != null
            ? Object.Instantiate(prefab, position, rotation)
            : null;
    }
}
