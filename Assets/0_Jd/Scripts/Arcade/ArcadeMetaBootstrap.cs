using Sol.Minigames;
using Sol.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Arcade
{
    /// <summary>
    /// Wires the overarching game loop into every scene without prefab edits:
    /// applies saved options, keeps the pause menu alive, and protects the
    /// handcrafted hub from accidentally running Labyrinth maze generation.
    /// </summary>
    public static class ArcadeMetaBootstrap
    {
        private const string HubSceneName = "Sc_ArcadeHub";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            ArcadeOptions.ApplyToListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ConfigureScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The editor restores its backup scene while leaving play mode.
            // Runtime bootstraps must not recreate persistent session objects
            // during that teardown transition.
            if (!Application.isPlaying || mode != LoadSceneMode.Single)
            {
                return;
            }

            ConfigureScene();
        }

        private static void ConfigureScene()
        {
            ConfigureInputContext();
            PauseMenuController.ConfigureForActiveScene();

            bool isHub = SceneManager.GetActiveScene().name == HubSceneName;

            if (isHub && Object.FindFirstObjectByType<HubGameLoop>() == null)
            {
                new GameObject("Hub Game Loop").AddComponent<HubGameLoop>();
            }
        }

        private static void ConfigureInputContext()
        {
            ArcadeInputCoordinator.EnsureExists();

            MainMenu mainMenu = Object.FindFirstObjectByType<MainMenu>();
            MainMenuUI airFootyMenu = Object.FindFirstObjectByType<MainMenuUI>();
            if (mainMenu != null || airFootyMenu != null)
            {
                ArcadeInputCoordinator.ResetForScene(
                    CursorLockMode.Locked,
                    false);
                ArcadeInputCoordinator.ShowMenu(
                    mainMenu != null
                        ? mainMenu.gameObject
                        : airFootyMenu.gameObject);
                return;
            }

            bool pointerDrivenGame =
                Object.FindFirstObjectByType<AtomSmasherGame>() != null ||
                Object.FindFirstObjectByType<NeonReflex.UIManager>() != null;
            ArcadeInputCoordinator.ResetForScene(
                pointerDrivenGame
                    ? CursorLockMode.Confined
                    : CursorLockMode.Locked,
                pointerDrivenGame);
        }
    }
}
