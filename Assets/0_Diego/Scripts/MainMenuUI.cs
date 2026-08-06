using Sol.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class MainMenuUI : MonoBehaviour
{
    [Header("Authored Air Footy Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private GameObject rulesPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button instructionsButton;
    [SerializeField] private Button backButton;

    [Header("Authored Mode and Team Selection")]
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private GameObject teamSelectionPanel;
    [SerializeField] private Button twoPlayerModeButton;
    [SerializeField] private Button fourPlayerModeButton;
    [SerializeField] private Button modeBackButton;
    [SerializeField] private Button blueTeamButton;
    [SerializeField] private Button redTeamButton;
    [SerializeField] private Button greenTeamButton;
    [SerializeField] private Button goldTeamButton;
    [SerializeField] private Button teamBackButton;
    [SerializeField] private Button overtimeToggleButton;
    private bool overtimeRequested = true;
    private Button firstModeButton;
    private Button firstTeamButton;
    private GameObject twoPlayerVariant;
    private GameObject fourPlayerVariant;
    private GameObject blueFeedRoot;
    private GameObject redFeedRoot;
    private GameObject greenFeedRoot;
    private GameObject goldFeedRoot;
    private ScoreUI scoreUI;
    private TMP_Text rulesText;
    [SerializeField] private Camera displayCamera;
    private AirFootyGameMode selectedMode = AirFootyGameMode.TwoPlayer;

    public bool IsMatchActive { get; private set; }

    private void Awake()
    {
        Time.timeScale = 0f;
        AudioListener.pause = false;
        AirFootySessionConfig.Clear();

        ResolveGameplayVariants();
        SetGameplayVariantActive(null);
        ResolveScenePresentation();
        SetGameplayPresentationActive(false);
        BuildSelectionPanels();
        ArcadeInputCoordinator.EnsureExists();
        ArcadeInputCoordinator.ShowMenu(mainMenuPanel, startButton);

        WireButton(startButton, StartGame);
        WireButton(instructionsButton, ShowInstructions);
        WireButton(backButton, ShowMainMenu);
    }

    private void Start()
    {
        ShowMainMenu();
    }

    private void Update()
    {
        if (IsMatchActive || !CancelPressedThisFrame())
        {
            return;
        }

        if (teamSelectionPanel != null && teamSelectionPanel.activeInHierarchy)
        {
            ShowModeSelection();
        }
        else if ((modeSelectionPanel != null && modeSelectionPanel.activeInHierarchy) ||
                 (instructionsPanel != null && instructionsPanel.activeInHierarchy))
        {
            ShowMainMenu();
        }
    }

    // The authored START button advances to mode selection.
    public void StartGame()
    {
        ShowModeSelection();
    }

    public void ShowInstructions()
    {
        SetGameplayPresentationActive(false);
        SetMenuPanel(mainMenuPanel, false);
        SetMenuPanel(instructionsPanel, true);
        SetMenuPanel(modeSelectionPanel, false);
        SetMenuPanel(teamSelectionPanel, false);
        SetMenuPanel(rulesPanel, false);
        ArcadeInputCoordinator.SetMenuFocus(instructionsPanel, backButton);
    }

    public void ShowMainMenu()
    {
        Time.timeScale = 0f;
        IsMatchActive = false;
        AirFootySessionConfig.Clear();
        overtimeRequested = true;
        RefreshOvertimeLabel();
        SetGameplayVariantActive(null);
        SetGameplayPresentationActive(false);
        SetMenuPanel(mainMenuPanel, true);
        SetMenuPanel(instructionsPanel, false);
        SetMenuPanel(modeSelectionPanel, false);
        SetMenuPanel(teamSelectionPanel, false);
        SetMenuPanel(rulesPanel, false);
        ArcadeInputCoordinator.SetMenuFocus(mainMenuPanel, startButton);
    }

    public void ShowModeSelection()
    {
        SetGameplayPresentationActive(false);
        SetMenuPanel(mainMenuPanel, false);
        SetMenuPanel(instructionsPanel, false);
        SetMenuPanel(modeSelectionPanel, true);
        SetMenuPanel(teamSelectionPanel, false);
        SetMenuPanel(rulesPanel, false);
        ArcadeInputCoordinator.SetMenuFocus(
            modeSelectionPanel,
            firstModeButton);
    }

    private void SelectMode(AirFootyGameMode mode)
    {
        selectedMode = mode;
        SetGameplayPresentationActive(false);
        bool fourPlayer = mode == AirFootyGameMode.FourPlayer;
        if (greenTeamButton != null)
        {
            greenTeamButton.gameObject.SetActive(fourPlayer);
        }
        if (goldTeamButton != null)
        {
            goldTeamButton.gameObject.SetActive(fourPlayer);
        }

        // Four player elimination always ends in overtime. The button stays on
        // screen so the rule is taught rather than hidden, but cannot be changed.
        if (fourPlayer)
        {
            overtimeRequested = true;
        }
        if (overtimeToggleButton != null)
        {
            overtimeToggleButton.interactable = !fourPlayer;
        }
        RefreshOvertimeLabel();

        SetMenuPanel(modeSelectionPanel, false);
        SetMenuPanel(teamSelectionPanel, true);
        ArcadeInputCoordinator.SetMenuFocus(
            teamSelectionPanel,
            firstTeamButton);
    }

    private void BeginMatch(AirFootyTeam humanTeam)
    {
        if (!AirFootySessionConfig.IsTeamAvailable(selectedMode, humanTeam))
        {
            return;
        }

        GameObject selectedVariant = selectedMode == AirFootyGameMode.FourPlayer
            ? fourPlayerVariant
            : twoPlayerVariant;
        if (selectedVariant == null)
        {
            Debug.LogError(
                $"Air Footy cannot start {selectedMode}: the matching prefab instance is missing from {SceneManager.GetActiveScene().name}.",
                this);
            return;
        }

        AirFootySessionConfig.Configure(selectedMode, humanTeam, overtimeRequested);
        IsMatchActive = true;
        SetMenuPanel(mainMenuPanel, false);
        SetMenuPanel(instructionsPanel, false);
        SetMenuPanel(modeSelectionPanel, false);
        SetMenuPanel(teamSelectionPanel, false);
        ConfigureRuleBanner();
        PrepareScoreDisplay(humanTeam);
        SetMenuPanel(rulesPanel, true);
        SetGameplayPresentationActive(true);
        SetGameplayVariantActive(selectedVariant);
        ArcadeInputCoordinator.EnterGameplay(
            CursorLockMode.Locked,
            false);
        Time.timeScale = 1f;
    }

    private void ResolveGameplayVariants()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "AirFooty_2Player")
            {
                twoPlayerVariant = root;
            }
            else if (root.name == "AirFooty_4Player")
            {
                fourPlayerVariant = root;
            }
            else if (root.name == "Player Blue")
            {
                blueFeedRoot = root;
            }
            else if (root.name == "Player Red")
            {
                redFeedRoot = root;
            }
            else if (root.name == "Player Green")
            {
                greenFeedRoot = root;
            }
            else if (root.name == "Player Gold")
            {
                goldFeedRoot = root;
            }
        }
    }

    private void ResolveScenePresentation()
    {
        scoreUI = FindFirstObjectByType<ScoreUI>(FindObjectsInactive.Include);
        rulesText = rulesPanel != null
            ? rulesPanel.GetComponentInChildren<TMP_Text>(true)
            : null;
        displayCamera = AirFootyCameraLookup.FindDisplayCamera();
        if (displayCamera == null || displayCamera.targetTexture != null)
        {
            Debug.LogWarning(
                "AirFooty is using its recovery display camera because the authored scene camera is missing or targets a texture. " +
                "Check the authored AirFooty scene camera.",
                this);
            GameObject cameraObject = new GameObject("AirFooty Display Camera");
            displayCamera = cameraObject.AddComponent<Camera>();
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = new Color(0.015f, 0.02f, 0.08f, 1f);
            displayCamera.fieldOfView = 42f;
            displayCamera.nearClipPlane = 0.3f;
            displayCamera.farClipPlane = 1000f;
            displayCamera.transform.SetPositionAndRotation(
                new Vector3(-13.34f, 10.5f, -12.37f),
                Quaternion.Euler(30f, 47.146f, 0f));
        }

        displayCamera.targetTexture = null;
        displayCamera.targetDisplay = 0;
        displayCamera.tag = "MainCamera";
        UniversalAdditionalCameraData cameraData =
            displayCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = true;
        if (displayCamera.GetComponent<AudioListener>() == null)
        {
            displayCamera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void SetGameplayVariantActive(GameObject selected)
    {
        if (twoPlayerVariant != null)
        {
            twoPlayerVariant.SetActive(twoPlayerVariant == selected);
        }
        if (fourPlayerVariant != null)
        {
            fourPlayerVariant.SetActive(fourPlayerVariant == selected);
        }
    }

    private void SetGameplayPresentationActive(bool active)
    {
        scoreUI?.SetGameplayHudVisible(active);

        if (displayCamera != null)
        {
            displayCamera.cullingMask = active ? ~0 : 0;
            displayCamera.enabled = true;
        }

        bool fourPlayer = active && selectedMode == AirFootyGameMode.FourPlayer;
        SetRootActive(blueFeedRoot, active);
        SetRootActive(redFeedRoot, active);
        SetRootActive(greenFeedRoot, fourPlayer);
        SetRootActive(goldFeedRoot, fourPlayer);
    }

    private void ConfigureRuleBanner()
    {
        if (rulesText == null)
        {
            return;
        }

        string rule = selectedMode == AirFootyGameMode.FourPlayer
            ? "5 GOALS IN YOUR OWN GOAL = ELIMINATED - LAST TEAM WINS"
            : "FIRST TEAM TO CONCEDE 5 GOALS LOSES";
        bool overtime =
            selectedMode == AirFootyGameMode.FourPlayer || overtimeRequested;
        rulesText.text = overtime
            ? rule + "\nAFTER 5:00 THE BALL GOES LIVE - PULSE ONLY, CONTACT KILLS"
            : rule;
    }

    /// <summary>
    /// Flips the two player overtime rule. Four player cannot be changed, so this
    /// is a no-op there and the label keeps reading MANDATORY.
    /// </summary>
    private void ToggleOvertime()
    {
        if (selectedMode == AirFootyGameMode.FourPlayer)
        {
            return;
        }

        overtimeRequested = !overtimeRequested;
        RefreshOvertimeLabel();
    }

    private void RefreshOvertimeLabel()
    {
        if (overtimeToggleButton == null)
        {
            return;
        }

        string label = selectedMode == AirFootyGameMode.FourPlayer
            ? "OVERTIME: 5:00 (MANDATORY)"
            : overtimeRequested
                ? "OVERTIME: ON"
                : "OVERTIME: OFF";
        SetButtonLabel(overtimeToggleButton, label);
    }

    // The menu mixes TMP and legacy Text labels, so both have to be written.
    private static void SetButtonLabel(Button button, string label)
    {
        TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = label;
        }

        Text legacyLabel = button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.text = label;
        }
    }

    private void PrepareScoreDisplay(AirFootyTeam humanTeam)
    {
        if (scoreUI == null)
        {
            return;
        }

        if (selectedMode == AirFootyGameMode.FourPlayer)
        {
            scoreUI.UpdateEliminationScores(0, 0, 0, 0, 5);
            return;
        }

        AirFootyTeam opponent = humanTeam == AirFootyTeam.Red
            ? AirFootyTeam.Blue
            : AirFootyTeam.Red;
        scoreUI.UpdateHeadToHeadScores(0, 0, humanTeam, opponent);
    }

    private static void SetRootActive(GameObject root, bool active)
    {
        if (root != null && root.activeSelf != active)
        {
            root.SetActive(active);
        }
    }

    private void BuildSelectionPanels()
    {
        if (modeSelectionPanel == null || teamSelectionPanel == null)
        {
            Debug.LogError(
                "Air Footy menu is missing its authored mode/team selection " +
                "panels. Assign them on the MainMenuUI component.",
                this);
            return;
        }

        ResolveAuthoredSelectionButtons();
        WireAuthoredSelectionButtons();
        modeSelectionPanel.SetActive(false);
        teamSelectionPanel.SetActive(false);
    }

    private void ResolveAuthoredSelectionButtons()
    {
        twoPlayerModeButton ??= FindButton(
            modeSelectionPanel,
            "Button 2 PLAYER - 1 BALL");
        fourPlayerModeButton ??= FindButton(
            modeSelectionPanel,
            "Button 4 PLAYER ELIMINATION - 2 BALLS");
        modeBackButton ??= FindButton(modeSelectionPanel, "Button BACK");
        blueTeamButton ??= FindButton(teamSelectionPanel, "Button BLUE");
        redTeamButton ??= FindButton(teamSelectionPanel, "Button RED");
        greenTeamButton ??= FindButton(teamSelectionPanel, "Button GREEN");
        goldTeamButton ??= FindButton(teamSelectionPanel, "Button GOLD");
        teamBackButton ??= FindButton(
            teamSelectionPanel,
            "Button BACK TO MODE");
        overtimeToggleButton ??= FindButton(
            teamSelectionPanel,
            "Button OVERTIME");
        firstModeButton = twoPlayerModeButton;
        firstTeamButton = blueTeamButton;
    }

    private void WireAuthoredSelectionButtons()
    {
        WireButton(
            twoPlayerModeButton,
            () => SelectMode(AirFootyGameMode.TwoPlayer));
        WireButton(
            fourPlayerModeButton,
            () => SelectMode(AirFootyGameMode.FourPlayer));
        WireButton(modeBackButton, ShowMainMenu);
        WireButton(blueTeamButton, () => BeginMatch(AirFootyTeam.Blue));
        WireButton(redTeamButton, () => BeginMatch(AirFootyTeam.Red));
        WireButton(greenTeamButton, () => BeginMatch(AirFootyTeam.Green));
        WireButton(goldTeamButton, () => BeginMatch(AirFootyTeam.Gold));
        WireButton(teamBackButton, ShowModeSelection);
        WireButton(overtimeToggleButton, ToggleOvertime);
    }

    private static Button FindButton(GameObject root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void SetMenuPanel(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
        {
            panel.SetActive(active);
        }
    }

    private static bool CancelPressedThisFrame()
    {
        return Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
               Gamepad.current?.buttonEast.wasPressedThisFrame == true;
    }
}
