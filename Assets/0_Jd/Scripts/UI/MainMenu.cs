using Sol.Minigames;
using Sol.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sol.Arcade
{
    /// <summary>
    /// "Insert Coin to Exit" main menu. Prefab-authored (built once by
    /// Sol/Setup/Menus And UI Prefabs); this component only wires behavior
    /// into the referenced widgets — no UI is generated at runtime. Minigames
    /// unlock for direct play after being finished once in the arcade.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Arcade/Main Menu")]
    public class MainMenu : MonoBehaviour
    {
        [Header("Scenes")]
        [SerializeField] private string hubSceneName = "Sc_ArcadeHub";
        [SerializeField] private string labyrinthSceneName = "Sc_LabyrinthCrawler";
        [SerializeField] private string hoopsSceneName = "Sc_Hoops";
        [SerializeField] private string atomSmasherSceneName = "Sc_AtomSmasher";
        [SerializeField] private string airFootySceneName = "AirFootyFinal";

        [Header("Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject minigamesPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject beatenBanner;

        [Header("Root Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button playMinigamesButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Minigame Buttons")]
        [SerializeField] private Button labyrinthButton;
        [SerializeField] private Text labyrinthLabel;
        [SerializeField] private Button hoopsButton;
        [SerializeField] private Text hoopsLabel;
        [SerializeField] private Button atomSmasherButton;
        [SerializeField] private Text atomSmasherLabel;
        [SerializeField] private Button minigamesBackButton;

        [Header("Options")]
        [SerializeField] private Button volumeButton;
        [SerializeField] private Text volumeLabel;
        [SerializeField] private Button optionsBackButton;

        private GameObject activePanel;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SimpleUiBuilder.EnsureEventSystem();
            ArcadeInputCoordinator.ShowMenu(rootPanel, startButton);

            WireButton(startButton, () => LoadScene(hubSceneName));
            WireButton(playMinigamesButton, () => ShowPanel(minigamesPanel));
            WireButton(optionsButton, () => ShowPanel(optionsPanel));
            WireButton(quitButton, QuitGame);
            WireButton(labyrinthButton, () => LoadScene(labyrinthSceneName));
            WireButton(hoopsButton, () => LoadScene(hoopsSceneName));
            WireButton(atomSmasherButton, () => LoadScene(atomSmasherSceneName));
            BuildAirFootyButton();
            WireButton(minigamesBackButton, () => ShowPanel(rootPanel));
            WireButton(volumeButton, CycleVolume);
            WireButton(optionsBackButton, () => ShowPanel(rootPanel));

            RefreshMinigameLock(labyrinthButton, labyrinthLabel, "LABYRINTH CRAWLER", "LabyrinthCrawler");
            RefreshMinigameLock(hoopsButton, hoopsLabel, "HOOPS", "Hoops");
            RefreshMinigameLock(atomSmasherButton, atomSmasherLabel, "ATOM SMASHER", "AtomSmasher");

            if (beatenBanner != null)
            {
                beatenBanner.SetActive(PlayerScoreCarrier.GameBeaten);
            }

            RefreshVolumeLabel();
            ShowPanel(rootPanel);
        }

        private void BuildAirFootyButton()
        {
            if (minigamesPanel == null ||
                minigamesPanel.transform.Find("Button AIR FOOTY") != null)
            {
                return;
            }

            Button airFootyButton = SimpleUiBuilder.CreateButton(
                minigamesPanel.transform,
                "AIR FOOTY",
                28,
                () => LoadScene(airFootySceneName));
            if (minigamesBackButton != null)
            {
                airFootyButton.transform.SetSiblingIndex(
                    minigamesBackButton.transform.GetSiblingIndex());
            }
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction onClick)
        {
            if (button != null)
            {
                button.onClick.AddListener(onClick);
            }
        }

        private static void RefreshMinigameLock(Button button, Text label, string displayName, string minigameId)
        {
            bool unlocked = PlayerScoreCarrier.IsMinigameCompleted(minigameId);
            if (button != null)
            {
                button.interactable = unlocked;
            }

            if (label != null)
            {
                label.text = unlocked ? displayName : $"{displayName}  [LOCKED]";
            }
        }

        // Stick and d-pad navigation is dead until something is selected, so the
        // menu always keeps a live selection — on open, on every panel switch,
        // and after a stray click deselects.
        private void Update()
        {
            if (CancelPressedThisFrame() && activePanel != rootPanel)
            {
                ShowPanel(rootPanel);
                return;
            }

            ArcadeInputCoordinator.SetMenuFocus(
                activePanel,
                FirstInteractable(activePanel));
        }

        private void ShowPanel(GameObject panel)
        {
            SetPanelActive(rootPanel, panel);
            SetPanelActive(minigamesPanel, panel);
            SetPanelActive(optionsPanel, panel);
            activePanel = panel;
            ArcadeInputCoordinator.SetMenuFocus(
                panel,
                FirstInteractable(panel));
        }

        private static Selectable FirstInteractable(GameObject panel)
        {
            if (panel == null)
            {
                return null;
            }

            foreach (Selectable candidate in panel.GetComponentsInChildren<Selectable>(false))
            {
                if (candidate.interactable)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool CancelPressedThisFrame()
        {
            return Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                   Gamepad.current?.buttonEast.wasPressedThisFrame == true;
        }

        private static void SetPanelActive(GameObject candidate, GameObject selected)
        {
            if (candidate != null)
            {
                candidate.SetActive(candidate == selected);
            }
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

        private static void LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"Main menu cannot load '{sceneName}'. Add it to Build Settings.");
                return;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
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
