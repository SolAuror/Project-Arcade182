using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Prefab-authored HUD for the Labyrinth Crawler. All visuals live in the
    /// LabyrinthCrawlerHud prefab and can be restyled by hand; this component
    /// only pushes current game state into the wired widgets each frame.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Hud")]
    public class LabyrinthHud : MonoBehaviour
    {
        [Serializable]
        public class SpellSlotWidget
        {
            public Text nameText;
            public Text levelText;
            [Tooltip("Rune icon pulled from the spell definition's Icon sprite.")]
            public Image icon;
            [Tooltip("Filled image swept over the slot while the spell cools down.")]
            public Image cooldownOverlay;
            [Tooltip("Enabled while the spell is still locked.")]
            public GameObject lockedOverlay;
        }

        [Header("Game")]
        [Tooltip("Found automatically when left empty.")]
        [SerializeField] private LabyrinthCrawlerGame game;

        [Header("Run Panel")]
        [SerializeField] private Text timerText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text enemiesText;
        [SerializeField] private Text statusText;

        [Tooltip("Top-centre banner naming the current floor.")]
        [SerializeField] private Text floorText;

        [Header("Vitals")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Image manaFill;
        [SerializeField] private Text manaText;

        [Header("Spell Slots")]
        [SerializeField] private List<SpellSlotWidget> spellSlots = new List<SpellSlotWidget>();

        [Header("Exit Dwell")]
        [SerializeField] private GameObject dwellGroup;
        [SerializeField] private Image dwellFill;

        [Header("Run Over")]
        [SerializeField] private GameObject runOverGroup;
        [SerializeField] private Text runOverText;

        [Header("Feedback")]
        [Tooltip("Mana bar tint when a cast fails for lack of mana.")]
        [SerializeField] private Color manaFailFlashColor = new Color(1f, 0.25f, 0.2f, 1f);

        [SerializeField, Min(0.05f)] private float manaFailFlashSeconds = 0.3f;

        [Tooltip("Score text pulse tint when the score increases (kills, stage bonus).")]
        [SerializeField] private Color scoreFlashColor = new Color(0.5f, 1f, 0.6f, 1f);

        [SerializeField, Min(0.05f)] private float scoreFlashSeconds = 0.35f;

        // Cycled by stage so every floor gets a name; the bitmap font maps
        // lowercase to caps, so these render as engraved-style banners.
        private static readonly string[] FloorNames =
        {
            "The Warrens",
            "The Ossuary",
            "The Flooded Halls",
            "The Fungal Deep",
            "The Iron Crypt",
            "The Sunken Chapel",
            "The Hollow Maw"
        };

        private Color manaFillBaseColor;
        private bool manaFillBaseColorCaptured;
        private int lastScoreSeen = -1;
        private float scoreFlashStrength;
        private Color scoreTextBaseColor;
        private bool scoreTextBaseColorCaptured;

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<LabyrinthCrawlerGame>();
            }
        }

        private void Update()
        {
            if (game == null)
            {
                return;
            }

            UpdateRunPanel();
            UpdateVitals();
            UpdateSpellSlots();
            UpdateDwell();
            UpdateRunOver();
        }

        private void UpdateRunPanel()
        {
            float seconds = game.RunSeconds;
            SetText(timerText, $"{(int)(seconds / 60f):0}:{seconds % 60f:00.0}");
            SetText(scoreText, $"Score {game.Score}");
            SetText(enemiesText, $"Foes {game.EnemiesRemaining}   Slain {game.EnemiesKilled}");

            int stage = Mathf.Max(1, game.CurrentStage);
            SetText(floorText, $"Floor {stage} - {FloorNames[(stage - 1) % FloorNames.Length]}");

            UpdateScoreFlash();

            if (statusText != null)
            {
                statusText.text = game.HasFailed
                    ? "The maze claims another."
                    : game.IsChoosingUpgrade
                        ? "The maze offers a boon."
                        : "Seek the waygate. Haste and slaughter are rewarded.";
            }
        }

        // Pulses the score line toward the flash tint whenever the score climbs,
        // reinforcing the world-space "+N" pops. Uses unscaled time so it still
        // animates while the upgrade screen has the game paused.
        private void UpdateScoreFlash()
        {
            if (scoreText == null)
            {
                return;
            }

            if (!scoreTextBaseColorCaptured)
            {
                scoreTextBaseColor = scoreText.color;
                scoreTextBaseColorCaptured = true;
            }

            if (lastScoreSeen >= 0 && game.Score > lastScoreSeen)
            {
                scoreFlashStrength = 1f;
            }

            lastScoreSeen = game.Score;

            if (scoreFlashStrength > 0f)
            {
                scoreFlashStrength = Mathf.Max(0f, scoreFlashStrength - Time.unscaledDeltaTime / scoreFlashSeconds);
            }

            scoreText.color = Color.Lerp(scoreTextBaseColor, scoreFlashColor, scoreFlashStrength);
        }

        private void UpdateVitals()
        {
            Health health = game.PlayerHealth;
            if (healthFill != null)
            {
                healthFill.fillAmount = health != null ? health.Normalized : 0f;
            }

            SetText(healthText, health != null ? $"HP {health.Current:0}/{health.Max:0}" : "HP --");

            Mana mana = game.PlayerMana;
            if (manaFill != null)
            {
                manaFill.fillAmount = mana != null ? mana.Normalized : 0f;

                if (!manaFillBaseColorCaptured)
                {
                    manaFillBaseColor = manaFill.color;
                    manaFillBaseColorCaptured = true;
                }

                // Red flash when a cast just failed for lack of mana.
                float sinceFail = mana != null ? Time.time - mana.LastFailedSpendTime : float.MaxValue;
                float flash = sinceFail <= manaFailFlashSeconds ? 1f - sinceFail / manaFailFlashSeconds : 0f;
                manaFill.color = Color.Lerp(manaFillBaseColor, manaFailFlashColor, flash);
            }

            SetText(manaText, mana != null ? $"MP {mana.Current:0}/{mana.Max:0}" : "MP --");
        }

        private void UpdateSpellSlots()
        {
            SpellCaster caster = game.PlayerCaster;

            for (int i = 0; i < spellSlots.Count; i++)
            {
                SpellSlotWidget widget = spellSlots[i];
                if (widget == null)
                {
                    continue;
                }

                SpellDefinition definition = caster != null ? caster.GetDefinition(i) : null;
                SpellCaster.SlotState state = caster != null ? caster.GetState(i) : null;

                SetText(widget.nameText, definition != null ? definition.DisplayName : "-");
                SetText(widget.levelText, state != null ? $"Lv{state.Level}" : string.Empty);

                bool locked = state == null || !state.Unlocked;

                if (widget.icon != null)
                {
                    Sprite iconSprite = definition != null ? definition.Icon : null;
                    if (widget.icon.sprite != iconSprite)
                    {
                        widget.icon.sprite = iconSprite;
                    }

                    bool showIcon = iconSprite != null;
                    if (widget.icon.enabled != showIcon)
                    {
                        widget.icon.enabled = showIcon;
                    }
                }

                if (widget.lockedOverlay != null && widget.lockedOverlay.activeSelf != locked)
                {
                    widget.lockedOverlay.SetActive(locked);
                }

                if (widget.cooldownOverlay != null)
                {
                    widget.cooldownOverlay.fillAmount = !locked && caster != null
                        ? caster.GetCooldownNormalized(i)
                        : 0f;
                }
            }
        }

        private void UpdateDwell()
        {
            LabyrinthExitPad pad = game.ExitPad;
            bool show = pad != null && pad.PlayerInside && game.IsRunning && game.EnemiesRemaining > 0;

            if (dwellGroup != null && dwellGroup.activeSelf != show)
            {
                dwellGroup.SetActive(show);
            }

            if (show && dwellFill != null)
            {
                dwellFill.fillAmount = pad.DwellProgress;
            }
        }

        private void UpdateRunOver()
        {
            bool show = game.HasFailed;
            if (runOverGroup != null && runOverGroup.activeSelf != show)
            {
                runOverGroup.SetActive(show);
            }

            if (show)
            {
                // The "Thou Hast Fallen" title is a static display-font Text
                // authored in the prefab; this line only carries the numbers.
                SetText(runOverText,
                    $"Score {game.Score}   Best {game.BestRecordedScore}   Tickets +{game.TicketsAwarded}");
            }
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
