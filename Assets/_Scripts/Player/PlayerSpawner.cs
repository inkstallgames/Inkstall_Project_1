using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        // Position and rotate the player at the correct floor spawn point
        if (FloorSpawnManager.Instance != null)
        {
            var (position, rotation) = FloorSpawnManager.Instance.GetSpawnPoint();
            transform.SetPositionAndRotation(position, rotation);
            Debug.Log($"Player spawned at floor {PlayerPrefs.GetInt("CurrentFloor", 0)} with rotation {rotation.eulerAngles}");
        }
        else
        {
            Debug.LogWarning("FloorSpawnManager instance not found!");
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }
    }
}
