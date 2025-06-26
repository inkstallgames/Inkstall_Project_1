using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [Header("Assign all props prefabs")]
    [SerializeField] private GameObject[] propsPrefabs;

    [Header("Assign World Spawn Points Parent")]
    [SerializeField] private Transform spawnPointsParent;

    [Header("Prop Settings")]
    [Min(1)] public int totalPropsToSpawn = 15;
    [Min(1)] public int fakePropCount = 5;
    public float minDistanceBetweenProps = 3f;
    public bool preventOverlap = true;
    
    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 30; // Larger initial pool to avoid runtime instantiation

    private Transform[] spawnPoints;
    private List<GameObject> spawnedProps;
    private List<Transform> tempSpawnList;

    private Dictionary<GameObject, List<GameObject>> propPools;
    
    // Cache for component lookups
    private PropIdentity tempIdentity;
    private CollectibleProp tempCollectible;

    void Awake()
    {
        // Pre-allocate collections to avoid GC allocations
        spawnedProps = new List<GameObject>(totalPropsToSpawn);
        tempSpawnList = new List<Transform>(50); // Reasonable capacity based on expected use
        propPools = new Dictionary<GameObject, List<GameObject>>(propsPrefabs.Length);
        
        // Cache spawn points
        int childCount = spawnPointsParent.childCount;
        spawnPoints = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
        {
            spawnPoints[i] = spawnPointsParent.GetChild(i);
        }

        InitializePools();
    }

    void Start()
    {
        SpawnRandomProps();
    }

    void InitializePools()
    {
        // Use larger initial pool size to avoid runtime instantiation
        int poolSize = Mathf.Max(initialPoolSize, totalPropsToSpawn);
        
        foreach (var prefab in propsPrefabs)
        {
            // Pre-allocate list with capacity to avoid resizing
            List<GameObject> pool = new List<GameObject>(poolSize);
            propPools[prefab] = pool;

            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(prefab);
                obj.SetActive(false);
                pool.Add(obj);
            }
        }
    }

    void SpawnRandomProps()
    {
        if (propsPrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogError("No prop prefabs or spawn points assigned!");
            return;
        }

        tempSpawnList.Clear();
        // Use AddRange with capacity already set to avoid resizing
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            tempSpawnList.Add(spawnPoints[i]);
        }
        Shuffle(tempSpawnList);

        int spawnCount = Mathf.Min(totalPropsToSpawn, tempSpawnList.Count);
        spawnedProps.Clear();

        int spawned = 0;
        int attempts = 0;

        while (spawned < spawnCount && attempts < tempSpawnList.Count)
        {
            Transform spawnPoint = tempSpawnList[attempts];
            attempts++;

            if (preventOverlap && IsTooClose(spawnPoint.position)) continue;

            GameObject prefab = propsPrefabs[Random.Range(0, propsPrefabs.Length)];
            GameObject prop = GetFromPool(prefab);
            if (prop == null) continue;

            prop.transform.position = spawnPoint.position;
            prop.transform.rotation = spawnPoint.rotation;
            prop.transform.SetParent(spawnPoint);
            prop.SetActive(true);

            // Cache component lookup
            tempIdentity = prop.GetComponent<PropIdentity>();
            if (tempIdentity == null) tempIdentity = prop.AddComponent<PropIdentity>();
            tempIdentity.isFake = false;

            spawnedProps.Add(prop);
            spawned++;
        }

        // Assign fake props
        Shuffle(spawnedProps);
        int fakesToAssign = Mathf.Min(fakePropCount, spawnedProps.Count);
        for (int i = 0; i < fakesToAssign; i++)
        {
            GameObject fakeProp = spawnedProps[i];
            if (fakeProp == null) continue;

            // Reuse cached component reference
            tempIdentity = fakeProp.GetComponent<PropIdentity>();
            if (tempIdentity != null)
            {
                tempIdentity.isFake = true;

                // Cache component lookup
                tempCollectible = fakeProp.GetComponent<CollectibleProp>();
                if (tempCollectible == null)
                {
                    fakeProp.AddComponent<CollectibleProp>();
                }

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.RegisterCollectible();
                }
            }
        }

        Debug.Log($"Spawned {spawnedProps.Count} props: {fakesToAssign} fake, {spawnedProps.Count - fakesToAssign} real.");
    }

    GameObject GetFromPool(GameObject prefab)
    {
        if (!propPools.ContainsKey(prefab)) return null;

        List<GameObject> pool = propPools[prefab];
        for (int i = 0; i < pool.Count; i++)
        {
            GameObject obj = pool[i];
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }

        // If we get here, the pool is exhausted - we should never need to expand if initial pool size is adequate
        Debug.LogWarning($"Object pool for {prefab.name} was exhausted. Consider increasing initialPoolSize.");
        return null;
    }

    void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int rnd = Random.Range(i, n);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }

    bool IsTooClose(Vector3 newPos)
    {
        for (int i = 0; i < spawnedProps.Count; i++)
        {
            GameObject existing = spawnedProps[i];
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
                if (prop == null) continue;

                PropIdentity identity = prop.GetComponent<PropIdentity>();
                if (identity != null && identity.isFake)
                {
                    Gizmos.DrawSphere(prop.transform.position, 0.3f);
                    UnityEditor.Handles.Label(prop.transform.position + Vector3.up * 0.5f, "Fake Prop");
                }
            }
        }
    }
#endif
}
