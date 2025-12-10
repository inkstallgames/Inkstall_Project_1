using UnityEngine;
using GoogleMobileAds;
using GoogleMobileAds.Api;
using System;

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

    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;

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
        // Clean up the old ad before loading a new one.
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        var adRequest = new AdRequest();
        
        // Create new interstitial ad
        InterstitialAd.Load(_interstitialId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            // If error is not null, the load request failed.
            if (error != null || ad == null)
            {
                Debug.LogError("Interstitial ad failed to load: " + error);
                return;
            }

            interstitialAd = ad;
            Debug.Log("Interstitial ad loaded successfully!");
        });
    }

    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            interstitialAd.Show();
        }
        else
        {
            Debug.Log("Interstitial ad is not ready yet.");
            RequestInterstitial();
        }
    }

    // -------------------- Rewarded Ad--------------------
    public void RequestRewarded()
    {
        // Clean up the old ad before loading a new one.
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        var adRequest = new AdRequest();
        
        // Create new rewarded ad
        RewardedAd.Load(_rewardedId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            // If error is not null, the load request failed.
            if (error != null || ad == null)
            {
                Debug.LogError("Rewarded ad failed to load: " + error);
                return;
            }

            rewardedAd = ad;
            Debug.Log("Rewarded ad loaded successfully!");
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
            // Called when the user should be rewarded for interacting with the ad.
            rewardedAd.Show((Reward reward) =>
            {
                rewardCallback?.Invoke(reward);
            });
        }
        else
        {
            Debug.Log("Rewarded ad is not ready yet.");
            RequestRewarded();
        }
    }
}
