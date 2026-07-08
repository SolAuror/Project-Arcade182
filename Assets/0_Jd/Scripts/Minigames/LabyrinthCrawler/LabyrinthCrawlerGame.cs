using System;
using System.Collections.Generic;
using Sol;
using Sol.Arcade;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler Game")]
    public class LabyrinthCrawlerGame : MonoBehaviour
    {
        [Header("Timer")]
        [SerializeField] private float timeLimitSeconds = 90f;
        [SerializeField] private bool startOnAwake = true;

        [Header("Maze Rules")]
        [SerializeField] private ArcadeGen3D mazeGenerator;
        [SerializeField] private LabyrinthMazeRules labyrinthMazeRules = new LabyrinthMazeRules();

        [Header("Score")]
        [SerializeField] private int pointsPerExit = 100;
        [SerializeField] private int pointsPerMazeLevel = 25;
        [SerializeField] private string minigameId = "LabyrinthCrawler";
        [SerializeField] private float ticketsPerPoint = 0.1f;
        [SerializeField] private PlayerScoreCarrier scoreCarrier;
        [SerializeField] private string legacyLastScorePlayerPrefsKey = "TimedMazeEscape.LastScore";
        [SerializeField] private string legacyBestScorePlayerPrefsKey = "TimedMazeEscape.BestScore";

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeExterior";
        [SerializeField] private float returnDelaySeconds = 2f;

        private float remainingSeconds;
        private float finishTime;
        private int currentMazeWidth;
        private int currentMazeDepth;
        private int exitsFound;
        private int score;
        private int ticketsAwarded;
        private int totalTickets;
        private int lastRecordedScore;
        private int bestRecordedScore;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;
        private bool scoreRecorded;
        private InputSystem_Actions inputActions;
        private InputActionMap labyrinthInputMap;
        private InputAction castAction;
        private InputAction pauseAction;

        public float RemainingSeconds => remainingSeconds;
        public int CurrentMazeWidth => currentMazeWidth;
        public int CurrentMazeDepth => currentMazeDepth;
        public int CurrentStage => exitsFound + 1;
        public int CurrentStageMultiplier => labyrinthMazeRules.GetScoreMultiplier(CurrentStage);
        public int CurrentEnemyCount => labyrinthMazeRules.GetEnemyCount(CurrentStage);
        public int ExitsFound => exitsFound;
        public int Score => score;
        public int LastRecordedScore => lastRecordedScore;
        public int BestRecordedScore => bestRecordedScore;
        public bool IsRunning => isRunning;
        public bool IsComplete => isComplete;
        public bool HasFailed => hasFailed;

        private void Awake()
        {
            if (mazeGenerator == null)
            {
                mazeGenerator = FindFirstObjectByType<ArcadeGen3D>();
            }

            ResolveScoreCarrier();
            PlayerScoreCarrier.ScoreRecord scoreRecord = ReadScoreRecord();
            lastRecordedScore = scoreRecord.LastScore;
            bestRecordedScore = scoreRecord.BestScore;
            totalTickets = scoreRecord.TotalTickets;
            remainingSeconds = Mathf.Max(0f, timeLimitSeconds);

            if (startOnAwake)
            {
                StartGame();
            }
        }

        private void OnEnable()
        {
            if (inputActions == null)
            {
                inputActions = new InputSystem_Actions();
            }

            castAction = inputActions.FindAction("LabyrinthCrawler/Cast", false);
            pauseAction = inputActions.FindAction("LabyrinthCrawler/Pause", false);
            labyrinthInputMap = castAction?.actionMap ?? pauseAction?.actionMap;

            if (castAction != null)
            {
                castAction.started += OnCastStarted;
            }

            if (pauseAction != null)
            {
                pauseAction.started += OnPauseStarted;
            }

            labyrinthInputMap?.Enable();
        }

        private void OnDisable()
        {
            if (castAction != null)
            {
                castAction.started -= OnCastStarted;
            }

            if (pauseAction != null)
            {
                pauseAction.started -= OnPauseStarted;
            }

            labyrinthInputMap?.Disable();
            castAction = null;
            pauseAction = null;
            labyrinthInputMap = null;
        }

        private void OnDestroy()
        {
            inputActions?.Dispose();
            inputActions = null;
        }

        private void Update()
        {
            if (!isRunning)
            {
                TickReturnDelay();
                return;
            }

            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (remainingSeconds <= 0f)
            {
                FailEscape();
            }
        }

        private void OnValidate()
        {
            timeLimitSeconds = Mathf.Max(1f, timeLimitSeconds);
            labyrinthMazeRules ??= new LabyrinthMazeRules();
            labyrinthMazeRules.OnValidate();
            pointsPerExit = Mathf.Max(1, pointsPerExit);
            pointsPerMazeLevel = Mathf.Max(0, pointsPerMazeLevel);
            ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
        }

        public void StartGame()
        {
            remainingSeconds = Mathf.Max(0f, timeLimitSeconds);
            finishTime = 0f;
            currentMazeWidth = labyrinthMazeRules.StartingMazeWidth;
            currentMazeDepth = labyrinthMazeRules.StartingMazeDepth;
            exitsFound = 0;
            score = 0;
            ticketsAwarded = 0;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            scoreRecorded = false;
            RebuildMaze();
        }

        public void CompleteEscape()
        {
            ReachExit();
        }

        public void ReachExit()
        {
            if (!isRunning || isComplete || hasFailed)
            {
                return;
            }

            exitsFound++;
            score += pointsPerExit + Mathf.Max(0, exitsFound - 1) * pointsPerMazeLevel;
            currentMazeWidth += labyrinthMazeRules.MazeGrowthPerStage;
            currentMazeDepth += labyrinthMazeRules.MazeGrowthPerStage;
            RebuildMaze();
            Debug.Log($"Exit found. Score: {score}. Next maze: {currentMazeWidth}x{currentMazeDepth}.", this);
        }

        public void FailEscape()
        {
            if (!isRunning || isComplete || hasFailed)
            {
                return;
            }

            isRunning = false;
            isComplete = true;
            hasFailed = true;
            finishTime = Time.unscaledTime;
            RecordScore();
            Debug.Log($"Labyrinth Crawler timer expired. Final score: {score}.", this);
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

            ArcadeMazeRules rules = labyrinthMazeRules.CreateArcadeRules(currentMazeWidth, currentMazeDepth);
            if (!mazeGenerator.GenerateWithRules(rules, ConfigureGeneratedExit))
            {
                Debug.LogWarning($"{name} could not generate the Labyrinth Crawler maze with its current rules.", this);
            }
        }

        private void ConfigureGeneratedExit()
        {
            if (mazeGenerator == null || mazeGenerator.Rooms == null)
            {
                return;
            }

            Vector2Int endRoomIndex = mazeGenerator.EndRoomIndex;
            Room3D[,] rooms = mazeGenerator.Rooms;
            if (endRoomIndex.x < 0 ||
                endRoomIndex.y < 0 ||
                endRoomIndex.x >= rooms.GetLength(0) ||
                endRoomIndex.y >= rooms.GetLength(1) ||
                rooms[endRoomIndex.x, endRoomIndex.y] == null)
            {
                return;
            }

            MazeExitInteractable[] exits =
                rooms[endRoomIndex.x, endRoomIndex.y].GetComponentsInChildren<MazeExitInteractable>(true);
            if (exits.Length == 0)
            {
                Debug.LogWarning($"{name} generated an end room without a MazeExitInteractable.", this);
                return;
            }

            foreach (MazeExitInteractable exit in exits)
            {
                exit.AssignLabyrinthCrawlerGame(this);
            }
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

        private void OnCastStarted(InputAction.CallbackContext context)
        {
            // Combat/resource behavior is added by the crawler pass; this map ownership is set up here.
        }

        private void OnPauseStarted(InputAction.CallbackContext context)
        {
            // UI migration is deferred, but the action belongs to the Labyrinth Crawler map now.
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

        private void OnGUI()
        {
            const int width = 320;
            const int height = 116;
            Rect area = new Rect(16f, 16f, width, height);

            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label($"Labyrinth Crawler: {remainingSeconds:0.0}s");
            GUILayout.Label($"Score: {score}  Stage: {CurrentStage}  x{CurrentStageMultiplier}");
            GUILayout.Label($"Maze: {currentMazeWidth} x {currentMazeDepth}");

            if (hasFailed)
            {
                GUILayout.Label($"Time expired. Best: {bestRecordedScore}  Tickets +{ticketsAwarded}/{totalTickets}");
            }
            else
            {
                GUILayout.Label("Find exits before the timer runs out.");
            }

            GUILayout.EndArea();
        }

        [Serializable]
        private class LabyrinthMazeRules
        {
            [Header("Rooms")]
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
                return Mathf.Max(0, startingEnemyCount + Mathf.Max(0, stage - 1) * enemyGrowthPerStage);
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
            }
        }
    }
}
