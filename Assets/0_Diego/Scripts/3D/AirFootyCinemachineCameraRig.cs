using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

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

    [Header("Broadcast Follow")]
    [SerializeField, Min(0f)] private float horizontalFollowDamping = 0.75f;
    [SerializeField, Min(0f)] private float verticalFollowDamping = 1f;
    [SerializeField, Min(0f)] private float horizontalAimDamping = 0.9f;
    [SerializeField, Min(0f)] private float verticalAimDamping = 1.1f;
    [SerializeField, Min(0f)] private float charmSway = 0.08f;
    [SerializeField, Min(0.01f)] private float charmSwayPeriod = 7f;

    [Header("Subtle Impact Noise")]
    [SerializeField, Range(0f, 1f)] private float impactResponse = 0.65f;
    [SerializeField, Min(0f)] private float impactDecayPerSecond = 1.2f;
    [SerializeField, Range(0f, 1f)] private float maximumImpactAmplitude = 0.5f;

    private CinemachineBrain brain;
    private CinemachineCamera broadcastCamera;
    private CinemachineBasicMultiChannelPerlin impactNoise;
    private NoiseSettings impactNoiseProfile;
    private Transform followTarget;
    private Transform lookTarget;
    private Vector3 baseCameraOffset;
    private Vector3 baseViewForward;
    private Vector3 baseViewRight;
    private float impactAmplitude;

    public bool IsReady => broadcastCamera != null && outputCamera != null;

    private void Awake()
    {
        ResolveReferences();
        BuildRig();
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

        UpdateTargets();
        UpdateImpactNoise();
    }

    private void OnDestroy()
    {
        if (impactNoiseProfile != null)
        {
            Destroy(impactNoiseProfile);
        }
    }

    public void AddImpact(float amount)
    {
        impactAmplitude = Mathf.Min(
            maximumImpactAmplitude,
            impactAmplitude + Mathf.Max(0f, amount) * impactResponse);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement3D>();
        }
        if (outputCamera == null)
        {
            outputCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        }
    }

    private void BuildRig()
    {
        if (broadcastCamera != null || player == null || outputCamera == null)
        {
            return;
        }

        brain = outputCamera.GetComponent<CinemachineBrain>();
        if (brain == null)
        {
            brain = outputCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        brain.IgnoreTimeScale = true;
        brain.DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Styles.EaseInOut,
            0.35f);

        baseCameraOffset = outputCamera.transform.position - arenaCentre;
        baseViewForward = Vector3.ProjectOnPlane(-baseCameraOffset, Vector3.up).normalized;
        if (baseViewForward.sqrMagnitude < 0.0001f)
        {
            baseViewForward = Vector3.forward;
        }
        baseViewRight = Vector3.Cross(Vector3.up, baseViewForward).normalized;

        followTarget = CreateRuntimeChild("AirFooty Camera Follow Target");
        lookTarget = CreateRuntimeChild("AirFooty Camera Look Target");
        UpdateTargets();

        GameObject cameraObject = new GameObject("AirFooty Broadcast Camera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.SetPositionAndRotation(
            outputCamera.transform.position,
            outputCamera.transform.rotation);

        broadcastCamera = cameraObject.AddComponent<CinemachineCamera>();
        broadcastCamera.Priority = 100;
        broadcastCamera.Lens = LensSettings.FromCamera(outputCamera);
        broadcastCamera.Follow = followTarget;
        broadcastCamera.LookAt = lookTarget;

        CinemachineFollow follow = cameraObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = baseCameraOffset;
        follow.TrackerSettings = new TrackerSettings
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

        CinemachineRotationComposer aim =
            cameraObject.AddComponent<CinemachineRotationComposer>();
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

        impactNoise = cameraObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
        impactNoiseProfile = BuildImpactNoiseProfile();
        impactNoise.NoiseProfile = impactNoiseProfile;
        impactNoise.AmplitudeGain = 0f;
        impactNoise.FrequencyGain = 1f;
    }

    private Transform CreateRuntimeChild(string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        return child.transform;
    }

    private void UpdateTargets()
    {
        if (player == null || followTarget == null || lookTarget == null)
        {
            return;
        }

        Vector3 playerOffset = player.transform.position - arenaCentre;
        Vector3 followPosition = arenaCentre + new Vector3(
            playerOffset.x * followInfluence.x,
            0f,
            playerOffset.z * followInfluence.y);

        float xEdge = Mathf.Max(
            Mathf.InverseLerp(-5.5f, -7.5f, playerOffset.x),
            Mathf.InverseLerp(-2.5f, -0.5f, playerOffset.x));
        float zEdge = Mathf.InverseLerp(2.3f, 3.5f, Mathf.Abs(playerOffset.z));
        float edgeAmount = Mathf.SmoothStep(0f, 1f, Mathf.Max(xEdge, zEdge));

        float swayPhase = Time.unscaledTime * Mathf.PI * 2f / charmSwayPeriod;
        Vector3 sway =
            baseViewRight * (Mathf.Sin(swayPhase) * charmSway) +
            baseViewForward * (Mathf.Cos(swayPhase * 0.73f) * charmSway * 0.45f);
        Vector3 lookPosition = arenaCentre + new Vector3(
            playerOffset.x * lookInfluence.x,
            edgeAmount * edgeLookLift,
            playerOffset.z * lookInfluence.y) + sway;

        followTarget.position = followPosition;
        lookTarget.position = lookPosition;
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

    private static NoiseSettings BuildImpactNoiseProfile()
    {
        NoiseSettings profile = ScriptableObject.CreateInstance<NoiseSettings>();
        profile.name = "AirFooty Subtle Impact Noise (Runtime)";
        profile.hideFlags = HideFlags.HideAndDontSave;
        profile.PositionNoise = new[]
        {
            new NoiseSettings.TransformNoiseParams
            {
                X = NoiseChannel(0.08f, 2.1f),
                Y = NoiseChannel(0.05f, 2.6f),
                Z = NoiseChannel(0.025f, 1.7f)
            }
        };
        profile.OrientationNoise = new[]
        {
            new NoiseSettings.TransformNoiseParams
            {
                X = NoiseChannel(0.28f, 2.4f),
                Y = NoiseChannel(0.2f, 1.9f),
                Z = NoiseChannel(0.42f, 2.8f)
            }
        };
        return profile;
    }

    private static NoiseSettings.NoiseParams NoiseChannel(float amplitude, float frequency)
    {
        return new NoiseSettings.NoiseParams
        {
            Amplitude = amplitude,
            Frequency = frequency,
            Constant = false
        };
    }

    private void OnValidate()
    {
        followInfluence.x = Mathf.Max(0f, followInfluence.x);
        followInfluence.y = Mathf.Max(0f, followInfluence.y);
        lookInfluence.x = Mathf.Max(0f, lookInfluence.x);
        lookInfluence.y = Mathf.Max(0f, lookInfluence.y);
        edgeLookLift = Mathf.Max(0f, edgeLookLift);
        horizontalFollowDamping = Mathf.Max(0f, horizontalFollowDamping);
        verticalFollowDamping = Mathf.Max(0f, verticalFollowDamping);
        horizontalAimDamping = Mathf.Max(0f, horizontalAimDamping);
        verticalAimDamping = Mathf.Max(0f, verticalAimDamping);
        charmSway = Mathf.Max(0f, charmSway);
        charmSwayPeriod = Mathf.Max(0.01f, charmSwayPeriod);
        impactDecayPerSecond = Mathf.Max(0f, impactDecayPerSecond);
    }
}
