using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sol.Minigames
{
    /// <summary>
    /// Drives authored storm-sky, fog and world-lighting assets. It creates no
    /// runtime textures or audio clips; LabyrinthStormAuthoring installs and
    /// wires those assets into the game prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Storm Director")]
    public sealed class StormDirector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private RetroPresenter presenter;
        [SerializeField] private AudioSource thunderSource;
        [SerializeField] private AudioClip[] thunderClips;
        [SerializeField] private Light lightningLight;
        [SerializeField] private LineRenderer boltCore;
        [SerializeField] private LineRenderer boltGlow;

        [Header("Storm Timing")]
        [SerializeField, Min(0.1f)] private float firstStrikeDelay = 2.5f;
        [SerializeField] private Vector2 strikeInterval = new Vector2(5f, 11f);
        [SerializeField] private Vector2 strikeDistance = new Vector2(0.25f, 1f);
        [SerializeField, Range(1, 4)] private int minimumPulses = 1;
        [SerializeField, Range(1, 4)] private int maximumPulses = 2;
        [SerializeField] private Vector2 pulseDuration = new Vector2(0.24f, 0.4f);
        [SerializeField] private Vector2 pulseGap = new Vector2(0.09f, 0.16f);

        [Header("Lightning")]
        [ColorUsage(true, true)]
        [SerializeField] private Color flashColor = new Color(0.72f, 1f, 0.62f, 1f);
        [Tooltip("Supplemental normal-based flash for PS1/Lit. The authored directional Light handles stock URP materials.")]
        [SerializeField, Range(0f, 1f)] private float worldFlashStrength = 0.25f;
        [SerializeField, Min(0f)] private float peakDirectionalIntensity = 1f;
        [SerializeField, Range(0f, 1f)] private float restingEntityPresence = 0.22f;
        [SerializeField, Range(0f, 1f)] private float strikeEntityPresence = 0.95f;
        [SerializeField, Range(0f, 3f)] private float strikeEntityGlow = 1.35f;

        [Header("World-Space Bolt")]
        [SerializeField] private Vector2 boltDistance = new Vector2(48f, 76f);
        [SerializeField, Min(5f)] private float boltHeight = 48f;
        [SerializeField, Range(4, 20)] private int boltSegments = 11;
        [SerializeField, Range(0f, 8f)] private float boltJitter = 2.4f;

        [Header("Local Light Shadow Budget")]
        [Tooltip("Point lights use six shadow-map faces each. Only this many nearest active lights may cast shadows.")]
        [SerializeField, Range(0, 4)] private int maximumShadowedPointLights = 2;
        [SerializeField, Min(0.05f)] private float shadowBudgetRefreshInterval = 0.25f;

        [Header("Ambient")]
        [SerializeField] private bool overrideAmbient = true;
        [ColorUsage(false, true)]
        [SerializeField] private Color ambientColor = new Color(0.14f, 0.16f, 0.11f, 1f);

        private static readonly int StormFlashColorId = Shader.PropertyToID("_StormFlashColor");
        private static readonly int StormFlashDirectionId = Shader.PropertyToID("_StormFlashDirection");

        private AmbientMode _previousAmbientMode;
        private Color _previousAmbientLight;
        private System.Random _random;
        private readonly List<Light> _pointLights = new List<Light>();
        private readonly Dictionary<Light, LightShadows> _authoredShadowModes =
            new Dictionary<Light, LightShadows>();
        private readonly LightDistanceComparer _lightDistanceComparer =
            new LightDistanceComparer();
        private float _nextShadowBudgetRefresh;

        private void Awake()
        {
            if (presenter == null)
            {
                presenter = GetComponentInParent<RetroPresenter>();
            }
            if (thunderSource == null)
            {
                thunderSource = GetComponent<AudioSource>();
            }
            if (lightningLight == null)
            {
                lightningLight = GetComponent<Light>();
            }
        }

        private void OnEnable()
        {
            _previousAmbientMode = RenderSettings.ambientMode;
            _previousAmbientLight = RenderSettings.ambientLight;

            if (overrideAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColor;
            }

            // Do not consume UnityEngine.Random: maze generation and authored
            // gameplay systems may rely on its global sequence.
            _random = new System.Random(unchecked(GetInstanceID() * 397 ^ System.Environment.TickCount));
            RefreshLocalShadowBudget();
            ApplyFrame(0f, Vector3.up, restingEntityPresence, 0f);
            StartCoroutine(StormLoop());
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextShadowBudgetRefresh)
            {
                RefreshLocalShadowBudget();
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            ApplyFrame(0f, Vector3.up, restingEntityPresence, 0f);
            Shader.SetGlobalColor(StormFlashColorId, Color.black);
            Shader.SetGlobalVector(
                StormFlashDirectionId,
                new Vector4(0f, 1f, 0f, 0f));
            SetBoltVisible(0f);
            RestoreLocalShadowModes();

            if (overrideAmbient)
            {
                RenderSettings.ambientMode = _previousAmbientMode;
                RenderSettings.ambientLight = _previousAmbientLight;
            }
        }

        private IEnumerator StormLoop()
        {
            yield return new WaitForSeconds(firstStrikeDelay);

            while (enabled)
            {
                yield return PlayStrike();
                yield return new WaitForSeconds(RandomRange(strikeInterval));
            }
        }

        private IEnumerator PlayStrike()
        {
            float distance = RandomRange(strikeDistance);
            float proximity = 1f - Mathf.InverseLerp(strikeDistance.x, strikeDistance.y, distance);
            float strikeStrength = Mathf.Lerp(0.55f, 1f, proximity);
            float resolveRoll = Mathf.Lerp(0.35f, 1f, NextFloat());
            int pulseCount = RandomRangeInclusive(minimumPulses, maximumPulses);
            Vector3 flashDirection = BuildWorldStrike(distance);

            if (thunderSource != null && thunderClips != null && thunderClips.Length > 0)
            {
                StartCoroutine(PlayThunderAfter(distance, strikeStrength));
            }

            for (int pulse = 0; pulse < pulseCount; pulse++)
            {
                float pulseStrength = pulse == 0 ? 1f : 0.5f;
                float duration = RandomRange(pulseDuration);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                    // A quick illumination swell with an occasional half-
                    // strength echo: between the old strobe and the slower
                    // single burst.
                    float attack = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(normalized / 0.16f));
                    float decay = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01((normalized - 0.3f) / 0.7f));
                    float envelope = Mathf.Min(attack, decay) * pulseStrength;
                    float intensity = envelope * strikeStrength;
                    float presence = Mathf.Lerp(
                        restingEntityPresence,
                        strikeEntityPresence * resolveRoll,
                        envelope);
                    ApplyFrame(
                        intensity,
                        flashDirection,
                        presence,
                        envelope * strikeEntityGlow * resolveRoll);
                    yield return null;
                }

                ApplyFrame(0f, flashDirection, restingEntityPresence, 0f);
                if (pulse + 1 < pulseCount)
                {
                    yield return new WaitForSeconds(RandomRange(pulseGap));
                }
            }
        }

        private IEnumerator PlayThunderAfter(float distance, float strikeStrength)
        {
            // Abstract gameplay distance: enough separation to imply depth
            // without forcing physically scaled kilometres on the maze.
            yield return new WaitForSeconds(Mathf.Lerp(0.15f, 1.7f, distance));

            if (!isActiveAndEnabled || thunderSource == null || thunderClips == null || thunderClips.Length == 0)
            {
                yield break;
            }

            AudioClip clip = thunderClips[RandomRangeInclusive(0, thunderClips.Length - 1)];
            if (clip != null)
            {
                thunderSource.PlayOneShot(clip, Mathf.Lerp(0.45f, 1f, strikeStrength));
            }
        }

        private void ApplyFrame(
            float intensity,
            Vector3 flashDirection,
            float entityPresence,
            float entityGlow)
        {
            if (presenter != null)
            {
                presenter.ApplyStormFlash(
                    intensity,
                    flashColor,
                    flashDirection,
                    entityPresence,
                    entityGlow);
            }

            Shader.SetGlobalColor(
                StormFlashColorId,
                flashColor * (intensity * worldFlashStrength));
            Shader.SetGlobalVector(
                StormFlashDirectionId,
                new Vector4(
                    flashDirection.x,
                    flashDirection.y,
                    flashDirection.z,
                    0f));

            if (lightningLight != null)
            {
                lightningLight.transform.rotation = Quaternion.LookRotation(
                    -flashDirection,
                    Mathf.Abs(Vector3.Dot(flashDirection, Vector3.up)) > 0.98f
                        ? Vector3.forward
                        : Vector3.up);
                lightningLight.color = flashColor;
                lightningLight.intensity = intensity * peakDirectionalIntensity;
                lightningLight.enabled = intensity > 0.001f;
            }

            SetBoltVisible(intensity);
        }

        private Vector3 BuildWorldStrike(float normalizedDistance)
        {
            float yaw = NextFloat() * Mathf.PI * 2f;
            Vector3 horizontalDirection = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

            Camera mainCamera = Camera.main;
            Vector3 viewerPosition = mainCamera != null
                ? mainCamera.transform.position
                : transform.position;
            float distance = Mathf.Lerp(
                Mathf.Min(boltDistance.x, boltDistance.y),
                Mathf.Max(boltDistance.x, boltDistance.y),
                Mathf.Clamp01(normalizedDistance));

            // This is a distant atmospheric event, not a physical strike.
            // Its lower end sits behind the maze skyline and is naturally
            // occluded by world geometry; there is no collision or gameplay
            // impact point.
            Vector3 bottom = viewerPosition + horizontalDirection * distance;
            bottom.y = viewerPosition.y + 2f;

            Vector3 top = bottom + Vector3.up * boltHeight - horizontalDirection * (boltHeight * 0.12f);
            BuildBoltGeometry(top, bottom, horizontalDirection);

            Vector3 directionToStrike = top - viewerPosition;
            return directionToStrike.sqrMagnitude > 0.001f
                ? directionToStrike.normalized
                : Vector3.up;
        }

        private void BuildBoltGeometry(
            Vector3 top,
            Vector3 bottom,
            Vector3 horizontalDirection)
        {
            int pointCount = Mathf.Max(2, boltSegments + 1);
            Vector3[] positions = new Vector3[pointCount];
            Vector3 sideways = Vector3.Cross(Vector3.up, horizontalDirection).normalized;

            for (int index = 0; index < pointCount; index++)
            {
                float t = index / (float)(pointCount - 1);
                Vector3 position = Vector3.Lerp(top, bottom, t);
                if (index > 0 && index + 1 < pointCount)
                {
                    float taper = Mathf.Sin(t * Mathf.PI);
                    float sidewaysOffset = (NextFloat() * 2f - 1f) * boltJitter * taper;
                    float depthOffset = (NextFloat() * 2f - 1f) * boltJitter * 0.45f * taper;
                    position += sideways * sidewaysOffset
                        + horizontalDirection * depthOffset;
                }
                positions[index] = position;
            }

            ApplyBoltPositions(boltGlow, positions);
            ApplyBoltPositions(boltCore, positions);
            SetBoltVisible(0f);
        }

        private static void ApplyBoltPositions(LineRenderer line, Vector3[] positions)
        {
            if (line == null)
            {
                return;
            }

            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }

        private void SetBoltVisible(float intensity)
        {
            SetLineVisible(boltGlow, intensity, 0.28f);
            SetLineVisible(boltCore, intensity, 1f);
        }

        private void SetLineVisible(LineRenderer line, float intensity, float alphaScale)
        {
            if (line == null)
            {
                return;
            }

            bool visible = intensity > 0.025f && line.positionCount > 1;
            line.enabled = visible;
            if (!visible)
            {
                return;
            }

            Color color = Color.Lerp(flashColor, Color.white, 0.62f);
            color.a = Mathf.Clamp01(intensity * alphaScale);
            line.startColor = color;
            line.endColor = color;
        }

        private void RefreshLocalShadowBudget()
        {
            _nextShadowBudgetRefresh = Time.unscaledTime
                + Mathf.Max(0.05f, shadowBudgetRefreshInterval);
            Camera camera = Camera.main;
            Vector3 viewerPosition = camera != null
                ? camera.transform.position
                : transform.position;

            _pointLights.Clear();
            Transform lightRoot = presenter != null
                ? presenter.transform
                : transform.parent != null
                    ? transform.parent
                    : transform;
            lightRoot.GetComponentsInChildren(true, _pointLights);
            for (int index = _pointLights.Count - 1; index >= 0; index--)
            {
                Light light = _pointLights[index];
                if (light == null || light.type != LightType.Point)
                {
                    _pointLights.RemoveAt(index);
                    continue;
                }

                if (!_authoredShadowModes.ContainsKey(light))
                {
                    _authoredShadowModes.Add(light, light.shadows);
                }

                if (light.isActiveAndEnabled)
                {
                    _pointLights.Add(light);
                }
                else
                {
                    light.shadows = LightShadows.None;
                    _pointLights.RemoveAt(index);
                }
            }

            _lightDistanceComparer.Origin = viewerPosition;
            _pointLights.Sort(_lightDistanceComparer);

            int shadowCount = Mathf.Min(
                Mathf.Max(0, maximumShadowedPointLights),
                _pointLights.Count);
            for (int index = 0; index < _pointLights.Count; index++)
            {
                _pointLights[index].shadows = index < shadowCount
                    ? LightShadows.Hard
                    : LightShadows.None;
            }
        }

        private void RestoreLocalShadowModes()
        {
            foreach (KeyValuePair<Light, LightShadows> entry in _authoredShadowModes)
            {
                if (entry.Key != null)
                {
                    entry.Key.shadows = entry.Value;
                }
            }

            _pointLights.Clear();
            _authoredShadowModes.Clear();
        }

        private sealed class LightDistanceComparer : IComparer<Light>
        {
            public Vector3 Origin { get; set; }

            public int Compare(Light a, Light b)
            {
                float distanceA = (a.transform.position - Origin).sqrMagnitude;
                float distanceB = (b.transform.position - Origin).sqrMagnitude;
                return distanceA.CompareTo(distanceB);
            }
        }

        private float NextFloat()
        {
            return (float)_random.NextDouble();
        }

        private float RandomRange(Vector2 range)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return Mathf.Lerp(min, max, NextFloat());
        }

        private int RandomRangeInclusive(int a, int b)
        {
            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            return _random.Next(min, max + 1);
        }
    }
}
