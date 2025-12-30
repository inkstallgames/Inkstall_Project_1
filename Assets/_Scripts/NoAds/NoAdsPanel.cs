using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoAdsPanel : MonoBehaviour
{
    public GameObject noAdsPanel;
    public GameObject MainCanvas;
    public TMPro.TextMeshProUGUI buyButtonText;
    public UnityEngine.UI.Button buyButton;
    public UnityEngine.UI.Button restoreButton;
    public IAPRemoveAdsManager iapManager; // Assign this in the Inspector
    public bool debugChangeText;

    private void Start()
    {
        UpdateUI();

        // Show restore button only on Apple platforms
        if (restoreButton != null)
        {
            bool isApplePlatform = Application.platform == RuntimePlatform.IPhonePlayer || 
                                   Application.platform == RuntimePlatform.OSXPlayer;
            restoreButton.gameObject.SetActive(isApplePlatform);
        }
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
            }
            if (buyButton != null)
                buyButton.interactable = false;
        }
    }

    public void OnRestorePurchasesClicked()
    {
        if (iapManager != null)
        {
            iapManager.RestorePurchases();
        }
        else
        {
            Debug.LogError("IAPRemoveAdsManager is not assigned in the Inspector on NoAdsPanel.");
        }
    }

    public void OnclickClosebtn()
    {
        noAdsPanel.SetActive(false);
        MainCanvas.SetActive(true);
    }
}
