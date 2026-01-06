using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChemicalBombManager : MonoBehaviour
{
    public static ChemicalBombManager Instance;

    [Header("Bullet Settings")]
    public int maxBombs = 6;
    public int currentBombs = 0;
    
    [Header("UI References")]
    public GameObject bombsContainerUI;
    public GameObject[] bombsUIElements;
    public Button shopButton;
    
    // Flag to track if the purchase limit has been reached
    private bool purchaseLimitReached = false;
    
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

    private void Update()
    {
        // If purchase limit is reached, keep shop button disabled
        if (purchaseLimitReached)
        {
            shopButton.gameObject.SetActive(false);
            return;
        }
        
        // Original logic for shop button visibility
        if (currentBombs <= 3)
        {
            shopButton.gameObject.SetActive(true);
        }
        else if(currentBombs > 3 && !shopButton.gameObject.activeSelf)
        {
            shopButton.gameObject.SetActive(false);
        }
    }
    
    void Start()
    {
        currentBombs = maxBombs;
        UpdateBombsUI();
    }

    public void SetMaxBombs(int newMaxBombs)
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
        UpdateBombsUI();
    }
    
    private void UpdateShopButtonState()
    {
        // If purchase limit is reached, keep shop button disabled
        if (purchaseLimitReached)
        {
            if (shopButton != null)
            {
                shopButton.gameObject.SetActive(false);
            }
            return;
        }
        
        // Original logic
        if (shopButton != null && currentBombs <= 3)
        {
            shopButton.gameObject.SetActive(true);
        }
        else if (shopButton != null && currentBombs > 3)
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
    
    // Public method to reset the purchase limit (call this when starting a new game)
    public void ResetPurchaseLimit()
    {
        purchaseLimitReached = false;
        UpdateShopButtonState();
    }
}
