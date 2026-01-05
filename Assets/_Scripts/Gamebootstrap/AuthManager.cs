using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Authentication;

#if UNITY_ANDROID
using GooglePlayGames;
#endif

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance;
    bool playGamesActivated = false;

    void Awake()
    {
        Debug.Log($"[AuthManager] Awake() called on GameObject: {gameObject.name}");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[AuthManager] Instance set successfully on GameObject: {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[AuthManager] Duplicate found on GameObject: {gameObject.name}. Instance already exists on: {Instance.gameObject.name}. Destroying this component only.");
            Destroy(this);
        }
    }

    async void Start()
    {
        Debug.Log("[AuthManager] Waiting for Unity Services to initialize...");
        while (!UnityServicesInitializer.IsInitialized)
            await Task.Yield();

        Debug.Log("[AuthManager] Unity Services initialized. Starting sign-in...");
        try
        {
            await SignInAnonymously();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("⚠ Anonymous sign-in failed: " + e.Message);
        }

        Debug.Log("[AuthManager] Waiting for CloudSaveManager instance...");
        // Wait for CloudSaveManager to initialize
        float timeout = 5f;
        float elapsed = 0f;
        while (CloudSaveManager.Instance == null && elapsed < timeout)
        {
            await Task.Yield();
            elapsed += Time.deltaTime;
        }

        if (CloudSaveManager.Instance != null)
        {
            Debug.Log("[AuthManager] Calling LoadAllPlayerDataFromCloud...");
            await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
            Debug.Log("[AuthManager] LoadAllPlayerDataFromCloud completed.");
        }
        else
        {
            Debug.LogError("[AuthManager] CloudSaveManager GameObject is not in the scene! Please add CloudSaveManager to your scene.");
            Debug.LogWarning("[AuthManager] Setting default values directly as fallback...");
            
            // Fallback: Set defaults directly if CloudSaveManager is missing
            if (!PlayerPrefs.HasKey("KeysCount"))
            {
                PlayerPrefs.SetInt("CurrentCoins", 0);
                PlayerPrefs.SetInt("KeysCount", 5);
                PlayerPrefs.SetString("StudentDoorData", "");
                PlayerPrefs.Save();
                Debug.Log("[AuthManager] Default values set: Keys=5, Coins=0");
            }
            
            // Manually trigger KeyManager to load since we can't invoke the event
            if (KeyManager.Instance != null)
            {
                KeyManager.Instance.FetchKeysFromPlayerPrefs();
            }
            
            // Manually trigger ProgressManager to load
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.FetchDoorDataFromPlayerPrefs();
            }
        }
    }

    async Task SignInAnonymously()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("✅ Anonymous Login Success");
        }
    }

#if UNITY_ANDROID
    public async void SignInWithGoogle()
    {
        Debug.Log("[AuthManager] SignInWithGoogle() called by user.");
        
        if (IsGoogleAlreadyLinked())
        {
            Debug.Log("ℹ Google already linked");
            return;
        }

        if (!playGamesActivated)
        {
            // // Configure Google Play Games
            // PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
            //     .RequestServerAuthCode(false)
            //     .Build();
            
            // PlayGamesPlatform.InitializeInstance(config);
            PlayGamesPlatform.Activate();
            playGamesActivated = true;
            Debug.Log("[AuthManager] Google Play Games platform activated.");
        }

        Debug.Log("[AuthManager] Starting Google Play Games authentication...");
        
        // Use TaskCompletionSource to make the callback awaitable
        var authTcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        
        Social.localUser.Authenticate(success =>
        {
            authTcs.SetResult(success);
        });

        bool authenticated = await authTcs.Task;
        
        if (!authenticated)
        {
            Debug.LogError("❌ Google Play Games login failed");
            return;
        }

        Debug.Log("✅ Google Play Games login success");

        // Get server auth code
        var authCodeTcs = new System.Threading.Tasks.TaskCompletionSource<string>();
        
        PlayGamesPlatform.Instance.RequestServerSideAccess(false, authCode =>
        {
            authCodeTcs.SetResult(authCode);
        });
        
        string serverAuthCode = await authCodeTcs.Task;
        
        if (string.IsNullOrEmpty(serverAuthCode))
        {
            Debug.LogError("❌ Failed to retrieve server auth code");
            return;
        }

        try
        {
            // Try to sign in with Google Play (this checks if account has existing data)
            Debug.Log("[AuthManager] Attempting to sign in with Google Play account...");
            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(serverAuthCode);
            
            Debug.Log("✅ Signed in with Google Play - Checking for existing cloud data...");
            
            // Load cloud data - if it exists, it will be loaded; if not, current progress will be uploaded
            if (CloudSaveManager.Instance != null)
            {
                await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
                Debug.Log("[AuthManager] Cloud data loaded from Google Play account.");
            }
        }
        catch (AuthenticationException e)
        {
            // If sign-in fails, the account might not exist, so link it to current anonymous account
            Debug.LogWarning($"⚠ Google Play sign-in failed: {e.Message}. Linking to current account instead...");
            
            try
            {
                await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(serverAuthCode);
                Debug.Log("✅ Google account linked with current progress");
                
                // Upload current progress to cloud
                if (CloudSaveManager.Instance != null)
                {
                    await CloudSaveManager.Instance.SaveAllPlayerDataToCloud();
                    Debug.Log("[AuthManager] Current progress uploaded to Google Play account.");
                }
            }
            catch (AuthenticationException linkError)
            {
                Debug.LogError($"❌ Failed to link Google account: {linkError.Message}");
            }
        }
    }

    bool IsGoogleAlreadyLinked()
    {
        var info = AuthenticationService.Instance.PlayerInfo;
        if (info == null || info.Identities == null) return false;

        foreach (var identity in info.Identities)
        {
            if (identity.TypeId == "google")
                return true;
        }
        return false;
    }
#endif

#if UNITY_IOS
    public async void SignInWithApple(string identityToken)
    {
        Debug.Log("[AuthManager] SignInWithApple() called by user.");
        
        if (IsAppleAlreadyLinked())
        {
            Debug.Log("ℹ Apple already linked");
            return;
        }

        if (string.IsNullOrEmpty(identityToken))
        {
            Debug.LogError("❌ Apple identity token is empty");
            return;
        }

        try
        {
            // Try to sign in with Apple (this checks if account has existing data)
            Debug.Log("[AuthManager] Attempting to sign in with Apple account...");
            await AuthenticationService.Instance.SignInWithAppleAsync(identityToken);
            
            Debug.Log("✅ Signed in with Apple - Checking for existing cloud data...");
            
            // Load cloud data - if it exists, it will be loaded; if not, current progress will be uploaded
            if (CloudSaveManager.Instance != null)
            {
                await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
                Debug.Log("[AuthManager] Cloud data loaded from Apple account.");
            }
        }
        catch (AuthenticationException e)
        {
            // If sign-in fails, the account might not exist, so link it to current anonymous account
            Debug.LogWarning($"⚠ Apple sign-in failed: {e.Message}. Linking to current account instead...");
            
            try
            {
                await AuthenticationService.Instance.LinkWithAppleAsync(identityToken);
                Debug.Log("✅ Apple account linked with current progress");
                
                // Upload current progress to cloud
                if (CloudSaveManager.Instance != null)
                {
                    await CloudSaveManager.Instance.SaveAllPlayerDataToCloud();
                    Debug.Log("[AuthManager] Current progress uploaded to Apple account.");
                }
            }
            catch (AuthenticationException linkError)
            {
                Debug.LogError($"❌ Failed to link Apple account: {linkError.Message}");
            }
        }
    }

    bool IsAppleAlreadyLinked()
    {
        var info = AuthenticationService.Instance.PlayerInfo;
        if (info == null || info.Identities == null) return false;

        foreach (var identity in info.Identities)
        {
            if (identity.TypeId == "apple.com")
                return true;
        }
        return false;
    }
#endif
}
