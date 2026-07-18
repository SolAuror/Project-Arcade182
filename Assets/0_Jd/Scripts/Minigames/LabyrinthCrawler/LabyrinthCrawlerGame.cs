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

        [Header("Score")]
        [Tooltip("Points per second under par when clearing a stage.")]
        [SerializeField, Min(0f)] private float timeBonusPerSecond = 10f;

        [Tooltip("Par seconds for stage 1.")]
        [SerializeField, Min(0f)] private float parBaseSeconds = 20f;

        [Tooltip("Par seconds added per stage (par scales with maze size).")]
        [SerializeField, Min(0f)] private float parPerStageSeconds = 6f;

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

        [Header("Fall Safety")]
        [Tooltip("Players falling below this world Y are teleported back to the stage start room.")]
        [SerializeField] private float fallRespawnY = -5f;

        [Header("Upgrades")]
        [SerializeField] private LabyrinthUpgradeSystem upgradeSystem = new LabyrinthUpgradeSystem();

        [Header("Secrets")]
        [Tooltip("Post-carve pass hiding dead-end rooms behind illusory walls. Labyrinth-only; the hub maze never runs this.")]
        [SerializeField] private LabyrinthSecretPass secretPass = new LabyrinthSecretPass();

        [Tooltip("Base points for uncovering a secret room, multiplied by the stage score multiplier.")]
        [SerializeField, Min(0)] private int pointsPerSecret = 100;

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeHub";
        [SerializeField] private float returnDelaySeconds = 2f;

        private readonly List<EnemyController> enemies = new List<EnemyController>();

        private LabyrinthUpgradeScreen upgradeScreen;
        private LabyrinthExitPad currentExitPad;
        private Transform enemiesParent;
        private Transform secretsParent;
        private int secretsFound;
        private Health playerHealth;
        private Mana playerMana;
        private SpellCaster playerCaster;
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

        public float RunSeconds => runTimer != null ? runTimer.Elapsed : 0f;
        public int CurrentMazeWidth => currentMazeWidth;
        public int CurrentMazeDepth => currentMazeDepth;
        public int CurrentStage => exitsFound + 1;
        public int CurrentStageMultiplier => labyrinthMazeRules.GetScoreMultiplier(CurrentStage);
        public int CurrentEnemyCount => labyrinthMazeRules.GetEnemyCount(CurrentStage);
        public int ExitsFound => exitsFound;
        public int EnemiesKilled => enemiesKilled;
        public int SecretsFound => secretsFound;
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

            Debug.Log($"Player fell below y {fallRespawnY:0.0}; respawning at the stage start room.", this);
            mazeGenerator.RespawnPlayerAtStartRoom();
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

        public void StartGame()
        {
            finishTime = 0f;
            currentMazeWidth = labyrinthMazeRules.StartingMazeWidth;
            currentMazeDepth = labyrinthMazeRules.StartingMazeDepth;
            exitsFound = 0;
            enemiesKilled = 0;
            secretsFound = 0;
            score = 0;
            ticketsAwarded = 0;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            isChoosingUpgrade = false;
            scoreRecorded = false;
            Time.timeScale = 1f;

            EnsurePlayerCombat();
            upgradeSystem.Bind(playerCaster, playerHealth, playerMana);

            runTimer.Begin();
            stageStartElapsed = 0f;
            RebuildMaze();
        }

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
            // exitsFound was just incremented, so stage 1 uses the base par.
            float parSeconds = parBaseSeconds + parPerStageSeconds * (exitsFound - 1);
            int stageTimeBonus = Mathf.Max(0, Mathf.RoundToInt((parSeconds - stageClearSeconds) * timeBonusPerSecond));
            score += stageTimeBonus;

            Debug.Log($"Stage {exitsFound} clear in {stageClearSeconds:0.0}s (par {parSeconds:0.0}s). +{stageTimeBonus} points, total {score}.", this);

            BeginUpgradeChoice();
        }

        public void NotifyEnemyDied(EnemyController enemy)
        {
            Vector3 killPosition = enemy != null ? enemy.transform.position : Vector3.zero;
            enemies.Remove(enemy);
            enemiesKilled++;

            // Kills score live (scaled by stage) so combat always pays off, even
            // on a run that ends before the first exit.
            int killScore = pointsPerKill * CurrentStageMultiplier;
            score += killScore;

            PlayClip(enemyKillClip);
            if (enemy != null && killScore > 0)
            {
                DamagePopup.SpawnText(killPosition + Vector3.up * 1.5f, $"+{killScore}", ScorePopColor, 0f, 1.2f);
            }
        }

        private void OnSecretRevealed(IllusoryWall wall)
        {
            secretsFound++;

            // The wall handles its own reveal juice (jingle + "Secret!" pop);
            // the game's contribution is the score, scaled like kills are.
            int gain = pointsPerSecret * CurrentStageMultiplier;
            score += gain;

            if (wall != null && gain > 0)
            {
                DamagePopup.SpawnText(wall.transform.position + Vector3.up * 0.5f, $"+{gain}", ScorePopColor, 0f, 1.2f);
            }
        }

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

            isChoosingUpgrade = false;
            Time.timeScale = 1f;
            runTimer.Resume();

            currentMazeWidth += labyrinthMazeRules.MazeGrowthPerStage;
            currentMazeDepth += labyrinthMazeRules.MazeGrowthPerStage;
            RebuildMaze();
        }

        private void OnPlayerDied()
        {
            if (!isRunning || isComplete)
            {
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

        private void TickReturnDelay()
        {
            if (!returnToSceneOnFinish || (!isComplete && !hasFailed))
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

        private void RebuildMaze()
        {
            if (mazeGenerator == null)
            {
                Debug.LogWarning($"{name} needs an assigned ArcadeGen3D maze generator for Labyrinth Crawler.", this);
                return;
            }

            DespawnEnemies();
            secretPass.Clear();
            currentExitPad = null;

            ArcadeMazeRules rules = labyrinthMazeRules.CreateArcadeRules(currentMazeWidth, currentMazeDepth);
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

            secretPass.SpawnSecrets(mazeGenerator, secretsParent, CurrentStage, OnSecretRevealed);
            stageStartElapsed = runTimer.Elapsed;
        }

        private void ConfigureGeneratedExit()
        {
            Room3D endRoom = GetRoom(mazeGenerator.EndRoomIndex);
            if (endRoom == null)
            {
                return;
            }

            // The stand-on pad replaces the old interact exit; keep any clerk quiet.
            foreach (MazeExitInteractable exit in endRoom.GetComponentsInChildren<MazeExitInteractable>(true))
            {
                exit.ExitEnabled = false;
                exit.gameObject.SetActive(false);
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
                        if (rooms[x, z] == null || distanceFromStart < minRoomDistance)
                        {
                            continue;
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

            for (int i = candidateRooms.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (candidateRooms[i], candidateRooms[j]) = (candidateRooms[j], candidateRooms[i]);
            }

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

        private void EnsurePlayerCombat()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning($"{name} could not find a GameObject tagged 'Player' for combat setup.", this);
                return;
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

            public ArcadeMazeRules CreateArcadeRules(int mazeWidth, int mazeDepth)
            {
                return new ArcadeMazeRules
                {
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
            }
        }
    }
}
