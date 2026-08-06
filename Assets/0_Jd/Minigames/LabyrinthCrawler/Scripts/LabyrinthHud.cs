using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("timerText")]
        [SerializeField] private Text secretsText;
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

        [Header("Title Animation")]
        [SerializeField, Min(0f)] private float titleFadeSeconds = 0.45f;
        [SerializeField, Min(0f)] private float titleWaveAmplitude = 0.65f;
        [SerializeField, Min(0f)] private float titleWaveSpeed = 2f;
        [SerializeField, Min(0f)] private float titleWaveFrequency = 0.12f;

        [Header("Opening Message")]
        [SerializeField, Min(0f)] private float openingMessageHoldSeconds = 2.5f;
        [SerializeField, Min(0.05f)] private float openingMessageFadeSeconds = 1f;
        [SerializeField, Min(0f)] private float emphasisHoldSeconds = 1f;
        [SerializeField, Min(0.05f)] private float emphasisFadeSeconds = 0.75f;
        [SerializeField] private Color emphasisRed = new Color(0.85f, 0.12f, 0.08f, 1f);

        [Header("Secrets Feedback")]
        [SerializeField] private Color secretFlashColor = new Color(0.75f, 0.95f, 1f, 1f);
        [SerializeField, Min(0.05f)] private float secretFlashSeconds = 0.5f;

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
            "The Hollow Maw",
            "The Bleeding Vault",
            "The Ashen Cloister",
            "The Gilded Tomb",
            "The Whispering Cistern",
            "The Broken Reliquary",
            "The Ember Catacombs",
            "The Starless Court",
            "The Shrouded Archive",
            "The Blackened Spire",
            "The Nameless Sepulchre",
            "The Drowned Gallery",
            "The Sable Foundry"
        };

        private Color manaFillBaseColor;
        private bool manaFillBaseColorCaptured;
        private int lastScoreSeen = -1;
        private float scoreFlashStrength;
        private Color scoreTextBaseColor;
        private bool scoreTextBaseColorCaptured;
        private int lastFloorSeen = -1;
        private float floorIntroStartedAt;
        private Vector3 floorTextBaseScale;
        private Color floorTextBaseColor;
        private bool floorTextBaseStateCaptured;
        private Color statusTextBaseColor;
        private bool statusTextBaseColorCaptured;
        private float openingMessageStartedAt;
        private int lastStageSecretsSeen = -1;
        private float secretFlashStrength;
        private Vector3 secretsTextBaseScale;
        private Color secretsTextBaseColor;
        private bool secretsTextBaseStateCaptured;
        private bool wasChoosingUpgrade;
        private bool wasFailed;

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<LabyrinthCrawlerGame>();
            }

            ConfigureFloorWave();
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
            if (game.IsChoosingUpgrade)
            {
                wasChoosingUpgrade = true;
            }
            else if (wasChoosingUpgrade)
            {
                wasChoosingUpgrade = false;
                ResetStageIntro();
            }

            if (game.HasFailed)
            {
                wasFailed = true;
            }
            else if (wasFailed)
            {
                wasFailed = false;
                ResetStageIntro();
            }

            SetText(secretsText, $"Secrets {game.StageSecretsFound}/{game.StageSecretsAvailable}");
            SetText(scoreText, $"Score {game.Score}");
            SetText(enemiesText, $"Foes {game.EnemiesRemaining}   Slain {game.EnemiesKilled}");

            int stage = Mathf.Max(1, game.CurrentStage);
            SetText(floorText, $"Floor {stage} - {FloorNames[(stage - 1) % FloorNames.Length]}");

            UpdateFloorTitle(stage);
            UpdateSecretsFlash();
            UpdateScoreFlash();
            UpdateStatusMessage();
        }

        private void ResetStageIntro()
        {
            lastFloorSeen = -1;
            lastStageSecretsSeen = -1;
            secretFlashStrength = 0f;
        }

        private void ConfigureFloorWave()
        {
            if (floorText == null)
            {
                return;
            }

            LabyrinthTextWave wave = floorText.GetComponent<LabyrinthTextWave>();
            if (wave == null)
            {
                Debug.LogError(
                    $"{name} requires an authored {nameof(LabyrinthTextWave)} on '{floorText.name}'.",
                    floorText);
                return;
            }

            wave.Configure(titleWaveAmplitude, titleWaveFrequency, titleWaveSpeed);
        }

        private void UpdateFloorTitle(int stage)
        {
            if (floorText == null)
            {
                return;
            }

            if (!floorTextBaseStateCaptured)
            {
                floorTextBaseScale = floorText.rectTransform.localScale;
                floorTextBaseColor = floorText.color;
                floorTextBaseStateCaptured = true;
            }

            if (stage != lastFloorSeen)
            {
                lastFloorSeen = stage;
                floorIntroStartedAt = Time.unscaledTime;
                openingMessageStartedAt = Time.unscaledTime;
            }

            float elapsed = Time.unscaledTime - floorIntroStartedAt;
            float reveal = titleFadeSeconds > 0f
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / titleFadeSeconds))
                : 1f;
            float breathe = 1f + Mathf.Sin(Time.unscaledTime * 1.35f) * 0.012f;
            floorText.rectTransform.localScale =
                Vector3.Scale(floorTextBaseScale, Vector3.one * Mathf.Lerp(0.92f, breathe, reveal));

            Color color = floorTextBaseColor;
            color.a *= reveal;
            floorText.color = color;
        }

        private void UpdateSecretsFlash()
        {
            if (secretsText == null)
            {
                return;
            }

            if (!secretsTextBaseStateCaptured)
            {
                secretsTextBaseScale = secretsText.rectTransform.localScale;
                secretsTextBaseColor = secretsText.color;
                secretsTextBaseStateCaptured = true;
            }

            int found = game.StageSecretsFound;
            if (lastStageSecretsSeen >= 0 && found > lastStageSecretsSeen)
            {
                secretFlashStrength = 1f;
            }

            lastStageSecretsSeen = found;
            if (secretFlashStrength > 0f)
            {
                secretFlashStrength =
                    Mathf.Max(0f, secretFlashStrength - Time.unscaledDeltaTime / secretFlashSeconds);
            }

            secretsText.color = Color.Lerp(secretsTextBaseColor, secretFlashColor, secretFlashStrength);
            secretsText.rectTransform.localScale =
                Vector3.Scale(secretsTextBaseScale, Vector3.one * (1f + secretFlashStrength * 0.12f));
        }

        private void UpdateStatusMessage()
        {
            if (statusText == null)
            {
                return;
            }

            if (!statusTextBaseColorCaptured)
            {
                statusTextBaseColor = statusText.color;
                statusTextBaseColorCaptured = true;
            }

            if (game.HasFailed)
            {
                SetStatusText("The maze claims another.", statusTextBaseColor);
                return;
            }

            if (game.IsChoosingUpgrade)
            {
                SetStatusText("The maze offers a boon.", statusTextBaseColor);
                return;
            }

            float elapsed = Time.unscaledTime - openingMessageStartedAt;
            if (elapsed <= openingMessageHoldSeconds)
            {
                SetStatusText(
                    "Seek the waygate. Slaughter and haste are rewarded.",
                    statusTextBaseColor);
                return;
            }

            elapsed -= openingMessageHoldSeconds;
            if (elapsed <= openingMessageFadeSeconds)
            {
                float t = Mathf.Clamp01(elapsed / openingMessageFadeSeconds);
                Color emphasisColor = Color.Lerp(statusTextBaseColor, emphasisRed, t);
                string surroundHex = ColorUtility.ToHtmlStringRGB(statusTextBaseColor);
                string emphasisHex = ColorUtility.ToHtmlStringRGB(emphasisColor);
                string alphaHex = Mathf.RoundToInt((1f - t) * 255f).ToString("X2");

                statusText.color = Color.white;
                SetText(
                    statusText,
                    $"<color=#{surroundHex}{alphaHex}>Seek the waygate. </color>" +
                    $"<color=#{emphasisHex}FF>Slaughter and haste</color>" +
                    $"<color=#{surroundHex}{alphaHex}> are rewarded.</color>");
                return;
            }

            elapsed -= openingMessageFadeSeconds;
            if (elapsed <= emphasisHoldSeconds)
            {
                SetStatusText("Slaughter and haste", emphasisRed);
                return;
            }

            elapsed -= emphasisHoldSeconds;
            if (elapsed <= emphasisFadeSeconds)
            {
                Color fadingRed = emphasisRed;
                fadingRed.a *= 1f - Mathf.Clamp01(elapsed / emphasisFadeSeconds);
                SetStatusText("Slaughter and haste", fadingRed);
                return;
            }

            SetStatusText(string.Empty, statusTextBaseColor);
        }

        private void SetStatusText(string value, Color color)
        {
            statusText.color = color;
            SetText(statusText, value);
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
