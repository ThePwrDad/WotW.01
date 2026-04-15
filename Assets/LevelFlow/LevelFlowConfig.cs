using UnityEngine;

[CreateAssetMenu(fileName = "LevelFlowConfig", menuName = "WotW2/Level Flow Config")]
public class LevelFlowConfig : ScriptableObject
{
    [Header("Level Identity")]
    public string levelKey = "Level 1";
    public string levelDisplayName = "Level 1";
    public string nextSceneName = "MainMenu";

    [Header("Start Sequence")]
    public float leaderboardPreviewSeconds = 10f;
    public bool allowStartSkip = true;
    public float countdownStepSeconds = 1f;

    [Header("Run Timer")]
    public float levelDurationSeconds = 60f;

    [Header("Score Targets")]
    public float bronzeTarget = 100f;
    public float silverTarget = 175f;
    public float goldTarget = 250f;

    public string EvaluateTargetTier(float score)
    {
        if (score >= goldTarget) return "Gold";
        if (score >= silverTarget) return "Silver";
        if (score >= bronzeTarget) return "Bronze";
        return "No Medal";
    }
}
