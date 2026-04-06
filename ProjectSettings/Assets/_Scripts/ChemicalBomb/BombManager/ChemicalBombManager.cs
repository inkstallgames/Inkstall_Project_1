using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChemicalBombManager : MonoBehaviour
{
    public static ChemicalBombManager Instance;

    [Header("Bullet Settings")]
    public int maxBombs = 6;
    public int currentBombs = 0;
    private int bombsPurchasedThisRoom = 0;
    private const int MAX_BOMB_PURCHASES_PER_ROOM = 3;
    
    [Header("UI References")]
    public GameObject bombsContainerUI;  // Reference to the bomb panel that should be disabled
    public GameObject[] bombsUIElements;
    public Button shopButton;
    public TextMeshProUGUI bombsRemainingText; // Add this in the Unity Inspector
    
    // Flag to track if the purchase limit has been reached
    private bool purchaseLimitReached = false;
    
    // Flag to track if we've shown the shop button for the first time
    private bool hasShownShopButton = false;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (shopButton != null)
        {
            shopButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // This space is intentionally left blank to prevent the shop button from being activated every frame.
    }
    
    public void InitializeBombs(int newMaxBombs)
    {
        maxBombs = newMaxBombs;
        currentBombs = maxBombs;
        UpdateBombsUI();
    }

    public void UpdateBombsUI()
    {
        // Update bullet UI elements
        if (bombsUIElements != null)
        {
            for (int i = 0; i < bombsUIElements.Length; i++)
            {
                if (bombsUIElements[i] != null)
                {
                    // Enable bullet UI if index is less than current bullet count
                    bombsUIElements[i].SetActive(i < currentBombs);
                }
            }
        }
        
        // Update shop button state
        UpdateShopButtonState();
    }
    
    public void DecreaseBomb()
    {
        if (currentBombs > 0)
        {
            currentBombs--;
            UpdateBombsUI();
            
            // Check for game over condition after decreasing bomb count
            if (currentBombs <= 0)
            {
                shopButton.gameObject.SetActive(false);
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.GameOver();
                }
            }
        }
    }
    
    public void AddBombs(int amount)
    {
        currentBombs = Mathf.Min(currentBombs + amount, maxBombs);
        bombsPurchasedThisRoom++;
        UpdateBombsUI();
        
        // Check if we've reached the purchase limit for this room
        if (bombsPurchasedThisRoom >= MAX_BOMB_PURCHASES_PER_ROOM)
        {
            DisableShopButton();
            // Disable the bomb panel
            if (bombsContainerUI != null)
            {
                bombsContainerUI.SetActive(false);
            }
            
            if (bombsRemainingText != null)
            {
                bombsRemainingText.text = "Max Purchases Reached";
            }
        }
        else if (bombsRemainingText != null)
        {
            int remaining = MAX_BOMB_PURCHASES_PER_ROOM - bombsPurchasedThisRoom;
            bombsRemainingText.text = $"Bombs Left: {remaining}/{MAX_BOMB_PURCHASES_PER_ROOM}";
        }
    }
    
    public void UpdateShopButtonState()
    {
        if (shopButton == null) return;
        
        // If purchase limit is reached, keep shop button disabled
        if (purchaseLimitReached)
        {
            shopButton.gameObject.SetActive(false);
            return;
        }
        
        // Only show the shop button the first time bomb count reaches 1
        if (currentBombs <= 1 && !hasShownShopButton)
        {
            shopButton.gameObject.SetActive(true);
            hasShownShopButton = true;
        }
        else if (currentBombs > 1)
        {
            shopButton.gameObject.SetActive(false);
        }
    }
    
    // Public method to permanently disable the shop button after reaching purchase limit
    public void DisableShopButton()
    {
        purchaseLimitReached = true;
        if (shopButton != null)
        {
            shopButton.gameObject.SetActive(false);
        }
        Debug.Log("Shop button permanently disabled - purchase limit reached");
    }
    
    // Call this when entering a new room
    public void OnRoomEntered()
    {
        bombsPurchasedThisRoom = 0;
        ResetPurchaseLimit();
        
        // Re-enable the bomb panel when entering a new room
        if (bombsContainerUI != null)
        {
            bombsContainerUI.SetActive(true);
        }
        
        if (bombsRemainingText != null)
        {
            bombsRemainingText.text = $"Bombs Left: {MAX_BOMB_PURCHASES_PER_ROOM}/{MAX_BOMB_PURCHASES_PER_ROOM}";
        }
    }
    
    // Public method to reset the purchase limit (call this when starting a new game)
    public void ResetPurchaseLimit()
    {
        purchaseLimitReached = false;
        hasShownShopButton = false;
        UpdateShopButtonState();
    }
}
