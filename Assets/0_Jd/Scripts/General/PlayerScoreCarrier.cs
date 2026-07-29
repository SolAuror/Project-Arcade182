using System;
using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Player Score Carrier")]
    public class PlayerScoreCarrier : MonoBehaviour
    {
        private const string TicketsPlayerPrefsKey = "ArcadeProgress.Tickets";
        private const string GoldenCoinPlayerPrefsKey = "ArcadeProgress.GoldenCoin";
        private const string GameBeatenPlayerPrefsKey = "ArcadeProgress.GameBeaten";

        private static PlayerScoreCarrier persistentInstance;

        [SerializeField, Min(0f)] private float defaultTicketsPerPoint = 0.1f;

        [Tooltip("Keep this carrier alive across scene loads (standalone ScoreManager object). Leave off for the player-mounted copy.")]
        [SerializeField] private bool persistAcrossScenes;

        private void Awake()
        {
            if (!persistAcrossScenes)
            {
                return;
            }

            if (persistentInstance != null && persistentInstance != this)
            {
                Destroy(gameObject); // a persistent carrier already made it here
                return;
            }

            persistentInstance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        public int TotalTickets => PlayerPrefs.GetInt(TicketsPlayerPrefsKey, 0);
        public float DefaultTicketsPerPoint => defaultTicketsPerPoint;
        public bool HasGoldenCoin => PlayerPrefs.GetInt(GoldenCoinPlayerPrefsKey, 0) == 1;

        public static bool GameBeaten => PlayerPrefs.GetInt(GameBeatenPlayerPrefsKey, 0) == 1;

        /// <summary>True once the minigame has been finished at least once (unlocks direct play).</summary>
        public static bool IsMinigameCompleted(string minigameId)
        {
            return PlayerPrefs.GetInt(GetCompletedKey(NormalizeMinigameId(minigameId)), 0) == 1;
        }

        /// <summary>Deducts tickets if the balance covers the price. False leaves the balance untouched.</summary>
        public bool TrySpendTickets(int amount)
        {
            amount = Mathf.Max(0, amount);
            int balance = TotalTickets;
            if (balance < amount)
            {
                return false;
            }

            PlayerPrefs.SetInt(TicketsPlayerPrefsKey, balance - amount);
            PlayerPrefs.Save();
            return true;
        }

        public void GrantGoldenCoin()
        {
            PlayerPrefs.SetInt(GoldenCoinPlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Consumes the golden coin at the exit door and marks the game beaten.</summary>
        public void RedeemGoldenCoin()
        {
            PlayerPrefs.SetInt(GoldenCoinPlayerPrefsKey, 0);
            PlayerPrefs.SetInt(GameBeatenPlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        public ScoreRecord RecordScore(
            string minigameId,
            int score,
            float ticketsPerPoint = -1f,
            string legacyLastScoreKey = null,
            string legacyBestScoreKey = null)
        {
            string normalizedId = NormalizeMinigameId(minigameId);
            string lastScoreKey = GetLastScoreKey(normalizedId);
            string bestScoreKey = GetBestScoreKey(normalizedId);

            MigrateLegacyScore(lastScoreKey, legacyLastScoreKey);
            MigrateLegacyScore(bestScoreKey, legacyBestScoreKey);

            int clampedScore = Mathf.Max(0, score);
            int previousBest = PlayerPrefs.GetInt(bestScoreKey, 0);
            int bestScore = Mathf.Max(previousBest, clampedScore);
            int ticketsAwarded = ConvertScoreToTickets(clampedScore, ticketsPerPoint);
            int totalTickets = AddClamped(TotalTickets, ticketsAwarded);

            PlayerPrefs.SetInt(lastScoreKey, clampedScore);
            PlayerPrefs.SetInt(bestScoreKey, bestScore);
            PlayerPrefs.SetInt(TicketsPlayerPrefsKey, totalTickets);
            PlayerPrefs.SetInt(GetCompletedKey(normalizedId), 1); // unlocks direct play from the main menu
            PlayerPrefs.Save();

            return new ScoreRecord(normalizedId, clampedScore, bestScore, ticketsAwarded, totalTickets);
        }

        public ScoreRecord ReadScore(string minigameId, string legacyLastScoreKey = null, string legacyBestScoreKey = null)
        {
            string normalizedId = NormalizeMinigameId(minigameId);
            string lastScoreKey = GetLastScoreKey(normalizedId);
            string bestScoreKey = GetBestScoreKey(normalizedId);

            MigrateLegacyScore(lastScoreKey, legacyLastScoreKey);
            MigrateLegacyScore(bestScoreKey, legacyBestScoreKey);

            return new ScoreRecord(
                normalizedId,
                PlayerPrefs.GetInt(lastScoreKey, 0),
                PlayerPrefs.GetInt(bestScoreKey, 0),
                0,
                TotalTickets);
        }

        public static PlayerScoreCarrier FindForPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.TryGetComponent(out PlayerScoreCarrier carrier))
            {
                return carrier;
            }

            return FindFirstObjectByType<PlayerScoreCarrier>();
        }

        private int ConvertScoreToTickets(int score, float ticketsPerPoint)
        {
            float conversionRate = ticketsPerPoint >= 0f ? ticketsPerPoint : defaultTicketsPerPoint;
            return Mathf.Max(0, Mathf.FloorToInt(score * conversionRate));
        }

        private static void MigrateLegacyScore(string targetKey, string legacyKey)
        {
            if (string.IsNullOrWhiteSpace(targetKey) ||
                PlayerPrefs.HasKey(targetKey) ||
                string.IsNullOrWhiteSpace(legacyKey) ||
                !PlayerPrefs.HasKey(legacyKey))
            {
                return;
            }

            PlayerPrefs.SetInt(targetKey, PlayerPrefs.GetInt(legacyKey, 0));
        }

        private static string NormalizeMinigameId(string minigameId)
        {
            return string.IsNullOrWhiteSpace(minigameId) ? "UnknownMinigame" : minigameId.Trim();
        }

        private static string GetLastScoreKey(string minigameId)
        {
            return $"ArcadeProgress.{minigameId}.LastScore";
        }

        private static string GetBestScoreKey(string minigameId)
        {
            return $"ArcadeProgress.{minigameId}.BestScore";
        }

        private static string GetCompletedKey(string minigameId)
        {
            return $"ArcadeProgress.{minigameId}.Completed";
        }

        private static int AddClamped(int left, int right)
        {
            long sum = (long)Mathf.Max(0, left) + Mathf.Max(0, right);
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        [Serializable]
        public readonly struct ScoreRecord
        {
            public ScoreRecord(string minigameId, int lastScore, int bestScore, int ticketsAwarded, int totalTickets)
            {
                MinigameId = minigameId;
                LastScore = lastScore;
                BestScore = bestScore;
                TicketsAwarded = ticketsAwarded;
                TotalTickets = totalTickets;
            }

            public string MinigameId { get; }
            public int LastScore { get; }
            public int BestScore { get; }
            public int TicketsAwarded { get; }
            public int TotalTickets { get; }
        }
    }
}
