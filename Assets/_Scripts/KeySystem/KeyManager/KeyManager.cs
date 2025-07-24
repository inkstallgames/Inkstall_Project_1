using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using TMPro;

[System.Serializable]
public class KeyResponse
{
    public int keys;
}

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int keysCount = 0;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private string apiBaseUrl = "http://localhost:4000/api/slot/get-keys/";

    public string userId;

    private void Awake()
    {
        // Set up singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Only initialize the UI, don't fetch keys yet
        keyText.text = keysCount.ToString();
    }

    // This will be called by UserIDBridge after it sets userId
    public void FetchKeysFromDB()
    {
        if (!string.IsNullOrEmpty(userId))
        {
            StartCoroutine(FetchDBKeyCount());
        }
        else
        {
            Debug.LogError("Cannot fetch keys: userId is empty");
        }
    }

    // Fetch the key count from the API
    public IEnumerator FetchDBKeyCount()
    {
        string url = apiBaseUrl + userId;
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string json = www.downloadHandler.text;
            KeyResponse data = JsonUtility.FromJson<KeyResponse>(json);
            keysCount = data.keys;
            UpdateUIKeyCount();
        }
        else
        {
            Debug.LogError("Error fetching keys: " + www.error);
        }
    }

    // Returns the current key count as an integer
    public int GetCurrentKeyCount()
    {
        return keysCount;
    }

    // Use a key (called by LockedDoor when unlocking)
    public bool UseKey()
    {
        if (keysCount > 0)
        {
            keysCount--;
            UpdateDBKeyCount();
            UpdateUIKeyCount();
            return true;
        }
        return false;
    }

    // Update the database with the current key count
    private void UpdateDBKeyCount()
    {
        StartCoroutine(SendKeyUpdateToDB());
    }

    private IEnumerator SendKeyUpdateToDB()
    {
        KeyResponse keyData = new KeyResponse();
        keyData.keys = keysCount;

        string jsonData = JsonUtility.ToJson(keyData);
        string url = apiBaseUrl + userId;
        UnityWebRequest www = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Failed to update key count: " + www.error);
        }
    }

    // Update the UI to show the current key count
    private void UpdateUIKeyCount()
    {
        keyText.text = keysCount.ToString();
        Debug.Log("Keys: " + keysCount);
    }
}
