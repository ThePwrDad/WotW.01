using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeightLifter;

// Attach to a level button in the selection menu.
// It reads unlock state and disables locked levels automatically.
public class LevelSelectLockState : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string sceneName;
    [SerializeField] private bool defaultUnlocked;

    [Header("UI")]
    [SerializeField] private Button levelButton;
    [SerializeField] private TMP_Text lockStateText;

    private void Awake()
    {
        if (levelButton == null)
        {
            levelButton = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        // First level can be defaultUnlocked=true; later levels depend on progression saves.
        bool unlocked = LevelProgressionStorage.IsSceneUnlocked(sceneName, defaultUnlocked);

        if (levelButton != null)
        {
            levelButton.interactable = unlocked;
        }

        if (lockStateText != null)
        {
            lockStateText.text = unlocked ? string.Empty : "Locked (Bronze required)";
        }
    }
}
