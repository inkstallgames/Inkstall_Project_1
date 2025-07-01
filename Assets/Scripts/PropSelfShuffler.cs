using UnityEngine;
using System.Collections.Generic;

public class PropSelfShuffler : MonoBehaviour
{
    [Tooltip("Possible spawn points for this prop (empty GameObjects in the scene)")]
    public List<Transform> spawnPoints;

    void Start()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning($"{name} has no spawn points assigned.");
            return;
        }

        // Pick one randomly
        Transform chosen = spawnPoints[Random.Range(0, spawnPoints.Count)];

        transform.position = chosen.position;
        transform.rotation = chosen.rotation;
    }
}
