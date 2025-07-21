using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int keysCount = 0;

    void Awake()
    {
        Instance = this; // No DontDestroyOnLoad here
    }

    private void Start()
    {
        FetchDBKeyCount();
        UpdateUIKeyCount();
    }

    public int FetchDBKeyCount()
    {
        return keysCount;
    }

    // Use a key (called by LockedDoor when unlocking)
    public bool UseKey()
    {
        if (keysCount > 0)
        {
            keysCount--;
            UpdateDBKeyCount();
            UpdateUIKeyCount();
            return true;
        }
        return false;
    }

    private void UpdateUIKeyCount()
    {
        
    }

    private void UpdateDBKeyCount()
    {
        
    }

}
