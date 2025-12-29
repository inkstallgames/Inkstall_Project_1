using UnityEngine;
using System.Collections.Generic;

public class FloorSpawnManager : MonoBehaviour
{
    public static FloorSpawnManager Instance { get; private set; }
    
    [Tooltip("Assign floor spawn points in order: Building0_Floor0, Building0_Floor1, Building0_Floor2, Building1_Floor0, etc.")]
    public Transform[] floorSpawnPoints;
    
    [Tooltip("Number of floors per building")]
    public int floorsPerBuilding = 3;
    
    private int currentFloor = 0;
    private int currentBuilding = 0;
    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Initialize()
    {
        if (isInitialized) return;
        
        // Load saved data
        LoadFloorData();
        
        // First launch setup
        if (!PlayerPrefs.HasKey("CurrentFloor") || !PlayerPrefs.HasKey("CurrentBuilding"))
        {
            currentFloor = 0;
            currentBuilding = 0;
            SaveFloorData();
            Debug.Log("[FloorSpawnManager] First launch - setting spawn to Building 0, Floor 0");
        }
        
        // Check ProgressManager for room completion data
        if (ProgressManager.Instance != null)
        {
            if (ProgressManager.Instance.IsDataLoaded())
            {
                UpdateSpawnPointForBuilding(currentBuilding);
            }
            else
            {
                // Wait for ProgressManager to load data
                ProgressManager.OnDataLoaded += OnProgressDataLoaded;
            }
        }
        else
        {
            Debug.LogWarning("ProgressManager not found! Spawn points won't update based on room completion.");
        }
        
        isInitialized = true;
    }
    
    private void OnProgressDataLoaded()
    {
        UpdateSpawnPointForBuilding(currentBuilding);
        // Unsubscribe using the same method reference
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
    }

    private void LoadFloorData()
    {
        currentBuilding = PlayerPrefs.GetInt("CurrentBuilding", 0);
        currentFloor = PlayerPrefs.GetInt("CurrentFloor", 0);
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
        
        if (spawnIndex < 0 || spawnIndex >= floorSpawnPoints.Length)
        {
            Debug.LogError($"[FloorSpawnManager] Invalid spawn point: Building {buildingNumber}, Floor {currentFloor}. " +
                         $"Index {spawnIndex} out of range (0-{floorSpawnPoints.Length - 1}).");
            return floorSpawnPoints.Length > 0 ? floorSpawnPoints[0] : null;
        }
        
        return floorSpawnPoints[spawnIndex];
    }
    
    // For backward compatibility with PlayerSpawner
    public (Vector3 position, Quaternion rotation) GetSpawnPoint()
    {
        Transform spawnPoint = GetSpawnPointForBuilding(currentBuilding);
        if (spawnPoint != null)
        {
            return (spawnPoint.position, spawnPoint.rotation);
        }
        return (Vector3.zero, Quaternion.identity);
    }
    
    private void SaveFloorData()
    {
        PlayerPrefs.SetInt("CurrentBuilding", currentBuilding);
        PlayerPrefs.SetInt("CurrentFloor", currentFloor);
        PlayerPrefs.Save();
    }
}
