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
        SimpleUiBuilder.EnsureEventSystem();
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

        AirFootySessionConfig.Configure(selectedMode, humanTeam);
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

        rulesText.text = selectedMode == AirFootyGameMode.FourPlayer
            ? "5 GOALS IN YOUR OWN GOAL = ELIMINATED - LAST TEAM WINS"
            : "FIRST TEAM TO CONCEDE 5 GOALS LOSES";
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
        if (modeSelectionPanel != null && teamSelectionPanel != null)
        {
            ResolveAuthoredSelectionButtons();
            WireAuthoredSelectionButtons();
            modeSelectionPanel.SetActive(false);
            teamSelectionPanel.SetActive(false);
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }
        if (canvas == null)
        {
            Debug.LogError("Air Footy menu requires an active Canvas.", this);
            return;
        }

        modeSelectionPanel = CreateSelectionPanel(canvas.transform, "Air Footy Mode Selection");
        RectTransform modeColumn = SimpleUiBuilder.CreateButtonColumn(
            modeSelectionPanel.transform,
            "Mode Selection Column",
            650f,
            18f);
        SimpleUiBuilder.CreateText(
            modeColumn,
            "Title",
            "SELECT MODE",
            54,
            SimpleUiBuilder.AccentColor);
        SimpleUiBuilder.CreateText(
            modeColumn,
            "Mode Help",
            "Choose the arena before selecting your team",
            24,
            SimpleUiBuilder.TextColor);
        twoPlayerModeButton = SimpleUiBuilder.CreateButton(
            modeColumn,
            "2 PLAYER - 1 BALL",
            30,
            () => SelectMode(AirFootyGameMode.TwoPlayer));
        firstModeButton = twoPlayerModeButton;
        fourPlayerModeButton = SimpleUiBuilder.CreateButton(
            modeColumn,
            "4 PLAYER ELIMINATION - 2 BALLS",
            30,
            () => SelectMode(AirFootyGameMode.FourPlayer));
        modeBackButton = SimpleUiBuilder.CreateButton(
            modeColumn,
            "BACK",
            26,
            ShowMainMenu);

        teamSelectionPanel = CreateSelectionPanel(canvas.transform, "Air Footy Team Selection");
        RectTransform teamColumn = SimpleUiBuilder.CreateButtonColumn(
            teamSelectionPanel.transform,
            "Team Selection Column",
            650f,
            14f);
        SimpleUiBuilder.CreateText(
            teamColumn,
            "Title",
            "SELECT YOUR TEAM",
            50,
            SimpleUiBuilder.AccentColor);
        SimpleUiBuilder.CreateText(
            teamColumn,
            "Team Help",
            "All unselected teams are AI controlled",
            23,
            SimpleUiBuilder.TextColor);
        blueTeamButton = SimpleUiBuilder.CreateButton(
            teamColumn,
            "BLUE",
            30,
            () => BeginMatch(AirFootyTeam.Blue));
        firstTeamButton = blueTeamButton;
        redTeamButton = SimpleUiBuilder.CreateButton(
            teamColumn,
            "RED",
            30,
            () => BeginMatch(AirFootyTeam.Red));
        greenTeamButton = SimpleUiBuilder.CreateButton(
            teamColumn,
            "GREEN",
            30,
            () => BeginMatch(AirFootyTeam.Green));
        goldTeamButton = SimpleUiBuilder.CreateButton(
            teamColumn,
            "GOLD",
            30,
            () => BeginMatch(AirFootyTeam.Gold));
        teamBackButton = SimpleUiBuilder.CreateButton(
            teamColumn,
            "BACK TO MODE",
            26,
            ShowModeSelection);

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

    private static GameObject CreateSelectionPanel(Transform parent, string panelName)
    {
        Image tint = SimpleUiBuilder.CreateFullScreenTint(
            parent,
            panelName,
            new Color(0.025f, 0.035f, 0.075f, 0.97f));
        tint.gameObject.transform.SetAsLastSibling();
        return tint.gameObject;
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
