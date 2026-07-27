using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
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
        Time.timeScale = 0f;
        ShowMainMenu();
        rulesPanel.SetActive(false);
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
