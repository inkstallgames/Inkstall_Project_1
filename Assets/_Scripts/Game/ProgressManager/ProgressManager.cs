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
    public string _id;
    public string studentId;
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

    private string studentId = "68931b31207ee46ce8769a1d"; // This should be set from login or player prefs
    private string baseUrl = "https://api.inkstall.in/api/student-portal/doors";
    
    public StudentDoorsData studentData;
    public bool isDataLoaded = false;
    
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
        // Load door data when the game starts
        StartCoroutine(LoadStudentDoorData());
    }

    public IEnumerator LoadStudentDoorData()
    {
        isDataLoaded = false;
        string url = $"{baseUrl}/student/{studentId}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            // Send the request and wait for response
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error: {webRequest.error}");
                
                // If data doesn't exist, create new student data
                if (webRequest.responseCode == 404)
                {
                    Debug.Log("Student data not found. Creating new data...");
                    yield return CreateNewStudentData();
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
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse JSON: {e.Message}");
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
        
        foreach (DoorInteraction door in doorInteractions)
        {
            UpdateDoorInteraction(door);
        }
    }

    public void UpdateDoorInteraction(DoorInteraction door)
    {
        if (!isDataLoaded || studentData == null || studentData.doors == null)
            return;
            
        // Get the door ID from the door interaction
        int doorId;
        if (int.TryParse(door.GetDoorID(), out doorId))
        {
            // Find the corresponding door data
            DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
            
            if (doorData != null)
            {
                // Update the door interaction with the data from the server
                door.SetUnlockable(doorData.isUnlockable);
                door.SetRoomCompleted(doorData.isRoomCompleted);
            }
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

    public void SetStudentId(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            studentId = id;
            Debug.Log($"[ProgressManager] Student ID set to: {studentId}");
        }
    }
}
