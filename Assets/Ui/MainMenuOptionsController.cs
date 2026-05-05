using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuOptionsController : MonoBehaviour
{
    [Header("UI Controls")]
    public Slider masterVolumeSlider;
    public TMP_Text masterVolumeValueText;
    public Toggle fullscreenToggle;
    public Toggle vSyncToggle;
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;

    [Header("Defaults")]
    [Range(0f, 1f)] public float defaultMasterVolume = 1f;
    public bool defaultFullscreen = true;
    public bool defaultVSync = true;

    private const string MasterVolumeKey = "settings.masterVolume";
    private const string FullscreenKey = "settings.fullscreen";
    private const string VSyncKey = "settings.vsync";
    private const string QualityKey = "settings.quality";
    private const string ResolutionKey = "settings.resolution";

    private readonly List<Resolution> availableResolutions = new List<Resolution>();
    private bool initialized;

    private void Start()
    {
        BuildResolutionDropdown();
        LoadAndApplySettings();
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            return;
        }

        RefreshUIFromCurrentSettings();
    }

    public void OnMasterVolumeChanged(float value)
    {
        float clamped = Mathf.Clamp01(value);
        AudioListener.volume = clamped;
        UpdateMasterVolumeLabel(clamped);
        PlayerPrefs.SetFloat(MasterVolumeKey, clamped);
        PlayerPrefs.Save();
    }

    public void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void OnVSyncChanged(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void OnQualityChanged(int qualityIndex)
    {
        int clamped = Mathf.Clamp(qualityIndex, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(clamped, true);
        PlayerPrefs.SetInt(QualityKey, clamped);
        PlayerPrefs.Save();
    }

    public void OnResolutionChanged(int resolutionIndex)
    {
        if (availableResolutions.Count == 0)
        {
            return;
        }

        int clamped = Mathf.Clamp(resolutionIndex, 0, availableResolutions.Count - 1);
        Resolution resolution = availableResolutions[clamped];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        PlayerPrefs.SetInt(ResolutionKey, clamped);
        PlayerPrefs.Save();
    }

    public void RestoreDefaults()
    {
        AudioListener.volume = defaultMasterVolume;
        Screen.fullScreen = defaultFullscreen;
        QualitySettings.vSyncCount = defaultVSync ? 1 : 0;
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);

        if (availableResolutions.Count > 0)
        {
            Resolution bestResolution = availableResolutions[availableResolutions.Count - 1];
            Screen.SetResolution(bestResolution.width, bestResolution.height, defaultFullscreen);
            PlayerPrefs.SetInt(ResolutionKey, availableResolutions.Count - 1);
        }

        PlayerPrefs.SetFloat(MasterVolumeKey, defaultMasterVolume);
        PlayerPrefs.SetInt(FullscreenKey, defaultFullscreen ? 1 : 0);
        PlayerPrefs.SetInt(VSyncKey, defaultVSync ? 1 : 0);
        PlayerPrefs.SetInt(QualityKey, QualitySettings.names.Length - 1);
        PlayerPrefs.Save();

        RefreshUIFromCurrentSettings();
    }

    private void BuildResolutionDropdown()
    {
        if (resolutionDropdown == null)
        {
            return;
        }

        resolutionDropdown.ClearOptions();
        availableResolutions.Clear();

        Resolution[] systemResolutions = Screen.resolutions;
        HashSet<string> seen = new HashSet<string>();
        List<string> labels = new List<string>();

        for (int i = 0; i < systemResolutions.Length; i++)
        {
            Resolution current = systemResolutions[i];
            string key = current.width + "x" + current.height;
            if (seen.Contains(key))
            {
                continue;
            }

            seen.Add(key);
            availableResolutions.Add(current);
            labels.Add(current.width + " x " + current.height);
        }

        resolutionDropdown.AddOptions(labels);
    }

    private void LoadAndApplySettings()
    {
        float savedVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume);
        bool savedFullscreen = PlayerPrefs.GetInt(FullscreenKey, defaultFullscreen ? 1 : 0) == 1;
        bool savedVSync = PlayerPrefs.GetInt(VSyncKey, defaultVSync ? 1 : 0) == 1;

        int maxQuality = Mathf.Max(0, QualitySettings.names.Length - 1);
        int savedQuality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, maxQuality), 0, maxQuality);

        AudioListener.volume = Mathf.Clamp01(savedVolume);
        Screen.fullScreen = savedFullscreen;
        QualitySettings.vSyncCount = savedVSync ? 1 : 0;
        QualitySettings.SetQualityLevel(savedQuality, true);

        if (availableResolutions.Count > 0)
        {
            int savedResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(ResolutionKey, GetCurrentResolutionIndex()), 0, availableResolutions.Count - 1);
            Resolution chosen = availableResolutions[savedResolutionIndex];
            Screen.SetResolution(chosen.width, chosen.height, savedFullscreen);
        }

        RefreshUIFromCurrentSettings();
    }

    private void RefreshUIFromCurrentSettings()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
            UpdateMasterVolumeLabel(AudioListener.volume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        if (vSyncToggle != null)
        {
            vSyncToggle.SetIsOnWithoutNotify(QualitySettings.vSyncCount > 0);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(QualitySettings.GetQualityLevel());
            qualityDropdown.RefreshShownValue();
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.SetValueWithoutNotify(GetCurrentResolutionIndex());
            resolutionDropdown.RefreshShownValue();
        }
    }

    private int GetCurrentResolutionIndex()
    {
        if (availableResolutions.Count == 0)
        {
            return 0;
        }

        int currentWidth = Screen.currentResolution.width;
        int currentHeight = Screen.currentResolution.height;

        for (int i = 0; i < availableResolutions.Count; i++)
        {
            if (availableResolutions[i].width == currentWidth && availableResolutions[i].height == currentHeight)
            {
                return i;
            }
        }

        return availableResolutions.Count - 1;
    }

    private void UpdateMasterVolumeLabel(float value)
    {
        if (masterVolumeValueText == null)
        {
            return;
        }

        int percent = Mathf.RoundToInt(value * 100f);
        masterVolumeValueText.text = percent + "%";
    }
}
