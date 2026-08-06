using TMPro;
using UnityEngine;

/// <summary>
/// The match clock as a piece of the stadium rather than a HUD element: the
/// countdown is rendered onto the jumbotron screens behind the goals, and the
/// board's own neon trim carries the alert state.
///
/// The text is authored (see AirFootyOvertimeAuthoring); this only pushes state
/// into it. One of these lives on every SD_Jumbotron, so a two goal arena has
/// two boards and a four goal arena has four, with no per-arena wiring.
/// </summary>
[DisallowMultipleComponent]
public sealed class AirFootyMatchClock3D : MonoBehaviour
{
    [Header("Authored Parts")]
    [SerializeField] private TMP_Text clockText;
    [Tooltip("The board's neon trim, flashed on each second of the run-in.")]
    [SerializeField] private AirFootyNeonPulse trim;

    [Header("Colour")]
    [SerializeField] private Color idleColor = new Color(0.3f, 0.95f, 1f, 1f);
    [SerializeField] private Color alertColor = new Color(1f, 0.72f, 0.12f, 1f);
    [SerializeField] private Color overtimeColor = new Color(1f, 0.18f, 0.25f, 1f);

    [Header("Behaviour")]
    [Tooltip(
        "Turn the board to face the display camera. Off by default so the clock " +
        "stays part of the stadium; switch it on only if a board reads badly.")]
    [SerializeField] private bool faceDisplayCamera;

    private Camera displayCamera;
    private Quaternion authoredRotation;
    private int lastWholeSecond = -1;
    private bool overtimeShown;

    private void Awake()
    {
        if (clockText == null)
        {
            clockText = GetComponentInChildren<TMP_Text>(true);
        }
        if (trim == null)
        {
            trim = GetComponent<AirFootyNeonPulse>();
        }
        if (clockText != null)
        {
            authoredRotation = clockText.transform.rotation;
        }
    }

    private void LateUpdate()
    {
        if (!faceDisplayCamera || clockText == null)
        {
            return;
        }

        if (displayCamera == null)
        {
            displayCamera = AirFootyCameraLookup.FindDisplayCamera();
            if (displayCamera == null)
            {
                clockText.transform.rotation = authoredRotation;
                return;
            }
        }

        // TMP renders down its local +Z, so look away from the camera to end up
        // facing it.
        Vector3 toCamera = clockText.transform.position - displayCamera.transform.position;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            clockText.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }
    }

    /// <summary>Blank the board, for matches running without a clock.</summary>
    public void SetHidden()
    {
        overtimeShown = false;
        lastWholeSecond = -1;
        if (clockText != null)
        {
            clockText.text = string.Empty;
        }
    }

    /// <summary>
    /// Shows the remaining time as M:SS. While <paramref name="alert"/> is set the
    /// board turns amber and flashes its trim once a second.
    /// </summary>
    public void SetTime(float secondsRemaining, bool alert)
    {
        if (clockText == null || overtimeShown)
        {
            return;
        }

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
        if (totalSeconds == lastWholeSecond)
        {
            return;
        }

        // Only on the tick: assigning TMP text rebuilds the mesh, and there are
        // up to four of these boards in the arena.
        lastWholeSecond = totalSeconds;
        clockText.text = $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
        clockText.color = alert ? alertColor : idleColor;

        if (alert)
        {
            trim?.Pulse(alertColor);
        }
    }

    /// <summary>The clock has run out and the ball is live.</summary>
    public void SetOvertime()
    {
        overtimeShown = true;
        if (clockText != null)
        {
            clockText.text = "OVERTIME";
            clockText.color = overtimeColor;
        }
        trim?.Pulse(overtimeColor);
    }
}
