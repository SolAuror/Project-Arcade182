using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Prefab-authored HUD for Atom Smasher. All visuals live in the
    /// AtomSmasherHud prefab and can be restyled by hand; this component only
    /// pushes current game state into the wired widgets each frame.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher/Hud")]
    public class AtomSmasherHud : MonoBehaviour
    {
        [Header("Game")]
        [Tooltip("Found automatically when left empty.")]
        [SerializeField] private AtomSmasherGame game;

        [Header("Info Panel")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text shotsText;
        [SerializeField] private Text targetsText;
        [SerializeField] private Text multiplierText;
        [SerializeField] private GameObject timerRow;
        [SerializeField] private Text timerText;

        [Header("Ball Rack")]
        [Tooltip("Authored ball icons (Peggle-style tube), one toggled on per remaining ball.")]
        [SerializeField] private List<Image> ballIcons = new List<Image>();

        [Tooltip("Shows +N when the rack overflows past the authored icons.")]
        [SerializeField] private Text ballOverflowText;

        [Header("Status")]
        [SerializeField] private Text statusText;

        [Header("Result")]
        [SerializeField] private GameObject resultGroup;
        [SerializeField] private Text resultText;

        private int lastShownShots = -1;

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<AtomSmasherGame>();
            }
        }

        private void Update()
        {
            if (game == null)
            {
                return;
            }

            SetText(scoreText, $"Score {game.Score}");
            SetText(waveText, $"Wave {game.WaveNumber}");
            SetText(shotsText, $"Shots {game.ShotsRemaining}");
            SetText(targetsText, $"Targets left {game.RequiredTargetsRemaining}");
            SetText(multiplierText, $"Chain x{game.CurrentShotMultiplier}");
            UpdateBallRack();

            if (timerRow != null && timerRow.activeSelf != game.UseTimerMode)
            {
                timerRow.SetActive(game.UseTimerMode);
            }

            if (game.UseTimerMode)
            {
                SetText(timerText, $"Timer {Mathf.CeilToInt(game.TimeRemaining)}s");
            }

            UpdateStatus();
            UpdateResult();
        }

        // Peggle-style rack: the icons are authored in the HUD prefab; this
        // only toggles them, overflowing past the last icon into a +N label.
        private void UpdateBallRack()
        {
            if (ballIcons.Count == 0)
            {
                return;
            }

            int shots = Mathf.Max(0, game.ShotsRemaining);
            if (shots == lastShownShots)
            {
                return;
            }

            lastShownShots = shots;

            for (int i = 0; i < ballIcons.Count; i++)
            {
                if (ballIcons[i] != null)
                {
                    ballIcons[i].gameObject.SetActive(i < shots);
                }
            }

            SetText(ballOverflowText, shots > ballIcons.Count ? $"+{shots - ballIcons.Count}" : string.Empty);
        }

        private void UpdateStatus()
        {
            string status = game.StatusMessage;
            if (string.IsNullOrEmpty(status) && game.CanLaunch)
            {
                status = "Aim and release to launch.";
            }

            SetText(statusText, status ?? string.Empty);
        }

        private void UpdateResult()
        {
            bool show = game.IsComplete || game.HasFailed;
            if (resultGroup != null && resultGroup.activeSelf != show)
            {
                resultGroup.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            string headline = game.IsComplete ? "BOARD CLEARED" : game.FailReason.ToUpperInvariant();
            SetText(resultText,
                $"{headline}\nScore {game.Score}   Best {game.BestRecordedScore}   Tickets +{game.TicketsAwarded}");
        }

        private static void SetText(Text target, string value)
        {
            if (target != null && target.text != value)
            {
                target.text = value;
            }
        }
    }
}
