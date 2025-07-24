using UnityEngine;
using System.Runtime.InteropServices;

public class UserIDBridge : MonoBehaviour
{
    public static string userId = "";

    [DllImport("__Internal")]
    private static extern string GetUserIdFromLocalStorage();

    void Start()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        GetUserIdFromLocalStorage();
        #endif
    }

    // This wil be called from JS
    public void ReceiveUserId(string id)
    {
        userId = id;
        Debug.Log("User ID received from localstorage: " + userId);
    }
}
