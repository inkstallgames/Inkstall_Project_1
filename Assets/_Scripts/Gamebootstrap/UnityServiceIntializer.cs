using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;
using System.Threading.Tasks;

public class UnityServicesInitializer : MonoBehaviour
{
    public static bool IsInitialized { get; private set; }

    async void Awake()
    {
        if (FindObjectsOfType<UnityServicesInitializer>().Length > 1)
        {
            Debug.LogWarning("[UnityServicesInitializer] Duplicate found. Destroying this component only.");
            Destroy(this);
            return;
        }

        DontDestroyOnLoad(gameObject);
        await InitializeServices();
    }

    async Task InitializeServices()
    {
        if (IsInitialized)
            return;

        try
        {
            var options = new InitializationOptions();

#if UNITY_EDITOR
            options.SetProfile("editor-test-user"); // 👈 SAME ID EVERY TIME
#endif

            await UnityServices.InitializeAsync(options);
            IsInitialized = true;

            Debug.Log("✅ Unity Services Initialized");
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Init Failed: " + e.Message);
        }
    }
}
