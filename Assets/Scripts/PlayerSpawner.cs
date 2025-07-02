using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Assign the Existing Player in Scene")]
    [SerializeField] private GameObject player;  // Reference to the player already in the scene

    [Header("Assign Spawn Points Parent")]
    [SerializeField] private Transform spawnPointsParent;

    private Transform[] spawnPoints;

    void Awake()
    {
        // Pre-allocate and directly fill the array in one step
        int childCount = spawnPointsParent.childCount;
        spawnPoints = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            spawnPoints[i] = spawnPointsParent.GetChild(i);
        }
    }

    void Start()
    {
        MovePlayerToSpawn();
    }

    void MovePlayerToSpawn()
    {
        if (player == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("Missing player reference or spawn points!");
            return;
        }

        // Pick a random spawn point
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Move the existing player to the spawn point
        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        // Assign to GameManager if needed
        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerController = player;
        }
        else
        {
            Debug.LogWarning("GameManager.Instance is null! PlayerController not set.");
        }
    }
}