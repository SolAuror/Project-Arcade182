using UnityEngine;

public enum AirFootyRallyTier
{
    Calm,
    Hot,
    Critical
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BallController3D))]
public sealed class AirFootyRallyDirector : MonoBehaviour
{
    [Header("Alternating Rally")]
    [SerializeField, Min(0.1f)] private float rallyWindowSeconds = 2.4f;
    [SerializeField, Min(2)] private int hotStrikeCount = 2;
    [SerializeField, Min(3)] private int criticalStrikeCount = 4;

    [Header("Tier Speed Caps")]
    [SerializeField, Min(0.1f)] private float calmMaximumSpeed = 10f;
    [SerializeField, Min(0.1f)] private float hotMaximumSpeed = 11f;
    [SerializeField, Min(0.1f)] private float criticalMaximumSpeed = 12f;

    [Header("Tier Presentation")]
    [SerializeField] private Color calmColor = new Color(0.25f, 0.85f, 1f, 0.75f);
    [SerializeField] private Color hotColor = new Color(1f, 0.72f, 0.18f, 0.9f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.96f, 0.78f, 1f);

    private BallController3D ball;
    private Light rallyGlow;
    private AirFootyTeam lastDeliberateTeam;
    private float lastDeliberateTime = float.NegativeInfinity;
    private int alternatingStrikeCount;

    public AirFootyRallyTier CurrentTier { get; private set; }
    public int AlternatingStrikeCount => alternatingStrikeCount;

    private void Awake()
    {
        ball = GetComponent<BallController3D>();
        BuildGlow();
    }

    private void OnEnable()
    {
        ball.DeliberateStrike += HandleDeliberateStrike;
        ball.Stalled += ResetRally;
        ball.ShotSequenceReset += ResetRally;
        ball.PlayStopped += ResetRally;
    }

    private void Start()
    {
        ApplyTier(AirFootyRallyTier.Calm, false);
    }

    private void OnDisable()
    {
        ball.DeliberateStrike -= HandleDeliberateStrike;
        ball.Stalled -= ResetRally;
        ball.ShotSequenceReset -= ResetRally;
        ball.PlayStopped -= ResetRally;
    }

    private void Update()
    {
        if (alternatingStrikeCount > 0 &&
            Time.time - lastDeliberateTime > rallyWindowSeconds)
        {
            ResetRally();
        }

        if (rallyGlow != null && rallyGlow.enabled)
        {
            float pulse = Mathf.Sin(Time.unscaledTime * 14f) * 0.5f + 0.5f;
            float baseIntensity =
                CurrentTier == AirFootyRallyTier.Critical ? 1.25f : 0.55f;
            rallyGlow.intensity = baseIntensity + pulse * 0.45f;
        }
    }

    private void HandleDeliberateStrike(
        AirFootyTeam team,
        AirFootyTouchType touchType)
    {
        bool alternated =
            lastDeliberateTeam != AirFootyTeam.None &&
            team != lastDeliberateTeam &&
            Time.time - lastDeliberateTime <= rallyWindowSeconds;
        alternatingStrikeCount = alternated
            ? alternatingStrikeCount + 1
            : 1;
        lastDeliberateTeam = team;
        lastDeliberateTime = Time.time;

        AirFootyRallyTier tier =
            alternatingStrikeCount >= criticalStrikeCount
                ? AirFootyRallyTier.Critical
                : alternatingStrikeCount >= hotStrikeCount
                    ? AirFootyRallyTier.Hot
                    : AirFootyRallyTier.Calm;
        ApplyTier(tier, true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AirFootyPlaytestTelemetry.Instance?.RecordRallyProgress(
            tier,
            alternatingStrikeCount);
#endif
    }

    private void ApplyTier(AirFootyRallyTier tier, bool announceChange)
    {
        bool changed = CurrentTier != tier;
        CurrentTier = tier;

        float speedCap;
        Color color;
        switch (tier)
        {
            case AirFootyRallyTier.Hot:
                speedCap = hotMaximumSpeed;
                color = hotColor;
                break;
            case AirFootyRallyTier.Critical:
                speedCap = criticalMaximumSpeed;
                color = criticalColor;
                break;
            default:
                speedCap = calmMaximumSpeed;
                color = calmColor;
                break;
        }

        ball.SetRallyPresentation(speedCap, color);
        if (rallyGlow != null)
        {
            rallyGlow.enabled = tier != AirFootyRallyTier.Calm;
            rallyGlow.color = color;
            rallyGlow.range =
                tier == AirFootyRallyTier.Critical ? 2.4f : 1.7f;
        }

        if (!changed || !announceChange)
        {
            return;
        }

        if (tier == AirFootyRallyTier.Hot)
        {
            AirFootyWorldPopup.Spawn(
                transform.position + Vector3.up * 0.85f,
                "RALLY HOT",
                hotColor);
        }
        else if (tier == AirFootyRallyTier.Critical)
        {
            AirFootyWorldPopup.Spawn(
                transform.position + Vector3.up * 0.85f,
                "CRITICAL!",
                criticalColor);
        }
    }

    private void ResetRally()
    {
        alternatingStrikeCount = 0;
        lastDeliberateTeam = AirFootyTeam.None;
        lastDeliberateTime = float.NegativeInfinity;
        ApplyTier(AirFootyRallyTier.Calm, false);
    }

    private void BuildGlow()
    {
        Transform authoredGlow = transform.Find("Rally Heat Glow");
        GameObject glowObject = authoredGlow != null
            ? authoredGlow.gameObject
            : new GameObject("Rally Heat Glow");
        glowObject.transform.SetParent(transform, false);
        rallyGlow = glowObject.GetComponent<Light>();
        if (rallyGlow == null)
        {
            rallyGlow = glowObject.AddComponent<Light>();
        }
        rallyGlow.type = LightType.Point;
        rallyGlow.shadows = LightShadows.None;
        rallyGlow.enabled = false;
    }

    private void OnValidate()
    {
        rallyWindowSeconds = Mathf.Max(0.1f, rallyWindowSeconds);
        hotStrikeCount = Mathf.Max(2, hotStrikeCount);
        criticalStrikeCount = Mathf.Max(hotStrikeCount + 1, criticalStrikeCount);
        calmMaximumSpeed = Mathf.Max(0.1f, calmMaximumSpeed);
        hotMaximumSpeed = Mathf.Max(calmMaximumSpeed, hotMaximumSpeed);
        criticalMaximumSpeed = Mathf.Max(hotMaximumSpeed, criticalMaximumSpeed);
    }
}
