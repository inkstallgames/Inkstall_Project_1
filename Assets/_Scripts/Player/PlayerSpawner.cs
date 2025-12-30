using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        // Position and rotate the player at the correct floor spawn point
        if (FloorSpawnManager.Instance != null)
        {
            var (position, rotation) = FloorSpawnManager.Instance.GetSpawnPoint();
            transform.SetPositionAndRotation(position, rotation);
        }
        else
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }
    }
}
