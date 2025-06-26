using System.Collections.Generic;
using UnityEngine;

// Add this component to prop prefabs to define their fixed spawn locations
public class PropFixedSpawnLocations : MonoBehaviour
{
    [Tooltip("Direct references to valid spawn points for this prop")]
    public Transform[] validSpawnPoints;
    
    [Tooltip("Maximum number of this prop type that can spawn")]
    [Range(1, 20)]
    public int maxSpawnCount = 3;
    
    [Tooltip("Higher weight = more likely to be selected")]
    [Range(1, 100)]
    public int spawnWeight = 50;
    
    // Returns a random valid spawn point that isn't already occupied
    public Transform GetRandomSpawnPoint()
    {
        if (validSpawnPoints == null || validSpawnPoints.Length == 0)
            return null;
            
        // Create a list of unoccupied spawn points
        List<Transform> availablePoints = new List<Transform>();
        foreach (var point in validSpawnPoints)
        {
            if (point != null && point.childCount == 0)
            {
                availablePoints.Add(point);
            }
        }
        
        if (availablePoints.Count == 0)
            return null;
            
        // Return a random available spawn point
        return availablePoints[Random.Range(0, availablePoints.Count)];
    }
}
