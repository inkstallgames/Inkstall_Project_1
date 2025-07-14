using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using UnityEngine.Events;

[Serializable]
public class KeyCountData
{
    public int keyCount;
}

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int startingKeys = 3;
    private int currentKeys;

    [Header("Database Settings")]
    [SerializeField] private string apiBaseUrl = "https://your-website-api.com/api";
    [SerializeField] private float apiRequestTimeout = 10f;
    [SerializeField] private bool useOfflineMode = false;

    [Header("User Authentication")]
    [SerializeField] private string userId = "";
    [SerializeField] private string authToken = "";

    [Header("Events")]
    public UnityEvent<int> OnKeysUpdated;

    private bool isInitialized = false;
    private bool isConnectingToDatabase = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize with starting keys as fallback
        currentKeys = startingKeys;
        
        if (OnKeysUpdated == null)
            OnKeysUpdated = new UnityEvent<int>();
    }

    private void Start()
    {
        // Fetch keys from database when the game starts
        StartCoroutine(InitializeKeysFromDatabase());
    }

    private IEnumerator InitializeKeysFromDatabase()
    {
        if (useOfflineMode)
        {
            Debug.Log("KeyManager: Using offline mode with " + startingKeys + " keys");
            isInitialized = true;
            OnKeysUpdated?.Invoke(currentKeys);
            yield break;
        }

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(authToken))
        {
            Debug.LogWarning("KeyManager: User ID or Auth Token not set. Using offline mode.");
            isInitialized = true;
            OnKeysUpdated?.Invoke(currentKeys);
            yield break;
        }

        isConnectingToDatabase = true;
        
        // Attempt to fetch keys from database
        using (UnityWebRequest webRequest = UnityWebRequest.Get($"{apiBaseUrl}/keys/{userId}"))
        {
            // Add authentication header
            webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
            webRequest.timeout = Mathf.RoundToInt(apiRequestTimeout);
            
            Debug.Log("KeyManager: Fetching keys from database...");
            
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    KeyCountData keyData = JsonUtility.FromJson<KeyCountData>(jsonResponse);
                    currentKeys = keyData.keyCount;
                    Debug.Log($"KeyManager: Successfully fetched {currentKeys} keys from database");
                }
                catch (Exception e)
                {
                    Debug.LogError($"KeyManager: Error parsing database response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"KeyManager: Failed to fetch keys from database. Error: {webRequest.error}");
            }
        }

        isConnectingToDatabase = false;
        isInitialized = true;
        OnKeysUpdated?.Invoke(currentKeys);
    }

    // Get the current number of keys
    public int GetKeyCount()
    {
        return currentKeys;
    }
    
    // Add keys to the player's inventory
    public void AddKeys(int amount)
    {
        if (amount <= 0) return;
        
        currentKeys += amount;
        OnKeysUpdated?.Invoke(currentKeys);
        
        // Update the database with new key count
        StartCoroutine(UpdateDBKeyCount());
    }
    
    // Use a key (called by LockedDoor when unlocking)
    public bool UseKey()
    {
        if (currentKeys > 0)
        {
            currentKeys--;
            OnKeysUpdated?.Invoke(currentKeys);
            StartCoroutine(UpdateDBKeyCount());
            return true;
        }
        return false;
    }

    // Update the key count in the database
    public IEnumerator UpdateDBKeyCount()
    {
        if (useOfflineMode || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(authToken))
        {
            Debug.Log("KeyManager: Skipping database update (offline mode or missing credentials)");
            yield break;
        }

        KeyCountData keyData = new KeyCountData { keyCount = currentKeys };
        string jsonData = JsonUtility.ToJson(keyData);

        using (UnityWebRequest webRequest = UnityWebRequest.Put($"{apiBaseUrl}/keys/{userId}", jsonData))
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {authToken}");
            webRequest.timeout = Mathf.RoundToInt(apiRequestTimeout);
            
            Debug.Log("KeyManager: Updating key count in database...");
            
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("KeyManager: Successfully updated key count in database");
            }
            else
            {
                Debug.LogError($"KeyManager: Failed to update key count in database. Error: {webRequest.error}");
            }
        }
    }
    
    // Check if the key manager has finished initializing
    public bool IsInitialized()
    {
        return isInitialized;
    }
    
    // Check if currently connecting to the database
    public bool IsConnectingToDatabase()
    {
        return isConnectingToDatabase;
    }
}
