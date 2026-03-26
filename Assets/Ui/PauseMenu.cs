using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Using the New Input System!

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuUI;
    private bool isPaused = false;
    private bool hasValidPauseUI = false;

    void Awake()
    {
        ResolvePauseMenuUI();
    }

    void Start()
    {
        hasValidPauseUI = EnsurePauseMenuUI();
        if (!hasValidPauseUI)
        {
            Debug.LogWarning("PauseMenuController disabled on this object because no pause menu UI could be resolved.", this);
            enabled = false;
            return;
        }

        // Ensure the game starts unpaused and the mouse is hidden
        Resume();
    }

    void Update()
    {
        // Check for Escape key using the New Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Resume()
    {
        if (!EnsurePauseMenuUI()) return;

        pauseMenuUI.SetActive(false);

        Time.timeScale = 1f; // Unfreeze time
        isPaused = false;

        // Hide and Lock Cursor for 3D gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Pause()
    {
        if (!EnsurePauseMenuUI()) return;

        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // Freeze time (physics, animations, etc.)
        isPaused = true;

        // Unlock and Show Cursor to click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    bool EnsurePauseMenuUI()
    {
        if (pauseMenuUI != null)
        {
            hasValidPauseUI = true;
            return true;
        }

        ResolvePauseMenuUI();
        hasValidPauseUI = pauseMenuUI != null;
        return hasValidPauseUI;
    }

    void ResolvePauseMenuUI()
    {
        if (pauseMenuUI != null)
        {
            return;
        }

        Transform directChild = transform.Find("PauseMenuPanel");
        if (directChild != null)
        {
            pauseMenuUI = directChild.gameObject;
            return;
        }

        Transform nestedChild = transform.Find("Canvas/PauseMenuPanel");
        if (nestedChild != null)
        {
            pauseMenuUI = nestedChild.gameObject;
            return;
        }

        GameObject foundByName = GameObject.Find("PauseMenuPanel");
        if (foundByName != null)
        {
            pauseMenuUI = foundByName;
        }
    }

    public void ResetLevel()
    {
        Time.timeScale = 1f; // IMPORTANT: Always unfreeze before reloading
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // Make sure this matches your scene name exactly!
    }
}
