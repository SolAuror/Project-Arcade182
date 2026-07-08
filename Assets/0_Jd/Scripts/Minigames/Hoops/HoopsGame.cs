using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Hoops Game")]
    public class HoopsGame : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField] private float roundSeconds = 75f;
        [SerializeField] private int targetScore = 10;
        [SerializeField] private bool startOnAwake = true;
        [SerializeField] private string minigameId = "Hoops";
        [SerializeField] private float ticketsPerPoint = 1f;
        [SerializeField] private PlayerScoreCarrier scoreCarrier;
        [SerializeField] private bool onlyOneActiveHoop = true;

        [Header("Hoops")]
        [SerializeField] private List<HoopsScoreZone> hoops = new List<HoopsScoreZone>();
        [SerializeField] private List<HoopsThrowable> throwables = new List<HoopsThrowable>();

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeExterior";
        [SerializeField] private float returnDelaySeconds = 2f;

        private float remainingSeconds;
        private float finishTime;
        private int score;
        private int bestRecordedScore;
        private int ticketsAwarded;
        private int totalTickets;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;
        private HoopsScoreZone activeHoop;
        private InputSystem_Actions inputActions;
        private InputActionMap hoopsInputMap;
        private InputAction resetBallAction;

        public int Score => score;
        public float RemainingSeconds => remainingSeconds;
        public HoopsScoreZone ActiveHoop => activeHoop;

        private void Awake()
        {
            if (hoops.Count == 0)
            {
                hoops.AddRange(FindObjectsByType<HoopsScoreZone>(FindObjectsSortMode.None));
            }

            if (throwables.Count == 0)
            {
                throwables.AddRange(FindObjectsByType<HoopsThrowable>(FindObjectsSortMode.None));
            }

            foreach (HoopsScoreZone hoop in hoops)
            {
                if (hoop != null)
                {
                    hoop.AssignGame(this);
                }
            }

            remainingSeconds = Mathf.Max(0f, roundSeconds);
            ResolveScoreCarrier();
            PlayerScoreCarrier.ScoreRecord scoreRecord = ReadScoreRecord();
            bestRecordedScore = scoreRecord.BestScore;
            totalTickets = scoreRecord.TotalTickets;

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

            resetBallAction = inputActions.FindAction("Hoops/ResetBall", false);
            hoopsInputMap = resetBallAction?.actionMap ?? inputActions.asset.FindActionMap("Hoops", false);
            if (resetBallAction != null)
            {
                resetBallAction.started += OnResetBallStarted;
            }

            hoopsInputMap?.Enable();
        }

        private void OnDisable()
        {
            if (resetBallAction != null)
            {
                resetBallAction.started -= OnResetBallStarted;
            }

            hoopsInputMap?.Disable();
            resetBallAction = null;
            hoopsInputMap = null;
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
                FinishGame(score >= targetScore);
            }
        }

        private void OnResetBallStarted(InputAction.CallbackContext context)
        {
            ResetThrowables();
        }

        private void ResetThrowables()
        {
            foreach (HoopsThrowable throwable in throwables)
            {
                throwable?.ResetToSpawn();
            }
        }

        private void OnValidate()
        {
            roundSeconds = Mathf.Max(1f, roundSeconds);
            targetScore = Mathf.Max(1, targetScore);
            ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
        }

        public void StartGame()
        {
            remainingSeconds = Mathf.Max(0f, roundSeconds);
            score = 0;
            ticketsAwarded = 0;
            finishTime = 0f;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            PickNextHoop(null);
        }

        public void RegisterScore(HoopsScoreZone hoop, HoopsThrowable scoreObject)
        {
            if (!isRunning || hoop == null)
            {
                return;
            }

            if (onlyOneActiveHoop && hoop != activeHoop)
            {
                return;
            }

            score += Mathf.Max(1, hoop.Points);
            scoreObject?.MarkScored();
            Debug.Log($"Hoops score: {score}", hoop);

            if (score >= targetScore)
            {
                FinishGame(true);
                return;
            }

            PickNextHoop(hoop);
        }

        private void PickNextHoop(HoopsScoreZone previousHoop)
        {
            if (!onlyOneActiveHoop || hoops.Count == 0)
            {
                foreach (HoopsScoreZone hoop in hoops)
                {
                    hoop?.SetActiveTarget(true);
                }

                activeHoop = null;
                return;
            }

            List<HoopsScoreZone> validHoops = new List<HoopsScoreZone>();
            foreach (HoopsScoreZone hoop in hoops)
            {
                if (hoop != null)
                {
                    validHoops.Add(hoop);
                }
            }

            if (validHoops.Count == 0)
            {
                activeHoop = null;
                return;
            }

            activeHoop = validHoops[Random.Range(0, validHoops.Count)];
            if (validHoops.Count > 1)
            {
                while (activeHoop == previousHoop)
                {
                    activeHoop = validHoops[Random.Range(0, validHoops.Count)];
                }
            }

            foreach (HoopsScoreZone hoop in validHoops)
            {
                hoop.SetActiveTarget(hoop == activeHoop);
            }
        }

        private void FinishGame(bool won)
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;
            isComplete = won;
            hasFailed = !won;
            finishTime = Time.unscaledTime;
            RecordScore();
            Debug.Log(won ? $"Hoops complete with {score} points." : $"Hoops failed with {score} points.", this);
        }

        private void ResolveScoreCarrier()
        {
            if (scoreCarrier == null)
            {
                scoreCarrier = PlayerScoreCarrier.FindForPlayer();
            }

            if (scoreCarrier == null)
            {
                Debug.LogWarning($"{name} could not find a PlayerScoreCarrier on the player. Hoops score will not persist.", this);
            }
        }

        private void RecordScore()
        {
            ResolveScoreCarrier();
            if (scoreCarrier == null)
            {
                bestRecordedScore = Mathf.Max(bestRecordedScore, score);
                return;
            }

            PlayerScoreCarrier.ScoreRecord scoreRecord = scoreCarrier.RecordScore(minigameId, score, ticketsPerPoint);
            bestRecordedScore = scoreRecord.BestScore;
            ticketsAwarded = scoreRecord.TicketsAwarded;
            totalTickets = scoreRecord.TotalTickets;
        }

        private PlayerScoreCarrier.ScoreRecord ReadScoreRecord()
        {
            return scoreCarrier != null
                ? scoreCarrier.ReadScore(minigameId)
                : new PlayerScoreCarrier.ScoreRecord(minigameId, 0, 0, 0, 0);
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

        private void OnGUI()
        {
            const int width = 320;
            const int height = 100;
            Rect area = new Rect(16f, 16f, width, height);

            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label($"Hoops: {score}/{targetScore}");
            GUILayout.Label($"Time: {remainingSeconds:0.0}s");

            if (isComplete)
            {
                GUILayout.Label($"Target score reached  Best: {bestRecordedScore}  Tickets +{ticketsAwarded}/{totalTickets}");
            }
            else if (hasFailed)
            {
                GUILayout.Label($"Round over  Best: {bestRecordedScore}  Tickets +{ticketsAwarded}/{totalTickets}");
            }
            else if (activeHoop != null)
            {
                GUILayout.Label($"Active hoop: {activeHoop.name}");
            }

            GUILayout.EndArea();
        }
    }
}
