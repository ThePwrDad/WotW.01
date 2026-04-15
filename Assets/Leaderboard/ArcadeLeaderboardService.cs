using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WotW2.Leaderboard
{
    [Serializable]
    public class LeaderboardEntryData
    {
        public string playerName;
        public float score;

        public LeaderboardEntryData(string playerName, float score)
        {
            this.playerName = playerName;
            this.score = score;
        }
    }

    [Serializable]
    public class LevelLeaderboardData
    {
        public string levelKey;
        public List<LeaderboardEntryData> entries = new List<LeaderboardEntryData>();
    }

    [Serializable]
    public class LeaderboardStorageData
    {
        public List<LevelLeaderboardData> boards = new List<LevelLeaderboardData>();
    }

    public static class ArcadeLeaderboardService
    {
        private const string SaveKey = "WOTW2_LEADERBOARD_DATA";
        public const int BoardSize = 5;

        private static readonly string[] SeedNames =
        {
            "Atlas",
            "Titan",
            "Valkyrie",
            "Bolt",
            "Nova"
        };

        private static readonly float[] SeedScores = { 250f, 200f, 150f, 100f, 50f };

        private static LeaderboardStorageData _cached;

        public static IReadOnlyList<LeaderboardEntryData> GetBoard(string levelKey)
        {
            EnsureLoaded();
            LevelLeaderboardData board = GetOrCreateBoard(levelKey);
            return board.entries;
        }

        public static bool TryOverwriteHighestBeaten(string levelKey, float score, string playerName, out int rankIndex)
        {
            EnsureLoaded();
            LevelLeaderboardData board = GetOrCreateBoard(levelKey);
            rankIndex = FindHighestBeatenIndex(board.entries, score);
            if (rankIndex < 0)
            {
                return false;
            }

            string normalizedName = NormalizeName(playerName);
            board.entries[rankIndex] = new LeaderboardEntryData(normalizedName, score);
            Save();
            return true;
        }

        public static string FormatBoardLines(string levelKey)
        {
            IReadOnlyList<LeaderboardEntryData> board = GetBoard(levelKey);
            var sb = new StringBuilder();
            for (int i = 0; i < board.Count; i++)
            {
                LeaderboardEntryData entry = board[i];
                sb.Append(i + 1);
                sb.Append(". ");
                sb.Append(entry.playerName);
                sb.Append(" - ");
                sb.Append(entry.score.ToString("F1"));
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        public static string NormalizeName(string inputName)
        {
            if (string.IsNullOrWhiteSpace(inputName))
            {
                return "Rookie";
            }

            string trimmed = inputName.Trim();
            if (trimmed.Length > 18)
            {
                trimmed = trimmed.Substring(0, 18);
            }

            return trimmed;
        }

        public static string BuildDefaultLevelKeyFromScene(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? "Unknown Level" : sceneName.Trim();
        }

        private static void EnsureLoaded()
        {
            if (_cached != null)
            {
                return;
            }

            if (!PlayerPrefs.HasKey(SaveKey))
            {
                _cached = new LeaderboardStorageData();
                return;
            }

            string raw = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                _cached = new LeaderboardStorageData();
                return;
            }

            try
            {
                _cached = JsonUtility.FromJson<LeaderboardStorageData>(raw);
                if (_cached == null || _cached.boards == null)
                {
                    _cached = new LeaderboardStorageData();
                }
            }
            catch (Exception)
            {
                _cached = new LeaderboardStorageData();
            }
        }

        private static LevelLeaderboardData GetOrCreateBoard(string levelKey)
        {
            string key = string.IsNullOrWhiteSpace(levelKey) ? "Unknown Level" : levelKey.Trim();
            for (int i = 0; i < _cached.boards.Count; i++)
            {
                if (string.Equals(_cached.boards[i].levelKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    EnsureBoardShape(_cached.boards[i]);
                    return _cached.boards[i];
                }
            }

            var created = new LevelLeaderboardData { levelKey = key };
            SeedBoard(created);
            _cached.boards.Add(created);
            Save();
            return created;
        }

        private static void EnsureBoardShape(LevelLeaderboardData board)
        {
            if (board.entries == null)
            {
                board.entries = new List<LeaderboardEntryData>();
            }

            if (board.entries.Count == BoardSize)
            {
                return;
            }

            while (board.entries.Count < BoardSize)
            {
                int idx = board.entries.Count;
                board.entries.Add(new LeaderboardEntryData(SeedNames[idx], SeedScores[idx]));
            }

            if (board.entries.Count > BoardSize)
            {
                board.entries.RemoveRange(BoardSize, board.entries.Count - BoardSize);
            }

            Save();
        }

        private static void SeedBoard(LevelLeaderboardData board)
        {
            board.entries.Clear();
            for (int i = 0; i < BoardSize; i++)
            {
                board.entries.Add(new LeaderboardEntryData(SeedNames[i], SeedScores[i]));
            }
        }

        private static int FindHighestBeatenIndex(List<LeaderboardEntryData> entries, float score)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (score > entries[i].score)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void Save()
        {
            string raw = JsonUtility.ToJson(_cached);
            PlayerPrefs.SetString(SaveKey, raw);
            PlayerPrefs.Save();
        }
    }
}
