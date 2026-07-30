using UnityEngine;

/// <summary>
/// Drives authored neon trim: emissive renderers through a material property
/// block and optional lights through their intensity. Nothing is instantiated
/// and the shared material assets are never written to, so the trim stays
/// tunable in the inspector and the scene looks the same in and out of play
/// mode apart from the pulse itself.
/// </summary>
[DisallowMultipleComponent]
public class AirFootyNeonPulse : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    [Header("Targets")]
    [SerializeField] private Renderer[] emissiveRenderers = new Renderer[0];
    [SerializeField] private Light[] pulseLights = new Light[0];

    [Header("Colour")]
    [SerializeField] private Color restColor = new Color(0.18f, 0.9f, 1f);
    [SerializeField] private bool tintFromPulseColor = true;

    [Header("Levels")]
    [SerializeField, Min(0f)] private float restLevel = 1.35f;
    [SerializeField, Min(0f)] private float pulseLevel = 5.5f;
    [SerializeField, Min(0f)] private float restLightIntensity = 1.8f;
    [SerializeField, Min(0f)] private float pulseLightIntensity = 6.5f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float pulseSeconds = 1.1f;
    [SerializeField, Min(0f)] private float breatheAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float breatheSpeed = 1.3f;

    private MaterialPropertyBlock propertyBlock;
    private Color activeColor;
    private float pulseRemaining;
    private float appliedLevel = -1f;
    private Color appliedColor;

    /// <summary>Flash at the trim's own rest colour.</summary>
    public void Pulse()
    {
        Pulse(restColor);
    }

    /// <summary>
    /// Flash, optionally recolouring the trim to <paramref name="color"/> so a
    /// goal reads in the scoring team's colour.
    /// </summary>
    public void Pulse(Color color)
    {
        activeColor = tintFromPulseColor ? color : restColor;
        pulseRemaining = pulseSeconds;
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        activeColor = restColor;
    }

    private void OnEnable()
    {
        appliedLevel = -1f;
        Apply(restLevel);
        ApplyLights(restLightIntensity);
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        pulseRemaining = Mathf.Max(0f, pulseRemaining - delta);

        // Ease out so the flash snaps on and falls away.
        float progress = pulseRemaining / pulseSeconds;
        float pulseWeight = progress * progress;

        // Only the lights breathe. Emission is deliberately steady at rest so
        // the renderer property blocks below settle and stop being rewritten.
        float breathe = 1f + Mathf.Sin(Time.unscaledTime * breatheSpeed) * breatheAmplitude;
        ApplyLights(Mathf.Lerp(restLightIntensity, pulseLightIntensity, pulseWeight) * breathe);

        Apply(Mathf.Lerp(restLevel, pulseLevel, pulseWeight));
    }

    /// <summary>
    /// Pushes the emission level to the trim, but only when it has actually
    /// changed. Writing property blocks every frame costs real CPU time and keeps
    /// these renderers permanently out of the SRP batcher, so at rest this does
    /// nothing at all.
    /// </summary>
    private void Apply(float level)
    {
        if (Mathf.Abs(level - appliedLevel) < 0.004f && activeColor == appliedColor)
        {
            return;
        }

        appliedLevel = level;
        appliedColor = activeColor;

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Color emissive = activeColor * level;
        emissive.a = 1f;

        for (int index = 0; index < emissiveRenderers.Length; index++)
        {
            Renderer target = emissiveRenderers[index];
            if (target == null)
            {
                continue;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(EmissionColorId, emissive);
            // Unlit trim carries its glow in the base colour instead.
            propertyBlock.SetColor(BaseColorId, emissive);
            propertyBlock.SetColor(LegacyColorId, emissive);
            target.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyLights(float lightIntensity)
    {
        for (int index = 0; index < pulseLights.Length; index++)
        {
            Light target = pulseLights[index];
            if (target == null)
            {
                continue;
            }

            target.color = activeColor;
            target.intensity = lightIntensity;
        }
    }
}
