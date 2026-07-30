using UnityEngine;

/// <summary>
/// Root of an AirFooty set-dressing bowl. It carries the footprint the bowl was
/// authored against, so resizing the arena is a documented change rather than
/// guesswork, and it gathers the pieces a match needs to talk to.
///
/// The bowl is purely cosmetic: no colliders, no gameplay references, nothing
/// the ball or the strikers can touch. Deleting it changes nothing but the view.
///
/// Two layouts ship with the kit:
/// <list type="bullet">
/// <item>2-goal: inset 10.5 x 6.0, Blue behind the player goal at -X, Red behind
/// the AI goal at +X, both long sides split at the halfway line.</item>
/// <item>4-goal square: inset 10.5 x 10.5, one team per side (Blue North, Red
/// East, Gold South, Green West).</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public class AirFootyStadiumRig : MonoBehaviour
{
    [Header("Footprint (arena centre to the front rail of a stand)")]
    [SerializeField, Min(1f)] private float standInsetX = 10.5f;
    [SerializeField, Min(1f)] private float standInsetZ = 6f;
    [Tooltip("How many goals the arena this bowl was dressed for has.")]
    [SerializeField, Range(2, 4)] private int goalCount = 2;

    [Header("Contents")]
    [SerializeField] private AirFootyCrowdDirector director;
    [SerializeField] private AirFootyCrowdStand[] stands = new AirFootyCrowdStand[0];
    [Tooltip("One anchor per goal end, for jumbotrons, portals, and tunnels.")]
    [SerializeField] private Transform[] goalEndAnchors = new Transform[0];

    public float StandInsetX => standInsetX;

    public float StandInsetZ => standInsetZ;

    public int GoalCount => goalCount;

    public bool IsSquareLayout => Mathf.Approximately(standInsetX, standInsetZ);

    public AirFootyCrowdDirector Director => director;

    public AirFootyCrowdStand[] Stands => stands;

    public Transform[] GoalEndAnchors => goalEndAnchors;

    /// <summary>Total authored crowd clones, handy for a quick budget check.</summary>
    public int CrowdSize
    {
        get
        {
            int total = 0;
            for (int index = 0; index < stands.Length; index++)
            {
                if (stands[index] != null)
                {
                    total += stands[index].SeatCount;
                }
            }

            return total;
        }
    }

    private void OnValidate()
    {
        if (goalCount == 4 && !IsSquareLayout)
        {
            Debug.LogWarning(
                $"{name} is set to four goals but its footprint is {standInsetX} x {standInsetZ}. " +
                "A four-goal arena wants a square bowl so every team gets an equal side.",
                this);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws the footprint so the clearance between the bowl and the arena is
    /// visible while the arena is being resized.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Vector3 centre = transform.position;
        Gizmos.color = new Color(0.18f, 0.9f, 1f, 0.85f);
        Gizmos.DrawWireCube(
            centre,
            new Vector3(standInsetX * 2f, 0.02f, standInsetZ * 2f));

        Gizmos.color = new Color(1f, 0.72f, 0.16f, 0.5f);
        for (int index = 0; index < goalEndAnchors.Length; index++)
        {
            if (goalEndAnchors[index] != null)
            {
                Gizmos.DrawWireSphere(goalEndAnchors[index].position, 0.6f);
            }
        }
    }
#endif
}
