using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BulletsManager : MonoBehaviour
{
    public static BulletsManager Instance;

    [Header("Bullet Settings")]
    public int bulletsCount = 0;
    private int bulletsToBuy = 1;
    public int maxBulletsToBuy = 3;
    public int costPerBullet = 10;
    public int maxBullets = 6;
    public int currentBullets = 0;
    
    [Header("UI References")]
    public GameObject bulletContainer;
    public GameObject[] bulletUIElements;
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
    
    void OnEnable()
    {
        currentBullets = maxBullets;
        UpdateBulletUI();
    }
    
    public void UpdateBulletUI()
    {
        // Update bullet UI elements
        if (bulletUIElements != null)
        {
            for (int i = 0; i < bulletUIElements.Length; i++)
            {
                if (bulletUIElements[i] != null)
                {
                    // Enable bullet UI if index is less than current bullet count
                    bulletUIElements[i].SetActive(i < currentBullets);
                }
            }
        }
        
        // Update shop button state
        UpdateShopButtonState();
    }
    
    public void DecreaseBullet()
    {
        if (currentBullets > 0)
        {
            currentBullets--;
            UpdateBulletUI();
        }
    }
    
    public void AddBullets(int amount)
    {
        currentBullets = Mathf.Min(currentBullets + amount, maxBullets);
        UpdateBulletUI();
    }
    
    private void UpdateShopButtonState()
    {
        // Enable shop button if bullet count is 3 or less
        if (shopButton != null && currentBullets <= 3)
        {
            shopButton.gameObject.SetActive(true);
        }
        else
        {
            shopButton.gameObject.SetActive(false);    
        }
    }

    public void BuyBullets()
    {
        if (CoinsManager.Instance.currentCoins >= costPerBullet * bulletsToBuy)
        {
            CoinsManager.Instance.currentCoins -= costPerBullet * bulletsToBuy;
            AddBullets(bulletsToBuy);
        }
    }
}
