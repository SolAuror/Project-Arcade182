using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement3D))]
[RequireComponent(typeof(AirFootyStrikeMotor3D))]
public sealed class PlayerActions3D : MonoBehaviour
{
    private const int ChargeRingSegments = 36;

    [Header("References")]
    [SerializeField] private PlayerMovement3D playerMovement;
    [SerializeField] private AirFootyStrikeMotor3D strikeMotor;

    [Header("Charge Feel")]
    [SerializeField, Range(0f, 1f)] private float chargingMoveMultiplier = 0.75f;
    [SerializeField, Min(0f)] private float meaningfulAimThreshold = 0.1f;
    [SerializeField, Min(0.1f)] private float chargeRingBaseRadius = 0.72f;
    [SerializeField, Min(0f)] private float chargeRingGrowth = 0.22f;
    [SerializeField] private Color chargeStartColor = new Color(0.12f, 0.62f, 1f, 0.85f);
    [SerializeField] private Color chargeFullColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private Color perfectWindowColor = new Color(1f, 0.88f, 0.35f, 1f);

    private InputAction kickAction;
    private LineRenderer chargeRing;
    private Material chargeRingMaterial;
    private Vector3 lastAimDirection = Vector3.right;
    private float chargeStartedAt;
    private bool actionsEnabled = true;
    private bool charging;

    public Vector3 CurrentAimDirection => lastAimDirection;
    public bool IsCharging => charging;
    public float ChargeFraction =>
        charging && strikeMotor != null
            ? strikeMotor.GetChargeFraction(Time.time - chargeStartedAt)
            : 0f;

    private void Awake()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement3D>();
        }
        if (strikeMotor == null)
        {
            strikeMotor = GetComponent<AirFootyStrikeMotor3D>();
        }

        BuildKickAction();
        BuildChargeRing();
    }

    private void OnEnable()
    {
        kickAction?.Enable();
    }

    private void OnDisable()
    {
        kickAction?.Disable();
        ClearHeldAction();
    }

    private void OnDestroy()
    {
        kickAction?.Dispose();
        if (chargeRingMaterial != null)
        {
            Destroy(chargeRingMaterial);
        }
    }

    private void Update()
    {
        UpdateAimDirection();

        if (!actionsEnabled || Mathf.Approximately(Time.timeScale, 0f))
        {
            ClearHeldAction();
            return;
        }

        if (kickAction.WasPressedThisFrame() &&
            strikeMotor != null &&
            strikeMotor.CanBeginCharge)
        {
            BeginCharge();
        }

        if (charging)
        {
            UpdateChargeRing();
            if (kickAction.WasReleasedThisFrame())
            {
                ReleaseKick();
            }
        }
    }

    public void SetActionsEnabled(bool enabled)
    {
        actionsEnabled = enabled;
        if (!enabled)
        {
            ClearHeldAction();
        }
    }

    public void ClearHeldAction()
    {
        charging = false;
        playerMovement?.SetMoveSpeedMultiplier(1f);
        if (chargeRing != null)
        {
            chargeRing.enabled = false;
        }
    }

    private void UpdateAimDirection()
    {
        if (playerMovement == null)
        {
            return;
        }

        Vector3 movementAim = playerMovement.CurrentMovementDirection;
        if (movementAim.sqrMagnitude >= meaningfulAimThreshold * meaningfulAimThreshold)
        {
            lastAimDirection = movementAim.normalized;
        }
    }

    private void BeginCharge()
    {
        charging = true;
        chargeStartedAt = Time.time;
        playerMovement?.SetMoveSpeedMultiplier(chargingMoveMultiplier);
        if (chargeRing != null)
        {
            chargeRing.enabled = true;
            UpdateChargeRing();
        }
    }

    private void ReleaseKick()
    {
        float heldSeconds = Mathf.Max(0f, Time.time - chargeStartedAt);
        strikeMotor.TryStrike(lastAimDirection, heldSeconds);
        ClearHeldAction();
    }

    private void BuildKickAction()
    {
        kickAction = new InputAction("AirFooty Kick", InputActionType.Button);
        kickAction.AddBinding("<Keyboard>/space");
        kickAction.AddBinding("<Gamepad>/buttonSouth");
    }

    private void BuildChargeRing()
    {
        GameObject ringObject = new GameObject("Kick Charge Ring");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.up * 0.03f;

        chargeRing = ringObject.AddComponent<LineRenderer>();
        chargeRing.useWorldSpace = false;
        chargeRing.loop = true;
        chargeRing.positionCount = ChargeRingSegments;
        chargeRing.startWidth = 0.045f;
        chargeRing.endWidth = 0.045f;
        chargeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        chargeRing.receiveShadows = false;
        chargeRing.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            chargeRingMaterial = new Material(shader)
            {
                name = "AirFooty Kick Charge Ring (Runtime)"
            };
            chargeRing.sharedMaterial = chargeRingMaterial;
        }

        SetChargeRingGeometry(chargeRingBaseRadius);
    }

    private void UpdateChargeRing()
    {
        if (chargeRing == null || strikeMotor == null)
        {
            return;
        }

        float heldSeconds = Mathf.Max(0f, Time.time - chargeStartedAt);
        float charge = strikeMotor.GetChargeFraction(heldSeconds);
        bool perfect = strikeMotor.IsPerfectRelease(heldSeconds);
        Color color = perfect
            ? perfectWindowColor
            : Color.Lerp(chargeStartColor, chargeFullColor, charge);

        chargeRing.startColor = color;
        chargeRing.endColor = color;
        SetChargeRingGeometry(chargeRingBaseRadius + chargeRingGrowth * charge);
    }

    private void SetChargeRingGeometry(float radius)
    {
        for (int i = 0; i < ChargeRingSegments; i++)
        {
            float angle = i / (float)ChargeRingSegments * Mathf.PI * 2f;
            chargeRing.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius));
        }
    }

    private void OnValidate()
    {
        chargingMoveMultiplier = Mathf.Clamp01(chargingMoveMultiplier);
        meaningfulAimThreshold = Mathf.Max(0f, meaningfulAimThreshold);
        chargeRingBaseRadius = Mathf.Max(0.1f, chargeRingBaseRadius);
        chargeRingGrowth = Mathf.Max(0f, chargeRingGrowth);
    }
}
