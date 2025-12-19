using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoAds : MonoBehaviour
{
    public GameObject noAdsPanel;
    public TMPro.TextMeshProUGUI buyButtonText;
    public UnityEngine.UI.Button buyButton;
    public bool debugChangeText;
    //public float ownedFontSize = 39.6f;

    private void Start()
    {
        UpdateUI();
    }

    private void OnEnable()
    {
        IAPRemoveAdsManager.OnPurchaseSuccess += UpdateUI;
    }

    private void OnDisable()
    {
        IAPRemoveAdsManager.OnPurchaseSuccess -= UpdateUI;
    }

    private void Update()
    {
        if (debugChangeText)
        {
            debugChangeText = false;
            if (buyButtonText != null)
            {
                buyButtonText.text = "Owned";
                //buyButtonText.fontSize = ownedFontSize;
            }
            if (buyButton != null)
                buyButton.interactable = false;
        }
    }

    void UpdateUI()
    {
        if (IAPRemoveAdsManager.IsAdsRemoved())
        {
            if (buyButtonText != null)
            {
                buyButtonText.text = "Owned";
                //buyButtonText.fontSize = ownedFontSize;
            }
            if (buyButton != null)
                buyButton.interactable = false;
        }
    }

    public void OnclickNoAdsBtn()
    {
        noAdsPanel.SetActive(true);
    }

    public void OnclickClosebtn()
    {
        noAdsPanel.SetActive(false);
    }
}
