using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Assign the Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Assign Spawn Points Parent")]
    [SerializeField] private Transform spawnPointsParent;

    private Transform[] spawnPoints;

    void Awake()
    {
        spawnPoints = new Transform[spawnPointsParent.childCount];
        for (int i = 0; i < spawnPointsParent.childCount; i++)
        {
            spawnPoints[i] = spawnPointsParent.GetChild(i);
        }
    }

    void Start()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (playerPrefab == null || spawnPoints.Length == 0)
        {
            // Error Handling
            Debug.LogWarning("Missing player prefab or player spawn points!");
            return;
        }

        // Pick a random spawn point
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Instantiate player at the chosen spawn point
        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

        // ✅ Assign spawned player to GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerController = player;
        }
        else
        {
            Debug.LogWarning("⚠️ GameManager.Instance is null! PlayerController not set.");
        }
    }
}
