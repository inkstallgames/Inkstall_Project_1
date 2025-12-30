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
        Debug.Log($"[FloorSpawnManager] Awake called in scene: {SceneManager.GetActiveScene().name} (Build Index: {SceneManager.GetActiveScene().buildIndex})");
        
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[FloorSpawnManager] Instance created and set to DontDestroyOnLoad");
            
            // Subscribe to scene loaded event to detect scene changes
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            Initialize();
        }
        else
        {
            Debug.Log("[FloorSpawnManager] Duplicate instance detected, destroying this one");
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
        Debug.Log($"[FloorSpawnManager] OnSceneLoaded - Scene: {scene.name}, Build Index: {scene.buildIndex}");
        
        // Check if we've moved to a different scene
        if (scene.buildIndex != currentSceneBuildIndex)
        {
            Debug.Log($"[FloorSpawnManager] Scene changed from {currentSceneBuildIndex} to {scene.buildIndex}. Re-initializing...");
            isInitialized = false;
            Initialize();
        }
    }
    
    private void Initialize()
    {
        if (isInitialized)
        {
            Debug.Log("[FloorSpawnManager] Already initialized, skipping");
            return;
        }
        
        // Get current scene build index
        currentSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        Debug.Log($"[FloorSpawnManager] ===== INITIALIZE START =====");
        Debug.Log($"[FloorSpawnManager] Current Scene Build Index: {currentSceneBuildIndex}");
        Debug.Log($"[FloorSpawnManager] Scene Name: {SceneManager.GetActiveScene().name}");
        
        // Map scene build index to building number
        // Scene 1 = Building 0, Scene 2 = Building 1, etc.
        int sceneBuildingNumber = Mathf.Max(0, currentSceneBuildIndex - 1);
        Debug.Log($"[FloorSpawnManager] Calculated Scene Building Number: {sceneBuildingNumber}");
        
        // Load saved data for this scene
        LoadFloorData();
        Debug.Log($"[FloorSpawnManager] After LoadFloorData - currentBuilding: {currentBuilding}, currentFloor: {currentFloor}");
        
        // First launch setup - check if PlayerPrefs exist for this scene
        string floorKey = GetSceneSpecificKey("CurrentFloor");
        string buildingKey = GetSceneSpecificKey("CurrentBuilding");
        Debug.Log($"[FloorSpawnManager] PlayerPrefs Keys - Floor: {floorKey}, Building: {buildingKey}");
        Debug.Log($"[FloorSpawnManager] PlayerPrefs Exists - Floor: {PlayerPrefs.HasKey(floorKey)}, Building: {PlayerPrefs.HasKey(buildingKey)}");
        
        if (!PlayerPrefs.HasKey(floorKey) || !PlayerPrefs.HasKey(buildingKey))
        {
            // Set default floor based on build index
            // If build index is 2, start from floor 1, otherwise start from floor 0
            currentFloor = (currentSceneBuildIndex == 2) ? 1 : 0;
            currentBuilding = sceneBuildingNumber;
            Debug.Log($"[FloorSpawnManager] NO PLAYERPREFS FOUND - Setting defaults: Building {currentBuilding}, Floor {currentFloor}");
            SaveFloorData();
        }
        else
        {
            Debug.Log($"[FloorSpawnManager] PLAYERPREFS FOUND - Loaded: Building {currentBuilding}, Floor {currentFloor}");
            // Ensure the building number matches the current scene
            if (currentBuilding != sceneBuildingNumber)
            {
                Debug.Log($"[FloorSpawnManager] BUILDING MISMATCH! Loaded Building {currentBuilding} but scene expects Building {sceneBuildingNumber}");
                currentBuilding = sceneBuildingNumber;
                SaveFloorData();
            }
            else
            {
                Debug.Log($"[FloorSpawnManager] Building number matches scene. All good!");
            }
        }
        
        Debug.Log($"[FloorSpawnManager] Final values before ProgressManager check - Building: {currentBuilding}, Floor: {currentFloor}");
        
        // Check ProgressManager for room completion data
        if (ProgressManager.Instance != null)
        {
            if (ProgressManager.Instance.IsDataLoaded())
            {
                Debug.Log($"[FloorSpawnManager] ProgressManager data is loaded. Calling UpdateSpawnPointForBuilding({currentBuilding})");
                UpdateSpawnPointForBuilding(currentBuilding);
            }
            else
            {
                Debug.Log($"[FloorSpawnManager] ProgressManager data NOT loaded yet. Subscribing to OnDataLoaded event.");
                // Wait for ProgressManager to load data
                ProgressManager.OnDataLoaded += OnProgressDataLoaded;
            }
        }
        else
        {
            Debug.LogWarning("ProgressManager not found! Spawn points won't update based on room completion.");
        }
        
        Debug.Log($"[FloorSpawnManager] ===== INITIALIZE END ===== Final: Building {currentBuilding}, Floor {currentFloor}");
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
        
        Debug.Log($"[FloorSpawnManager] Loaded data for scene {currentSceneBuildIndex}: Building {currentBuilding}, Floor {currentFloor}");
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
            Debug.Log($"[FloorSpawnManager] Updated spawn to Building {currentBuilding}, Floor {currentFloor}");
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
            Debug.LogError($"Invalid spawn point: Building {newBuilding}, Floor {floor}. Index {spawnIndex} out of range.");
            return;
        }
        
        currentFloor = floor;
        currentBuilding = newBuilding;
        SaveFloorData();
        
        Debug.Log($"[FloorSpawnManager] Spawn point set to Building {currentBuilding}, Floor {currentFloor}");
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
            Debug.Log($"[FloorSpawnManager] Door {doorId} unlocked on floor {doorFloor}, updating spawn point");
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
        
        Debug.Log($"[FloorSpawnManager] GetSpawnPointForBuilding - Building: {buildingNumber}, Floor: {currentFloor}, Calculated Index: {spawnIndex}");
        
        if (spawnIndex < 0 || spawnIndex >= floorSpawnPoints.Length)
        {
            Debug.LogError($"[FloorSpawnManager] Invalid spawn point: Building {buildingNumber}, Floor {currentFloor}. " +
                         $"Index {spawnIndex} out of range (0-{floorSpawnPoints.Length - 1}).");
            return floorSpawnPoints.Length > 0 ? floorSpawnPoints[0] : null;
        }
        
        Debug.Log($"[FloorSpawnManager] Returning spawn point at index {spawnIndex}: {floorSpawnPoints[spawnIndex].name}");
        return floorSpawnPoints[spawnIndex];
    }
    
    // For backward compatibility with PlayerSpawner
    public (Vector3 position, Quaternion rotation) GetSpawnPoint()
    {
        Debug.Log($"[FloorSpawnManager] GetSpawnPoint called - Current Building: {currentBuilding}, Current Floor: {currentFloor}");
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
        
        Debug.Log($"[FloorSpawnManager] Saved data for scene {currentSceneBuildIndex}: Building {currentBuilding}, Floor {currentFloor}");
    }
}
