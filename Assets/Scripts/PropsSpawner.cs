using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [Header("Assign all props prefabs")]
    [SerializeField] private GameObject[] propsPrefabs;

    [Header("Assign Spawn Points Parent")]
    [SerializeField] private Transform spawnPointsParent;

    [Header("Prop Settings")]
    [Min(1)] public int totalPropsToSpawn = 15;
    [Min(1)] public int fakePropCount = 5;
    public float minDistanceBetweenProps = 3f;
    public bool preventOverlap = true;

    private List<GameObject> spawnedProps = new List<GameObject>();
    
    // Pre-allocate lists to avoid GC allocations
    private List<GameObject> availablePrefabs;
    private Dictionary<GameObject, int> prefabSpawnCounts;

    void Awake()
    {
        // Initialize collections to avoid GC allocations
        availablePrefabs = new List<GameObject>(propsPrefabs.Length);
        prefabSpawnCounts = new Dictionary<GameObject, int>();
    }

    void Start()
    {
        SpawnRandomProps();
    }

    void SpawnRandomProps()
    {
        if (propsPrefabs.Length == 0)
        {
            Debug.LogError("No prop prefabs assigned!");
            return;
        }

        spawnedProps.Clear();
        prefabSpawnCounts.Clear();
        
        // Create a working copy of prefabs we can modify
        availablePrefabs.Clear();
        foreach (var prefab in propsPrefabs)
        {
            if (prefab != null && prefab.GetComponent<PropFixedSpawnLocations>() != null)
            {
                availablePrefabs.Add(prefab);
                prefabSpawnCounts[prefab] = 0;
            }
        }
        
        int totalSpawned = 0;
        
        // Keep spawning until we reach our target or run out of options
        while (totalSpawned < totalPropsToSpawn && availablePrefabs.Count > 0)
        {
            // Select a random prefab
            int randomIndex = Random.Range(0, availablePrefabs.Count);
            GameObject selectedPrefab = availablePrefabs[randomIndex];
            
            // Get the prop's spawn config
            PropFixedSpawnLocations config = selectedPrefab.GetComponent<PropFixedSpawnLocations>();
            
            // Let the prop decide where it should spawn
            Transform spawnPoint = config.GetRandomSpawnPoint();
            
            // If no valid spawn point found, remove this prefab and continue
            if (spawnPoint == null)
            {
                availablePrefabs.RemoveAt(randomIndex);
                continue;
            }
            
            // Check if too close to another prop
            if (preventOverlap && IsTooClose(spawnPoint.position))
            {
                // Try again with the same prefab next iteration
                continue;
            }
            
            // Spawn the prop
            GameObject prop = Instantiate(selectedPrefab, spawnPoint.position, spawnPoint.rotation);
            prop.transform.SetParent(spawnPoint);
            
            var identity = prop.GetComponent<PropIdentity>() ?? prop.AddComponent<PropIdentity>();
            identity.isFake = false;
            
            spawnedProps.Add(prop);
            totalSpawned++;
            
            // Increment the spawn count for this prefab
            prefabSpawnCounts[selectedPrefab]++;
            
            // If we've reached the max count for this prefab, remove it
            if (prefabSpawnCounts[selectedPrefab] >= config.maxSpawnCount)
            {
                availablePrefabs.Remove(selectedPrefab);
            }
        }
        
        // Assign fake props
        AssignFakeProps();
        
        Debug.Log($"Spawned {spawnedProps.Count} props: {fakePropCount} fake, {spawnedProps.Count - fakePropCount} real.");
    }
    
    private void AssignFakeProps()
    {
        // Shuffle the props to randomize which ones are fake
        Shuffle(spawnedProps);
        
        // Limit fake props to the specified count or available props
        int fakesToAssign = Mathf.Min(fakePropCount, spawnedProps.Count);
        
        for (int i = 0; i < fakesToAssign; i++)
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
        if (!Application.isPlaying && propsPrefabs != null)
        {
            // Draw lines from prefabs to their valid spawn points in edit mode
            foreach (var prefab in propsPrefabs)
            {
                if (prefab == null) continue;
                
                PropFixedSpawnLocations config = prefab.GetComponent<PropFixedSpawnLocations>();
                if (config == null || config.validSpawnPoints == null) continue;
                
                // Generate a consistent color for this prefab
                int hash = prefab.name.GetHashCode();
                float r = (hash & 0xFF) / 255f;
                float g = ((hash >> 8) & 0xFF) / 255f;
                float b = ((hash >> 16) & 0xFF) / 255f;
                Gizmos.color = new Color(r, g, b, 0.8f);
                
                // Draw spheres at each valid spawn point
                foreach (var spawnPoint in config.validSpawnPoints)
                {
                    if (spawnPoint == null) continue;
                    
                    Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
                    
                    // Draw prefab name above spawn point
                    UnityEditor.Handles.Label(spawnPoint.position + Vector3.up * 0.7f, prefab.name);
                }
            }
        }
        else if (Application.isPlaying && spawnedProps != null)
        {
            // In play mode, show fake props
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
