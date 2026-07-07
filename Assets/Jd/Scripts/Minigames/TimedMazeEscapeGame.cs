using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Timed Maze Escape Game")]
    public class TimedMazeEscapeGame : MonoBehaviour
    {
        [Header("Timer")]
        [SerializeField] private float timeLimitSeconds = 90f;
        [SerializeField] private bool startOnAwake = true;

        [Header("Maze Growth")]
        [SerializeField] private DynamicMazeEscapeBuilder mazeBuilder;
        [SerializeField] private int startingMazeWidth = 3;
        [SerializeField] private int startingMazeDepth = 3;
        [SerializeField] private int mazeGrowthPerExit = 1;
        [SerializeField] private bool disableLegacyStaticMazeObjects = true;

        [Header("Score")]
        [SerializeField] private int pointsPerExit = 100;
        [SerializeField] private int pointsPerMazeLevel = 25;
        [SerializeField] private string lastScorePlayerPrefsKey = "TimedMazeEscape.LastScore";
        [SerializeField] private string bestScorePlayerPrefsKey = "TimedMazeEscape.BestScore";

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
        private int lastRecordedScore;
        private int bestRecordedScore;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;
        private bool scoreRecorded;

        private static readonly string[] LegacyStaticMazeObjectNames =
        {
            "Floor",
            "North Boundary",
            "South Boundary",
            "East Boundary",
            "West Boundary",
            "Maze Wall A",
            "Maze Wall B",
            "Maze Wall C",
            "Maze Wall D",
            "Maze Wall E",
            "Finish Trigger"
        };

        public float RemainingSeconds => remainingSeconds;
        public int CurrentMazeWidth => currentMazeWidth;
        public int CurrentMazeDepth => currentMazeDepth;
        public int ExitsFound => exitsFound;
        public int Score => score;
        public int LastRecordedScore => lastRecordedScore;
        public int BestRecordedScore => bestRecordedScore;
        public bool IsRunning => isRunning;
        public bool IsComplete => isComplete;
        public bool HasFailed => hasFailed;

        private void Awake()
        {
            if (mazeBuilder == null)
            {
                mazeBuilder = FindFirstObjectByType<DynamicMazeEscapeBuilder>();
            }

            if (mazeBuilder == null)
            {
                GameObject builderObject = new GameObject("Dynamic Maze Builder");
                mazeBuilder = builderObject.AddComponent<DynamicMazeEscapeBuilder>();
            }

            lastRecordedScore = PlayerPrefs.GetInt(lastScorePlayerPrefsKey, 0);
            bestRecordedScore = PlayerPrefs.GetInt(bestScorePlayerPrefsKey, 0);
            remainingSeconds = Mathf.Max(0f, timeLimitSeconds);

            if (startOnAwake)
            {
                StartGame();
            }
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
            startingMazeWidth = Mathf.Max(2, startingMazeWidth);
            startingMazeDepth = Mathf.Max(2, startingMazeDepth);
            mazeGrowthPerExit = Mathf.Max(1, mazeGrowthPerExit);
            pointsPerExit = Mathf.Max(1, pointsPerExit);
            pointsPerMazeLevel = Mathf.Max(0, pointsPerMazeLevel);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
        }

        public void StartGame()
        {
            remainingSeconds = Mathf.Max(0f, timeLimitSeconds);
            finishTime = 0f;
            currentMazeWidth = Mathf.Max(2, startingMazeWidth);
            currentMazeDepth = Mathf.Max(2, startingMazeDepth);
            exitsFound = 0;
            score = 0;
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
            currentMazeWidth += mazeGrowthPerExit;
            currentMazeDepth += mazeGrowthPerExit;
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
            Debug.Log($"Maze escape timer expired. Final score: {score}.", this);
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
            if (mazeBuilder == null)
            {
                return;
            }

            if (disableLegacyStaticMazeObjects)
            {
                DisableLegacyStaticMazeObjects();
            }

            mazeBuilder.BuildMaze(currentMazeWidth, currentMazeDepth, this);
        }

        private void DisableLegacyStaticMazeObjects()
        {
            foreach (string objectName in LegacyStaticMazeObjectNames)
            {
                GameObject legacyObject = GameObject.Find(objectName);
                if (legacyObject != null && legacyObject.GetComponentInParent<DynamicMazeEscapeBuilder>() == null)
                {
                    legacyObject.SetActive(false);
                }
            }
        }

        private void RecordScore()
        {
            if (scoreRecorded)
            {
                return;
            }

            scoreRecorded = true;
            lastRecordedScore = score;
            bestRecordedScore = Mathf.Max(bestRecordedScore, score);

            if (!string.IsNullOrWhiteSpace(lastScorePlayerPrefsKey))
            {
                PlayerPrefs.SetInt(lastScorePlayerPrefsKey, lastRecordedScore);
            }

            if (!string.IsNullOrWhiteSpace(bestScorePlayerPrefsKey))
            {
                PlayerPrefs.SetInt(bestScorePlayerPrefsKey, bestRecordedScore);
            }

            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            const int width = 320;
            const int height = 116;
            Rect area = new Rect(16f, 16f, width, height);

            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label($"Maze Escape: {remainingSeconds:0.0}s");
            GUILayout.Label($"Score: {score}  Exits: {exitsFound}");
            GUILayout.Label($"Maze: {currentMazeWidth} x {currentMazeDepth}");

            if (hasFailed)
            {
                GUILayout.Label($"Time expired. Best: {bestRecordedScore}");
            }
            else
            {
                GUILayout.Label("Find exits before the timer runs out.");
            }

            GUILayout.EndArea();
        }
    }
}
