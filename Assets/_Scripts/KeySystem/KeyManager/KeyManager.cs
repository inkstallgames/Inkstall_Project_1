using UnityEngine;

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int startingKeys = 0;
    private int currentKeys;

    private void Start()
    {
        FetchDBKeyCount();
        UpdateUIKeyCount();
    }

    private void FetchDBKeyCount()
    {
        
    }

    private void UseKey()
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
