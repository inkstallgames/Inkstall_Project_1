using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [Header("Assign all props prefabs")]
    [SerializeField] private GameObject[] propsPrefabs;

    [Header("Assign World Spawn Points Parent")]
    [SerializeField] private Transform spawnPointsParent;

    [Header("Assign Lockable Containers (Rooms, Chests, etc.)")]
    [SerializeField] private Container[] containers;

    [Header("Prop Settings")]
    [Min(1)] public int totalPropsToSpawn = 15;
    [Min(1)] public int fakePropCount = 5;
    public float minDistanceBetweenProps = 3f;
    public bool preventOverlap = true;
    [Range(0f, 1f)] public float chanceToUseContainer = 0.3f;

    private Transform[] spawnPoints;
    private List<GameObject> spawnedProps = new List<GameObject>();

    void Awake()
    {
        // Cache all spawn point transforms from parent
        List<Transform> points = new List<Transform>();
        foreach (Transform child in spawnPointsParent)
            points.Add(child);
        spawnPoints = points.ToArray();
    }

    void Start()
    {
        SpawnRandomProps();
    }

    void SpawnRandomProps()
    {
        if (propsPrefabs.Length == 0 || (spawnPoints.Length == 0 && containers.Length == 0))
        {
            Debug.LogError("No prop prefabs, spawn points, or containers assigned!");
            return;
        }

        // Shuffle world spawn points
        List<Transform> shuffledSpawns = new List<Transform>(spawnPoints);
        Shuffle(shuffledSpawns);

        int spawnCount = totalPropsToSpawn;
        spawnedProps.Clear();

        for (int i = 0; i < spawnCount; i++)
        {
            GameObject prefab = propsPrefabs[Random.Range(0, propsPrefabs.Length)];
            GameObject prop = null;

            // --- Try spawning inside a container ---
            if (Random.value < chanceToUseContainer && containers.Length > 0)
            {
                List<Container> available = new List<Container>();
                foreach (var c in containers)
                {
                    if (!c.hasPropInside)
                        available.Add(c);
                }

                if (available.Count > 0)
                {
                    Container chosen = available[Random.Range(0, available.Count)];
                    prop = Instantiate(prefab);
                    prop.SetActive(false); // Will be revealed when task is completed
                    chosen.InsertProp(prop);
                }
            }

            // --- Fallback to world position ---
            if (prop == null && i < shuffledSpawns.Count)
            {
                Vector3 spawnPos = shuffledSpawns[i].position;
                if (preventOverlap && IsTooClose(spawnPos)) continue;

                prop = Instantiate(prefab, spawnPos, shuffledSpawns[i].rotation);
            }

            if (prop == null) continue;

            // Ensure prop has identity and mark as real by default
            var identity = prop.GetComponent<PropIdentity>() ?? prop.AddComponent<PropIdentity>();
            identity.isFake = false;

            spawnedProps.Add(prop);
        }

        // --- Assign fake props ---
        Shuffle(spawnedProps);
        for (int i = 0; i < fakePropCount && i < spawnedProps.Count; i++)
        {
            GameObject fakeProp = spawnedProps[i];
            var identity = fakeProp.GetComponent<PropIdentity>();
            identity.isFake = true;

            if (fakeProp.GetComponent<CollectibleProp>() == null)
                fakeProp.AddComponent<CollectibleProp>();

            GameManager.Instance?.RegisterCollectible();
        }

        Debug.Log($"Spawned {spawnedProps.Count} props: {fakePropCount} fake, {spawnedProps.Count - fakePropCount} real.");
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            (list[i], list[rnd]) = (list[rnd], list[i]);
        }
    }

    bool IsTooClose(Vector3 newPos)
    {
        foreach (var existing in spawnedProps)
        {
            if (existing == null) continue;
            if (Vector3.Distance(existing.transform.position, newPos) < minDistanceBetweenProps)
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (spawnPointsParent == null) return;

        Gizmos.color = Color.cyan;
        foreach (Transform child in spawnPointsParent)
        {
            if (child != null)
                Gizmos.DrawWireSphere(child.position, 0.5f);
        }

        if (Application.isPlaying && spawnedProps != null)
        {
            Gizmos.color = Color.red;
            foreach (var prop in spawnedProps)
            {
                if (prop == null || prop.Equals(null)) continue;

                var identity = prop.GetComponent<PropIdentity>();
                if (identity != null && identity.isFake)
                {
                    Gizmos.DrawSphere(prop.transform.position, 0.3f);
                }
            }
        }
    }
#endif
}
