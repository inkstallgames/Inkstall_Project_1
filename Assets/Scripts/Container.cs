using System.Collections.Generic;
using UnityEngine;

public class Container : MonoBehaviour
{
    [Header("Spawn Points Inside This Container")]
    [Tooltip("Empty Transforms inside the container where props can spawn.")]
    public Transform[] propSpawnPoints;

    [Header("Lockable object to lock if something spawns inside")]
    [Tooltip("Assign the door, lid, or any lockable part that should lock.")]
    public LockableObject lockableObject;

    [HideInInspector] public bool hasPropInside = false;


    /// Returns all defined spawn points inside this container.
    /// Useful if external systems want to know where this container can spawn things.
    public List<Transform> GetSpawnPoints()
    {
        return new List<Transform>(propSpawnPoints);
    }


    /// Inserts a prop into this container, places it at a random spawn point,
    /// hides it, and locks the linked LockableObject.
    /// <param name="prop">The prop to insert and hide inside this container.</param>
    public void InsertProp(GameObject prop)
    {
        if (propSpawnPoints == null || propSpawnPoints.Length == 0)
        {
            Debug.LogWarning($"[{name}] No propSpawnPoints assigned!");
            return;
        }

        // Pick a random spawn point inside the container
        Transform chosenPoint = propSpawnPoints[Random.Range(0, propSpawnPoints.Length)];

        // Move and parent the prop
        prop.transform.position = chosenPoint.position;
        prop.transform.rotation = chosenPoint.rotation;
        prop.transform.SetParent(transform);
        prop.SetActive(false); // Hidden until unlocked

        // Mark container as used
        hasPropInside = true;

        // Lock the door/lid/etc if assigned
        if (lockableObject != null)
        {
            lockableObject.isLocked = true;
            lockableObject.containedObject = prop;
        }

        Debug.Log($"[{name}] Inserted prop '{prop.name}' and locked '{(lockableObject ? lockableObject.gameObject.name : "None")}'.");
    }
}
