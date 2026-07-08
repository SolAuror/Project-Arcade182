using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Game")]
    public class AtomSmasherGame : MonoBehaviour
    {
        private enum QuantumModifier
        {
            SpeedBoost,
            SplitBall
        }

        [Serializable]
        private sealed class ObstacleSpawnOption
        {
            [SerializeField] private GameObject prefab;
            [SerializeField, Min(1)] private int spawnWeight = 1;
            [SerializeField, Min(1)] private int minimumWave = 1;
            [SerializeField, Range(0f, 1f)] private float symmetryChance = 0.35f;
            [SerializeField] private bool randomizeRotation = true;

            public GameObject Prefab => prefab;
            public int SpawnWeight => Mathf.Max(0, spawnWeight);
            public int MinimumWave => Mathf.Max(1, minimumWave);
            public float SymmetryChance => Mathf.Clamp01(symmetryChance);
            public bool RandomizeRotation => randomizeRotation;
            public bool IsAvailable(int wave) => prefab != null && SpawnWeight > 0 && wave >= MinimumWave;
        }

        [Serializable]
        private sealed class MovingTargetSpawnOption
        {
            [SerializeField] private AtomSmasherTarget prefab;
            [SerializeField, Min(1)] private int spawnWeight = 1;
            [SerializeField, Min(1)] private int minimumWave = 2;

            public AtomSmasherTarget Prefab => prefab;
            public int SpawnWeight => Mathf.Max(0, spawnWeight);
            public int MinimumWave => Mathf.Max(1, minimumWave);
            public bool IsAvailable(int wave) => prefab != null && SpawnWeight > 0 && wave >= MinimumWave;
        }

        [Serializable]
        private sealed class QuantumEffectOption
        {
            [SerializeField] private QuantumModifier modifier = QuantumModifier.SpeedBoost;
            [SerializeField] private string displayName = "Speed Boost";
            [SerializeField, Min(1)] private int spawnWeight = 1;
            [SerializeField, Min(1)] private int minimumWave = 1;

            public QuantumModifier Modifier => modifier;
            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? modifier.ToString() : displayName;
            public int SpawnWeight => Mathf.Max(0, spawnWeight);
            public int MinimumWave => Mathf.Max(1, minimumWave);
            public bool IsAvailable(int wave) => SpawnWeight > 0 && wave >= MinimumWave;
        }

        private const string GameTitle = "Atom Smasher";

        [Header("Rules")]
        [SerializeField] private int roundShots = 10;
        [SerializeField] private bool startOnAwake = true;
        [SerializeField] private int scoreMultiplierStep = 1;
        [SerializeField] private string minigameId = "AtomSmasher";
        [SerializeField] private float ticketsPerPoint = 0.1f;
        [SerializeField] private PlayerScoreCarrier scoreCarrier;
        [SerializeField] private bool replayOnClear = true;
        [SerializeField] private bool shuffleTargetsOnClear = true;
        [SerializeField] private Rect shuffleArea = new Rect(-9.75f, -0.4f, 19.7f, 4.85f);
        [SerializeField] private float shuffleMinTargetDistance = 1f;
        [SerializeField] private int maxShufflePlacementAttemptsPerTarget = 40;
        [SerializeField] private bool useTimerMode;
        [SerializeField] private float roundTimeSeconds = 60f;
        [SerializeField] private bool resetShotsOnWaveClear = true;

        [Header("Board")]
        [SerializeField] private AtomSmasherLauncher launcher;
        [SerializeField] private AtomSmasherBall ballPrefab;
        [SerializeField] private List<AtomSmasherTarget> targets = new List<AtomSmasherTarget>();
        [SerializeField] private float physicsPlaneZ = 0f;
        [SerializeField] private float drainY = -6.5f;
        [SerializeField] private float ballSettleSpeed = 0.15f;
        [SerializeField] private float ballSettleSeconds = 1.25f;
        [SerializeField] private float maxBallLifeSeconds = 14f;

        [Header("Obstructions")]
        [SerializeField] private bool spawnObstructionOnEachWave = true;
        [SerializeField] private List<ObstacleSpawnOption> obstacleOptions = new List<ObstacleSpawnOption>();
        [SerializeField] private Transform obstructionParent;
        [SerializeField] private Rect obstructionSpawnArea = new Rect(-8.75f, 0.15f, 17.5f, 4.3f);
        [SerializeField] private Rect obstructionReservedLaunchArea = new Rect(-2.5f, -5.8f, 5f, 2.4f);
        [SerializeField] private Rect obstructionReservedDrainArea = new Rect(-10.5f, -6.8f, 21f, 0.9f);
        [SerializeField] private float obstructionMinTargetDistance = 1.25f;
        [SerializeField] private int maxObstructionPlacementAttempts = 40;

        [Header("Moving Targets")]
        [SerializeField] private bool spawnMovingTargets = true;
        [SerializeField] private List<MovingTargetSpawnOption> movingTargetOptions = new List<MovingTargetSpawnOption>();
        [SerializeField] private Transform movingTargetParent;
        [SerializeField] private int movingTargetsPerWave = 1;
        [SerializeField] private Rect movingTargetSpawnArea = new Rect(-8.75f, 0f, 17.5f, 4.5f);
        [SerializeField] private float movingTargetMinDistance = 1.1f;
        [SerializeField] private int maxMovingTargetPlacementAttempts = 40;

        [Header("Quantum Targets")]
        [SerializeField] private bool spawnQuantumTargetPerWave = true;
        [SerializeField] private AtomSmasherTarget quantumTargetPrefab;
        [SerializeField] private Transform quantumTargetParent;
        [SerializeField] private Rect quantumSpawnArea = new Rect(-8.75f, 0f, 17.5f, 4.5f);
        [SerializeField] private float quantumMinTargetDistance = 1.1f;
        [SerializeField] private int maxQuantumPlacementAttempts = 40;
        [SerializeField] private float quantumSpeedMultiplier = 1.6f;
        [SerializeField] private float quantumMaxSpeed = 24f;
        [SerializeField] private int maxActiveBalls = 3;
        [SerializeField] private float splitAngleDegrees = 18f;
        [SerializeField] private float splitSpeedMultiplier = 0.95f;
        [SerializeField] private Color quantumBoostColor = new Color(0.25f, 1f, 0.45f, 1f);
        [SerializeField] private float quantumVisualSeconds = 0.85f;
        [SerializeField] private float statusMessageSeconds = 2f;
        [SerializeField] private List<QuantumEffectOption> quantumEffectOptions = new List<QuantumEffectOption>();

        [Header("Feedback")]
        [SerializeField] private AudioSource feedbackAudioSource;
        [SerializeField] private AudioClip targetHitClip;
        [SerializeField] private AudioClip quantumTriggerClip;
        [SerializeField] private AudioClip waveClearClip;
        [SerializeField] private AudioClip failedRoundClip;
        [SerializeField] private ParticleSystem targetHitParticles;
        [SerializeField] private ParticleSystem quantumTriggerParticles;
        [SerializeField] private ParticleSystem waveClearParticles;
        [SerializeField] private ParticleSystem failedRoundParticles;
        [SerializeField] private Transform feedbackOrigin;
        [SerializeField] private float defaultParticleLifetime = 1.25f;

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeHub";
        [SerializeField] private float returnDelaySeconds = 2f;

        private readonly List<AtomSmasherBall> activeBalls = new List<AtomSmasherBall>();
        private readonly Dictionary<AtomSmasherBall, int> ballScoreMultipliers = new Dictionary<AtomSmasherBall, int>();
        private readonly List<GameObject> activeObstructions = new List<GameObject>();
        private readonly List<AtomSmasherTarget> activeQuantumTargets = new List<AtomSmasherTarget>();
        private readonly List<AtomSmasherTarget> activeMovingTargets = new List<AtomSmasherTarget>();
        private int shotsRemaining;
        private int score;
        private int requiredTargetsRemaining;
        private int waveNumber;
        private float timeRemaining;
        private float finishTime;
        private float statusMessageExpiresAt;
        private string statusMessage;
        private int bestRecordedScore;
        private int ticketsAwarded;
        private int totalTickets;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;
        private bool waveClearPending;

        public AtomSmasherBall BallPrefab => ballPrefab;
        public float PhysicsPlaneZ => physicsPlaneZ;
        public float DrainY => drainY;
        public int ShotsRemaining => shotsRemaining;
        public int Score => score;
        public int RequiredTargetsRemaining => requiredTargetsRemaining;
        public int WaveNumber => waveNumber;
        public float TimeRemaining => timeRemaining;
        public bool IsRunning => isRunning;
        public bool CanLaunch => isRunning && !isComplete && !hasFailed && ActiveBallCount == 0 && shotsRemaining > 0;
        public int CurrentShotMultiplier => GetCurrentShotMultiplier();
        public bool UseTimerMode => useTimerMode;
        public bool IsComplete => isComplete;
        public bool HasFailed => hasFailed;
        public int RoundShots => roundShots;
        public int BestRecordedScore => bestRecordedScore;
        public int TicketsAwarded => ticketsAwarded;
        public int TotalTickets => totalTickets;
        public string StatusMessage => HasStatusMessage ? statusMessage : string.Empty;
        public string FailReason => useTimerMode && timeRemaining <= 0f ? "Time expired" : "Out of shots";

        private int ActiveBallCount
        {
            get
            {
                PruneMissingBalls();
                return activeBalls.Count;
            }
        }

        private bool HasStatusMessage => !string.IsNullOrEmpty(statusMessage) && Time.unscaledTime <= statusMessageExpiresAt;

        private void Awake()
        {
            RefreshTargetsFromScene();

            foreach (AtomSmasherTarget target in targets)
            {
                target?.AssignGame(this);
            }

            if (launcher == null)
            {
                launcher = FindFirstObjectByType<AtomSmasherLauncher>();
            }

            launcher?.AssignGame(this);

            if (feedbackAudioSource == null)
            {
                feedbackAudioSource = GetComponent<AudioSource>();
            }

            ResolveScoreCarrier();
            PlayerScoreCarrier.ScoreRecord scoreRecord = ReadScoreRecord();
            bestRecordedScore = scoreRecord.BestScore;
            totalTickets = scoreRecord.TotalTickets;

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

                if (useTimerMode)
                {
                    TickTimer();
                    if (!isRunning)
                    {
                        return;
                    }
                }

                if (waveClearPending)
                {
                    StartNextWave();
                    return;
                }

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
            scoreMultiplierStep = Mathf.Max(1, scoreMultiplierStep);
            ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
            NormalizeRect(ref shuffleArea);
            shuffleMinTargetDistance = Mathf.Max(0f, shuffleMinTargetDistance);
            maxShufflePlacementAttemptsPerTarget = Mathf.Max(1, maxShufflePlacementAttemptsPerTarget);
            roundTimeSeconds = Mathf.Max(0f, roundTimeSeconds);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
            ballSettleSpeed = Mathf.Max(0f, ballSettleSpeed);
            ballSettleSeconds = Mathf.Max(0f, ballSettleSeconds);
            maxBallLifeSeconds = Mathf.Max(1f, maxBallLifeSeconds);
            NormalizeRect(ref obstructionSpawnArea);
            NormalizeRect(ref obstructionReservedLaunchArea);
            NormalizeRect(ref obstructionReservedDrainArea);
            obstructionMinTargetDistance = Mathf.Max(0f, obstructionMinTargetDistance);
            maxObstructionPlacementAttempts = Mathf.Max(1, maxObstructionPlacementAttempts);
            NormalizeRect(ref movingTargetSpawnArea);
            movingTargetsPerWave = Mathf.Max(0, movingTargetsPerWave);
            movingTargetMinDistance = Mathf.Max(0f, movingTargetMinDistance);
            maxMovingTargetPlacementAttempts = Mathf.Max(1, maxMovingTargetPlacementAttempts);
            NormalizeRect(ref quantumSpawnArea);
            quantumMinTargetDistance = Mathf.Max(0f, quantumMinTargetDistance);
            maxQuantumPlacementAttempts = Mathf.Max(1, maxQuantumPlacementAttempts);
            quantumSpeedMultiplier = Mathf.Max(0.01f, quantumSpeedMultiplier);
            quantumMaxSpeed = Mathf.Max(0f, quantumMaxSpeed);
            maxActiveBalls = Mathf.Max(1, maxActiveBalls);
            splitAngleDegrees = Mathf.Clamp(splitAngleDegrees, 0f, 90f);
            splitSpeedMultiplier = Mathf.Max(0.01f, splitSpeedMultiplier);
            quantumVisualSeconds = Mathf.Max(0f, quantumVisualSeconds);
            statusMessageSeconds = Mathf.Max(0f, statusMessageSeconds);
            defaultParticleLifetime = Mathf.Max(0.1f, defaultParticleLifetime);
        }

        public void StartGame()
        {
            foreach (AtomSmasherBall activeBall in activeBalls)
            {
                if (activeBall != null)
                {
                    Destroy(activeBall.gameObject);
                }
            }

            activeBalls.Clear();
            ballScoreMultipliers.Clear();
            ClearWaveObjects();
            shotsRemaining = Mathf.Max(1, roundShots);
            score = 0;
            ticketsAwarded = 0;
            waveNumber = 1;
            timeRemaining = Mathf.Max(0f, roundTimeSeconds);
            finishTime = 0f;
            statusMessage = string.Empty;
            statusMessageExpiresAt = 0f;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            waveClearPending = false;
            requiredTargetsRemaining = 0;

            requiredTargetsRemaining = ResetTargetsForWave();
            SpawnWaveObjects();

            if (useTimerMode && timeRemaining <= 0f)
            {
                FinishGame(false);
                return;
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

            AtomSmasherBall ball = Instantiate(ballPrefab, position, Quaternion.identity);
            RegisterBall(ball);
            shotsRemaining = Mathf.Max(0, shotsRemaining - 1);
            ball.Launch(direction.normalized * launchSpeed);
            return true;
        }

        public void RegisterBall(AtomSmasherBall ball)
        {
            if (ball == null || activeBalls.Contains(ball))
            {
                return;
            }

            ball.Initialize(this, physicsPlaneZ, drainY, ballSettleSpeed, ballSettleSeconds, maxBallLifeSeconds);
            activeBalls.Add(ball);
            ballScoreMultipliers[ball] = 1;
        }

        public void RegisterTargetHit(AtomSmasherTarget target, AtomSmasherBall ball)
        {
            if (!isRunning || target == null)
            {
                return;
            }

            Vector3 feedbackPosition = target.transform.position;
            PlayFeedback(targetHitClip, targetHitParticles, feedbackPosition, new Color(1f, 0.9f, 0.35f, 1f));

            int targetScore = Mathf.Max(0, target.ScoreValue);
            int multiplier = GetBallScoreMultiplier(ball);
            score = AddClamped(score, MultiplyClamped(targetScore, multiplier));
            AdvanceBallScoreMultiplier(ball, multiplier);

            AtomSmasherQuantumTarget quantumTarget = target.GetComponent<AtomSmasherQuantumTarget>();
            if (quantumTarget != null)
            {
                ApplyRandomQuantumModifier(ball, feedbackPosition);
            }

            if (target.RequiredTarget)
            {
                requiredTargetsRemaining = Mathf.Max(0, requiredTargetsRemaining - 1);
            }

            if (requiredTargetsRemaining <= 0 && !waveClearPending)
            {
                PlayFeedback(waveClearClip, waveClearParticles, GetBoardFeedbackPosition(), new Color(0.35f, 0.85f, 1f, 1f));

                if (replayOnClear)
                {
                    waveClearPending = true;
                }
                else
                {
                    FinishGame(true);
                }
            }
        }

        public void NotifyBallFinished(AtomSmasherBall ball, bool destroyBall = true)
        {
            if (ball != null)
            {
                activeBalls.Remove(ball);
                ballScoreMultipliers.Remove(ball);
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
                AtomSmasherBall activeBall = activeBalls[i];
                if (activeBall == null)
                {
                    if (!ReferenceEquals(activeBall, null))
                    {
                        ballScoreMultipliers.Remove(activeBall);
                    }

                    activeBalls.RemoveAt(i);
                }
            }
        }

        private void RefreshTargetsFromScene()
        {
            HashSet<AtomSmasherTarget> uniqueTargets = new HashSet<AtomSmasherTarget>();
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                AtomSmasherTarget target = targets[i];
                if (target == null || !uniqueTargets.Add(target))
                {
                    targets.RemoveAt(i);
                }
            }

            AtomSmasherTarget[] sceneTargets = FindObjectsByType<AtomSmasherTarget>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (AtomSmasherTarget target in sceneTargets)
            {
                if (target != null && uniqueTargets.Add(target) && target.GetComponent<AtomSmasherQuantumTarget>() == null)
                {
                    targets.Add(target);
                }
            }
        }

        private int ResetTargetsForWave()
        {
            int remainingRequiredTargets = 0;

            foreach (AtomSmasherTarget target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                target.ResetTarget();
                target.AssignGame(this);

                if (target.RequiredTarget)
                {
                    remainingRequiredTargets++;
                }
            }

            return remainingRequiredTargets;
        }

        private void StartNextWave()
        {
            waveClearPending = false;
            waveNumber = waveNumber == int.MaxValue ? int.MaxValue : waveNumber + 1;
            ClearWaveObjects();

            if (resetShotsOnWaveClear)
            {
                shotsRemaining = Mathf.Max(1, roundShots);
            }

            if (shuffleTargetsOnClear)
            {
                ShuffleTargets();
            }

            requiredTargetsRemaining = ResetTargetsForWave();
            SpawnWaveObjects();

            if (requiredTargetsRemaining == 0)
            {
                FinishGame(true);
            }
        }

        private void ShuffleTargets()
        {
            NormalizeRect(ref shuffleArea);

            List<Vector2> placedPositions = new List<Vector2>(targets.Count);
            float minDistanceSquared = shuffleMinTargetDistance * shuffleMinTargetDistance;

            foreach (AtomSmasherTarget target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                Vector2 chosenPosition = RandomPointInArea(shuffleArea);
                for (int attempt = 0; attempt < maxShufflePlacementAttemptsPerTarget; attempt++)
                {
                    Vector2 candidatePosition = RandomPointInArea(shuffleArea);
                    if (IsFarEnoughFromPositions(candidatePosition, placedPositions, minDistanceSquared))
                    {
                        chosenPosition = candidatePosition;
                        break;
                    }
                }

                SetTargetGameLocalPosition(target, chosenPosition);
                placedPositions.Add(chosenPosition);
            }
        }

        private void SpawnWaveObjects()
        {
            SpawnObstructionForWave();
            SpawnMovingTargetsForWave();
            SpawnQuantumTargetForWave();
        }

        private void ClearWaveObjects()
        {
            for (int i = activeObstructions.Count - 1; i >= 0; i--)
            {
                if (activeObstructions[i] != null)
                {
                    Destroy(activeObstructions[i]);
                }
            }

            activeObstructions.Clear();

            for (int i = activeQuantumTargets.Count - 1; i >= 0; i--)
            {
                if (activeQuantumTargets[i] != null)
                {
                    Destroy(activeQuantumTargets[i].gameObject);
                }
            }

            activeQuantumTargets.Clear();

            for (int i = activeMovingTargets.Count - 1; i >= 0; i--)
            {
                if (activeMovingTargets[i] != null)
                {
                    Destroy(activeMovingTargets[i].gameObject);
                }
            }

            activeMovingTargets.Clear();
        }

        private void SpawnObstructionForWave()
        {
            if (!spawnObstructionOnEachWave || obstacleOptions == null || obstacleOptions.Count == 0)
            {
                return;
            }

            Rect leftArea;
            Rect rightArea;
            SplitAreaLeftRight(obstructionSpawnArea, out leftArea, out rightArea);

            ObstacleSpawnOption leftOption = PickWeightedObstacleOption();
            if (leftOption == null)
            {
                return;
            }

            if (!TryChooseSpawnPoint(leftArea, obstructionMinTargetDistance, maxObstructionPlacementAttempts, out Vector2 leftPosition))
            {
                return;
            }

            float leftRotation = leftOption.RandomizeRotation ? Random.Range(0f, 180f) : 0f;
            SpawnObstacleInstance(leftOption, leftPosition, leftRotation);

            ObstacleSpawnOption rightOption = Random.value <= leftOption.SymmetryChance ? leftOption : PickWeightedObstacleOption();
            if (rightOption == null)
            {
                rightOption = leftOption;
            }

            Vector2 rightPosition = MirrorPositionX(leftPosition, obstructionSpawnArea.center.x);
            if (!rightArea.Contains(rightPosition) || !IsSpawnPointValid(rightPosition, obstructionMinTargetDistance * obstructionMinTargetDistance))
            {
                if (!TryChooseSpawnPoint(rightArea, obstructionMinTargetDistance, maxObstructionPlacementAttempts, out rightPosition))
                {
                    return;
                }
            }

            float rightRotation = rightOption == leftOption ? -leftRotation : (rightOption.RandomizeRotation ? Random.Range(0f, 180f) : 0f);
            SpawnObstacleInstance(rightOption, rightPosition, rightRotation);
        }

        private GameObject SpawnObstacleInstance(ObstacleSpawnOption option, Vector2 localPosition, float zRotation)
        {
            if (option == null || option.Prefab == null)
            {
                return null;
            }

            Transform parent = obstructionParent != null ? obstructionParent : transform;
            GameObject obstruction = Instantiate(option.Prefab, parent);
            SetObjectGameLocalPosition(obstruction.transform, localPosition);
            obstruction.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);

            foreach (AtomSmasherStaticBar staticBar in obstruction.GetComponentsInChildren<AtomSmasherStaticBar>())
            {
                staticBar.Initialize(physicsPlaneZ);
            }

            foreach (AtomSmasherMovingBar movingBar in obstruction.GetComponentsInChildren<AtomSmasherMovingBar>())
            {
                movingBar.Initialize(physicsPlaneZ);
            }

            activeObstructions.Add(obstruction);
            return obstruction;
        }

        private void SpawnMovingTargetsForWave()
        {
            if (!spawnMovingTargets || movingTargetOptions == null || movingTargetOptions.Count == 0 || movingTargetsPerWave <= 0)
            {
                return;
            }

            for (int i = 0; i < movingTargetsPerWave; i++)
            {
                MovingTargetSpawnOption option = PickWeightedMovingTargetOption();
                if (option == null || !TryChooseSpawnPoint(movingTargetSpawnArea, movingTargetMinDistance, maxMovingTargetPlacementAttempts, out Vector2 localPosition))
                {
                    continue;
                }

                Transform parent = movingTargetParent != null ? movingTargetParent : transform;
                AtomSmasherTarget movingTarget = Instantiate(option.Prefab, parent);
                SetObjectGameLocalPosition(movingTarget.transform, localPosition);
                movingTarget.AssignGame(this);
                movingTarget.ResetTarget();

                foreach (AtomSmasherMovingTarget motion in movingTarget.GetComponentsInChildren<AtomSmasherMovingTarget>())
                {
                    motion.Initialize(physicsPlaneZ);
                }

                activeMovingTargets.Add(movingTarget);
                if (movingTarget.RequiredTarget)
                {
                    requiredTargetsRemaining++;
                }
            }
        }

        private void SpawnQuantumTargetForWave()
        {
            if (!spawnQuantumTargetPerWave || quantumTargetPrefab == null)
            {
                return;
            }

            if (!TryChooseSpawnPoint(quantumSpawnArea, quantumMinTargetDistance, maxQuantumPlacementAttempts, out Vector2 localPosition))
            {
                return;
            }

            Transform parent = quantumTargetParent != null ? quantumTargetParent : transform;
            AtomSmasherTarget quantumTarget = Instantiate(quantumTargetPrefab, parent);
            SetObjectGameLocalPosition(quantumTarget.transform, localPosition);
            quantumTarget.AssignGame(this);
            quantumTarget.ResetTarget();
            activeQuantumTargets.Add(quantumTarget);
        }

        private bool TryChooseSpawnPoint(Rect area, float minDistance, int maxAttempts, out Vector2 localPosition)
        {
            NormalizeRect(ref area);
            localPosition = RandomPointInArea(area);
            float minDistanceSquared = minDistance * minDistance;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2 candidatePosition = RandomPointInArea(area);
                if (IsSpawnPointValid(candidatePosition, minDistanceSquared))
                {
                    localPosition = candidatePosition;
                    return true;
                }
            }

            return IsSpawnPointValid(localPosition, minDistanceSquared);
        }

        private bool IsSpawnPointValid(Vector2 candidatePosition, float minDistanceSquared)
        {
            if (obstructionReservedLaunchArea.Contains(candidatePosition) || obstructionReservedDrainArea.Contains(candidatePosition))
            {
                return false;
            }

            if (!IsFarEnoughFromPositions(candidatePosition, GetOccupiedGameLocalPositions(), minDistanceSquared))
            {
                return false;
            }

            return true;
        }

        private List<Vector2> GetOccupiedGameLocalPositions()
        {
            List<Vector2> occupiedPositions = new List<Vector2>(targets.Count + activeQuantumTargets.Count + activeObstructions.Count);

            foreach (AtomSmasherTarget target in targets)
            {
                if (target != null)
                {
                    occupiedPositions.Add(GetGameLocalPosition(target.transform));
                }
            }

            foreach (AtomSmasherTarget quantumTarget in activeQuantumTargets)
            {
                if (quantumTarget != null)
                {
                    occupiedPositions.Add(GetGameLocalPosition(quantumTarget.transform));
                }
            }

            foreach (AtomSmasherTarget movingTarget in activeMovingTargets)
            {
                if (movingTarget != null)
                {
                    occupiedPositions.Add(GetGameLocalPosition(movingTarget.transform));
                }
            }

            foreach (GameObject obstruction in activeObstructions)
            {
                if (obstruction != null)
                {
                    occupiedPositions.Add(GetGameLocalPosition(obstruction.transform));
                }
            }

            return occupiedPositions;
        }

        private void SetTargetGameLocalPosition(AtomSmasherTarget target, Vector2 localPosition)
        {
            SetObjectGameLocalPosition(target.transform, localPosition);
        }

        private void SetObjectGameLocalPosition(Transform targetTransform, Vector2 localPosition)
        {
            Vector3 gameLocalPosition = new Vector3(localPosition.x, localPosition.y, physicsPlaneZ);

            if (targetTransform.parent == transform)
            {
                targetTransform.localPosition = gameLocalPosition;
            }
            else
            {
                targetTransform.position = transform.TransformPoint(gameLocalPosition);
            }
        }

        private Vector2 GetGameLocalPosition(Transform targetTransform)
        {
            Vector3 localPosition = targetTransform.parent == transform
                ? targetTransform.localPosition
                : transform.InverseTransformPoint(targetTransform.position);

            return new Vector2(localPosition.x, localPosition.y);
        }

        private Vector2 RandomPointInArea(Rect area)
        {
            return new Vector2(
                Random.Range(area.xMin, area.xMax),
                Random.Range(area.yMin, area.yMax));
        }

        private ObstacleSpawnOption PickWeightedObstacleOption()
        {
            return PickWeightedOption(obstacleOptions, option => option != null && option.IsAvailable(waveNumber), option => option.SpawnWeight);
        }

        private MovingTargetSpawnOption PickWeightedMovingTargetOption()
        {
            return PickWeightedOption(movingTargetOptions, option => option != null && option.IsAvailable(waveNumber), option => option.SpawnWeight);
        }

        private QuantumEffectOption PickWeightedQuantumEffectOption()
        {
            return PickWeightedOption(quantumEffectOptions, option => option != null && option.IsAvailable(waveNumber), option => option.SpawnWeight);
        }

        private static T PickWeightedOption<T>(List<T> options, Predicate<T> isAvailable, Func<T, int> getWeight) where T : class
        {
            if (options == null || options.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            foreach (T option in options)
            {
                if (option != null && isAvailable(option))
                {
                    totalWeight += Mathf.Max(0, getWeight(option));
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            foreach (T option in options)
            {
                if (option == null || !isAvailable(option))
                {
                    continue;
                }

                roll -= Mathf.Max(0, getWeight(option));
                if (roll < 0)
                {
                    return option;
                }
            }

            return null;
        }

        private static void SplitAreaLeftRight(Rect area, out Rect leftArea, out Rect rightArea)
        {
            NormalizeRect(ref area);
            float halfWidth = area.width * 0.5f;
            leftArea = new Rect(area.xMin, area.yMin, halfWidth, area.height);
            rightArea = new Rect(area.xMin + halfWidth, area.yMin, halfWidth, area.height);
        }

        private static Vector2 MirrorPositionX(Vector2 position, float centerX)
        {
            return new Vector2(centerX - (position.x - centerX), position.y);
        }

        private static bool IsFarEnoughFromPositions(Vector2 candidatePosition, List<Vector2> placedPositions, float minDistanceSquared)
        {
            if (minDistanceSquared <= 0f)
            {
                return true;
            }

            foreach (Vector2 placedPosition in placedPositions)
            {
                if ((candidatePosition - placedPosition).sqrMagnitude < minDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private void ApplyRandomQuantumModifier(AtomSmasherBall ball, Vector3 feedbackPosition)
        {
            PlayFeedback(quantumTriggerClip, quantumTriggerParticles, feedbackPosition, quantumBoostColor);

            QuantumEffectOption effectOption = PickWeightedQuantumEffectOption();
            QuantumModifier modifier = effectOption != null ? effectOption.Modifier : ChooseFallbackQuantumModifier();
            string effectName = effectOption != null ? effectOption.DisplayName : GetDefaultQuantumEffectName(modifier);

            switch (modifier)
            {
                case QuantumModifier.SplitBall:
                    if (TrySplitBall(ball))
                    {
                        ShowStatusMessage($"Quantum: {effectName}");
                        return;
                    }

                    ApplySpeedBoost(ball);
                    ShowStatusMessage("Quantum: Speed Boost");
                    return;
                default:
                    ApplySpeedBoost(ball);
                    ShowStatusMessage("Quantum: Speed Boost");
                    return;
            }
        }

        private QuantumModifier ChooseFallbackQuantumModifier()
        {
            if (ActiveBallCount >= maxActiveBalls || ballPrefab == null)
            {
                return QuantumModifier.SpeedBoost;
            }

            return Random.value < 0.5f ? QuantumModifier.SpeedBoost : QuantumModifier.SplitBall;
        }

        private static string GetDefaultQuantumEffectName(QuantumModifier modifier)
        {
            return modifier == QuantumModifier.SplitBall ? "Split" : "Speed Boost";
        }

        private void ApplySpeedBoost(AtomSmasherBall ball)
        {
            if (ball == null)
            {
                return;
            }

            ball.TryBoostPlanarVelocity(quantumSpeedMultiplier, quantumMaxSpeed);
            ball.ApplyTemporaryVisualState(quantumBoostColor, quantumVisualSeconds);
        }

        private bool TrySplitBall(AtomSmasherBall sourceBall)
        {
            if (sourceBall == null || ballPrefab == null || ActiveBallCount >= maxActiveBalls)
            {
                return false;
            }

            Vector3 sourceVelocity = sourceBall.PlanarVelocity;
            if (sourceVelocity.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float angle = Random.value < 0.5f ? splitAngleDegrees : -splitAngleDegrees;
            Vector3 splitVelocity = Quaternion.Euler(0f, 0f, angle) * sourceVelocity * splitSpeedMultiplier;
            AtomSmasherBall splitBall = Instantiate(ballPrefab, sourceBall.transform.position, Quaternion.identity);
            RegisterBall(splitBall);
            ballScoreMultipliers[splitBall] = GetBallScoreMultiplier(sourceBall);
            splitBall.Launch(splitVelocity);
            splitBall.ApplyTemporaryVisualState(quantumBoostColor, quantumVisualSeconds);
            sourceBall.ApplyTemporaryVisualState(quantumBoostColor, quantumVisualSeconds);
            return true;
        }

        private void PlayFeedback(AudioClip clip, ParticleSystem particlesPrefab, Vector3 position, Color fallbackColor)
        {
            if (feedbackAudioSource != null && clip != null)
            {
                feedbackAudioSource.PlayOneShot(clip);
            }

            if (particlesPrefab != null)
            {
                ParticleSystem particles = Instantiate(particlesPrefab, position, Quaternion.identity);
                Destroy(particles.gameObject, defaultParticleLifetime);
                return;
            }

            Debug.LogWarning($"{name} has no Atom Smasher particle prefab assigned for feedback at {position}.", this);
        }

        private Vector3 GetBoardFeedbackPosition()
        {
            Transform origin = feedbackOrigin != null ? feedbackOrigin : transform;
            Vector3 position = origin.position;
            position.z = physicsPlaneZ;
            return position;
        }

        private void ShowStatusMessage(string message)
        {
            statusMessage = message;
            statusMessageExpiresAt = Time.unscaledTime + statusMessageSeconds;
        }

        private void TickTimer()
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
            if (timeRemaining <= 0f)
            {
                FinishGame(false);
            }
        }

        private static void NormalizeRect(ref Rect rect)
        {
            if (rect.width < 0f)
            {
                rect.x += rect.width;
                rect.width = Mathf.Abs(rect.width);
            }

            if (rect.height < 0f)
            {
                rect.y += rect.height;
                rect.height = Mathf.Abs(rect.height);
            }
        }

        private int GetBallScoreMultiplier(AtomSmasherBall ball)
        {
            if (ball != null && ballScoreMultipliers.TryGetValue(ball, out int multiplier))
            {
                return Mathf.Max(1, multiplier);
            }

            return 1;
        }

        private void AdvanceBallScoreMultiplier(AtomSmasherBall ball, int previousMultiplier)
        {
            if (ball == null)
            {
                return;
            }

            long nextMultiplier = (long)Mathf.Max(1, previousMultiplier) + Mathf.Max(1, scoreMultiplierStep);
            ballScoreMultipliers[ball] = nextMultiplier > int.MaxValue ? int.MaxValue : (int)nextMultiplier;
        }

        private int GetCurrentShotMultiplier()
        {
            PruneMissingBalls();
            if (activeBalls.Count == 0)
            {
                return 1;
            }

            return GetBallScoreMultiplier(activeBalls[0]);
        }

        private static int AddClamped(int currentScore, int points)
        {
            long nextScore = (long)currentScore + Mathf.Max(0, points);
            return nextScore > int.MaxValue ? int.MaxValue : (int)nextScore;
        }

        private static int MultiplyClamped(int value, int multiplier)
        {
            long product = (long)Mathf.Max(0, value) * Mathf.Max(1, multiplier);
            return product > int.MaxValue ? int.MaxValue : (int)product;
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
            waveClearPending = false;
            finishTime = Time.unscaledTime;

            if (!won)
            {
                PlayFeedback(failedRoundClip, failedRoundParticles, GetBoardFeedbackPosition(), new Color(1f, 0.2f, 0.2f, 1f));
            }

            RecordScore();
            Debug.Log(won ? $"{GameTitle} complete with {score} points." : $"{GameTitle} failed with {score} points.", this);
        }

        private void ResolveScoreCarrier()
        {
            if (scoreCarrier == null)
            {
                scoreCarrier = PlayerScoreCarrier.FindForPlayer();
            }

            if (scoreCarrier == null)
            {
                Debug.LogWarning($"{name} could not find a PlayerScoreCarrier on the player. {GameTitle} score will not persist.", this);
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

    }
}
