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
    [SerializeField] private int maxDoorId = 16; // Maximum door ID in the game
    
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
        Debug.Log("[ProgressManager] Starting ProgressManager...");
        
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
            Debug.Log($"[ProgressManager] Student ID available: {studentId}. Loading door data...");
            LoadStudentDoorData();
        }
        else
        {
            Debug.LogWarning("[ProgressManager] No student ID available at Start(). Data will be loaded when ID becomes available.");
            // For testing/development, use a default student ID if none is available
            #if UNITY_EDITOR
            studentId = "default_test_id";
            Debug.Log($"[ProgressManager] Using default test ID in Editor: {studentId}");
            LoadStudentDoorData();
            #endif
        }
    }
    
    // Get student ID from StudentIdManager or directly from PlayerPrefs
    private void GetStudentId()
    {
        Debug.Log("[ProgressManager] Attempting to get student ID...");
        
        // First try StudentIdManager
        if (StudentIdManager.Instance != null)
        {
            string id = StudentIdManager.Instance.GetStudentId();
            if (!string.IsNullOrEmpty(id))
            {
                studentId = id;
                Debug.Log($"[ProgressManager] Successfully got student ID from StudentIdManager: {studentId}");
                return;
            }
            
            Debug.Log("[ProgressManager] No student ID available in StudentIdManager yet. Subscribing to OnStudentIdLoaded event.");
            // Subscribe to StudentIdManager events to get the ID when it becomes available
            StudentIdManager.Instance.OnStudentIdLoaded += HandleStudentIdLoaded;
            return;
        }
        
        Debug.LogWarning("[ProgressManager] StudentIdManager.Instance is null. Trying PlayerPrefs...");
        
        // If still empty, try PlayerPrefs directly
        studentId = PlayerPrefs.GetString("StudentId", "");
        if (!string.IsNullOrEmpty(studentId))
        {
            Debug.Log($"[ProgressManager] Got student ID from PlayerPrefs: {studentId}");
            return;
        }
        
        // Log if we still don't have a student ID
        Debug.LogWarning("[ProgressManager] No student ID found in StudentIdManager or PlayerPrefs");
    }
    
    private void HandleStudentIdLoaded(string id)
    {
        Debug.Log("[ProgressManager] HandleStudentIdLoaded called");
        
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError("[ProgressManager] Received null or empty student ID in HandleStudentIdLoaded");
            return;
        }
        
        // Unsubscribe to avoid multiple calls
        if (StudentIdManager.Instance != null)
        {
            Debug.Log("[ProgressManager] Unsubscribing from OnStudentIdLoaded event");
            StudentIdManager.Instance.OnStudentIdLoaded -= HandleStudentIdLoaded;
        }
        
        // Set the student ID and load the data
        studentId = id;
        Debug.Log($"[ProgressManager] Student ID loaded from event. New ID: {studentId}. Loading door data...");
        LoadStudentDoorData();
    }

    public void LoadStudentDoorData(bool isRetry = false)
    {
        if (!isRetry)
        {
            currentRetryAttempt = 0;
        }

        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("[ProgressManager] Cannot load door data: studentId is null or empty");
            return;
        }

        if (isRetry)
        {
            if (currentRetryAttempt >= maxRetryAttempts)
            {
                Debug.LogError($"[ProgressManager] Max retry attempts ({maxRetryAttempts}) reached. Giving up.");
                return;
            }
            Debug.Log($"[ProgressManager] Retry attempt {currentRetryAttempt + 1} of {maxRetryAttempts}");
            currentRetryAttempt++;
        }

        Debug.Log($"[ProgressManager] Loading door data for student ID: {studentId}");
        StartCoroutine(LoadStudentDoorDataCoroutine(isRetry));
    }

    private IEnumerator LoadStudentDoorDataCoroutine(bool isRetry = false)
    {
        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("[ProgressManager] Cannot load door data: studentId is null or empty");
            yield break;
        }
        
        Debug.Log($"[ProgressManager] Loading door data for student ID: {studentId}");
        isDataLoaded = false;
        string url = $"{baseUrl}/student/{studentId}";
        Debug.Log($"[ProgressManager] API URL: {url}");
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error loading student data: {webRequest.error}");
                
                // Retry with exponential backoff
                if (currentRetryAttempt < maxRetryAttempts)
                {
                    float retryDelay = initialRetryDelay * Mathf.Pow(2, currentRetryAttempt);
                    Debug.Log($"[ProgressManager] Retrying in {retryDelay} seconds...");
                    yield return new WaitForSeconds(retryDelay);
                    LoadStudentDoorData(true);
                    yield break;
                }
                
                // Check if the student exists
                if (webRequest.responseCode == 404)
                {
                    Debug.Log("Student not found, creating new student data");
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
                Debug.Log($"Received data: {jsonResponse}");
                
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
                    
                    Debug.Log("Student data loaded successfully");
                    
                    // Notify subscribers that data is loaded
                    OnDataLoaded?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing student data: {e.Message}");
                    
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
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error creating student data: {webRequest.error}");
            }
            else
            {
                Debug.Log("Student data created successfully");
                // Load the newly created data
                yield return LoadStudentDoorDataCoroutine();
            }
        }
    }

    public IEnumerator UpdateDoorStatus(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Correct API endpoint format - this is the key fix
        string url = $"{baseUrl}/{studentId}/{doorId}";
        
        // Create a proper JSON structure that matches the API expectations
        string jsonPayload = $"{{\"isUnlockable\": {isUnlockable.ToString().ToLower()}, \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}}}";
        
        Debug.Log($"[ProgressManager] Sending update to URL: {url}");
        Debug.Log($"[ProgressManager] Payload: {jsonPayload}");
        
        using (UnityWebRequest webRequest = UnityWebRequest.Put(url, jsonPayload))
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ProgressManager] Error updating door status: {webRequest.error}");
                Debug.LogError($"[ProgressManager] Response code: {webRequest.responseCode}");
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    Debug.LogError($"[ProgressManager] Response: {webRequest.downloadHandler.text}");
                }
            }
            else
            {
                Debug.Log($"[ProgressManager] Door {doorId} status updated successfully");
                Debug.Log($"[ProgressManager] Response: {webRequest.downloadHandler.text}");
                
                // Update local data
                if (studentData != null && studentData.doors != null)
                {
                    DoorData door = studentData.doors.Find(d => d.doorId == doorId);
                    if (door != null)
                    {
                        door.isUnlockable = isUnlockable;
                        door.isRoomCompleted = isRoomCompleted;
                        Debug.Log($"[ProgressManager] Local data updated for door {doorId}: isUnlockable={door.isUnlockable}, isRoomCompleted={door.isRoomCompleted}");
                    }
                }
                
                // Force reload data to verify changes
                StartCoroutine(ReloadDataAfterDelay(1.0f));
            }
        }
    }

    private IEnumerator VerifyDoorUpdate(int doorId, bool expectedUnlockable, bool expectedCompleted)
    {
        // Wait a bit to ensure the server has processed the update
        yield return new WaitForSeconds(0.5f);
        
        string url = $"{baseUrl}/student/{studentId}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ProgressManager] Error verifying door update: {webRequest.error}");
            }
            else
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
                        
                        Debug.Log($"[ProgressManager] Verification for door {doorId} - " +
                                 $"Expected: isUnlockable={expectedUnlockable}, isRoomCompleted={expectedCompleted}, " +
                                 $"Actual: isUnlockable={verifiedDoor.isUnlockable}, isRoomCompleted={verifiedDoor.isRoomCompleted}, " +
                                 $"Success: {updateSuccessful}");
                        
                        if (!updateSuccessful)
                        {
                            Debug.LogWarning($"[ProgressManager] Door {doorId} update verification failed! Database values don't match expected values.");
                        }
                        
                        // Update our local data with the verified data
                        studentData = verifiedData;
                        isDataLoaded = true;
                        
                        // Notify subscribers that data has been updated
                        OnDataLoaded?.Invoke();
                    }
                    else
                    {
                        Debug.LogWarning($"[ProgressManager] Door {doorId} not found in verification data");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ProgressManager] Error parsing verification data: {e.Message}");
                }
            }
        }
    }
    
    // Mark a room as completed and unlock the next door
    public void MarkRoomAsCompleted(int doorId)
    {
        Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - MarkRoomAsCompleted called for door {doorId}");
        
        if (studentData == null || studentData.doors == null)
        {
            Debug.LogError($"[ProgressManager] <color=red>DATABASE ERROR</color> - Cannot mark room as completed: studentData or doors is null");
            return;
        }
        
        DoorData door = studentData.doors.Find(d => d.doorId == doorId);
        if (door == null)
        {
            Debug.LogError($"[ProgressManager] <color=red>DATABASE ERROR</color> - Cannot mark room as completed: Door {doorId} not found in studentData");
            return;
        }
        
        Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - Current door {doorId} state before update: isUnlockable={door.isUnlockable}, isRoomCompleted={door.isRoomCompleted}");
        
        // First update the current door (set isUnlockable=false, isRoomCompleted=true)
        Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - Updating current door {doorId} to: isUnlockable=false, isRoomCompleted=true");
        UpdateDoorStatusDirect(doorId, false, true);
        
        // Find the next door by ID
        int nextDoorId = doorId + 1;
        DoorData nextDoor = studentData.doors.Find(d => d.doorId == nextDoorId);
        
        if (nextDoor != null)
        {
            Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - Found next door {nextDoorId}, current state: isUnlockable={nextDoor.isUnlockable}, isRoomCompleted={nextDoor.isRoomCompleted}");
            // Update the next door (set isUnlockable=true, keep isRoomCompleted as is)
            Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - Updating next door {nextDoorId} to: isUnlockable=true, isRoomCompleted={nextDoor.isRoomCompleted}");
            UpdateDoorStatusDirect(nextDoorId, true, nextDoor.isRoomCompleted);
        }
        else
        {
            Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - No next door found after door {doorId}");
        }
        
        // Force reload data from server after updates
        Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - Scheduling data reload from server after 1.0 seconds");
        StartCoroutine(ReloadDataAfterDelay(1.0f));
        
        // Verify the updates were applied
        Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - Scheduling verification for door {doorId} update");
        StartCoroutine(VerifyDatabaseUpdates(doorId, false, true));
        if (nextDoor != null)
        {
            Debug.Log($"[ProgressManager] <color=magenta>ROOM COMPLETION</color> - Scheduling verification for next door {nextDoorId} update");
            StartCoroutine(VerifyDatabaseUpdates(nextDoorId, true, nextDoor.isRoomCompleted));
        }
    }
    
    // Verify that database updates were applied correctly
    private IEnumerator VerifyDatabaseUpdates(int doorId, bool expectedUnlockable, bool expectedCompleted)
    {
        // Wait a bit to ensure the server has processed the update
        yield return new WaitForSeconds(2.0f);
        
        string url = $"{baseUrl}/student/{studentId}";
        Debug.Log($"[ProgressManager] Verifying database updates for door {doorId} at URL: {url}");
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ProgressManager] Error verifying database updates: {webRequest.error}");
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;
                Debug.Log($"[ProgressManager] Database verification response: {jsonResponse}");
                
                try
                {
                    StudentDoorsData verifiedData = JsonConvert.DeserializeObject<StudentDoorsData>(jsonResponse);
                    DoorData verifiedDoor = verifiedData.doors.Find(d => d.doorId == doorId);
                    
                    if (verifiedDoor != null)
                    {
                        bool updateSuccessful = verifiedDoor.isUnlockable == expectedUnlockable && 
                                               verifiedDoor.isRoomCompleted == expectedCompleted;
                        
                        Debug.Log($"[ProgressManager] Verification for door {doorId} - " +
                                 $"Expected: isUnlockable={expectedUnlockable}, isRoomCompleted={expectedCompleted}, " +
                                 $"Actual: isUnlockable={verifiedDoor.isUnlockable}, isRoomCompleted={verifiedDoor.isRoomCompleted}, " +
                                 $"Success: {updateSuccessful}");
                        
                        if (!updateSuccessful)
                        {
                            Debug.LogError($"[ProgressManager] Door {doorId} update verification failed! Database values don't match expected values.");
                            Debug.LogError($"[ProgressManager] Attempting to update again with direct API call...");
                            
                            // Try a different API endpoint as a fallback
                            StartCoroutine(UpdateDoorStatusFallback(doorId, expectedUnlockable, expectedCompleted));
                        }
                        else
                        {
                            Debug.Log($"[ProgressManager] Door {doorId} update verified successfully!");
                        }
                        
                        // Update our local data with the verified data
                        studentData = verifiedData;
                        isDataLoaded = true;
                        
                        // Notify subscribers that data has been updated
                        OnDataLoaded?.Invoke();
                    }
                    else
                    {
                        Debug.LogWarning($"[ProgressManager] Door {doorId} not found in verification data");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ProgressManager] Error parsing verification data: {e.Message}");
                }
            }
        }
    }
    
    // Fallback method to update door status using a different API endpoint
    private IEnumerator UpdateDoorStatusFallback(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Try a different API endpoint format
        string url = $"{baseUrl}/student/{studentId}";
        
        // Create a proper JSON structure that matches the API expectations
        string jsonPayload = $"{{\"isUnlockable\": {isUnlockable.ToString().ToLower()}, \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}}}";
        
        Debug.Log($"[ProgressManager] Fallback - Sending update to URL: {url}");
        Debug.Log($"[ProgressManager] Fallback - Payload: {jsonPayload}");
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "PUT"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
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
                Debug.Log($"[ProgressManager] Fallback - Door {doorId} status updated successfully");
                Debug.Log($"[ProgressManager] Fallback - Response: {webRequest.downloadHandler.text}");
                
                // Force reload data to verify changes
                StartCoroutine(ReloadDataAfterDelay(1.0f));
            }
        }
    }
    
    // Update door status with direct HTTP request
    private void UpdateDoorStatusDirect(int doorId, bool isUnlockable, bool isRoomCompleted)
    {
        // Exact API endpoint format from the screenshot
        string url = $"{baseUrl}/{studentId}/{doorId}";
        
        // Create the exact JSON structure from the screenshot
        string jsonPayload = "{\n" +
            $"  \"isUnlockable\": {isUnlockable.ToString().ToLower()},\n" +
            $"  \"isRoomCompleted\": {isRoomCompleted.ToString().ToLower()}\n" +
            "}";
        
        Debug.Log($"[ProgressManager] <color=blue>DATABASE WRITE</color> - Sending update to URL: {url}");
        Debug.Log($"[ProgressManager] <color=blue>DATABASE WRITE</color> - Payload: {jsonPayload}");
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
        
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "PUT"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            Debug.Log($"[ProgressManager] <color=blue>DATABASE WRITE</color> - Sending web request for door {doorId}");
            webRequest.SendWebRequest();
            
            // Wait for completion (this is not ideal but ensures sequential execution)
            int waitCount = 0;
            while (!webRequest.isDone)
            {
                // Small delay to prevent freezing
                System.Threading.Thread.Sleep(50);
                waitCount++;
                if (waitCount % 10 == 0) // Log every ~500ms
                {
                    Debug.Log($"[ProgressManager] <color=blue>DATABASE WRITE</color> - Still waiting for door {doorId} update response... ({waitCount * 50}ms)");
                }
            }
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ProgressManager] <color=red>DATABASE ERROR</color> - Error updating door {doorId} status: {webRequest.error}");
                Debug.LogError($"[ProgressManager] <color=red>DATABASE ERROR</color> - Response code: {webRequest.responseCode}");
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    Debug.LogError($"[ProgressManager] <color=red>DATABASE ERROR</color> - Response: {webRequest.downloadHandler.text}");
                }
            }
            else
            {
                Debug.Log($"[ProgressManager] <color=green>DATABASE SUCCESS</color> - Door {doorId} status updated successfully");
                Debug.Log($"[ProgressManager] <color=green>DATABASE SUCCESS</color> - Response: {webRequest.downloadHandler.text}");
                
                // Update local data
                if (studentData != null && studentData.doors != null)
                {
                    DoorData door = studentData.doors.Find(d => d.doorId == doorId);
                    if (door != null)
                    {
                        Debug.Log($"[ProgressManager] <color=green>LOCAL UPDATE</color> - Updating local data for door {doorId} from: isUnlockable={door.isUnlockable}, isRoomCompleted={door.isRoomCompleted} to: isUnlockable={isUnlockable}, isRoomCompleted={isRoomCompleted}");
                        door.isUnlockable = isUnlockable;
                        door.isRoomCompleted = isRoomCompleted;
                        Debug.Log($"[ProgressManager] <color=green>LOCAL UPDATE</color> - Local data updated for door {doorId}");
                    }
                    else
                    {
                        Debug.LogWarning($"[ProgressManager] <color=yellow>LOCAL UPDATE WARNING</color> - Door {doorId} not found in local data after successful database update");
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
        Debug.Log($"[ProgressManager] <color=teal>DATA RELOAD</color> - Scheduled reload after {delay} seconds");
        yield return new WaitForSeconds(delay);
        Debug.Log("[ProgressManager] <color=teal>DATA RELOAD</color> - Forcing data reload from server now");
        LoadStudentDoorData();
    }

    private void UpdateAllDoorInteractions()
    {
        try
        {
            if (!isDataLoaded || studentData == null || studentData.doors == null)
            {
                Debug.LogWarning("[ProgressManager] Cannot update doors: Data not loaded or null");
                return;
            }
                
            // Find all door interactions in the scene
            DoorInteraction[] doorInteractions = FindObjectsOfType<DoorInteraction>();
            
            Debug.Log($"[ProgressManager] Updating {doorInteractions.Length} doors with database values");
            
            // Log all available door IDs in database
            string availableDoorIds = "";
            foreach (var d in studentData.doors)
            {
                if (!string.IsNullOrEmpty(availableDoorIds)) availableDoorIds += ", ";
                availableDoorIds += d.doorId.ToString();
            }
            Debug.Log($"[ProgressManager] Available door IDs in database: {availableDoorIds}");
            
            foreach (DoorInteraction door in doorInteractions)
            {
                if (door != null)
                {
                    UpdateDoorInteraction(door);
                }
            }
            
            // Notify subscribers that all doors have been updated
            OnDataLoaded?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error in UpdateAllDoorInteractions: {e.Message}");
        }
    }

    public void UpdateDoorInteraction(DoorInteraction door)
    {
        try
        {
            if (door == null)
            {
                Debug.LogError("[ProgressManager] <color=red>ERROR</color> - Cannot update null door reference");
                return;
            }
            
            Debug.Log($"[ProgressManager] <color=purple>DOOR UPDATE</color> - Starting UpdateDoorInteraction for door {door.doorID}");
            
            if (!isDataLoaded || studentData == null || studentData.doors == null)
            {
                Debug.LogWarning($"[ProgressManager] <color=yellow>WARNING</color> - Data not loaded yet or null. isDataLoaded: {isDataLoaded}, studentData: {studentData != null}, doors: {(studentData != null && studentData.doors != null)}");
                return;
            }
                
            int doorId = door.doorID;
            
            // Validate doorId is in valid range (1-16)
            if (doorId < 1 || doorId > 16)
            {
                Debug.LogError($"[ProgressManager] <color=red>ERROR</color> - Door ID {doorId} is outside valid range (1-16)");
                return;
            }
            
            Debug.Log($"[ProgressManager] <color=purple>DOOR UPDATE</color> - Looking for door ID {doorId} in database with {studentData.doors.Count} doors");
            
            DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
            
            if (doorData != null)
            {
                Debug.Log($"[ProgressManager] <color=purple>DOOR UPDATE</color> - Found door {doorId} in database with values - Unlockable: {doorData.isUnlockable}, Completed: {doorData.isRoomCompleted}");
                
                // Log current door state before update
                Debug.Log($"[ProgressManager] <color=purple>DOOR UPDATE</color> - Door {doorId} state BEFORE update - isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}");
                
                // Check if values are different
                bool valueChanged = (door.isUnlockable != doorData.isUnlockable || door.isRoomCompleted != doorData.isRoomCompleted);
                
                // Update the door properties
                door.isUnlockable = doorData.isUnlockable;
                door.isRoomCompleted = doorData.isRoomCompleted;
                
                // Log after update
                Debug.Log($"[ProgressManager] <color=purple>DOOR UPDATE</color> - Door {doorId} state AFTER update - isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}, Values Changed: {valueChanged}");
                
                door.UpdateDoorVisuals();
                Debug.Log($"[ProgressManager] <color=purple>DOOR UPDATE</color> - Door {doorId} visuals updated");
            }
            else
            {
                Debug.LogWarning($"[ProgressManager] Door {doorId} not found in database. Creating it now.");
                
                // Create the door data with default values
                EnsureDoorDataExists(doorId, door.gameObject.name);
                
                // Try to find the door again after ensuring it exists
                doorData = studentData.doors.Find(d => d.doorId == doorId);
                if (doorData != null)
                {
                    Debug.Log($"[ProgressManager] After creation - Found door {doorId}. Setting values - Unlockable: {doorData.isUnlockable}, Completed: {doorData.isRoomCompleted}");
                    door.isUnlockable = doorData.isUnlockable;
                    door.isRoomCompleted = doorData.isRoomCompleted;
                    door.UpdateDoorVisuals();
                }
            }
            Debug.Log($"[ProgressManager] === Finished UpdateDoorInteraction for door {door.doorID} ===\n");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ProgressManager] Error in UpdateDoorInteraction: {e.Message}");
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
        if (!isDataLoaded || studentData == null || studentData.doors == null)
        {
            Debug.LogWarning($"[ProgressManager] Data not loaded yet, can't ensure door {doorId} exists");
            return;
        }
        
        // Check if the door exists in the student data
        DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
        
        if (doorData == null)
        {
            // Door doesn't exist, create it
            Debug.Log($"[ProgressManager] Door {doorId} ({doorName}) not found in database, creating it");
            
            // Create new door data with default values
            DoorData newDoorData = new DoorData
            {
                doorId = doorId,
                name = doorName,
                isUnlockable = false,
                isRoomCompleted = false,
            };
            
            // Add to the student data
            studentData.doors.Add(newDoorData);
            
            // Save the updated student data to the database
            StartCoroutine(SaveStudentDoorData());
        }
    }
    
    // Save the current student door data to the database
    private IEnumerator SaveStudentDoorData()
    {
        if (!isDataLoaded || studentData == null)
        {
            Debug.LogError("[ProgressManager] Cannot save student data: Data not loaded");
            yield break;
        }
        
        string url = $"{baseUrl}/update/{studentId}";
        string jsonData = JsonConvert.SerializeObject(studentData);
        
        using (UnityWebRequest webRequest = UnityWebRequest.Put(url, jsonData))
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ProgressManager] Error saving student data: {webRequest.error}");
            }
            else
            {
                Debug.Log("[ProgressManager] Student data saved successfully");
            }
        }
    }

    public void SetStudentId(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            studentId = id;
            Debug.Log($"[ProgressManager] Student ID set to: {studentId}");
        }
    }
    
    // Ensures that all doors from 1 to maxDoorId exist in the database
    private void EnsureAllDoorsExist()
    {
        if (studentData == null || studentData.doors == null)
        {
            Debug.LogError("[ProgressManager] Cannot ensure doors exist: studentData is null");
            return;
        }
        
        Debug.Log($"[ProgressManager] Ensuring all doors from 1 to {maxDoorId} exist");
        
        // Check for each door ID from 1 to maxDoorId
        for (int doorId = 1; doorId <= maxDoorId; doorId++)
        {
            // Check if the door exists
            DoorData existingDoor = studentData.doors.Find(d => d.doorId == doorId);
            
            if (existingDoor == null)
            {
                // Door doesn't exist, create it with default values
                Debug.Log($"[ProgressManager] Door {doorId} not found in database, creating it");
                
                DoorData newDoor = new DoorData
                {
                    doorId = doorId,
                    name = $"Door {doorId}",
                    isUnlockable = (doorId == 1), // Only first door is unlockable by default
                    isRoomCompleted = false,
                    description = $"Door {doorId} Description"
                };
                
                studentData.doors.Add(newDoor);
                Debug.Log($"[ProgressManager] Created door {doorId} with isUnlockable={newDoor.isUnlockable}, isRoomCompleted={newDoor.isRoomCompleted}");
            }
        }
        
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
}

[Serializable]
public class DoorUpdateData
{
    public bool isUnlockable;
    public bool isRoomCompleted;
}
