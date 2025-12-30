using UnityEngine;
using UnityEngine.Purchasing;

public class IAPRemoveAdsManager : MonoBehaviour, IStoreListener
{

    private static IStoreController storeController;
    private static IExtensionProvider storeExtensionProvider;
    private static bool isInitialized = false;

    public static string REMOVE_ADS = "remove_ads";
    
    private AdManager adManager;



    void Start()
    {
        adManager = AdManager.Instance;
        
        if (!isInitialized)
        {
            InitializePurchasing();
            isInitialized = true;
        }
    }

    // ===================== INIT =====================
    void InitializePurchasing()
    {
        var builder = ConfigurationBuilder.Instance(
            StandardPurchasingModule.Instance());

        builder.AddProduct(REMOVE_ADS, ProductType.NonConsumable);

        UnityPurchasing.Initialize(this, builder);
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;
        storeExtensionProvider = extensions;

        CheckExistingPurchase();
    }

    // OLD Unity IAP versions
    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError("IAP Initialization Failed: " + error);
    }

    // NEW Unity IAP versions
    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"IAP Initialization Failed: {error}, {message}");
    }

    // ===================== BUY =====================
    public void BuyRemoveAds()
    {
        if (storeController == null)
        {
            Debug.LogWarning("IAP not initialized yet");
            return;
        }

        Product product = storeController.products.WithID(REMOVE_ADS);

        if (product != null && product.availableToPurchase)
        {
            storeController.InitiatePurchase(product);
        }
    }

    // ===================== PURCHASE =====================
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        if (args.purchasedProduct.definition.id == REMOVE_ADS)
        {
            GrantRemoveAds();
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogError("Purchase Failed: " + failureReason);
    }

    // ===================== CHECK EXISTING =====================
    void CheckExistingPurchase()
    {
        Product product = storeController.products.WithID(REMOVE_ADS);

        if (product != null && product.hasReceipt)
        {
            GrantRemoveAds();
        }
    }

    public static event System.Action OnPurchaseSuccess;

    // ===================== REWARD =====================
    void GrantRemoveAds()
    {
        Debug.Log("Remove Ads purchase successful! Disabling ads...");
        
        // Update the AdManager to disable ads
        if (adManager != null)
        {
            adManager.SetAdsRemoved(true);
            Debug.Log("Ads have been successfully disabled.");
        }
        else
        {
            Debug.LogError("AdManager reference is missing! Ads will be re-enabled on next launch.");
            // Fallback: Save to PlayerPrefs as a backup
            PlayerPrefs.SetInt("AdsRemoved", 1);
            PlayerPrefs.Save();
        }
        
        // Notify any listeners that the purchase was successful
        OnPurchaseSuccess?.Invoke();
    }

    // ===================== RESTORE =====================
    public void RestorePurchases()
    {
        if (!isInitialized)
        {
            Debug.LogError("RestorePurchases failed. IAP not initialized.");
            return;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            Debug.Log("Restoring purchases...");
            var apple = storeExtensionProvider.GetExtension<IAppleExtensions>();
            apple.RestoreTransactions((success, error) => {
                if (success)
                {
                    Debug.Log("Transactions restored successfully.");
                }
                else
                {
                    Debug.LogError("Restore failed: " + error);
                }
            });
        }
        else
        {
            Debug.Log("Restore purchases not supported on this platform.");
        }
    }

    void DisableAds()
    {
        Debug.Log("Ads Disabled");
        // Example:
        // AdsManager.Instance.DisableAds();
    }

    // ===================== PUBLIC CHECK =====================
    public static bool IsAdsRemoved()
    {
        return PlayerPrefs.GetInt("RemoveAds", 0) == 1;
    }
}
