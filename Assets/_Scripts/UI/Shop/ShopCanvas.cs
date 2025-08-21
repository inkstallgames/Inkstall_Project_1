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
            buyButton.interactable = CoinsManager.Instance.currentCoins >= totalCost;
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
        UpdateBombsUI();
        UpdatePlayerUI();
        
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
        if (CoinsManager.Instance != null && CoinsManager.Instance.currentCoins >= totalCost)
        {
            // Deduct coins using CoinsManager
            CoinsManager.Instance.SpendCoins(totalCost, (success) => {
                if (success)
                {
                    // Add bombs to ChemicalBombManager if it exists
                    if (ChemicalBombManager.Instance != null)
                    {
                        ChemicalBombManager.Instance.AddBombs(currentBombsCount);
                        Debug.Log($"Added {currentBombsCount} bombs to ChemicalBombManager");
                        
                        // Update local tracking for UI
                        playerBombs += currentBombsCount;
                        
                        // Save changes to PlayerPrefs as backup
                        SavePlayerData();
                        
                        // Show success message
                        Debug.Log($"Purchased {currentBombsCount} bombs for {totalCost} coins. You now have {playerBombs} bombs and {playerCoins} coins.");
                        
                        // Close the shop after purchase
                        CloseShopCanvas();
                    }
                    else
                    {
                        Debug.LogError("ChemicalBombManager instance not found!");
                    }
                }
                else
                {
                    Debug.LogError("Failed to spend coins!");
                }
            });
        }
        else
        {
            // Show not enough coins message
            Debug.Log("Not enough coins to purchase bombs!");
        }
    }
}
