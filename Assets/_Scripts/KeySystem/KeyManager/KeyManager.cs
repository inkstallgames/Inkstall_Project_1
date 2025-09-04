using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

[System.Serializable]
public class KeyDetail
{
    public string type;
    public int scrollCount;
    public string _id;
    public string date;
}

[System.Serializable]
public class KeyResponse
{
    public string _id;
    public string studentId;
    public int freeKeys;
    public int totalKeys;
    public List<KeyDetail> keyDetails;
    public string lastUpdated;
    public int __v;
}

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int keysCount = 0;
    [SerializeField] private int totalKeys = 0;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private string apiBaseUrl = "https://api.inkstall.in/api/slot/get-keys/";
    [SerializeField] private string useKeysUrl = "https://api.inkstall.in/api/slot/use-keys/";

    // Hardcoded user ID for testing purposes
    public string studentId = "681ee0e6198ad04bf6c1c733";
    
    // Store the full key data for potential future use
    private KeyResponse keyData;

    // Flag to track if we've successfully fetched keys
    private bool hasInitializedKeys = false;

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
        
        // If studentId is not set, try to get it from GameDataManager
    if (string.IsNullOrEmpty(studentId) && GameDataManager.Instance != null)
    {
        studentId = GameDataManager.Instance.StudentId;
    }
    
    // Now fetch the data
    if (!string.IsNullOrEmpty(studentId))
    {
        FetchKeysFromDB();  // Or FetchCoins() for CoinsManager
    }
        
        // Schedule periodic refresh of keys (every 30 seconds)
        InvokeRepeating("FetchKeysFromDB", 30f, 30f);
    }

    // This will be called by UserIDBridge after it sets userId
    public void FetchKeysFromDB()
    {
        if (!string.IsNullOrEmpty(studentId))
        {
            StartCoroutine(FetchDBKeyCount());
        }
        else
        {
        }
    }

    // Fetch the key count from the API
    public IEnumerator FetchDBKeyCount()
    {
        
        string url = apiBaseUrl + studentId;
        
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.timeout = 15; // Set timeout to 15 seconds
        
        float startTime = Time.time;
        yield return www.SendWebRequest();
        float endTime = Time.time;
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            string json = www.downloadHandler.text;
            
            // Check if 'totalKeys' field exists in JSON
            bool hasTotalKeysField = json.Contains("\"totalKeys\":");
            
            try
            {
                // Instead of using JsonUtility, manually extract the totalKeys value
                int totalKeysValue = ExtractTotalKeysFromJson(json);
                
                // Update the key counts
                int oldCount = keysCount;
                keysCount = totalKeysValue;
                totalKeys = totalKeysValue;
                
                // Set initialization flag
                hasInitializedKeys = true;
                
                // Visual indicator for successful fetch
                StartCoroutine(ShowFetchSuccessIndicator());
                
                // Call UpdateUIKeyCount to update the UI
                UpdateUIKeyCount();
                
                // Also force a UI update to ensure it's displayed correctly
                ForceUpdateUI();
                
                // Verify UI was updated
                if (keyText != null)
                {
                }
                else
                {
                }
            }
            catch (System.Exception e)
            {
            }
        }
        else
        {
        }
    }
    
    // Helper method to extract totalKeys from MongoDB JSON format
    private int ExtractTotalKeysFromJson(string json)
    {
        
        try
        {
            // Find the totalKeys field
            int totalKeysIndex = json.IndexOf("\"totalKeys\":");
            if (totalKeysIndex < 0)
            {
                return 0;
            }
            
            // Extract the value after "totalKeys":
            string substring = json.Substring(totalKeysIndex + "\"totalKeys\":".Length);
            
            // Find the end of the value (comma or closing brace)
            int commaIndex = substring.IndexOf(",");
            int braceIndex = substring.IndexOf("}");
            
            int endIndex = -1;
            if (commaIndex >= 0 && braceIndex >= 0)
            {
                endIndex = Math.Min(commaIndex, braceIndex);
            }
            else if (commaIndex >= 0)
            {
                endIndex = commaIndex;
            }
            else if (braceIndex >= 0)
            {
                endIndex = braceIndex;
            }
            
            if (endIndex < 0)
            {
                return 0;
            }
            
            // Extract just the value
            string valueStr = substring.Substring(0, endIndex).Trim();
            
            // Parse to int
            int value;
            if (int.TryParse(valueStr, out value))
            {
                return value;
            }
            else
            {
                return 0;
            }
        }
        catch (System.Exception e)
        {
            return 0;
        }
    }
    
    // Show a visual indicator when keys are successfully fetched
    private IEnumerator ShowFetchSuccessIndicator()
    {
        // Optional: Change text color to indicate success
        if (keyText == null) yield break;
        
        Color originalColor = keyText.color;
        keyText.color = Color.green;
        
        yield return new WaitForSeconds(1.5f);
        
        // Return to original color
        keyText.color = originalColor;
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

        // Store current key count for UI display
        int previousKeyCount = keysCount;
        
        // Show a temporary "using key" state in the UI
        if (keyText != null)
        {
            // Show a visual indicator that key is being used
            keyText.text = (previousKeyCount - 1).ToString();
            keyText.color = Color.yellow; // Visual indicator that this is a temporary state
        }
        
        // Reduce local key count for internal tracking
        keysCount--;
        
        // Start the coroutine to use a key and update from database
        StartCoroutine(UseKeyCoroutine(previousKeyCount));
        
        return true;
    }
    
    private IEnumerator UseKeyCoroutine(int previousKeyCount)
    {
        // Use the exact format from the backend code: PATCH /api/slot/use-keys/:userId/:keysToUse
        int keysToUse = 1; // We're using 1 key at a time
        string url = "https://api.inkstall.in/api/slot/use-keys/" + studentId + "/" + keysToUse;
        
        // Create PATCH request (no body needed as parameters are in URL)
        UnityWebRequest webRequest = new UnityWebRequest(url, "PATCH");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");

        yield return webRequest.SendWebRequest();
        
        if (webRequest.downloadHandler != null && webRequest.downloadHandler.text != null)
        {
        }
        
        // Handle errors or success
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            // If there was an error, revert the key count change
            keysCount = previousKeyCount;
            
            // Reset UI to original color and value
            if (keyText != null)
            {
                keyText.text = previousKeyCount.ToString();
                keyText.color = Color.white;
            }
            
            // Visual feedback for error
            StartCoroutine(ShowErrorIndicator());
        }
        else
        {
            // After successful key use, fetch the latest count from database
            yield return StartCoroutine(FetchLatestKeyCount());
        }
    }
    
    // New method to fetch the latest key count after using a key
    private IEnumerator FetchLatestKeyCount()
    {
        
        // Wait a short delay to ensure the database has processed the previous request
        yield return new WaitForSeconds(0.5f);
        
        string url = apiBaseUrl + studentId;
        
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.timeout = 15; // Set timeout to 15 seconds
        
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            string json = www.downloadHandler.text;
            
            try
            {
                // Extract the totalKeys value
                int totalKeysValue = ExtractTotalKeysFromJson(json);
                
                // Update the key counts
                keysCount = totalKeysValue;
                totalKeys = totalKeysValue;
                
                // Update UI with the latest count from database
                if (keyText != null)
                {
                    keyText.text = keysCount.ToString();
                    keyText.color = Color.white; // Reset to normal color
                }
                
                // Visual indicator for successful fetch
                StartCoroutine(ShowUpdateSuccessIndicator());
                
            }
            catch (System.Exception e)
            {
            }
        }
        else
        {
        }
    }
    
    // Show a visual indicator for errors
    private IEnumerator ShowErrorIndicator()
    {
        // Change text color to indicate error
        if (keyText == null) yield break;
        
        Color originalColor = keyText.color;
        keyText.color = Color.red;
        
        yield return new WaitForSeconds(1.5f);
        
        // Return to original color
        keyText.color = originalColor;
    }
    
    // Update the UI text displaying the key count
    private void UpdateKeyText()
    {
        if (keyText != null)
        {
            keyText.text = keysCount.ToString();
        }
    }

    // Update the database with the current key count
    private void UpdateDBKeyCount()
    {
        StartCoroutine(SendKeyUpdateToDB());
    }

    private IEnumerator SendKeyUpdateToDB()
    {
        
        // Create a simple JSON object with just the totalKeys field
        string jsonData = "{\"totalKeys\":" + keysCount + "}";
        
        string url = apiBaseUrl + studentId;
        
        UnityWebRequest www = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();
        
        if (www.result != UnityWebRequest.Result.Success)
        {
        }
        else
        {
            // Visual feedback for successful update
            StartCoroutine(ShowUpdateSuccessIndicator());
        }
    }
    
    // Show a visual indicator when keys are successfully updated
    private IEnumerator ShowUpdateSuccessIndicator()
    {
        // Optional: Change text color to indicate success
        if (keyText == null) yield break;
        
        Color originalColor = keyText.color;
        keyText.color = Color.cyan;
        
        yield return new WaitForSeconds(1f);
        
        // Return to original color
        keyText.color = originalColor;
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
        UpdateUIKeyCount();
    }
    
    // For debugging - call this to force a refresh
    public void DebugRefreshKeys()
    {
        FetchKeysFromDB();
    }
}
