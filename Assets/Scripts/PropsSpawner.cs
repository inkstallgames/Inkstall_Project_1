using System.Collections.Generic;
using UnityEngine;

public class PropsSpawner : MonoBehaviour
{
    [Header("Prop Settings")]
    [SerializeField] private GameObject[] propPrefabs;
    [Min(1)] public int totalPropsToSpawn = 15;
    [Min(1)] public int fakePropCount = 5;
    public float minDistanceBetweenProps = 3f;
    public bool preventOverlap = true;

    [Header("Props Spawn Points")]
    [SerializeField] private Transform spawnPointsParent;

    private Transform[] spawnPoints;
    private List<GameObject> spawnedProps = new List<GameObject>();

    void Awake()
    {
        List<Transform> points = new List<Transform>();
        foreach (Transform child in spawnPointsParent)
            points.Add(child);
        spawnPoints = points.ToArray();
    }

    void Start()
    {
        fakePropCount = Mathf.Clamp(fakePropCount, 0, totalPropsToSpawn);
        SpawnAllProps();
    }

    void SpawnAllProps()
    {
        if (propPrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogError("❌ No prop prefabs or spawn points assigned!");
            return;
        }

        List<Transform> shuffledSpawns = new List<Transform>(spawnPoints);
        Shuffle(shuffledSpawns);

        int spawnCount = Mathf.Min(totalPropsToSpawn, shuffledSpawns.Count);
        spawnedProps.Clear();

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = shuffledSpawns[i].position;
            if (preventOverlap && IsTooClose(spawnPos)) continue;

            GameObject prefab = propPrefabs[Random.Range(0, propPrefabs.Length)];
            GameObject prop = Instantiate(prefab, spawnPos, shuffledSpawns[i].rotation);

            var identity = prop.GetComponent<PropIdentity>() ?? prop.AddComponent<PropIdentity>();
            identity.isFake = false;

            spawnedProps.Add(prop);
        }

        Shuffle(spawnedProps);

        for (int i = 0; i < fakePropCount && i < spawnedProps.Count; i++)
        {
            GameObject fakeProp = spawnedProps[i];
            var identity = fakeProp.GetComponent<PropIdentity>();
            identity.isFake = true;

            if (fakeProp.GetComponent<CollectibleProp>() == null)
                fakeProp.AddComponent<CollectibleProp>();

            fakeProp.name += " (FAKE)"; // Dev-friendly name
            Debug.Log($"🟡 Marked as FAKE: {fakeProp.name}");

            GameManager.Instance?.RegisterCollectible();
        }

        Debug.Log($"✅ Spawned {spawnedProps.Count} props: {fakePropCount} fake, {spawnedProps.Count - fakePropCount} real.");
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
                // ✅ Skip if destroyed
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
