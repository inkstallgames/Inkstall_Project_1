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
    
    // Pre-allocate lists to avoid GC allocations
    private List<Transform> shuffledSpawns;

    void Awake()
    {
        // Pre-allocate the array based on expected size
        int childCount = spawnPointsParent.childCount;
        spawnPoints = new Transform[childCount];
        shuffledSpawns = new List<Transform>(childCount);
        
        // Directly fill the array without creating a temporary list
        for (int i = 0; i < childCount; i++)
        {
            spawnPoints[i] = spawnPointsParent.GetChild(i);
        }
    }

    void Start()
    {
        SpawnRandomProps();
    }

    void SpawnRandomProps()
    {
        if (propsPrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogError("No prop prefabs or spawn points assigned!");
            return;
        }

        // Clear and reuse the list instead of creating a new one
        shuffledSpawns.Clear();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            shuffledSpawns.Add(spawnPoints[i]);
        }
        
        Shuffle(shuffledSpawns);

        int spawnCount = Mathf.Min(totalPropsToSpawn, shuffledSpawns.Count);
        spawnedProps.Clear();

        int spawned = 0;
        int attempts = 0;

        while (spawned < spawnCount && attempts < shuffledSpawns.Count)
        {
            Transform spawnPoint = shuffledSpawns[attempts];
            attempts++;

            if (preventOverlap && IsTooClose(spawnPoint.position)) continue;

            GameObject prefab = propsPrefabs[Random.Range(0, propsPrefabs.Length)];
            GameObject prop = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

            // 🔧 Important line for child-based detection
            prop.transform.SetParent(spawnPoint); // Makes prop a child of its spawn point

            // Optional: Keep world position if your prefab has offsets
            // prop.transform.SetParent(spawnPoint, worldPositionStays: true);

            var identity = prop.GetComponent<PropIdentity>() ?? prop.AddComponent<PropIdentity>();
            identity.isFake = false;

            spawnedProps.Add(prop);
            spawned++;
        }

        // Assign fake props
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
