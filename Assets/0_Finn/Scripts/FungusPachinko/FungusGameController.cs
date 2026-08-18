using System;
using System.Collections;
using Sol.Minigames;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Finn.Minigames
{
    public enum FungusGamePhase
    {
        Aiming,
        BallInPlay,
        GameOver
    }

    /// <summary>Summary raised once when the game ends.</summary>
    public struct FungusGameResult
    {
        public int FinalScore;
        public int TicketsAwarded;
        public bool AllLightsOut;
    }

    /// <summary>
    /// Orchestrates one game of Fungus Pachinko Ball: five balls, one point per light
    /// turned off, tickets paid 1:1 through the shared PlayerScoreCarrier, with the +50
    /// all-lights-out bonus folded into the recorded score before conversion.
    /// Lives on the FungusPachinkoMachine prefab root; every collaborator is a serialized
    /// reference wired by the builder so the machine stays one self-contained unit.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Game Controller")]
    [RequireComponent(typeof(AudioSource))]
    public class FungusGameController : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private FungusDropper dropper;
        [SerializeField] private FungusLightBank lightBank;
        [SerializeField] private FungusBall ballPrefab;

        [Header("Rules")]
        [SerializeField] private int ballsPerGame = 5;
        [SerializeField] private int allLightsBonusPoints = 50;

        [Header("Payout")]
        [SerializeField] private string minigameId = "FungusPachinko";
        [SerializeField] private float ticketsPerPoint = 1f;

        [Header("Scene Flow")]
        [SerializeField] private string returnSceneName = "Sc_ArcadeHub";
        [SerializeField] private float returnDelaySeconds = 4f;

        [Header("Audio")]
        [SerializeField] private AudioClip ballFinishedSound;
        [SerializeField] private float ballFinishedVolume = 1f;

        [SerializeField] private AudioClip gameOverSound;
        [SerializeField] private float gameOverVolume = 1f;

        [SerializeField] private AudioClip allLightsOutSound;
        [SerializeField] private float allLightsOutVolume = 1f;

        public int Score { get; private set; }
        public int BallsRemaining { get; private set; }
        public FungusGamePhase Phase { get; private set; } = FungusGamePhase.Aiming;

        public event Action<int> ScoreChanged;
        public event Action<int> BallsChanged;
        public event Action<FungusGameResult> GameEnded;

        private FungusBall activeBall;
        private AudioSource audioSource;

        private void Awake()
        {
            BallsRemaining = ballsPerGame;
            audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (dropper != null)
            {
                dropper.DropRequested += HandleDropRequested;
            }

            if (lightBank != null)
            {
                lightBank.AnyLightTurnedOff += HandleLightTurnedOff;
                lightBank.AllLightsOut += HandleAllLightsOut;
            }
        }

        private void OnDisable()
        {
            if (dropper != null)
            {
                dropper.DropRequested -= HandleDropRequested;
            }

            if (lightBank != null)
            {
                lightBank.AnyLightTurnedOff -= HandleLightTurnedOff;
                lightBank.AllLightsOut -= HandleAllLightsOut;
            }
        }

        private void Start()
        {
            if (dropper != null)
            {
                dropper.AllowInput = true;
            }

            ScoreChanged?.Invoke(Score);
            BallsChanged?.Invoke(BallsRemaining);
        }

        private void HandleDropRequested()
        {
            if (Phase != FungusGamePhase.Aiming ||
                BallsRemaining <= 0 ||
                ballPrefab == null)
            {
                return;
            }

            BallsRemaining--;
            BallsChanged?.Invoke(BallsRemaining);

            activeBall = Instantiate(
                ballPrefab,
                dropper.BallSpawnPoint.position,
                Quaternion.identity,
                transform
            );

            activeBall.Finished += HandleBallFinished;

            Phase = FungusGamePhase.BallInPlay;
            dropper.AllowInput = false;
        }

        private void HandleLightTurnedOff(FungusLight boardLight)
        {
            if (Phase == FungusGamePhase.GameOver)
            {
                return;
            }

            Score++;
            ScoreChanged?.Invoke(Score);

            DamagePopup.SpawnText(
                boardLight.transform.position,
                "+1",
                new Color(1f, 0.9f, 0.3f)
            );
        }

        private void HandleAllLightsOut()
        {
            if (Phase == FungusGamePhase.GameOver)
            {
                return;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            // Play the special victory sound immediately when
            // the final light is collected.
            if (allLightsOutSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(
                    allLightsOutSound,
                    allLightsOutVolume
                );
            }

            EndGame();
        }

        private void HandleBallFinished(FungusBall ball)
        {
            if (activeBall == ball)
            {
                activeBall = null;
            }

            // This is a normal ball finish, so play the ball-finished sound.
            DestroyBall(ball, true);

            if (Phase == FungusGamePhase.GameOver)
            {
                return;
            }

            if (BallsRemaining > 0)
            {
                Phase = FungusGamePhase.Aiming;

                if (dropper != null)
                {
                    dropper.AllowInput = true;
                }
            }
            else
            {
                EndGame();
            }
        }

        /// <summary>
        /// Destroys the ball.
        /// playFinishSound controls whether the normal ball-finished sound plays.
        /// </summary>
        private void DestroyBall(FungusBall ball, bool playFinishSound)
        {
            if (ball == null)
            {
                return;
            }

            if (playFinishSound &&
                ballFinishedSound != null &&
                audioSource != null)
            {
                audioSource.PlayOneShot(
                    ballFinishedSound,
                    ballFinishedVolume
                );
            }

            ball.Finished -= HandleBallFinished;

            Destroy(ball.gameObject);
        }

        private void EndGame()
        {
            Phase = FungusGamePhase.GameOver;

            if (dropper != null)
            {
                dropper.AllowInput = false;
            }

            bool allOut = lightBank != null && lightBank.AllOut;

            // If all lights were collected, the special victory sound
            // has already played. Destroy the active ball silently so
            // we don't immediately play the normal ball-finished sound.
            if (activeBall != null)
            {
                DestroyBall(activeBall, !allOut);
                activeBall = null;
            }

            int finalScore = Score +
                             (allOut ? allLightsBonusPoints : 0);

            int ticketsAwarded;

            PlayerScoreCarrier carrier = PlayerScoreCarrier.FindForPlayer();

            if (carrier != null)
            {
                ticketsAwarded = carrier
                    .RecordScore(
                        minigameId,
                        finalScore,
                        ticketsPerPoint
                    )
                    .TicketsAwarded;
            }
            else
            {
                Debug.LogWarning(
                    "FungusGameController: no PlayerScoreCarrier found; " +
                    "tickets were not persisted.",
                    this
                );

                ticketsAwarded = Mathf.FloorToInt(
                    finalScore *
                    Mathf.Max(0f, ticketsPerPoint)
                );
            }

            // Only play the normal game-over sound if this was NOT
            // a perfect clear.
            if (!allOut &&
                gameOverSound != null &&
                audioSource != null)
            {
                audioSource.PlayOneShot(
                    gameOverSound,
                    gameOverVolume
                );
            }

            GameEnded?.Invoke(new FungusGameResult
            {
                FinalScore = finalScore,
                TicketsAwarded = ticketsAwarded,
                AllLightsOut = allOut
            });

            StartCoroutine(ReturnToHubAfterDelay());
        }

        private IEnumerator ReturnToHubAfterDelay()
        {
            yield return new WaitForSeconds(returnDelaySeconds);

            if (!string.IsNullOrEmpty(returnSceneName) &&
                Application.CanStreamedLevelBeLoaded(returnSceneName))
            {
                SceneManager.LoadScene(
                    returnSceneName,
                    LoadSceneMode.Single
                );
            }
            else
            {
                Debug.LogWarning(
                    $"FungusGameController: return scene '{returnSceneName}' " +
                    "is not loadable; staying in the minigame scene.",
                    this
                );
            }
        }
    }
}