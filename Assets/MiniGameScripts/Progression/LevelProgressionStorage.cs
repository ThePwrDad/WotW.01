using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeightLifter
{
    // Shared persistence layer for leaderboard and level unlocks.
    // Uses PlayerPrefs so data survives between play sessions.
    public static class LevelProgressionStorage
    {
        private const int DefaultLeaderboardSize = 10;

        private static string SeedKey(string levelId) => $"WL_Seeded_{levelId}";
        private static string LeaderboardKey(string levelId, int rankIndex) => $"WL_LB_{levelId}_{rankIndex}";
        private static string UnlockKey(string sceneName) => $"WL_Unlocked_{sceneName}";

        // Seeds the leaderboard one time per level so initial target scores are pre-populated.
        public static void EnsureLeaderboardSeeded(string levelId, int[] preloadedTargets)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return;
            if (PlayerPrefs.GetInt(SeedKey(levelId), 0) == 1) return;

            int[] safeTargets = BuildSafeTargets(preloadedTargets, DefaultLeaderboardSize);
            for (int i = 0; i < DefaultLeaderboardSize; i++)
            {
                PlayerPrefs.SetInt(LeaderboardKey(levelId, i), safeTargets[i]);
            }

            PlayerPrefs.SetInt(SeedKey(levelId), 1);
            PlayerPrefs.Save();
        }

        // Returns high-to-low scores (1 = best).
        public static List<int> GetLeaderboard(string levelId)
        {
            List<int> scores = new List<int>(DefaultLeaderboardSize);
            if (string.IsNullOrWhiteSpace(levelId)) return scores;

            for (int i = 0; i < DefaultLeaderboardSize; i++)
            {
                scores.Add(PlayerPrefs.GetInt(LeaderboardKey(levelId, i), 0));
            }

            scores.Sort((a, b) => b.CompareTo(a));
            return scores;
        }

        // Inserts a new score, re-sorts, trims to top N, and persists the result.
        public static List<int> SubmitScore(string levelId, int score, int maxEntries = DefaultLeaderboardSize)
        {
            List<int> scores = GetLeaderboard(levelId);
            scores.Add(Mathf.Max(0, score));
            scores.Sort((a, b) => b.CompareTo(a));

            int keepCount = Mathf.Clamp(maxEntries, 1, DefaultLeaderboardSize);
            if (scores.Count > keepCount)
            {
                scores.RemoveRange(keepCount, scores.Count - keepCount);
            }

            for (int i = 0; i < keepCount; i++)
            {
                PlayerPrefs.SetInt(LeaderboardKey(levelId, i), scores[i]);
            }

            PlayerPrefs.Save();
            return scores;
        }

        // Stores unlocked state for the next level when Bronze or better is achieved.
        public static void UnlockScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;
            PlayerPrefs.SetInt(UnlockKey(sceneName), 1);
            PlayerPrefs.Save();
        }

        // Checks if a level is unlocked, with an optional default state for first level/menu flows.
        public static bool IsSceneUnlocked(string sceneName, bool defaultUnlocked = false)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return defaultUnlocked;
            return PlayerPrefs.GetInt(UnlockKey(sceneName), defaultUnlocked ? 1 : 0) == 1;
        }

        // Normalizes config input to a valid top-10 descending list.
        private static int[] BuildSafeTargets(int[] input, int count)
        {
            int[] output = new int[count];
            int fallback = 0;

            for (int i = 0; i < count; i++)
            {
                int value = (input != null && i < input.Length) ? Mathf.Max(0, input[i]) : fallback;
                output[i] = value;
                fallback = value;
            }

            Array.Sort(output, (a, b) => b.CompareTo(a));
            return output;
        }
    }
}
