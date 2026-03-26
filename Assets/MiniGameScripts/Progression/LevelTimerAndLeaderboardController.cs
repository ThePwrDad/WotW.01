using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace WeightLifter
{
    // Level runtime coordinator:
    // - counts down the timer
    // - submits final score at timeout
    // - updates leaderboard UI
    // - unlocks the next scene when Bronze threshold is met
    public class LevelTimerAndLeaderboardController : MonoBehaviour
    {
        [Header("Config")]
        public LevelProgressionConfig progressionConfig;

        [Header("Runtime References")]
        public PlayerStats playerStats;

        [Header("UI")]
        public TMP_Text timerText;
        public TMP_Text scoreToWinText;
        public TMP_Text leaderboardText;
        public TMP_Text levelResultText;

        private float _timeRemaining;
        private bool _completed;

        private void Start()
        {
            if (playerStats == null)
            {
                playerStats = FindFirstObjectByType<PlayerStats>();
            }

            if (progressionConfig == null)
            {
                Debug.LogError("LevelTimerAndLeaderboardController: progressionConfig is not assigned.");
                enabled = false;
                return;
            }

            _timeRemaining = Mathf.Max(1f, progressionConfig.levelDurationSeconds);

            // Seed preloaded target scores once, then reuse persisted leaderboard runs.
            LevelProgressionStorage.EnsureLeaderboardSeeded(progressionConfig.levelId, progressionConfig.preloadedTargets);

            UpdateStaticUI();
            UpdateTimerUI();
            UpdateLeaderboardUI(LevelProgressionStorage.GetLeaderboard(progressionConfig.levelId));
        }

        private void Update()
        {
            if (_completed) return;

            // Simple level timer: when it hits zero, we lock completion to a single submit.
            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                CompleteLevel();
            }

            UpdateTimerUI();
        }

        private void CompleteLevel()
        {
            _completed = true;

            // Score source is current strength so it matches existing Swol progression.
            int finalScore = playerStats != null ? Mathf.RoundToInt(playerStats.currentStrength) : 0;
            List<int> updatedScores = LevelProgressionStorage.SubmitScore(progressionConfig.levelId, finalScore, 10);
            int bronzeTarget = progressionConfig.GetBronzeTarget();
            bool unlockedNext = finalScore >= bronzeTarget;

            if (unlockedNext)
            {
                LevelProgressionStorage.UnlockScene(progressionConfig.nextLevelSceneName);
            }

            UpdateLeaderboardUI(updatedScores);
            UpdateResultUI(finalScore, bronzeTarget, unlockedNext);
        }

        private void UpdateStaticUI()
        {
            if (scoreToWinText == null) return;

            int bronzeTarget = progressionConfig.GetBronzeTarget();
            scoreToWinText.text = $"Score to unlock next level (Bronze): {bronzeTarget}";
        }

        private void UpdateTimerUI()
        {
            if (timerText == null) return;

            int totalSeconds = Mathf.CeilToInt(_timeRemaining);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"Time: {minutes:00}:{seconds:00}";
        }

        private void UpdateResultUI(int finalScore, int bronzeTarget, bool unlockedNext)
        {
            if (levelResultText == null) return;

            string medal = GetMedalLabel(finalScore);
            string unlockText = unlockedNext
                ? $"Unlocked next level: {progressionConfig.nextLevelSceneName}"
                : $"Need {bronzeTarget} to unlock next level";

            levelResultText.text = $"Final Score: {finalScore}\nRank Reward: {medal}\n{unlockText}";
        }

        private string GetMedalLabel(int finalScore)
        {
            if (progressionConfig.preloadedTargets == null || progressionConfig.preloadedTargets.Length < 3)
            {
                return "No Belt";
            }

            int gold = progressionConfig.preloadedTargets[0];
            int silver = progressionConfig.preloadedTargets[1];
            int bronze = progressionConfig.preloadedTargets[2];

            // Top three thresholds map directly to belt labels.
            if (finalScore >= gold) return "Gold Belt";
            if (finalScore >= silver) return "Silver Belt";
            if (finalScore >= bronze) return "Bronze Belt";
            return "No Belt";
        }

        private void UpdateLeaderboardUI(List<int> scores)
        {
            if (leaderboardText == null || scores == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Leaderboard");

            for (int i = 0; i < scores.Count; i++)
            {
                string beltLabel = i switch
                {
                    0 => " (Gold Belt)",
                    1 => " (Silver Belt)",
                    2 => " (Bronze Belt)",
                    _ => string.Empty
                };

                sb.AppendLine($"{i + 1}. {scores[i]}{beltLabel}");
            }

            leaderboardText.text = sb.ToString();
        }
    }
}
