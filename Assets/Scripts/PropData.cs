using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class PropData
{
    public string propName;
    public GameObject prefab;
    public List<Transform> spawnPoints; // Specific spawn points for this prop
}
