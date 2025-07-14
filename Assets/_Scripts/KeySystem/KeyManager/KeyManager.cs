using UnityEngine;
using System.Collections;
using TMPro;

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


    // Add keys to the player's inventory
    public void AddKeys(int amount)
    {
        currentKeys += amount;
        UpdateKeyCountUI();
        ShowTemporaryMessage($"Gained {amount} key" + (amount > 1 ? "s" : "") + "!");
    }

    // Get the current number of keys
    public int GetKeyCount()
    {
        return currentKeys;
    }
}
