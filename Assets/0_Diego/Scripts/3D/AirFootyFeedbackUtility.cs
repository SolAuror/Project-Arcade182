using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public static class AirFootyFeedbackUtility
{
    private const float VaporiseBurstMaximumLifetime = 4f;

    private static AudioClip impactClip;
    private static AudioClip goalClip;
    private static AudioClip countdownClip;
    private static AudioClip vaporiseClip;
    private static GameObject vaporisePrefab;
    private static bool missingImpactReported;
    private static bool missingGoalReported;
    private static bool missingCountdownReported;
    private static bool missingVaporiseReported;
    private static bool missingVaporisePrefabReported;

    public static void Configure(
        AudioClip authoredImpactClip,
        AudioClip authoredGoalClip,
        AudioClip authoredCountdownClip,
        AudioClip authoredVaporiseClip,
        GameObject authoredVaporisePrefab)
    {
        impactClip = authoredImpactClip;
        goalClip = authoredGoalClip;
        countdownClip = authoredCountdownClip;
        vaporiseClip = authoredVaporiseClip;
        vaporisePrefab = authoredVaporisePrefab;
        missingImpactReported = false;
        missingGoalReported = false;
        missingCountdownReported = false;
        missingVaporiseReported = false;
        missingVaporisePrefabReported = false;
    }

    public static AudioClip ImpactClip =>
        RequiredClip(impactClip, "impact", ref missingImpactReported);

    public static AudioClip GoalClip =>
        RequiredClip(goalClip, "goal", ref missingGoalReported);

    public static AudioClip CountdownClip =>
        RequiredClip(countdownClip, "countdown", ref missingCountdownReported);

    /// <summary>
    /// The authored vaporise sting. Baked to a wav asset rather than synthesised,
    /// so it is auditable in the project like every other authored asset.
    /// </summary>
    public static AudioClip VaporiseClip
    {
        get
        {
            if (vaporiseClip != null)
            {
                return vaporiseClip;
            }

            if (!missingVaporiseReported)
            {
                missingVaporiseReported = true;
                Debug.LogWarning(
                    "AirFooty is missing its authored vaporise clip. Falling back to the goal clip. " +
                    "Check the authored AirFooty feedback assets.");
            }

            return GoalClip;
        }
    }

    /// <summary>
    /// Spawns the authored vaporise burst, tinted to the victim's team. Falls back
    /// to the runtime goal burst only if the prefab has not been authored yet.
    /// </summary>
    public static void SpawnVaporiseBurst(Vector3 position, Color color)
    {
        if (vaporisePrefab == null)
        {
            if (!missingVaporisePrefabReported)
            {
                missingVaporisePrefabReported = true;
                Debug.LogWarning(
                    "AirFooty is missing its authored vaporise VFX prefab. Falling back to the goal burst. " +
                    "Check the authored AirFooty feedback assets.");
            }
            SpawnGoalBurst(position, color);
            return;
        }

        GameObject instance = Object.Instantiate(vaporisePrefab, position, Quaternion.identity);
        instance.name = "AirFooty Vaporise Burst";

        // The prefab owns its shape, timing and lifetime. The only thing decided
        // at spawn is whose colour it wears.
        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        }

        // Backstop so a mis-authored prefab cannot leak an object per kill.
        Object.Destroy(instance, VaporiseBurstMaximumLifetime);
    }

    public static void SpawnGoalBurst(Vector3 position, Color color)
    {
        GameObject burstObject =
            AirFootyPrefabLibrary.InstantiateGoalBurst(position);
        if (burstObject == null)
        {
            Debug.LogError("AirFooty goal burst prefab is missing.");
            return;
        }
        else
        {
            burstObject.name = "AirFooty Goal Burst";
        }

        ParticleSystem particles = burstObject.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            Debug.LogError("AirFooty goal burst prefab is missing its authored ParticleSystem.", burstObject);
            Object.Destroy(burstObject);
            return;
        }
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 34)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 62f;
        shape.radius = 0.45f;
        shape.rotation = new Vector3(0f, 90f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLife = particles.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color, 0.25f),
                new GradientColorKey(color * 0.65f, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLife.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.12f;
        renderer.lengthScale = 1.8f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (renderer.sharedMaterial == null)
        {
            Debug.LogError("AirFooty goal burst ParticleSystem is missing its authored material.", renderer);
            Object.Destroy(burstObject);
            return;
        }

        particles.Play();
    }

    public static IEnumerator FlashRenderer(Renderer target, Color flashColor, float seconds)
    {
        if (target == null)
        {
            yield break;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        target.GetPropertyBlock(block);

        Color baseColor = Color.white;
        Material material = target.sharedMaterial;
        if (material != null)
        {
            if (material.HasProperty("_BaseColor"))
            {
                baseColor = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                baseColor = material.GetColor("_Color");
            }
        }

        block.SetColor("_BaseColor", flashColor);
        block.SetColor("_Color", flashColor);
        target.SetPropertyBlock(block);

        float finishAt = Time.unscaledTime + Mathf.Max(0.01f, seconds);
        while (Time.unscaledTime < finishAt)
        {
            yield return null;
        }

        if (target != null)
        {
            block.SetColor("_BaseColor", baseColor);
            block.SetColor("_Color", baseColor);
            target.SetPropertyBlock(block);
        }
    }

    private static AudioClip RequiredClip(
        AudioClip clip,
        string clipRole,
        ref bool reported)
    {
        if (clip == null && !reported)
        {
            reported = true;
            Debug.LogError(
                $"AirFooty is missing its authored {clipRole} clip. " +
                "Check the authored AirFooty feedback assets.");
        }

        return clip;
    }
}
