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
            Debug.Log("[KeyManager] Instance initialized");
        }
        else
        {
            Debug.Log("[KeyManager] Duplicate instance found, destroying this one");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Initialize the UI and fetch keys on start for testing
        Debug.Log(" [INIT] Start method called");
        Debug.Log(" [INIT] Initial keysCount: " + keysCount);
        Debug.Log(" [INIT] Initial totalKeys: " + totalKeys);
        
        // Check if keyText is assigned
        if (keyText == null)
        {
            Debug.LogError(" [INIT] keyText reference is null! UI will not update.");
            
            // Try to find the key text component if not assigned
            keyText = GameObject.FindObjectOfType<TextMeshProUGUI>();
            if (keyText != null)
            {
                Debug.Log(" [INIT] Found TextMeshProUGUI component automatically: " + keyText.name);
            }
        }
        else
        {
            Debug.Log(" [INIT] keyText reference is valid: " + keyText.name);
            keyText.text = totalKeys.ToString();
            Debug.Log(" [INIT] Initial UI text set to: " + keyText.text);
        }
        
        Debug.Log(" [INIT] Attempting to fetch keys from database with userId: " + studentId);
        FetchKeysFromDB();
        
        // Schedule periodic refresh of keys (every 30 seconds)
        InvokeRepeating("FetchKeysFromDB", 30f, 30f);
    }

    // This will be called by UserIDBridge after it sets userId
    public void FetchKeysFromDB()
    {
        Debug.Log(" [API CALL] FetchKeysFromDB called");
        if (!string.IsNullOrEmpty(studentId))
        {
            Debug.Log(" [API CALL] Valid userId found: " + studentId + ", starting fetch coroutine");
            StartCoroutine(FetchDBKeyCount());
        }
        else
        {
            Debug.LogError(" [API CALL] Cannot fetch keys: userId is empty");
        }
    }

    // Fetch the key count from the API
    public IEnumerator FetchDBKeyCount()
    {
        // Add these debug logs at the start
        Debug.Log(" [API CALL] Starting API call to fetch keys...");
        Debug.Log(" [API CALL] Current keysCount BEFORE fetch: " + keysCount);
        Debug.Log(" [API CALL] Current totalKeys BEFORE fetch: " + totalKeys);
        
        string url = apiBaseUrl + studentId;
        Debug.Log($" [API CALL] URL: {url}");
        
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.timeout = 15; // Set timeout to 15 seconds
        Debug.Log(" [API CALL] Web request created, sending...");
        
        float startTime = Time.time;
        yield return www.SendWebRequest();
        float endTime = Time.time;
        
        Debug.Log(" [API CALL] Request completed in " + (endTime - startTime).ToString("F2") + " seconds");
        Debug.Log(" [API CALL] Response code: " + www.responseCode);

        if (www.result == UnityWebRequest.Result.Success)
        {
            string json = www.downloadHandler.text;
            Debug.Log(" [API CALL] API Response received: " + json);
            
            // Check if 'totalKeys' field exists in JSON
            bool hasTotalKeysField = json.Contains("\"totalKeys\":");
            Debug.Log(" [API CALL] JSON contains totalKeys field: " + hasTotalKeysField);
            
            try
            {
                // Instead of using JsonUtility, manually extract the totalKeys value
                int totalKeysValue = ExtractTotalKeysFromJson(json);
                Debug.Log(" [API CALL] Successfully extracted totalKeys: " + totalKeysValue);
                
                // Update the key counts
                int oldCount = keysCount;
                keysCount = totalKeysValue;
                totalKeys = totalKeysValue;
                
                Debug.Log(" [API CALL] Keys updated: " + oldCount + " → " + keysCount);
                Debug.Log(" [API CALL] keysCount is now: " + keysCount);
                Debug.Log(" [API CALL] totalKeys is now: " + totalKeys);
                
                // Set initialization flag
                hasInitializedKeys = true;
                
                // Visual indicator for successful fetch
                StartCoroutine(ShowFetchSuccessIndicator());
                
                // Call UpdateUIKeyCount to update the UI
                Debug.Log(" [API CALL] Calling UpdateUIKeyCount to update UI...");
                UpdateUIKeyCount();
                
                // Also force a UI update to ensure it's displayed correctly
                Debug.Log(" [API CALL] Also forcing UI update to ensure correct display...");
                ForceUpdateUI();
                
                // Verify UI was updated
                if (keyText != null)
                {
                    Debug.Log(" [API CALL] After UI update, keyText.text = " + keyText.text);
                }
                else
                {
                    Debug.LogError(" [API CALL] keyText is null after update!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(" [API CALL] Error parsing JSON response: " + e.Message);
                Debug.LogError(" [API CALL] JSON that failed to parse: " + json);
            }
        }
        else
        {
            Debug.LogError(" [API CALL] Error fetching keys: " + www.error);
            Debug.LogError(" [API CALL] Response code: " + www.responseCode);
            if (www.downloadHandler != null && www.downloadHandler.text != null)
            {
                Debug.LogError(" [API CALL] Error response body: " + www.downloadHandler.text);
            }
        }
    }
    
    // Helper method to extract totalKeys from MongoDB JSON format
    private int ExtractTotalKeysFromJson(string json)
    {
        Debug.Log(" [JSON PARSE] Attempting to extract totalKeys from JSON");
        
        try
        {
            // Find the totalKeys field
            int totalKeysIndex = json.IndexOf("\"totalKeys\":");
            if (totalKeysIndex < 0)
            {
                Debug.LogError(" [JSON PARSE] Could not find totalKeys field in JSON");
                return 0;
            }
            
            // Extract the value after "totalKeys":
            string substring = json.Substring(totalKeysIndex + "\"totalKeys\":".Length);
            Debug.Log(" [JSON PARSE] Substring after totalKeys: " + substring.Substring(0, Math.Min(20, substring.Length)) + "...");
            
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
                Debug.LogError(" [JSON PARSE] Could not find end of totalKeys value");
                return 0;
            }
            
            // Extract just the value
            string valueStr = substring.Substring(0, endIndex).Trim();
            Debug.Log(" [JSON PARSE] Extracted value string: " + valueStr);
            
            // Parse to int
            int value;
            if (int.TryParse(valueStr, out value))
            {
                Debug.Log(" [JSON PARSE] Successfully parsed totalKeys: " + value);
                return value;
            }
            else
            {
                Debug.LogError(" [JSON PARSE] Failed to parse totalKeys value: " + valueStr);
                return 0;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(" [JSON PARSE] Exception while extracting totalKeys: " + e.Message);
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
        Debug.Log(" [UI UPDATE] UpdateUIKeyCount called, keysCount=" + keysCount + ", totalKeys=" + totalKeys);
        
        // Check if keyText is assigned
        if (keyText == null)
        {
            Debug.LogError(" [UI UPDATE] keyText is null! Cannot update UI.");
            
            // Try to find the key text component if not assigned
            keyText = GameObject.FindObjectOfType<TextMeshProUGUI>();
            if (keyText != null)
            {
                Debug.Log(" [UI UPDATE] Found TextMeshProUGUI component automatically: " + keyText.name);
            }
            else
            {
                return;
            }
        }
        
        // Update the UI text
        keyText.text = keysCount.ToString();
        
        // Verify the text was set correctly
        Debug.Log(" [UI UPDATE] UI updated - Text set to: " + keyText.text);
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
            Debug.LogWarning("No keys available to use!");
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
        Debug.Log(" [API CALL] Using key at URL: " + url);
        Debug.Log(" [API CALL] Current student ID: " + studentId);
        Debug.Log(" [API CALL] Current keys count before API call: " + previousKeyCount);
        Debug.Log(" [API CALL] HTTP Method: PATCH");
        
        // Create PATCH request (no body needed as parameters are in URL)
        UnityWebRequest webRequest = new UnityWebRequest(url, "PATCH");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");

        Debug.Log(" [API CALL] Web request created, sending...");
        yield return webRequest.SendWebRequest();
        Debug.Log(" [API CALL] Update request completed with result: " + webRequest.result);
        Debug.Log(" [API CALL] Response code: " + webRequest.responseCode);
        
        if (webRequest.downloadHandler != null && webRequest.downloadHandler.text != null)
        {
            Debug.Log(" [API CALL] Response body: " + webRequest.downloadHandler.text);
        }
        
        // Log all response headers for debugging
        Debug.Log(" [API CALL] Response Headers:");
        var responseHeaders = webRequest.GetResponseHeaders();
        if (responseHeaders != null)
        {
            foreach (var header in responseHeaders)
            {
                Debug.Log($" [API CALL] Header: {header.Key}: {header.Value}");
            }
        }
        
        // Handle errors or success
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(" [API CALL] Error using key: " + webRequest.error);
            Debug.LogError(" [API CALL] Response code: " + webRequest.responseCode);
            if (webRequest.downloadHandler != null && webRequest.downloadHandler.text != null)
            {
                Debug.LogError(" [API CALL] Error response body: " + webRequest.downloadHandler.text);
            }
            
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
        Debug.Log(" [API CALL] Fetching latest key count to ensure UI accuracy");
        
        // Wait a short delay to ensure the database has processed the previous request
        yield return new WaitForSeconds(0.5f);
        
        string url = apiBaseUrl + studentId;
        Debug.Log($" [API CALL] URL: {url}");
        
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.timeout = 15; // Set timeout to 15 seconds
        Debug.Log(" [API CALL] Web request created, sending...");
        
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            string json = www.downloadHandler.text;
            Debug.Log(" [API CALL] API Response received: " + json);
            
            try
            {
                // Extract the totalKeys value
                int totalKeysValue = ExtractTotalKeysFromJson(json);
                Debug.Log(" [API CALL] Successfully extracted totalKeys: " + totalKeysValue);
                
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
                
                Debug.Log(" [API CALL] Latest key count fetched and UI updated: " + keysCount);
            }
            catch (System.Exception e)
            {
                Debug.LogError(" [API CALL] Error parsing JSON response: " + e.Message);
            }
        }
        else
        {
            Debug.LogError(" [API CALL] Error fetching latest keys: " + www.error);
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
        Debug.Log(" [API CALL] UpdateDBKeyCount called, starting coroutine");
        StartCoroutine(SendKeyUpdateToDB());
    }

    private IEnumerator SendKeyUpdateToDB()
    {
        Debug.Log(" [API CALL] SendKeyUpdateToDB coroutine started");
        
        // Create a simple JSON object with just the totalKeys field
        string jsonData = "{\"totalKeys\":" + keysCount + "}";
        Debug.Log(" [API CALL] Sending update to DB with JSON: " + jsonData);
        
        string url = apiBaseUrl + studentId;
        Debug.Log(" [API CALL] Sending update to URL: " + url);
        
        UnityWebRequest www = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        Debug.Log(" [API CALL] Web request created, sending...");
        yield return www.SendWebRequest();
        Debug.Log(" [API CALL] Update request completed with result: " + www.result);

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(" [API CALL] Failed to update key count: " + www.error);
            Debug.LogError(" [API CALL] Response code: " + www.responseCode);
            if (www.downloadHandler != null && www.downloadHandler.text != null)
            {
                Debug.LogError(" [API CALL] Error response body: " + www.downloadHandler.text);
            }
        }
        else
        {
            Debug.Log(" [API CALL] Key count updated successfully in database");
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
        Debug.Log(" [FORCE UI] ForceUpdateUI called");
        Debug.Log(" [FORCE UI] Current keysCount: " + keysCount);
        Debug.Log(" [FORCE UI] Current totalKeys: " + totalKeys);
        
        // Check if keyText is assigned
        if (keyText == null)
        {
            Debug.LogError(" [FORCE UI] keyText is null! Cannot update UI.");
            
            // Try to find the key text component if not assigned
            keyText = GameObject.FindObjectOfType<TextMeshProUGUI>();
            if (keyText != null)
            {
                Debug.Log(" [FORCE UI] Found TextMeshProUGUI component automatically: " + keyText.name);
            }
            else
            {
                return;
            }
        }
        
        // Update the UI text directly
        keyText.text = keysCount.ToString();
        Debug.Log(" [FORCE UI] UI text set to: " + keyText.text);
        
        // Force UI refresh
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            Debug.Log(" [FORCE UI] Refreshing canvas: " + canvas.name);
            canvas.enabled = false;
            canvas.enabled = true;
        }
        
        // Force TMPro to update
        keyText.ForceMeshUpdate();
        
        Debug.Log(" [FORCE UI] UI refresh complete");
    }
    
    // For debugging - call this from other scripts or the Unity Editor
    public void DebugResetKeys()
    {
        keysCount = 0;
        totalKeys = 0;
        UpdateUIKeyCount();
        Debug.Log(" [DEBUG] Keys reset to 0");
    }
    
    // For debugging - call this to force a refresh
    public void DebugRefreshKeys()
    {
        Debug.Log(" [DEBUG] Manual refresh requested");
        FetchKeysFromDB();
    }
}
