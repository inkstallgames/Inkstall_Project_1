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

        await SignInAnonymously();
        await CloudSaveManager.Instance.LoadAllPlayerDataFromCloud();
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
