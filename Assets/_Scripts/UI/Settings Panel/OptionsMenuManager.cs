using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.Audio; 
using StarterAssets; 
using UnityEngine.SceneManagement;

public class OptionsMenuManager : MonoBehaviour
{
    public GameObject SettingsPanel; 
    public GameObject MainCanvas; 
    public GameObject DeletePopUp; 
    
    [Header("UI Controls")]
    public Slider sensitivitySlider; 
    public Slider volumeSlider; 
    public Slider musicSlider; 
    
    [Header("Sensitivity Settings")]
    public float sensitivityMultiplier = 1.0f; // Adjust this in the inspector to scale sensitivity
    
    // Settings values
    private float screenSensitivity = 0.2f;
    private float volumeLevel = 0.5f;
    private float musicVolume = 0.5f;
    
    // PlayerPrefs keys for saving settings
    private const string SENSITIVITY_KEY = "ScreenSensitivity";
    private const string VOLUME_KEY = "VolumeLevel";
    private const string MUSIC_KEY = "MusicVolume";
    
    // References to other components
    private FirstPersonController playerController;
    private AudioManager audioManager;
    
    private void Start()
    {
        // Find references
        playerController = FindObjectOfType<FirstPersonController>();
        audioManager = AudioManager.Instance;

        // Load saved settings
        LoadSettings();

        // Initialize UI elements with saved values
        UpdateUIFromSettings();

        // Apply settings
        ApplySensitivity(screenSensitivity);
        ApplyVolume(volumeLevel);
        ApplyMusicVolume(musicVolume);

        // Add listeners to UI elements
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(ApplySensitivity);

        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(ApplyVolume);

        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(ApplyMusicVolume);
    }

    // Call this function when button is clicked
    public void EnableOptionsMenu()
    {
        
        // Update music slider to reflect current music volume
        if (musicSlider != null)
        {
            musicSlider.value = musicVolume;
        }

        // Update sensitivity slider to reflect current sensitivity
        if (playerController != null && sensitivitySlider != null)
        {
            sensitivitySlider.value = playerController.touchSensitivity / sensitivityMultiplier;
        }

        // Toggle the active state
        SettingsPanel.SetActive(true);
        Time.timeScale = 0f; 
        MainCanvas.SetActive(false); 
    }

    public void OnResumeButtonClicked()
    {
        SaveSettings(); 
        SettingsPanel.SetActive(false); 
        MainCanvas.SetActive(true); 
        Time.timeScale = 1f; 
    }
    public void OnCloseButtonClicked()
    {
        SettingsPanel.SetActive(false); 
        MainCanvas.SetActive(true); 
        Time.timeScale = 1f; 
    }

    public void OnMenuButtonClicked()
    {
        SceneManager.LoadScene("Menu");
    }
    
    // Apply screen sensitivity
    public void ApplySensitivity(float sensitivity)
    {
        screenSensitivity = sensitivity;
        
        // Apply to player controller if available
        if (playerController != null)
        {
            playerController.touchSensitivity = sensitivity * sensitivityMultiplier;
        }
    }
    
    // Apply volume setting
    public void ApplyVolume(float volume)
    {
        volumeLevel = volume;
        
        // Update AudioManager SFX volume
        if (audioManager != null)
        {
            audioManager.UpdateSFXVolume(volume);
        }

        // Get the music source to exclude it
        AudioSource musicSource = null;
        if (audioManager != null)
        {
            musicSource = audioManager.GetAudioSource();
        }

        // Set the volume of all audio sources EXCEPT the music source
        AudioSource[] audioSources = FindObjectsOfType<AudioSource>();
        foreach (AudioSource source in audioSources)
        {
            // Skip the music source
            if (source == musicSource) continue;

            source.volume = volume;
        }
    }
    
    // Apply music volume
    public void ApplyMusicVolume(float volume)
    {
        musicVolume = volume;

        // Apply to audio manager if available
        if (audioManager != null)
        {
            audioManager.SetMusicVolume(volume);
        }
    }
    
    // Updates all UI elements to match current settings
    private void UpdateUIFromSettings()
    {
        if (sensitivitySlider != null)
            sensitivitySlider.value = screenSensitivity;
            
        if (volumeSlider != null)
            volumeSlider.value = volumeLevel;
            
        if (musicSlider != null)
            musicSlider.value = musicVolume;
    }
    
    // Save settings to PlayerPrefs
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(SENSITIVITY_KEY, screenSensitivity);
        PlayerPrefs.SetFloat(VOLUME_KEY, volumeLevel);
        PlayerPrefs.SetFloat(MUSIC_KEY, musicVolume);
        PlayerPrefs.Save();
    }
    
    // Load settings from PlayerPrefs
    private void LoadSettings()
    {
        screenSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, 0.5f);
        volumeLevel = PlayerPrefs.GetFloat(VOLUME_KEY, 0.5f);
        musicVolume = PlayerPrefs.GetFloat(MUSIC_KEY, 0.5f);
    }

    public void OnDeleteButtonClicked()
    {
        DeletePopUp.SetActive(true);
    }

    public void OnNotDeleteButtonClicked()
    {
        DeletePopUp.SetActive(false);
    }

    private void OnApplicationQuit()
    {
        SaveSettings();
    }
    public void ExitToHome(){
        SceneManager.LoadScene(0);
    }
}
