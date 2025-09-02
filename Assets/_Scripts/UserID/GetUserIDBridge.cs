using UnityEngine;
using System.Runtime.InteropServices;

// Acts as a bridge between the webGL and unity
public class GetUserIDBridge : MonoBehaviour
{
    public static string userId = "";
    // Default user ID to use if none is found in local storage
    public string defaultUserId = "681ee0e6198ad04bf6c1c733"; // Same as the one in KeyManager

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void GetUserIdFromLocalStorage();
#endif


    void Start()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        GetUserIdFromLocalStorage();
        #else
        // In editor or non-WebGL builds, use the default ID
        UseDefaultUserId("");
        #endif
    }

    // This will be called from JS
    public void ReceiveUserId(string id)
    {
        userId = id;
        Debug.Log("User ID received from localstorage: " + userId);
        SendUserIdToKeyManager();
        SendUserIdToCoinsManager();
    }

    // This will be called when userId is not found in localStorage or when in editor
    public void UseDefaultUserId(string unused)
    {
        userId = defaultUserId;
        Debug.Log("Using default User ID: " + userId);
        SendUserIdToKeyManager();
        SendUserIdToCoinsManager();
    }

    public void SendUserIdToKeyManager()
    {
        if (KeyManager.Instance != null)
        {
            KeyManager.Instance.studentId = userId;
            // Call the new method to fetch keys after setting the userId
            KeyManager.Instance.FetchKeysFromDB();
        }
        else
        {
            Debug.LogError("KeyManager instance not found! Make sure it exists in the scene.");
        }
    }

    public void SendUserIdToCoinsManager()
    {
        if (CoinsManager.Instance != null)
        {
            CoinsManager.Instance.userId = userId;
            // Call the new method to fetch coins after setting the userId
            CoinsManager.Instance.FetchCoins();
        }
        else
        {
            Debug.LogError("CoinsManager instance not found! Make sure it exists in the scene.");
        }
    }
}
