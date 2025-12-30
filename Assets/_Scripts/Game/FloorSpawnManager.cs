using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class FloorSpawnManager : MonoBehaviour
{
    public static FloorSpawnManager Instance { get; private set; }
    
    [Tooltip("Assign floor spawn points in order: Building0_Floor0, Building0_Floor1, Building0_Floor2, Building1_Floor0, etc.")]
    public Transform[] floorSpawnPoints;
    
    [Tooltip("Number of floors per building")]
    private int floorsPerBuilding = 3;
    
    private int currentFloor = 0;
    private int currentBuilding = 0;
    private bool isInitialized = false;
    private int currentSceneBuildIndex;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Subscribe to scene loaded event to detect scene changes
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Check if we've moved to a different scene
        if (scene.buildIndex != currentSceneBuildIndex)
        {
            isInitialized = false;
            Initialize();
        }
    }
    
    private void Initialize()
    {
        if (isInitialized)
        {
            return;
        }
        
        // Get current scene build index
        currentSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        // Map scene build index to building number (0-based)
        // Scene 1 (Build Index 1) -> Building 0
        // Scene 2 (Build Index 2) -> Building 1
        // etc.
        int sceneBuildingNumber = Mathf.Max(0, currentSceneBuildIndex - 1);
        
        // Load saved floor data for this scene
        LoadFloorData();
        
        // Get scene-specific PlayerPrefs keys
        string floorKey = $"CurrentFloor_Scene{currentSceneBuildIndex}";
        string buildingKey = $"CurrentBuilding_Scene{currentSceneBuildIndex}";
        
        // If no saved data exists for this scene, initialize with default values
        if (!PlayerPrefs.HasKey(floorKey) || !PlayerPrefs.HasKey(buildingKey))
        {
            // If this is the second scene (Build Index 2), start at floor 1 instead of 0
            currentFloor = (currentSceneBuildIndex == 2) ? 1 : 0;
            currentBuilding = sceneBuildingNumber;
            SaveFloorData();
        }
        else if (currentBuilding != sceneBuildingNumber)
        {
            currentBuilding = sceneBuildingNumber;
            SaveFloorData();
        }
        
        // Update spawn point based on ProgressManager data if available
        if (ProgressManager.Instance != null && ProgressManager.Instance.IsDataLoaded())
        {
            UpdateSpawnPointForBuilding(currentBuilding);
        }
        
        isInitialized = true;
    }
    
    // Generate scene-specific PlayerPrefs key
    private string GetSceneSpecificKey(string baseKey)
    {
        return $"{baseKey}_Scene{currentSceneBuildIndex}";
    }
    
    private void OnProgressDataLoaded()
    {
        UpdateSpawnPointForBuilding(currentBuilding);
        // Unsubscribe using the same method reference
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
    }

    private void LoadFloorData()
    {
        string buildingKey = GetSceneSpecificKey("CurrentBuilding");
        string floorKey = GetSceneSpecificKey("CurrentFloor");
        
        currentBuilding = PlayerPrefs.GetInt(buildingKey, 0);
        currentFloor = PlayerPrefs.GetInt(floorKey, 0);
        
    }

    public void UpdateSpawnPointForBuilding(int buildingNumber)
    {
        if (ProgressManager.Instance == null) return;
        
        int highestFloor = 0;
        
        // Calculate room range for this building (6 rooms per building: 2 per floor)
        int roomStart = buildingNumber * 6 + 1;
        int roomEnd = roomStart + 5;
        
        // Check all rooms in this building
        for (int roomId = roomStart; roomId <= roomEnd; roomId++)
        {
            var door = ProgressManager.Instance.GetDoorData(roomId);
            if (door != null && door.isRoomCompleted)
            {
                // Calculate floor within this building (0-2 for 3 floors)
                int floor = (roomId - roomStart) / 2;
                if (floor > highestFloor)
                {
                    highestFloor = floor;
                }
            }
        }
        
        // Update spawn point if we found a higher floor or changed buildings
        if (highestFloor > currentFloor || buildingNumber != currentBuilding)
        {
            currentBuilding = buildingNumber;
            currentFloor = highestFloor;
            SaveFloorData();
        }
    }
    
    // For backward compatibility
    public void UpdateSpawnPointFromProgress()
    {
        UpdateSpawnPointForBuilding(currentBuilding);
    }

    public void SetCurrentFloor(int floor, int building = -1)
    {
        int newBuilding = building >= 0 ? building : currentBuilding;
        
        // Calculate the spawn point index
        int spawnIndex = (newBuilding * floorsPerBuilding) + floor;
        
        if (spawnIndex < 0 || spawnIndex >= floorSpawnPoints.Length)
        {
            return;
        }
        
        currentFloor = floor;
        currentBuilding = newBuilding;
        SaveFloorData();
    }

    // Call this when a room is completed to update the spawn point if needed
    public void OnRoomCompleted(int roomId)
    {
        if (!isInitialized) Initialize();
        UpdateSpawnPointForBuilding(currentBuilding);
    }
    
    // Call this when a door is unlocked with a key
    public void OnDoorUnlocked(int doorId)
    {
        if (!isInitialized) Initialize();
        
        // Calculate which floor this door is on (1-2 = floor 0, 3-4 = floor 1, 5-6 = floor 2)
        int doorFloor = (doorId - 1) / 2;
        
        // Only update if this door is on a higher floor than current
        if (doorFloor > currentFloor)
        {
            SetCurrentFloor(doorFloor);
        }
    }

    public Transform GetCurrentSpawnPoint()
    {
        return GetSpawnPointForBuilding(currentBuilding);
    }
    
    public Transform GetSpawnPointForBuilding(int buildingNumber)
    {
        // Calculate the spawn point index
        int spawnIndex = (buildingNumber * floorsPerBuilding) + currentFloor;
        
        if (spawnIndex < 0 || spawnIndex >= floorSpawnPoints.Length || floorSpawnPoints[spawnIndex] == null)
        {
            return null;
        }
        
        return floorSpawnPoints[spawnIndex];
    }
    
    // For backward compatibility with PlayerSpawner
    public (Vector3 position, Quaternion rotation) GetSpawnPoint()
    {
        Transform spawnPoint = GetSpawnPointForBuilding(currentBuilding);
        if (spawnPoint != null)
        {
            Debug.Log($"[FloorSpawnManager] Returning spawn position: {spawnPoint.position}, rotation: {spawnPoint.rotation.eulerAngles}");
            return (spawnPoint.position, spawnPoint.rotation);
        }
        Debug.LogError("[FloorSpawnManager] No spawn point found, returning zero position");
        return (Vector3.zero, Quaternion.identity);
    }
    
    private void SaveFloorData()
    {
        string buildingKey = GetSceneSpecificKey("CurrentBuilding");
        string floorKey = GetSceneSpecificKey("CurrentFloor");
        
        PlayerPrefs.SetInt(buildingKey, currentBuilding);
        PlayerPrefs.SetInt(floorKey, currentFloor);
        PlayerPrefs.Save();
        
    }
}
