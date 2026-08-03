using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement3D))]
[RequireComponent(typeof(AirFootyStrikeMotor3D))]
[RequireComponent(typeof(AirFootyAbilityChargeBank3D))]
public sealed class PlayerActions3D : MonoBehaviour
{
    private enum TurboTechnique
    {
        None,
        TurboPulse,
        TurboDash
    }

    private const int PulseRingSegments = 44;
    private const int PipRingSegments = 14;

    [Header("References")]
    [SerializeField] private PlayerMovement3D playerMovement;
    [SerializeField] private AirFootyStrikeMotor3D strikeMotor;
    [SerializeField] private AirFootyAbilityChargeBank3D chargeBank;

    [Header("Pulse Feel")]
    [SerializeField, Min(0f)] private float meaningfulAimThreshold = 0.1f;
    [SerializeField, Min(0.05f)] private float pulseWaveSeconds = 0.24f;
    [SerializeField] private Color pulseTapColor = new Color(0.2f, 0.92f, 1f, 0.9f);
    [SerializeField] private Color pulseFullColor = new Color(1f, 0.88f, 0.35f, 1f);
    [SerializeField] private Color unavailableColor = new Color(0.3f, 0.36f, 0.45f, 0.55f);

    [Header("Dash Aim")]
    [SerializeField, Min(0.1f)] private float dashAimLength = 0.55f;
    [SerializeField, Min(0.01f)] private float dashAimWidth = 0.075f;
    [SerializeField] private Color dashAimColor = new Color(0.15f, 0.85f, 1f, 0.95f);
    [FormerlySerializedAs("comboReadyColor")]
    [SerializeField] private Color turboReadyColor = new Color(1f, 0.35f, 0.92f, 1f);

    [Header("Dash")]
    [SerializeField, Min(0.05f)] private float dashDuration = 0.14f;
    [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2.2f;
    [SerializeField, Min(0f)] private float dashCooldown = 0.17f;
    [SerializeField, Min(0f)] private float missedDashRecovery = 0.06f;
    [SerializeField, Range(0f, 1f)] private float missedDashMoveMultiplier = 0.82f;

    [Header("Turbo Tech")]
    [FormerlySerializedAs("dashPulseMultiplier")]
    [SerializeField, Min(1f)] private float turboPulsePowerMultiplier = 1.15f;
    [FormerlySerializedAs("pulseDashSpeedBoost")]
    [SerializeField, Min(1f)] private float turboDashSpeedMultiplier = 1.2f;
    [FormerlySerializedAs("pulseDashDurationBonus")]
    [SerializeField, Min(0f)] private float turboDashExtensionDuration = 0.06f;
    [FormerlySerializedAs("pulseDashGraceSeconds")]
    [SerializeField, Min(0f)] private float turboDashGraceSeconds = 0.1f;
    [FormerlySerializedAs("pulseDashResumeDuration")]
    [SerializeField, Min(0.05f)] private float turboDashAfterburnerDuration = 0.1f;
    [SerializeField, Min(0f)] private float inputBufferSeconds = 0.18f;

    [Header("Turbo Overdrive FX")]
    [SerializeField, Min(0.05f)] private float turboFxDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float turboTrailTime = 0.3f;
    [SerializeField] private Color turboPulseFxColor = new Color(1f, 0.28f, 0.88f, 1f);
    [SerializeField] private Color turboDashFxColor = new Color(0.1f, 0.9f, 1f, 1f);

    private InputAction pulseAction;
    private InputAction dashAction;
    private LineRenderer pulseRing;
    private LineRenderer dashAimIndicator;
    private LineRenderer turboStabilizer;
    private TrailRenderer[] turboThrusters;
    private Light turboGlow;
    private Renderer turboRenderer;
    private LineRenderer[] chargePips;
    private Material pulseMaterial;
    private bool ownsPulseMaterial;
    private AudioSource feedbackAudio;
    private Vector3 lastAimDirection = Vector3.right;
    private float pulseStartedAt;
    private bool actionsEnabled = true;
    private bool pulseCharging;
    private bool dashing;
    private bool dashConnected;
    private float dashEndsAt;
    private float dashReadyAt;
    private float movementRecoveryEndsAt;
    private float dashQueuedUntil;
    private float pulseQueuedUntil;
    private float turboDashWindowUntil = float.NegativeInfinity;
    private float turboFxUntil = float.NegativeInfinity;
    private bool turboPulsePrimed;
    private TurboTechnique activeTurboTechnique;
    private TurboTechnique turboFxTechnique;

    public Vector3 CurrentAimDirection => lastAimDirection;
    public bool IsCharging => pulseCharging;
    public float ChargeFraction =>
        pulseCharging && strikeMotor != null
            ? strikeMotor.GetChargeFraction(Time.time - pulseStartedAt)
            : 0f;

    private void Awake()
    {
        playerMovement = playerMovement != null
            ? playerMovement
            : GetComponent<PlayerMovement3D>();
        strikeMotor = strikeMotor != null
            ? strikeMotor
            : GetComponent<AirFootyStrikeMotor3D>();
        chargeBank = chargeBank != null
            ? chargeBank
            : GetComponent<AirFootyAbilityChargeBank3D>();

        BuildInputActions();
        BuildPulsePresentation();
        feedbackAudio = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        pulseAction?.Enable();
        dashAction?.Enable();
    }

    private void OnDisable()
    {
        pulseAction?.Disable();
        dashAction?.Disable();
        ClearActionState();
    }

    private void OnDestroy()
    {
        pulseAction?.Dispose();
        dashAction?.Dispose();
        if (ownsPulseMaterial && pulseMaterial != null)
        {
            Destroy(pulseMaterial);
        }
    }

    private void Update()
    {
        UpdateAimDirection();
        UpdateChargePips();
        UpdateTurboPresentation();

        if (!actionsEnabled || Mathf.Approximately(Time.timeScale, 0f))
        {
            if (dashAimIndicator != null)
            {
                dashAimIndicator.enabled = false;
            }
            ClearActionState();
            return;
        }

        UpdateDashState();
        if (dashAction.WasPressedThisFrame())
        {
            dashQueuedUntil = Time.time + inputBufferSeconds;
        }
        if (Time.time <= dashQueuedUntil)
        {
            TryBeginDash();
        }

        if (pulseAction.WasPressedThisFrame())
        {
            pulseQueuedUntil = Time.time + inputBufferSeconds;
        }
        if (!TryActivateTurboDash())
        {
            TryBeginPulseCharge();
        }
        UpdateDashAimIndicator();

        if (!pulseCharging)
        {
            if (pulseRing != null)
            {
                pulseRing.enabled = false;
            }
            return;
        }

        UpdatePulseRing();
        if (pulseAction.WasReleasedThisFrame() || ChargeFraction >= 1f)
        {
            FirePulse();
        }
    }

    public void SetActionsEnabled(bool enabled)
    {
        actionsEnabled = enabled;
        if (!enabled)
        {
            ClearActionState();
            chargeBank?.Refill();
        }
    }

    public void ClearHeldAction()
    {
        pulseCharging = false;
        turboPulsePrimed = false;
        if (pulseRing != null)
        {
            pulseRing.enabled = false;
        }
    }

    private void ClearActionState()
    {
        ClearHeldAction();
        dashing = false;
        dashConnected = false;
        dashEndsAt = float.NegativeInfinity;
        dashReadyAt = float.NegativeInfinity;
        movementRecoveryEndsAt = float.NegativeInfinity;
        dashQueuedUntil = float.NegativeInfinity;
        pulseQueuedUntil = float.NegativeInfinity;
        turboDashWindowUntil = float.NegativeInfinity;
        turboPulsePrimed = false;
        activeTurboTechnique = TurboTechnique.None;
        StopTurboPresentation();
        playerMovement?.CancelDash();
        playerMovement?.SetMoveSpeedMultiplier(1f);
    }

    private void UpdateAimDirection()
    {
        if (playerMovement == null)
        {
            return;
        }

        Vector3 movementAim = playerMovement.CurrentMovementDirection;
        if (movementAim.sqrMagnitude >=
            meaningfulAimThreshold * meaningfulAimThreshold)
        {
            lastAimDirection = movementAim.normalized;
        }
    }

    private void BeginPulseCharge()
    {
        pulseCharging = true;
        turboPulsePrimed = false;
        pulseStartedAt = Time.time;
        if (pulseRing != null)
        {
            pulseRing.enabled = true;
            UpdatePulseRing();
        }
    }

    private void TryBeginPulseCharge()
    {
        if (pulseCharging ||
            Time.time > pulseQueuedUntil ||
            pulseAction == null ||
            !pulseAction.IsPressed() ||
            strikeMotor == null ||
            !strikeMotor.CanBeginCharge ||
            !strikeMotor.IsPulseReady ||
            chargeBank == null ||
            chargeBank.CurrentCharges <= 0)
        {
            return;
        }

        pulseQueuedUntil = float.NegativeInfinity;
        BeginPulseCharge();
    }

    private void FirePulse()
    {
        if (!pulseCharging || chargeBank == null || strikeMotor == null)
        {
            ClearHeldAction();
            return;
        }

        if (!strikeMotor.IsPulseReady)
        {
            ClearHeldAction();
            return;
        }

        bool isTurboPulse =
            turboPulsePrimed &&
            activeTurboTechnique != TurboTechnique.TurboDash;
        bool isTurboDash =
            !isTurboPulse &&
            activeTurboTechnique != TurboTechnique.TurboPulse &&
            IsTurboDashWindowOpen();
        if (isTurboPulse)
        {
            activeTurboTechnique = TurboTechnique.TurboPulse;
            TriggerTurboPresentation(TurboTechnique.TurboPulse);
        }
        float charge = isTurboPulse ? 1f : ChargeFraction;
        float powerMultiplier = isTurboPulse
            ? turboPulsePowerMultiplier
            : 1f;
        if (!chargeBank.TrySpend())
        {
            ClearHeldAction();
            return;
        }

        float radius =
            strikeMotor.GetPulseRadius(charge) * powerMultiplier;
        AirFootyStrikeResult result = strikeMotor.TryPulse(
            charge,
            powerMultiplier,
            powerMultiplier);
        Color color = isTurboPulse
            ? turboReadyColor
            : Color.Lerp(pulseTapColor, pulseFullColor, charge);
        StartCoroutine(PlayPulseWave(radius, color));

        if (isTurboDash)
        {
            ApplyTurboDashBoost();
        }

        if (isTurboPulse || isTurboDash)
        {
            string label = isTurboPulse
                ? "TURBO PULSE"
                : "TURBO DASH";
            AirFootyWorldPopup.Spawn(
                transform.position + Vector3.up * 0.9f,
                label,
                turboReadyColor);
        }

        if (feedbackAudio != null && result == AirFootyStrikeResult.Miss)
        {
            feedbackAudio.pitch = 1.4f;
            feedbackAudio.volume = 0.06f;
            feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.ImpactClip);
        }

        ClearHeldAction();
    }

    private bool TryActivateTurboDash()
    {
        if (pulseCharging ||
            Time.time > pulseQueuedUntil ||
            pulseAction == null ||
            !pulseAction.IsPressed() ||
            !IsTurboDashWindowOpen() ||
            activeTurboTechnique == TurboTechnique.TurboPulse ||
            strikeMotor == null ||
            !strikeMotor.IsPulseReady ||
            chargeBank == null ||
            chargeBank.CurrentCharges <= 0 ||
            !chargeBank.TrySpend())
        {
            return false;
        }

        pulseQueuedUntil = float.NegativeInfinity;
        float charge = 0f;
        float radius = strikeMotor.GetPulseRadius(charge);
        AirFootyStrikeResult result = strikeMotor.TryPulse(charge);
        StartCoroutine(PlayPulseWave(radius, turboReadyColor));
        ApplyTurboDashBoost();
        AirFootyWorldPopup.Spawn(
            transform.position + Vector3.up * 0.9f,
            "TURBO DASH",
            turboReadyColor);

        if (feedbackAudio != null && result == AirFootyStrikeResult.Miss)
        {
            feedbackAudio.pitch = 1.4f;
            feedbackAudio.volume = 0.06f;
            feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.ImpactClip);
        }

        return true;
    }

    private bool IsTurboDashWindowOpen()
    {
        return dashing || Time.time <= turboDashWindowUntil;
    }

    private void ApplyTurboDashBoost()
    {
        activeTurboTechnique = TurboTechnique.TurboDash;
        TriggerTurboPresentation(TurboTechnique.TurboDash);
        bool activeDash = dashing &&
                          playerMovement != null &&
                          playerMovement.IsDashing;
        if (activeDash)
        {
            dashEndsAt += turboDashExtensionDuration;
            playerMovement.BoostActiveDash(
                turboDashSpeedMultiplier,
                turboDashExtensionDuration);
        }
        else
        {
            dashing = true;
            dashConnected = false;
            dashEndsAt = Time.time + turboDashAfterburnerDuration;
            movementRecoveryEndsAt = float.NegativeInfinity;
            playerMovement?.SetMoveSpeedMultiplier(1f);
            playerMovement?.BeginDash(
                lastAimDirection,
                turboDashAfterburnerDuration,
                dashSpeedMultiplier * turboDashSpeedMultiplier);
        }

        turboDashWindowUntil = dashEndsAt + turboDashGraceSeconds;
    }

    private void TryBeginDash()
    {
        if (dashing ||
            Time.time < dashReadyAt ||
            Time.time < movementRecoveryEndsAt ||
            playerMovement == null ||
            strikeMotor == null ||
            chargeBank == null ||
            chargeBank.CurrentCharges <= 0 ||
            !strikeMotor.IsStrikeReady)
        {
            return;
        }

        if (!chargeBank.TrySpend())
        {
            return;
        }

        activeTurboTechnique = TurboTechnique.None;
        if (pulseCharging && strikeMotor != null)
        {
            float heldSeconds = Mathf.Max(0f, Time.time - pulseStartedAt);
            float secondsUntilFull =
                Mathf.Max(0f, strikeMotor.TimeToFullCharge - heldSeconds);
            turboPulsePrimed = secondsUntilFull <= dashDuration;
            if (turboPulsePrimed)
            {
                activeTurboTechnique = TurboTechnique.TurboPulse;
            }
        }

        dashing = true;
        dashConnected = false;
        dashEndsAt = Time.time + dashDuration;
        turboDashWindowUntil = dashEndsAt + turboDashGraceSeconds;
        dashReadyAt = Time.time + dashCooldown;
        dashQueuedUntil = float.NegativeInfinity;
        playerMovement.BeginDash(
            lastAimDirection,
            dashDuration,
            dashSpeedMultiplier);
    }

    private void UpdateDashState()
    {
        if (movementRecoveryEndsAt > 0f &&
            Time.time >= movementRecoveryEndsAt)
        {
            movementRecoveryEndsAt = float.NegativeInfinity;
            playerMovement?.SetMoveSpeedMultiplier(1f);
        }

        if (!dashing || Time.time < dashEndsAt)
        {
            return;
        }

        dashing = false;
        playerMovement?.CancelDash();
        if (!dashConnected)
        {
            movementRecoveryEndsAt = Time.time + missedDashRecovery;
            playerMovement?.SetMoveSpeedMultiplier(missedDashMoveMultiplier);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryResolveDashContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryResolveDashContact(collision);
    }

    private void TryResolveDashContact(Collision collision)
    {
        if (!dashing ||
            dashConnected ||
            collision.collider.GetComponentInParent<BallController3D>() == null)
        {
            return;
        }

        AirFootyStrikeResult result =
            strikeMotor.TryDashStrike(lastAimDirection);
        if (result != AirFootyStrikeResult.Hit &&
            result != AirFootyStrikeResult.Perfect)
        {
            return;
        }

        dashConnected = true;
        dashing = false;
        playerMovement?.CancelDash();
        playerMovement?.SetMoveSpeedMultiplier(1f);
    }

    private void BuildInputActions()
    {
        pulseAction = new InputAction("AirFooty Pulse", InputActionType.Button);
        pulseAction.AddBinding("<Mouse>/leftButton");
        pulseAction.AddBinding("<Gamepad>/buttonSouth");

        dashAction = new InputAction("AirFooty Dash", InputActionType.Button);
        dashAction.AddBinding("<Mouse>/rightButton");
        dashAction.AddBinding("<Keyboard>/leftShift");
        dashAction.AddBinding("<Keyboard>/rightShift");
        dashAction.AddBinding("<Gamepad>/rightTrigger");
        dashAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void BuildPulsePresentation()
    {
        Transform authoredRing = transform.Find("Hover Pulse Charge");
        LineRenderer authoredRingRenderer = authoredRing != null
            ? authoredRing.GetComponent<LineRenderer>()
            : null;
        pulseMaterial = authoredRingRenderer != null
            ? authoredRingRenderer.sharedMaterial
            : null;

        Shader shader = Shader.Find("Sprites/Default");
        if (pulseMaterial == null && shader != null)
        {
            pulseMaterial = new Material(shader)
            {
                name = "AirFooty Pulse (Runtime)"
            };
            ownsPulseMaterial = true;
        }

        GameObject ringObject = authoredRing != null
            ? authoredRing.gameObject
            : new GameObject("Hover Pulse Charge");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.up * 0.04f;
        pulseRing = BuildLineRenderer(
            ringObject,
            PulseRingSegments,
            0.06f);
        pulseRing.enabled = false;

        Transform authoredAim = transform.Find("Dash Aim Indicator");
        GameObject aimObject = authoredAim != null
            ? authoredAim.gameObject
            : new GameObject("Dash Aim Indicator");
        aimObject.transform.SetParent(transform, false);
        dashAimIndicator = aimObject.GetComponent<LineRenderer>();
        if (dashAimIndicator == null)
        {
            dashAimIndicator = aimObject.AddComponent<LineRenderer>();
        }
        dashAimIndicator.useWorldSpace = true;
        dashAimIndicator.loop = false;
        dashAimIndicator.positionCount = 3;
        dashAimIndicator.startWidth = dashAimWidth;
        dashAimIndicator.endWidth = dashAimWidth;
        dashAimIndicator.numCapVertices = 3;
        dashAimIndicator.numCornerVertices = 2;
        dashAimIndicator.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        dashAimIndicator.receiveShadows = false;
        dashAimIndicator.sharedMaterial = pulseMaterial;

        BuildTurboPresentation();

        int pipCount = chargeBank != null ? chargeBank.MaximumCharges : 3;
        chargePips = new LineRenderer[pipCount];
        for (int i = 0; i < pipCount; i++)
        {
            Transform authoredPip = transform.Find($"Ability Charge {i + 1}");
            GameObject pipObject = authoredPip != null
                ? authoredPip.gameObject
                : new GameObject($"Ability Charge {i + 1}");
            pipObject.transform.SetParent(transform, false);
            pipObject.transform.localPosition = new Vector3(
                (i - (pipCount - 1) * 0.5f) * 0.3f,
                0.07f,
                -0.86f);
            chargePips[i] = BuildLineRenderer(
                pipObject,
                PipRingSegments,
                0.05f);
            SetRingGeometry(chargePips[i], 0.09f);
        }
    }

    private LineRenderer BuildLineRenderer(
        GameObject owner,
        int segments,
        float width)
    {
        LineRenderer line = owner.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = owner.AddComponent<LineRenderer>();
        }
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = width;
        line.endWidth = width;
        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = pulseMaterial;
        return line;
    }

    private void UpdatePulseRing()
    {
        if (pulseRing == null || strikeMotor == null)
        {
            return;
        }

        float charge = ChargeFraction;
        float visualCharge = turboPulsePrimed ? 1f : charge;
        Color color = turboPulsePrimed
            ? turboReadyColor
            : Color.Lerp(pulseTapColor, pulseFullColor, charge);
        pulseRing.startColor = color;
        pulseRing.endColor = color;
        float pulse = charge >= 0.98f
            ? Mathf.Sin(Time.unscaledTime * 20f) * 0.04f
            : 0f;
        SetRingGeometry(
            pulseRing,
            strikeMotor.GetPulseRadius(visualCharge) *
            (turboPulsePrimed ? turboPulsePowerMultiplier : 1f) +
            pulse);
    }

    private void UpdateDashAimIndicator()
    {
        if (dashAimIndicator == null)
        {
            return;
        }

        dashAimIndicator.enabled = actionsEnabled;
        Vector3 direction = lastAimDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }
        direction.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 tip =
            transform.position +
            Vector3.up * 0.055f +
            direction * (0.58f + dashAimLength);
        Vector3 arrowBase = tip - direction * dashAimLength;
        dashAimIndicator.SetPosition(0, arrowBase + side * 0.19f);
        dashAimIndicator.SetPosition(1, tip);
        dashAimIndicator.SetPosition(2, arrowBase - side * 0.19f);

        bool dashAvailable =
            !dashing &&
            Time.time >= dashReadyAt &&
            Time.time >= movementRecoveryEndsAt &&
            chargeBank != null &&
            chargeBank.CurrentCharges > 0;
        bool turboPulseReady =
            pulseCharging &&
            strikeMotor != null &&
            strikeMotor.TimeToFullCharge -
            Mathf.Max(0f, Time.time - pulseStartedAt) <= dashDuration;
        bool turboDashReady =
            IsTurboDashWindowOpen() &&
            chargeBank != null &&
            chargeBank.CurrentCharges > 0;
        bool turboTechReady = turboPulseReady || turboDashReady;
        Color color = turboTechReady
            ? turboReadyColor
            : dashAvailable || dashing
                ? dashAimColor
                : unavailableColor;
        dashAimIndicator.startColor = color;
        dashAimIndicator.endColor = color;
    }

    private void BuildTurboPresentation()
    {
        turboRenderer = GetComponentInChildren<Renderer>();

        Transform authoredStabilizer = transform.Find("Turbo Stabilizers");
        GameObject stabilizerObject = authoredStabilizer != null
            ? authoredStabilizer.gameObject
            : new GameObject("Turbo Stabilizers");
        stabilizerObject.transform.SetParent(transform, false);
        stabilizerObject.transform.localPosition = Vector3.up * 0.07f;
        turboStabilizer = stabilizerObject.GetComponent<LineRenderer>();
        if (turboStabilizer == null)
        {
            turboStabilizer = stabilizerObject.AddComponent<LineRenderer>();
        }
        turboStabilizer.useWorldSpace = false;
        turboStabilizer.loop = true;
        turboStabilizer.positionCount = 12;
        turboStabilizer.startWidth = 0.09f;
        turboStabilizer.endWidth = 0.05f;
        turboStabilizer.numCornerVertices = 2;
        turboStabilizer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        turboStabilizer.receiveShadows = false;
        turboStabilizer.sharedMaterial = pulseMaterial;
        turboStabilizer.enabled = false;

        turboThrusters = new TrailRenderer[2];
        for (int i = 0; i < turboThrusters.Length; i++)
        {
            Transform authoredThruster = transform.Find($"Turbo Thruster {i + 1}");
            GameObject thruster = authoredThruster != null
                ? authoredThruster.gameObject
                : new GameObject($"Turbo Thruster {i + 1}");
            thruster.transform.SetParent(transform, true);
            TrailRenderer trail = thruster.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = thruster.AddComponent<TrailRenderer>();
            }
            trail.time = turboTrailTime;
            trail.minVertexDistance = 0.025f;
            trail.startWidth = 0.24f;
            trail.endWidth = 0f;
            trail.numCornerVertices = 3;
            trail.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = pulseMaterial;
            trail.emitting = false;
            turboThrusters[i] = trail;
        }

        Transform authoredGlow = transform.Find("Turbo Reactor Glow");
        GameObject glowObject = authoredGlow != null
            ? authoredGlow.gameObject
            : new GameObject("Turbo Reactor Glow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = Vector3.up * 0.38f;
        turboGlow = glowObject.GetComponent<Light>();
        if (turboGlow == null)
        {
            turboGlow = glowObject.AddComponent<Light>();
        }
        turboGlow.type = LightType.Point;
        turboGlow.range = 3.3f;
        turboGlow.shadows = LightShadows.None;
        turboGlow.enabled = false;
    }

    private void TriggerTurboPresentation(TurboTechnique technique)
    {
        turboFxTechnique = technique;
        turboFxUntil = Mathf.Max(
            turboFxUntil,
            Time.unscaledTime + turboFxDuration);
        Color color = technique == TurboTechnique.TurboPulse
            ? turboPulseFxColor
            : turboDashFxColor;
        if (turboRenderer != null)
        {
            StartCoroutine(AirFootyFeedbackUtility.FlashRenderer(
                turboRenderer,
                color,
                0.22f));
        }
    }

    private void UpdateTurboPresentation()
    {
        bool active = Time.unscaledTime <= turboFxUntil;
        if (turboStabilizer == null || turboThrusters == null)
        {
            return;
        }

        Color color = turboFxTechnique == TurboTechnique.TurboPulse
            ? turboPulseFxColor
            : turboDashFxColor;
        float flicker = 0.82f + Mathf.Sin(Time.unscaledTime * 28f) * 0.18f;
        turboStabilizer.enabled = active;
        turboStabilizer.startColor = color;
        turboStabilizer.endColor = new Color(color.r, color.g, color.b, 0.28f);
        if (active)
        {
            float spin = Time.unscaledTime * 5.5f;
            for (int i = 0; i < turboStabilizer.positionCount; i++)
            {
                float angle =
                    i / (float)turboStabilizer.positionCount * Mathf.PI * 2f +
                    spin;
                float radius = i % 2 == 0 ? 0.72f : 0.52f;
                turboStabilizer.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
            }
        }

        Vector3 direction = lastAimDirection.sqrMagnitude > 0.0001f
            ? lastAimDirection.normalized
            : Vector3.right;
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 rear =
            transform.position - direction * 0.46f + Vector3.up * 0.16f;
        for (int i = 0; i < turboThrusters.Length; i++)
        {
            TrailRenderer trail = turboThrusters[i];
            trail.transform.position =
                rear + side * (i == 0 ? -0.24f : 0.24f);
            trail.startColor = color * flicker;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.emitting = active;
        }

        if (turboGlow != null)
        {
            turboGlow.enabled = active;
            turboGlow.color = color;
            turboGlow.intensity = 3.2f * flicker;
        }
    }

    private void StopTurboPresentation()
    {
        turboFxUntil = float.NegativeInfinity;
        if (turboStabilizer != null)
        {
            turboStabilizer.enabled = false;
        }
        if (turboThrusters != null)
        {
            foreach (TrailRenderer trail in turboThrusters)
            {
                if (trail != null)
                {
                    trail.emitting = false;
                }
            }
        }
        if (turboGlow != null)
        {
            turboGlow.enabled = false;
        }
    }

    private void UpdateChargePips()
    {
        if (chargePips == null || chargeBank == null)
        {
            return;
        }

        for (int i = 0; i < chargePips.Length; i++)
        {
            LineRenderer pip = chargePips[i];
            bool available = i < chargeBank.CurrentCharges;
            Color color = available ? pulseTapColor : unavailableColor;
            if (!available && i == chargeBank.CurrentCharges)
            {
                color = Color.Lerp(
                    unavailableColor,
                    pulseTapColor,
                    chargeBank.RechargeFraction);
            }
            pip.startColor = color;
            pip.endColor = color;
        }
    }

    private IEnumerator PlayPulseWave(float radius, Color color)
    {
        GameObject waveObject = new GameObject("Hover Pulse Wave");
        waveObject.transform.SetParent(transform, false);
        waveObject.transform.localPosition = Vector3.up * 0.045f;
        LineRenderer wave = BuildLineRenderer(
            waveObject,
            PulseRingSegments,
            0.09f);

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
                1f - progress);
            wave.startColor = faded;
            wave.endColor = faded;
            yield return null;
        }

        if (waveObject != null)
        {
            Destroy(waveObject);
        }
    }

    private static void SetRingGeometry(LineRenderer ring, float radius)
    {
        if (ring == null)
        {
            return;
        }

        int segments = ring.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius));
        }
    }

    private void OnValidate()
    {
        meaningfulAimThreshold = Mathf.Max(0f, meaningfulAimThreshold);
        pulseWaveSeconds = Mathf.Max(0.05f, pulseWaveSeconds);
        dashAimLength = Mathf.Max(0.1f, dashAimLength);
        dashAimWidth = Mathf.Max(0.01f, dashAimWidth);
        dashDuration = Mathf.Max(0.05f, dashDuration);
        dashSpeedMultiplier = Mathf.Max(1f, dashSpeedMultiplier);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        missedDashRecovery = Mathf.Max(0f, missedDashRecovery);
        missedDashMoveMultiplier = Mathf.Clamp01(missedDashMoveMultiplier);
        turboPulsePowerMultiplier = Mathf.Max(1f, turboPulsePowerMultiplier);
        turboDashSpeedMultiplier = Mathf.Max(1f, turboDashSpeedMultiplier);
        turboDashExtensionDuration = Mathf.Max(0f, turboDashExtensionDuration);
        turboDashGraceSeconds = Mathf.Max(0f, turboDashGraceSeconds);
        turboDashAfterburnerDuration =
            Mathf.Max(0.05f, turboDashAfterburnerDuration);
        inputBufferSeconds = Mathf.Max(0f, inputBufferSeconds);
        turboFxDuration = Mathf.Max(0.05f, turboFxDuration);
        turboTrailTime = Mathf.Max(0.05f, turboTrailTime);
    }
}
