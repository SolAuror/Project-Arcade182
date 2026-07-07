using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Hoop Throw Game")]
    public class HoopThrowGame : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField] private float roundSeconds = 75f;
        [SerializeField] private int targetScore = 10;
        [SerializeField] private bool startOnAwake = true;
        [SerializeField] private bool onlyOneActiveHoop = true;

        [Header("Hoops")]
        [SerializeField] private List<HoopScoreZone> hoops = new List<HoopScoreZone>();

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeExterior";
        [SerializeField] private float returnDelaySeconds = 2f;

        private float remainingSeconds;
        private float finishTime;
        private int score;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;
        private HoopScoreZone activeHoop;

        public int Score => score;
        public float RemainingSeconds => remainingSeconds;
        public HoopScoreZone ActiveHoop => activeHoop;

        private void Awake()
        {
            if (hoops.Count == 0)
            {
                hoops.AddRange(FindObjectsByType<HoopScoreZone>(FindObjectsSortMode.None));
            }

            foreach (HoopScoreZone hoop in hoops)
            {
                if (hoop != null)
                {
                    hoop.AssignGame(this);
                }
            }

            remainingSeconds = Mathf.Max(0f, roundSeconds);

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
                FinishGame(score >= targetScore);
            }
        }

        private void OnValidate()
        {
            roundSeconds = Mathf.Max(1f, roundSeconds);
            targetScore = Mathf.Max(1, targetScore);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
        }

        public void StartGame()
        {
            remainingSeconds = Mathf.Max(0f, roundSeconds);
            score = 0;
            finishTime = 0f;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            PickNextHoop(null);
        }

        public void RegisterScore(HoopScoreZone hoop, ThrowableScoreObject scoreObject)
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
            Debug.Log($"Hoop score: {score}", hoop);

            if (score >= targetScore)
            {
                FinishGame(true);
                return;
            }

            PickNextHoop(hoop);
        }

        private void PickNextHoop(HoopScoreZone previousHoop)
        {
            if (!onlyOneActiveHoop || hoops.Count == 0)
            {
                foreach (HoopScoreZone hoop in hoops)
                {
                    hoop?.SetActiveTarget(true);
                }

                activeHoop = null;
                return;
            }

            List<HoopScoreZone> validHoops = new List<HoopScoreZone>();
            foreach (HoopScoreZone hoop in hoops)
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

            foreach (HoopScoreZone hoop in validHoops)
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
            Debug.Log(won ? $"Hoop Throw complete with {score} points." : $"Hoop Throw failed with {score} points.", this);
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
            GUILayout.Label($"Hoop Throw: {score}/{targetScore}");
            GUILayout.Label($"Time: {remainingSeconds:0.0}s");

            if (isComplete)
            {
                GUILayout.Label("Target score reached");
            }
            else if (hasFailed)
            {
                GUILayout.Label("Round over");
            }
            else if (activeHoop != null)
            {
                GUILayout.Label($"Active hoop: {activeHoop.name}");
            }

            GUILayout.EndArea();
        }
    }
}
