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

    private Transform[] spawnPoints;
    private List<GameObject> spawnedProps = new List<GameObject>();
    private List<Transform> tempSpawnList = new List<Transform>();

    private Dictionary<GameObject, List<GameObject>> propPools = new Dictionary<GameObject, List<GameObject>>();

    void Awake()
    {
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
        foreach (var prefab in propsPrefabs)
        {
            if (!propPools.ContainsKey(prefab))
                propPools[prefab] = new List<GameObject>();

            for (int i = 0; i < totalPropsToSpawn; i++)
            {
                GameObject obj = Instantiate(prefab);
                obj.SetActive(false);
                propPools[prefab].Add(obj);
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
        tempSpawnList.AddRange(spawnPoints);
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

            PropIdentity identity = prop.GetComponent<PropIdentity>();
            if (identity == null) identity = prop.AddComponent<PropIdentity>();
            identity.isFake = false;

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

            PropIdentity identity = fakeProp.GetComponent<PropIdentity>();
            if (identity != null)
            {
                identity.isFake = true;

                CollectibleProp collectible = fakeProp.GetComponent<CollectibleProp>();
                if (collectible == null)
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

        foreach (var obj in propPools[prefab])
        {
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }

        // Optional: expand pool if none available
        GameObject newObj = Instantiate(prefab);
        newObj.SetActive(false);
        propPools[prefab].Add(newObj);
        return newObj;
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
