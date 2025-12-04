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
        // Load data from PlayerPrefs
        LoadStudentDoorDataFromPrefs();
        
        isDataLoaded = true;
        OnDataLoaded?.Invoke();
    }

    private void LoadStudentDoorDataFromPrefs()
    {
        string json = PlayerPrefs.GetString("StudentDoorData", "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                studentData = JsonUtility.FromJson<StudentDoorsData>(json);
                Debug.Log($"[ProgressManager] Loaded door data from PlayerPrefs: {json}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProgressManager] Failed to parse door data from PlayerPrefs: {e.Message}");
                studentData = new StudentDoorsData();
                studentData.doors = new List<DoorData>();
            }
        }
        else
        {
            studentData = new StudentDoorsData();
            studentData.doors = new List<DoorData>();
        }

        // Ensure we have valid data structure
        if (studentData == null)
        {
            studentData = new StudentDoorsData();
        }
        if (studentData.doors == null)
        {
            studentData.doors = new List<DoorData>();
        }

        // Ensure all doors exist
        EnsureAllDoorsExist();
    }

    private void SaveStudentDoorDataToPrefs()
    {
        if (studentData != null)
        {
            string json = JsonUtility.ToJson(studentData);
            PlayerPrefs.SetString("StudentDoorData", json);
            PlayerPrefs.Save();
            Debug.Log($"[ProgressManager] Saved door data to PlayerPrefs: {json}");
        }
    }
    
    public bool IsDataLoaded()
    {
        return isDataLoaded;
    }
    


    public void MarkRoomAsCompleted(int doorId)
    {
        // Calculate nextDoorId
        int nextDoorId = -1;
        if (doorId < maxDoorId)
        {
             // Check if it's not the last door of a building (6, 12, 18, 24)
             bool isLastDoorInBuilding = (doorId == 6 || doorId == 12 || doorId == 18 || doorId == 24);
             if (!isLastDoorInBuilding)
             {
                 nextDoorId = doorId + 1;
             }
        }

        // Update current door
        UpdateLocalDoorData(doorId, false, true);

        // Update next door if exists
        if (nextDoorId != -1)
        {
             var nextDoor = GetDoorData(nextDoorId);
             bool nextCompleted = nextDoor != null ? nextDoor.isRoomCompleted : false;
             UpdateLocalDoorData(nextDoorId, true, nextCompleted);
        }

        // Update the scene immediately for instant feedback
        UpdateDoorInstancesInScene(doorId, nextDoorId);
    }

    public IEnumerator UpdateDoorStatus(int doorId, Dictionary<string, object> updates)
    {
        if (updates == null || updates.Count == 0) yield break;

        // Get current state
        var door = GetDoorData(doorId);
        bool isUnlockable = door != null ? door.isUnlockable : false;
        bool isRoomCompleted = door != null ? door.isRoomCompleted : false;

        if (updates.ContainsKey("isUnlockable"))
        {
            isUnlockable = (bool)updates["isUnlockable"];
        }
        if (updates.ContainsKey("isRoomCompleted"))
        {
            isRoomCompleted = (bool)updates["isRoomCompleted"];
        }

        UpdateLocalDoorData(doorId, isUnlockable, isRoomCompleted);
        yield break;
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
        }
        else
        {
            studentData.doors.Add(new DoorData 
            { 
                doorId = doorId, 
                isUnlockable = isUnlockable, 
                isRoomCompleted = isRoomCompleted 
            });
        }
        
        // Save changes
        SaveStudentDoorDataToPrefs();
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
                EnsureDoorDataExists(doorId, door.gameObject.name);
                
                // Try to find the door again after ensuring it exists
                doorData = studentData.doors.Find(d => d.doorId == doorId);
                if (doorData != null)
                {
                    // Update the door properties using setter methods
                    door.SetUnlockable(doorData.isUnlockable);
                    door.SetRoomCompleted(doorData.isRoomCompleted);
                    door.UpdateDoorVisuals();
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
            
            // Save the updated student data to the database
            SaveStudentDoorDataToPrefs();
        }
    }
    
    // This method is now deprecated as we use UpdateDoorStatus for more reliable individual updates.
    // It can be kept for bulk-saving if needed in the future, but requires server-side support.
    private IEnumerator SaveStudentDoorData()
    {
        SaveStudentDoorDataToPrefs();
        yield break;
    }


    
    // Ensures that all doors from 1 to maxDoorId exist in the database
    public void EnsureAllDoorsExist()
    {
        if (studentData == null || studentData.doors == null)
        {
            Debug.LogError("[ProgressManager] Cannot ensure doors exist: studentData is null");
            return;
        }
        
        // Check for each door ID from 1 to maxDoorId
        for (int doorId = 1; doorId <= maxDoorId; doorId++)
        {
            // Check if the door exists
            DoorData existingDoor = studentData.doors.Find(d => d.doorId == doorId);
            
            if (existingDoor == null)
            {
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
            }
            else if (doorId == 1)
            {
                // First door should always be unlockable if not completed
                if (!existingDoor.isRoomCompleted && !existingDoor.isUnlockable)
                {
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
                            nextDoor.isUnlockable = true;
                        }
                    }
                }
            }
        }
        
        // Validate door progression logic
        ValidateDoorProgressionLogic();
        
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
            
            if (pendingDoorUpdates.Count > 0)
            {
                
                
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
                            
                            // Get the door data
                            DoorData doorData = studentData.doors.Find(d => d.doorId == doorId);
                            if (doorData != null)
                            {
                                // Update the door properties using setter methods
                                door.SetUnlockable(doorData.isUnlockable);
                                door.SetRoomCompleted(doorData.isRoomCompleted);
                                
                                // Make sure the door updates its visuals
                                door.UpdateDoorVisuals();
                                
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
    
    // No longer creating default door data - only loading from server
    
    // Validate door progression logic to ensure consistency
    private void ValidateDoorProgressionLogic()
    {
        if (studentData == null || studentData.doors == null || studentData.doors.Count == 0)
        {
            return;
        }
        
        bool changesNeeded = false;
        
        // Sort doors by ID to ensure proper order
        studentData.doors.Sort((a, b) => a.doorId.CompareTo(b.doorId));
        
        // First door should always be unlockable if not completed
        DoorData firstDoor = studentData.doors.Find(d => d.doorId == 1);
        if (firstDoor != null && !firstDoor.isRoomCompleted && !firstDoor.isUnlockable)
        {
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
                nextDoor.isUnlockable = true;
                changesNeeded = true;
            }
            
            // If next door is completed or unlockable, all previous doors should be completed
            if ((nextDoor.isRoomCompleted || nextDoor.isUnlockable) && !currentDoor.isRoomCompleted)
            {
                currentDoor.isRoomCompleted = true;
                currentDoor.isUnlockable = false;
                changesNeeded = true;
            }
        }
        
        if (changesNeeded)
        {
            Debug.Log("[ProgressManager-VALIDATE] Door progression inconsistencies found. Saving corrected data.");
            SaveStudentDoorDataToPrefs();
        }
        else
        {
            Debug.Log("[ProgressManager-VALIDATE] Door progression logic is valid.");
        }
    }
    
    // Update door instances in the scene after a room is completed
    private void UpdateDoorInstancesInScene(int completedDoorId, int nextDoorId)
    {
        // Find all door interactions in the scene
        DoorInteraction[] allDoors = FindObjectsOfType<DoorInteraction>();
        
        foreach (DoorInteraction door in allDoors)
        {
            if (door == null) continue;
            
            int doorId = door.doorID;
            
            // Update the completed door
            if (doorId == completedDoorId)
            {
                door.isUnlockable = false;
                door.isRoomCompleted = true;
                door.UpdateDoorVisuals();
            }
            // Update the next door if there is one
            else if (nextDoorId > 0 && doorId == nextDoorId)
            {
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
