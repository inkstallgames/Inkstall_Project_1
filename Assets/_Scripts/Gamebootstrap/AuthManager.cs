using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Authentication;

#if UNITY_ANDROID
using GooglePlayGames;
using UnityEngine.SocialPlatforms;
#endif

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance;
    private bool playGamesActivated = false;

    void Awake()
    {
        Debug.Log($"[AuthManager] Awake() called on GameObject: {gameObject.name}");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[AuthManager] Instance set successfully.");
        }
        else
        {
            Debug.LogWarning("[AuthManager] Duplicate instance detected. Destroying this one.");
            Destroy(this);
        }
    }

    async void Start()
    {
        Debug.Log("[AuthManager] Start() called. Waiting for Unity Services...");

        while (!UnityServicesInitializer.IsInitialized)
        {
            await Task.Yield();
        }

        Debug.Log("[AuthManager] Unity Services initialized.");

        try
        {
            await SignInAnonymously();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"⚠ Anonymous sign-in failed: {e}");
        }

        Debug.Log("[AuthManager] Waiting for CloudSaveManager...");

        float timeout = 5f;
        float elapsed = 0f;

        while (CloudSaveManager.Instance == null && elapsed < timeout)
        {
            await Task.Yield();
            elapsed += Time.deltaTime;
        }

        if (CloudSaveManager.Instance != null)
        {
            Debug.Log("[AuthManager] Loading cloud data on launch...");
            await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
        }
        else
        {
            Debug.LogError("[AuthManager] CloudSaveManager not found.");
        }
    }

    async Task SignInAnonymously()
    {
        Debug.Log($"[AuthManager] IsSignedIn: {AuthenticationService.Instance.IsSignedIn}");

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("[AuthManager] Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"✅ Anonymous login success. PlayerID: {AuthenticationService.Instance.PlayerId}");
        }
        else
        {
            Debug.Log("[AuthManager] Already signed in.");
        }
    }

#if UNITY_ANDROID
    public void SignInWithGoogle()
    {
        Debug.Log("[AuthManager] SignInWithGoogle() called.");

        if (IsGoogleAlreadyLinked())
        {
            Debug.Log("ℹ Google already linked. Just loading cloud data.");
            _ = CloudSaveManager.Instance?.LoadAllPlayerDataFromCloud();
            return;
        }

        if (!playGamesActivated)
        {
            PlayGamesPlatform.Activate();
            playGamesActivated = true;
            Debug.Log("[AuthManager] Play Games platform activated.");
        }

        Debug.Log("[AuthManager] Authenticating with Google Play Games...");

        Social.localUser.Authenticate(success =>
        {
            if (!success)
            {
                Debug.LogError("❌ Google Play Games login failed.");
                return;
            }

            Debug.Log("✅ Google Play Games login success.");

            PlayGamesPlatform.Instance.RequestServerSideAccess(false, authCode =>
            {
                if (string.IsNullOrEmpty(authCode))
                {
                    Debug.LogError("❌ Server auth code is null.");
                    return;
                }

                StartCoroutine(ProcessGoogleSignInCoroutine(authCode));
            });
        });
    }

    System.Collections.IEnumerator ProcessGoogleSignInCoroutine(string serverAuthCode)
    {
        var task = ProcessGoogleSignIn(serverAuthCode);
        yield return new WaitUntil(() => task.IsCompleted);
    }

   async Task ProcessGoogleSignIn(string serverAuthCode)
    {
        try
        {
            Debug.Log("[AuthManager] Signing in with Google Play (Unity Auth)...");
            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(serverAuthCode);

            Debug.Log("✅ Google sign-in successful. Loading cloud data...");
            await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
        }
        catch (AuthenticationException e)
        {
            Debug.LogWarning($"⚠ Google sign-in failed: {e.Message} | Code: {e.ErrorCode}");

            // ✅ RETURNING USER / REINSTALL CASE
            if (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked ||
                AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("🔁 Google account already linked. Loading cloud data only.");

                if (CloudSaveManager.Instance != null)
                {
                    await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
                    Debug.Log("☁ Cloud data loaded for returning user");
                }

                return;
            }

            // ✅ FIRST-TIME GOOGLE USER ONLY
            Debug.Log("🔗 Linking Google account to anonymous user...");
            await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(serverAuthCode);

            if (CloudSaveManager.Instance != null)
            {
                await CloudSaveManager.Instance.SaveAllPlayerDataToCloud();
                Debug.Log("☁ Local data uploaded after linking Google account");
            }
        }
    }


    bool IsGoogleAlreadyLinked()
    {
        var info = AuthenticationService.Instance.PlayerInfo;
        if (info?.Identities == null) return false;

        foreach (var identity in info.Identities)
        {
            if (identity.TypeId == "google_play_games")
                return true;
        }

        return false;
    }
#endif

#if UNITY_IOS
    public async void SignInWithApple(string identityToken)
    {
        Debug.Log("[AuthManager] SignInWithApple() called.");

        if (IsAppleAlreadyLinked())
        {
            Debug.Log("ℹ Apple already linked.");
            return;
        }

        try
        {
            await AuthenticationService.Instance.SignInWithAppleAsync(identityToken);
            await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
        }
        catch (AuthenticationException)
        {
            await AuthenticationService.Instance.LinkWithAppleAsync(identityToken);
            await CloudSaveManager.Instance.SaveAllPlayerDataToCloud();
        }
    }

    bool IsAppleAlreadyLinked()
    {
        var info = AuthenticationService.Instance.PlayerInfo;
        if (info?.Identities == null) return false;

        foreach (var identity in info.Identities)
        {
            if (identity.TypeId == "apple.com")
                return true;
        }

        return false;
    }
#endif
}
