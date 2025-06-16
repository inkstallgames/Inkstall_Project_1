using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [Header("Prop Settings")]
    [SerializeField] private GameObject[] propPrefabs;
    public int numberOfPropsToSpawn = 5;
    public float minDistanceBetweenProps = 5f;

    [Header("Spawn Point Settings")]
    [SerializeField] Transform spawnParent;

    [Header("Audio Settings")]
    [SerializeField] AudioClip pickupSound;
    [SerializeField] float pickupVolume = 1f;

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

        List<GameObject> shuffledProps = new List<GameObject>(propPrefabs);
        Shuffle(shuffledProps);

        List<Transform> shuffledSpawnPoints = new List<Transform>(spawnPoints);
        Shuffle(shuffledSpawnPoints);

        List<Transform> selectedSpawns = new List<Transform>();

        foreach (Transform candidate in shuffledSpawnPoints)
        {
            bool valid = true;
            foreach (Transform placed in selectedSpawns)
            {
                if (Vector3.Distance(candidate.position, placed.position) < minDistanceBetweenProps)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                selectedSpawns.Add(candidate);
                if (selectedSpawns.Count == numberOfPropsToSpawn)
                    break;
            }
        }

        if (selectedSpawns.Count < numberOfPropsToSpawn)
        {
            Debug.LogWarning($"⚠️ Only {selectedSpawns.Count}/{numberOfPropsToSpawn} props could be placed due to spacing.");
        }

        for (int i = 0; i < selectedSpawns.Count; i++)
        {
            GameObject propToSpawn = shuffledProps[i];
            Transform spawnPoint = selectedSpawns[i];

            GameObject spawnedProp = Instantiate(propToSpawn, spawnPoint.position, spawnPoint.rotation);

            if (!spawnedProp.TryGetComponent<Collider>(out _))
                spawnedProp.AddComponent<BoxCollider>();

            CollectibleProp cp = spawnedProp.GetComponent<CollectibleProp>();
            if (cp == null)
                cp = spawnedProp.AddComponent<CollectibleProp>();

            cp.SetPickupSound(pickupSound, pickupVolume); // 🔊 Assign sound

            spawnedProp.tag = "Collectible";

            if (GameManager.Instance != null)
                GameManager.Instance.RegisterCollectible();
            else
                Debug.LogWarning("❌ GameManager not found during prop registration!");
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
