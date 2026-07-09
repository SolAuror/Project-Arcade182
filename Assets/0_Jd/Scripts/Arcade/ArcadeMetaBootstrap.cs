using Sol.Minigames;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Arcade
{
    /// <summary>
    /// Wires the overarching game loop into every scene without prefab edits:
    /// applies saved options, keeps the pause menu alive, and spawns the hub
    /// game loop (maze regen + golden exit door) whenever the hub loads.
    /// </summary>
    public static class ArcadeMetaBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            ArcadeOptions.ApplyToListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ConfigureScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
            {
                return;
            }

            ConfigureScene();
        }

        private static void ConfigureScene()
        {
            PauseMenuController.ConfigureForActiveScene();

            // The hub is the scene with a maze generator but no labyrinth game.
            bool isHub = Object.FindFirstObjectByType<ArcadeGen3D>() != null &&
                         Object.FindFirstObjectByType<LabyrinthCrawlerGame>() == null &&
                         Object.FindFirstObjectByType<MainMenu>() == null;

            if (isHub && Object.FindFirstObjectByType<HubGameLoop>() == null)
            {
                new GameObject("Hub Game Loop").AddComponent<HubGameLoop>();
            }
        }
    }
}
