using Unity.Services.Authentication;
using UnityEngine;
using System.Threading.Tasks;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance;

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
        // Wait until Unity Services are initialized
        while (!UnityServicesInitializer.IsInitialized)
        {
            await Task.Yield();
        }

        try
        {
            await SignInAnonymously();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Sign-in failed, proceeding in offline mode. Error: {e.Message}");
        }

        // This will now run regardless of sign-in success.
        // CloudSaveManager will handle loading from cloud or local PlayerPrefs.
        if (CloudSaveManager.Instance != null)
        {
            await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
        }
        else
        {
            Debug.LogError("CloudSaveManager instance not found. Player data cannot be loaded.");
        }
    }


    async Task SignInAnonymously()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("✅ Anonymous Login Success");
            Debug.Log("Player ID: " + AuthenticationService.Instance.PlayerId);
        }
    }
}
