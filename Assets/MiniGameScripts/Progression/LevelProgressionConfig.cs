using UnityEngine;

namespace WeightLifter
{
    // Per-level tuning data: timer length, seeded leaderboard targets, and next-level unlock route.
    [CreateAssetMenu(fileName = "LevelProgressionConfig", menuName = "WeightLifter/Level Progression Config")]
    public class LevelProgressionConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable ID used for leaderboard save keys. Example: Level01")]
        public string levelId = "Level01";

        [Tooltip("Optional scene name for this level (for UI/debug only).")]
        public string sceneName = "Level01";

        [Tooltip("Scene to unlock when Bronze or better is reached.")]
        public string nextLevelSceneName = "Level02";

        [Header("Timer")]
        [Min(1f)]
        public float levelDurationSeconds = 180f;

        [Header("Leaderboard")]
        [Tooltip("Preloaded target leaderboard values. Use 10 entries for ranks 1-10.")]
        public int[] preloadedTargets = new int[10] { 300, 260, 220, 190, 160, 140, 120, 100, 80, 60 };

        // Bronze is rank #3, which acts as the progression gate for unlocking the next level.
        public int GetBronzeTarget()
        {
            if (preloadedTargets == null || preloadedTargets.Length < 3)
            {
                return 0;
            }

            return Mathf.Max(0, preloadedTargets[2]);
        }
    }
}
