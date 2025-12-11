using UnityEngine;
using GoogleMobileAds;
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance;

    [Header("AdMob App ID")]
    [Tooltip("Find this in your AdMob dashboard")]
    private string androidAppId = "ca-app-pub-8376488234284532~5753664751";
    private string iosAppId = "ca-app-pub-8376488234284532~2318590859";

    [Header("Interstitial Ad IDs")]
    [Tooltip("Find these in your AdMob dashboard")]
    private string androidInterstitialId = "ca-app-pub-8376488234284532/7408918392";
    private string iosInterstitialId = "ca-app-pub-8376488234284532/7302775482";

    [Header("Rewarded Ad IDs")]
    [Tooltip("Find these in your AdMob dashboard")]
    private string androidRewardedId = "ca-app-pub-8376488234284532/2867038155";
    private string iosRewardedId = "ca-app-pub-8376488234284532/9427758836";

    // Runtime properties
    private string _interstitialId;
    private string _rewardedId;
    private int _maxRetryAttempts = 3;
    private int _currentInterstitialRetry = 0;
    private int _currentRewardedRetry = 0;

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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Set platform-specific IDs
#if UNITY_ANDROID
        string appId = androidAppId;
        _interstitialId = androidInterstitialId;
        _rewardedId = androidRewardedId;
#elif UNITY_IPHONE
        string appId = iosAppId;
        _interstitialId = iosInterstitialId;
        _rewardedId = iosRewardedId;
#else
        string appId = "unexpected_platform";
        _interstitialId = "unexpected_platform";
        _rewardedId = "unexpected_platform";
#endif

        // Basic configuration for live ads
        var requestConfiguration = new RequestConfiguration
        {
            TagForChildDirectedTreatment = TagForChildDirectedTreatment.Unspecified,
            TagForUnderAgeOfConsent = TagForUnderAgeOfConsent.Unspecified
        };
        MobileAds.SetRequestConfiguration(requestConfiguration);

        // Initialize the Mobile Ads SDK
        MobileAds.Initialize(initStatus => 
        {
            Debug.Log("Mobile Ads SDK initialized successfully!");
            // Now that the SDK is initialized, request ads
            RequestInterstitial();
            RequestRewarded();
        });
    }

    // -------------------- Interstitial Ad--------------------
    public void RequestInterstitial()
    {
        if (_isInterstitialLoading) return;
        _isInterstitialLoading = true;

        // Clean up the old ad before loading a new one.
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        var adRequest = new AdRequest();
        
        Debug.Log("Loading interstitial ad...");
        InterstitialAd.Load(_interstitialId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            _isInterstitialLoading = false;
            
            // If error is not null, the load request failed.
            if (error != null || ad == null)
            {
                _currentInterstitialRetry++;
                if (_currentInterstitialRetry < _maxRetryAttempts)
                {
                    Debug.LogWarning($"Interstitial ad failed to load (Attempt {_currentInterstitialRetry}/{_maxRetryAttempts}): {error?.GetMessage()}");
                    // Retry after a delay
                    Invoke(nameof(RequestInterstitial), 2f);
                }
                else
                {
                    Debug.LogError($"Failed to load interstitial ad after {_maxRetryAttempts} attempts: {error?.GetMessage()}");
                    _currentInterstitialRetry = 0;
                }
                return;
            }

            _currentInterstitialRetry = 0;
            interstitialAd = ad;
            Debug.Log("Interstitial ad loaded successfully!");
            
            // Register event handlers
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("Interstitial ad closed.");
                RequestInterstitial(); // Pre-load the next ad
            };
            
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogError($"Interstitial ad failed to show: {error.GetMessage()}");
                RequestInterstitial(); // Try to load another ad
            };
        });
    }

    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("Showing interstitial ad...");
            interstitialAd.Show();
            return;
        }
        
        Debug.Log("Interstitial ad is not ready yet. Requesting a new one...");
        RequestInterstitial();
        
        // If we don't have an ad ready, you might want to continue the game
        // or show a message to the user
    }

    // -------------------- Rewarded Ad--------------------
    public void RequestRewarded()
    {
        if (_isRewardedLoading) return;
        _isRewardedLoading = true;

        // Clean up the old ad before loading a new one.
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        var adRequest = new AdRequest();
        
        Debug.Log("Loading rewarded ad...");
        RewardedAd.Load(_rewardedId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            _isRewardedLoading = false;
            
            // If error is not null, the load request failed.
            if (error != null || ad == null)
            {
                _currentRewardedRetry++;
                if (_currentRewardedRetry < _maxRetryAttempts)
                {
                    Debug.LogWarning($"Rewarded ad failed to load (Attempt {_currentRewardedRetry}/{_maxRetryAttempts}): {error?.GetMessage()}");
                    // Retry after a delay
                    Invoke(nameof(RequestRewarded), 2f);
                }
                else
                {
                    Debug.LogError($"Failed to load rewarded ad after {_maxRetryAttempts} attempts: {error?.GetMessage()}");
                    _currentRewardedRetry = 0;
                }
                return;
            }

            _currentRewardedRetry = 0;
            rewardedAd = ad;
            Debug.Log("Rewarded ad loaded successfully!");
            
            // Register event handlers
            ad.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("Rewarded ad closed.");
                RequestRewarded(); // Pre-load the next ad
            };
            
            ad.OnAdFullScreenContentFailed += (AdError adError) =>
            {
                Debug.LogError($"Rewarded ad failed to show: {adError.GetMessage()}");
                RequestRewarded(); // Try to load another ad
            };
        });
    }

    // Call this method from Unity UI button
    public void ShowRewardedAd()
    {
        ShowRewarded((Reward reward) => {
            // This runs when the player earns the reward
            if (KeyManager.Instance != null)
            {
                KeyManager.Instance.AddKeys(1);
            }
        });
    }

    public void ShowRewarded(Action<Reward> rewardCallback = null)
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            Debug.Log("Showing rewarded ad...");
            // Called when the user should be rewarded for interacting with the ad.
            rewardedAd.Show((Reward reward) =>
            {
                Debug.Log("User earned reward: " + reward.Amount + " " + reward.Type);
                rewardCallback?.Invoke(reward);
            });
            return;
        }
        
        Debug.Log("Rewarded ad is not ready yet. Requesting a new one...");
        RequestRewarded();
        
        // If you want to notify the user that the ad isn't ready
        // You could show a UI message here
    }
}
