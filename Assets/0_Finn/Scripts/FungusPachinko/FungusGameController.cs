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

        public int Score { get; private set; }
        public int BallsRemaining { get; private set; }
        public FungusGamePhase Phase { get; private set; } = FungusGamePhase.Aiming;

        public event Action<int> ScoreChanged;
        public event Action<int> BallsChanged;
        public event Action<FungusGameResult> GameEnded;

        private FungusBall activeBall;

        private void Awake()
        {
            BallsRemaining = ballsPerGame;
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
            if (Phase != FungusGamePhase.Aiming || BallsRemaining <= 0 || ballPrefab == null)
            {
                return;
            }

            BallsRemaining--;
            BallsChanged?.Invoke(BallsRemaining);

            activeBall = Instantiate(ballPrefab, dropper.BallSpawnPoint.position, Quaternion.identity, transform);
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
            DamagePopup.SpawnText(boardLight.transform.position, "+1", new Color(1f, 0.9f, 0.3f));
        }

        private void HandleAllLightsOut()
        {
            // Perfect clear ends the game on the spot — nothing left on the board to score.
            if (Phase != FungusGamePhase.GameOver)
            {
                EndGame();
            }
        }

        private void HandleBallFinished(FungusBall ball)
        {
            ball.Finished -= HandleBallFinished;
            if (activeBall == ball)
            {
                activeBall = null;
            }

            Destroy(ball.gameObject);

            if (Phase == FungusGamePhase.GameOver)
            {
                return;
            }

            if (BallsRemaining > 0)
            {
                Phase = FungusGamePhase.Aiming;
                dropper.AllowInput = true;
            }
            else
            {
                EndGame();
            }
        }

        private void EndGame()
        {
            Phase = FungusGamePhase.GameOver;
            if (dropper != null)
            {
                dropper.AllowInput = false;
            }

            if (activeBall != null)
            {
                activeBall.Finished -= HandleBallFinished;
                Destroy(activeBall.gameObject);
                activeBall = null;
            }

            bool allOut = lightBank != null && lightBank.AllOut;
            int finalScore = Score + (allOut ? allLightsBonusPoints : 0);

            int ticketsAwarded;
            PlayerScoreCarrier carrier = PlayerScoreCarrier.FindForPlayer();
            if (carrier != null)
            {
                ticketsAwarded = carrier.RecordScore(minigameId, finalScore, ticketsPerPoint).TicketsAwarded;
            }
            else
            {
                Debug.LogWarning("FungusGameController: no PlayerScoreCarrier found; tickets were not persisted.", this);
                ticketsAwarded = Mathf.FloorToInt(finalScore * Mathf.Max(0f, ticketsPerPoint));
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

            if (!string.IsNullOrEmpty(returnSceneName) && Application.CanStreamedLevelBeLoaded(returnSceneName))
            {
                SceneManager.LoadScene(returnSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning(
                    $"FungusGameController: return scene '{returnSceneName}' is not loadable; staying in the minigame scene.",
                    this);
            }
        }
    }
}
