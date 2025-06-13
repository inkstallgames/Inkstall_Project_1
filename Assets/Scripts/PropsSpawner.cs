using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] propPrefabs; // Total 100 props
    [SerializeField] private Transform spawnParent; // Parent of all spawn points

    private Transform[] spawnPoints;  // Automatically populated
    public int numberOfPropsToSpawn = 5;

    void Awake()
    {
        // Collect all child transforms under the parent
        List<Transform> points = new List<Transform>();
        foreach (Transform child in spawnParent)
        {
            points.Add(child);
        }
        spawnPoints = points.ToArray();
    }

    void Start()
    {
        SpawnRandomProps();
    }

    void SpawnRandomProps()
    {
        // Clone arrays to lists for shuffling
        List<GameObject> shuffledProps = new List<GameObject>(propPrefabs);
        List<Transform> shuffledSpawns = new List<Transform>(spawnPoints);

        // Shuffle props
        for (int i = 0; i < shuffledProps.Count; i++)
        {
            int rnd = Random.Range(i, shuffledProps.Count);
            var temp = shuffledProps[i];
            shuffledProps[i] = shuffledProps[rnd];
            shuffledProps[rnd] = temp;
        }

        // Shuffle spawn points
        for (int i = 0; i < shuffledSpawns.Count; i++)
        {
            int rnd = Random.Range(i, shuffledSpawns.Count);
            var temp = shuffledSpawns[i];
            shuffledSpawns[i] = shuffledSpawns[rnd];
            shuffledSpawns[rnd] = temp;
        }

        // Spawn unique props at unique spawn points
        for (int i = 0; i < numberOfPropsToSpawn; i++)
        {
            GameObject propToSpawn = shuffledProps[i];
            Transform spawnPoint = shuffledSpawns[i];
            Instantiate(propToSpawn, spawnPoint.position, spawnPoint.rotation);
        }
    }
}
