using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;



public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int keysCount;
    [SerializeField] private TextMeshProUGUI keyText;
    
    // Event to notify when keys count changes
    public event Action OnKeysChanged;
    [Header("Sound Effects")]
    [SerializeField] private AudioClip keyCollectSound; // Sound to play when keys are added


    private void Awake()
    {
        // Set up singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Load keys from PlayerPrefs first
        LoadKeys();

        // Update UI after loading keys
        UpdateUIKeyCount();
    }

    private void OnEnable()
    {
        // Refresh UI when scene changes or object is enabled
        UpdateUIKeyCount();
    }

    private void LoadKeys()
    {
        // Load from PlayerPrefs, if no value exists it will use the default value (10)
        keysCount = PlayerPrefs.GetInt("KeysCount", 10);
        UpdateUIKeyCount();
    }

    private void SaveKeys()
    {
        PlayerPrefs.SetInt("KeysCount", keysCount);
        PlayerPrefs.Save();
    }

    public void AddKeys(int amount)
    {
        int previousCount = keysCount;
        keysCount += amount;
        
        // Save the keys
        SaveKeys();
        
        UpdateUIKeyCount();
        
        // Play sound effect if keys were actually added
        if (amount > 0 && keyCollectSound != null)
        {
            // Play the sound at the camera's position to ensure it's heard
            if (Camera.main != null)
            {
                AudioManager.Instance.PlaySFXAtPoint(keyCollectSound, Camera.main.transform.position);
            }
            else
            {
                // Fallback to world origin if no main camera is found
                AudioManager.Instance.PlaySFXAtPoint(keyCollectSound, Vector3.zero);
            }
        }
    }



    // Update the UI to show the current key count
    private void UpdateUIKeyCount()
    {
        // Update the UI text if assigned
        if (keyText != null)
        {
            keyText.text = keysCount.ToString();
        }
        
        // Notify listeners
        OnKeysChanged?.Invoke();
    }

    /// <summary>
    /// Set the key text UI reference (called by UI scripts in each scene)
    /// </summary>
    public void SetKeyTextUI(TextMeshProUGUI newKeyText)
    {
        keyText = newKeyText;
        UpdateUIKeyCount();
    }

    // Returns the current key count as an integer
    public int GetCurrentKeyCount()
    {
        return keysCount;
    }


    public bool UseKey()
    {
        // Check if we have keys available
        if (keysCount <= 0)
        {
            return false;
        }

        // Reduce local key count for internal tracking
        keysCount--;
        SaveKeys();
        UpdateUIKeyCount();

        // Notify the timer system that a key was used
        if (KeyRefreshTimer.Instance != null)
        {
            KeyRefreshTimer.Instance.OnKeyUsed();
        }

        return true;
    }

    // Update the UI text displaying the key count
    private void UpdateKeyText()
    {
        if (keyText != null)
        {
            keyText.text = keysCount.ToString();
        }
    }



    // Force update the UI with the current key count
    public void ForceUpdateUI()
    {

        // Check if keyText is assigned
        if (keyText == null)
        {

            // Try to find the key text component if not assigned
            keyText = GameObject.FindObjectOfType<TextMeshProUGUI>();
            if (keyText != null)
            {
            }
            else
            {
                return;
            }
        }

        // Update the UI text directly
        keyText.text = keysCount.ToString();

        // Force UI refresh
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            canvas.enabled = false;
            canvas.enabled = true;
        }

        // Force TMPro to update
        keyText.ForceMeshUpdate();
        
        // Notify listeners
        OnKeysChanged?.Invoke();
    }

    // For debugging - call this from other scripts or the Unity Editor
    public void DebugResetKeys()
    {
        keysCount = 0;
        SaveKeys();
        UpdateUIKeyCount();
    }

    // For debugging - call this to force a refresh

}
