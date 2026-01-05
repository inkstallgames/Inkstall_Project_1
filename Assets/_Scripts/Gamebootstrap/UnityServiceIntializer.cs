using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;
using System.Threading.Tasks;

public class UnityServicesInitializer : MonoBehaviour
{
    public static bool IsInitialized { get; private set; }

    async void Awake()
    {
        Debug.Log("[UnityServicesInitializer] Awake() called.");
        
        if (FindObjectsOfType<UnityServicesInitializer>().Length > 1)
        {
            Debug.LogWarning("[UnityServicesInitializer] Duplicate found. Destroying this component only.");
            Destroy(this);
            return;
        }

        DontDestroyOnLoad(gameObject);
        Debug.Log("[UnityServicesInitializer] Starting InitializeServices...");
        await InitializeServices();
    }

    async Task InitializeServices()
    {
        Debug.Log("[UnityServicesInitializer] InitializeServices() called.");
        
        if (IsInitialized)
        {
            Debug.Log("[UnityServicesInitializer] Already initialized.");
            return;
        }

        try
        {
            Debug.Log("[UnityServicesInitializer] Creating initialization options...");
            var options = new InitializationOptions();

#if UNITY_EDITOR
            options.SetProfile("editor-test-user");
            Debug.Log("[UnityServicesInitializer] Using editor test profile.");
#endif

            Debug.Log("[UnityServicesInitializer] Calling UnityServices.InitializeAsync...");
            await UnityServices.InitializeAsync(options);
            IsInitialized = true;

            Debug.Log("✅ Unity Services Initialized");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Unity Services Init Failed: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }
}
