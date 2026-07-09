using Sol.Minigames;
using Sol.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sol.Arcade
{
    /// <summary>
    /// Escape-key pause menu. Prefab-authored (Resources/UI/PauseMenu, built
    /// once by Sol/Setup/Menus And UI Prefabs) and instantiated for the whole
    /// session by <see cref="ArcadeMetaBootstrap"/> — no UI is generated at
    /// runtime. Minigame scenes get Resume / Quit to Hub (Atom Smasher adds a
    /// 2D-3D camera toggle); the hub gets Resume / Options / Quit to Menu /
    /// Quit Game. The main menu scene gets nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Pause Menu Controller")]
    public class PauseMenuController : MonoBehaviour
    {
        private enum PauseContext
        {
            None,
            Hub,
            Minigame
        }

        private const string PauseMenuResourcePath = "UI/PauseMenu";
        private const string AtomSmasherOrthoPlayerPrefsKey = "AtomSmasher.OrthographicView";

        private static PauseMenuController instance;

        [Header("Scenes")]
        [SerializeField] private string hubSceneName = "Sc_ArcadeHub";
        [SerializeField] private string menuSceneName = "Sc_MainMenu";

        [Header("Widgets")]
        [SerializeField] private GameObject pauseCanvas;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button viewToggleButton;
        [SerializeField] private Text viewToggleLabel;
        [SerializeField] private Button quitToHubButton;
        [SerializeField] private Button volumeButton;
        [SerializeField] private Text volumeLabel;
        [SerializeField] private Button quitToMenuButton;
        [SerializeField] private Button quitGameButton;

        private PauseContext context;
        private AtomSmasherGame atomSmasherGame;
        private float pausedFromTimeScale = 1f;
        private CursorLockMode pausedFromLockMode;
        private bool isPaused;

        // Authored board-camera projection, captured per Atom Smasher scene so
        // the 2D/3D toggle always frames the play area exactly as authored.
        private bool authoredProjectionCaptured;
        private bool authoredOrthographic;
        private float authoredOrthographicSize;
        private float authoredFieldOfView;

        public static void EnsureExists()
        {
            if (instance != null)
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(PauseMenuResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"Pause menu prefab missing at Resources/{PauseMenuResourcePath}. Run Sol/Setup/Menus And UI Prefabs.");
                return;
            }

            GameObject controllerObject = Instantiate(prefab);
            controllerObject.name = "Pause Menu Controller";
            instance = controllerObject.GetComponent<PauseMenuController>();
            DontDestroyOnLoad(controllerObject);
        }

        public static void ConfigureForActiveScene()
        {
            EnsureExists();
            if (instance != null)
            {
                instance.Configure();
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            WireButton(resumeButton, Resume);
            WireButton(viewToggleButton, ToggleAtomSmasherView);
            WireButton(quitToHubButton, () => QuitToScene(hubSceneName));
            WireButton(volumeButton, CycleVolume);
            WireButton(quitToMenuButton, () => QuitToScene(menuSceneName));
            WireButton(quitGameButton, QuitGame);

            if (pauseCanvas != null)
            {
                pauseCanvas.SetActive(false);
            }
        }

        private void Update()
        {
            if (context == PauseContext.None || pauseCanvas == null)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }

            if (isPaused)
            {
                // Keep the cursor free while paused; controllers re-lock on resume.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Configure()
        {
            if (isPaused)
            {
                Resume();
            }

            atomSmasherGame = FindFirstObjectByType<AtomSmasherGame>();

            if (FindFirstObjectByType<MainMenu>() != null)
            {
                context = PauseContext.None;
            }
            else if (atomSmasherGame != null ||
                     FindFirstObjectByType<HoopsGame>() != null ||
                     FindFirstObjectByType<LabyrinthCrawlerGame>() != null)
            {
                context = PauseContext.Minigame;
            }
            else
            {
                context = PauseContext.Hub;
            }

            bool minigame = context == PauseContext.Minigame;
            SetButtonVisible(viewToggleButton, minigame && atomSmasherGame != null);
            SetButtonVisible(quitToHubButton, minigame);
            SetButtonVisible(volumeButton, context == PauseContext.Hub);
            SetButtonVisible(quitToMenuButton, context == PauseContext.Hub);
            SetButtonVisible(quitGameButton, context == PauseContext.Hub);

            RefreshVolumeLabel();

            authoredProjectionCaptured = false;
            if (atomSmasherGame != null)
            {
                CaptureAuthoredProjection();
                ApplySavedAtomSmasherView();
                RefreshViewToggleLabel();
            }
        }

        private void CaptureAuthoredProjection()
        {
            Camera boardCamera = FindBoardCamera();
            if (boardCamera == null)
            {
                return;
            }

            authoredOrthographic = boardCamera.orthographic;
            authoredOrthographicSize = boardCamera.orthographicSize;
            authoredFieldOfView = boardCamera.fieldOfView;
            authoredProjectionCaptured = true;
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction onClick)
        {
            if (button != null)
            {
                button.onClick.AddListener(onClick);
            }
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null && button.gameObject.activeSelf != visible)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void Pause()
        {
            isPaused = true;
            pausedFromTimeScale = Time.timeScale;
            pausedFromLockMode = Cursor.lockState;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            SimpleUiBuilder.EnsureEventSystem();
            pauseCanvas.SetActive(true);
        }

        private void Resume()
        {
            if (!isPaused)
            {
                if (pauseCanvas != null)
                {
                    pauseCanvas.SetActive(false);
                }

                return;
            }

            isPaused = false;
            Time.timeScale = pausedFromTimeScale;
            AudioListener.pause = false;
            Cursor.lockState = pausedFromLockMode;
            Cursor.visible = pausedFromLockMode != CursorLockMode.Locked;
            pauseCanvas.SetActive(false);
        }

        private void QuitToScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"Pause menu cannot load '{sceneName}'. Add it to Build Settings.");
                return;
            }

            isPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            pauseCanvas.SetActive(false);
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        private void CycleVolume()
        {
            AudioListener.volume = ArcadeOptions.CycleMasterVolume();
            RefreshVolumeLabel();
        }

        private void RefreshVolumeLabel()
        {
            if (volumeLabel != null)
            {
                volumeLabel.text = $"VOLUME: {Mathf.RoundToInt(ArcadeOptions.MasterVolume * 100f)}%";
            }
        }

        // --- Atom Smasher 2D/3D view -------------------------------------

        private void ToggleAtomSmasherView()
        {
            Camera boardCamera = FindBoardCamera();
            if (boardCamera == null)
            {
                return;
            }

            SetBoardCameraOrthographic(boardCamera, !boardCamera.orthographic);
            PlayerPrefs.SetInt(AtomSmasherOrthoPlayerPrefsKey, boardCamera.orthographic ? 1 : 0);
            PlayerPrefs.Save();
            RefreshViewToggleLabel();
        }

        private void ApplySavedAtomSmasherView()
        {
            if (!PlayerPrefs.HasKey(AtomSmasherOrthoPlayerPrefsKey))
            {
                return;
            }

            Camera boardCamera = FindBoardCamera();
            if (boardCamera != null)
            {
                SetBoardCameraOrthographic(boardCamera, PlayerPrefs.GetInt(AtomSmasherOrthoPlayerPrefsKey) == 1);
            }
        }

        // The camera never moves. Returning to the authored mode restores the
        // authored values exactly; the other mode derives its parameter so the
        // board plane keeps the authored framing (no zoom jump, no dead space).
        private void SetBoardCameraOrthographic(Camera boardCamera, bool orthographic)
        {
            if (!authoredProjectionCaptured)
            {
                CaptureAuthoredProjection();
            }

            float planeZ = atomSmasherGame != null ? atomSmasherGame.PhysicsPlaneZ : 0f;
            float distance = Mathf.Max(0.1f, Mathf.Abs(boardCamera.transform.position.z - planeZ));

            if (orthographic == authoredOrthographic)
            {
                boardCamera.orthographicSize = authoredOrthographicSize;
                boardCamera.fieldOfView = authoredFieldOfView;
            }
            else if (orthographic)
            {
                // Authored perspective -> matching ortho size at the board plane.
                boardCamera.orthographicSize = distance * Mathf.Tan(authoredFieldOfView * 0.5f * Mathf.Deg2Rad);
            }
            else
            {
                // Authored ortho -> matching FOV at the board plane.
                boardCamera.fieldOfView = 2f * Mathf.Atan(authoredOrthographicSize / distance) * Mathf.Rad2Deg;
            }

            boardCamera.orthographic = orthographic;

            // The shake FX writes FOV/ortho size every frame it's active; give
            // it the new baseline so it doesn't fight the toggle.
            AtomSmasherCameraFx cameraFx = boardCamera.GetComponent<AtomSmasherCameraFx>();
            if (cameraFx != null)
            {
                cameraFx.RefreshBaseProjection();
            }
        }

        private Camera FindBoardCamera()
        {
            AtomSmasherCameraFx cameraFx = FindFirstObjectByType<AtomSmasherCameraFx>();
            return cameraFx != null ? cameraFx.GetComponent<Camera>() : Camera.main;
        }

        private void RefreshViewToggleLabel()
        {
            if (viewToggleLabel == null)
            {
                return;
            }

            Camera boardCamera = FindBoardCamera();
            bool orthographic = boardCamera != null && boardCamera.orthographic;
            viewToggleLabel.text = orthographic ? "VIEW: 2D (FLAT)" : "VIEW: 3D (DEPTH)";
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
