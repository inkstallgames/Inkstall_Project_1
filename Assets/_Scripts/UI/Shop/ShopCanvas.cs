using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopCanvas : MonoBehaviour
{
    public GameObject bulletsShopCanvas;
    public GameObject mobileControlsCanvas;

    [Header("Coins")]
    public TextMeshProUGUI coinsCount;
    private int playerCoins;
    
    [Header("Bullet UI")]
    public GameObject bombs1Image;
    public GameObject bombs2Image;  
    public GameObject bombs3Image;
    public Button plusButton;
    public Button minusButton;
    public TextMeshProUGUI bombsCountText;
    public TextMeshProUGUI totalCostText;
    public Button buyButton;
    
    [Header("Player Inventory")]
    public int playerBombs = 0;
    public TextMeshProUGUI playerBombsText; // Reference to UI text showing player's bullet count
    
    private int currentBombsCount = 1;
    private const int MAX_BOMBS = 3;
    private const int MIN_BOMBS = 1;
    
    // Track how many bombs have been purchased in this shop session
    private int bombsPurchasedThisSession = 0;
    private const int MAX_BOMBS_PER_SESSION = 3;
    
    // Static variable to track total bombs purchased across shop sessions
    private static int totalBombsPurchased = 0;

    // Tiered pricing for bullets
    private readonly Dictionary<int, int> bombsPrices = new Dictionary<int, int>
    {
        { 1, 200 },  // 1 bomb costs 200 coins
        { 2, 300 },  // 2 bomb cost 300 coins
        { 3, 500 }   // 3 bomb cost 500 coins
    };
    
    // PlayerPrefs keys
    private const string COINS_KEY = "PlayerCoins";
    private const string BOMBS_KEY = "PlayerBombs";

    void Start()
    {
        // Load saved player data
        LoadPlayerData();
        
        // Set initial state
        currentBombsCount = 1;
        UpdateBombsUI();
        UpdatePlayerUI();
        
        // Add listeners to buttons
        if (plusButton != null)
        {
            plusButton.onClick.AddListener(IncreaseBombsCount);
        }
        
        if (minusButton != null)
        {
            minusButton.onClick.AddListener(DecreaseBombsCount);
        }
        
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(BuyBombs);
        }
    }

    void OnEnable()
    {
        // Subscribe to CoinsManager events
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.OnCoinsUpdated += UpdatePlayerUI;
            CoinsManager.Instance.FetchCoins();
        }
        
        // Update UI
        UpdateBombsUI();
        UpdatePlayerUI();
    }

    void OnDisable()
    {
        // Unsubscribe from CoinsManager events
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.OnCoinsUpdated -= UpdatePlayerUI;
        }
    }

    void Update()
    {
        // Update the buy button interactability based on whether player can afford the bullets
        if (buyButton != null && CoinsManager.Instance != null)
        {
            int totalCost = bombsPrices[currentBombsCount];
            buyButton.interactable = CoinsManager.Instance.currentCoins >= totalCost && totalBombsPurchased < MAX_BOMBS_PER_SESSION;
        }
    }

    private void LoadPlayerData()
    {
        // Load saved bombs from PlayerPrefs
        playerBombs = PlayerPrefs.GetInt(BOMBS_KEY, 0);
        
        // Update UI
        UpdatePlayerUI();
    }
    
    private void SavePlayerData()
    {
        // Save current coins and bullets to PlayerPrefs
        PlayerPrefs.SetInt(COINS_KEY, playerCoins);
        PlayerPrefs.SetInt(BOMBS_KEY, playerBombs);
        PlayerPrefs.Save();
    }

    public void EnableShopCanvas()
    {
        // Load the latest player data when opening shop
        LoadPlayerData();
        
        // Reset to default state when opening shop
        currentBombsCount = 1;
        // Don't reset bombsPurchasedThisSession anymore
        UpdateBombsUI();
        UpdatePlayerUI();
        
        // Update buy button state based on total bombs purchased
        if (buyButton != null)
        {
            buyButton.interactable = totalBombsPurchased < MAX_BOMBS_PER_SESSION;
        }
        
        // Show shop UI
        bulletsShopCanvas.SetActive(true);
        Time.timeScale = 0f; // Pause the game
        mobileControlsCanvas.SetActive(false); // Hide the mobile controls UI
    }

    public void CloseShopCanvas()
    {
        bulletsShopCanvas.SetActive(false); // Hide the shop UI
        mobileControlsCanvas.SetActive(true); // Show the mobile controls UI
        Time.timeScale = 1f; // Resume the game
    }

    public void IncreaseBombsCount()
    {
        if (currentBombsCount < MAX_BOMBS)
        {
            currentBombsCount++;
            UpdateBombsUI();
        }
    }
    
    public void DecreaseBombsCount()
    {
        if (currentBombsCount > MIN_BOMBS)
        {
            currentBombsCount--;
            UpdateBombsUI();
        }
    }

    private void UpdateBombsUI()
    {
        // Update bombs images
        if (bombs1Image != null) bombs1Image.SetActive(currentBombsCount >= 1);
        if (bombs2Image != null) bombs2Image.SetActive(currentBombsCount >= 2);
        if (bombs3Image != null) bombs3Image.SetActive(currentBombsCount >= 3);
        
        // Update bombs count text
        if (bombsCountText != null)
        {
            bombsCountText.text = currentBombsCount.ToString();
        }
        
        // Update total cost
        if (totalCostText != null)
        {
            int totalCost = bombsPrices[currentBombsCount];
            totalCostText.text = $"{totalCost} coins";
        }
    }
    
    private void UpdatePlayerUI()
    {
        // Get current coins from CoinsManager if available
        if (CoinsManager.Instance != null)
        {
            playerCoins = CoinsManager.Instance.currentCoins;
        }
        
        // Update coins display
        if (coinsCount != null)
        {
            coinsCount.text = playerCoins.ToString();
        }
        
        // Update bombs display if available
        if (playerBombsText != null)
        {
            playerBombsText.text = playerBombs.ToString();
        }
    }
    
    public void BuyBombs()
    {
        int totalCost = bombsPrices[currentBombsCount];
        
        // Check if player has enough coins and if CoinsManager exists
        if (CoinsManager.Instance != null && CoinsManager.Instance.currentCoins >= totalCost && totalBombsPurchased < MAX_BOMBS_PER_SESSION)
        {
            // Deduct coins using CoinsManager
            CoinsManager.Instance.SpendCoins(totalCost, $"Purchased {currentBombsCount} chemical bombs", (success) => {
                if (success)
                {
                    // Add bombs to ChemicalBombManager if it exists
                    if (ChemicalBombManager.Instance != null)
                    {
                        ChemicalBombManager.Instance.AddBombs(currentBombsCount);
                        
                        // Update local tracking for UI
                        playerBombs += currentBombsCount;
                        bombsPurchasedThisSession += currentBombsCount;
                        totalBombsPurchased += currentBombsCount;
                        
                        // Check if we've reached the limit and update shop button in ChemicalBombManager
                        if (totalBombsPurchased >= MAX_BOMBS_PER_SESSION && ChemicalBombManager.Instance.shopButton != null)
                        {
                            ChemicalBombManager.Instance.DisableShopButton();
                        }
                        
                        // Save changes to PlayerPrefs as backup
                        SavePlayerData();
                        
                        // Fetch the latest coin count from the database to ensure UI is up-to-date
                        if (CoinsManager.Instance != null)
                        {
                            CoinsManager.Instance.FetchCoins();
                        }
                        
                        // Update UI with the latest data
                        UpdatePlayerUI();
                        
                        // Close the shop after purchase
                        CloseShopCanvas();
                    }
                    else
                    {
                        // ChemicalBombManager not found, show error
                        ShowErrorMessage("Error: Bomb manager not found!");
                    }
                }
                else
                {
                    Debug.LogError("Failed to spend coins!");
                    // Show error message to the player
                    ShowErrorMessage("Failed to purchase bombs. Please try again.");
                }
            });
        }
        else
        {
            // Show not enough coins message or session limit reached message
            if (CoinsManager.Instance.currentCoins < totalCost)
            {
                Debug.Log("Not enough coins to purchase bombs!");
                ShowErrorMessage("Not enough coins to purchase bombs!");
            }
            else if (totalBombsPurchased >= MAX_BOMBS_PER_SESSION)
            {
                Debug.Log("You have reached the purchase limit of 3 bombs!");
                ShowErrorMessage("You have reached the purchase limit of 3 bombs!");
            }
        }
    }
    
    // Method to show error messages to the player
    private void ShowErrorMessage(string message)
    {
        // You can implement this to show a UI message to the player
        // For now, just log to console
        Debug.LogWarning(message);
    }
    
    // Public method to reset the bomb purchase counter (call this when starting a new game)
    public static void ResetBombPurchaseCounter()
    {
        totalBombsPurchased = 0;
    }
}
