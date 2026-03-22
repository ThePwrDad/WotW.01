using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class StartMenuController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject startMenuPanel;
    public GameObject levelSelectionPanel;

    void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        if (startMenuPanel != null) startMenuPanel.SetActive(true);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        // Ensure the main panel is active and selection is hidden at the start
        startMenuPanel.SetActive(true);
        levelSelectionPanel.SetActive(false);
    }
    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (levelSelectionPanel.activeSelf)
            {
                CloseLevelSelection();
            }
        }
    }

    // Call this from the "Level Selection" button
    public void OpenLevelSelection()
    {
        Debug.Log("Button Clicked!");//confirming the button is working in the console
        if (startMenuPanel != null && levelSelectionPanel != null)
        {
            startMenuPanel.SetActive(false);
            levelSelectionPanel.SetActive(true);
            Debug.Log("Panels toggled successfully.");
        }
        else
        {
            Debug.LogError("StartMenuPanel or LevelSelectionPanel is not assigned in the inspector.");
        }
        
        //ThePauseMenuController should have already handled hiding and locking the cursor, but we can ensure it here as well
        }
    // Call this from a "Back" button inside your Level Selection panel
    public void CloseLevelSelection()
    {
        levelSelectionPanel.SetActive(false);
        startMenuPanel.SetActive(true);
    }

    // Call this from the "Quit" button
    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
    // Add this inside the StartMenuController class
public void LoadSpecificLevel(string sceneName)
{
    // It's good practice to ensure time is unpaused when switching scenes
    Time.timeScale = 1f; 
    //hide and lock the cursor before the new scene loads
    UnityEngine.Cursor.visible = false;
    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
    
    SceneManager.LoadScene(sceneName);
}
}
