using UnityEngine;
using System.Collections.Generic;

public class PropSelfShuffler : MonoBehaviour
{
    [Header("Spawn Option 1: Parent that holds all spawn points")]
    public Transform spawnPointsParent;

    [Header("Spawn Option 2: Manual list of spawn points")]
    public List<Transform> spawnPoints;

    private void Start()
    {
        List<Transform> availablePoints = new List<Transform>();

        // Option 1: Use children of parent
        if (spawnPointsParent != null)
        {
            foreach (Transform child in spawnPointsParent)
            {
                if (child != null)
                    availablePoints.Add(child);
            }
        }

        // Option 2: Use manually assigned list
        if (availablePoints.Count == 0 && spawnPoints != null && spawnPoints.Count > 0)
        {
            availablePoints.AddRange(spawnPoints);
        }

        if (availablePoints.Count == 0)
        {
            return;
        }

        // Choose a random one
        Transform chosen = availablePoints[Random.Range(0, availablePoints.Count)];
        transform.position = chosen.position;
        transform.rotation = chosen.rotation;
    }
}
