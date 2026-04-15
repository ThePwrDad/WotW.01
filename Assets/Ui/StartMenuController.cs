using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using WotW2.Leaderboard;

public class StartMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject startMenuPanel;
    public GameObject levelSelectionPanel;
    public GameObject leaderboardPanel;

    [Header("Leaderboard UI")]
    public TMP_Text leaderboardTitleText;
    public TMP_Text leaderboardEntriesText;
    public string selectedLevelKey = "Level 1";

    void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        if (startMenuPanel != null) startMenuPanel.SetActive(true);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);

        RefreshLeaderboardPanel();
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (leaderboardPanel != null && leaderboardPanel.activeSelf)
        {
            CloseLeaderboard();
            return;
        }

        if (levelSelectionPanel != null && levelSelectionPanel.activeSelf)
        {
            CloseLevelSelection();
        }
    }

    // Call this from the "Level Selection" button
    public void OpenLevelSelection()
    {
        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);
    }

    // Call this from a "Back" button inside your Level Selection panel
    public void CloseLevelSelection()
    {
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (startMenuPanel != null) startMenuPanel.SetActive(true);
    }

    public void OpenLeaderboard()
    {
        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(true);
        RefreshLeaderboardPanel();
    }

    public void CloseLeaderboard()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        if (startMenuPanel != null) startMenuPanel.SetActive(true);
    }

    public void SetLeaderboardLevelKey(string levelKey)
    {
        if (!string.IsNullOrWhiteSpace(levelKey))
        {
            selectedLevelKey = levelKey.Trim();
        }

        RefreshLeaderboardPanel();
    }

    public void RefreshLeaderboardPanel()
    {
        if (leaderboardTitleText != null)
        {
            leaderboardTitleText.text = selectedLevelKey + " Top 5";
        }

        if (leaderboardEntriesText != null)
        {
            leaderboardEntriesText.text = ArcadeLeaderboardService.FormatBoardLines(selectedLevelKey);
        }
    }

    // Call this from the "Quit" button
    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }

    public void LoadSpecificLevel(string sceneName)
    {
        Time.timeScale = 1f;
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;

        SceneManager.LoadScene(sceneName);
    }
}
