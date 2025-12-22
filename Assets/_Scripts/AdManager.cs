using UnityEngine;
using GoogleMobileAds;
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance;

    [Header("AdMob App ID")]
    private string androidAppId = "ca-app-pub-8376488234284532~5753664751";
    private string iosAppId = "ca-app-pub-8376488234284532~2318590859";

    [Header("Interstitial Ad IDs")]
    private string androidInterstitialId = "ca-app-pub-8376488234284532/7408918392";
    private string iosInterstitialId = "ca-app-pub-8376488234284532/7302775482";

    [Header("Rewarded Ad IDs")]
    private string androidRewardedId = "ca-app-pub-8376488234284532/2867038155";
    private string iosRewardedId = "ca-app-pub-8376488234284532/9427758836";

    // Runtime properties (UNCHANGED)
    private string _interstitialId;
    private string _rewardedId;

    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;

    private bool _isInterstitialLoading = false;
    private bool _isRewardedLoading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[AdManager] Singleton instance created and set to DontDestroyOnLoad.");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[AdManager] Duplicate AdManager instance found, destroying this one.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Debug.Log($"[AdManager] Starting AdManager on {SystemInfo.deviceModel} ({Application.platform})");
        
#if UNITY_ANDROID
        _interstitialId = androidInterstitialId;
        _rewardedId = androidRewardedId;
        Debug.Log($"[AdManager] Android Mode - App ID: {androidAppId}");
        Debug.Log($"[AdManager] Rewarded Ad ID: {_rewardedId}");
#elif UNITY_IOS
        _interstitialId = iosInterstitialId;
        _rewardedId = iosRewardedId;
        Debug.Log($"[AdManager] iOS Mode - App ID: {iosAppId}");
        Debug.Log($"[AdManager] Rewarded Ad ID: {_rewardedId}");
#else
        Debug.Log("[AdManager] Running on unsupported platform");
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Configure test device ID before initialization for testing
        List<string> testDeviceIds = new List<string> { "69d6891543cce296d6693e79cd17ec9c" };
        RequestConfiguration requestConfiguration = new RequestConfiguration
        {
            TestDeviceIds = testDeviceIds
        };
        MobileAds.SetRequestConfiguration(requestConfiguration);
        Debug.Log("[AdManager] DEVELOPMENT BUILD: Using test device configuration.");
#else
        Debug.Log("[AdManager] RELEASE BUILD: Using live ad units.");
#endif

        Debug.Log("[AdManager] Initializing AdMob...");
        MobileAds.Initialize(initStatus =>
        {
            Debug.Log($"[AdManager] AdMob initialization complete. Status: {initStatus.ToString()}");
            Debug.Log("[AdManager] Loading all ads...");
            LoadAllAds();
        });
    }

    // ================= INTERSTITIAL =================
    public void RequestInterstitial()
    {
        if (_isInterstitialLoading)
        {
            Debug.Log("[Interstitial] Ad is already loading, request skipped.");
            return;
        }
        
        _isInterstitialLoading = true;
        Debug.Log($"[Interstitial] Requesting new ad with ID: {_interstitialId}");

        interstitialAd?.Destroy();
        interstitialAd = null;

        InterstitialAd.Load(_interstitialId, new AdRequest(), (ad, error) =>
        {
            _isInterstitialLoading = false;

            if (error != null)
            {
                Debug.LogError($"[Interstitial] Load failed. Code: {error.GetCode()}, Message: {error.GetMessage()}");
                return;
            }

            if (ad == null)
            {
                Debug.LogError("[Interstitial] Ad object returned was null.");
                return;
            }

            interstitialAd = ad;
            Debug.Log("[Interstitial] Ad loaded successfully.");

            // Register for ad events
            interstitialAd.OnAdFullScreenContentOpened += () => Debug.Log("[Interstitial] Ad content opened.");
            interstitialAd.OnAdFullScreenContentClosed += () => 
            {
                Debug.Log("[Interstitial] Ad content closed. Requesting next ad.");
                RequestInterstitial();
            };
            interstitialAd.OnAdFullScreenContentFailed += (AdError adError) => 
            {
                Debug.LogError($"[Interstitial] Ad failed to show. Error: {adError.GetMessage()}");
                RequestInterstitial();
            };
        });
    }

    public void ShowInterstitialAd()
    {
        Debug.Log("[Interstitial] ===== SHOW INTERSTITIAL AD REQUESTED =====");

        if (interstitialAd == null)
        {
            Debug.LogError("[Interstitial] Ad not ready: instance is null. Requesting a new one.");
            RequestInterstitial();
            return;
        }

        if (!interstitialAd.CanShowAd())
        {
            Debug.LogError("[Interstitial] Ad not ready: CanShowAd() returned false. Requesting a new one.");
            RequestInterstitial();
            return;
        }

        Debug.Log("[Interstitial] Ad is ready. Attempting to show.");
        try
        {
            interstitialAd.Show();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Interstitial] An exception occurred while trying to show the ad: {e.Message}");
        }
    }

    // ================= REWARDED =================
    public void RequestRewarded()
    {
        if (_isRewardedLoading)
        {
            Debug.Log("[Rewarded] Already loading a rewarded ad, skipping new request");
            return;
        }

        _isRewardedLoading = true;
        Debug.Log($"[Rewarded] Starting to load rewarded ad (ID: {_rewardedId})");

        if (rewardedAd != null)
        {
            Debug.Log("[Rewarded] Destroying previous rewarded ad instance");
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        Debug.Log("[Rewarded] Creating new rewarded ad request");
        var adRequest = new AdRequest();
        Debug.Log($"[Rewarded] Sending ad request with: {adRequest}");

        RewardedAd.Load(_rewardedId, adRequest, (ad, error) =>
        {
            _isRewardedLoading = false;
            Debug.Log($"[Rewarded] Ad load completed. Success: {ad != null}, Error: {error?.GetMessage() ?? "None"}");

            if (error != null)
            {
                Debug.LogError("[Rewarded] NO AD / LOAD FAILED");
                Debug.LogError("[Rewarded] Code: " + error.GetCode());
                Debug.LogError("[Rewarded] Message: " + error.GetMessage());
                return;
            }

            if (ad == null)
            {
                Debug.LogError("[Rewarded] NO AD RETURNED (ad == null)");
                return;
            }

            rewardedAd = ad;
            Debug.Log("[Rewarded] LOAD SUCCESS");

            rewardedAd.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("[Rewarded] OPENED");
            };

            rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[Rewarded] CLOSED (no reward if not earned)");
                RequestRewarded();
            };

            rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogError("[Rewarded] SHOW FAILED: " + error.GetMessage());
                RequestRewarded();
            };
        });
    }

    private void LoadAllAds()
    {
        RequestInterstitial();
        RequestRewarded();
    }

    public void ShowRewardedAd()
    {
        Debug.Log("[Rewarded] ===== SHOW REWARDED AD REQUESTED =====");
        Debug.Log($"[Rewarded] Current Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        Debug.Log($"[Rewarded] Is Playing: {Application.isPlaying}");
        
        ShowRewarded(() =>
        {
            Debug.Log("[Rewarded] ===== REWARD CALLBACK TRIGGERED =====");
            if (KeyManager.Instance != null)
            {
                int keysBefore = KeyManager.Instance.GetCurrentKeyCount();
                Debug.Log($"[Rewarded] Before adding key. Current keys: {keysBefore}");
                
                // Add the key
                KeyManager.Instance.AddKeys(1);
                
                int keysAfter = KeyManager.Instance.GetCurrentKeyCount();
                Debug.Log($"[Rewarded] After adding key. Expected: {keysBefore + 1}, Actual: {keysAfter}");
                
                if (keysAfter <= keysBefore)
                {
                    Debug.LogError($"[Rewarded] KEY NOT ADDED PROPERLY! Before: {keysBefore}, After: {keysAfter}");
                }
                else
                {
                    Debug.Log($"[Rewarded] Successfully added key! New total: {keysAfter}");
                }
            }
            else
            {
                Debug.LogError("[Rewarded] CRITICAL: KeyManager.Instance is null! Cannot add reward keys.");
            }
        });
    }

    public void ShowRewarded(Action onReward = null)
    {
        Debug.Log($"[Rewarded] SHOW REQUESTED (Platform: {Application.platform})");

        if (rewardedAd == null)
        {
            Debug.LogError("[Rewarded] NOT READY (NULL)");
            RequestRewarded();
            return;
        }

        if (!rewardedAd.CanShowAd())
        {
            Debug.LogError("[Rewarded] NOT READY (CanShowAd = FALSE)");
            RequestRewarded();
            return;
        }

        Debug.Log("[Rewarded] SHOWING");

        try
        {
            rewardedAd.Show(reward =>
            {
                Debug.Log($"[Rewarded] USER EARNED REWARD - Amount: {reward.Amount}, Type: {reward.Type}");
                // Only invoke the callback when reward is actually earned
                onReward?.Invoke();
                
                // Preload next ad
                RequestRewarded();
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Rewarded] Exception when showing ad: {e.Message}");
            // Don't give reward if there was an error showing the ad
            RequestRewarded();
        }
    }
}
