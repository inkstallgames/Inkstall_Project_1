using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [Header("Prop Settings")]
    [SerializeField] private GameObject[] propPrefabs; // All available prop prefabs
    public int numberOfPropsToSpawn = 5;
    public float minDistanceBetweenProps = 5f; // ✅ Minimum allowed distance between props

    [Header("Spawn Point Settings")]
    [SerializeField] private Transform spawnParent;    // Parent containing spawn points

    private Transform[] spawnPoints;

    void Awake()
    {
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
        if (propPrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogError("❌ No prop prefabs or spawn points assigned!");
            return;
        }

        if (numberOfPropsToSpawn > propPrefabs.Length)
        {
            Debug.LogWarning("⚠️ Not enough unique props. Reducing count.");
            numberOfPropsToSpawn = propPrefabs.Length;
        }

        if (numberOfPropsToSpawn > spawnPoints.Length)
        {
            Debug.LogWarning("⚠️ Not enough spawn points. Reducing count.");
            numberOfPropsToSpawn = spawnPoints.Length;
        }

        List<GameObject> shuffledProps = new List<GameObject>(propPrefabs);
        Shuffle(shuffledProps);

        List<Transform> usedSpawns = new List<Transform>();
        int spawnedCount = 0;
        int attempts = 0;
        int maxAttempts = 100; // avoid infinite loop

        while (spawnedCount < numberOfPropsToSpawn && attempts < maxAttempts)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // Check distance from already used spawn points
            bool tooClose = false;
            foreach (Transform used in usedSpawns)
            {
                if (Vector3.Distance(spawnPoint.position, used.position) < minDistanceBetweenProps)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                GameObject propToSpawn = shuffledProps[spawnedCount];
                GameObject spawnedProp = Instantiate(propToSpawn, spawnPoint.position, spawnPoint.rotation);

                // Add collider if missing
                if (!spawnedProp.TryGetComponent<Collider>(out _))
                    spawnedProp.AddComponent<BoxCollider>();

                // Add CollectibleProp script if missing
                if (!spawnedProp.TryGetComponent<CollectibleProp>(out _))
                    spawnedProp.AddComponent<CollectibleProp>();

                // Optional tag
                spawnedProp.tag = "Collectible";

                // Register with GameManager
                if (GameManager.Instance != null)
                    GameManager.Instance.RegisterCollectible();
                else
                    Debug.LogWarning("❌ GameManager not found during prop registration!");

                usedSpawns.Add(spawnPoint);
                spawnedCount++;
            }

            attempts++;
        }

        if (spawnedCount < numberOfPropsToSpawn)
        {
            Debug.LogWarning($"⚠️ Only {spawnedCount}/{numberOfPropsToSpawn} props spawned due to spacing limits.");
        }
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }
}
