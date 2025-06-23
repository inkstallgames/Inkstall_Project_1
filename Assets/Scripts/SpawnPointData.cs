using UnityEngine;

public class SpawnPointData : MonoBehaviour
{
    public LockableObject associatedLock;
    [HideInInspector] public bool isUsed = false;
}
