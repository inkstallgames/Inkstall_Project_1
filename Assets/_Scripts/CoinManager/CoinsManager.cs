using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class CoinsManager : MonoBehaviour
{
    public static CoinsManager Instance;

    public TextMeshProUGUI coinText; // Assign in Inspector
    private string playerId;
    private int currentCoins;

    private string getCoinsURL = "https://yourwebsite.com/api/get-coins/";
    private string spendCoinsURL = "https://yourwebsite.com/api/spend-coins";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // persist if needed
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Auto-login using saved ID
        if (PlayerPrefs.HasKey("studentId"))
        {
            playerId = PlayerPrefs.GetString("studentId");
            FetchCoins();
        }
        else
        {
            Debug.LogWarning("No studentId found");
        }
    }

    public void FetchCoins()
    {
        StartCoroutine(FetchCoinsFromServer());
    }

    public void SpendCoins(int amount)
    {
        if (currentCoins >= amount)
        {
            currentCoins -= amount;
            UpdateCoinUI();
            StartCoroutine(SendSpendRequest(amount));
        }
        else
        {
            Debug.Log("Not enough coins.");
        }
    }

    public void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = currentCoins.ToString();
    }

    IEnumerator FetchCoinsFromServer()
    {
        UnityWebRequest request = UnityWebRequest.Get(getCoinsURL + playerId);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            CoinResponse res = JsonUtility.FromJson<CoinResponse>(request.downloadHandler.text);
            currentCoins = res.coins;
            UpdateCoinUI();
        }
        else
        {
            Debug.LogError("Failed to fetch coins: " + request.error);
        }
    }

    IEnumerator SendSpendRequest(int amount)
    {
        CoinSpendRequest body = new CoinSpendRequest { playerId = playerId, amount = amount };
        string json = JsonUtility.ToJson(body);

        UnityWebRequest request = new UnityWebRequest(spendCoinsURL, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Spend failed: " + request.error);
            // Optionally refund coins here
        }
    }

    [System.Serializable]
    public class CoinResponse { public int coins; }

    [System.Serializable]
    public class CoinSpendRequest
    {
        public string playerId;
        public int amount;
    }
}
