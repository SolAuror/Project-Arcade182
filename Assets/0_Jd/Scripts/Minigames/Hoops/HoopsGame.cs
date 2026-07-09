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
        [SerializeField] private float roundSeconds = 60f;
        [SerializeField] private bool startOnAwake = true;
        [SerializeField] private string minigameId = "Hoops";
        [SerializeField] private float ticketsPerPoint = 1f;
        [SerializeField] private PlayerScoreCarrier scoreCarrier;
        [SerializeField] private bool onlyOneActiveHoop = true;

        [Header("Hoops")]
        [SerializeField] private List<HoopsScoreZone> hoops = new List<HoopsScoreZone>();
        [SerializeField] private List<HoopsThrowable> throwables = new List<HoopsThrowable>();
        [SerializeField] private List<HoopsScorable> scorables = new List<HoopsScorable>();

        [Header("Arena Bounds")]
        [Tooltip("World-space box around the court; balls and props outside it return to spawn after the grace period. Not enforced on held objects.")]
        [SerializeField] private Vector3 arenaCenter = new Vector3(0f, 6f, 6f);

        [SerializeField] private Vector3 arenaSize = new Vector3(36f, 24f, 32f);

        [Tooltip("Seconds an object may stay outside the arena before being returned.")]
        [SerializeField, Min(0.5f)] private float outOfBoundsReturnSeconds = 2f;

        [Header("Streak")]
        [Tooltip("Seconds after a score before the streak cools off.")]
        [SerializeField, Min(1f)] private float streakWindowSeconds = 10f;

        [Tooltip("Consecutive scores needed per extra multiplier step.")]
        [SerializeField, Min(1)] private int scoresPerMultiplierStep = 2;

        [SerializeField, Min(1)] private int maxStreakMultiplier = 4;

        [Header("Hoop Stages")]
        [Tooltip("Goals before active hoops start sliding on one axis.")]
        [SerializeField, Min(0)] private int slideAtGoals = 3;

        [Tooltip("Goals before active hoops slide on both axes at once.")]
        [SerializeField, Min(0)] private int dualSlideAtGoals = 6;

        [Header("Winged Hoop")]
        [Tooltip("Goals before winged hoops may appear.")]
        [SerializeField, Min(0)] private int wingedFromGoals = 6;

        [Tooltip("Chance the next active hoop takes flight off its pole as a winged bonus hoop.")]
        [SerializeField, Range(0f, 1f)] private float wingedHoopChance = 0.2f;

        [Tooltip("Point multiplier for scoring on a winged hoop.")]
        [SerializeField, Min(1)] private int wingedBonusMultiplier = 2;

        [Header("Audio")]
        [Tooltip("2D source for game feedback. Auto-added when missing; assign clips to enable each cue.")]
        [SerializeField] private AudioSource feedbackAudioSource;

        [SerializeField] private AudioClip scoreClip;

        [Tooltip("Extra pitch per streak count so hot streaks ring higher.")]
        [SerializeField, Range(0f, 0.5f)] private float scorePitchPerStreak = 0.05f;

        [Tooltip("Played instead of Score Clip on winged hoops. Falls back to Score Clip.")]
        [SerializeField] private AudioClip wingedScoreClip;

        [Tooltip("Soft cue when a new hoop becomes the target.")]
        [SerializeField] private AudioClip hoopActivateClip;

        [Tooltip("Tick once per second during the final five seconds.")]
        [SerializeField] private AudioClip countdownTickClip;

        [SerializeField] private AudioClip roundEndClip;

        [Header("Feel")]
        [Tooltip("Show floating point numbers at the hoop when a shot scores.")]
        [SerializeField] private bool showScorePopups = true;

        [SerializeField] private Color popupBaseColor = new Color(1f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color popupHotColor = new Color(1f, 0.35f, 0.2f, 1f);

        [Header("Scene Flow")]
        [SerializeField] private bool returnToSceneOnFinish = true;
        [SerializeField] private string returnSceneName = "Sc_ArcadeHub";
        [SerializeField] private float returnDelaySeconds = 2f;

        private float remainingSeconds;
        private float finishTime;
        private int score;
        private int goalsScored;
        private int streakCount;
        private float lastStreakScoreTime = -999f;
        private int bestRecordedScore;
        private int ticketsAwarded;
        private int totalTickets;
        private bool isRunning;
        private bool isComplete;
        private bool hasFailed;
        private HoopsScoreZone activeHoop;
        private int lastCountdownTickSecond = -1;
        private readonly Dictionary<Component, float> outOfBoundsSince = new Dictionary<Component, float>();
        private InputSystem_Actions inputActions;
        private InputActionMap hoopsInputMap;
        private InputAction resetBallAction;

        public int Score => score;
        public float RemainingSeconds => remainingSeconds;
        public HoopsScoreZone ActiveHoop => activeHoop;
        public int GoalsScored => goalsScored;
        public int CurrentMovementStage =>
            goalsScored >= dualSlideAtGoals ? 2 : goalsScored >= slideAtGoals ? 1 : 0;
        public bool IsRunning => isRunning;
        public bool IsComplete => isComplete;
        public bool HasFailed => hasFailed;
        public int BestRecordedScore => bestRecordedScore;
        public int TicketsAwarded => ticketsAwarded;
        public int TotalTickets => totalTickets;
        public int StreakCount => streakCount;
        public int MaxStreakMultiplier => maxStreakMultiplier;
        public int CurrentStreakMultiplier =>
            Mathf.Clamp(1 + streakCount / Mathf.Max(1, scoresPerMultiplierStep), 1, maxStreakMultiplier);
        public float StreakWindowRemainingNormalized =>
            streakCount > 0
                ? Mathf.Clamp01(1f - (Time.time - lastStreakScoreTime) / Mathf.Max(0.01f, streakWindowSeconds))
                : 0f;

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

            if (scorables.Count == 0)
            {
                scorables.AddRange(FindObjectsByType<HoopsScorable>(FindObjectsSortMode.None));
            }

            foreach (HoopsScoreZone hoop in hoops)
            {
                if (hoop != null)
                {
                    hoop.AssignGame(this);
                }
            }

            EnsureAudioSource();
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
                FinishGame(true); // no score cap: the round simply ends at time
                return;
            }

            TickCountdownAudio();
            TickOutOfBounds();

            if (streakCount > 0 && Time.time - lastStreakScoreTime > streakWindowSeconds)
            {
                streakCount = 0; // streak cooled off
            }
        }

        // Anything that escapes the court (balls launched over the fence,
        // props knocked into the void) walks back to its spawn after a grace
        // period — unless the player is carrying it.
        private void TickOutOfBounds()
        {
            Bounds arena = new Bounds(arenaCenter, arenaSize);

            foreach (HoopsThrowable throwable in throwables)
            {
                if (throwable != null && TrackOutOfBounds(throwable, arena))
                {
                    throwable.ResetToSpawn();
                }
            }

            foreach (HoopsScorable scorable in scorables)
            {
                if (scorable != null && TrackOutOfBounds(scorable, arena))
                {
                    scorable.ResetToSpawn();
                }
            }
        }

        private bool TrackOutOfBounds(Component item, Bounds arena)
        {
            bool held = Sol.Grab.GrabManager.Instance != null &&
                        Sol.Grab.GrabManager.Instance.HeldObject != null &&
                        Sol.Grab.GrabManager.Instance.HeldObject.transform.IsChildOf(item.transform);

            if (held || arena.Contains(item.transform.position))
            {
                outOfBoundsSince.Remove(item);
                return false;
            }

            if (!outOfBoundsSince.TryGetValue(item, out float since))
            {
                outOfBoundsSince[item] = Time.time;
                return false;
            }

            if (Time.time - since < outOfBoundsReturnSeconds)
            {
                return false;
            }

            outOfBoundsSince.Remove(item);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireCube(arenaCenter, arenaSize);
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
            ticketsPerPoint = Mathf.Max(0f, ticketsPerPoint);
            returnDelaySeconds = Mathf.Max(0f, returnDelaySeconds);
        }

        public void StartGame()
        {
            remainingSeconds = Mathf.Max(0f, roundSeconds);
            score = 0;
            goalsScored = 0;
            ticketsAwarded = 0;
            finishTime = 0f;
            streakCount = 0;
            lastStreakScoreTime = -999f;
            lastCountdownTickSecond = -1;
            isRunning = true;
            isComplete = false;
            hasFailed = false;
            PickNextHoop(null);
        }

        public void RegisterScore(HoopsScoreZone hoop, HoopsThrowable throwable, HoopsScorable scorable = null)
        {
            if (!isRunning || hoop == null)
            {
                return;
            }

            if (onlyOneActiveHoop && hoop != activeHoop)
            {
                return;
            }

            streakCount = Time.time - lastStreakScoreTime <= streakWindowSeconds ? streakCount + 1 : 1;
            lastStreakScoreTime = Time.time;

            // Item value x hoop difficulty (authored size points x movement
            // stage it was scored at), then winged bonus and streak on top.
            int itemPoints = scorable != null
                ? Mathf.Max(1, scorable.Points)
                : Mathf.Max(1, throwable != null ? throwable.Points : 1);
            int hoopDifficulty = Mathf.Max(1, hoop.Points) * Mathf.Max(1, hoop.StageMultiplier);
            if (hoop.IsWinged)
            {
                hoopDifficulty *= Mathf.Max(1, wingedBonusMultiplier);
            }

            int earned = itemPoints * hoopDifficulty * CurrentStreakMultiplier;
            score += earned;
            goalsScored++;

            hoop.PlayScoreFeedback();
            AudioClip clip = hoop.IsWinged && wingedScoreClip != null ? wingedScoreClip : scoreClip;
            PlayClip(clip, 1f + scorePitchPerStreak * Mathf.Max(0, streakCount - 1));

            if (showScorePopups)
            {
                float heat = Mathf.Clamp01((CurrentStreakMultiplier - 1f) / Mathf.Max(1f, maxStreakMultiplier - 1f));
                DamagePopup.Spawn(hoop.transform.position + Vector3.up * 0.4f, earned, Color.Lerp(popupBaseColor, popupHotColor, heat));
            }

            throwable?.MarkScored();
            scorable?.MarkScored();
            Debug.Log($"Hoops score: {score} (goal {goalsScored}, item {itemPoints} x hoop {hoopDifficulty}, streak x{CurrentStreakMultiplier})", hoop);

            PickNextHoop(hoop);
        }

        private void PickNextHoop(HoopsScoreZone previousHoop)
        {
            if (!onlyOneActiveHoop || hoops.Count == 0)
            {
                foreach (HoopsScoreZone hoop in hoops)
                {
                    hoop?.SetActiveTarget(true, CurrentMovementStage, false);
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

            int stage = CurrentMovementStage;
            bool winged = goalsScored >= wingedFromGoals && Random.value < wingedHoopChance;
            foreach (HoopsScoreZone hoop in validHoops)
            {
                hoop.SetActiveTarget(hoop == activeHoop, stage, winged && hoop == activeHoop);
            }

            PlayClip(hoopActivateClip);
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
            PlayClip(roundEndClip);
            RecordScore();
            Debug.Log(won ? $"Hoops complete with {score} points." : $"Hoops failed with {score} points.", this);
        }

        private void EnsureAudioSource()
        {
            if (feedbackAudioSource == null && !TryGetComponent(out feedbackAudioSource))
            {
                feedbackAudioSource = gameObject.AddComponent<AudioSource>();
            }

            feedbackAudioSource.playOnAwake = false;
            feedbackAudioSource.spatialBlend = 0f; // 2D game feedback
        }

        private void PlayClip(AudioClip clip, float pitch = 1f)
        {
            if (feedbackAudioSource == null || clip == null)
            {
                return;
            }

            feedbackAudioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            feedbackAudioSource.PlayOneShot(clip);
        }

        private void TickCountdownAudio()
        {
            if (countdownTickClip == null || remainingSeconds > 5f)
            {
                return;
            }

            int second = Mathf.CeilToInt(remainingSeconds);
            if (second != lastCountdownTickSecond)
            {
                lastCountdownTickSecond = second;
                PlayClip(countdownTickClip, 1f + (5 - second) * 0.05f); // ticks rise as time runs out
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

    }
}
