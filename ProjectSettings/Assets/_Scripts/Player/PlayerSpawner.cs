using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"[PlayerSpawner] ===== START =====");
        Debug.Log($"[PlayerSpawner] Current Scene: {SceneManager.GetActiveScene().name} (Build Index: {SceneManager.GetActiveScene().buildIndex})");
        
        // Position and rotate the player at the correct floor spawn point
        if (FloorSpawnManager.Instance != null)
        {
            Debug.Log($"[PlayerSpawner] FloorSpawnManager instance found");
            var (position, rotation) = FloorSpawnManager.Instance.GetSpawnPoint();
            Debug.Log($"[PlayerSpawner] Received spawn position: {position}, rotation: {rotation.eulerAngles}");
            
            transform.SetPositionAndRotation(position, rotation);
            Debug.Log($"[PlayerSpawner] Player transform set to position: {transform.position}, rotation: {transform.rotation.eulerAngles}");
            
            // Get scene-specific PlayerPrefs key
            int sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
            string floorKey = $"CurrentFloor_Scene{sceneBuildIndex}";
            string buildingKey = $"CurrentBuilding_Scene{sceneBuildIndex}";
            int savedFloor = PlayerPrefs.GetInt(floorKey, -1);
            int savedBuilding = PlayerPrefs.GetInt(buildingKey, -1);
            
            Debug.Log($"[PlayerSpawner] PlayerPrefs for this scene - Building: {savedBuilding}, Floor: {savedFloor}");
            Debug.Log($"[PlayerSpawner] ===== SPAWN COMPLETE =====");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawner] FloorSpawnManager instance not found!");
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }
    }
}
