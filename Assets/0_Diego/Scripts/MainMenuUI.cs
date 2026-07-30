using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Arcade Flow")]
    [Tooltip("Start gameplay immediately after the arcade cabinet loads this scene.")]
    [SerializeField] private bool startImmediately = true;

    [Header("Standalone Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private GameObject rulesPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button instructionsButton;
    [SerializeField] private Button backButton;

    private void Awake()
    {
        startButton.onClick.AddListener(StartGame);
        instructionsButton.onClick.AddListener(ShowInstructions);
        backButton.onClick.AddListener(ShowMainMenu);
    }

    private void Start()
    {
        if (startImmediately)
        {
            StartGame();
        }
        else
        {
            Time.timeScale = 0f;
            ShowMainMenu();
            rulesPanel.SetActive(false);
        }
    }

    public void StartGame()
    {
        mainMenuPanel.SetActive(false);
        instructionsPanel.SetActive(false);
        rulesPanel.SetActive(true);
        Time.timeScale = 1f;
    }

    public void ShowInstructions()
    {
        mainMenuPanel.SetActive(false);
        instructionsPanel.SetActive(true);
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        instructionsPanel.SetActive(false);
    }
}
