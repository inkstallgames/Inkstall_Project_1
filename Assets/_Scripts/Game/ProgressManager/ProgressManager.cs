using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[Serializable]
public class DoorData
{
    public int doorId;
    public string name;
    public bool isUnlockable;
    public bool isRoomCompleted;
    public string description;
}

[Serializable]
public class StudentDoorsData
{
    public List<DoorData> doors;
}

public class ProgressManager : MonoBehaviour
{
    private static ProgressManager _instance;
    public static ProgressManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ProgressManager");
                _instance = go.AddComponent<ProgressManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    [System.Serializable]
    private class LocalDoorState
    {
        public int doorId;
        public bool isUnlockable;
        public bool isRoomCompleted;
        public long lastUpdated;
    }
    
    [System.Serializable]
    private class LocalDoorStatesWrapper
    {
        public List<LocalDoorState> doors = new List<LocalDoorState>();
    }

    [Header("Retry Settings")]
    [SerializeField] private float initialRetryDelay = 2f;
    [SerializeField] private int maxRetryAttempts = 3;
    private int currentRetryAttempt = 0;

    [Header("Door Settings")]
    [SerializeField] private int maxDoorId = 24; // Maximum door ID in the game (24 doors across 4 buildings)
    
    private string studentId = ""; // Will be set from StudentIdManager
    private string baseUrl = "https://api.inkstall.in/api/student-portal/doors";
    
    public StudentDoorsData studentData;
    public bool isDataLoaded = false;
    
    // Add a static event that doors can subscribe to
    public static event Action OnDataLoaded = delegate { };
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Initialize the student data if it's null
        if (studentData == null)
        {
            studentData = new StudentDoorsData();
            studentData.doors = new List<DoorData>();
        }
        
        // Get the student ID from StudentIdManager or PlayerPrefs
        GetStudentId();
        
        if (!string.IsNullOrEmpty(studentId))
        {
            // Load data from server
            LoadStudentDoorData();
        }
        #if UNITY_EDITOR
        else
        {
            studentId = "default_test_id";
            LoadStudentDoorData();
        }
        #endif
        
        // Start a coroutine to check if data is loaded after a delay
        StartCoroutine(CheckDataLoadedAfterDelay());
    }
    
    // Coroutine to check if data is loaded after a delay
    private IEnumerator CheckDataLoadedAfterDelay()
    {
        // Wait a short time to allow normal loading process to complete
        yield return new WaitForSeconds(5f);
        
        if (!isDataLoaded)
        {
            CreateDefaultDoorData();
        }
    }
    
    public bool IsDataLoaded()
    {
        return isDataLoaded;
    }
    
    // Get student ID from StudentIdManager or directly from PlayerPrefs
    public string GetStudentId()
    {
        // First check if we already have the student ID
        if (!string.IsNullOrEmpty(studentId))
        {
            return studentId;
        }

        // Try to get from StudentIdManager
        if (StudentIdManager.Instance != null)
        {
            string id = StudentIdManager.Instance.GetStudentId();
            if (!string.IsNullOrEmpty(id))
            {
                studentId = id;
                return studentId;
            }
            
            // Subscribe to StudentIdManager events to get the ID when it becomes available
            StudentIdManager.Instance.OnStudentIdLoaded += HandleStudentIdLoaded;
        }
        
        // If still empty, try PlayerPrefs directly
        if (string.IsNullOrEmpty(studentId))
        {
            studentId = PlayerPrefs.GetString("StudentId", "");
        }
        
        return studentId;
    }
    
    private void HandleStudentIdLoaded(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        
        // Unsubscribe to avoid multiple calls
        if (StudentIdManager.Instance != null)
        {
            StudentIdManager.Instance.OnStudentIdLoaded -= HandleStudentIdLoaded;
        }
        
        // Set the student ID and load the data
        studentId = id;
        LoadStudentDoorData();
    }

    public void LoadStudentDoorData(bool isRetry = false)
    {
        if (!isRetry)
        {
            currentRetryAttempt = 0;
        }

        if (string.IsNullOrEmpty(studentId)) return;
        if (isRetry && currentRetryAttempt >= maxRetryAttempts) return;
        
        if (isRetry)
        {
            currentRetryAttempt++;
        }

        StartCoroutine(LoadStudentDoorDataCoroutine(isRetry));
    }

    private IEnumerator LoadStudentDoorDataCoroutine(bool isRetry = false)
    {
        if (string.IsNullOrEmpty(studentId))
        {
            yield break;
        }
        
        // If data is already loaded and this is not a retry, don't reload
        if (isDataLoaded && !isRetry)
        {
            yield break;
        }
        
        isDataLoaded = false;
        string url = $"{baseUrl}/student/{studentId}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                // Retry with exponential backoff
                if (currentRetryAttempt < maxRetryAttempts)
                {
                    float retryDelay = initialRetryDelay * Mathf.Pow(2, currentRetryAttempt);
                    yield return new WaitForSeconds(retryDelay);
                    LoadStudentDoorData(true);
                    yield break;
                }
                
                // Check if the student exists
                if (webRequest.responseCode == 404)
                {
                    yield return StartCoroutine(CreateNewStudentData());
                }
                else
                {
                    // Create default door data if server request fails
                    CreateDefaultDoorData();
                }
            }
            else
            {
                // Parse the response
                string jsonResponse = webRequest.downloadHandler.text;
                
                try
                {
                    // First try standard JSON deserialization
                    studentData = JsonConvert.DeserializeObject<StudentDoorsData>(jsonResponse);
                    
                    if (studentData == null || studentData.doors == null || studentData.doors.Count == 0)
                    {
                        // If standard deserialization fails or returns empty data, try MongoDB format parsing
                        ParseMongoDBJson(jsonResponse);
                    }
                    
                    isDataLoaded = true;
                    
                    // Update all door interactions in the scene
                    UpdateAllDoorInteractions();
                    
                    // Notify subscribers that data is loaded
                    OnDataLoaded?.Invoke();
                }
                catch (Exception parseEx)
                {
                    try
                    {
                        // Try MongoDB format parsing
                        ParseMongoDBJson(jsonResponse);
                        
                        isDataLoaded = true;
                        UpdateAllDoorInteractions();
                        OnDataLoaded?.Invoke();
                    }
                    catch (Exception mongoEx)
                    {
                        // Create default door data when parsing fails
                        CreateDefaultDoorData();
                    }
                }
            }
        }
    }

    private IEnumerator CreateNewStudentData()
    {
        string url = $"{baseUrl}/initialize/{studentId}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.PostWwwForm(url, ""))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result != UnityWebRequest.Result.ConnectionError && 
                webRequest.result != UnityWebRequest.Result.ProtocolError)
            {
                // Load the newly created data
                yield return LoadStudentDoorDataCoroutine();
            }
        }
    }

    public IEnumerator UpdateDoorStatus(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Skip if studentId is not set
        if (string.IsNullOrEmpty(studentId))
        {
            yield break;
        }

        // Skip processing for invalid door IDs
        if (doorId < 1 || doorId > 24)
        {
            yield break;
        }
        
        // Check if the status is already what we want to set
        var existingDoor = studentData?.doors?.Find(d => d.doorId == doorId);
        if (existingDoor != null && 
            existingDoor.isUnlockable == isUnlockable && 
            existingDoor.isRoomCompleted == isRoomCompleted)
        {
            yield break;
        }
        
        string url = $"{baseUrl}/{studentId}/{doorId}";
        
        // Create a proper JSON structure that matches the API expectations
        string jsonPayload = $"{{\"isUnlockable\": {isUnlockable.ToString().ToLower()}, \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}}}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Put(url, jsonPayload))
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result != UnityWebRequest.Result.ConnectionError && 
                webRequest.result != UnityWebRequest.Result.ProtocolError)
            {
                // Update local data
                if (studentData != null && studentData.doors != null)
                {
                    DoorData door = studentData.doors.Find(d => d.doorId == doorId);
                    if (door != null)
                    {
                        door.isUnlockable = isUnlockable;
                        door.isRoomCompleted = isRoomCompleted;
                    }
                }
                
                // Force reload data to verify changes
                StartCoroutine(ReloadDataAfterDelay(1.0f));
            }
        }
    }

    private IEnumerator VerifyDoorUpdate(int doorId, bool expectedUnlockable, bool expectedCompleted)
    {
        // Skip verification for invalid door IDs
        if (doorId < 1 || doorId > 24) yield break;
        
        // Wait a bit to ensure the server has processed the update
        yield return new WaitForSeconds(0.5f);
        
        string url = $"{baseUrl}/student/{studentId}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result != UnityWebRequest.Result.ConnectionError && 
                webRequest.result != UnityWebRequest.Result.ProtocolError)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                
                try
                {
                    StudentDoorsData verifiedData = JsonConvert.DeserializeObject<StudentDoorsData>(jsonResponse);
                    DoorData verifiedDoor = verifiedData.doors.Find(d => d.doorId == doorId);
                    
                    if (verifiedDoor != null)
                    {
                        // Update our local data with the verified data
                        studentData = verifiedData;
                        isDataLoaded = true;
                        
                        // Process any pending door updates
                        ProcessPendingDoorUpdates();
                        
                        // Notify subscribers that data has been updated
                        OnDataLoaded?.Invoke();
                    }
                }
                catch (Exception)
                {
                    // Silently handle the error
                }
            }
        }
    }
    
    // Mark a room as completed and unlock the next door
    public void MarkRoomAsCompleted(int doorId)
    {
        bool isValidDoor = doorId >= 1 && doorId <= 24;
        
        if (!isValidDoor)
        {
            return;
        }
        
        if (string.IsNullOrEmpty(studentId))
        {
            return;
        }
        
        if (studentData?.doors == null)
        {
            return;
        }
        
        // Find the door data
        DoorData door = studentData.doors.Find(d => d.doorId == doorId);
        if (door == null)
        {
            EnsureDoorDataExists(doorId, $"Door {doorId}");
            door = studentData.doors.Find(d => d.doorId == doorId);
            
            if (door == null)
            {
                return;
            }
        }
        
        if (door.isRoomCompleted)
        {
            return;
        }
        
        // First update the current door (set isUnlockable=false, isRoomCompleted=true)
        UpdateDoorStatusDirect(doorId, false, true);
        
        // Check if this is the last door in a building (doors 6, 12, 18, 24)
        bool isLastDoorInBuilding = (doorId == 6 || doorId == 12 || doorId == 18 || doorId == 24);
        
        // If it's the last door in a building, we don't need to unlock the next door
        if (!isLastDoorInBuilding && doorId < maxDoorId)
        {
            // Find the next door by ID
            int nextDoorId = doorId + 1;
            DoorData nextDoor = studentData.doors.Find(d => d.doorId == nextDoorId);
            
            if (nextDoor != null)
            {
                // Update the next door (set isUnlockable=true, keep isRoomCompleted as is)
                UpdateDoorStatusDirect(nextDoorId, true, nextDoor.isRoomCompleted);
                
                // Verify the updates were applied for the next door
                StartCoroutine(VerifyDatabaseUpdates(nextDoorId, true, nextDoor.isRoomCompleted));
                
                // Update local door instances in the scene
                UpdateDoorInstancesInScene(doorId, nextDoorId);
            }
            else
            {
                // Try to create the next door
                EnsureDoorDataExists(nextDoorId, $"Door {nextDoorId}");
            }
        }
        else
        {
            // Just update the current door instance in the scene
            UpdateDoorInstancesInScene(doorId, -1);
        }
        
        // Force reload data from server after updates
        StartCoroutine(ReloadDataAfterDelay(1.0f));
        
        // Verify the updates were applied for the current door
        StartCoroutine(VerifyDatabaseUpdates(doorId, false, true));
    }
    
    // Verify that database updates were applied correctly
    private IEnumerator VerifyDatabaseUpdates(int doorId, bool expectedUnlockable, bool expectedCompleted)
    {
        // Wait a bit to ensure the server has processed the update
        yield return new WaitForSeconds(2.0f);
        
        string url = $"{baseUrl}/student/{studentId}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result != UnityWebRequest.Result.ConnectionError && 
                webRequest.result != UnityWebRequest.Result.ProtocolError)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                
                try
                {
                    StudentDoorsData verifiedData = JsonConvert.DeserializeObject<StudentDoorsData>(jsonResponse);
                    
                    if (verifiedData == null || verifiedData.doors == null)
                    {
                        yield break;
                    }
                    
                    DoorData verifiedDoor = verifiedData.doors.Find(d => d.doorId == doorId);
                    
                    if (verifiedDoor != null)
                    {
                        bool updateSuccessful = verifiedDoor.isUnlockable == expectedUnlockable && 
                                               verifiedDoor.isRoomCompleted == expectedCompleted;
                        
                        if (updateSuccessful)
                        {
                        }
                        else
                        {
                            // Try a different API endpoint as a fallback
                            StartCoroutine(UpdateDoorStatusFallback(doorId, expectedUnlockable, expectedCompleted));
                        }
                        
                        // Update our local data with the verified data
                        studentData = verifiedData;
                        isDataLoaded = true;
                        
                        // Process any pending door updates
                        ProcessPendingDoorUpdates();
                        
                        // Notify subscribers that data has been updated
                        OnDataLoaded?.Invoke();
                    }
                    else
                    {
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VERIFY_DB_ERROR] Exception during verification of door {doorId}: {ex.Message}");
                    Debug.LogError($"[VERIFY_DB_ERROR] Stack trace: {ex.StackTrace}");
                }
            }
            else
            {
                Debug.LogError($"[VERIFY_DB_ERROR] Web request failed during verification of door {doorId}: {webRequest.error}");
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    Debug.LogError($"[VERIFY_DB_ERROR] Response: {webRequest.downloadHandler.text}");
                }
            }
        }
    }
    
    // Fallback method to update door status using a different API endpoint
    public IEnumerator UpdateDoorStatusFallback(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Skip processing for invalid door IDs
        if (doorId < 1 || doorId > 24)
        {
            Debug.LogError($"[DOOR_UPDATE_FALLBACK] Invalid doorId: {doorId}");
            yield break;
        }

        // Try a different API endpoint format
        string url = $"{baseUrl}/update/door";
        
        // Create a more detailed JSON structure that includes the studentId and doorId
        string jsonPayload = $"{{\"studentId\": \"{studentId}\", \"doorId\": {doorId}, \"isUnlockable\": {isUnlockable.ToString().ToLower()}, \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}}}";
        
        Debug.Log($"[DOOR_UPDATE_FALLBACK] Attempting fallback update for door {doorId}");
        Debug.Log($"[DOOR_UPDATE_FALLBACK] URL: {url}");
        Debug.Log($"[DOOR_UPDATE_FALLBACK] Payload: {jsonPayload}");
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST")) // Changed to POST for this endpoint
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            // Add timeout to prevent hanging
            webRequest.timeout = 15; // Increased timeout for fallback
            
            yield return webRequest.SendWebRequest();
            
            Debug.Log($"[DOOR_UPDATE_FALLBACK_RESPONSE] Door {doorId} - Status: {webRequest.responseCode}, Result: {webRequest.result}");
            if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
            {
                Debug.Log($"[DOOR_UPDATE_FALLBACK_RESPONSE] Response text: {webRequest.downloadHandler.text}");
            }
            
            if (webRequest.responseCode == 200 || webRequest.responseCode == 201 || webRequest.responseCode == 204)
            {
                Debug.Log($"[DOOR_UPDATE_FALLBACK] Successfully updated door {doorId} via fallback");
                
                // Update local data to match server state
                UpdateLocalDoorData(doorId, isUnlockable, isRoomCompleted);
                
                // Force reload data to verify changes
                StartCoroutine(ReloadDataAfterDelay(1.0f));
            }
            else
            {
                string errorMsg = $"Failed to update door {doorId} via fallback. Error: {webRequest.error}, Response Code: {webRequest.responseCode}";
                if (webRequest.downloadHandler != null && !string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    errorMsg += $"\nResponse: {webRequest.downloadHandler.text}";
                }
                Debug.LogError($"[DOOR_UPDATE_FALLBACK] {errorMsg}");
                
                // Try one more time with a different endpoint as last resort
                yield return StartCoroutine(LastResortUpdate(doorId, isUnlockable, isRoomCompleted));
            }
        }
    }
    
    // Last resort update method with a different endpoint format
    private IEnumerator LastResortUpdate(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        Debug.Log($"[LAST_RESORT_UPDATE] Trying last resort update for door {doorId}");
        
        // Try a completely different endpoint format - direct to the API endpoint
        string url = "https://api.inkstall.in/api/student-portal/doors/update-door-status";
        
        // Create a more detailed JSON payload with all required fields
        string jsonPayload = $"{{\"studentId\": \"{studentId}\", " +
                            $"\"doorId\": {doorId}, " +
                            $"\"isUnlockable\": {isUnlockable.ToString().ToLower()}, " +
                            $"\"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}, " +
                            $"\"timestamp\": \"{System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}\"}}";
        
        Debug.Log($"[LAST_RESORT_UPDATE] URL: {url}");
        Debug.Log($"[LAST_RESORT_UPDATE] Payload: {jsonPayload}");
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = 20; // Longer timeout for last attempt
            
            yield return webRequest.SendWebRequest();
            
            Debug.Log($"[LAST_RESORT_RESPONSE] Door {doorId} - Status: {webRequest.responseCode}, Result: {webRequest.result}");
            if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
            {
                Debug.Log($"[LAST_RESORT_RESPONSE] Response text: {webRequest.downloadHandler.text}");
            }
            
            // Accept any 2xx status code as success
            if (webRequest.responseCode >= 200 && webRequest.responseCode < 300)
            {
                Debug.Log($"[LAST_RESORT_UPDATE] Successfully updated door {doorId} via last resort method");
                
                // Update local data to match what we tried to set
                UpdateLocalDoorData(doorId, isUnlockable, isRoomCompleted);
                
                // Force reload data to verify changes
                StartCoroutine(ReloadDataAfterDelay(1.5f));
                
                // Try to parse the response to confirm update
                try {
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        string responseText = webRequest.downloadHandler.text;
                        if (responseText.Contains("success") || responseText.Contains("updated"))
                        {
                            Debug.Log($"[LAST_RESORT_UPDATE] Server confirmed update success for door {doorId}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LAST_RESORT_UPDATE] Could not parse response: {ex.Message}");
                }
            }
            else
            {
                string errorMsg = $"[LAST_RESORT_UPDATE] Final attempt failed for door {doorId}. Error: {webRequest.error}, Response Code: {webRequest.responseCode}";
                if (!string.IsNullOrEmpty(webRequest.downloadHandler?.text))
                {
                    errorMsg += $"\nResponse: {webRequest.downloadHandler.text}";
                }
                Debug.LogError(errorMsg);
                
                // Even if the server update failed, we'll update the local data to maintain consistency
                // This ensures the game remains playable even if the server is down
                UpdateLocalDoorData(doorId, isUnlockable, isRoomCompleted);
                
                // Also update the door in the local cache to ensure it's consistent
                SaveDoorToLocalCache(doorId, isUnlockable, isRoomCompleted);
            }
        }
    }
    
    // Save door state to local cache for offline consistency
    private void SaveDoorToLocalCache(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        try
        {
            // Get existing cache or create new
            string cacheKey = $"DoorCache_{studentId}";
            string cachedData = PlayerPrefs.GetString(cacheKey, "{\"doors\":[]}")
;
            
            // Parse the cached data
            LocalDoorStatesWrapper doorCache;
            try
            {
                doorCache = JsonConvert.DeserializeObject<LocalDoorStatesWrapper>(cachedData);
                if (doorCache == null) doorCache = new LocalDoorStatesWrapper();
                if (doorCache.doors == null) doorCache.doors = new List<LocalDoorState>();
            }
            catch
            {
                doorCache = new LocalDoorStatesWrapper();
                doorCache.doors = new List<LocalDoorState>();
            }
            
            // Find or create door entry
            LocalDoorState doorState = doorCache.doors.Find(d => d.doorId == doorId);
            if (doorState == null)
            {
                doorState = new LocalDoorState
                {
                    doorId = doorId,
                    isUnlockable = isUnlockable,
                    isRoomCompleted = isRoomCompleted,
                    lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                doorCache.doors.Add(doorState);
            }
            else
            {
                doorState.isUnlockable = isUnlockable;
                doorState.isRoomCompleted = isRoomCompleted;
                doorState.lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            
            // Save back to PlayerPrefs
            string updatedCache = JsonConvert.SerializeObject(doorCache);
            PlayerPrefs.SetString(cacheKey, updatedCache);
            PlayerPrefs.Save();
            
            Debug.Log($"[DOOR_CACHE] Saved door {doorId} to local cache: isUnlockable={isUnlockable}, isRoomCompleted={isRoomCompleted}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DOOR_CACHE] Error saving door to cache: {ex.Message}");
        }
    }
    
    // Update door status with direct HTTP request
    public IEnumerator UpdateDoorStatusDirect(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Skip if studentId is not set
        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("[DOOR_UPDATE_ERROR] Cannot update door status: studentId is not set");
            yield break;
        }

        // Skip processing for invalid door IDs
        if (doorId < 1 || doorId > 24)
        {
            Debug.LogError($"[DOOR_UPDATE_ERROR] Invalid doorId: {doorId}");
            yield break;
        }

        string url = $"{baseUrl}/{studentId}/{doorId}";
        
        // Create the JSON structure
        string jsonPayload = $"{{\"isUnlockable\": {isUnlockable.ToString().ToLower()}, \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}}}";
        
        Debug.Log($"[DOOR_UPDATE_START] Door {doorId} update request: URL={url}");
        Debug.Log($"[DOOR_UPDATE_PAYLOAD] {jsonPayload}");
        
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = 10; // Add timeout to prevent hanging
            
            Debug.Log($"[DOOR_UPDATE_REQUEST] Sending PUT request to {url}");
            
            // Send the request and wait for it to complete
            yield return webRequest.SendWebRequest();
            
            // Log the response details
            Debug.Log($"[DOOR_UPDATE_RESPONSE] Door {doorId} - Status: {webRequest.responseCode}, Result: {webRequest.result}");
            
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseText = webRequest.downloadHandler.text;
                Debug.Log($"[DOOR_UPDATE_SUCCESS] Door {doorId} updated successfully. Response: {responseText}");
                
                // Update local data to match server state
                UpdateLocalDoorData(doorId, isUnlockable, isRoomCompleted);
                
                // Force reload data to verify changes
                StartCoroutine(ReloadDataAfterDelay(1.0f));
            }
            else
            {
                string errorDetails = $"Error: {webRequest.error}\n";
                errorDetails += $"Response Code: {webRequest.responseCode}\n";
                errorDetails += $"Response: {webRequest.downloadHandler.text}";
                
                Debug.LogError($"[DOOR_UPDATE_ERROR] Failed to update door {doorId}:\n{errorDetails}");
                
                // Try fallback update method if the main one fails
                yield return StartCoroutine(UpdateDoorStatusFallback(doorId, isUnlockable, isRoomCompleted));
            }
        }
    }
    
    private void UpdateLocalDoorData(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        if (studentData?.doors == null)
        {
            Debug.LogError("[DOOR_UPDATE_LOCAL] studentData or doors is null");
            return;
        }
        
        DoorData door = studentData.doors.Find(d => d.doorId == doorId);
        if (door != null)
        {
            door.isUnlockable = isUnlockable;
            door.isRoomCompleted = isRoomCompleted;
            Debug.Log($"[DOOR_UPDATE_LOCAL] Updated local door {doorId}: Unlockable={isUnlockable}, Completed={isRoomCompleted}");
        }
        else
        {
            Debug.LogWarning($"[DOOR_UPDATE_LOCAL] Door {doorId} not found in local data, adding new entry");
            studentData.doors.Add(new DoorData 
            { 
                doorId = doorId, 
                isUnlockable = isUnlockable, 
                isRoomCompleted = isRoomCompleted 
            });
        }
    }
    

    // Force reload data from server after a delay
    private IEnumerator ReloadDataAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadStudentDoorData();
    }

    private void UpdateAllDoorInteractions()
    {
        try
        {
            if (!isDataLoaded || studentData?.doors == null)
            {
                // Debug.LogWarning("[ProgressManager] Cannot update door interactions: data not loaded or studentData is null");
                return;
            }
                
            // Find all door interactions in the scene
            DoorInteraction[] doorInteractions = FindObjectsOfType<DoorInteraction>();
            
            // Only log if we find doors 1-24 in the scene
            if (doorInteractions.Any(d => d != null && d.doorID >= 1 && d.doorID <= 24))
            {
                Debug.Log($"[ProgressManager] Found {doorInteractions.Count(d => d != null && d.doorID >= 1 && d.doorID <= 24)} doors with IDs 1-24 in the scene");
                Debug.Log($"[ProgressManager] Student data contains {studentData.doors.Count(d => d.doorId >= 1 && d.doorId <= 24)} doors with IDs 1-24");
                
                // Log the first few doors in student data for debugging (only doors 1-24)
                foreach (var door in studentData.doors.Where(d => d.doorId >= 1 && d.doorId <= 24).Take(5))
                {
                    Debug.Log($"[ProgressManager] Door {door.doorId}: isUnlockable={door.isUnlockable}, isRoomCompleted={door.isRoomCompleted}");
                }
            }
            
            int updatedCount = 0;
            int invalidCount = 0;
            
            foreach (DoorInteraction door in doorInteractions)
            {
                if (door != null)
                {
                    int doorId = door.doorID;
                    
                    if (doorId >= 1 && doorId <= 24)
                    {
                        UpdateDoorInteraction(door);
                        updatedCount++;
                    }
                    else
                    {
                        invalidCount++;
                    }
                }
            }
            
            // Only log if we updated doors with IDs 1-24
            if ((updatedCount > 0 || invalidCount > 0) && doorInteractions.Any(d => d != null && d.doorID >= 1 && d.doorID <= 24))
            {
                Debug.Log($"[ProgressManager] Updated {updatedCount} doors, skipped {invalidCount} invalid doors");
            }
            
            // Process any pending door updates
            ProcessPendingDoorUpdates();
            
            // Notify subscribers that all doors have been updated
            OnDataLoaded?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error updating all door interactions: {e.Message}");
        }
    }

    // List to keep track of doors that need updating once data is loaded
    private List<DoorInteraction> pendingDoorUpdates = new List<DoorInteraction>();

    public void UpdateDoorInteraction(DoorInteraction door)
    {
        if (door == null) return;
        
        int doorId = door.doorID;
        bool isValidDoor = doorId >= 1 && doorId <= 24;
        
        try
        {
            if (!isDataLoaded || studentData?.doors == null) 
            {
                if (doorId >= 1 && doorId <= 24)
                {
                    // Debug.Log($"[ProgressManager] Door {doorId}: Data not loaded yet, queuing for later update");
                }
                
                // Add to pending updates if not already in the list
                if (!pendingDoorUpdates.Contains(door))
                {
                    pendingDoorUpdates.Add(door);
                }
                return;
            }
            
            if (doorId < 1 || doorId > 24) 
            {
                return;
            }
            
            DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
            
            if (doorData != null)
            {
                // Log the current state of the door before updating (only for doors 1-24)
                if (doorId >= 1 && doorId <= 24)
                {
                    // Debug.Log($"[ProgressManager] Door {doorId} before update - Door isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}");
                    // Debug.Log($"[ProgressManager] Door {doorId} data from DB - isUnlockable: {doorData.isUnlockable}, isRoomCompleted: {doorData.isRoomCompleted}");
                }
                
                // Update the door properties using setter methods
                door.SetUnlockable(doorData.isUnlockable);
                door.SetRoomCompleted(doorData.isRoomCompleted);
                
                // Make sure the door updates its visuals
                door.UpdateDoorVisuals();
                
                // Log the state after update to verify changes were applied (only for doors 1-24)
                if (doorId >= 1 && doorId <= 24)
                {
                    // Debug.Log($"[ProgressManager] Door {doorId} after update - isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}");
                }
            }
            else
            {
                // Create the door data with default values (only log for doors 1-24)
                if (doorId >= 1 && doorId <= 24)
                {
                    Debug.Log($"[ProgressManager] Door {doorId} not found in database. Creating it with default values.");
                }
                EnsureDoorDataExists(doorId, door.gameObject.name);
                
                // Try to find the door again after ensuring it exists
                doorData = studentData.doors.Find(d => d.doorId == doorId);
                if (doorData != null)
                {
                    // Update the door properties using setter methods
                    door.SetUnlockable(doorData.isUnlockable);
                    door.SetRoomCompleted(doorData.isRoomCompleted);
                    door.UpdateDoorVisuals();
                    
                    if (doorId >= 1 && doorId <= 24)
                    {
                        Debug.Log($"[ProgressManager] Created and updated door {doorId} state: isUnlockable={doorData.isUnlockable}, isRoomCompleted={doorData.isRoomCompleted}");
                    }
                }
                else
                {
                    Debug.LogError($"[ProgressManager] Failed to create door {doorId} in database.");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error updating door {doorId}: {e.Message}");
        }
    }

    // Helper method to get door data by ID
    public DoorData GetDoorData(int doorId)
    {
        if (isDataLoaded && studentData != null && studentData.doors != null)
        {
            return studentData.doors.Find(d => d.doorId == doorId);
        }
        return null;
    }

    // Ensures that a door exists in the database, creates it if it doesn't
    public void EnsureDoorDataExists(int doorId, string doorName)
    {
        // Skip if door ID is not in valid range (1-24)
        if (doorId < 1 || doorId > 24)
        {
            return;
        }
        
        if (!isDataLoaded || studentData?.doors == null)
        {
            // Debug.LogWarning($"[ProgressManager] Cannot ensure door {doorId} exists: data not loaded or studentData is null");
            return;
        }
        
        // Check if the door exists in the student data
        DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
        
        if (doorData == null)
        {
            // Check if this is the first door in a building
            bool isFirstDoorInBuilding = (doorId == 1 || doorId == 7 || doorId == 13 || doorId == 19);
            
            // For building 1, the first door is always unlockable
            // For other buildings, check if the previous building's last door is completed
            bool shouldBeUnlockable = false;
            
            if (doorId == 1)
            {
                // First door in the game is always unlockable
                shouldBeUnlockable = true;
            }
            else if (isFirstDoorInBuilding)
            {
                // For other buildings' first doors, check if previous building is completed
                int previousBuildingLastDoorId = doorId - 1;
                DoorData previousBuildingLastDoor = studentData.doors.Find(d => d.doorId == previousBuildingLastDoorId);
                
                if (previousBuildingLastDoor != null && previousBuildingLastDoor.isRoomCompleted)
                {
                    shouldBeUnlockable = true;
                }
            }
            
            // Create new door data with appropriate values
            DoorData newDoorData = new DoorData
            {
                doorId = doorId,
                name = doorName,
                isUnlockable = shouldBeUnlockable,
                isRoomCompleted = false,
                description = $"Room behind Door {doorId}"
            };
            
            // Add to the student data
            studentData.doors.Add(newDoorData);
            
            if (doorId >= 1 && doorId <= 24)
            {
                Debug.Log($"[ProgressManager] Created door {doorId} in database: isUnlockable={newDoorData.isUnlockable}, isRoomCompleted={newDoorData.isRoomCompleted}");
            }
            
            // Save the updated student data to the database
            StartCoroutine(SaveStudentDoorData());
        }
    }
    
    // Save the current student door data to the database
    private IEnumerator SaveStudentDoorData()
    {
        if (!isDataLoaded || studentData == null) yield break;
        
        string url = $"{baseUrl}/update/{studentId}";
        string jsonData = JsonConvert.SerializeObject(studentData);
        
        using (UnityWebRequest webRequest = UnityWebRequest.Put(url, jsonData))
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");
            yield return webRequest.SendWebRequest();
        }
    }

    public void SetStudentId(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            studentId = id;
        }
    }
    
    // Ensures that all doors from 1 to maxDoorId exist in the database
    public void EnsureAllDoorsExist()
    {
        if (studentData == null || studentData.doors == null)
        {
            Debug.LogError("[ProgressManager] Cannot ensure doors exist: studentData is null");
            return;
        }
        
        if (maxDoorId >= 1 && maxDoorId <= 24)
        {
            Debug.Log($"[ProgressManager] Ensuring all doors from 1 to {maxDoorId} exist in database");
        }
        
        // Check for each door ID from 1 to maxDoorId
        for (int doorId = 1; doorId <= maxDoorId; doorId++)
        {
            // Check if the door exists
            DoorData existingDoor = studentData.doors.Find(d => d.doorId == doorId);
            
            if (existingDoor == null)
            {
                // Door doesn't exist, create it with default values
                if (doorId >= 1 && doorId <= 24)
                {
                    Debug.Log($"[ProgressManager] Door {doorId} not found in database, creating it");
                }
                
                // Only the first door in each building is unlockable by default (doors 1, 7, 13, 19)
                bool isFirstDoorInBuilding = (doorId == 1 || doorId == 7 || doorId == 13 || doorId == 19);
                
                DoorData newDoor = new DoorData
                {
                    doorId = doorId,
                    name = $"Door {doorId}",
                    isUnlockable = doorId == 1, // Only the very first door is unlockable by default
                    isRoomCompleted = false,
                    description = $"Room behind Door {doorId}"
                };
                
                studentData.doors.Add(newDoor);
                if (doorId >= 1 && doorId <= 24)
                {
                    Debug.Log($"[ProgressManager] Created door {doorId} in database: isUnlockable={newDoor.isUnlockable}, isRoomCompleted={newDoor.isRoomCompleted}");
                }
            }
            else if (doorId == 1)
            {
                // First door should always be unlockable if not completed
                if (!existingDoor.isRoomCompleted && !existingDoor.isUnlockable)
                {
                    Debug.Log("[ProgressManager] First door is not unlockable. Fixing door progression.");
                    existingDoor.isUnlockable = true;
                }
            }
            else
            {
                // Ensure door progression logic is maintained
                // If a door is marked as completed, the next door should be unlockable
                if (existingDoor.isRoomCompleted && doorId < maxDoorId)
                {
                    // Check if this is the last door in a building (doors 6, 12, 18, 24)
                    bool isLastDoorInBuilding = (doorId == 6 || doorId == 12 || doorId == 18 || doorId == 24);
                    
                    // If it's the last door in a building, we don't need to unlock the next door
                    if (!isLastDoorInBuilding)
                    {
                        DoorData nextDoor = studentData.doors.Find(d => d.doorId == doorId + 1);
                        if (nextDoor != null && !nextDoor.isUnlockable && !nextDoor.isRoomCompleted)
                        {
                            if (doorId >= 1 && doorId <= 24)
                            {
                                Debug.Log($"[ProgressManager] Door {doorId} is completed but next door {doorId + 1} is not unlockable. Fixing progression.");
                            }
                            nextDoor.isUnlockable = true;
                        }
                    }
                    else
                    {
                        if (doorId >= 1 && doorId <= 24)
                        {
                            Debug.Log($"[ProgressManager] Door {doorId} is the last door in its building. No next door to unlock.");
                        }
                    }
                }
            }
        }
        
        // Validate door progression logic
        ValidateDoorProgressionLogic();
        
        // Save the updated student data to the database
        StartCoroutine(SaveStudentDoorData());
    }
    
    // Load door states from local storage
    // Process any doors that were waiting for data to load
    private void ProcessPendingDoorUpdates()
    {
        if (!isDataLoaded || studentData?.doors == null)
        {
            // Debug.LogWarning($"[ProgressManager] Cannot process pending door updates: data not loaded yet. Pending doors: {pendingDoorUpdates.Count}");
            return;
        }
        
        if (pendingDoorUpdates.Count > 0)
        {
            // Remove any null doors first (might have been destroyed)
            int removedCount = pendingDoorUpdates.RemoveAll(door => door == null);
            if (removedCount > 0)
            {
                Debug.Log($"[ProgressManager] Removed {removedCount} null doors from pending updates list");
            }
            
            if (pendingDoorUpdates.Count > 0)
            {
                Debug.Log($"[ProgressManager] Processing {pendingDoorUpdates.Count} pending door updates");
                
                // Create a copy of the list to avoid modification issues during iteration
                List<DoorInteraction> doorsCopy = new List<DoorInteraction>(pendingDoorUpdates);
                pendingDoorUpdates.Clear();
                
                int successCount = 0;
                foreach (DoorInteraction door in doorsCopy)
                {
                    if (door != null)
                    {
                        int doorId = door.doorID;
                        if (doorId >= 1 && doorId <= 24)
                        {
                            Debug.Log($"[ProgressManager] Processing pending update for Door {doorId}");
                            
                            // Get the door data
                            DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
                            if (doorData != null)
                            {
                                // Log the current state of the door before updating
                                if (doorId >= 1 && doorId <= 24)
                                {
                                    Debug.Log($"[ProgressManager] Door {doorId} before update - Door isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}");
                                    Debug.Log($"[ProgressManager] Door {doorId} data from DB - isUnlockable: {doorData.isUnlockable}, isRoomCompleted: {doorData.isRoomCompleted}");
                                }
                                
                                // Update the door properties using setter methods
                                door.SetUnlockable(doorData.isUnlockable);
                                door.SetRoomCompleted(doorData.isRoomCompleted);
                                
                                // Make sure the door updates its visuals
                                door.UpdateDoorVisuals();
                                
                                // Log the state after update to verify changes were applied (only for doors 1-24)
                                if (doorId >= 1 && doorId <= 24)
                                {
                                    Debug.Log($"[ProgressManager] Door {doorId} after update - isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}");
                                }
                                
                                successCount++;
                            }
                            else
                            {
                                Debug.LogWarning($"[ProgressManager] Door {doorId} data not found in student data");
                                // Try to create the door data
                                EnsureDoorDataExists(doorId, door.gameObject.name);
                                UpdateDoorInteraction(door);
                            }
                        }
                    }
                }
                
                if (successCount > 0 && successCount == doorsCopy.Count)
                {
                    Debug.Log($"[ProgressManager] Successfully updated all {successCount} pending doors");
                }
                else if (successCount > 0)
                {
                    Debug.Log($"[ProgressManager] Updated {successCount} of {doorsCopy.Count} pending doors (some were null or invalid)");
                }
                else
                {
                    Debug.LogWarning("[ProgressManager] Failed to update any pending doors");
                }
            }
        }
    }
    
    
    // Parse MongoDB JSON format which contains $oid and $date fields
    private void ParseMongoDBJson(string jsonResponse)
    {
        try
        {
            Debug.Log("[ProgressManager] Attempting manual parsing of MongoDB JSON format");

            if (studentData == null) studentData = new StudentDoorsData();
            if (studentData.doors == null) studentData.doors = new List<DoorData>();
            studentData.doors.Clear();

            // Find the start of the doors array
            string doorsArrayIdentifier = "\"doors\":[";
            int doorsArrayIndex = jsonResponse.IndexOf(doorsArrayIdentifier);
            if (doorsArrayIndex == -1)
            {
                throw new System.Exception("Could not find 'doors' array in JSON response.");
            }

            // Get the substring that contains the array content
            string doorsContent = jsonResponse.Substring(doorsArrayIndex + doorsArrayIdentifier.Length);
            int doorsArrayEndIndex = doorsContent.IndexOf(']');
            if (doorsArrayEndIndex == -1)
            {
                throw new System.Exception("Could not find closing bracket for 'doors' array.");
            }

            doorsContent = doorsContent.Substring(0, doorsArrayEndIndex);

            // Split the array content into individual door objects
            string[] doorObjects = doorsContent.Split(new[] { "{" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string doorObject in doorObjects)
            {
                string parsableObject = doorObject.Trim();
                if (!parsableObject.EndsWith("}"))
                {
                    parsableObject = parsableObject.Substring(0, parsableObject.LastIndexOf('}') + 1);
                }

                try
                {
                    int doorId = ExtractInt(parsableObject, "\"doorId\":");
                    bool isUnlockable = ExtractBool(parsableObject, "\"isUnlockable\":");
                    bool isRoomCompleted = ExtractBool(parsableObject, "\"isRoomCompleted\":");

                    DoorData newDoor = new DoorData
                    {
                        doorId = doorId,
                        name = $"Door {doorId}",
                        isUnlockable = isUnlockable,
                        isRoomCompleted = isRoomCompleted,
                        description = $"Room behind Door {doorId}"
                    };
                    studentData.doors.Add(newDoor);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ProgressManager] Failed to parse a door object: {ex.Message}. Object: {parsableObject}");
                }
            }

            Debug.Log($"[ProgressManager] Found and parsed {studentData.doors.Count} doors from MongoDB JSON");

            if (studentData.doors.Count == 0)
            {
                throw new System.Exception("No valid door data found in MongoDB JSON after parsing.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error in manual MongoDB JSON parsing: {e.Message}");
            throw; // Rethrow to be caught by the caller
        }
    }

    private int ExtractInt(string json, string key)
    {
        int keyIndex = json.IndexOf(key);
        if (keyIndex == -1) return 0;
        string valueStr = json.Substring(keyIndex + key.Length);
        int endIndex = valueStr.IndexOfAny(new[] { ',', '}' });
        if (endIndex != -1) valueStr = valueStr.Substring(0, endIndex);
        int.TryParse(valueStr.Trim(), out int result);
        return result;
    }

    private bool ExtractBool(string json, string key)
    {
        int keyIndex = json.IndexOf(key);
        if (keyIndex == -1) return false;
        string valueStr = json.Substring(keyIndex + key.Length);
        int endIndex = valueStr.IndexOfAny(new[] { ',', '}' });
        if (endIndex != -1) valueStr = valueStr.Substring(0, endIndex);
        bool.TryParse(valueStr.Trim(), out bool result);
        return result;
    }
    
    // Create default door data when no data is available
    private void CreateDefaultDoorData()
    {
        Debug.Log("[ProgressManager] Creating default door data");
        
        if (studentData == null)
        {
            studentData = new StudentDoorsData();
            studentData.doors = new List<DoorData>();
        }
        else if (studentData.doors == null)
        {
            studentData.doors = new List<DoorData>();
        }
        
        // Clear existing data
        studentData.doors.Clear();
        
        // Create default door data - only door 1 is unlockable initially
        for (int i = 1; i <= maxDoorId; i++)
        {
            DoorData door = new DoorData
            {
                doorId = i,
                name = $"Door {i}",
                isUnlockable = (i == 1), // Only first door is unlockable
                isRoomCompleted = false,
                description = $"Room behind Door {i}"
            };
            
            studentData.doors.Add(door);
        }
        
        Debug.Log($"[ProgressManager] Created default data for {studentData.doors.Count} doors");
        isDataLoaded = true;
        
        // Update all door interactions in the scene
        UpdateAllDoorInteractions();
        
        // Notify subscribers that data is loaded
        OnDataLoaded?.Invoke();
    }
    
    // Validate door progression logic to ensure consistency
    private void ValidateDoorProgressionLogic()
    {
        if (studentData == null || studentData.doors == null || studentData.doors.Count == 0)
        {
            Debug.LogWarning("[ProgressManager] Cannot validate door progression: studentData is null or empty");
            return;
        }
        
        Debug.Log("[ProgressManager] Validating door progression logic");
        
        bool changesNeeded = false;
        
        // Sort doors by ID to ensure proper order
        studentData.doors.Sort((a, b) => a.doorId.CompareTo(b.doorId));
        
        // First door should always be unlockable if not completed
        DoorData firstDoor = studentData.doors.Find(d => d.doorId == 1);
        if (firstDoor != null && !firstDoor.isRoomCompleted && !firstDoor.isUnlockable)
        {
            Debug.Log("[ProgressManager] First door is not unlockable. Fixing progression.");
            firstDoor.isUnlockable = true;
            changesNeeded = true;
        }
        
        // First door in each building should be unlockable if all previous building doors are completed
        int[] buildingFirstDoors = { 1, 7, 13, 19 };
        for (int i = 1; i < buildingFirstDoors.Length; i++)
        {
            int currentFirstDoorId = buildingFirstDoors[i];
            int previousBuildingLastDoorId = buildingFirstDoors[i] - 1;
            
            DoorData currentFirstDoor = studentData.doors.Find(d => d.doorId == currentFirstDoorId);
            DoorData previousBuildingLastDoor = studentData.doors.Find(d => d.doorId == previousBuildingLastDoorId);
            
            if (currentFirstDoor != null && previousBuildingLastDoor != null && 
                previousBuildingLastDoor.isRoomCompleted && !currentFirstDoor.isRoomCompleted && !currentFirstDoor.isUnlockable)
            {
                Debug.Log($"[ProgressManager] Building first door {currentFirstDoorId} should be unlockable since previous building is completed.");
                currentFirstDoor.isUnlockable = true;
                changesNeeded = true;
            }
        }
        
        // Check each door's state and ensure proper progression within each building
        for (int i = 0; i < studentData.doors.Count - 1; i++)
        {
            DoorData currentDoor = studentData.doors[i];
            DoorData nextDoor = studentData.doors[i + 1];
            
            // Skip validation between buildings (doors 6->7, 12->13, 18->19)
            if (currentDoor.doorId == 6 || currentDoor.doorId == 12 || currentDoor.doorId == 18)
            {
                continue;
            }
            
            // If current door is completed, next door should be unlockable
            if (currentDoor.isRoomCompleted && !nextDoor.isUnlockable && !nextDoor.isRoomCompleted)
            {
                Debug.Log($"[ProgressManager] Door {currentDoor.doorId} is completed but next door {nextDoor.doorId} is not unlockable. Fixing progression.");
                nextDoor.isUnlockable = true;
                changesNeeded = true;
            }
            
            // If next door is completed or unlockable, all previous doors should be completed
            if ((nextDoor.isRoomCompleted || nextDoor.isUnlockable) && !currentDoor.isRoomCompleted)
            {
                Debug.Log($"[ProgressManager] Door {nextDoor.doorId} is unlockable/completed but previous door {currentDoor.doorId} is not completed. Fixing progression.");
                currentDoor.isRoomCompleted = true;
                currentDoor.isUnlockable = false;
                changesNeeded = true;
            }
        }
        
        if (changesNeeded)
        {
            Debug.Log("[ProgressManager] Door progression issues found and fixed. Saving changes to database.");
            StartCoroutine(SaveStudentDoorData());
        }
    }
    
    // Update door instances in the scene after a room is completed
    private void UpdateDoorInstancesInScene(int completedDoorId, int nextDoorId)
    {
        if (nextDoorId > 0)
        {
            Debug.Log($"[ProgressManager] Updating door instances in scene for doors {completedDoorId} and {nextDoorId}");
        }
        else
        {
            Debug.Log($"[ProgressManager] Updating door instance in scene for door {completedDoorId} (no next door)");
        }
        
        // Find all door interactions in the scene
        DoorInteraction[] allDoors = FindObjectsOfType<DoorInteraction>();
        
        foreach (DoorInteraction door in allDoors)
        {
            if (door == null) continue;
            
            int doorId = door.doorID;
            
            // Update the completed door
            if (doorId == completedDoorId)
            {
                Debug.Log($"[ProgressManager] Updating completed door {doorId} in scene: isUnlockable=false, isRoomCompleted=true");
                door.isUnlockable = false;
                door.isRoomCompleted = true;
                door.UpdateDoorVisuals();
            }
            // Update the next door if there is one
            else if (nextDoorId > 0 && doorId == nextDoorId)
            {
                Debug.Log($"[ProgressManager] Updating next door {doorId} in scene: isUnlockable=true");
                door.isUnlockable = true;
                door.UpdateDoorVisuals();
            }
        }
    }
}

[Serializable]
public class DoorUpdateData
{
    public bool isUnlockable;
    public bool isRoomCompleted;
}
