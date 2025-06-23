using UnityEngine;

public class Container : MonoBehaviour
{
    public Transform propSpawnPoint;
    public LockableObject lockableObject;
    public bool hasPropInside = false;

    public void InsertProp(GameObject prop)
    {
        prop.transform.position = propSpawnPoint.position;
        prop.transform.rotation = propSpawnPoint.rotation;
        prop.transform.SetParent(transform);

        hasPropInside = true;
        lockableObject.isLocked = true;
        lockableObject.containedObject = prop;

        prop.SetActive(true);
    }
}
