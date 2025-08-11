using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class CoinsManager : MonoBehaviour
{
    public static CoinsManager Instance;

    public TextMeshProUGUI coinText; // Assign in Inspector
    public string userId;
    public int currentCoins;

    private string getCoinsURL = "http://localhost:4000/api/slot/get-keys";
    private string spendCoinsURL = "http://localhost:4000/api/slot/spend-keys";

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

    public void FetchCoins()
    {
        StartCoroutine(FetchCoinsFromServer());
    }

    IEnumerator FetchCoinsFromServer()
    {
        UnityWebRequest request = UnityWebRequest.Get(getCoinsURL + userId);
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
    
    public void SpendCoins(int amount, System.Action<bool> onComplete = null)
    {
        if (currentCoins >= amount)
        {
            // First, update the UI immediately for better responsiveness
            currentCoins -= amount;
            UpdateCoinUI();
            
            // Then start the server request
            StartCoroutine(SendSpendRequest(amount, (success) => {
                if (!success)
                {
                    // If server request failed, revert the local changes
                    currentCoins += amount;
                    UpdateCoinUI();
                }
                onComplete?.Invoke(success);
            }));
        }
        else
        {
            Debug.Log("Not enough coins.");
            onComplete?.Invoke(false);
        }
    }

    public void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = currentCoins.ToString();
    }

    IEnumerator SendSpendRequest(int amount, System.Action<bool> onComplete)
    {
        CoinSpendRequest body = new CoinSpendRequest { userId = userId, amount = amount };
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
            onComplete?.Invoke(false);
            yield break;
        }
        
        onComplete?.Invoke(true);
    }

    public void FetchLocalCoinsCount()
    {
        FetchCoins();
    }

    [System.Serializable]
    public class CoinResponse { public int coins; }

    [System.Serializable]
    public class CoinSpendRequest
    {
        public string userId;
        public int amount;
    }
}
