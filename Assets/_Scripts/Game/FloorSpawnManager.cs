using UnityEngine;

public class FloorSpawnManager : MonoBehaviour
{
    public static FloorSpawnManager Instance { get; private set; }
    
    [Tooltip("Assign floor spawn points in order: 0=Ground, 1=1st Floor, 2=2nd Floor, etc.")]
    public Transform[] floorSpawnPoints;
    
    private int currentFloor = 0; // 0 = ground floor
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
        
        // First, load any saved floor data
        LoadFloorData();
        
        // If no floor is saved (first launch), set to floor 0 (spawn point 1)
        if (!PlayerPrefs.HasKey("CurrentFloor"))
        {
            currentFloor = 0;
            PlayerPrefs.SetInt("CurrentFloor", currentFloor);
            PlayerPrefs.Save();
            Debug.Log("[FloorSpawnManager] First launch - setting spawn to floor 0");
        }
        
        // Then check ProgressManager for higher floors if available
        if (ProgressManager.Instance != null)
        {
            if (ProgressManager.Instance.IsDataLoaded())
            {
                UpdateSpawnPointFromProgress();
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
        UpdateSpawnPointFromProgress();
        ProgressManager.OnDataLoaded -= OnProgressDataLoaded;
    }

    private void LoadFloorData()
    {
        currentFloor = PlayerPrefs.GetInt("CurrentFloor", 0);
    }

    public void UpdateSpawnPointFromProgress()
    {
        if (ProgressManager.Instance == null) return;
        
        int highestFloor = 0;
        
        // Check all doors to find the highest completed floor
        for (int i = 1; i <= 6; i++) // Assuming 6 rooms total (1-6)
        {
            var door = ProgressManager.Instance.GetDoorData(i);
            if (door != null && door.isRoomCompleted)
            {
                int floor = (i - 1) / 2; // Calculate floor from room ID
                if (floor > highestFloor)
                {
                    highestFloor = floor;
                }
            }
        }
        
        // Update to the highest floor with completed rooms
        if (highestFloor > currentFloor)
        {
            SetCurrentFloor(highestFloor);
        }
    }

    private void SetCurrentFloor(int floorIndex)
    {
        if (floorIndex >= 0 && floorIndex < floorSpawnPoints.Length)
        {
            currentFloor = floorIndex;
            PlayerPrefs.SetInt("CurrentFloor", currentFloor);
            PlayerPrefs.Save();
            Debug.Log($"[FloorSpawnManager] Spawn point updated to floor {currentFloor}");
        }
    }
    
    // Call this when a room is completed to update the spawn point if needed
    public void OnRoomCompleted(int roomId)
    {
        if (!isInitialized) Initialize();
        UpdateSpawnPointFromProgress();
    }

    public (Vector3 position, Quaternion rotation) GetSpawnPoint()
    {
        if (floorSpawnPoints == null || floorSpawnPoints.Length == 0)
        {
            Debug.LogError("No floor spawn points assigned!");
            return (Vector3.zero, Quaternion.identity);
        }

        int safeFloor = Mathf.Clamp(currentFloor, 0, floorSpawnPoints.Length - 1);
        if (floorSpawnPoints[safeFloor] != null)
        {
            return (floorSpawnPoints[safeFloor].position, floorSpawnPoints[safeFloor].rotation);
        }
        return (Vector3.zero, Quaternion.identity);
    }
}
