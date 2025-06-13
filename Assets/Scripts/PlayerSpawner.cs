using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{    [Header("Assign the Player Prefab")]
    public GameObject playerPrefab;

    [Header("List of Spawn Points")]
    public Transform[] spawnPoints;


    // Start is called before the first frame update
    void Start()
    {
        SpawnPlayer();
    }

    void SpawnPlayer()
    {
        if (playerPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("Missing player prefab or spawn points!");
            return;
        }

        // Pick a random spawn point
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Instantiate player at the chosen spawn point
        Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
    }
    
}
