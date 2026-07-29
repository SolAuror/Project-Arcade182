using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Scene-scoped PS1/boomer-shooter presentation for Labyrinth Crawler.
    /// Renders the gameplay camera into a low-res point-filtered
    /// RenderTexture, draws it to screen through the Arcade/PS1/Present
    /// posterize+dither material, and applies olive exponential fog plus the
    /// vertex-snap grid used by Arcade/PS1/Lit. It owns scene-scoped camera,
    /// RenderSettings and shader-global state while active and restores that
    /// state on disable. Screen Space Overlay UI (HUD, upgrade screen) is
    /// unaffected and stays native-res on top.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Retro Presenter")]
    public class RetroPresenter : MonoBehaviour
    {
        #region Configuration

        [Header("Render Target")]
        [Tooltip("Vertical resolution of the low-res target; width follows the screen aspect. 240 = PS1-era line count.")]
        [SerializeField, Range(120, 540)] private int verticalResolution = 240;

        [Tooltip("Vertex-snap grid as a fraction of the render target resolution. 1 = snap to target pixels; lower = wobblier.")]
        [SerializeField, Range(0.25f, 2f)] private float snapScale = 1f;

        [Header("Present")]
        [Tooltip("Material using Arcade/PS1/Present. Instantiated at runtime; leave empty for a plain point-filtered upscale.")]
        [SerializeField] private Material presentMaterial;

        [Header("Storm Sky")]
        [Tooltip("Authored Arcade/PS1/Storm Sky material. Instantiated at runtime so lightning never dirties the asset.")]
        [SerializeField] private Material stormSkyMaterial;

        [Tooltip("How strongly lightning lifts the dungeon fog toward the flash colour.")]
        [SerializeField, Range(0f, 1f)] private float stormFogResponse = 0.45f;

        [Header("Dungeon Murk")]
        [Tooltip("Drive scene fog while this presenter is active.")]
        [SerializeField] private bool overrideFog = true;

        [Tooltip("Fog and camera background colour. Near-black olive keeps the dungeon and storm sky in one palette.")]
        [SerializeField] private Color fogColor = new Color(0.035f, 0.045f, 0.03f, 1f);

        [Tooltip("Exponential fog density. 0.11 preserves local readability while retaining the dungeon murk.")]
        [SerializeField, Range(0f, 0.5f)] private float fogDensity = 0.11f;

        #endregion

        #region Runtime and Saved Scene State

        private static readonly int SnapResolutionId = Shader.PropertyToID("_RetroSnapResolution");
        private static readonly int StormFlashId = Shader.PropertyToID("_StormFlash");
        private static readonly int StormFlashDirectionId =
            Shader.PropertyToID("_StormFlashDirection");
        private static readonly int EntityPresenceId = Shader.PropertyToID("_EntityPresence");
        private static readonly int EntityGlowId = Shader.PropertyToID("_EntityGlow");

        private Camera _gameCamera;
        private RenderTexture _target;
        private Material _presentInstance;
        private Material _stormSkyInstance;
        private Canvas _outputCanvas;
        private RawImage _outputImage;
        private Camera _clearCamera;
        private int _builtScreenWidth;
        private int _builtScreenHeight;

        private CameraClearFlags _previousClearFlags;
        private Color _previousBackground;
        private RenderTexture _previousTarget;

        private bool _previousFog;
        private FogMode _previousFogMode;
        private Color _previousFogColor;
        private float _previousFogDensity;
        private Material _previousSkybox;

        #endregion

        #region Scene-State Lifecycle

        private void OnEnable()
        {
            _previousFog = RenderSettings.fog;
            _previousFogMode = RenderSettings.fogMode;
            _previousFogColor = RenderSettings.fogColor;
            _previousFogDensity = RenderSettings.fogDensity;
            _previousSkybox = RenderSettings.skybox;

            if (overrideFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
            }

            if (stormSkyMaterial != null)
            {
                _stormSkyInstance = new Material(stormSkyMaterial)
                {
                    name = stormSkyMaterial.name + " (Runtime)"
                };
                RenderSettings.skybox = _stormSkyInstance;
            }
        }

        private void OnDisable()
        {
            RenderSettings.fog = _previousFog;
            RenderSettings.fogMode = _previousFogMode;
            RenderSettings.fogColor = _previousFogColor;
            RenderSettings.fogDensity = _previousFogDensity;
            RenderSettings.skybox = _previousSkybox;

            ReleaseCamera();
            TearDownOutput();
            ReleaseTarget();
            if (_stormSkyInstance != null)
            {
                Destroy(_stormSkyInstance);
                _stormSkyInstance = null;
            }
            Shader.SetGlobalVector(SnapResolutionId, Vector4.zero);
        }

        #endregion

        #region Storm Integration

        /// <summary>
        /// Applies one frame of the storm envelope without exposing or
        /// mutating the authored sky material. StormDirector is the expected
        /// caller; keeping fog and the skybox here gives their global state a
        /// single save/restore owner.
        /// </summary>
        public void ApplyStormFlash(
            float intensity,
            Color flashColor,
            Vector3 flashDirection,
            float entityPresence,
            float entityGlow)
        {
            // Parent/child disable order is not guaranteed. If the presenter
            // has already restored RenderSettings, a child StormDirector must
            // not write the dungeon fog back and leak it into the hub.
            if (!isActiveAndEnabled)
            {
                return;
            }

            intensity = Mathf.Clamp01(intensity);

            if (overrideFog)
            {
                float fogLift = intensity * stormFogResponse;
                RenderSettings.fogColor = Color.Lerp(fogColor, flashColor, fogLift);
            }

            if (_stormSkyInstance == null)
            {
                return;
            }

            _stormSkyInstance.SetFloat(StormFlashId, intensity);
            _stormSkyInstance.SetVector(StormFlashDirectionId, new Vector4(
                flashDirection.x,
                flashDirection.y,
                flashDirection.z,
                0f));
            _stormSkyInstance.SetFloat(EntityPresenceId, Mathf.Clamp01(entityPresence));
            _stormSkyInstance.SetFloat(EntityGlowId, Mathf.Max(0f, entityGlow));
        }

        #endregion

        #region Camera and Render Target

        // LateUpdate so the bind happens after PlayerSpawn instantiates the
        // player rig (the camera lives on the runtime-spawned LabCrawlerController).
        private void LateUpdate()
        {
            if (_gameCamera == null || !_gameCamera.isActiveAndEnabled)
            {
                ReleaseCamera();
                TryBindCamera();
            }
            else if (Screen.width != _builtScreenWidth || Screen.height != _builtScreenHeight)
            {
                RebuildTarget();
            }
        }

        private void TryBindCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || cam == _clearCamera)
            {
                return;
            }

            _gameCamera = cam;
            _previousClearFlags = cam.clearFlags;
            _previousBackground = cam.backgroundColor;
            _previousTarget = cam.targetTexture;

            cam.clearFlags = _stormSkyInstance != null
                ? CameraClearFlags.Skybox
                : CameraClearFlags.SolidColor;
            cam.backgroundColor = overrideFog ? fogColor : Color.black;

            RebuildTarget();
            EnsureOutput();
        }

        private void ReleaseCamera()
        {
            if (_gameCamera == null)
            {
                return;
            }

            _gameCamera.targetTexture = _previousTarget;
            _gameCamera.clearFlags = _previousClearFlags;
            _gameCamera.backgroundColor = _previousBackground;
            _gameCamera = null;
        }

        private void RebuildTarget()
        {
            if (_gameCamera == null)
            {
                return;
            }

            _gameCamera.targetTexture = null;
            ReleaseTarget();

            _builtScreenWidth = Screen.width;
            _builtScreenHeight = Screen.height;

            float aspect = Mathf.Max(0.1f, (float)_builtScreenWidth / Mathf.Max(1, _builtScreenHeight));
            int height = Mathf.Max(120, verticalResolution);
            int width = Mathf.Max(160, Mathf.RoundToInt(height * aspect));

            _target = new RenderTexture(width, height, 24)
            {
                name = "RetroPresenter_RT",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                antiAliasing = 1
            };
            _target.Create();

            _gameCamera.targetTexture = _target;
            Shader.SetGlobalVector(SnapResolutionId,
                new Vector4(width * snapScale, height * snapScale, 0f, 0f));

            if (_outputImage != null)
            {
                _outputImage.texture = _target;
            }
            if (_presentInstance != null)
            {
                _presentInstance.mainTexture = _target;
            }
        }

        private void ReleaseTarget()
        {
            if (_target == null)
            {
                return;
            }

            _target.Release();
            Destroy(_target);
            _target = null;
        }

        #endregion

        #region Presentation Output

        private void EnsureOutput()
        {
            if (_outputCanvas != null)
            {
                if (_outputImage != null)
                {
                    _outputImage.texture = _target;
                }
                if (_presentInstance != null)
                {
                    _presentInstance.mainTexture = _target;
                }
                return;
            }

            // Bare camera that owns the backbuffer (clear only, culls
            // nothing). The gameplay camera now renders offscreen, and
            // Screen Space Overlay canvases need a camera to draw over.
            var clearGO = new GameObject("RetroPresenter_Clear");
            clearGO.transform.SetParent(transform, false);
            _clearCamera = clearGO.AddComponent<Camera>();
            _clearCamera.clearFlags = CameraClearFlags.SolidColor;
            _clearCamera.backgroundColor = Color.black;
            _clearCamera.cullingMask = 0;
            _clearCamera.orthographic = true;
            _clearCamera.nearClipPlane = 0.01f;
            _clearCamera.farClipPlane = 1f;
            _clearCamera.depth = -100f;
            _clearCamera.allowMSAA = false;
            _clearCamera.allowHDR = false;
            _clearCamera.useOcclusionCulling = false;

            UniversalAdditionalCameraData cameraData = _clearCamera.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderType = CameraRenderType.Base;
                cameraData.renderPostProcessing = false;
                cameraData.renderShadows = false;
                cameraData.requiresColorOption = CameraOverrideOption.Off;
                cameraData.requiresDepthOption = CameraOverrideOption.Off;
                cameraData.antialiasing = AntialiasingMode.None;
            }

            // Overlay canvas below the HUD (sortingOrder -100) showing the RT
            // through the posterize+dither material.
            var canvasGO = new GameObject("RetroPresenter_Canvas");
            canvasGO.transform.SetParent(transform, false);
            _outputCanvas = canvasGO.AddComponent<Canvas>();
            _outputCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _outputCanvas.sortingOrder = -100;

            var imageGO = new GameObject("Output");
            imageGO.transform.SetParent(canvasGO.transform, false);
            _outputImage = imageGO.AddComponent<RawImage>();
            RectTransform rect = _outputImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (presentMaterial != null)
            {
                _presentInstance = new Material(presentMaterial)
                {
                    name = presentMaterial.name + " (Runtime)",
                    mainTexture = _target
                };
                _outputImage.material = _presentInstance;
            }

            _outputImage.texture = _target;
            _outputImage.raycastTarget = false;
        }

        private void TearDownOutput()
        {
            if (_presentInstance != null)
            {
                Destroy(_presentInstance);
                _presentInstance = null;
            }
            if (_outputCanvas != null)
            {
                Destroy(_outputCanvas.gameObject);
                _outputCanvas = null;
                _outputImage = null;
            }
            if (_clearCamera != null)
            {
                Destroy(_clearCamera.gameObject);
                _clearCamera = null;
            }
        }

        #endregion
    }
}
