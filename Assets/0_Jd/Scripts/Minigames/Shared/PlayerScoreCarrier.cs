using System;
using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Player Score Carrier")]
    public class PlayerScoreCarrier : MonoBehaviour
    {
        private const string TicketsPlayerPrefsKey = "ArcadeProgress.Tickets";

        [SerializeField, Min(0f)] private float defaultTicketsPerPoint = 0.1f;

        public int TotalTickets => PlayerPrefs.GetInt(TicketsPlayerPrefsKey, 0);
        public float DefaultTicketsPerPoint => defaultTicketsPerPoint;

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
