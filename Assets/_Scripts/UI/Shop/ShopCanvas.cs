using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopCanvas : MonoBehaviour
{
    public GameObject buybombsPanel;
    public GameObject mobileControlsCanvas;

    [Header("UI References")]
    public TextMeshProUGUI availableCoinsText; // "Available Coins: 700"
    public TextMeshProUGUI bombMultiplierText; // "x2"
    public TextMeshProUGUI coinsCostText;      // "400 Coins"
    public TextMeshProUGUI buyButtonText;      // "BUY 2 BOMBS"
    
    [Header("Buttons")]
    public Button plusButton;
    public Button minusButton;
    public Button buyButton;
    public Button closeButton; // Added close button reference

    [Header("Shop Settings")]
    private int currentBombsCount = 1;
    private const int MIN_BOMBS = 1;
    private const int MAX_BOMBS_PER_PURCHASE = 3; // Arbitrary limit for UI, or based on max inventory
    private const int BOMB_PRICE = 200;

    private int playerCoins;

    void Start()
    {
        // Initialize UI
        UpdateShopUI();
        
        // Add listeners
        if (plusButton != null) plusButton.onClick.AddListener(IncreaseBombsCount);
        if (minusButton != null) minusButton.onClick.AddListener(DecreaseBombsCount);
        if (buyButton != null) buyButton.onClick.AddListener(BuyBombs);
        if (closeButton != null) closeButton.onClick.AddListener(CloseShopCanvas);
    }

    void OnEnable()
    {
        // Subscribe to coin updates
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.OnCoinsUpdated += UpdatePlayerCoinsUI;
            CoinsManager.Instance.FetchCoins();
        }
        
        // Reset selection when opening shop
        currentBombsCount = 1;
        UpdateShopUI();
    }

    void OnDisable()
    {
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.OnCoinsUpdated -= UpdatePlayerCoinsUI;
        }
    }

    public void EnableShopCanvas()
    {
        buybombsPanel.SetActive(true);
        mobileControlsCanvas.SetActive(false);
        Time.timeScale = 0f;
        
        // Refresh data
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.FetchCoins();
        }
        UpdateShopUI();
    }

    public void CloseShopCanvas()
    {
        buybombsPanel.SetActive(false);
        mobileControlsCanvas.SetActive(true);
        Time.timeScale = 1f;
    }

    public void IncreaseBombsCount()
    {
        if (currentBombsCount < MAX_BOMBS_PER_PURCHASE)
        {
            currentBombsCount++;
            UpdateShopUI();
        }
    }

    public void DecreaseBombsCount()
    {
        if (currentBombsCount > MIN_BOMBS)
        {
            currentBombsCount--;
            UpdateShopUI();
        }
    }

    private void UpdateShopUI()
    {
        // Update Multiplier Text
        if (bombMultiplierText != null)
        {
            bombMultiplierText.text = $"x{currentBombsCount}";
        }

        // Update Cost Text
        int totalCost = currentBombsCount * BOMB_PRICE;
        if (coinsCostText != null)
        {
            coinsCostText.text = $"{totalCost} Coins";
        }

        // Update Buy Button Text
        if (buyButtonText != null)
        {
            buyButtonText.text = $"BUY {currentBombsCount} BOMBS";
        }

        // Update Buy Button Interactability
        if (buyButton != null)
        {
            if (CoinsManager.Instance != null)
            {
                buyButton.interactable = CoinsManager.Instance.currentCoins >= totalCost;
            }
        }
        
        UpdatePlayerCoinsUI();
    }

    private void UpdatePlayerCoinsUI()
    {
        if (CoinsManager.Instance != null)
        {
            playerCoins = CoinsManager.Instance.currentCoins;
        }

        if (availableCoinsText != null)
        {
            availableCoinsText.text = $"Available Coins: {playerCoins}";
        }
    }

    public void BuyBombs()
    {
        int totalCost = currentBombsCount * BOMB_PRICE;

        if (CoinsManager.Instance != null && CoinsManager.Instance.currentCoins >= totalCost)
        {
            CoinsManager.Instance.SpendCoins(totalCost, $"Purchased {currentBombsCount} chemical bombs", (success) => {
                if (success)
                {
                    // Add bombs to inventory
                    if (ChemicalBombManager.Instance != null)
                    {
                        ChemicalBombManager.Instance.AddBombs(currentBombsCount);
                    }
                    
                    // Reset selection or close shop? 
                    // Usually keeping the shop open is better UX, just update UI
                    currentBombsCount = 1;
                    UpdateShopUI();
                    
                    Debug.Log($"Successfully purchased {currentBombsCount} bombs.");
                }
            });
        }
        else
        {
            Debug.Log("Not enough coins!");
        }
    }
}
