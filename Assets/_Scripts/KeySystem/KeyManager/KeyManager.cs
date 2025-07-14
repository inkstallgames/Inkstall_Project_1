using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int startingKeys = 0;
    private int currentKeys;


    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize keys
        currentKeys = startingKeys;
    }

    // Get the current number of keys
    public int GetKeyCount()
    {
        return currentKeys;
    }
    
    
    // Use a key (called by LockedDoor when unlocking)
    public bool UseKey()
    {
        if (currentKeys > 0)
        {
            currentKeys--;
            UpdateDBKeyCount();
            return true;
        }
        return false;
    }

    public void UpdateDBKeyCount()
    {
        
    }
   
}
