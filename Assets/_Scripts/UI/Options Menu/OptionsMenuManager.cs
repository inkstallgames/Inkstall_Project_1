using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.Audio; 
using StarterAssets; 

public class OptionsMenuManager : MonoBehaviour
{
    public GameObject optionsMenuCanvas; 
    public GameObject mobileControlsCanvas; 
    
    [Header("UI Controls")]
    public Slider sensitivitySlider; 
    public Slider volumeSlider; 
    public Toggle musicToggle; 
    
    // Settings values
    private float screenSensitivity = 0.5f;
    private float volumeLevel = 0.5f;
    private bool musicEnabled = true;
    
    // PlayerPrefs keys for saving settings
    private const string SENSITIVITY_KEY = "ScreenSensitivity";
    private const string VOLUME_KEY = "VolumeLevel";
    private const string MUSIC_KEY = "MusicEnabled";
    
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
        if (sensitivitySlider != null)
            sensitivitySlider.value = screenSensitivity;
            
        if (volumeSlider != null)
            volumeSlider.value = volumeLevel;
            
        if (musicToggle != null)
            musicToggle.isOn = musicEnabled;
            
        // Apply settings
        ApplySensitivity(screenSensitivity);
        ApplyVolume(volumeLevel);
        ApplyMusicSetting(musicEnabled);
        
        // Add listeners to UI elements
        if (sensitivitySlider != null)
            sensitivitySlider.onValueChanged.AddListener(ApplySensitivity);
            
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(ApplyVolume);
            
        if (musicToggle != null)
            musicToggle.onValueChanged.AddListener(ApplyMusicSetting);
    }
    
    // Call this function when button is clicked
    public void EnableOptionsMenu()
    {
        // Update music toggle to reflect current music state
        if (audioManager != null && musicToggle != null)
        {
            musicToggle.isOn = audioManager.IsMusicPlaying();
        }

        // Toggle the active state
        optionsMenuCanvas.SetActive(true);
        Time.timeScale = 0f; 
        mobileControlsCanvas.SetActive(false); 
    }

    public void ResumeGame()
    {
        SaveSettings(); 
        optionsMenuCanvas.SetActive(false); 
        mobileControlsCanvas.SetActive(true); 
        Time.timeScale = 1f; 
    }
    
    // Apply screen sensitivity
    public void ApplySensitivity(float sensitivity)
    {
        screenSensitivity = sensitivity;
        
        // Apply to player controller if available
        if (playerController != null)
        {
            playerController.touchSensitivity = sensitivity;
        }
    }
    
    // Apply volume setting
    public void ApplyVolume(float volume)
    {
        volumeLevel = volume;
        
        // Set the volume of all audio sources
        AudioSource[] audioSources = FindObjectsOfType<AudioSource>();
        foreach (AudioSource source in audioSources)
        {
            source.volume = volume;
        }
    }
    
    // Apply music setting
    public void ApplyMusicSetting(bool enabled)
    {
        musicEnabled = enabled;
        
        // Apply to audio manager if available
        if (audioManager != null)
        {
            if (enabled)
            {
                audioManager.PlayMusic();
            }
            else
            {
                audioManager.StopMusic();
            }
        }
    }
    
    // Save settings to PlayerPrefs
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(SENSITIVITY_KEY, screenSensitivity);
        PlayerPrefs.SetFloat(VOLUME_KEY, volumeLevel);
        PlayerPrefs.SetInt(MUSIC_KEY, musicEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    // Load settings from PlayerPrefs
    private void LoadSettings()
    {
        screenSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_KEY, 1.0f);
        volumeLevel = PlayerPrefs.GetFloat(VOLUME_KEY, 1.0f);
        musicEnabled = PlayerPrefs.GetInt(MUSIC_KEY, 1) == 1;
    }
}
