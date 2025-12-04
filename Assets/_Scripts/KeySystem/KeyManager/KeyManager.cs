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
    [SerializeField] private int keysCount = 0;
    [SerializeField] private int totalKeys = 0;
    [SerializeField] private TextMeshProUGUI keyText;


    private void Awake()
    {
        // Set up singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Initialize the UI and fetch keys on start for testing

        // Check if keyText is assigned
        if (keyText == null)
        {

            // Try to find the key text component if not assigned
            keyText = GameObject.FindObjectOfType<TextMeshProUGUI>();
            if (keyText != null)
            {
            }
        }
        else
        {
            keyText.text = totalKeys.ToString();
        }

        // Load keys from PlayerPrefs
        LoadKeys();
    }

    private void LoadKeys()
    {
        keysCount = PlayerPrefs.GetInt("KeysCount", 0);
        totalKeys = PlayerPrefs.GetInt("TotalKeys", 0);
        UpdateUIKeyCount();
    }

    private void SaveKeys()
    {
        PlayerPrefs.SetInt("KeysCount", keysCount);
        PlayerPrefs.SetInt("TotalKeys", totalKeys);
        PlayerPrefs.Save();
    }

    public void AddKeys(int amount)
    {
        keysCount += amount;
        totalKeys += amount;
        SaveKeys();
        UpdateUIKeyCount();
    }





    // Update the UI to show the current key count
    private void UpdateUIKeyCount()
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

        // Update the UI text
        keyText.text = keysCount.ToString();

        // Verify the text was set correctly
    }

    // Returns the current key count as an integer
    public int GetCurrentKeyCount()
    {
        return keysCount;
    }

    // Returns the total key count as an integer
    public int GetTotalKeyCount()
    {
        return totalKeys;
    }

    /// <summary>
    /// Use a key and update the database
    /// </summary>
    /// <returns>True if key was successfully used, false otherwise</returns>
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
    }

    // For debugging - call this from other scripts or the Unity Editor
    public void DebugResetKeys()
    {
        keysCount = 0;
        totalKeys = 0;
        SaveKeys();
        UpdateUIKeyCount();
    }

    // For debugging - call this to force a refresh

}
