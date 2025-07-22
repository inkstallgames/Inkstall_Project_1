using UnityEngine;
using TMPro;

public class BulletShop : MonoBehaviour
{
    public int coinCount = 100;
    public int bulletCount = 0;
    private int bulletsToBuy = 1;
    public int maxBulletsToBuy = 3;
    public int costPerBullet = 10;

    [Header("UI Elements")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI bulletAmountText;
    public TextMeshProUGUI totalCostText;
    public TextMeshProUGUI plusButton;
    public TextMeshProUGUI minusButton;
    public TextMeshProUGUI buyButton;

    void Start()
    {
        UpdateUI();
    }

    public void IncreaseBullet()
    {
        if (bulletsToBuy < maxBulletsToBuy)
        {
            bulletsToBuy++;
            UpdateUI();
        }
    }

    public void DecreaseBullet()
    {
        if (bulletsToBuy > 1)
        {
            bulletsToBuy--;
            UpdateUI();
        }
    }

    public void BuyBullets()
    {
        int totalCost = bulletsToBuy * costPerBullet;

        if (coinCount >= totalCost)
        {
            coinCount -= totalCost;
            bulletCount += bulletsToBuy;
            bulletsToBuy = 1; // reset selection
            UpdateUI();
            Debug.Log("Bullets Bought! Total bullets: " + bulletCount);
        }
        else
        {
            Debug.Log("Not enough coins!");
        }
    }

    void UpdateUI()
    {
        coinText.text = coinCount.ToString();
        bulletAmountText.text = bulletsToBuy.ToString();
        totalCostText.text = (bulletsToBuy * costPerBullet).ToString();

        plusButton.text = "+";
        minusButton.text = "-";
        buyButton.text = "Buy";
    }
}
