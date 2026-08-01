using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public static class AirFootyFeedbackUtility
{
    private const int SampleRate = 44100;

    private static AudioClip impactClip;
    private static AudioClip goalClip;
    private static AudioClip countdownClip;
    private static Material particleMaterial;

    public static AudioClip ImpactClip =>
        impactClip != null ? impactClip : impactClip = CreateImpactClip();

    public static AudioClip GoalClip =>
        goalClip != null ? goalClip : goalClip = CreateGoalClip();

    public static AudioClip CountdownClip =>
        countdownClip != null ? countdownClip : countdownClip = CreateCountdownClip();

    public static void SpawnGoalBurst(Vector3 position, Color color)
    {
        GameObject burstObject = new GameObject("AirFooty Goal Burst");
        burstObject.transform.position = position;

        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
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
        renderer.sharedMaterial = GetParticleMaterial();

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

    private static Material GetParticleMaterial()
    {
        if (particleMaterial != null)
        {
            return particleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader != null)
        {
            particleMaterial = new Material(shader)
            {
                name = "AirFooty Goal Particles (Runtime)"
            };
        }

        return particleMaterial;
    }

    private static AudioClip CreateImpactClip()
    {
        const float duration = 0.075f;
        return CreateClip("AirFooty Impact", duration, (time, progress) =>
        {
            float envelope = (1f - progress) * (1f - progress);
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(230f, 105f, progress) * time);
            float tick = Mathf.Sin(2f * Mathf.PI * 920f * time) * (1f - Mathf.Clamp01(progress * 6f));
            return (tone * 0.65f + tick * 0.35f) * envelope * 0.5f;
        });
    }

    private static AudioClip CreateGoalClip()
    {
        const float duration = 0.52f;
        return CreateClip("AirFooty Goal", duration, (time, progress) =>
        {
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress)) * (1f - progress * 0.35f);
            float first = Mathf.Sin(2f * Mathf.PI * 392f * time);
            float second = Mathf.Sin(2f * Mathf.PI * 523.25f * time);
            float octave = Mathf.Sin(2f * Mathf.PI * 784f * time) * 0.25f;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.65f, progress));
            return Mathf.Lerp(first, second + octave, blend) * envelope * 0.23f;
        });
    }

    private static AudioClip CreateCountdownClip()
    {
        const float duration = 0.09f;
        return CreateClip("AirFooty Countdown", duration, (time, progress) =>
        {
            float envelope = 1f - progress;
            return Mathf.Sin(2f * Mathf.PI * 660f * time) * envelope * envelope * 0.2f;
        });
    }

    private static AudioClip CreateClip(
        string clipName,
        float duration,
        System.Func<float, float, float> sample)
    {
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] data = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / SampleRate;
            float progress = (float)i / Mathf.Max(1, sampleCount - 1);
            data[i] = Mathf.Clamp(sample(time, progress), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

public sealed class AirFootyWorldPopup : MonoBehaviour
{
    private TextMesh textMesh;
    private Color baseColor;
    private float startTime;
    private float lifeSeconds;

    public static void Spawn(Vector3 position, string message, Color color)
    {
        GameObject popupObject = new GameObject("AirFooty Goal Message");
        popupObject.transform.position = position;
        AirFootyWorldPopup popup = popupObject.AddComponent<AirFootyWorldPopup>();
        popup.Configure(message, color, 0.9f);
    }

    private void Configure(string message, Color color, float lifetime)
    {
        textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.text = message;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 72;
        textMesh.characterSize = 0.04f;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.color = color;

        MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        baseColor = color;
        lifeSeconds = lifetime;
        startTime = Time.unscaledTime;
    }

    private void Update()
    {
        float progress = (Time.unscaledTime - startTime) / Mathf.Max(0.01f, lifeSeconds);
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Camera view = AirFootyCameraLookup.FindDisplayCamera();
        if (view != null)
        {
            transform.rotation = view.transform.rotation;
        }

        transform.position += Vector3.up * (0.7f * Time.unscaledDeltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.15f, 1f - (1f - progress) * (1f - progress));
        textMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - progress);
    }
}
