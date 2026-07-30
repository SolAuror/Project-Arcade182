using UnityEngine;

/// <summary>
/// Animates one grandstand section's crowd: a gentle idle breath while play
/// runs, a jumping celebration with a wave travelling along the stand when its
/// team scores, and a slump when it concedes.
///
/// Section authoring contract (the authoring tool bakes this; keep it if you
/// hand-edit a section):
/// <list type="bullet">
/// <item>local +Z faces the pitch,</item>
/// <item>local +X runs along the stand,</item>
/// <item>seats rest at their authored local transform.</item>
/// </list>
/// Every pose is derived from that resting transform, so nudging a clone in the
/// inspector moves where it animates from. Crowd blocks must not be rotated
/// relative to the section, because the vertical hop is applied in block-local
/// space.
///
/// One section is one team. The current two-goal arena puts a Blue and a Red
/// section on each long side; the planned square arena gives a whole side to
/// one team. Nothing here needs to change between the two.
/// </summary>
[DisallowMultipleComponent]
public class AirFootyCrowdStand : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private AirFootyStandSide side = AirFootyStandSide.North;
    [SerializeField] private AirFootyCrowdTeam team = AirFootyCrowdTeam.Neutral;
    [SerializeField] private AirFootyCrowdBlock[] blocks = new AirFootyCrowdBlock[0];
    [SerializeField] private AirFootyNeonPulse neon;

    [Header("Idle")]
    [SerializeField, Min(0f)] private float idleBobHeight = 0.035f;
    [SerializeField, Min(0f)] private float idleBobSpeed = 1.6f;
    [SerializeField, Range(0f, 20f)] private float idleSwayDegrees = 4f;

    [Header("Celebration")]
    [SerializeField, Min(0f)] private float celebrateSeconds = 3.5f;
    [SerializeField, Min(0f)] private float hopHeight = 0.34f;
    [SerializeField, Min(0f)] private float hopSpeed = 7.5f;
    [SerializeField, Range(0f, 45f)] private float celebrateLeanDegrees = 14f;
    [SerializeField, Min(0f)] private float waveLift = 0.22f;
    [SerializeField, Min(0.1f)] private float waveWidth = 2.6f;
    [SerializeField, Min(0f)] private float waveSpeed = 9f;

    [Header("Concede")]
    [SerializeField, Min(0f)] private float slumpSeconds = 2f;
    [SerializeField, Min(0f)] private float slumpDrop = 0.12f;
    [SerializeField, Range(0f, 45f)] private float slumpLeanDegrees = 12f;

    [Header("Mood Blend")]
    [SerializeField, Min(0.01f)] private float moodAttackSeconds = 0.18f;
    [SerializeField, Min(0.01f)] private float moodReleaseSeconds = 0.8f;

    private Transform[] seats;
    private Vector3[] restPositions;
    private Quaternion[] restRotations;
    private float[] phases;
    private float[] runCoordinates;
    private float runStart;
    private float runEnd;

    private float celebrateWeight;
    private float slumpWeight;
    private float celebrateRemaining;
    private float slumpRemaining;
    private float celebrateStartTime;
    private bool waveRunsForward = true;

    public AirFootyStandSide Side => side;

    public AirFootyCrowdTeam Team => team;

    public int SeatCount => seats != null ? seats.Length : 0;

    private void Awake()
    {
        CacheSeats();
    }

    /// <summary>Jump, wave, and lean back for the default duration.</summary>
    public void Celebrate()
    {
        Celebrate(celebrateSeconds);
    }

    /// <summary>
    /// Jump, wave, and lean back for <paramref name="seconds"/>. A longer
    /// celebration already in flight is never cut short.
    /// </summary>
    public void Celebrate(float seconds)
    {
        float requested = Mathf.Max(0f, seconds);
        if (requested <= celebrateRemaining)
        {
            return;
        }

        celebrateRemaining = requested;
        celebrateStartTime = Time.unscaledTime;
        slumpRemaining = 0f;
        // Alternating the wave direction stops repeat goals looking canned.
        waveRunsForward = !waveRunsForward;
        neon?.Pulse(AirFootyCrowdPalette.Of(team));
    }

    /// <summary>Sit down and lean forward for the default duration.</summary>
    public void Slump()
    {
        Slump(slumpSeconds);
    }

    /// <summary>Sit down and lean forward for <paramref name="seconds"/>.</summary>
    public void Slump(float seconds)
    {
        slumpRemaining = Mathf.Max(slumpRemaining, Mathf.Max(0f, seconds));
        celebrateRemaining = 0f;
    }

    /// <summary>Drop straight back to the idle breath, e.g. on kick-off.</summary>
    public void CalmDown()
    {
        celebrateRemaining = 0f;
        slumpRemaining = 0f;
    }

    private void Update()
    {
        if (seats == null || seats.Length == 0)
        {
            return;
        }

        // Unscaled time keeps the crowd alive through the goal hit-stop.
        float delta = Time.unscaledDeltaTime;
        celebrateRemaining = Mathf.Max(0f, celebrateRemaining - delta);
        slumpRemaining = Mathf.Max(0f, slumpRemaining - delta);
        celebrateWeight = Approach(celebrateWeight, celebrateRemaining > 0f ? 1f : 0f, delta);
        slumpWeight = Approach(slumpWeight, slumpRemaining > 0f ? 1f : 0f, delta);

        float time = Time.unscaledTime;
        float wavePosition = WavePosition(time);

        for (int index = 0; index < seats.Length; index++)
        {
            Transform seat = seats[index];
            if (seat == null)
            {
                continue;
            }

            float phase = phases[index] * Mathf.PI * 2f;
            float wave = 0f;
            if (celebrateWeight > 0.001f)
            {
                float offset = (runCoordinates[index] - wavePosition) / waveWidth;
                wave = Mathf.Exp(-offset * offset);
            }

            float idleBob = Mathf.Sin(time * idleBobSpeed + phase) * idleBobHeight;
            float hop = Mathf.Abs(Mathf.Sin(time * hopSpeed + phase)) * hopHeight;
            float rise = Mathf.Lerp(idleBob, hop + wave * waveLift, celebrateWeight)
                         - slumpDrop * slumpWeight;

            float lean = slumpLeanDegrees * slumpWeight
                         - celebrateLeanDegrees * celebrateWeight * (0.65f + 0.35f * wave);
            float yaw = idleSwayDegrees
                        * Mathf.Sin(time * idleBobSpeed * 0.7f + phase * 1.7f)
                        * (1f + celebrateWeight * 1.5f);

            seat.localPosition = restPositions[index] + new Vector3(0f, rise, 0f);
            seat.localRotation = restRotations[index] * Quaternion.Euler(lean, yaw, 0f);
        }
    }

    /// <summary>
    /// A single wave sweep per celebration, started just off the end of the
    /// stand so the first clones are already rising when it becomes visible.
    /// </summary>
    private float WavePosition(float time)
    {
        float travelled = (time - celebrateStartTime) * waveSpeed;
        return waveRunsForward
            ? runStart - waveWidth + travelled
            : runEnd + waveWidth - travelled;
    }

    private float Approach(float current, float target, float delta)
    {
        float seconds = target > current ? moodAttackSeconds : moodReleaseSeconds;
        return Mathf.MoveTowards(current, target, delta / Mathf.Max(0.01f, seconds));
    }

    private void CacheSeats()
    {
        int count = 0;
        for (int block = 0; block < blocks.Length; block++)
        {
            count += CountSeats(blocks[block]);
        }

        seats = new Transform[count];
        restPositions = new Vector3[count];
        restRotations = new Quaternion[count];
        phases = new float[count];
        runCoordinates = new float[count];
        runStart = 0f;
        runEnd = 0f;

        int index = 0;
        float minimum = float.MaxValue;
        float maximum = float.MinValue;
        for (int block = 0; block < blocks.Length; block++)
        {
            if (blocks[block] == null)
            {
                continue;
            }

            Transform[] blockSeats = blocks[block].Seats;
            for (int seatIndex = 0; seatIndex < blockSeats.Length; seatIndex++)
            {
                Transform seat = blockSeats[seatIndex];
                if (seat == null)
                {
                    continue;
                }

                Vector3 rest = seat.localPosition;
                // The wave travels along the section's local X axis.
                float run = transform.InverseTransformPoint(seat.position).x;

                seats[index] = seat;
                restPositions[index] = rest;
                restRotations[index] = seat.localRotation;
                runCoordinates[index] = run;
                phases[index] = StableHash(run, rest.z, index);
                minimum = Mathf.Min(minimum, run);
                maximum = Mathf.Max(maximum, run);
                index++;
            }
        }

        if (index == 0)
        {
            Debug.LogWarning(
                $"{name} has no crowd seats. Re-run Tools > AirFooty > Author Stadium Set Dressing Kit.",
                this);
            return;
        }

        runStart = minimum;
        runEnd = maximum;
    }

    private static int CountSeats(AirFootyCrowdBlock block)
    {
        if (block == null)
        {
            return 0;
        }

        int count = 0;
        Transform[] blockSeats = block.Seats;
        for (int index = 0; index < blockSeats.Length; index++)
        {
            if (blockSeats[index] != null)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// A repeatable per-seat offset in the 0-1 range. Deriving it from the
    /// authored position keeps neighbours out of lockstep without storing a
    /// random value on every clone.
    /// </summary>
    private static float StableHash(float run, float depth, int index)
    {
        float noise = Mathf.Sin(run * 12.9898f + depth * 78.233f + index * 0.017f) * 43758.5453f;
        return noise - Mathf.Floor(noise);
    }

    private void OnValidate()
    {
        waveWidth = Mathf.Max(0.1f, waveWidth);
        moodAttackSeconds = Mathf.Max(0.01f, moodAttackSeconds);
        moodReleaseSeconds = Mathf.Max(0.01f, moodReleaseSeconds);
    }
}
