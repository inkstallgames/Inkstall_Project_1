using System.Collections.Generic;
using UnityEngine;

// Add this component to prop prefabs to define their fixed spawn locations
public class PropFixedSpawnLocations : MonoBehaviour
{
    [Tooltip("Names of valid spawn points for this prop")]
    public string[] validSpawnPointNames;
    
    [Tooltip("Maximum number of this prop type that can spawn")]
    [Range(1, 20)]
    public int maxSpawnCount = 3;
    
    [Tooltip("Higher weight = more likely to be selected")]
    [Range(1, 100)]
    public int spawnWeight = 50;
    
    // Pre-allocated list to avoid GC allocations
    private static readonly List<Transform> tempAvailablePoints = new List<Transform>(20);
    
    // Returns a random valid spawn point that isn't already occupied
    public Transform GetRandomSpawnPoint(Dictionary<string, Transform> allSpawnPoints)
    {
        if (validSpawnPointNames == null || validSpawnPointNames.Length == 0 || allSpawnPoints == null)
            return null;
            
        // Reuse the pre-allocated list instead of creating a new one
        tempAvailablePoints.Clear();
        
        // Find unoccupied spawn points from our valid list
        foreach (string pointName in validSpawnPointNames)
        {
            if (string.IsNullOrEmpty(pointName)) continue;
            
            if (allSpawnPoints.TryGetValue(pointName, out Transform point))
            {
                if (point != null && point.childCount == 0)
                {
                    tempAvailablePoints.Add(point);
                }
            }
        }
        
        if (tempAvailablePoints.Count == 0)
            return null;
            
        // Return a random available spawn point
        return tempAvailablePoints[Random.Range(0, tempAvailablePoints.Count)];
    }
}
