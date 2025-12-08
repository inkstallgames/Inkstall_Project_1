using UnityEngine;

public class FloorSpawnManager : MonoBehaviour
{
    public static FloorSpawnManager Instance { get; private set; }
    
    [Tooltip("Assign floor spawn points in order: 0=Ground, 1=1st Floor, 2=2nd Floor, etc.")]
    public Transform[] floorSpawnPoints;
    
    private int currentFloor = 0; // 0 = ground floor

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadFloorData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadFloorData()
    {
        currentFloor = PlayerPrefs.GetInt("CurrentFloor", 0);
    }

    public void SetCurrentFloor(int floorIndex)
    {
        if (floorIndex >= 0 && floorIndex < floorSpawnPoints.Length)
        {
            currentFloor = floorIndex;
            PlayerPrefs.SetInt("CurrentFloor", currentFloor);
            PlayerPrefs.Save();
        }
    }

    public Vector3 GetSpawnPosition()
    {
        if (floorSpawnPoints == null || floorSpawnPoints.Length == 0)
        {
            Debug.LogError("No floor spawn points assigned!");
            return Vector3.zero;
        }

        int safeFloor = Mathf.Clamp(currentFloor, 0, floorSpawnPoints.Length - 1);
        return floorSpawnPoints[safeFloor] != null ? 
               floorSpawnPoints[safeFloor].position : 
               Vector3.zero;
    }
}
