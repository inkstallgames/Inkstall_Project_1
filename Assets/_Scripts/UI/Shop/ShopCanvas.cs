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
    public GameObject bullet1Image;
    public GameObject bullet2Image;  
    public GameObject bullet3Image;
    public Button plusButton;
    public Button minusButton;
    public TextMeshProUGUI bulletCountText;
    public TextMeshProUGUI totalCostText;
    public Button buyButton;
    
    [Header("Player Inventory")]
    public int playerBullets = 0;
    public TextMeshProUGUI playerBulletsText; // Reference to UI text showing player's bullet count
    
    private int currentBulletCount = 1;
    private const int MAX_BULLETS = 3;
    private const int MIN_BULLETS = 1;

    // Tiered pricing for bullets
    private readonly Dictionary<int, int> bulletPrices = new Dictionary<int, int>
    {
        { 1, 200 },  // 1 bullet costs 200 coins
        { 2, 300 },  // 2 bullets cost 300 coins
        { 3, 500 }   // 3 bullets cost 500 coins
    };
    
    // PlayerPrefs keys
    private const string COINS_KEY = "PlayerCoins";
    private const string BULLETS_KEY = "PlayerBullets";

    void Start()
    {
        // Load saved player data
        LoadPlayerData();
        
        // Set initial state
        currentBulletCount = 1;
        UpdateBulletUI();
        UpdatePlayerUI();
        
        // Add listeners to buttons
        if (plusButton != null)
        {
            plusButton.onClick.AddListener(IncreaseBulletCount);
        }
        
        if (minusButton != null)
        {
            minusButton.onClick.AddListener(DecreaseBulletCount);
        }
        
        if (buyButton != null)
        {
            buyButton.onClick.AddListener(BuyBullets);
        }
    }


    void OnEnable()
    {
        CoinsManager.Instance.FetchCoins();
        // UpdateUI();
    }

    void Update()
    {
        // Update the buy button interactability based on whether player can afford the bullets
        if (buyButton != null)
        {
            int totalCost = bulletPrices[currentBulletCount];
            buyButton.interactable = playerCoins >= totalCost;
        }
    }

    private void LoadPlayerData()
    {
        // Load saved coins and bullets from PlayerPrefs
        playerCoins = PlayerPrefs.GetInt(COINS_KEY, 10000); // Default 10000 coins for testing
        playerBullets = PlayerPrefs.GetInt(BULLETS_KEY, 0);
    }
    
    private void SavePlayerData()
    {
        // Save current coins and bullets to PlayerPrefs
        PlayerPrefs.SetInt(COINS_KEY, playerCoins);
        PlayerPrefs.SetInt(BULLETS_KEY, playerBullets);
        PlayerPrefs.Save();
    }

    public void EnableShopCanvas()
    {
        // Load the latest player data when opening shop
        LoadPlayerData();
        
        // Reset to default state when opening shop
        currentBulletCount = 1;
        UpdateBulletUI();
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

    public void IncreaseBulletCount()
    {
        if (currentBulletCount < MAX_BULLETS)
        {
            currentBulletCount++;
            UpdateBulletUI();
        }
    }
    
    public void DecreaseBulletCount()
    {
        if (currentBulletCount > MIN_BULLETS)
        {
            currentBulletCount--;
            UpdateBulletUI();
        }
    }

    private void UpdateBulletUI()
    {
        // Update bullet images
        if (bullet1Image != null) bullet1Image.SetActive(currentBulletCount >= 1);
        if (bullet2Image != null) bullet2Image.SetActive(currentBulletCount >= 2);
        if (bullet3Image != null) bullet3Image.SetActive(currentBulletCount >= 3);
        
        // Update bullet count text
        if (bulletCountText != null)
        {
            bulletCountText.text = currentBulletCount.ToString();
        }
        
        // Update total cost
        if (totalCostText != null)
        {
            int totalCost = bulletPrices[currentBulletCount];
            totalCostText.text = $"{totalCost} coins";
        }
    }
    
    private void UpdatePlayerUI()
    {
        // Update coins display
        if (coinsCount != null)
        {
            coinsCount.text = playerCoins.ToString();
        }
        
        // Update bullets display if available
        if (playerBulletsText != null)
        {
            playerBulletsText.text = playerBullets.ToString();
        }
    }
    
    public void BuyBullets()
    {
        int totalCost = bulletPrices[currentBulletCount];
        
        // Check if player has enough coins
        if (playerCoins >= totalCost)
        {
            // Deduct coins
            playerCoins -= totalCost;
            
            // Add bullets to inventory
            playerBullets += currentBulletCount;
            
            // Save changes
            SavePlayerData();
            
            // Update UI
            UpdatePlayerUI();
            
            // Show success message
            Debug.Log($"Purchased {currentBulletCount} bullets for {totalCost} coins. You now have {playerBullets} bullets and {playerCoins} coins.");
            
            // Close the shop after purchase
            CloseShopCanvas();
        }
        else
        {
            // Show not enough coins message
            Debug.Log("Not enough coins to purchase bullets!");
        }
    }
}
