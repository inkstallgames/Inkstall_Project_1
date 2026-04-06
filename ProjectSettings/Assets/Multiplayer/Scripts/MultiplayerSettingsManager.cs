using System;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerSettingsManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag your new UI Slider here")]
    public Slider sensitivitySlider;

    [Header("Settings")]
    public float defaultSensitivity = 0.2f;
    
    // This key is strictly for multiplayer to keep it decoupled from single-player settings
    private const string MP_SENSITIVITY_KEY = "MultiplayerSensitivity";

    // Event for real-time sensitivity updates
    public static event Action<float> OnSensitivityChangedEvent;

    private void Start()
    {
        if (sensitivitySlider != null)
        {
            // Load existing sensitivity or default
            float savedSens = PlayerPrefs.GetFloat(MP_SENSITIVITY_KEY, defaultSensitivity);
            sensitivitySlider.value = savedSens;

            // Listen for slider changes
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }
        else
        {
            Debug.LogWarning("[MultiplayerSettingsManager] Sensitivity Slider is not assigned in the Inspector!");
        }
    }

    private void OnSensitivityChanged(float value)
    {
        // Save the new value instantly
        PlayerPrefs.SetFloat(MP_SENSITIVITY_KEY, value);
        PlayerPrefs.Save();
        
        // Broadcast the change
        OnSensitivityChangedEvent?.Invoke(value);
    }
}
