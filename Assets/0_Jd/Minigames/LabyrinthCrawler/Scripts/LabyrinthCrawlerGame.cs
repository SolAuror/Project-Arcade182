using System;
using System.Collections.Generic;
using Sol;
using Sol.Arcade;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Minigames
{
    /// <summary>
    /// First-person spellcasting roguelike crawler: a stopwatch-timed run through
    /// regenerating mazes. Reach the exit pad to clear a stage (instant when all
    /// enemies are dead, dwell otherwise), pick 1-of-3 upgrades between stages,
    /// die and the run ends — score persists and the player returns to the hub.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler Game")]
    public class LabyrinthCrawlerGame : MonoBehaviour
    {
        #region Inspector Configuration

        [Header("Run Timer")]
        [Tooltip("Shared stopwatch for the run. Auto-added when missing.")]
        [SerializeField] private MinigameTimer runTimer;

        [SerializeField] private bool startOnAwake = true;

        [Header("Maze Rules")]
        [SerializeField] private ArcadeGen3D mazeGenerator;
        [SerializeField] private LabyrinthMazeRules labyrinthMazeRules = new LabyrinthMazeRules();

        [Header("Combat Setup")]
        [Tooltip("Enemy prefabs spawned each stage (alternated). Need EnemyController.")]
        [SerializeField] private List<GameObject> enemyPrefabs = new List<GameObject>();

        [Tooltip("Player loadout applied when the player's SpellCaster has no slots: Attack, Cast, Pulse order.")]
        [SerializeField] private List<SpellDefinition> playerSpells = new List<SpellDefinition>();

        [Tooltip("How many of the player spells start unlocked (progressive unlock).")]
        [SerializeField, Min(0)] private int playerSpellsUnlockedAtStart = 1;

        [Tooltip("Minimum room distance (manhattan) from the start room before an enemy may spawn, so packs never open on top of the player.")]
        [SerializeField, Min(0)] private int minEnemySpawnRoomDistance = 2;

        [Header("Exit")]
        [Tooltip("Seconds standing on the exit pad while enemies are alive.")]
        [SerializeField, Min(0f)] private float clearDwellSeconds = 1.5f;

        [Tooltip("Authored exit pad spawned when the end room does not already contain one (extracted from the DungeonExit room).")]
        [SerializeField] private LabyrinthExitPad exitPadPrefab;

        [Tooltip("Vertical portal beacon instanced onto the active exit pad each stage. Reveals on room clear (blue) or the Cartographer timer (red while enemies live).")]
        [SerializeField] private LabyrinthExitBeacon exitBeaconPrefab;

        [Header("Score")]
        [Tooltip("Points per second under par when clearing a stage.")]
        [SerializeField, Min(0f)] private float timeBonusPerSecond = 10f;

        [Tooltip("Par seconds for the starting maze size.")]
        [SerializeField, Min(0f)] private float parBaseSeconds = 20f;

        [Tooltip("Par seconds added per grid cell beyond the starting bounds. Organic-mask variation does not change par; a growth skip freezes both the bounds and par.")]
        [SerializeField, Min(0f)] private float parPerRoomSeconds = 0.8f;

        [Tooltip("Base points per kill, multiplied by the current stage score multiplier. Awarded live so kills always score.")]
        [SerializeField, Min(0)] private int pointsPerKill = 25;

        [SerializeField] private string minigameId = "LabyrinthCrawler";
        [SerializeField] private float ticketsPerPoint = 0.1f;
        [SerializeField] private PlayerScoreCarrier scoreCarrier;
        [SerializeField] private string legacyLastScorePlayerPrefsKey = "TimedMazeEscape.LastScore";
        [SerializeField] private string legacyBestScorePlayerPrefsKey = "TimedMazeEscape.BestScore";

        [Header("Audio")]
        [Tooltip("2D source for run feedback. Auto-added when missing; assign clips to enable each cue.")]
        [SerializeField] private AudioSource feedbackAudioSource;

        [SerializeField] private AudioClip playerHurtClip;
        [SerializeField] private AudioClip enemyKillClip;

        [Tooltip("Dry-fire cue when a cast fails for lack of mana.")]
        [SerializeField] private AudioClip castFailClip;

        [SerializeField] private AudioClip stageClearClip;
        [SerializeField] private AudioClip upgradePickedClip;
        [SerializeField] private AudioClip runOverClip;

        [Tooltip("Cue when a Second Wind charge saves the player from a lethal blow.")]
        [SerializeField] private AudioClip reviveClip;

        [Header("Fall Safety")]
        [Tooltip("Players falling below this world Y are caught and returned to the stage start room. With deep pits, set this just BELOW the pit floor (VoidDeep sits at ~-9) so the player falls the whole way and feels the drop before the reset. VoidDeep is a particle effect, not a collider, so the fall is a clean free-fall.")]
        [SerializeField] private float fallRespawnY = -12f;

        [Tooltip("Damage each pit fall deals, as a fraction of CURRENT health, escalating per fall across the run to punish repeated failure (the last value repeats once the list runs out). 0.99 nearly empties the bar without a guaranteed kill.")]
        [SerializeField] private float[] pitFallDamageFractions = { 0.25f, 0.25f, 0.5f, 0.99f };

        [Tooltip("Rare chance, on each pit fall, that the fall instead drops the player straight to the next floor - no upgrade pick, no damage. Engineered luck: a lucky escape from a bad fall. 0 disables.")]
        [SerializeField, Range(0f, 1f)] private float pitFallSkipFloorChance = 0.03f;

        [Header("Upgrades")]
        [SerializeField] private LabyrinthUpgradeSystem upgradeSystem = new LabyrinthUpgradeSystem();

        [Header("Secrets")]
        [Tooltip("Post-carve pass hiding dead-end rooms behind illusory walls. Labyrinth-only; the hub maze never runs this.")]
        [SerializeField] private LabyrinthSecretPass secretPass = new LabyrinthSecretPass();

        [Tooltip("Base points for uncovering a secret room, multiplied by the stage score multiplier.")]
        [SerializeField, Min(0)] private int pointsPerSecret = 100;

        [Tooltip("Base points for uncovering a shortcut, multiplied by the stage multiplier. Lower than a room: a shortcut already pays for itself in saved time. Exit-path blockers pay nothing at all.")]
        [SerializeField, Min(0)] private int pointsPerShortcut = 50;

        [Header("Secret Cache Rewards")]
        [Tooltip("Relative chance a cache holds a Shrine (full heal + mana).")]
        [SerializeField, Min(0f)] private float shrineRewardWeight = 1f;

        [Tooltip("Relative chance a cache holds a Hoard (large score bounty).")]
        [SerializeField, Min(0f)] private float hoardRewardWeight = 0.8f;

        [Tooltip("Relative chance a cache holds a Boon (a genuine extra upgrade pick). Keep rare: this is the only cache reward that adds power.")]
        [SerializeField, Min(0f)] private float boonRewardWeight = 0.35f;

        [Tooltip("Base points for a Hoard cache, multiplied by the stage score multiplier.")]
        [SerializeField, Min(0)] private int hoardPoints = 500;

        [Tooltip("At or above this fill fraction of BOTH pools, a Shrine pays forward as permanent max instead of a wasted restore.")]
        [SerializeField, Range(0.1f, 1f)] private float shrineFullThreshold = 0.9f;

        [Tooltip("Permanent max health granted when a Shrine is found at full strength.")]
        [SerializeField, Min(0f)] private float shrineOverflowMaxHealth = 10f;

        [Tooltip("Permanent max mana granted when a Shrine is found at full strength.")]
        [SerializeField, Min(0f)] private float shrineOverflowMaxMana = 10f;

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeHub";
        [SerializeField] private float returnDelaySeconds = 2f;

        [Tooltip("Legacy timed auto-return after death. Off by default now that the run-over screen offers Restart/Quit.")]
        [SerializeField] private bool autoReturnAfterFail;

        #endregion

        #region Runtime State

        private readonly List<EnemyController> enemies = new List<EnemyController>();

        private LabyrinthUpgradeScreen upgradeScreen;
        private LabyrinthExitPad currentExitPad;
        private Transform enemiesParent;
        private Transform secretsParent;
        private int secretsFound;
        private int stageSecretsFound;
        private int pendingBonusPicks;
        private int pitFallCount;
        private Health playerHealth;
        private Mana playerMana;
        private SpellCaster playerCaster;
        private Player.Controller playerController;
        private float finishTime;
        private float stageStartElapsed;
        private int currentMazeWidth;
        private int currentMazeDepth;
        private int exitsFound;
        private int enemiesKilled;
        private int score;
        private int ticketsAwarded;
        private int totalTickets;
        private int lastRecordedScore;
        private int bestRecordedScore;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;
        private bool isChoosingUpgrade;
        private bool scoreRecorded;
        private float lastSeenManaFailTime = -999f;

        // Bright green so score gains read differently from the gold damage numbers.
        private static readonly Color ScorePopColor = new Color(0.45f, 1f, 0.55f, 1f);

        // Warm gold-white for the Second Wind save, distinct from score green.
        private static readonly Color RevivePopColor = new Color(1f, 0.85f, 0.4f, 1f);

        // Cool teal for shrine restores, violet for the rare extra-pick boon.
        private static readonly Color ShrinePopColor = new Color(0.5f, 0.95f, 0.9f, 1f);
        private static readonly Color BoonPopColor = new Color(0.8f, 0.6f, 1f, 1f);

        // Harsh red for pit-fall damage; bright violet-white for the lucky floor skip.
        private static readonly Color PitFallPopColor = new Color(1f, 0.35f, 0.3f, 1f);
        private static readonly Color LuckyFallPopColor = new Color(0.85f, 0.7f, 1f, 1f);

        private enum SecretRewardKind
        {
            Shrine,
            Hoard,
            Boon
        }

        #endregion

        #region Public State

        public float RunSeconds => runTimer != null ? runTimer.Elapsed : 0f;
        public float StageElapsedSeconds => Mathf.Max(0f, RunSeconds - stageStartElapsed);

        /// <summary>Seconds into a stage after which the exit beacon reveals despite live enemies (Cartographer); infinite otherwise.</summary>
        public float ExitRevealAfterSeconds => upgradeSystem.ExitRevealAfterSeconds;
        public int CurrentMazeWidth => currentMazeWidth;
        public int CurrentMazeDepth => currentMazeDepth;
        public int CurrentStage => exitsFound + 1;
        public int CurrentStageMultiplier => labyrinthMazeRules.GetScoreMultiplier(CurrentStage);
        public int CurrentEnemyCount => labyrinthMazeRules.GetEnemyCount(CurrentStage);
        public int ExitsFound => exitsFound;
        public int EnemiesKilled => enemiesKilled;
        public int SecretsFound => secretsFound;
        public int StageSecretsFound => stageSecretsFound;
        public int StageSecretsAvailable => secretPass.TrackableSecretCount;
        public int Score => score;
        public int LastRecordedScore => lastRecordedScore;
        public int BestRecordedScore => bestRecordedScore;
        public bool IsRunning => isRunning;
        public bool IsComplete => isComplete;
        public bool HasFailed => hasFailed;
        public bool IsChoosingUpgrade => isChoosingUpgrade;
        public bool CanPlayerAct => isRunning && !isChoosingUpgrade && !isComplete;
        public ArcadeGen3D Maze => mazeGenerator;
        public Health PlayerHealth => playerHealth;
        public Mana PlayerMana => playerMana;
        public SpellCaster PlayerCaster => playerCaster;
        public LabyrinthExitPad ExitPad => currentExitPad;
        public int TicketsAwarded => ticketsAwarded;
        public int TotalTickets => totalTickets;

        public int EnemiesRemaining
        {
            get
            {
                int alive = 0;
                foreach (EnemyController enemy in enemies)
                {
                    if (enemy != null)
                    {
                        alive++;
                    }
                }

                return alive;
            }
        }

        #endregion

        #region Lifecycle and Fall Safety

        private void Awake()
        {
            if (mazeGenerator == null)
            {
                mazeGenerator = FindFirstObjectByType<ArcadeGen3D>();
            }

            if (runTimer == null && !TryGetComponent(out runTimer))
            {
                runTimer = gameObject.AddComponent<MinigameTimer>();
            }

            runTimer.Mode = MinigameTimer.TimerMode.Stopwatch;

            if (feedbackAudioSource == null && !TryGetComponent(out feedbackAudioSource))
            {
                feedbackAudioSource = gameObject.AddComponent<AudioSource>();
            }

            feedbackAudioSource.playOnAwake = false;
            feedbackAudioSource.spatialBlend = 0f; // 2D run feedback

            // Authored in the LabyrinthCrawlerHud prefab; its panel starts inactive.
            upgradeScreen = FindFirstObjectByType<LabyrinthUpgradeScreen>(FindObjectsInactive.Include);
            if (upgradeScreen == null)
            {
                Debug.LogWarning($"{name} found no LabyrinthUpgradeScreen in the scene; stage rewards will be skipped.", this);
            }

            ResolveScoreCarrier();
            PlayerScoreCarrier.ScoreRecord scoreRecord = ReadScoreRecord();
            lastRecordedScore = scoreRecord.LastScore;
            bestRecordedScore = scoreRecord.BestScore;
            totalTickets = scoreRecord.TotalTickets;

            if (startOnAwake)
            {
                StartGame();
            }
        }

        private void Update()
        {
            if (!isRunning && !isChoosingUpgrade)
            {
                TickReturnDelay();
                return;
            }

            if (isRunning && !isChoosingUpgrade)
            {
                RespawnPlayerIfFallenOut();
                TickCastFailAudio();
            }
        }

        private void TickCastFailAudio()
        {
            if (playerMana == null || playerMana.LastFailedSpendTime <= lastSeenManaFailTime)
            {
                return;
            }

            lastSeenManaFailTime = playerMana.LastFailedSpendTime;
            PlayClip(castFailClip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (feedbackAudioSource != null && clip != null)
            {
                feedbackAudioSource.PlayOneShot(clip);
            }
        }

        private void OnPlayerDamaged(float amount)
        {
            PlayClip(playerHurtClip);
        }

        private void RespawnPlayerIfFallenOut()
        {
            Transform player = playerHealth != null ? playerHealth.transform : null;
            if (player == null || mazeGenerator == null || player.position.y >= fallRespawnY)
            {
                return;
            }

            HandlePitFall(player);
        }

        // The player has fallen the full depth of a pit. Get them out first so the
        // fall cannot re-fire next frame (especially before the async lucky rebuild),
        // then resolve the consequence: a rare lucky drop to the next floor,
        // otherwise escalating fall damage.
        private void HandlePitFall(Transform player)
        {
            pitFallCount++;
            Vector3 popAt = player.position + Vector3.up * 1.5f;

            if (pitFallSkipFloorChance > 0f && UnityEngine.Random.value < pitFallSkipFloorChance)
            {
                mazeGenerator.RespawnPlayerAtStartRoom();
                Debug.Log("Lucky pit fall: dropping to the next floor without an upgrade.", this);
                DamagePopup.SpawnText(popAt, "Lucky fall!", LuckyFallPopColor, 0f, 1.4f);
                AdvanceFloorWithoutUpgrade();
                return;
            }

            ApplyPitFallDamage(popAt);
            mazeGenerator.RespawnPlayerAtStartRoom();
        }

        // Escalating pit-fall damage as a fraction of CURRENT health, so the 0.99
        // step nearly empties the bar but leaves a sliver rather than a guaranteed
        // kill. The list index tracks total falls this run (last value repeats).
        private void ApplyPitFallDamage(Vector3 popAt)
        {
            if (playerHealth == null || pitFallDamageFractions == null || pitFallDamageFractions.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(pitFallCount - 1, 0, pitFallDamageFractions.Length - 1);
            float fraction = Mathf.Clamp01(pitFallDamageFractions[index]);
            float damage = playerHealth.Current * fraction;
            if (damage <= 0f)
            {
                return;
            }

            // Neutral source so the player's own faction never filters out the hit.
            playerHealth.TakeDamage(damage, Faction.Neutral);
            DamagePopup.SpawnText(popAt, $"-{Mathf.RoundToInt(fraction * 100f)}%", PitFallPopColor, 0f, 1.2f);
        }

        // Rare lucky-fall reward: advance a floor like a clear, but skip the upgrade
        // draft and the time bonus. Mirrors the growth (and Stasis) handling of the
        // normal stage advance so difficulty scaling stays consistent.
        private void AdvanceFloorWithoutUpgrade()
        {
            exitsFound++;
            PlayClip(stageClearClip);

            if (upgradeSystem.ConsumeMazeGrowthSkip())
            {
                Debug.Log("Stasis Sigil held: the lucky floor keeps its current size.", this);
            }
            else
            {
                currentMazeWidth += labyrinthMazeRules.MazeGrowthPerStage;
                currentMazeDepth += labyrinthMazeRules.MazeGrowthPerStage;
            }

            RebuildMaze();
        }

        private void OnValidate()
        {
            labyrinthMazeRules ??= new LabyrinthMazeRules();
            labyrinthMazeRules.OnValidate();
            ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;

            if (playerHealth != null)
            {
                playerHealth.OnDied.RemoveListener(OnPlayerDied);
                playerHealth.OnDamaged.RemoveListener(OnPlayerDamaged);
            }
        }

        #endregion

        #region Run and Stage Flow

        public void StartGame()
        {
            finishTime = 0f;
            currentMazeWidth = labyrinthMazeRules.StartingMazeWidth;
            currentMazeDepth = labyrinthMazeRules.StartingMazeDepth;
            exitsFound = 0;
            enemiesKilled = 0;
            secretsFound = 0;
            stageSecretsFound = 0;
            pendingBonusPicks = 0;
            pitFallCount = 0;
            score = 0;
            ticketsAwarded = 0;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            isChoosingUpgrade = false;
            scoreRecorded = false;
            Time.timeScale = 1f;

            EnsurePlayerCombat();
            upgradeSystem.Bind(playerCaster, playerHealth, playerMana, playerController);

            runTimer.Begin();
            stageStartElapsed = 0f;
            RebuildMaze();
        }

        /// <summary>
        /// Compatibility entry point used by the older clerk-style maze exit.
        /// New crawler stages use <see cref="LabyrinthExitPad"/> and reach the
        /// same stage transition through this method.
        /// </summary>
        public void CompleteEscape()
        {
            ReachExit();
        }

        public void ReachExit()
        {
            if (!isRunning || isComplete || hasFailed || isChoosingUpgrade)
            {
                return;
            }

            exitsFound++;
            PlayClip(stageClearClip);

            float stageClearSeconds = runTimer.Elapsed - stageStartElapsed;
            // Par is anchored to configured grid bounds, not the stage number,
            // so a Stasis Sigil freezes par with the maze instead of banking an
            // ever-growing time bonus on the same-sized floor.
            int startingArea = labyrinthMazeRules.StartingMazeWidth * labyrinthMazeRules.StartingMazeDepth;
            int extraRooms = Mathf.Max(0, currentMazeWidth * currentMazeDepth - startingArea);
            float parSeconds = parBaseSeconds + parPerRoomSeconds * extraRooms;
            int stageTimeBonus = Mathf.Max(0, Mathf.RoundToInt((parSeconds - stageClearSeconds) * timeBonusPerSecond));
            score += stageTimeBonus;

            Debug.Log($"Stage {exitsFound} clear in {stageClearSeconds:0.0}s (par {parSeconds:0.0}s). +{stageTimeBonus} points, total {score}.", this);

            BeginUpgradeChoice();
        }

        #endregion

        #region Combat and Secret Rewards

        public void NotifyEnemyDied(EnemyController enemy)
        {
            Vector3 killPosition = enemy != null ? enemy.transform.position : Vector3.zero;
            enemies.Remove(enemy);
            enemiesKilled++;

            // Kills score live (scaled by stage) so combat always pays off, even
            // on a run that ends before the first exit.
            int killScore = pointsPerKill * CurrentStageMultiplier;
            score += killScore;

            if (upgradeSystem.LifeOnKillHeal > 0f && playerHealth != null)
            {
                playerHealth.Heal(upgradeSystem.LifeOnKillHeal);
            }

            if (upgradeSystem.ManaOnKillRestore > 0f && playerMana != null)
            {
                playerMana.Restore(upgradeSystem.ManaOnKillRestore);
            }

            PlayClip(enemyKillClip);
            if (enemy != null && killScore > 0)
            {
                DamagePopup.SpawnText(killPosition + Vector3.up * 1.5f, $"+{killScore}", ScorePopColor, 0f, 1.2f);
            }
        }

        private void OnSecretRevealed(IllusoryWall wall, LabyrinthSecretPass.SecretSiteKind kind)
        {
            // A blocker sat in the way to the exit; pushing through it is not a
            // discovery, so it pays nothing and does not count as a secret.
            if (kind == LabyrinthSecretPass.SecretSiteKind.ExitPathBlocker)
            {
                return;
            }

            secretsFound++;
            stageSecretsFound++;

            // The wall handles its own reveal juice (jingle + "Secret!" pop);
            // the game's contribution is the score, scaled like kills are. The
            // room's real prize is the cache waiting inside it.
            int basePoints = kind == LabyrinthSecretPass.SecretSiteKind.Room ? pointsPerSecret : pointsPerShortcut;
            int gain = basePoints * CurrentStageMultiplier;
            score += gain;

            if (wall != null && gain > 0)
            {
                DamagePopup.SpawnText(wall.transform.position + Vector3.up * 0.5f, $"+{gain}", ScorePopColor, 0f, 1.2f);
            }
        }

        /// <summary>
        /// Rolls and applies the contents of a secret-room cache. Only the Boon
        /// outcome adds power, and it is the rarest, so a searching player is
        /// paid mostly in sustain and score rather than a doubled upgrade curve.
        /// </summary>
        private void OnSecretCacheCollected(LabyrinthSecretCache cache)
        {
            Vector3 position = (cache != null ? cache.transform.position : Vector3.zero) + Vector3.up * 0.6f;

            switch (RollSecretReward())
            {
                case SecretRewardKind.Hoard:
                    GrantHoard(position);
                    break;
                case SecretRewardKind.Boon:
                    GrantBoon(position);
                    break;
                default:
                    GrantShrine(position);
                    break;
            }
        }

        private SecretRewardKind RollSecretReward()
        {
            float shrineWeight = Mathf.Max(0f, shrineRewardWeight);
            float hoardWeight = Mathf.Max(0f, hoardRewardWeight);
            float boonWeight = Mathf.Max(0f, boonRewardWeight);
            float total = shrineWeight + hoardWeight + boonWeight;
            if (total <= 0f)
            {
                return SecretRewardKind.Shrine;
            }

            float roll = UnityEngine.Random.value * total;
            if (roll < shrineWeight)
            {
                return SecretRewardKind.Shrine;
            }

            return roll < shrineWeight + hoardWeight ? SecretRewardKind.Hoard : SecretRewardKind.Boon;
        }

        private void GrantShrine(Vector3 position)
        {
            bool healthFull = playerHealth == null || playerHealth.Normalized >= shrineFullThreshold;
            bool manaFull = playerMana == null || playerMana.Normalized >= shrineFullThreshold;

            // A restore found at full strength would be a dud, which teaches
            // players to stop detouring; pay it forward as permanent max.
            if (healthFull && manaFull)
            {
                if (playerHealth != null)
                {
                    playerHealth.IncreaseMax(shrineOverflowMaxHealth);
                }

                if (playerMana != null)
                {
                    playerMana.IncreaseMax(shrineOverflowMaxMana);
                }

                DamagePopup.SpawnText(position, "Shrine: Attuned!", ShrinePopColor, 0f, 1.4f);
                return;
            }

            if (playerHealth != null)
            {
                playerHealth.Heal(playerHealth.Max);
            }

            if (playerMana != null)
            {
                playerMana.ResetToMax();
            }

            DamagePopup.SpawnText(position, "Shrine: Restored!", ShrinePopColor, 0f, 1.4f);
        }

        private void GrantHoard(Vector3 position)
        {
            int gain = hoardPoints * CurrentStageMultiplier;
            score += gain;
            DamagePopup.SpawnText(position, $"Hoard! +{gain}", ScorePopColor, 0f, 1.4f);
        }

        private void GrantBoon(Vector3 position)
        {
            pendingBonusPicks++;
            DamagePopup.SpawnText(position, "Boon: Extra pick!", BoonPopColor, 0f, 1.4f);
        }

        #endregion

        #region Upgrade Draft

        private void BeginUpgradeChoice()
        {
            if (upgradeScreen == null)
            {
                OnUpgradePicked(null);
                return;
            }

            isChoosingUpgrade = true;
            runTimer.Pause();
            Time.timeScale = 0f;
            upgradeScreen.Show(upgradeSystem.BuildChoices(), OnUpgradePicked);
        }

        private void OnUpgradePicked(LabyrinthUpgrade upgrade)
        {
            upgradeSystem.Apply(upgrade);
            if (upgrade != null)
            {
                PlayClip(upgradePickedClip);
            }

            // Boon caches bank extra picks; spend them here, while the stage is
            // already paused, rather than opening a draft mid-run (which would
            // freeze every enemy and hand the player a panic button).
            if (pendingBonusPicks > 0 && upgradeScreen != null)
            {
                pendingBonusPicks--;
                upgradeScreen.Show(upgradeSystem.BuildChoices(), OnUpgradePicked);
                return;
            }

            isChoosingUpgrade = false;
            Time.timeScale = 1f;
            runTimer.Resume();

            if (upgradeSystem.ConsumeMazeGrowthSkip())
            {
                Debug.Log("Stasis Sigil held: the next maze keeps its current size.", this);
            }
            else
            {
                currentMazeWidth += labyrinthMazeRules.MazeGrowthPerStage;
                currentMazeDepth += labyrinthMazeRules.MazeGrowthPerStage;
            }

            RebuildMaze();
        }

        #endregion

        #region Death and Scene Flow

        private void OnPlayerDied()
        {
            if (!isRunning || isComplete)
            {
                return;
            }

            // Second Wind intercepts the killing blow: spend a charge, stand the
            // player back up mid-run, and let the death event fall through.
            if (upgradeSystem.TryConsumeReviveCharge() && playerHealth != null)
            {
                playerHealth.Revive(playerHealth.Max * upgradeSystem.ReviveHealthFraction);
                playerMana?.ResetToMax();
                PlayClip(reviveClip);
                DamagePopup.SpawnText(playerHealth.transform.position + Vector3.up * 1.5f, "Second Wind!", RevivePopColor, 0f, 1.4f);
                Debug.Log("Second Wind spent: the run continues.", this);
                return;
            }

            isRunning = false;
            isComplete = true;
            hasFailed = true;
            isChoosingUpgrade = false;
            Time.timeScale = 1f;
            runTimer.Pause();
            finishTime = Time.unscaledTime;
            PlayClip(runOverClip);

            RecordScore();
            Debug.Log($"Run over. Stages {exitsFound}, kills {enemiesKilled}. Final score: {score}.", this);
        }

        // The run-over screen owns the exit decision after a death; the timed
        // return only remains for the legacy auto-return opt-in.
        public void RestartRun()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        public void QuitToHub()
        {
            if (string.IsNullOrWhiteSpace(returnSceneName) || !Application.CanStreamedLevelBeLoaded(returnSceneName))
            {
                Debug.LogWarning($"{name} cannot return to '{returnSceneName}'. Add the scene to Build Settings or update Return Scene Name.", this);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
        }

        private void TickReturnDelay()
        {
            if (!returnToSceneOnFinish || (!isComplete && !hasFailed))
            {
                return;
            }

            if (hasFailed && !autoReturnAfterFail)
            {
                return;
            }

            if (Time.unscaledTime - finishTime < returnDelaySeconds)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(returnSceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
            {
                Debug.LogWarning($"{name} cannot return to '{returnSceneName}'. Add the scene to Build Settings or update Return Scene Name.", this);
                returnToSceneOnFinish = false;
                return;
            }

            SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
        }

        #endregion

        #region Maze Generation and Stage Content

        private void RebuildMaze()
        {
            if (mazeGenerator == null)
            {
                Debug.LogWarning($"{name} needs an assigned ArcadeGen3D maze generator for Labyrinth Crawler.", this);
                return;
            }

            DespawnEnemies();
            secretPass.Clear();
            stageSecretsFound = 0;
            currentExitPad = null;

            ArcadeMazeRules rules = labyrinthMazeRules.CreateArcadeRules(
                currentMazeWidth, currentMazeDepth,
                labyrinthMazeRules.GetPitCount(CurrentStage),
                labyrinthMazeRules.GetRoomCount(CurrentStage),
                labyrinthMazeRules.GetSolidBlockCount(CurrentStage),
                labyrinthMazeRules.GetAuthoredBuildingCount(CurrentStage));
            rules.activateEndRoomExit = false; // the exit pad replaces the interact clerk

            if (!mazeGenerator.GenerateWithRules(rules, OnMazeReady))
            {
                Debug.LogWarning($"{name} could not generate the Labyrinth Crawler maze with its current rules.", this);
            }
        }

        private void OnMazeReady()
        {
            // Generation finishes after every scene PlayerSpawn.Start() has run,
            // so this is the final word on where the stage begins.
            mazeGenerator.RespawnPlayerAtStartRoom();
            ConfigureGeneratedExit();
            SpawnEnemies();

            if (secretsParent == null)
            {
                secretsParent = new GameObject("Labyrinth Secrets").transform;
            }

            secretPass.SpawnSecrets(mazeGenerator, secretsParent, CurrentStage, OnSecretRevealed, OnSecretCacheCollected);
            stageStartElapsed = runTimer.Elapsed;
        }

        private void ConfigureGeneratedExit()
        {
            // The generator may move the destination away from the preliminary
            // DungeonExit prefab after evaluating the completed graph. Silence
            // every authored marker first so the old room cannot display a
            // second, non-functional exit.
            Room3D[,] generatedRooms = mazeGenerator.Rooms;
            if (generatedRooms != null)
            {
                foreach (Room3D room in generatedRooms)
                {
                    if (room == null)
                    {
                        continue;
                    }

                    foreach (MazeExitInteractable exit in
                             room.GetComponentsInChildren<MazeExitInteractable>(true))
                    {
                        exit.ExitEnabled = false;
                        exit.gameObject.SetActive(false);
                    }

                    foreach (LabyrinthExitPad pad in
                             room.GetComponentsInChildren<LabyrinthExitPad>(true))
                    {
                        pad.gameObject.SetActive(false);
                    }
                }
            }

            Room3D endRoom = GetRoom(mazeGenerator.EndRoomIndex);
            if (endRoom == null)
            {
                return;
            }

            // Prefer the pad authored inside the end room (DungeonExit ships
            // one); fall back to spawning the extracted prefab so any room can
            // serve as the exit. Nothing is built from primitives anymore.
            currentExitPad = endRoom.GetComponentInChildren<LabyrinthExitPad>(true);
            if (currentExitPad == null && exitPadPrefab != null)
            {
                currentExitPad = Instantiate(exitPadPrefab, endRoom.transform);
            }

            if (currentExitPad == null)
            {
                Debug.LogWarning($"{name} found no LabyrinthExitPad in the end room and has no exit pad prefab assigned; the stage cannot be cleared.", this);
                return;
            }

            currentExitPad.gameObject.SetActive(true);
            currentExitPad.Initialize(this, clearDwellSeconds);
            ConfigureExitBeacon();
        }

        // The portal beacon rises from the active pad each stage. It is optional
        // polish, so a missing prefab is silent; the pad still clears the stage.
        private void ConfigureExitBeacon()
        {
            if (currentExitPad == null || exitBeaconPrefab == null)
            {
                return;
            }

            Transform padTransform = currentExitPad.transform;

            // Parent to the end room, not the pad: the pad's squashed scale would
            // otherwise distort the beam column. The room is torn down each
            // rebuild, so the beacon is cleaned up with it.
            LabyrinthExitBeacon beacon = Instantiate(
                exitBeaconPrefab, padTransform.position, Quaternion.identity, padTransform.parent);
            beacon.Bind(this);
        }

        private void SpawnEnemies()
        {
            if (enemyPrefabs == null || enemyPrefabs.Count == 0)
            {
                Debug.LogWarning($"{name} has no enemy prefabs assigned; stages will be combat-free.", this);
                return;
            }

            Room3D[,] rooms = mazeGenerator.Rooms;
            if (rooms == null)
            {
                return;
            }

            List<Room3D> candidateRooms = new List<Room3D>();
            Vector2Int start = mazeGenerator.StartRoomIndex;
            int minRoomDistance = minEnemySpawnRoomDistance;
            while (candidateRooms.Count == 0 && minRoomDistance >= 1)
            {
                for (int x = 0; x < rooms.GetLength(0); x++)
                {
                    for (int z = 0; z < rooms.GetLength(1); z++)
                    {
                        int distanceFromStart = Mathf.Abs(x - start.x) + Mathf.Abs(z - start.y);
                        if (rooms[x, z] == null || rooms[x, z].IsPit || rooms[x, z].IsSolidBlock || distanceFromStart < minRoomDistance)
                        {
                            continue; // never spawn a foe on a floorless pit or sealed inside a block
                        }

                        candidateRooms.Add(rooms[x, z]);
                    }
                }

                // Tiny mazes may have no rooms far enough out; relax the ring.
                minRoomDistance--;
            }

            if (candidateRooms.Count == 0)
            {
                return;
            }

            LabyrinthRuntimeUtility.Shuffle(candidateRooms);

            if (enemiesParent == null)
            {
                enemiesParent = new GameObject("Labyrinth Enemies").transform;
            }

            float offsetRadius = Mathf.Min(mazeGenerator.RoomWidth, mazeGenerator.RoomLength) * 0.2f;
            int enemyCount = CurrentEnemyCount;
            for (int i = 0; i < enemyCount; i++)
            {
                GameObject prefab = enemyPrefabs[i % enemyPrefabs.Count];
                if (prefab == null)
                {
                    continue;
                }

                Room3D room = candidateRooms[i % candidateRooms.Count];
                Vector2 offset = UnityEngine.Random.insideUnitCircle * offsetRadius;
                Vector3 position = room.transform.position + new Vector3(offset.x, 1f, offset.y);

                GameObject enemyObject = Instantiate(prefab, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), enemiesParent);
                EnemyController enemy = enemyObject.GetComponent<EnemyController>();
                if (enemy == null)
                {
                    Debug.LogWarning($"{name} enemy prefab '{prefab.name}' is missing an EnemyController.", this);
                    Destroy(enemyObject);
                    continue;
                }

                enemy.Initialize(this);
                enemies.Add(enemy);
            }
        }

        private void DespawnEnemies()
        {
            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }

            enemies.Clear();
        }

        private Room3D GetRoom(Vector2Int index)
        {
            Room3D[,] rooms = mazeGenerator != null ? mazeGenerator.Rooms : null;
            if (rooms == null ||
                index.x < 0 ||
                index.y < 0 ||
                index.x >= rooms.GetLength(0) ||
                index.y >= rooms.GetLength(1))
            {
                return null;
            }

            return rooms[index.x, index.y];
        }

        #endregion

        #region Player Setup and Score Persistence

        private void EnsurePlayerCombat()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning($"{name} could not find a GameObject tagged 'Player' for combat setup.", this);
                return;
            }

            playerController = player.GetComponentInParent<Player.Controller>();
            if (playerController == null)
            {
                Debug.LogWarning($"{name} found no Player.Controller on the player; move speed upgrades will not be offered.", this);
            }

            if (!player.TryGetComponent(out playerHealth))
            {
                playerHealth = player.AddComponent<Health>();
            }

            playerHealth.Faction = Faction.Player;

            if (!player.TryGetComponent(out playerMana))
            {
                playerMana = player.AddComponent<Mana>();
            }

            if (!player.TryGetComponent(out playerCaster))
            {
                playerCaster = player.AddComponent<SpellCaster>();
            }

            if (playerCaster.SlotCount == 0 && playerSpells.Count > 0)
            {
                playerCaster.ConfigureSlots(playerSpells, playerSpellsUnlockedAtStart);
            }

            if (!player.TryGetComponent(out PlayerSpellInput _))
            {
                player.AddComponent<PlayerSpellInput>();
            }

            if (!player.TryGetComponent(out PlayerHitFeedback _))
            {
                player.AddComponent<PlayerHitFeedback>();
            }

            playerHealth.ResetToMax();
            playerMana.ResetToMax();
            playerHealth.OnDied.RemoveListener(OnPlayerDied);
            playerHealth.OnDied.AddListener(OnPlayerDied);
            playerHealth.OnDamaged.RemoveListener(OnPlayerDamaged);
            playerHealth.OnDamaged.AddListener(OnPlayerDamaged);
        }

        private void ResolveScoreCarrier()
        {
            if (scoreCarrier == null)
            {
                scoreCarrier = PlayerScoreCarrier.FindForPlayer();
            }

            if (scoreCarrier == null)
            {
                Debug.LogWarning($"{name} could not find a PlayerScoreCarrier on the player. Labyrinth Crawler score will not persist.", this);
            }
        }

        private void RecordScore()
        {
            if (scoreRecorded)
            {
                return;
            }

            scoreRecorded = true;
            ResolveScoreCarrier();
            if (scoreCarrier == null)
            {
                lastRecordedScore = score;
                bestRecordedScore = Mathf.Max(bestRecordedScore, score);
                return;
            }

            PlayerScoreCarrier.ScoreRecord scoreRecord = scoreCarrier.RecordScore(
                minigameId,
                score,
                ticketsPerPoint,
                legacyLastScorePlayerPrefsKey,
                legacyBestScorePlayerPrefsKey);
            lastRecordedScore = scoreRecord.LastScore;
            bestRecordedScore = scoreRecord.BestScore;
            ticketsAwarded = scoreRecord.TicketsAwarded;
            totalTickets = scoreRecord.TotalTickets;
        }

        private PlayerScoreCarrier.ScoreRecord ReadScoreRecord()
        {
            return scoreCarrier != null
                ? scoreCarrier.ReadScore(minigameId, legacyLastScorePlayerPrefsKey, legacyBestScorePlayerPrefsKey)
                : new PlayerScoreCarrier.ScoreRecord(minigameId, 0, 0, 0, 0);
        }

        #endregion

        #region Serializable Maze Rules

        [Serializable]
        private class LabyrinthMazeRules
        {
            [Header("Rooms")]
            [Tooltip("Use the room prefabs and placement mode authored on the ArcadeGen3D generator (thematic dungeon rooms). Disable to override with the lists below.")]
            [SerializeField] private bool useGeneratorRoomPrefabs = true;

            [SerializeField] private List<GameObject> possibleRoomPrefabs = new List<GameObject>();
            [SerializeField] private GameObject firstRoomPrefab;
            [SerializeField] private GameObject lastRoomPrefab;
            [SerializeField] private GameObject centerRoomPrefab;
            [SerializeField] private ArcadeGen3D.SpecialRoomPlacementMode specialRoomPlacementMode =
                ArcadeGen3D.SpecialRoomPlacementMode.GenerateFromCenter;

            [Header("Stage Size")]
            [SerializeField] private int startingMazeWidth = 3;
            [SerializeField] private int startingMazeDepth = 3;
            [SerializeField] private int mazeGrowthPerStage = 1;

            [Header("Braiding")]
            [Tooltip("Fraction of dead-ends opened into loops after the carve. Loops give the player a route around pits so a pit obstructs rather than seals the floor. 0 = classic single-path maze.")]
            [SerializeField, Range(0f, 1f)] private float braidRate = 0.35f;

            [Header("Pits")]
            [Tooltip("Void apparatus spawned beneath a designated pit room. Extract it once from DungeonCellPit via Sol/Labyrinth/Build Pit Void Prefab, then assign PitVoid.prefab here. Leave empty to disable pits.")]
            [SerializeField] private GameObject pitVoidPrefab;

            [Tooltip("Pit cells on the first stage. The maze carves AROUND them, so the exit is always reachable and pits stretch the route.")]
            [SerializeField, Min(0)] private int startingPitCount = 2;

            [Tooltip("Extra pit cells added per stage as the maze grows.")]
            [SerializeField, Min(0)] private int pitGrowthPerStage = 1;

            [Header("Buildings")]
            [Tooltip("Procedural buildings on the first stage. They use the shared Building Component planner for organic/L/T/tower-house footprints, roofs, entrances and supports; obstacle-first placement keeps the exit reachable. (Field name kept for prefab compatibility.)")]
            [SerializeField, Min(0)] private int startingSolidBlockCount = 1;

            [Tooltip("Extra procedural buildings added per stage as the maze grows.")]
            [SerializeField, Min(0)] private int solidBlockGrowthPerStage = 1;

            [Tooltip("Smallest building-planner bounding box in cells (1 = a single-cell hut).")]
            [SerializeField, Min(1)] private int buildingMinSize = 1;

            [Tooltip("Largest building-planner bounding box. The generated footprint grows organically inside this limit.")]
            [SerializeField, Min(1)] private int buildingMaxSize = 2;

            [Tooltip("Maximum full-height cells in each generated building column, including the ground floor.")]
            [SerializeField, Range(1, 8)] private int buildingHeightLimit = 3;

            [Tooltip("Requested street entrances on each generated building.")]
            [SerializeField, Min(1)] private int buildingEntranceCount = 1;

            [Tooltip("Chance the stage exit appears inside a successfully placed procedural building.")]
            [SerializeField, Range(0f, 1f)] private float buildingExitChance = 0.35f;

            [Tooltip("Choose the final exit from the completed walkable graph so it is far from the player without changing the generated layout.")]
            [SerializeField] private bool optimizeExitPlacement = true;

            [Tooltip("Minimum interior room steps beyond the nearest real building entrance. 1 prevents entrance-cell exits.")]
            [SerializeField, Min(1)] private int minimumBuildingExitDepth = 1;

            [Tooltip("Indoor exits must preserve at least this fraction of the farthest outdoor route distance.")]
            [SerializeField, Range(0f, 1f)] private float minimumBuildingExitDistanceRatio = 0.75f;

            [Tooltip("Choose the final player start from the completed walkable graph after selecting the exit.")]
            [SerializeField] private bool optimizePlayerSpawnPlacement = true;

            [Tooltip("Chance the optimized player start appears inside a qualifying procedural building.")]
            [SerializeField, Range(0f, 1f)] private float buildingPlayerSpawnChance = 0.35f;

            [Tooltip("Minimum interior room steps beyond the nearest real building entrance for an indoor player start.")]
            [SerializeField, Min(1)] private int minimumBuildingPlayerSpawnDepth = 1;

            [Tooltip("Indoor player starts must preserve at least this fraction of the farthest outdoor route from the exit.")]
            [SerializeField, Range(0f, 1f)] private float minimumBuildingPlayerSpawnDistanceRatio = 0.75f;

            [Tooltip("Hand-authored building prefabs dropped in obstacle-first alongside the procedural ones. Footprint is read from the prefab's bounds; entrances are the perimeter WallSockets flagged as authored openings. Empty = procedural buildings only.")]
            [SerializeField] private List<GameObject> authoredBuildings = new List<GameObject>();

            [Tooltip("Authored buildings placed on the first stage, drawn from the Authored Buildings list.")]
            [SerializeField, Min(0)] private int startingAuthoredBuildingCount;

            [Tooltip("Extra authored buildings added per stage as the maze grows.")]
            [SerializeField, Min(0)] private int authoredBuildingGrowthPerStage;

            [Header("Plazas (open squares)")]
            [Tooltip("Rare open-air plazas on the first stage - widened outdoor squares among the narrow streets (interior walls removed). Only removes walls, so the maze stays solvable. Keep low; 0 = all narrow streets. (Field name kept for prefab compatibility.)")]
            [SerializeField, Min(0)] private int startingRoomCount = 1;

            [Tooltip("Extra plazas added per stage as the maze grows.")]
            [SerializeField, Min(0)] private int roomGrowthPerStage;

            [Tooltip("Smallest plaza edge in cells.")]
            [SerializeField, Min(2)] private int roomMinSize = 2;

            [Tooltip("Largest plaza edge in cells. Keep this well under the starting maze size so a plaza never swallows the whole level.")]
            [SerializeField, Min(2)] private int roomMaxSize = 2;

            [Header("Footprint")]
            [Tooltip("Carve inside an organic, non-rectangular blob so the level outline is irregular and pits stretch the journey around it.")]
            [SerializeField] private bool organicFootprint = true;

            [Tooltip("Fraction of the WxH grid kept active for the organic blob. Lower = more eroded / more irregular.")]
            [SerializeField, Range(0.35f, 1f)] private float footprintFill = 0.7f;

            [Header("Stage Scaling")]
            [SerializeField] private int startingScoreMultiplier = 1;
            [SerializeField] private int scoreMultiplierGrowthPerStage = 1;
            [SerializeField] private int startingEnemyCount = 2;
            [SerializeField] private int enemyGrowthPerStage = 1;

            [Tooltip("One extra enemy every N stages on top of the linear growth, so packs snowball on later waves.")]
            [SerializeField, Min(1)] private int bonusEnemyEveryNStages = 2;

            [Header("Outer Openings")]
            [SerializeField] private bool openStartOuterWall;
            [SerializeField] private Room3D.Directions startOuterWallDirection = Room3D.Directions.SOUTH;
            [SerializeField] private bool openEndOuterWall;
            [SerializeField] private Room3D.Directions endOuterWallDirection = Room3D.Directions.NORTH;

            [Header("Player And Exit")]
            [SerializeField] private bool respawnPlayerAtStart = true;
            [SerializeField] private bool activateEndRoomExit = true;

            public int StartingMazeWidth => startingMazeWidth;
            public int StartingMazeDepth => startingMazeDepth;
            public int MazeGrowthPerStage => mazeGrowthPerStage;

            public int GetPitCount(int stage)
            {
                return Mathf.Max(0, startingPitCount + Mathf.Max(0, stage - 1) * pitGrowthPerStage);
            }

            public int GetRoomCount(int stage)
            {
                return Mathf.Max(0, startingRoomCount + Mathf.Max(0, stage - 1) * roomGrowthPerStage);
            }

            public int GetSolidBlockCount(int stage)
            {
                return Mathf.Max(0, startingSolidBlockCount + Mathf.Max(0, stage - 1) * solidBlockGrowthPerStage);
            }

            public int GetAuthoredBuildingCount(int stage)
            {
                return Mathf.Max(0, startingAuthoredBuildingCount + Mathf.Max(0, stage - 1) * authoredBuildingGrowthPerStage);
            }

            public ArcadeMazeRules CreateArcadeRules(int mazeWidth, int mazeDepth, int pitCount, int roomCount, int solidBlockCount, int authoredBuildingCount)
            {
                return new ArcadeMazeRules
                {
                    plazaCount = roomCount,
                    plazaMinSize = roomMinSize,
                    plazaMaxSize = roomMaxSize,
                    proceduralBuildingCount = solidBlockCount,
                    buildingMinSize = buildingMinSize,
                    buildingMaxSize = buildingMaxSize,
                    buildingHeightLimit = buildingHeightLimit,
                    buildingEntranceCount = buildingEntranceCount,
                    buildingExitChance = buildingExitChance,
                    optimizeExitPlacement = optimizeExitPlacement,
                    minimumBuildingExitDepth = minimumBuildingExitDepth,
                    minimumBuildingExitDistanceRatio =
                        minimumBuildingExitDistanceRatio,
                    optimizePlayerSpawnPlacement =
                        optimizePlayerSpawnPlacement,
                    buildingPlayerSpawnChance =
                        buildingPlayerSpawnChance,
                    minimumBuildingPlayerSpawnDepth =
                        minimumBuildingPlayerSpawnDepth,
                    minimumBuildingPlayerSpawnDistanceRatio =
                        minimumBuildingPlayerSpawnDistanceRatio,
                    authoredBuildingCount = authoredBuildingCount,
                    authoredBuildings = authoredBuildings != null
                        ? new List<GameObject>(authoredBuildings)
                        : new List<GameObject>(),
                    overrideRoomPrefabs = !useGeneratorRoomPrefabs,
                    possibleRoomPrefabs = possibleRoomPrefabs != null
                        ? new List<GameObject>(possibleRoomPrefabs)
                        : new List<GameObject>(),
                    firstRoomPrefab = firstRoomPrefab,
                    lastRoomPrefab = lastRoomPrefab,
                    centerRoomPrefab = centerRoomPrefab,
                    specialRoomPlacementMode = specialRoomPlacementMode,
                    numX = Mathf.Max(1, mazeWidth),
                    numZ = Mathf.Max(1, mazeDepth),
                    braidRate = braidRate,
                    pitCount = pitCount,
                    pitVoidPrefab = pitVoidPrefab,
                    organicFootprint = organicFootprint,
                    footprintFill = footprintFill,
                    openStartOuterWall = openStartOuterWall,
                    startOuterWallDirection = startOuterWallDirection,
                    openEndOuterWall = openEndOuterWall,
                    endOuterWallDirection = endOuterWallDirection,
                    respawnPlayerAtStart = respawnPlayerAtStart,
                    activateEndRoomExit = activateEndRoomExit
                };
            }

            public int GetScoreMultiplier(int stage)
            {
                return Mathf.Max(1, startingScoreMultiplier + Mathf.Max(0, stage - 1) * scoreMultiplierGrowthPerStage);
            }

            public int GetEnemyCount(int stage)
            {
                int stagesIn = Mathf.Max(0, stage - 1);
                int bonus = stagesIn / Mathf.Max(1, bonusEnemyEveryNStages);
                return Mathf.Max(0, startingEnemyCount + stagesIn * enemyGrowthPerStage + bonus);
            }

            public void OnValidate()
            {
                startingMazeWidth = Mathf.Max(2, startingMazeWidth);
                startingMazeDepth = Mathf.Max(2, startingMazeDepth);
                mazeGrowthPerStage = Mathf.Max(1, mazeGrowthPerStage);
                startingScoreMultiplier = Mathf.Max(1, startingScoreMultiplier);
                scoreMultiplierGrowthPerStage = Mathf.Max(0, scoreMultiplierGrowthPerStage);
                startingEnemyCount = Mathf.Max(0, startingEnemyCount);
                enemyGrowthPerStage = Mathf.Max(0, enemyGrowthPerStage);
                bonusEnemyEveryNStages = Mathf.Max(1, bonusEnemyEveryNStages);
                buildingMinSize = Mathf.Max(1, buildingMinSize);
                buildingMaxSize = Mathf.Max(buildingMinSize, buildingMaxSize);
                minimumBuildingExitDepth =
                    Mathf.Max(1, minimumBuildingExitDepth);
                minimumBuildingPlayerSpawnDepth =
                    Mathf.Max(1, minimumBuildingPlayerSpawnDepth);
            }
        }

        #endregion
    }
}
