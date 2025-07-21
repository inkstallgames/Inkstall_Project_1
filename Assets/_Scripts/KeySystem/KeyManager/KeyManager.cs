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

    private void FetchDBKeyCount()
    {
        
    }

    public void UseKey()
    {
        UpdateUIKeyCount();
        UpdateDBKeyCount();
    }

    private void UpdateUIKeyCount()
    {
        
    }

    private void UpdateDBKeyCount()
    {
        
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
