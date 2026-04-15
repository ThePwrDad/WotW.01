using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WeightLifter;
using StarterAssets;
using WotW2.Leaderboard;

public class LevelRunFlowController : MonoBehaviour
{
    [Header("Config")]
    public LevelFlowConfig config;

    [Header("Gameplay References")]
    public PlayerStats playerStats;
    public ThirdPersonController movementController;

    [Header("UI References")]
    public GameObject startLeaderboardPanel;
    public TMP_Text startLeaderboardTitleText;
    public TMP_Text startLeaderboardListText;

    public TMP_Text levelTimerText;

    public GameObject countdownPanel;
    public TMP_Text countdownText;

    public GameObject resultsPanel;
    public TMP_Text finalSwolText;
    public TMP_Text medalText;
    public TMP_Text leaderboardStatusText;
    public TMP_InputField playerNameInput;
    public GameObject submitScoreContainer;

    private bool _runActive;
    private bool _runEnded;
    private bool _hasSubmitted;
    private float _timeRemaining;
    private string _levelKey;
    private string _nextSceneName;

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogError("LevelRunFlowController needs a LevelFlowConfig assigned.", this);
        }

        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (movementController == null)
        {
            movementController = FindFirstObjectByType<ThirdPersonController>();
        }

        _levelKey = config != null && !string.IsNullOrWhiteSpace(config.levelKey)
            ? config.levelKey
            : ArcadeLeaderboardService.BuildDefaultLevelKeyFromScene(SceneManager.GetActiveScene().name);

        _nextSceneName = config != null ? config.nextSceneName : "MainMenu";
    }

    private void Start()
    {
        _timeRemaining = config != null ? Mathf.Max(1f, config.levelDurationSeconds) : 60f;
        UpdateTimerText(_timeRemaining);

        HidePanel(startLeaderboardPanel);
        HidePanel(countdownPanel);
        HidePanel(resultsPanel);

        StartCoroutine(RunStartFlow());
    }

    private void Update()
    {
        if (!_runActive || _runEnded)
        {
            return;
        }

        _timeRemaining -= Time.deltaTime;
        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            UpdateTimerText(_timeRemaining);
            EndRunFromTimer();
            return;
        }

        UpdateTimerText(_timeRemaining);
    }

    private IEnumerator RunStartFlow()
    {
        SetGameplayActive(false);
        LevelFlowState.SetPauseBlocked(true);

        ShowStartLeaderboard();
        float previewDuration = config != null ? Mathf.Max(0f, config.leaderboardPreviewSeconds) : 10f;
        bool allowSkip = config != null && config.allowStartSkip;
        yield return WaitForSecondsRealtimeWithSkip(previewDuration, allowSkip);

        HidePanel(startLeaderboardPanel);

        yield return RunCountdown();

        SetGameplayActive(true);
        LevelFlowState.SetPauseBlocked(false);
        _runActive = true;
    }

    private IEnumerator RunCountdown()
    {
        ShowPanel(countdownPanel);
        if (countdownText == null)
        {
            yield return null;
        }

        float step = config != null ? Mathf.Max(0.2f, config.countdownStepSeconds) : 1f;
        for (int i = 3; i >= 1; i--)
        {
            yield return PopAndFadeCountdownNumber(i.ToString(), step);
        }

        HidePanel(countdownPanel);
    }

    private IEnumerator PopAndFadeCountdownNumber(string value, float duration)
    {
        countdownText.text = value;
        countdownText.transform.localScale = Vector3.one * 0.6f;

        Color baseColor = countdownText.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float alpha = 1f - t;
            countdownText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            float pop = 0.6f + (0.8f * Mathf.Sin(t * Mathf.PI * 0.5f));
            countdownText.transform.localScale = Vector3.one * pop;

            if (config != null && config.allowStartSkip && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                break;
            }

            yield return null;
        }

        countdownText.color = baseColor;
    }

    private IEnumerator WaitForSecondsRealtimeWithSkip(float duration, bool allowSkip)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (allowSkip && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                break;
            }

            yield return null;
        }
    }

    private void EndRunFromTimer()
    {
        if (_runEnded)
        {
            return;
        }

        _runEnded = true;
        _runActive = false;

        SetGameplayActive(false);
        LevelFlowState.SetPauseBlocked(true);

        ShowResultsPanel();
    }

    private void ShowStartLeaderboard()
    {
        ShowPanel(startLeaderboardPanel);

        if (startLeaderboardTitleText != null)
        {
            string displayName = config != null ? config.levelDisplayName : SceneManager.GetActiveScene().name;
            startLeaderboardTitleText.text = displayName + " Top 5";
        }

        if (startLeaderboardListText != null)
        {
            startLeaderboardListText.text = ArcadeLeaderboardService.FormatBoardLines(_levelKey);
        }
    }

    private void ShowResultsPanel()
    {
        ShowPanel(resultsPanel);

        float finalScore = playerStats != null ? playerStats.currentStrength : 0f;
        if (finalSwolText != null)
        {
            finalSwolText.text = "Your Final Swol: " + finalScore.ToString("F1");
        }

        if (medalText != null)
        {
            string tier = config != null ? config.EvaluateTargetTier(finalScore) : "No Medal";
            medalText.text = "Tier: " + tier;
        }

        int beatenIndex = FindBeatenRank(finalScore);
        bool qualifies = beatenIndex >= 0;

        if (submitScoreContainer != null)
        {
            submitScoreContainer.SetActive(qualifies);
        }

        if (leaderboardStatusText != null)
        {
            if (qualifies)
            {
                leaderboardStatusText.text = "New Rank Available: #" + (beatenIndex + 1);
            }
            else
            {
                leaderboardStatusText.text = "No new leaderboard rank this run.";
            }
        }
    }

    private int FindBeatenRank(float score)
    {
        var board = ArcadeLeaderboardService.GetBoard(_levelKey);
        for (int i = 0; i < board.Count; i++)
        {
            if (score > board[i].score)
            {
                return i;
            }
        }

        return -1;
    }

    public void SubmitScore()
    {
        if (_hasSubmitted || _runActive)
        {
            return;
        }

        float score = playerStats != null ? playerStats.currentStrength : 0f;
        string name = playerNameInput != null ? playerNameInput.text : string.Empty;

        bool didWrite = ArcadeLeaderboardService.TryOverwriteHighestBeaten(_levelKey, score, name, out int rankIndex);

        _hasSubmitted = true;
        if (submitScoreContainer != null)
        {
            submitScoreContainer.SetActive(false);
        }

        if (leaderboardStatusText != null)
        {
            leaderboardStatusText.text = didWrite
                ? "Score saved at rank #" + (rankIndex + 1)
                : "Score did not beat current top 5.";
        }
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        LevelFlowState.SetPauseBlocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        Time.timeScale = 1f;
        LevelFlowState.SetPauseBlocked(false);

        if (!string.IsNullOrWhiteSpace(_nextSceneName))
        {
            SceneManager.LoadScene(_nextSceneName);
            return;
        }

        int current = SceneManager.GetActiveScene().buildIndex;
        int next = current + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(next);
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        LevelFlowState.SetPauseBlocked(false);
        SceneManager.LoadScene("MainMenu");
    }

    private void SetGameplayActive(bool active)
    {
        Time.timeScale = active ? 1f : 0f;
        Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !active;

        if (movementController != null)
        {
            movementController.SetExternalMovementLock(!active);
        }

        if (playerStats != null)
        {
            playerStats.isBusy = !active;
        }
    }

    private void UpdateTimerText(float remaining)
    {
        if (levelTimerText == null)
        {
            return;
        }

        int total = Mathf.CeilToInt(remaining);
        int minutes = total / 60;
        int seconds = total % 60;
        levelTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private static void ShowPanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(true);
    }

    private static void HidePanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(false);
    }
}
