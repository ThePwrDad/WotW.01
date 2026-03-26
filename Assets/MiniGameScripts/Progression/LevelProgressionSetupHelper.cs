using TMPro;
using UnityEngine;

namespace WeightLifter
{
    // Drop this on any scene object to auto-wire LevelTimerAndLeaderboardController references.
    // It helps reduce manual inspector setup per level.
    public class LevelProgressionSetupHelper : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("If null, helper will look on this GameObject first, then anywhere in scene.")]
        [SerializeField] private LevelTimerAndLeaderboardController controller;

        [Header("Search Names")]
        [SerializeField] private string timerTextName = "TimerText";
        [SerializeField] private string scoreToWinTextName = "ScoreToWinText";
        [SerializeField] private string leaderboardTextName = "LeaderboardText";
        [SerializeField] private string levelResultTextName = "LevelResultText";

        [Header("Behavior")]
        [Tooltip("Run auto-wire at Start if any required references are missing.")]
        [SerializeField] private bool autoWireOnStart = true;

        private void Reset()
        {
            AutoWireNow();
        }

        private void Start()
        {
            if (!autoWireOnStart) return;
            if (controller == null || HasMissingControllerReferences(controller))
            {
                AutoWireNow();
            }
        }

        [ContextMenu("Auto-Wire Progression References")]
        public void AutoWireNow()
        {
            if (controller == null)
            {
                controller = GetComponent<LevelTimerAndLeaderboardController>();
            }

            if (controller == null)
            {
                controller = FindFirstObjectByType<LevelTimerAndLeaderboardController>(FindObjectsInactive.Include);
            }

            if (controller == null)
            {
                Debug.LogWarning("LevelProgressionSetupHelper: Could not find LevelTimerAndLeaderboardController in scene.", this);
                return;
            }

            if (controller.playerStats == null)
            {
                controller.playerStats = FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Include);
            }

            // Keep existing assignments if already set; only fill in missing references.
            if (controller.timerText == null)
                controller.timerText = FindTextByName(timerTextName);

            if (controller.scoreToWinText == null)
                controller.scoreToWinText = FindTextByName(scoreToWinTextName);

            if (controller.leaderboardText == null)
                controller.leaderboardText = FindTextByName(leaderboardTextName);

            if (controller.levelResultText == null)
                controller.levelResultText = FindTextByName(levelResultTextName);

            Debug.Log("LevelProgressionSetupHelper: Auto-wire complete. Check controller fields for final assignments.", this);
        }

        private static bool HasMissingControllerReferences(LevelTimerAndLeaderboardController target)
        {
            if (target == null) return true;

            return target.playerStats == null ||
                   target.timerText == null ||
                   target.scoreToWinText == null ||
                   target.leaderboardText == null ||
                   target.levelResultText == null;
        }

        private TMP_Text FindTextByName(string wantedName)
        {
            if (string.IsNullOrWhiteSpace(wantedName)) return null;

            TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // 1) Exact name match first.
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text t = allTexts[i];
                if (t != null && t.name == wantedName)
                {
                    return t;
                }
            }

            // 2) Fallback to contains match for flexibility.
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text t = allTexts[i];
                if (t != null && t.name.IndexOf(wantedName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
