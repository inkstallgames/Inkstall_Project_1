using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using TMPro;

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int keysCount = 0;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private string apiUrl = "https://your-api.com/keys/player123";

    void Awake()
    {
        Instance = this;  // No DontDestroyOnLoad here
    }

    private void Start()
    {
        StartCoroutine(FetchDBKeyCount());
    }

    // Fetch the key count from the API
    public IEnumerator FetchDBKeyCount()
    {
        UnityWebRequest www = UnityWebRequest.Get(apiUrl);
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

    // Update the UI to show the current key count
    private void UpdateUIKeyCount()
    {
        keyText.text = $"Keys: {keysCount}";
        Debug.Log("Keys: " + keysCount);
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
        UnityWebRequest www = new UnityWebRequest(apiUrl, "POST");
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

    // Class to hold the key count response from the API
    [System.Serializable]
    public class KeyResponse
    {
        public int keys;
    }
}
