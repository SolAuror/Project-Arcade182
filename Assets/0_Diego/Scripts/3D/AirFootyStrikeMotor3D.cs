using UnityEngine;

public enum AirFootyStrikeResult
{
    Unavailable,
    Miss,
    Hit,
    Perfect
}

[DisallowMultipleComponent]
public sealed class AirFootyStrikeMotor3D : MonoBehaviour
{
    private const int QueryCapacity = 8;

    [Header("References")]
    [SerializeField] private BallController3D ball;
    [SerializeField] private Collider strikerCollider;
    [SerializeField] private AirFootyTeam team = AirFootyTeam.Player;

    [Header("Strike Speeds")]
    [SerializeField, Min(0f)] private float tapKickSpeed = 6.5f;
    [SerializeField, Min(0f)] private float fullChargeKickSpeed = 10.5f;
    [SerializeField, Min(0f)] private float perfectKickSpeed = 12f;
    [SerializeField, Range(0f, 1f)] private float tapChargeThreshold = 0.15f;

    [Header("Charge")]
    [SerializeField, Min(0.05f)] private float timeToFullCharge = 0.55f;
    [SerializeField, Min(0f)] private float perfectReleaseWindow = 0.08f;

    [Header("Reach and Recovery")]
    [SerializeField, Min(0f)] private float strikeRangeBeyondSurface = 0.35f;
    [SerializeField, Range(0f, 0.5f)] private float directionToBallBlend = 0.18f;
    [SerializeField, Min(0f)] private float kickCooldown = 0.4f;
    [SerializeField, Min(0f)] private float missRecovery = 0.25f;

    private readonly Collider[] queryResults = new Collider[QueryCapacity];
    private Collider ballCollider;
    private Renderer strikerRenderer;
    private AudioSource feedbackAudio;
    private AirFootyCameraFx cameraFx;
    private float cooldownUntil;
    private float recoveryUntil;

    public float TimeToFullCharge => timeToFullCharge;
    public float PerfectReleaseWindow => perfectReleaseWindow;
    public bool CanBeginCharge =>
        ball != null &&
        ball.CanMove &&
        Time.time >= cooldownUntil &&
        Time.time >= recoveryUntil;

    private void Awake()
    {
        ResolveReferences();
        BuildFeedbackAudio();
    }

    public float GetChargeFraction(float heldSeconds)
    {
        return Mathf.Clamp01(Mathf.Max(0f, heldSeconds) / timeToFullCharge);
    }

    public bool IsPerfectRelease(float heldSeconds)
    {
        float windowStart = Mathf.Max(0f, timeToFullCharge - perfectReleaseWindow);
        return heldSeconds >= windowStart && heldSeconds <= timeToFullCharge;
    }

    public AirFootyStrikeResult TryStrike(Vector3 requestedAim, float heldSeconds)
    {
        if (!CanBeginCharge)
        {
            return AirFootyStrikeResult.Unavailable;
        }

        Vector3 aim = FlattenAndNormalize(requestedAim);
        if (aim == Vector3.zero)
        {
            return AirFootyStrikeResult.Unavailable;
        }

        cooldownUntil = Time.time + kickCooldown;
        BallController3D targetBall = FindBallInStrikeRange(aim);
        if (targetBall == null)
        {
            recoveryUntil = Time.time + missRecovery;
            PlayWhiffFeedback();
            return AirFootyStrikeResult.Miss;
        }

        float charge = GetChargeFraction(heldSeconds);
        bool perfect = IsPerfectRelease(heldSeconds);
        AirFootyTouchType touchType = charge <= tapChargeThreshold
            ? AirFootyTouchType.TapKick
            : AirFootyTouchType.ChargedKick;
        float targetSpeed = perfect
            ? perfectKickSpeed
            : Mathf.Lerp(tapKickSpeed, fullChargeKickSpeed, charge);
        Vector3 strikeDirection = ResolveStrikeDirection(aim, targetBall.transform.position);

        if (!targetBall.ApplyStrike(team, touchType, strikeDirection, targetSpeed))
        {
            return AirFootyStrikeResult.Unavailable;
        }

        PlayHitFeedback(charge, perfect);
        return perfect ? AirFootyStrikeResult.Perfect : AirFootyStrikeResult.Hit;
    }

    private void ResolveReferences()
    {
        if (ball == null)
        {
            ball = FindFirstObjectByType<BallController3D>();
        }
        if (strikerCollider == null)
        {
            strikerCollider = GetComponent<Collider>();
        }

        ballCollider = ball != null ? ball.GetComponent<Collider>() : null;
        strikerRenderer = GetComponentInChildren<Renderer>();
    }

    private BallController3D FindBallInStrikeRange(Vector3 aim)
    {
        if (ball == null || strikerCollider == null || ballCollider == null)
        {
            ResolveReferences();
        }
        if (ball == null || strikerCollider == null || ballCollider == null)
        {
            return null;
        }

        Bounds strikerBounds = strikerCollider.bounds;
        Bounds targetBounds = ballCollider.bounds;
        float strikerRadius = Mathf.Max(strikerBounds.extents.x, strikerBounds.extents.z);
        float ballRadius = Mathf.Max(targetBounds.extents.x, targetBounds.extents.z);
        float forwardOffset = strikerRadius + strikeRangeBeyondSurface * 0.5f;
        float queryRadius = ballRadius + strikeRangeBeyondSurface * 0.5f;
        Vector3 queryCentre = transform.position + aim * forwardOffset;

        int hitCount = Physics.OverlapSphereNonAlloc(
            queryCentre,
            queryRadius,
            queryResults,
            ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = queryResults[i];
            if (hit != null && hit.GetComponentInParent<BallController3D>() == ball)
            {
                return ball;
            }
        }

        return null;
    }

    private Vector3 ResolveStrikeDirection(Vector3 aim, Vector3 ballPosition)
    {
        Vector3 toBall = FlattenAndNormalize(ballPosition - transform.position);
        if (toBall == Vector3.zero || Vector3.Dot(aim, toBall) <= 0f)
        {
            return aim;
        }

        return Vector3.Slerp(aim, toBall, directionToBallBlend).normalized;
    }

    private static Vector3 FlattenAndNormalize(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private void BuildFeedbackAudio()
    {
        feedbackAudio = GetComponent<AudioSource>();
        if (feedbackAudio == null)
        {
            feedbackAudio = gameObject.AddComponent<AudioSource>();
        }

        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0.25f;
        feedbackAudio.dopplerLevel = 0f;
    }

    private void PlayWhiffFeedback()
    {
        if (feedbackAudio == null)
        {
            return;
        }

        feedbackAudio.pitch = 1.5f;
        feedbackAudio.volume = 0.08f;
        feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.ImpactClip);
    }

    private void PlayHitFeedback(float charge, bool perfect)
    {
        if (feedbackAudio != null)
        {
            feedbackAudio.pitch = perfect ? 1.35f : Mathf.Lerp(0.9f, 1.15f, charge);
            feedbackAudio.volume = perfect ? 0.42f : Mathf.Lerp(0.2f, 0.34f, charge);
            feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.ImpactClip);
        }

        if (perfect)
        {
            if (cameraFx == null && Camera.main != null)
            {
                cameraFx = Camera.main.GetComponent<AirFootyCameraFx>();
            }

            cameraFx?.AddTrauma(0.12f);
            if (strikerRenderer != null)
            {
                StartCoroutine(AirFootyFeedbackUtility.FlashRenderer(
                    strikerRenderer,
                    Color.white,
                    0.1f));
            }
        }
    }

    private void OnValidate()
    {
        tapKickSpeed = Mathf.Max(0f, tapKickSpeed);
        fullChargeKickSpeed = Mathf.Max(tapKickSpeed, fullChargeKickSpeed);
        perfectKickSpeed = Mathf.Max(fullChargeKickSpeed, perfectKickSpeed);
        timeToFullCharge = Mathf.Max(0.05f, timeToFullCharge);
        perfectReleaseWindow = Mathf.Clamp(perfectReleaseWindow, 0f, timeToFullCharge);
        strikeRangeBeyondSurface = Mathf.Max(0f, strikeRangeBeyondSurface);
        kickCooldown = Mathf.Max(0f, kickCooldown);
        missRecovery = Mathf.Max(0f, missRecovery);
    }
}
