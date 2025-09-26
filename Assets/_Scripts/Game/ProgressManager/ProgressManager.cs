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

        // If we have a student ID, load the data
        if (!string.IsNullOrEmpty(studentId))
        {
            LoadStudentDoorData();
        }
        #if UNITY_EDITOR
        else if (string.IsNullOrEmpty(studentId))
        {
            studentId = "default_test_id";
            LoadStudentDoorData();
        }
        #endif
    }
    
    // Get student ID from StudentIdManager or directly from PlayerPrefs
    private void GetStudentId()
    {
        // First try StudentIdManager
        if (StudentIdManager.Instance != null)
        {
            string id = StudentIdManager.Instance.GetStudentId();
            if (!string.IsNullOrEmpty(id))
            {
                studentId = id;
                return;
            }
            
            // Subscribe to StudentIdManager events to get the ID when it becomes available
            StudentIdManager.Instance.OnStudentIdLoaded += HandleStudentIdLoaded;
            return;
        }
        
        // If still empty, try PlayerPrefs directly
        studentId = PlayerPrefs.GetString("StudentId", "");
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
        if (string.IsNullOrEmpty(studentId)) yield break;
        
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
                    // Try to load from local cache if available
                    LoadLocalDoorStates();
                }
            }
            else
            {
                // Parse the response
                string jsonResponse = webRequest.downloadHandler.text;
                
                try
                {
                    studentData = JsonConvert.DeserializeObject<StudentDoorsData>(jsonResponse);
                    isDataLoaded = true;
                    
                    // Ensure we have all door IDs from 1 to 16
                    EnsureAllDoorsExist();
                    
                    // Update all door interactions in the scene
                    UpdateAllDoorInteractions();
                    
                    // Save to local cache
                    SaveLocalDoorStates();
                    
                    // Notify subscribers that data is loaded
                    OnDataLoaded?.Invoke();
                }
                catch (Exception)
                {
                    // Try to load from local cache if available
                    LoadLocalDoorStates();
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
            Debug.LogWarning("[ProgressManager] Cannot update door status: studentId is not set");
            yield break;
        }

        // Skip processing for invalid door IDs
        if (doorId < 1 || doorId > 24)
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
            Debug.LogWarning($"[ProgressManager] Invalid door ID: {doorId}. Valid range is 1-24.");
            return;
        }
        
        if (studentData?.doors == null)
        {
            if (doorId >= 1 && doorId <= 24)
            {
                Debug.LogError($"[ProgressManager] Cannot mark room as completed: studentData or doors is null");
            }
            return;
        }
        
        if (doorId >= 1 && doorId <= 24)
        {
            Debug.Log($"[ProgressManager] Marking room with door {doorId} as completed");
        }
        
        DoorData door = studentData.doors.Find(d => d.doorId == doorId);
        if (door == null)
        {
            if (doorId >= 1 && doorId <= 24)
            {
                Debug.LogWarning($"[ProgressManager] Door {doorId} not found in database. Creating it first.");
            }
            EnsureDoorDataExists(doorId, $"Door {doorId}");
            door = studentData.doors.Find(d => d.doorId == doorId);
            
            if (door == null)
            {
                Debug.LogError($"[ProgressManager] Failed to create door {doorId} in database.");
                return;
            }
        }
        
        if (door.isRoomCompleted)
        {
            if (doorId >= 1 && doorId <= 24)
            {
                Debug.Log($"[ProgressManager] Door {doorId} is already marked as completed. No action needed.");
            }
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
                if (doorId >= 1 && doorId <= 24 && nextDoorId >= 1 && nextDoorId <= 24)
            {
                Debug.Log($"[ProgressManager] Unlocking next door {nextDoorId} after completing door {doorId}");
            }
                // Update the next door (set isUnlockable=true, keep isRoomCompleted as is)
                UpdateDoorStatusDirect(nextDoorId, true, nextDoor.isRoomCompleted);
                
                // Verify the updates were applied for the next door
                StartCoroutine(VerifyDatabaseUpdates(nextDoorId, true, nextDoor.isRoomCompleted));
                
                // Update local door instances in the scene
                UpdateDoorInstancesInScene(doorId, nextDoorId);
            }
            else
            {
                Debug.LogWarning($"[ProgressManager] Next door {nextDoorId} not found in database.");
                // Try to create the next door
                EnsureDoorDataExists(nextDoorId, $"Door {nextDoorId}");
            }
        }
        else
        {
            Debug.Log($"[ProgressManager] Door {doorId} is the last door in its building. No next door to unlock.");
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
                    DoorData verifiedDoor = verifiedData.doors.Find(d => d.doorId == doorId);
                    
                    if (verifiedDoor != null)
                    {
                        bool updateSuccessful = verifiedDoor.isUnlockable == expectedUnlockable && 
                                               verifiedDoor.isRoomCompleted == expectedCompleted;
                        
                        if (!updateSuccessful)
                        {
                            // Try a different API endpoint as a fallback
                            StartCoroutine(UpdateDoorStatusFallback(doorId, expectedUnlockable, expectedCompleted));
                        }
                        
                        // Update our local data with the verified data
                        studentData = verifiedData;
                        isDataLoaded = true;
                        
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
    
    // Fallback method to update door status using a different API endpoint
    private IEnumerator UpdateDoorStatusFallback(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Skip processing for invalid door IDs
        if (doorId < 1 || doorId > 24)
        {
            yield break;
        }

        // Try a different API endpoint format
        string url = $"{baseUrl}/student/{studentId}";
        
        // Create a proper JSON structure that matches the API expectations
        string jsonPayload = $"{{\"isUnlockable\": {isUnlockable.ToString().ToLower()}, \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}}}";
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "PUT"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result != UnityWebRequest.Result.ConnectionError && 
                webRequest.result != UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ProgressManager] Fallback - Error updating door status: {webRequest.error}");
                Debug.LogError($"[ProgressManager] Fallback - Response code: {webRequest.responseCode}");
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    Debug.LogError($"[ProgressManager] Fallback - Response: {webRequest.downloadHandler.text}");
                }
            }
            else
            {
                if (doorId >= 1 && doorId <= 24)
                {
                    Debug.Log($"[ProgressManager] Fallback - Door {doorId} status updated successfully");
                    Debug.Log($"[ProgressManager] Fallback - Response: {webRequest.downloadHandler.text}");
                }
                
                // Force reload data to verify changes
                StartCoroutine(ReloadDataAfterDelay(1.0f));
            }
        }
    }
    
    // Update door status with direct HTTP request
    private void UpdateDoorStatusDirect(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Skip if studentId is not set
        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogWarning("[ProgressManager] Cannot update door status: studentId is not set");
            return;
        }

        // Skip processing for invalid door IDs
        if (doorId < 1 || doorId > 24)
        {
            return;
        }

        string url = $"{baseUrl}/{studentId}/{doorId}";
        
        // Create the JSON structure
        string jsonPayload = "{\n" +
            $"  \"isUnlockable\": {isUnlockable.ToString().ToLower()},\n" +
            $"  \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}\n" +
            "}";
        
        if (doorId >= 1 && doorId <= 24)
        {
            Debug.Log($"[ProgressManager] Sending door {doorId} update to database: isUnlockable={isUnlockable}, isRoomCompleted={isRoomCompleted}");
        }
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "PUT"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            webRequest.SendWebRequest();
            
            // Wait for completion (this is not ideal but ensures sequential execution)
            int waitCount = 0;
            while (!webRequest.isDone)
            {
                // Small delay to prevent freezing
                System.Threading.Thread.Sleep(50);
                waitCount++;
                if (waitCount % 20 == 0) // Log every ~1000ms
                {
                    if (doorId >= 1 && doorId <= 24 && waitCount % 4 == 0) // Only log every 4th update for doors 1-24
                    {
                        Debug.Log($"[ProgressManager] Waiting for door {doorId} database update... ({(waitCount * 50)}ms)");
                    }
                }
            }
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ProgressManager] Error updating door {doorId} in database: {webRequest.error}");
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    Debug.LogError($"[ProgressManager] Database response: {webRequest.downloadHandler.text}");
                }
            }
            else
            {
                if (doorId >= 1 && doorId <= 24)
                {
                    Debug.Log($"[ProgressManager] Door {doorId} successfully updated in database");
                }
                
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
            }
            
            // Dispose of the request
            webRequest.Dispose();
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
                Debug.LogWarning("[ProgressManager] Cannot update door interactions: data not loaded or studentData is null");
                return;
            }
                
            // Find all door interactions in the scene
            DoorInteraction[] doorInteractions = FindObjectsOfType<DoorInteraction>();
            
            // Only log if we find doors 1-24 in the scene
            if (doorInteractions.Any(d => d != null && d.doorID >= 1 && d.doorID <= 24))
            {
                Debug.Log($"[ProgressManager] Found {doorInteractions.Length} door interactions in the scene");
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
                        Debug.LogWarning($"[ProgressManager] Door has invalid ID: {doorId}. Valid range is 1-24.");
                    }
                }
            }
            
            if (updatedCount > 0 || invalidCount > 0)
            {
                Debug.Log($"[ProgressManager] Updated {updatedCount} doors, skipped {invalidCount} invalid doors");
            }
            
            // Notify subscribers that all doors have been updated
            OnDataLoaded?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error updating all door interactions: {e.Message}");
        }
    }

    public void UpdateDoorInteraction(DoorInteraction door)
    {
        if (door == null) return;
        
        int doorId = door.doorID;
        bool isValidDoor = doorId >= 1 && doorId <= 24;
        
        try
        {
            if (!isDataLoaded || studentData?.doors == null) 
            {
                Debug.LogWarning($"[ProgressManager] Cannot update door {doorId}: data not loaded or studentData is null");
                return;
            }
            
            if (doorId < 1 || doorId > 24) 
            {
                Debug.LogWarning($"[ProgressManager] Invalid door ID: {doorId}. Valid range is 1-24.");
                return;
            }
            
            DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
            
            if (doorData != null)
            {
                // Update the door properties
                bool stateChanged = (door.isUnlockable != doorData.isUnlockable || door.isRoomCompleted != doorData.isRoomCompleted);
                
                door.isUnlockable = doorData.isUnlockable;
                door.isRoomCompleted = doorData.isRoomCompleted;
                door.UpdateDoorVisuals();
                
                if (stateChanged)
                {
                    if (doorId >= 1 && doorId <= 24)
                    {
                        Debug.Log($"[ProgressManager] Updated door {doorId} state: isUnlockable={doorData.isUnlockable}, isRoomCompleted={doorData.isRoomCompleted}");
                    }
                }
            }
            else
            {
                // Create the door data with default values
                if (doorId >= 1 && doorId <= 24)
                {
                    Debug.Log($"[ProgressManager] Door {doorId} not found in database. Creating it with default values.");
                }
                EnsureDoorDataExists(doorId, door.gameObject.name);
                
                // Try to find the door again after ensuring it exists
                doorData = studentData.doors.Find(d => d.doorId == doorId);
                if (doorData != null)
                {
                    door.isUnlockable = doorData.isUnlockable;
                    door.isRoomCompleted = doorData.isRoomCompleted;
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
            Debug.LogWarning($"[ProgressManager] Invalid door ID: {doorId}. Valid range is 1-24.");
            return;
        }
        
        if (!isDataLoaded || studentData?.doors == null)
        {
            Debug.LogWarning($"[ProgressManager] Cannot ensure door {doorId} exists: data not loaded or studentData is null");
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
    
    // Save door states to local storage
    private void SaveLocalDoorStates()
    {
        if (studentData == null || studentData.doors == null)
        {
            Debug.LogWarning("[ProgressManager] Cannot save local door states: studentData is null");
            return;
        }
        
        try
        {
            LocalDoorStatesWrapper wrapper = new LocalDoorStatesWrapper();
            
            foreach (DoorData door in studentData.doors)
            {
                wrapper.doors.Add(new LocalDoorState
                {
                    doorId = door.doorId,
                    isUnlockable = door.isUnlockable,
                    isRoomCompleted = door.isRoomCompleted,
                    lastUpdated = System.DateTime.UtcNow.Ticks
                });
            }
            
            string json = JsonConvert.SerializeObject(wrapper);
            PlayerPrefs.SetString($"DoorStates_{studentId}", json);
            PlayerPrefs.Save();
            
            Debug.Log($"[ProgressManager] Saved {wrapper.doors.Count} door states to local storage");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error saving local door states: {e.Message}");
        }
    }
    
    // Load door states from local storage
    private void LoadLocalDoorStates()
    {
        try
        {
            string key = $"DoorStates_{studentId}";
            
            if (PlayerPrefs.HasKey(key))
            {
                string json = PlayerPrefs.GetString(key);
                LocalDoorStatesWrapper wrapper = JsonConvert.DeserializeObject<LocalDoorStatesWrapper>(json);
                
                if (wrapper != null && wrapper.doors != null && wrapper.doors.Count > 0)
                {
                    Debug.Log($"[ProgressManager] Loading {wrapper.doors.Count} door states from local storage");
                    
                    // Initialize student data if needed
                    if (studentData == null)
                    {
                        studentData = new StudentDoorsData();
                    }
                    
                    if (studentData.doors == null)
                    {
                        studentData.doors = new List<DoorData>();
                    }
                    else
                    {
                        studentData.doors.Clear();
                    }
                    
                    // Convert local states to door data
                    foreach (LocalDoorState localState in wrapper.doors)
                    {
                        studentData.doors.Add(new DoorData
                        {
                            doorId = localState.doorId,
                            name = $"Door {localState.doorId}",
                            isUnlockable = localState.isUnlockable,
                            isRoomCompleted = localState.isRoomCompleted,
                            description = $"Door {localState.doorId} Description"
                        });
                    }
                    
                    // Ensure we have all doors from 1 to maxDoorId
                    EnsureAllDoorsExist();
                    
                    isDataLoaded = true;
                    
                    // Update all door interactions in the scene
                    UpdateAllDoorInteractions();
                    
                    Debug.Log("[ProgressManager] Successfully loaded door states from local storage");
                    
                    // Notify subscribers that data is loaded
                    OnDataLoaded?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[ProgressManager] Local door states are null or empty");
                }
            }
            else
            {
                Debug.LogWarning($"[ProgressManager] No local door states found for student ID: {studentId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error loading local door states: {e.Message}");
        }
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
