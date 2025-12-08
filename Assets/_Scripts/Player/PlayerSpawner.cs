         using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        // Position the player at the correct floor spawn point
        if (FloorSpawnManager.Instance != null)
        {
            transform.position = FloorSpawnManager.Instance.GetSpawnPosition();
            Debug.Log($"Player spawned at floor {PlayerPrefs.GetInt("CurrentFloor", 0)}");
        }
        else
        {
            Debug.LogWarning("FloorSpawnManager instance not found!");
        }
    }
}
