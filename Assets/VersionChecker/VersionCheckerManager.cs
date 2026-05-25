using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if FIREBASE_REMOTE_CONFIG
using Firebase;
using Firebase.Extensions;
using Firebase.RemoteConfig;
#endif

public class VersionCheckerManager : MonoBehaviour
{
    public static VersionCheckerManager Instance { get; private set; }

    [Header("References")]
    public VersionUpdatePopup updatePopup;

    [Header("Behaviour")]
    public bool checkOnStart = true;
    [Tooltip("When enabled, version checks also run in the Unity Editor.")]
    public bool runInEditor;

    [Header("Store URLs")]
    public string androidStoreUrl = "https://play.google.com/store/apps/details?id=com.inkstall.xenoattack";
    [Tooltip("Example: https://apps.apple.com/app/id1234567890")]
    public string iosStoreUrl = "";

    private bool _isChecking;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (checkOnStart)
        {
            _ = CheckForUpdateAsync();
        }
    }

    public async Task CheckForUpdateAsync()
    {
        if (_isChecking)
        {
            return;
        }

        if (!ShouldRunVersionCheck())
        {
            return;
        }

        _isChecking = true;

        try
        {
#if FIREBASE_REMOTE_CONFIG
            await CheckForUpdateWithFirebaseAsync();
#else
            Debug.LogWarning("[VersionCheckerManager] Firebase Remote Config is not installed. Import FirebaseRemoteConfig.unitypackage from the Firebase Unity SDK.");
#endif
        }
        finally
        {
            _isChecking = false;
        }
    }

#if FIREBASE_REMOTE_CONFIG
    private async Task CheckForUpdateWithFirebaseAsync()
    {
        DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogError($"[VersionCheckerManager] Firebase dependencies unavailable: {dependencyStatus}");
            return;
        }

        FirebaseRemoteConfig remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        await remoteConfig.SetDefaultsAsync(BuildDefaultConfigValues());

        bool fetchSucceeded = await FetchRemoteConfigAsync(remoteConfig);
        if (!fetchSucceeded)
        {
            return;
        }

        await remoteConfig.ActivateAsync();

        if (!remoteConfig.GetValue(VersionCheckerConfigKeys.CheckEnabled).BooleanValue)
        {
            Debug.Log("[VersionCheckerManager] Version check disabled in Remote Config.");
            return;
        }

        string currentVersion = Application.version;
        string latestVersion = GetRemoteString(remoteConfig, GetLatestVersionKey(), currentVersion);
        string message = GetRemoteString(
            remoteConfig,
            VersionCheckerConfigKeys.UpdateMessage,
            updatePopup != null ? updatePopup.defaultMessage : "A new version is available.");

        if (!AppVersionUtility.IsUpdateAvailable(currentVersion, latestVersion))
        {
            Debug.Log($"[VersionCheckerManager] App is up to date. Current: {currentVersion}, Latest: {latestVersion}");
            return;
        }

        ShowUpdatePopup(message);
    }

    private static Dictionary<string, object> BuildDefaultConfigValues()
    {
        string currentVersion = Application.version;

        return new Dictionary<string, object>
        {
            { VersionCheckerConfigKeys.CheckEnabled, true },
            { VersionCheckerConfigKeys.AndroidLatestVersion, currentVersion },
            { VersionCheckerConfigKeys.IosLatestVersion, currentVersion },
            { VersionCheckerConfigKeys.AndroidMinimumVersion, currentVersion },
            { VersionCheckerConfigKeys.IosMinimumVersion, currentVersion },
            { VersionCheckerConfigKeys.UpdateMessage, "A new version of Xeno Attack is available. Please update to continue." },
            { VersionCheckerConfigKeys.AndroidForceUpdate, false },
            { VersionCheckerConfigKeys.IosForceUpdate, false },
            { VersionCheckerConfigKeys.AndroidStoreUrl, "https://play.google.com/store/apps/details?id=com.inkstall.xenoattack" },
            { VersionCheckerConfigKeys.IosStoreUrl, "" }
        };
    }

    private async Task<bool> FetchRemoteConfigAsync(FirebaseRemoteConfig remoteConfig)
    {
        try
        {
            await remoteConfig.FetchAsync(System.TimeSpan.Zero);
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[VersionCheckerManager] Remote Config fetch failed: {exception.Message}");
            return false;
        }
    }

    private void ShowUpdatePopup(string message)
    {
        if (updatePopup == null)
        {
            Debug.LogWarning("[VersionCheckerManager] Update popup is not assigned.");
            return;
        }

        updatePopup.Show(message, OpenStorePage);
    }

    private static string GetRemoteString(FirebaseRemoteConfig remoteConfig, string key, string fallback)
    {
        ConfigValue value = remoteConfig.GetValue(key);
        string remoteValue = value.StringValue;
        return string.IsNullOrWhiteSpace(remoteValue) ? fallback : remoteValue;
    }
#endif

    public void OpenStorePage()
    {
        string storeUrl = GetStoreUrl();
        if (string.IsNullOrWhiteSpace(storeUrl))
        {
            Debug.LogError("[VersionCheckerManager] Store URL is not configured.");
            return;
        }

        Debug.Log($"[VersionCheckerManager] Opening store URL: {storeUrl}");
        Application.OpenURL(storeUrl);
    }

    private string GetStoreUrl()
    {
#if FIREBASE_REMOTE_CONFIG
        FirebaseRemoteConfig remoteConfig = FirebaseRemoteConfig.DefaultInstance;
        if (remoteConfig != null)
        {
            string remoteStoreUrl = GetRemoteString(remoteConfig, GetStoreUrlKey(), string.Empty);
            if (!string.IsNullOrWhiteSpace(remoteStoreUrl))
            {
                return remoteStoreUrl;
            }
        }
#endif

#if UNITY_ANDROID
        return androidStoreUrl;
#elif UNITY_IOS
        return iosStoreUrl;
#else
        return androidStoreUrl;
#endif
    }

    private bool ShouldRunVersionCheck()
    {
#if UNITY_EDITOR
        return runInEditor;
#elif UNITY_ANDROID || UNITY_IOS
        return true;
#else
        return false;
#endif
    }

#if FIREBASE_REMOTE_CONFIG
    private static string GetLatestVersionKey()
    {
#if UNITY_IOS
        return VersionCheckerConfigKeys.IosLatestVersion;
#else
        return VersionCheckerConfigKeys.AndroidLatestVersion;
#endif
    }

    private static string GetStoreUrlKey()
    {
#if UNITY_IOS
        return VersionCheckerConfigKeys.IosStoreUrl;
#else
        return VersionCheckerConfigKeys.AndroidStoreUrl;
#endif
    }
#endif
}
