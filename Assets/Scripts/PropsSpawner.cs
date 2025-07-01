using UnityEngine;
using System.Collections.Generic;

public class PropsSpawner : MonoBehaviour
{
    [Header("All Prop Prefabs with Their Spawn Points")]
    [SerializeField] private List<PropData> allProps;

    [Header("Fake Version of a Prop (Alien Disguised)")]
    [SerializeField] private GameObject fakePropPrefab;

    [Header("Spawning Settings")]
    [SerializeField] private int totalPropsToSpawn = 10;
    [SerializeField] private int totalFakeProps = 3;

    private List<Transform> usedSpawnPoints = new List<Transform>();

    void Start()
    {
        SpawnProps();
    }

    void SpawnProps()
    {
        if (totalFakeProps > totalPropsToSpawn)
        {
            Debug.LogError("Fake props can't exceed total props to spawn.");
            return;
        }

        // 1. Randomly pick N unique props
        List<PropData> selectedProps = GetRandomProps(allProps, totalPropsToSpawn);

        // 2. Randomly choose K props out of selected as fake
        List<int> fakeIndexes = GetRandomIndexes(totalPropsToSpawn, totalFakeProps);

        for (int i = 0; i < selectedProps.Count; i++)
        {
            PropData prop = selectedProps[i];

            // 3. Choose one random unused spawn point for this prop
            List<Transform> validSpawnPoints = new List<Transform>();
            foreach (var point in prop.spawnPoints)
            {
                if (!usedSpawnPoints.Contains(point))
                    validSpawnPoints.Add(point);
            }

            if (validSpawnPoints.Count == 0)
            {
                Debug.LogWarning($"No available spawn points for prop: {prop.propName}");
                continue;
            }

            Transform chosenPoint = validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
            usedSpawnPoints.Add(chosenPoint);

            // 4. Decide whether this prop is fake or real
            bool isFake = fakeIndexes.Contains(i);

            GameObject prefabToSpawn = isFake ? fakePropPrefab : prop.prefab;

            GameObject spawned = Instantiate(prefabToSpawn, chosenPoint.position, chosenPoint.rotation);

            // 5. Optional Tagging
            spawned.name = isFake ? prop.propName + "_FAKE" : prop.propName + "_REAL";
            spawned.tag = isFake ? "FakeProp" : "RealProp";
        }
    }

    // Get N unique props randomly from the list
    List<PropData> GetRandomProps(List<PropData> sourceList, int count)
    {
        List<PropData> copy = new List<PropData>(sourceList);
        Shuffle(copy);
        return copy.GetRange(0, Mathf.Min(count, copy.Count));
    }

    // Get K unique random indices in [0, total)
    List<int> GetRandomIndexes(int total, int count)
    {
        List<int> indices = new List<int>();
        while (indices.Count < count)
        {
            int r = Random.Range(0, total);
            if (!indices.Contains(r))
                indices.Add(r);
        }
        return indices;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rand];
            list[rand] = temp;
        }
    }
}
