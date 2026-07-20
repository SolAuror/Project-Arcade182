using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Death-screen controls. Lives on the (initially inactive) RunOverGroup
    /// inside the LabyrinthCrawlerHud prefab; LabyrinthHud toggles the group
    /// with the game's fail state. While open this frees the cursor and
    /// routes the Restart / Quit buttons (or the R / Q keys) to the game.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Run Over Screen")]
    public class LabyrinthRunOverScreen : MonoBehaviour
    {
        [Tooltip("Found automatically when left empty.")]
        [SerializeField] private LabyrinthCrawlerGame game;

        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        private void Awake()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(Quit);
            }
        }

        private void OnEnable()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<LabyrinthCrawlerGame>();
            }

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDisable()
        {
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                Restart();
            }
            else if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                Quit();
            }
        }

        private void Restart()
        {
            if (game != null)
            {
                game.RestartRun();
            }
        }

        private void Quit()
        {
            if (game != null)
            {
                game.QuitToHub();
            }
        }
    }
}
