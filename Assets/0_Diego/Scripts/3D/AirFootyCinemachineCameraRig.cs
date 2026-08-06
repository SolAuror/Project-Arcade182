using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class AirFootyCinemachineCameraRig : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement3D player;
    [SerializeField] private Camera outputCamera;

    [Header("Arena Composition")]
    [SerializeField] private Vector3 arenaCentre;
    [SerializeField] private Vector2 followInfluence = new Vector2(0.12f, 0.1f);
    [SerializeField] private Vector2 lookInfluence = new Vector2(0.24f, 0.22f);
    [SerializeField, Min(0f)] private float edgeLookLift = 0.35f;
    [SerializeField] private Vector2 trackedScreenPosition = new Vector2(0f, -0.06f);

    [Header("Team Isometric View")]
    [Tooltip("Distance behind the active player's home edge and to their right.")]
    [SerializeField, Min(0.1f)] private float cornerDistance = 11f;
    [Tooltip("Height matching the two equal corner axes for a true isometric angle.")]
    [SerializeField, Min(0.1f)] private float cameraHeight = 11f;

    [Header("Broadcast Follow")]
    [SerializeField, Min(0f)] private float horizontalFollowDamping = 0.75f;
    [SerializeField, Min(0f)] private float verticalFollowDamping = 1f;
    [SerializeField, Min(0f)] private float horizontalAimDamping = 0.9f;
    [SerializeField, Min(0f)] private float verticalAimDamping = 1.1f;
    [SerializeField, Min(0f)] private float charmSway = 0.08f;
    [SerializeField, Min(0.01f)] private float charmSwayPeriod = 7f;

    [Header("Player Camera Input")]
    [Tooltip("How much mouse movement contributes to the camera wiggle.")]
    [SerializeField, Min(0f)] private float mouseWiggleSensitivity = 0.0035f;
    [Tooltip("World-space look-target movement produced by full stick/mouse input.")]
    [SerializeField, Min(0f)] private float inputWiggleAmplitude = 0.65f;
    [SerializeField, Min(0f)] private float inputWiggleDamping = 8f;
    [SerializeField, Range(0f, 1f)] private float stickWiggleDeadzone = 0.15f;
    [Tooltip("Maximum FOV change from the neutral camera framing.")]
    [SerializeField, Min(0f)] private float maximumZoomDegrees = 3f;
    [SerializeField, Min(0f)] private float zoomStep = 0.35f;
    [SerializeField, Min(0f)] private float zoomDamping = 7f;

    [Header("Subtle Impact Noise")]
    [SerializeField, Range(0f, 1f)] private float impactResponse = 0.65f;
    [SerializeField, Min(0f)] private float impactDecayPerSecond = 1.2f;
    [SerializeField, Range(0f, 1f)] private float maximumImpactAmplitude = 0.5f;

    [Header("Authored Rig")]
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private CinemachineCamera broadcastCamera;
    [SerializeField] private CinemachineFollow followComponent;
    [SerializeField] private CinemachineBasicMultiChannelPerlin impactNoise;
    [SerializeField] private NoiseSettings impactNoiseProfile;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform lookTarget;
    private Vector3 baseCameraOffset;
    private Vector3 baseViewForward;
    private Vector3 baseViewRight;
    private Quaternion teamRotation = Quaternion.identity;
    private bool configurationErrorLogged;
    private float impactAmplitude;
    private int teamCullingMask = ~0;
    private InputAction mouseDeltaAction;
    private InputAction leftStickAction;
    private InputAction zoomWheelAction;
    private InputAction zoomInAction;
    private InputAction zoomOutAction;
    private Vector2 inputWiggle;
    private float zoomAmount;
    private float targetZoomAmount;
    private float baseFieldOfView;

    public bool IsReady =>
        outputCamera != null &&
        brain != null &&
        broadcastCamera != null &&
        followComponent != null &&
        impactNoise != null &&
        impactNoiseProfile != null &&
        followTarget != null &&
        lookTarget != null;
    public Camera OutputCamera => outputCamera;

    /// <summary>
    /// Culling mask for the selected team, with that team's own corner dressing
    /// excluded. Anything that re-enables the camera should restore this rather
    /// than resetting to "everything".
    /// </summary>
    public int TeamCullingMask => teamCullingMask;

    private void Awake()
    {
        BuildInputActions();
        ResolveReferences();
        ApplyTeamPerspective(ResolvePlayerTeam());
        BuildRig();
    }

    private void OnEnable()
    {
        mouseDeltaAction?.Enable();
        leftStickAction?.Enable();
        zoomWheelAction?.Enable();
        zoomInAction?.Enable();
        zoomOutAction?.Enable();
    }

    private void OnDisable()
    {
        mouseDeltaAction?.Disable();
        leftStickAction?.Disable();
        zoomWheelAction?.Disable();
        zoomInAction?.Disable();
        zoomOutAction?.Disable();
    }

    private void OnDestroy()
    {
        mouseDeltaAction?.Dispose();
        leftStickAction?.Dispose();
        zoomWheelAction?.Dispose();
        zoomInAction?.Dispose();
        zoomOutAction?.Dispose();
    }

    private void LateUpdate()
    {
        if (!IsReady)
        {
            ResolveReferences();
            BuildRig();
            if (!IsReady)
            {
                return;
            }
        }

        // Re-assert rather than trusting whoever last touched the camera: the
        // menu resets the mask to "everything" when it hands over to gameplay,
        // which would put this team's own corner dressing back in front of it.
        if (outputCamera.cullingMask != teamCullingMask)
        {
            outputCamera.cullingMask = teamCullingMask;
        }

        UpdatePlayerCameraInput();
        UpdateTargets();
        UpdateImpactNoise();
    }

    public void AddImpact(float amount)
    {
        impactAmplitude = Mathf.Min(
            maximumImpactAmplitude,
            impactAmplitude + Mathf.Max(0f, amount) * impactResponse);
    }

    public void SetPlayer(
        PlayerMovement3D selectedPlayer,
        AirFootyTeam selectedTeam)
    {
        player = selectedPlayer;
        ResolveReferences();
        ApplyTeamPerspective(selectedTeam);
        if (player != null && !IsReady)
        {
            BuildRig();
        }
        UpdateTargets();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement3D>();
        }
        if (outputCamera == null)
        {
            outputCamera = AirFootyCameraLookup.FindDisplayCamera();
        }
    }

    private void BuildRig()
    {
        if (player == null || outputCamera == null)
        {
            return;
        }

        brain = outputCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            LogConfigurationErrorOnce(
                "AirFooty camera is missing its authored CinemachineBrain.",
                outputCamera);
            return;
        }

        brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        brain.IgnoreTimeScale = true;
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut,
            0.35f);

        RefreshViewBasis();

        followTarget ??= transform.Find("AirFooty Camera Follow Target");
        lookTarget ??= transform.Find("AirFooty Camera Look Target");
        Transform cameraTransform = broadcastCamera != null
            ? broadcastCamera.transform
            : transform.Find("AirFooty Broadcast Camera");
        if (followTarget == null || lookTarget == null || cameraTransform == null)
        {
            LogConfigurationErrorOnce(
                "AirFooty camera rig is missing an authored target or broadcast camera child.",
                this);
            return;
        }

        broadcastCamera ??= cameraTransform.GetComponent<CinemachineCamera>();
        followComponent ??= cameraTransform.GetComponent<CinemachineFollow>();
        impactNoise ??= cameraTransform.GetComponent<CinemachineBasicMultiChannelPerlin>();
        CinemachineRotationComposer aim =
            cameraTransform.GetComponent<CinemachineRotationComposer>();
        if (broadcastCamera == null || followComponent == null || aim == null || impactNoise == null || impactNoiseProfile == null)
        {
            LogConfigurationErrorOnce(
                "AirFooty broadcast camera is missing an authored Cinemachine component or noise profile.",
                cameraTransform);
            return;
        }

        UpdateTargets();

        cameraTransform.SetPositionAndRotation(
            outputCamera.transform.position,
            outputCamera.transform.rotation);

        broadcastCamera.Priority = 100;
        LensSettings outputLens = LensSettings.FromCamera(outputCamera);
        baseFieldOfView = outputLens.FieldOfView;
        broadcastCamera.Lens = outputLens;
        broadcastCamera.Follow = followTarget;
        broadcastCamera.LookAt = lookTarget;

        followComponent.FollowOffset = baseCameraOffset;
        followComponent.TrackerSettings = new TrackerSettings
        {
            BindingMode = BindingMode.WorldSpace,
            PositionDamping = new Vector3(
                horizontalFollowDamping,
                verticalFollowDamping,
                horizontalFollowDamping),
            AngularDampingMode = AngularDampingMode.Euler,
            RotationDamping = Vector3.zero,
            QuaternionDamping = 0f
        };

        aim.Damping = new Vector2(horizontalAimDamping, verticalAimDamping);
        aim.CenterOnActivate = true;
        ScreenComposerSettings composition = ScreenComposerSettings.Default;
        composition.ScreenPosition = trackedScreenPosition;
        composition.DeadZone = new ScreenComposerSettings.DeadZoneSettings
        {
            Enabled = true,
            Size = new Vector2(0.08f, 0.06f)
        };
        composition.HardLimits = new ScreenComposerSettings.HardLimitSettings
        {
            Enabled = true,
            Size = new Vector2(0.32f, 0.26f),
            Offset = Vector2.zero
        };
        aim.Composition = composition;

        impactNoise.NoiseProfile = impactNoiseProfile;
        impactNoise.AmplitudeGain = 0f;
        impactNoise.FrequencyGain = 1f;
        configurationErrorLogged = false;
    }

    private void LogConfigurationErrorOnce(string message, Object context)
    {
        if (configurationErrorLogged)
        {
            return;
        }

        configurationErrorLogged = true;
        Debug.LogError(message, context);
    }

    private AirFootyTeam ResolvePlayerTeam()
    {
        if (player == null)
        {
            return AirFootyTeam.Blue;
        }

        AirFootyTeamMember3D member =
            player.GetComponent<AirFootyTeamMember3D>();
        AirFootyTeam team = member != null
            ? member.Team
            : AirFootyTeamMember3D.InferFromHierarchy(player.transform);
        return team != AirFootyTeam.None ? team : AirFootyTeam.Blue;
    }

    private void ApplyTeamPerspective(AirFootyTeam selectedTeam)
    {
        if (outputCamera == null)
        {
            return;
        }

        if (selectedTeam == AirFootyTeam.None)
        {
            selectedTeam = AirFootyTeam.Blue;
        }

        // Drop this team's own corner dressing, which sits between its camera and
        // the pitch. Stored so SetGameplayPresentationActive can restore it
        // without clobbering the exclusion.
        teamCullingMask = AirFootyTeamViewMask.CullingMaskFor(selectedTeam);
        outputCamera.cullingMask = teamCullingMask;

        teamRotation = Quaternion.Euler(0f, TeamCameraYaw(selectedTeam), 0f);
        Vector3 homeDirection =
            AirFootyTeamMember3D.HomeDirection(selectedTeam).normalized;
        Vector3 inwardDirection = -homeDirection;
        Vector3 playerRight = Vector3.Cross(
            Vector3.up,
            inwardDirection).normalized;
        Vector3 rotatedPosition =
            arenaCentre +
            homeDirection * cornerDistance +
            playerRight * cornerDistance +
            Vector3.up * cameraHeight;
        Quaternion rotatedRotation = Quaternion.LookRotation(
            arenaCentre - rotatedPosition,
            Vector3.up);
        outputCamera.transform.SetPositionAndRotation(
            rotatedPosition,
            rotatedRotation);

        baseCameraOffset = rotatedPosition - arenaCentre;
        RefreshViewBasis();
        if (followComponent != null)
        {
            followComponent.FollowOffset = baseCameraOffset;
        }
        if (broadcastCamera != null)
        {
            broadcastCamera.transform.SetPositionAndRotation(
                rotatedPosition,
                rotatedRotation);
        }
    }

    private void RefreshViewBasis()
    {
        if (outputCamera != null && baseCameraOffset.sqrMagnitude <= 0.0001f)
        {
            baseCameraOffset = outputCamera.transform.position - arenaCentre;
        }

        baseViewForward = Vector3.ProjectOnPlane(
            -baseCameraOffset,
            Vector3.up).normalized;
        if (baseViewForward.sqrMagnitude < 0.0001f)
        {
            baseViewForward = Vector3.forward;
        }
        baseViewRight = Vector3.Cross(Vector3.up, baseViewForward).normalized;
    }

    /// <summary>
    /// Yaw that carries Blue-local tracking offsets onto another team's side,
    /// so the follow and look targets react consistently for every player.
    ///
    /// The value is the rotation taking Blue's home direction onto that team's:
    /// a Y rotation of t maps (x, z) to (x cos t + z sin t, -x sin t + z cos t),
    /// so Blue's (-1, 0, 0) reaches Green's (0, 0, 1) at +90 and Gold's
    /// (0, 0, -1) at -90. Green and Gold were previously the other way round,
    /// which sat both of them behind their opponent's goal.
    /// </summary>
    private static float TeamCameraYaw(AirFootyTeam team)
    {
        return team switch
        {
            AirFootyTeam.Red => 180f,
            AirFootyTeam.Green => 90f,
            AirFootyTeam.Gold => -90f,
            _ => 0f
        };
    }

    private void UpdateTargets()
    {
        if (player == null || followTarget == null || lookTarget == null)
        {
            return;
        }

        Vector3 playerOffset = player.transform.position - arenaCentre;
        Vector3 localPlayerOffset =
            Quaternion.Inverse(teamRotation) * playerOffset;
        Vector3 localFollowOffset = new Vector3(
            localPlayerOffset.x * followInfluence.x,
            0f,
            localPlayerOffset.z * followInfluence.y);
        Vector3 followPosition = arenaCentre +
                                 teamRotation * localFollowOffset;

        float xEdge = Mathf.Max(
            Mathf.InverseLerp(-5.5f, -7.5f, localPlayerOffset.x),
            Mathf.InverseLerp(-2.5f, -0.5f, localPlayerOffset.x));
        float zEdge = Mathf.InverseLerp(
            2.3f,
            3.5f,
            Mathf.Abs(localPlayerOffset.z));
        float edgeAmount = Mathf.SmoothStep(0f, 1f, Mathf.Max(xEdge, zEdge));

        float swayPhase = Time.unscaledTime * Mathf.PI * 2f / charmSwayPeriod;
        Vector3 sway =
            baseViewRight * (Mathf.Sin(swayPhase) * charmSway) +
            baseViewForward * (Mathf.Cos(swayPhase * 0.73f) * charmSway * 0.45f);
        Vector3 localLookOffset = new Vector3(
            localPlayerOffset.x * lookInfluence.x,
            edgeAmount * edgeLookLift,
            localPlayerOffset.z * lookInfluence.y);
        Vector3 lookPosition = arenaCentre +
                               teamRotation * localLookOffset +
                               sway +
                               baseViewRight * (inputWiggle.x * inputWiggleAmplitude) +
                               Vector3.up * (inputWiggle.y * inputWiggleAmplitude);

        followTarget.position = followPosition;
        lookTarget.position = lookPosition;
    }

    private void UpdatePlayerCameraInput()
    {
        Vector2 mouseInput = mouseDeltaAction != null
            ? mouseDeltaAction.ReadValue<Vector2>() * mouseWiggleSensitivity
            : Vector2.zero;
        Vector2 stickInput = leftStickAction != null
            ? ApplyDeadzone(leftStickAction.ReadValue<Vector2>(), stickWiggleDeadzone)
            : Vector2.zero;
        Vector2 desiredWiggle = Vector2.ClampMagnitude(mouseInput + stickInput, 1f);
        float wiggleBlend = 1f - Mathf.Exp(-inputWiggleDamping * Time.unscaledDeltaTime);
        inputWiggle = Vector2.Lerp(inputWiggle, desiredWiggle, wiggleBlend);

        float wheel = zoomWheelAction != null
            ? zoomWheelAction.ReadValue<float>()
            : 0f;
        if (Mathf.Abs(wheel) > 0.001f)
        {
            // Mouse scroll-up reports a positive value, which zooms in by
            // reducing the field of view below.
            targetZoomAmount = Mathf.Clamp(
                targetZoomAmount + Mathf.Sign(wheel) * zoomStep,
                -1f,
                1f);
        }

        float triggerZoom = 0f;
        if (zoomInAction != null)
        {
            triggerZoom += zoomInAction.ReadValue<float>();
        }
        if (zoomOutAction != null)
        {
            triggerZoom -= zoomOutAction.ReadValue<float>();
        }
        targetZoomAmount = Mathf.Clamp(
            targetZoomAmount + triggerZoom * zoomStep * Time.unscaledDeltaTime,
            -1f,
            1f);

        float zoomBlend = 1f - Mathf.Exp(-zoomDamping * Time.unscaledDeltaTime);
        zoomAmount = Mathf.Lerp(zoomAmount, targetZoomAmount, zoomBlend);
        if (broadcastCamera != null && maximumZoomDegrees > 0f)
        {
            LensSettings lens = broadcastCamera.Lens;
            lens.FieldOfView = Mathf.Max(
                1f,
                baseFieldOfView - zoomAmount * maximumZoomDegrees);
            broadcastCamera.Lens = lens;
        }
    }

    private static Vector2 ApplyDeadzone(Vector2 input, float deadzone)
    {
        float magnitude = input.magnitude;
        if (magnitude <= deadzone)
        {
            return Vector2.zero;
        }

        return input / magnitude * Mathf.InverseLerp(deadzone, 1f, magnitude);
    }

    private void BuildInputActions()
    {
        mouseDeltaAction = new InputAction(
            "AirFooty Camera Mouse Wiggle",
            InputActionType.Value);
        mouseDeltaAction.AddBinding("<Mouse>/delta");

        leftStickAction = new InputAction(
            "AirFooty Camera Stick Wiggle",
            InputActionType.Value);
        leftStickAction.AddBinding("<Gamepad>/leftStick");

        zoomWheelAction = new InputAction(
            "AirFooty Camera Zoom Wheel",
            InputActionType.Value);
        zoomWheelAction.AddBinding("<Mouse>/scroll/y");

        zoomInAction = new InputAction(
            "AirFooty Camera Zoom In",
            InputActionType.Value);
        zoomInAction.AddBinding("<Gamepad>/rightTrigger");

        zoomOutAction = new InputAction(
            "AirFooty Camera Zoom Out",
            InputActionType.Value);
        zoomOutAction.AddBinding("<Gamepad>/leftTrigger");
    }

    private void UpdateImpactNoise()
    {
        if (impactNoise == null)
        {
            return;
        }

        impactAmplitude = Mathf.Max(
            0f,
            impactAmplitude - impactDecayPerSecond * Time.unscaledDeltaTime);
        impactNoise.AmplitudeGain = impactAmplitude;
    }

    private void OnValidate()
    {
        followInfluence.x = Mathf.Max(0f, followInfluence.x);
        followInfluence.y = Mathf.Max(0f, followInfluence.y);
        lookInfluence.x = Mathf.Max(0f, lookInfluence.x);
        lookInfluence.y = Mathf.Max(0f, lookInfluence.y);
        edgeLookLift = Mathf.Max(0f, edgeLookLift);
        cornerDistance = Mathf.Max(0.1f, cornerDistance);
        cameraHeight = Mathf.Max(0.1f, cameraHeight);
        horizontalFollowDamping = Mathf.Max(0f, horizontalFollowDamping);
        verticalFollowDamping = Mathf.Max(0f, verticalFollowDamping);
        horizontalAimDamping = Mathf.Max(0f, horizontalAimDamping);
        verticalAimDamping = Mathf.Max(0f, verticalAimDamping);
        charmSway = Mathf.Max(0f, charmSway);
        charmSwayPeriod = Mathf.Max(0.01f, charmSwayPeriod);
        mouseWiggleSensitivity = Mathf.Max(0f, mouseWiggleSensitivity);
        inputWiggleAmplitude = Mathf.Max(0f, inputWiggleAmplitude);
        inputWiggleDamping = Mathf.Max(0f, inputWiggleDamping);
        zoomStep = Mathf.Max(0f, zoomStep);
        maximumZoomDegrees = Mathf.Max(0f, maximumZoomDegrees);
        zoomDamping = Mathf.Max(0f, zoomDamping);
        impactDecayPerSecond = Mathf.Max(0f, impactDecayPerSecond);
    }
}

internal static class AirFootyCameraLookup
{
    public static Camera FindDisplayCamera()
    {
        Camera namedInactive = null;
        Camera taggedActive = null;
        Camera taggedInactive = null;
        Camera activeCandidate = null;
        Camera inactiveCandidate = null;
        foreach (Camera candidate in Object.FindObjectsByType<Camera>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.targetTexture != null)
            {
                continue;
            }

            if (candidate.name == "AirFooty Display Camera")
            {
                if (candidate.isActiveAndEnabled)
                {
                    return candidate;
                }

                namedInactive ??= candidate;
                continue;
            }

            if (candidate.CompareTag("MainCamera"))
            {
                if (candidate.isActiveAndEnabled)
                {
                    taggedActive ??= candidate;
                }
                else
                {
                    taggedInactive ??= candidate;
                }
                continue;
            }

            if (candidate.isActiveAndEnabled)
            {
                activeCandidate ??= candidate;
            }
            else
            {
                inactiveCandidate ??= candidate;
            }
        }

        return namedInactive ??
               taggedActive ??
               taggedInactive ??
               activeCandidate ??
               inactiveCandidate;
    }
}
