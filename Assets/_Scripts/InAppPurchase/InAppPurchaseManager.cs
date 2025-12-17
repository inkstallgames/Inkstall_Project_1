using UnityEngine;
using UnityEngine.Purchasing;

public class IAPRemoveAdsManager : MonoBehaviour, IStoreListener
{
    private static IStoreController storeController;
    private static IExtensionProvider storeExtensionProvider;
    private static bool isInitialized = false;

    public static string REMOVE_ADS = "remove_ads";

    void Awake()
    {
        // Prevent duplicate instances
        if (FindObjectsOfType<IAPRemoveAdsManager>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
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

    // ===================== REWARD =====================
    void GrantRemoveAds()
    {
        PlayerPrefs.SetInt("RemoveAds", 1);
        PlayerPrefs.Save();

        DisableAds();
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
