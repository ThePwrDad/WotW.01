using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Using the New Input System!

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuUI;
    private bool isPaused = false;

    void Start()
    {
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
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f; // Unfreeze time
        isPaused = false;

        // Hide and Lock Cursor for 3D gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f; // Freeze time (physics, animations, etc.)
        isPaused = true;

        // Unlock and Show Cursor to click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
