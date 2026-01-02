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

    async void Start()
    {
        while (!UnityServicesInitializer.IsInitialized)
            await Task.Yield();

        try
        {
            await SignInAnonymously();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("⚠ Anonymous sign-in failed: " + e.Message);
        }

        if (CloudSaveManager.Instance != null)
            await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
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
    public void SignInWithGoogle()
    {
        if (IsGoogleAlreadyLinked())
        {
            Debug.Log("ℹ Google already linked");
            return;
        }

        if (!playGamesActivated)
        {
            PlayGamesPlatform.Activate();
            playGamesActivated = true;
        }

        Social.localUser.Authenticate(success =>
        {
            if (!success)
            {
                Debug.LogError("❌ Google Play Games login failed");
                return;
            }

            Debug.Log("✅ Google Play Games login success");

            PlayGamesPlatform.Instance.RequestServerSideAccess(false, async authCode =>
            {
                if (string.IsNullOrEmpty(authCode))
                {
                    Debug.LogError("❌ Failed to retrieve server auth code");
                    return;
                }

                try
                {
                    await AuthenticationService.Instance
                        .LinkWithGooglePlayGamesAsync(authCode);

                    Debug.Log("✅ Google account linked with Unity");
                }
                catch (AuthenticationException e)
                {
                    Debug.LogWarning("⚠ Google already linked or conflict: " + e.Message);
                }
            });
        });
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
    public async void LinkApple(string identityToken)
    {
        try
        {
            await AuthenticationService.Instance.LinkWithAppleAsync(identityToken);
            Debug.Log("✅ Apple account linked");
        }
        catch (AuthenticationException e)
        {
            Debug.LogWarning("⚠ Apple link failed: " + e.Message);
        }
    }
#endif
}
