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
        if (currentBombs <= 3)
        {
            shopButton.gameObject.SetActive(true);
        }
        else if(currentBombs > 3 && !shopButton.gameObject.activeSelf)
        {
            shopButton.gameObject.SetActive(false);
        }
        if(currentBombs <= 0)
        {
            GameManager.Instance.GameOver();
        }   
    }
    
    void OnEnable()
    {
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
        }
    }
    
    public void AddBombs(int amount)
    {
        currentBombs = Mathf.Min(currentBombs + amount, maxBombs);
        UpdateBombsUI();
    }
    
    private void UpdateShopButtonState()
    {
        // Enable shop button if bullet count is 3 or less
        if (shopButton != null && currentBombs <= 3)
        {
            shopButton.gameObject.SetActive(true);
        }
        else if (shopButton != null && currentBombs > 3)
        {
            shopButton.gameObject.SetActive(false);    
        }
    }
}
