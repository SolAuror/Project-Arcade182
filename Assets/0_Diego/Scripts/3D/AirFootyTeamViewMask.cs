using UnityEngine;

/// <summary>
/// Marks set dressing as belonging to a team's corner so it can be hidden from
/// that team's camera.
///
/// Each team views the pitch from its own corner, which puts a corner pylon and
/// its grandstand directly between that team's camera and the play area. Rather
/// than move the bowl, the offending piece is tagged with the team whose view it
/// blocks; <see cref="AirFootyCinemachineCameraRig"/> drops the matching layer
/// from the camera's culling mask when that team is selected. Every other team
/// still sees it, so the arena reads as complete from every other angle.
///
/// Culling masks work on layers, not tags, so this assigns a layer rather than
/// carrying the team as data the camera would have to search for each frame.
/// </summary>
[DisallowMultipleComponent]
public sealed class AirFootyTeamViewMask : MonoBehaviour
{
    [Tooltip("The team whose camera this object blocks. It is hidden only from that team.")]
    [SerializeField] private AirFootyTeam hiddenFromTeam = AirFootyTeam.None;

    [Tooltip("Apply to every child renderer as well as this object.")]
    [SerializeField] private bool includeChildren = true;

    public AirFootyTeam HiddenFromTeam => hiddenFromTeam;

    public void Configure(AirFootyTeam team)
    {
        hiddenFromTeam = team;
        Apply();
    }

    private void Awake()
    {
        Apply();
    }

    private void Apply()
    {
        int layer = LayerFor(hiddenFromTeam);
        if (layer < 0)
        {
            return;
        }

        gameObject.layer = layer;
        if (!includeChildren)
        {
            return;
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    /// <summary>
    /// Layer index for a team, or -1 when the project has no such layer. Named
    /// lookup rather than a hard-coded index so a project that reorders its
    /// layers degrades to "visible to everyone" instead of hiding the wrong set.
    /// </summary>
    public static int LayerFor(AirFootyTeam team)
    {
        string layerName = team switch
        {
            AirFootyTeam.Blue => "AirFootyHideBlue",
            AirFootyTeam.Red => "AirFootyHideRed",
            AirFootyTeam.Green => "AirFootyHideGreen",
            AirFootyTeam.Gold => "AirFootyHideGold",
            _ => null
        };

        return layerName == null ? -1 : LayerMask.NameToLayer(layerName);
    }

    /// <summary>
    /// Culling mask that shows everything except the given team's dressing.
    /// </summary>
    public static int CullingMaskFor(AirFootyTeam team)
    {
        int mask = ~0;
        int layer = LayerFor(team);
        if (layer >= 0)
        {
            mask &= ~(1 << layer);
        }

        return mask;
    }
}
