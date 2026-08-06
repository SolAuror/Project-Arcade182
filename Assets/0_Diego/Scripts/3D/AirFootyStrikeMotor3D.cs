using System.Collections;
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
    private const int PulseWaveSegments = 44;

    [Header("References")]
    [SerializeField] private BallController3D ball;
    [SerializeField] private Collider strikerCollider;
    [SerializeField] private AirFootyTeam team = AirFootyTeam.Player;

    [Header("Strike Speeds")]
    [SerializeField, Min(0f)] private float tapKickSpeed = 6.5f;
    [SerializeField, Min(0f)] private float fullChargeKickSpeed = 10.5f;
    [SerializeField, Min(0f)] private float perfectKickSpeed = 12f;
    [SerializeField, Min(0f)] private float dashKickSpeed = 10.5f;
    [SerializeField, Range(0f, 1f)] private float tapChargeThreshold = 0.15f;

    [Header("Hover Pulse")]
    [SerializeField, Min(0.1f)] private float tapPulseRadius = 1.65f;
    [SerializeField, Min(0.1f)] private float fullPulseRadius = 2.35f;
    [SerializeField, Min(0f)] private float tapPulseImpulse = 3.25f;
    [SerializeField, Min(0f)] private float fullPulseImpulse = 8f;
    [SerializeField, Min(0f)] private float pulseCooldown = 0.06f;
    [SerializeField] private bool emitPulseWave;
    [SerializeField, Min(0.05f)] private float pulseWaveSeconds = 0.24f;
    [SerializeField] private Color aiPulseColor = new Color(1f, 0.18f, 0.08f, 0.95f);

    [Header("Charge")]
    [SerializeField, Min(0.05f)] private float timeToFullCharge = 0.55f;
    [SerializeField, Min(0f)] private float perfectReleaseWindow = 0.14f;
    [SerializeField, Min(0f)] private float perfectReleaseGrace = 0.12f;

    [Header("Reach and Recovery")]
    [SerializeField, Min(0f)] private float strikeRangeBeyondSurface = 0.35f;
    [SerializeField, Range(-1f, 1f)] private float minimumForwardAimDot = 0.05f;
    [SerializeField, Range(0f, 0.5f)] private float directionToBallBlend = 0.18f;
    [SerializeField, Min(0f)] private float kickCooldown = 0.25f;
    [SerializeField, Min(0f)] private float missRecovery = 0.12f;

    private Collider ballCollider;
    private BallController3D[] availableBalls;
    private Renderer strikerRenderer;
    private AudioSource feedbackAudio;
    private AirFootyCameraFx cameraFx;
    private float cooldownUntil;
    private float recoveryUntil;
    private float pulseReadyAt;

    public float TimeToFullCharge => timeToFullCharge;
    public float PerfectReleaseWindow => perfectReleaseWindow;
    public bool CanBeginCharge =>
        FindAvailableBall() != null;
    public bool IsStrikeReady =>
        CanBeginCharge &&
        Time.time >= cooldownUntil &&
        Time.time >= recoveryUntil;
    public bool IsPulseReady =>
        CanBeginCharge &&
        Time.time >= pulseReadyAt;

    private void Awake()
    {
        ResolveReferences();
        BuildFeedbackAudio();
    }

    public void ConfigureTeam(AirFootyTeam configuredTeam)
    {
        team = configuredTeam;
    }

    /// <summary>
    /// Draws a ring when this motor pulses. The human already spawns its own wave
    /// from PlayerActions3D, so this is for AI sides, whose pulses would otherwise
    /// be invisible - which matters in overtime, where a pulse is what arms a ball.
    /// </summary>
    public void SetPulseWaveEmission(bool emit)
    {
        emitPulseWave = emit;
    }

    public float GetChargeFraction(float heldSeconds)
    {
        return Mathf.Clamp01(Mathf.Max(0f, heldSeconds) / timeToFullCharge);
    }

    public bool IsPerfectRelease(float heldSeconds)
    {
        float windowStart = Mathf.Max(0f, timeToFullCharge - perfectReleaseWindow);
        return heldSeconds >= windowStart &&
               heldSeconds <= timeToFullCharge + perfectReleaseGrace;
    }

    public bool IsOvercharged(float heldSeconds)
    {
        return heldSeconds > timeToFullCharge + perfectReleaseGrace;
    }

    public float GetPulseRadius(float chargeFraction)
    {
        return Mathf.Lerp(
            tapPulseRadius,
            fullPulseRadius,
            Mathf.Clamp01(chargeFraction));
    }

    public bool IsBallInPulseRange(float chargeFraction)
    {
        float radius = GetPulseRadius(chargeFraction);
        ResolveAvailableBalls();
        for (int i = 0; i < availableBalls.Length; i++)
        {
            BallController3D candidate = availableBalls[i];
            if (candidate == null || !candidate.CanMove)
            {
                continue;
            }

            Vector3 offset = candidate.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    public AirFootyStrikeResult TryPulse(
        float chargeFraction,
        float radiusMultiplier = 1f,
        float impulseMultiplier = 1f)
    {
        if (!IsPulseReady)
        {
            PlayUnavailableFeedback();
            return AirFootyStrikeResult.Unavailable;
        }

        float charge = Mathf.Clamp01(chargeFraction);
        pulseReadyAt = Time.time + pulseCooldown;
        float radius =
            GetPulseRadius(charge) * Mathf.Max(0f, radiusMultiplier);
        float impulse =
            Mathf.Lerp(tapPulseImpulse, fullPulseImpulse, charge) *
            Mathf.Max(0f, impulseMultiplier);
        if (emitPulseWave)
        {
            StartCoroutine(PlayPulseWave(radius, aiPulseColor));
        }
        AirFootyTouchType touchType = charge <= tapChargeThreshold
            ? AirFootyTouchType.TapKick
            : AirFootyTouchType.ChargedKick;
        ResolveAvailableBalls();
        bool hit = false;
        for (int i = 0; i < availableBalls.Length; i++)
        {
            BallController3D candidate = availableBalls[i];
            if (candidate != null && candidate.ApplyPulse(
                    team,
                    transform.position,
                    radius,
                    impulse,
                    touchType))
            {
                hit = true;
            }
        }

        if (!hit)
        {
            PlayWhiffFeedback();
            return AirFootyStrikeResult.Miss;
        }

        bool fullPower = charge >= 0.98f;
        PlayHitFeedback(charge, fullPower);
        return fullPower
            ? AirFootyStrikeResult.Perfect
            : AirFootyStrikeResult.Hit;
    }

    public bool IsBallInStrikeRange(Vector3 requestedAim)
    {
        Vector3 aim = FlattenAndNormalize(requestedAim);
        return aim != Vector3.zero && FindBallInStrikeRange(aim) != null;
    }

    public AirFootyStrikeResult TryStrike(Vector3 requestedAim, float heldSeconds)
    {
        if (!IsStrikeReady)
        {
            PlayUnavailableFeedback();
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

    public AirFootyStrikeResult TryDashStrike(Vector3 requestedAim)
    {
        if (!IsStrikeReady)
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
            return AirFootyStrikeResult.Miss;
        }

        Vector3 strikeDirection =
            ResolveStrikeDirection(aim, targetBall.transform.position);
        if (!targetBall.ApplyStrike(
                team,
                AirFootyTouchType.DashKick,
                strikeDirection,
                dashKickSpeed))
        {
            return AirFootyStrikeResult.Unavailable;
        }

        PlayHitFeedback(0.85f, false);
        return AirFootyStrikeResult.Hit;
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
        ResolveAvailableBalls();
        strikerRenderer = GetComponentInChildren<Renderer>();
    }

    private void ResolveAvailableBalls()
    {
        Transform root = transform.root;
        availableBalls = root != null
            ? root.GetComponentsInChildren<BallController3D>(false)
            : null;
        if (availableBalls == null || availableBalls.Length == 0)
        {
            availableBalls = FindObjectsByType<BallController3D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }
    }

    private BallController3D FindAvailableBall()
    {
        ResolveAvailableBalls();
        for (int i = 0; i < availableBalls.Length; i++)
        {
            if (availableBalls[i] != null && availableBalls[i].CanMove)
            {
                return availableBalls[i];
            }
        }

        return null;
    }

    private BallController3D FindBallInStrikeRange(Vector3 aim)
    {
        if (strikerCollider == null)
        {
            ResolveReferences();
        }
        if (strikerCollider == null)
        {
            return null;
        }

        Bounds strikerBounds = strikerCollider.bounds;
        float strikerRadius = Mathf.Max(strikerBounds.extents.x, strikerBounds.extents.z);
        ResolveAvailableBalls();
        BallController3D best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < availableBalls.Length; i++)
        {
            BallController3D candidate = availableBalls[i];
            Collider candidateCollider = candidate != null
                ? candidate.GetComponent<Collider>()
                : null;
            if (candidate == null || !candidate.CanMove || candidateCollider == null)
            {
                continue;
            }

            Bounds targetBounds = candidateCollider.bounds;
            float ballRadius = Mathf.Max(targetBounds.extents.x, targetBounds.extents.z);
            float legalCentreDistance =
                strikerRadius + ballRadius + strikeRangeBeyondSurface;
            Vector3 toBall = targetBounds.center - strikerBounds.center;
            toBall.y = 0f;
            float centreDistance = toBall.magnitude;
            if (centreDistance > legalCentreDistance ||
                centreDistance <= 0.0001f ||
                Vector3.Dot(aim, toBall / centreDistance) < minimumForwardAimDot ||
                centreDistance >= bestDistance)
            {
                continue;
            }

            best = candidate;
            bestDistance = centreDistance;
        }

        return best;
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
            Debug.LogError("AirFooty striker is missing its authored feedback AudioSource.", this);
            return;
        }

        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0.25f;
        feedbackAudio.dopplerLevel = 0f;
    }

    private IEnumerator PlayPulseWave(float radius, Color color)
    {
        Transform authoredWave = transform.Find("AI Hover Pulse Wave");
        if (authoredWave == null)
        {
            yield break;
        }
        GameObject waveObject = authoredWave.gameObject;
        waveObject.SetActive(true);

        LineRenderer wave = waveObject.GetComponent<LineRenderer>();
        if (wave == null)
        {
            Debug.LogError("AirFooty AI Hover Pulse Wave is missing its authored LineRenderer.", waveObject);
            waveObject.SetActive(false);
            yield break;
        }
        wave.useWorldSpace = false;
        wave.loop = true;
        wave.positionCount = PulseWaveSegments;
        wave.startWidth = 0.1f;
        wave.endWidth = 0.1f;
        wave.numCornerVertices = 3;
        wave.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        wave.receiveShadows = false;
        if (wave.sharedMaterial == null)
        {
            Debug.LogError("AirFooty AI Hover Pulse Wave is missing its authored material.", wave);
            waveObject.SetActive(false);
            yield break;
        }

        float startedAt = Time.unscaledTime;
        while (wave != null)
        {
            float progress =
                (Time.unscaledTime - startedAt) /
                Mathf.Max(0.01f, pulseWaveSeconds);
            if (progress >= 1f)
            {
                break;
            }

            float eased = 1f - (1f - progress) * (1f - progress);
            SetRingGeometry(wave, Mathf.Lerp(0.55f, radius, eased));
            Color faded = new Color(
                color.r,
                color.g,
                color.b,
                color.a * (1f - progress));
            wave.startColor = faded;
            wave.endColor = faded;
            yield return null;
        }

        if (waveObject != null)
        {
            waveObject.SetActive(false);
        }
    }

    private static void SetRingGeometry(LineRenderer ring, float radius)
    {
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius));
        }
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

    private void PlayUnavailableFeedback()
    {
        if (feedbackAudio == null)
        {
            return;
        }

        feedbackAudio.pitch = 0.72f;
        feedbackAudio.volume = 0.045f;
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
            if (cameraFx == null)
            {
                Camera displayCamera = AirFootyCameraLookup.FindDisplayCamera();
                if (displayCamera != null)
                {
                    cameraFx = displayCamera.GetComponent<AirFootyCameraFx>();
                }
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
        dashKickSpeed = Mathf.Clamp(dashKickSpeed, tapKickSpeed, perfectKickSpeed);
        tapPulseRadius = Mathf.Max(0.1f, tapPulseRadius);
        fullPulseRadius = Mathf.Max(tapPulseRadius, fullPulseRadius);
        tapPulseImpulse = Mathf.Max(0f, tapPulseImpulse);
        fullPulseImpulse = Mathf.Max(tapPulseImpulse, fullPulseImpulse);
        pulseCooldown = Mathf.Max(0f, pulseCooldown);
        pulseWaveSeconds = Mathf.Max(0.05f, pulseWaveSeconds);
        timeToFullCharge = Mathf.Max(0.05f, timeToFullCharge);
        perfectReleaseWindow = Mathf.Clamp(perfectReleaseWindow, 0f, timeToFullCharge);
        perfectReleaseGrace = Mathf.Max(0f, perfectReleaseGrace);
        strikeRangeBeyondSurface = Mathf.Max(0f, strikeRangeBeyondSurface);
        kickCooldown = Mathf.Max(0f, kickCooldown);
        missRecovery = Mathf.Max(0f, missRecovery);
    }
}
