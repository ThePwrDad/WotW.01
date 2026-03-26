using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Using the New Input System!

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuUI;
    private bool isPaused = false;
    private bool hasWarnedMissingPauseMenu;

    void Awake()
    {
        ResolvePauseMenuReference();
    }

    void Start()
    {
        // Ensure the game starts unpaused and the mouse is hidden
        Resume();
    }

    private void ResolvePauseMenuReference()
    {
        if (pauseMenuUI != null) return;

        // 1. Direct child named "PauseMenu"
        Transform directChild = transform.Find("PauseMenu");
        if (directChild != null) { pauseMenuUI = directChild.gameObject; return; }

        // 2. Any child (including inactive) containing "pause"
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;
            if (child.name.IndexOf("pause", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                pauseMenuUI = child.gameObject;
                return;
            }
        }

        // 3. Broad scene search — covers UI panels that are siblings or unrelated GameObjects
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            // Skip assets (prefabs not in the scene), Editor-only objects, and self
            if (!go.scene.isLoaded || go == gameObject) continue;
            if (go.name.IndexOf("pause", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                go.name.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                pauseMenuUI = go;
                return;
            }
        }

        // 4. Looser fallback: any loaded scene object with "pause" in the name
        foreach (GameObject go in allObjects)
        {
            if (!go.scene.isLoaded || go == gameObject) continue;
            if (go.name.IndexOf("pause", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                pauseMenuUI = go;
                return;
            }
        }
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
        if (pauseMenuUI == null)
        {
            ResolvePauseMenuReference();
        }

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        else if (!hasWarnedMissingPauseMenu)
        {
            Debug.LogWarning("PauseMenuController: pauseMenuUI is not assigned. Assign it in the inspector.");
            hasWarnedMissingPauseMenu = true;
        }

        Time.timeScale = 1f; // Unfreeze time
        isPaused = false;

        // Hide and Lock Cursor for 3D gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Pause()
    {
        if (pauseMenuUI == null)
        {
            ResolvePauseMenuReference();
        }

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
        else if (!hasWarnedMissingPauseMenu)
        {
            Debug.LogWarning("PauseMenuController: pauseMenuUI is not assigned. Assign it in the inspector.");
            hasWarnedMissingPauseMenu = true;
        }

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
