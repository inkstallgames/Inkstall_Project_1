using UnityEngine;
using System.Runtime.InteropServices;

// Acts as a bridge between the webGL and unity
public class GetUserIDBridge : MonoBehaviour
{
    public static string userId = "";

    [DllImport("__Internal")]
    private static extern void GetUserIdFromLocalStorage();

    void Start()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        GetUserIdFromLocalStorage();
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

    public void SendUserIdToKeyManager()
    {
        if (KeyManager.Instance != null)
        {
            KeyManager.Instance.userId = userId;
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

