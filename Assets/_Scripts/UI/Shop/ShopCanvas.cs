using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopCanvas : MonoBehaviour
{
    public GameObject shopCanvas;
    public GameObject mobileControlsCanvas;

    [Header("Coins")]
    public TextMeshProUGUI coinsCount;
    
    [Header("Bullet UI")]
    public GameObject bullet1Image;
    public GameObject bullet2Image;
    public GameObject bullet3Image;
    public Button plusButton;
    public Button minusButton;
    public TextMeshProUGUI bulletCountText;
    public TextMeshProUGUI totalCostText;
    public int bulletCount;

    public bool canBuyBullets = false;
    
    private int currentBulletCount = 1;
    private const int MAX_BULLETS = 3;
    private const int MIN_BULLETS = 1;

    private const int COST_PER_BULLET = 250;

    void Start()
    {
        // Set initial state
        currentBulletCount = 1;
        UpdateBulletUI();
        
        // Add listeners to buttons
        if (plusButton != null)
        {
            plusButton.onClick.AddListener(IncreaseBulletCount);
        }
        
        if (minusButton != null)
        {
            minusButton.onClick.AddListener(DecreaseBulletCount);
        }
    }

    void Update()
    {
        if (coinsCount.text.ToInt() >= (bulletCount * COST_PER_BULLET))
        {
            canBuyBullets = true;
        }
        else if (coinsCount.text.ToInt() < (bulletCount * COST_PER_BULLET))
        {
            canBuyBullets = false;
        }
    }





    public void EnableShopCanvas()
    {
        // Reset to default state when opening shop
        currentBulletCount = 1;
        UpdateBulletUI();
        
        // Show shop UI
        shopCanvas.SetActive(true);
        Time.timeScale = 0f; // Pause the game
        mobileControlsCanvas.SetActive(false); // Hide the mobile controls UI
    }

    public void CloseShopCanvas()
    {
        shopCanvas.SetActive(false); // Hide the shop UI
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
            int totalCost = currentBulletCount * COST_PER_BULLET;
            totalCostText.text = $"{totalCost} coins";
        }
    }
    
    public void BuyBullets()
    {
        // Here you can add the logic to actually purchase the bullets
        // For example, check if player has enough coins, then add bullets to their inventory
        
        // After purchase, you might want to close the shop or show a success message
        Debug.Log($"Purchased {currentBulletCount} bullets for {currentBulletCount * COST_PER_BULLET} coins");
        
        // Close the shop after purchase
        CloseShopCanvas();
    }
}
