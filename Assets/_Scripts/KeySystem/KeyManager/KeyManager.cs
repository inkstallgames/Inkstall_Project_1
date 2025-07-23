using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using TMPro;
using System.Runtime.InteropServices;

public class KeyManager : MonoBehaviour
{
    public static KeyManager Instance;

    [Header("Key Settings")]
    [SerializeField] private int keysCount = 0;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private string apiBaseUrl = "http://localhost:4000/api/slot/get-keys/";

    private string userId = "default_player";

    // Import the JavaScript function to get studentId from localStorage
    [DllImport("__Internal")]
    private static extern string GetStudentId();

    void Awake()
    {
        Instance = this;  // No DontDestroyOnLoad here
    }

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Get studentId from localStorage in WebGL builds
        userId = GetStudentId();
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("No student ID found in localStorage, using default");
            userId = "default_player";
        }
        else
        {
            Debug.Log("Retrieved Student ID from localStorage: " + userId);
        }
#else
        // Use a test ID when running in the Unity Editor
        userId = "test_student_id";
        Debug.Log("Running in Editor with test Student ID: " + userId);
#endif

        StartCoroutine(FetchDBKeyCount());
        keyText.text = keysCount.ToString();
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

    // Update the UI to show the current key count
    private void UpdateUIKeyCount()
    {
        keyText.text = keysCount.ToString();
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

    // Class to hold the key count response from the API
    [System.Serializable]
    public class KeyResponse
    {
        public int keys;
    }
}
