using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoAdsPanel : MonoBehaviour
{
    public GameObject noAdsPanel;
    public GameObject MainCanvas;
    public TMPro.TextMeshProUGUI buyButtonText;
    public UnityEngine.UI.Button buyButton;
    public bool debugChangeText;

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

    public void OnclickClosebtn()
    {
        noAdsPanel.SetActive(false);
        MainCanvas.SetActive(true);
    }
}
