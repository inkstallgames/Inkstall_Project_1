using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] propPrefabs; // All available prop prefabs
    [SerializeField] private Transform spawnParent;    // Parent containing spawn points
    public int numberOfPropsToSpawn = 5;

    private Transform[] spawnPoints;

    void Awake()
    {
        // Collect all child transforms under the parent as spawn points
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
            Debug.LogWarning("⚠️ Not enough unique props to spawn. Reducing count.");
            numberOfPropsToSpawn = propPrefabs.Length;
        }

        if (numberOfPropsToSpawn > spawnPoints.Length)
        {
            Debug.LogWarning("⚠️ Not enough spawn points. Reducing count.");
            numberOfPropsToSpawn = spawnPoints.Length;
        }

        List<GameObject> shuffledProps = new List<GameObject>(propPrefabs);
        List<Transform> shuffledSpawns = new List<Transform>(spawnPoints);

        // Shuffle props and spawns
        Shuffle(shuffledProps);
        Shuffle(shuffledSpawns);

        for (int i = 0; i < numberOfPropsToSpawn; i++)
        {
            GameObject propToSpawn = shuffledProps[i];
            Transform spawnPoint = shuffledSpawns[i];

            GameObject spawnedProp = Instantiate(propToSpawn, spawnPoint.position, spawnPoint.rotation);

            // Add collider if missing
            if (!spawnedProp.TryGetComponent<Collider>(out _))
            {
                spawnedProp.AddComponent<BoxCollider>();
            }

            // Add CollectibleProp script if missing
            if (!spawnedProp.TryGetComponent<CollectibleProp>(out _))
            {
                spawnedProp.AddComponent<CollectibleProp>();
            }

            // Optional tag
            spawnedProp.tag = "Collectible";

            // ✅ Register with GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterCollectible();
            }
            else
            {
                Debug.LogWarning("❌ GameManager instance not found during prop registration!");
            }
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
