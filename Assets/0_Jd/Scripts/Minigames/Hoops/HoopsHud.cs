using UnityEngine;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Prefab-authored HUD for Hoops. All visuals live in the HoopsHud prefab
    /// and can be restyled by hand; this component only pushes current game
    /// state into the wired widgets each frame.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Hoops/Hud")]
    public class HoopsHud : MonoBehaviour
    {
        [Header("Game")]
        [Tooltip("Found automatically when left empty.")]
        [SerializeField] private HoopsGame game;

        [Header("Scoreboard")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text timerText;

        [Tooltip("Timer text switches to Alert Color when this many seconds remain.")]
        [SerializeField, Min(0f)] private float alertSeconds = 10f;

        [SerializeField] private Color timerColor = new Color32(0xF2, 0xE9, 0xD8, 0xFF);
        [SerializeField] private Color alertColor = new Color32(0xE8, 0x72, 0x2A, 0xFF);

        [Header("Active Hoop")]
        [SerializeField] private GameObject activeHoopGroup;
        [SerializeField] private Text activeHoopText;

        [Header("Streak")]
        [SerializeField] private GameObject streakGroup;
        [SerializeField] private Text streakText;

        [Tooltip("Fill draining down as the streak window runs out.")]
        [SerializeField] private Image streakFill;

        [SerializeField] private Color streakColorCool = new Color32(0xF2, 0xC1, 0x4E, 0xFF);
        [SerializeField] private Color streakColorHot = new Color32(0xE8, 0x3A, 0x2A, 0xFF);

        [Tooltip("Scale punch applied when the streak grows.")]
        [SerializeField, Min(1f)] private float streakPunchScale = 1.35f;

        [SerializeField, Min(0.1f)] private float streakPunchDecay = 6f;

        [Header("Result")]
        [SerializeField] private GameObject resultGroup;
        [SerializeField] private Text resultText;

        private int lastShownStreak;

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<HoopsGame>();
            }
        }

        private void Update()
        {
            if (game == null)
            {
                return;
            }

            SetText(scoreText, $"Score {game.Score}");

            if (timerText != null)
            {
                float remaining = game.RemainingSeconds;
                SetText(timerText, $"Time {remaining:0.0}s");

                bool alert = game.IsRunning && remaining <= alertSeconds && remaining > 0f;
                timerText.color = alert ? alertColor : timerColor;
                timerText.transform.localScale = alert
                    ? Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 8f))
                    : Vector3.one;
            }

            UpdateActiveHoop();
            UpdateStreak();
            UpdateResult();
        }

        private void UpdateStreak()
        {
            if (streakGroup == null)
            {
                return;
            }

            int streak = game.StreakCount;
            bool show = game.IsRunning && streak >= 2;
            if (streakGroup.activeSelf != show)
            {
                streakGroup.SetActive(show);
            }

            if (!show)
            {
                lastShownStreak = streak;
                streakGroup.transform.localScale = Vector3.one;
                return;
            }

            int multiplier = game.CurrentStreakMultiplier;
            SetText(streakText, $"x{multiplier}  STREAK {streak}");

            float heat = Mathf.Clamp01((multiplier - 1f) / Mathf.Max(1f, game.MaxStreakMultiplier - 1f));
            Color heatColor = Color.Lerp(streakColorCool, streakColorHot, heat);
            if (streakText != null)
            {
                streakText.color = heatColor;
            }

            if (streakFill != null)
            {
                streakFill.fillAmount = game.StreakWindowRemainingNormalized;
                streakFill.color = heatColor;
            }

            // Punch on growth, then spring back.
            if (streak > lastShownStreak)
            {
                streakGroup.transform.localScale = Vector3.one * streakPunchScale;
            }

            lastShownStreak = streak;
            streakGroup.transform.localScale = Vector3.Lerp(
                streakGroup.transform.localScale,
                Vector3.one,
                Time.deltaTime * streakPunchDecay);
        }

        private void UpdateActiveHoop()
        {
            HoopsScoreZone hoop = game.ActiveHoop;
            bool show = game.IsRunning && hoop != null;

            if (activeHoopGroup != null && activeHoopGroup.activeSelf != show)
            {
                activeHoopGroup.SetActive(show);
            }

            if (show)
            {
                int multiplier = Mathf.Max(1, hoop.Points) * Mathf.Max(1, hoop.StageMultiplier);
                string stageTag = hoop.IsWinged
                    ? "  WINGED!"
                    : game.CurrentMovementStage == 2
                        ? "  WILD"
                        : game.CurrentMovementStage == 1
                            ? "  SLIDING"
                            : string.Empty;
                SetText(activeHoopText, $"Target: {hoop.name}  (x{multiplier}){stageTag}");
            }
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

            SetText(resultText,
                $"TIME!\nScore {game.Score}   Best {game.BestRecordedScore}   Tickets +{game.TicketsAwarded}");
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
