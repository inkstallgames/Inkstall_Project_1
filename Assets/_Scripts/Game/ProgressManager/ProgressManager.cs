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
    public static event Action OnDataLoaded;
    
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
        // Get student ID from StudentIdManager
        GetStudentId();
        
        // Load student door data from db if we have a student ID
        if (!string.IsNullOrEmpty(studentId))
        {
            StartCoroutine(LoadStudentDoorData());
        }
        else
        {
            Debug.LogWarning("[ProgressManager] No student ID available. Cannot load door data.");
        }
    }
    
    private void GetStudentId()
    {
        // Try to get student ID from StudentIdManager
        if (StudentIdManager.Instance != null)
        {
            string id = StudentIdManager.Instance.GetStudentId();
            if (!string.IsNullOrEmpty(id))
            {
                SetStudentId(id);
                Debug.Log($"[ProgressManager] Got student ID from StudentIdManager: {studentId}");
                return;
            }
            
            // Subscribe to StudentIdManager events to get the ID when it becomes available
            StudentIdManager.Instance.OnStudentIdLoaded += HandleStudentIdLoaded;
        }
        else
        {
            // If still empty, try PlayerPrefs directly
            studentId = PlayerPrefs.GetString("StudentId", "");
            if (!string.IsNullOrEmpty(studentId))
            {
                Debug.Log($"[ProgressManager] Got student ID from PlayerPrefs: {studentId}");
                return;
            }
            
            Debug.LogWarning("[ProgressManager] No student ID found in StudentIdManager or PlayerPrefs");
        }
    }
    
    private void HandleStudentIdLoaded(string id)
    {
        // Unsubscribe to avoid multiple calls
        if (StudentIdManager.Instance != null)
        {
            StudentIdManager.Instance.OnStudentIdLoaded -= HandleStudentIdLoaded;
        }
        
        // Set the student ID and load data
        SetStudentId(id);
        Debug.Log($"[ProgressManager] Student ID loaded from event: {studentId}");
        StartCoroutine(LoadStudentDoorData());
    }

    public IEnumerator LoadStudentDoorData()
    {
        isDataLoaded = false;
        string url = $"{baseUrl}/student/{studentId}";
        
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
                yield return LoadStudentDoorData();
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
        if (!isDataLoaded || studentData == null || studentData.doors == null)
            return;
            
        // Find all door interactions in the scene
        DoorInteraction[] doorInteractions = FindObjectsOfType<DoorInteraction>();
        
        Debug.Log($"[ProgressManager] Updating {doorInteractions.Length} doors with database values");
        
        foreach (DoorInteraction door in doorInteractions)
        {
            UpdateDoorInteraction(door);
        }
    }

    public void UpdateDoorInteraction(DoorInteraction door)
    {
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
