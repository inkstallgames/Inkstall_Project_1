using System;
using System.Collections;
using System.Collections.Generic;
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

    public void LoadStudentDoorData()
    {
        if (!string.IsNullOrEmpty(studentId))
        {
            Debug.Log($"[ProgressManager] Loading door data for student ID: {studentId}");
            StartCoroutine(LoadStudentDoorDataCoroutine());
        }
        else
        {
            Debug.LogError("[ProgressManager] Cannot load door data: studentId is null or empty");
        }
    }

    private IEnumerator LoadStudentDoorDataCoroutine()
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
                
                // Check if the student exists
                if (webRequest.responseCode == 404)
                {
                    Debug.Log("Student not found, creating new student data");
                    yield return StartCoroutine(CreateNewStudentData());
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
                    
                    // Update all door interactions in the scene
                    UpdateAllDoorInteractions();
                    
                    Debug.Log("Student data loaded successfully");
                    
                    // Notify subscribers that data is loaded
                    OnDataLoaded?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing student data: {e.Message}");
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
        string url = $"{baseUrl}/{studentId}/{doorId}";
        
        // Create the payload
        string payload = JsonUtility.ToJson(new 
        {
            isUnlockable = isUnlockable,
            isRoomCompleted = isRoomCompleted
        });
        
        using (UnityWebRequest webRequest = UnityWebRequest.Put(url, payload))
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error updating door status: {webRequest.error}");
            }
            else
            {
                Debug.Log($"Door {doorId} status updated successfully");
                
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
        }
    }

    public void MarkRoomAsCompleted(int doorId)
    {
        if (studentData != null && studentData.doors != null)
        {
            DoorData door = studentData.doors.Find(d => d.doorId == doorId);
            if (door != null)
            {
                // Update the next door to be unlockable
                int nextDoorId = doorId + 1;
                DoorData nextDoor = studentData.doors.Find(d => d.doorId == nextDoorId);
                
                if (nextDoor != null)
                {
                    StartCoroutine(UpdateDoorStatus(doorId, door.isUnlockable, true)); // Mark current room as completed
                    StartCoroutine(UpdateDoorStatus(nextDoorId, true, nextDoor.isRoomCompleted)); // Make next door unlockable
                }
                else
                {
                    StartCoroutine(UpdateDoorStatus(doorId, door.isUnlockable, true)); // Just mark current room as completed
                }
            }
        }
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
                Debug.LogError("[ProgressManager] Cannot update null door reference");
                return;
            }
            
            Debug.Log($"[ProgressManager] === Starting UpdateDoorInteraction for door {door.doorID} ===");
            
            if (!isDataLoaded || studentData == null || studentData.doors == null)
            {
                Debug.LogWarning($"[ProgressManager] Data not loaded yet or null. isDataLoaded: {isDataLoaded}, studentData: {studentData != null}, doors: {(studentData != null && studentData.doors != null)}");
                return;
            }
                
            int doorId = door.doorID;
            Debug.Log($"[ProgressManager] Looking for door ID {doorId} in database with {studentData.doors.Count} doors");
            
            // Print all available door IDs and their unlockable states
            foreach (var d in studentData.doors)
            {
                Debug.Log($"[ProgressManager] DB Door - ID: {d.doorId}, Name: {d.name}, Unlockable: {d.isUnlockable}, Completed: {d.isRoomCompleted}");
            }
            
            DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
            
            if (doorData != null)
            {
                Debug.Log($"[ProgressManager] Found door {doorId} in database. Setting values - Unlockable: {doorData.isUnlockable}, Completed: {doorData.isRoomCompleted}");
                
                // Log current door state before update
                Debug.Log($"[ProgressManager] Before update - Door {doorId} state - isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}");
                
                // Update the door properties
                door.isUnlockable = doorData.isUnlockable;
                door.isRoomCompleted = doorData.isRoomCompleted;
                
                // Log after update
                Debug.Log($"[ProgressManager] After update - Door {doorId} state - isUnlockable: {door.isUnlockable}, isRoomCompleted: {door.isRoomCompleted}");
                
                door.UpdateDoorVisuals();
            }
            else
            {
                string availableDoorIds = "";
                foreach (var d in studentData.doors)
                {
                    if (!string.IsNullOrEmpty(availableDoorIds)) availableDoorIds += ", ";
                    availableDoorIds += d.doorId;
                }
                Debug.LogWarning($"[ProgressManager] Door {doorId} not found in database. Available door IDs: {availableDoorIds}");
                
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
}
