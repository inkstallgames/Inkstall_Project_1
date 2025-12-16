// using UnityEngine;
// using UnityEngine.Purchasing;
// using UnityEngine.Purchasing.Security;

// public class IAPRemoveAdsManager : MonoBehaviour, IStoreListener
// {
//     private static IStoreController storeController;
//     private static IExtensionProvider storeExtensionProvider;

//     public static string REMOVE_ADS = "remove_ads";

//     void Awake()
//     {
//         DontDestroyOnLoad(gameObject);
//     }

//     void Start()
//     {
//         if (storeController == null)
//         {
//             InitializePurchasing();
//         }
//     }

//     void InitializePurchasing()
//     {
//         var builder = ConfigurationBuilder.Instance(
//             StandardPurchasingModule.Instance());

//         builder.AddProduct(REMOVE_ADS, ProductType.NonConsumable);

//         UnityPurchasing.Initialize(this, builder);
//     }

//     // ===================== BUY =====================
//     public void BuyRemoveAds()
//     {
//         if (storeController == null) return;

//         Product product = storeController.products.WithID(REMOVE_ADS);

//         if (product != null && product.availableToPurchase)
//         {
//             storeController.InitiatePurchase(product);
//         }
//     }

//     // ===================== INIT =====================
//     public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
//     {
//         storeController = controller;
//         storeExtensionProvider = extensions;

//         CheckExistingPurchase();
//     }

//     public void OnInitializeFailed(InitializationFailureReason error)
//     {
//         Debug.LogError("IAP Init Failed: " + error);
//     }

//     // ===================== PURCHASE =====================
//     public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
//     {
//         if (args.purchasedProduct.definition.id == REMOVE_ADS)
//         {
//             if (ValidateReceipt(args.purchasedProduct))
//             {
//                 GrantRemoveAds();
//             }
//             else
//             {
//                 Debug.LogError("Receipt validation failed");
//             }
//         }

//         return PurchaseProcessingResult.Complete;
//     }

//     public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
//     {
//         Debug.LogError("Purchase Failed: " + failureReason);
//     }

//     // ===================== RECEIPT VALIDATION =====================
//     bool ValidateReceipt(Product product)
//     {
// #if UNITY_EDITOR
//         return true; // Skip validation in editor
// #endif

//         try
//         {
//             var validator = new CrossPlatformValidator(
//                 GooglePlayTangle.Data(),
//                 AppleTangle.Data(),
//                 Application.identifier
//             );

//             validator.Validate(product.receipt);
//             return true;
//         }
//         catch (IAPSecurityException)
//         {
//             return false;
//         }
//     }

//     // ===================== RESTORE / CHECK =====================
//     void CheckExistingPurchase()
//     {
//         Product product = storeController.products.WithID(REMOVE_ADS);

//         if (product != null && product.hasReceipt)
//         {
//             GrantRemoveAds();
//         }
//     }

//     public void RestorePurchases()
//     {
// #if UNITY_IOS
//         var apple = storeExtensionProvider.GetExtension<IAppleExtensions>();
//         apple.RestoreTransactions(result =>
//         {
//             Debug.Log("Restore Result: " + result);
//         });
// #endif
//     }

//     // ===================== REWARD =====================
//     void GrantRemoveAds()
//     {
//         PlayerPrefs.SetInt("RemoveAds", 1);
//         PlayerPrefs.Save();

//         DisableAds();
//     }

//     void DisableAds()
//     {
//         Debug.Log("Ads Disabled");

//         // Example:
//         // AdsManager.Instance.DisableAds();
//     }

//     // ===================== PUBLIC CHECK =====================
//     public static bool IsAdsRemoved()
//     {
//         return PlayerPrefs.GetInt("RemoveAds", 0) == 1;
//     }
// }
