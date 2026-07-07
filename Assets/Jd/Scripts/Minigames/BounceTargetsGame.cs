using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Bounce Targets Game")]
    public class BounceTargetsGame : MonoBehaviour
    {
        [Header("Rules")]
        [SerializeField] private int roundShots = 10;
        [SerializeField] private bool startOnAwake = true;

        [Header("Board")]
        [SerializeField] private BounceLauncher launcher;
        [SerializeField] private BounceBall ballPrefab;
        [SerializeField] private List<BounceTarget> targets = new List<BounceTarget>();
        [SerializeField] private float physicsPlaneZ = 0f;
        [SerializeField] private float drainY = -6.5f;
        [SerializeField] private float ballSettleSpeed = 0.15f;
        [SerializeField] private float ballSettleSeconds = 1.25f;
        [SerializeField] private float maxBallLifeSeconds = 14f;

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeExterior";
        [SerializeField] private float returnDelaySeconds = 2f;

        private readonly List<BounceBall> activeBalls = new List<BounceBall>();
        private int shotsRemaining;
        private int score;
        private int requiredTargetsRemaining;
        private float finishTime;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;

        public BounceBall BallPrefab => ballPrefab;
        public float PhysicsPlaneZ => physicsPlaneZ;
        public float DrainY => drainY;
        public int ShotsRemaining => shotsRemaining;
        public int Score => score;
        public int RequiredTargetsRemaining => requiredTargetsRemaining;
        public bool IsRunning => isRunning;
        public bool CanLaunch => isRunning && !isComplete && !hasFailed && ActiveBallCount == 0 && shotsRemaining > 0;

        private int ActiveBallCount
        {
            get
            {
                PruneMissingBalls();
                return activeBalls.Count;
            }
        }

        private void Awake()
        {
            if (targets.Count == 0)
            {
                targets.AddRange(FindObjectsByType<BounceTarget>(FindObjectsSortMode.None));
            }

            foreach (BounceTarget target in targets)
            {
                target?.AssignGame(this);
            }

            if (launcher == null)
            {
                launcher = FindFirstObjectByType<BounceLauncher>();
            }

            launcher?.AssignGame(this);

            if (startOnAwake)
            {
                StartGame();
            }
        }

        private void Update()
        {
            if (isRunning)
            {
                PruneMissingBalls();
                if (activeBalls.Count == 0 && shotsRemaining <= 0 && requiredTargetsRemaining > 0)
                {
                    FinishGame(false);
                }

                return;
            }

            if (!isRunning)
            {
                TickReturnDelay();
            }
        }

        private void OnValidate()
        {
            roundShots = Mathf.Max(1, roundShots);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
            ballSettleSpeed = Mathf.Max(0f, ballSettleSpeed);
            ballSettleSeconds = Mathf.Max(0f, ballSettleSeconds);
            maxBallLifeSeconds = Mathf.Max(1f, maxBallLifeSeconds);
        }

        public void StartGame()
        {
            foreach (BounceBall activeBall in activeBalls)
            {
                if (activeBall != null)
                {
                    Destroy(activeBall.gameObject);
                }
            }

            activeBalls.Clear();
            shotsRemaining = Mathf.Max(1, roundShots);
            score = 0;
            finishTime = 0f;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            requiredTargetsRemaining = 0;

            foreach (BounceTarget target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                target.ResetTarget();
                target.AssignGame(this);

                if (target.RequiredTarget)
                {
                    requiredTargetsRemaining++;
                }
            }

            if (requiredTargetsRemaining == 0)
            {
                FinishGame(true);
            }
        }

        public bool TryLaunchBall(Vector3 position, Vector3 direction, float launchSpeed)
        {
            if (!CanLaunch || ballPrefab == null)
            {
                return false;
            }

            BounceBall ball = Instantiate(ballPrefab, position, Quaternion.identity);
            RegisterBall(ball);
            shotsRemaining = Mathf.Max(0, shotsRemaining - 1);
            ball.Launch(direction.normalized * launchSpeed);
            return true;
        }

        public void RegisterBall(BounceBall ball)
        {
            if (ball == null || activeBalls.Contains(ball))
            {
                return;
            }

            ball.Initialize(this, physicsPlaneZ, drainY, ballSettleSpeed, ballSettleSeconds, maxBallLifeSeconds);
            activeBalls.Add(ball);
        }

        public void RegisterTargetHit(BounceTarget target, BounceBall ball)
        {
            if (!isRunning || target == null)
            {
                return;
            }

            score += Mathf.Max(0, target.ScoreValue);

            if (target.RequiredTarget)
            {
                requiredTargetsRemaining = Mathf.Max(0, requiredTargetsRemaining - 1);
            }

            if (requiredTargetsRemaining <= 0)
            {
                FinishGame(true);
            }
        }

        public void NotifyBallFinished(BounceBall ball, bool destroyBall = true)
        {
            if (ball != null)
            {
                activeBalls.Remove(ball);
                if (destroyBall)
                {
                    Destroy(ball.gameObject);
                }
            }

            PruneMissingBalls();

            if (!isRunning || isComplete || hasFailed || activeBalls.Count > 0)
            {
                return;
            }

            if (shotsRemaining <= 0)
            {
                FinishGame(false);
            }
        }

        private void PruneMissingBalls()
        {
            for (int i = activeBalls.Count - 1; i >= 0; i--)
            {
                if (activeBalls[i] == null)
                {
                    activeBalls.RemoveAt(i);
                }
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
            Debug.Log(won ? $"Bounce Targets complete with {score} points." : $"Bounce Targets failed with {score} points.", this);
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
            const int width = 340;
            const int height = 112;
            Rect area = new Rect(16f, 16f, width, height);

            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label($"Bounce Targets: {score} points");
            GUILayout.Label($"Shots: {shotsRemaining}/{roundShots}");
            GUILayout.Label($"Targets left: {requiredTargetsRemaining}");

            if (isComplete)
            {
                GUILayout.Label("Board cleared");
            }
            else if (hasFailed)
            {
                GUILayout.Label("Out of shots");
            }
            else if (CanLaunch)
            {
                GUILayout.Label("Aim and release to launch.");
            }

            GUILayout.EndArea();
        }
    }
}
