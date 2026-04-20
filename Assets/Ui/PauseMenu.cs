using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Using the New Input System!

public class PauseMenuController : MonoBehaviour
{
    public GameObject pauseMenuUI;
    [Tooltip("If true, logs a warning when no pause menu panel can be found in the scene.")]
    public bool logMissingPauseMenuWarning = false;

    private bool isPaused = false;
    private bool hasValidPauseUI = false;
    private bool loggedMissingPauseUIWarning = false;

    private static readonly string[] PausePanelNames =
    {
        "PauseMenuPanel",
        "PauseMenu",
        "Pause Panel"
    };

    void Awake()
    {
        ResolvePauseMenuUI();
    }

    void Start()
    {
        hasValidPauseUI = EnsurePauseMenuUI();

        // Ensure the game starts unpaused and the mouse is hidden
        Resume();
    }

    void Update()
    {
        if (!hasValidPauseUI)
        {
            hasValidPauseUI = EnsurePauseMenuUI();
            if (!hasValidPauseUI) return;
        }

        if (LevelFlowState.IsPauseBlocked)
        {
            if (isPaused)
            {
                Resume();
            }
            return;
        }

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
            loggedMissingPauseUIWarning = false;
            hasValidPauseUI = true;
            return true;
        }

        ResolvePauseMenuUI();
        hasValidPauseUI = pauseMenuUI != null;

        if (!hasValidPauseUI && logMissingPauseMenuWarning && !loggedMissingPauseUIWarning)
        {
            Debug.LogWarning("PauseMenuController could not resolve a pause menu UI in this scene.", this);
            loggedMissingPauseUIWarning = true;
        }

        return hasValidPauseUI;
    }

    void ResolvePauseMenuUI()
    {
        if (pauseMenuUI != null)
        {
            return;
        }

        // Scene setups vary between levels and prefabs, so pause UI resolution checks the local
        // hierarchy first, then the active scene, then a simple name lookup as a final fallback.
        Transform localMatch = FindNamedDescendant(transform);
        if (localMatch != null)
        {
            pauseMenuUI = localMatch.gameObject;
            return;
        }

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform sceneMatch = FindNamedDescendant(roots[i].transform);
            if (sceneMatch != null)
            {
                pauseMenuUI = sceneMatch.gameObject;
                return;
            }
        }

        for (int i = 0; i < PausePanelNames.Length; i++)
        {
            GameObject foundByName = GameObject.Find(PausePanelNames[i]);
            if (foundByName != null)
            {
                pauseMenuUI = foundByName;
                return;
            }
        }
    }

    private static Transform FindNamedDescendant(Transform root)
    {
        if (root == null) return null;

        for (int i = 0; i < PausePanelNames.Length; i++)
        {
            if (root.name == PausePanelNames[i])
                return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindNamedDescendant(child);
            if (found != null)
                return found;
        }

        return null;
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
