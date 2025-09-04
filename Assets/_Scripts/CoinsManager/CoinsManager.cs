using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

public class CoinsManager : MonoBehaviour
{
    public static CoinsManager Instance;

    // Event that will be triggered whenever coins are updated
    public event System.Action OnCoinsUpdated;

    public TextMeshProUGUI coinText; // Assign in Inspector
    public string userId;
    public int currentCoins;

    private string getCoinsURL = "https://api.inkstall.in/api/student-portal/studentpoints/";
    private string spendCoinsURL = "https://api.inkstall.in/api/student-portal/studentpoints/deduct-points";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[CoinsManager] Instance initialized");
            DontDestroyOnLoad(gameObject); // persist if needed
        }
        else
        {
            Debug.Log("[CoinsManager] Duplicate instance found, destroying this one");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // If studentId is not set, try to get it from GameDataManager
        if (string.IsNullOrEmpty(userId) && GameDataManager.Instance != null)
        {
            userId = GameDataManager.Instance.StudentId;
        }
        
        // Now fetch the data
        if (!string.IsNullOrEmpty(userId))
        {
            FetchCoins();
        }
    }

    public void FetchCoins()
    {
        Debug.Log("[CoinsManager] FetchCoins called");
        if (!string.IsNullOrEmpty(userId))
        {
            Debug.Log("[CoinsManager] Valid userId found: " + userId + ", starting fetch coroutine");
            StartCoroutine(FetchCoinsFromServer());
        }
        else
        {
            Debug.LogError("[CoinsManager] Cannot fetch coins: userId is empty");
        }
    }

    IEnumerator FetchCoinsFromServer()
    {
        Debug.Log("[CoinsManager] Starting API call to fetch coins...");
        Debug.Log("[CoinsManager] Current coins BEFORE fetch: " + currentCoins);
        
        string url = getCoinsURL + userId;
        Debug.Log($"[CoinsManager] URL: {url}");
        
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 15; // Set timeout to 15 seconds
        Debug.Log("[CoinsManager] Web request created, sending...");
        
        float startTime = Time.time;
        yield return request.SendWebRequest();
        float endTime = Time.time;
        
        Debug.Log("[CoinsManager] Request completed in " + (endTime - startTime).ToString("F2") + " seconds");
        Debug.Log("[CoinsManager] Response code: " + request.responseCode);

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            Debug.Log("[CoinsManager] API Response received: " + json);
            
            try
            {
                // Parse the response to get the currentMonthPoints.totalPoints value
                CoinResponse res = JsonUtility.FromJson<CoinResponse>(json);
                Debug.Log("[CoinsManager] Successfully parsed JSON response");
                
                if (res != null && res.currentMonthPoints != null)
                {
                    Debug.Log("[CoinsManager] COINS FETCHED FROM API: " + res.currentMonthPoints.totalPoints);
                    
                    int oldCoins = currentCoins;
                    currentCoins = res.currentMonthPoints.totalPoints;
                    
                    Debug.Log("[CoinsManager] Coins updated: " + oldCoins + " → " + currentCoins);
                    UpdateCoinUI();
                    
                    // Notify listeners that coins have been updated
                    OnCoinsUpdated?.Invoke();
                    Debug.Log("[CoinsManager] OnCoinsUpdated event invoked");
                }
                else
                {
                    Debug.LogError("[CoinsManager] Failed to get currentMonthPoints from response");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CoinsManager] Error parsing JSON response: " + e.Message);
                Debug.LogError("[CoinsManager] JSON that failed to parse: " + json);
            }
        }
        else
        {
            Debug.LogError("[CoinsManager] Failed to fetch coins: " + request.error);
            Debug.LogError("[CoinsManager] Response code: " + request.responseCode);
            if (request.downloadHandler != null && request.downloadHandler.text != null)
            {
                Debug.LogError("[CoinsManager] Error response body: " + request.downloadHandler.text);
            }
        }
    }
    
    public void SpendCoins(int amount, string reason, System.Action<bool> onComplete = null)
    {
        Debug.Log("[CoinsManager] SpendCoins called with amount: " + amount);
        Debug.Log("[CoinsManager] Current coins before spending: " + currentCoins);
        
        if (currentCoins >= amount)
        {
            // First, update the UI immediately for better responsiveness
            currentCoins -= amount;
            Debug.Log("[CoinsManager] Coins reduced to: " + currentCoins);
            UpdateCoinUI();
            // Notify listeners that coins have been updated
            OnCoinsUpdated?.Invoke();
            
            // Then send the request to the server
            StartCoroutine(SendSpendRequest(amount, reason, (success) => {
                if (!success)
                {
                    Debug.LogError("[CoinsManager] Server request failed, reverting coin change");
                    // If server request failed, revert the local changes
                    currentCoins += amount;
                    Debug.Log("[CoinsManager] Coins reverted to: " + currentCoins);
                    UpdateCoinUI();
                    // Notify listeners that coins have been updated (reverted)
                    OnCoinsUpdated?.Invoke();
                }
                else
                {
                    Debug.Log("[CoinsManager] Server request successful, coins spent");
                }
                onComplete?.Invoke(success);
            }));
        }
        else
        {
            Debug.Log("[CoinsManager] Not enough coins. Required: " + amount + ", Available: " + currentCoins);
            onComplete?.Invoke(false);
        }
    }
    
    public void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.text = currentCoins.ToString();
            Debug.Log("[CoinsManager] UI updated with coins: " + currentCoins);
        }
        else
        {
            Debug.LogWarning("[CoinsManager] coinText is null, cannot update UI");
        }
    }

    IEnumerator SendSpendRequest(int amount, string reason, System.Action<bool> onComplete)
    {
        // Format the month as yyyy-MM
        string currentMonth = System.DateTime.Now.ToString("yyyy-MM");
        
        CoinSpendRequest body = new CoinSpendRequest { 
            studentId = userId, 
            points = amount, 
            reason = reason,
            month = currentMonth
        };
        string json = JsonUtility.ToJson(body);
        
        Debug.Log("[CoinsManager] Sending spend request with data: " + json);
        Debug.Log("[CoinsManager] URL: " + spendCoinsURL);

        UnityWebRequest request = new UnityWebRequest(spendCoinsURL, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        // Add Authorization header if we have a token
        string studentToken = PlayerPrefs.GetString("studenttoken", "");
        if (!string.IsNullOrEmpty(studentToken))
        {
            request.SetRequestHeader("Authorization", "Bearer " + studentToken);
            Debug.Log("[CoinsManager] Added Authorization header");
        }
        else
        {
            Debug.LogWarning("[CoinsManager] No student token found, proceeding without Authorization header");
        }

        Debug.Log("[CoinsManager] Web request created, sending...");
        yield return request.SendWebRequest();
        Debug.Log("[CoinsManager] Spend request completed with result: " + request.result);

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[CoinsManager] Spend failed: " + request.error);
            Debug.LogError("[CoinsManager] Response code: " + request.responseCode);
            if (request.downloadHandler != null && request.downloadHandler.text != null)
            {
                Debug.LogError("[CoinsManager] Error response body: " + request.downloadHandler.text);
            }
            onComplete?.Invoke(false);
            yield break;
        }
        
        Debug.Log("[CoinsManager] Spend successful");
        onComplete?.Invoke(true);
    }

    IEnumerator FetchCoinsAfterSpend()
    {
        // Wait a longer delay to ensure the server has processed the deduction
        Debug.Log("[CoinsManager] Waiting for server to process deduction...");
        yield return new WaitForSeconds(3.0f);
        
        Debug.Log("[CoinsManager] Fetching updated coins after spend...");
        
        // Create a new web request to fetch the latest coin count
        UnityWebRequest request = UnityWebRequest.Get(getCoinsURL + userId);
        yield return request.SendWebRequest();
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[CoinsManager] Failed to fetch updated coins: " + request.error);
            yield break;
        }
        
        string responseJson = request.downloadHandler.text;
        Debug.Log("[CoinsManager] Fetch response after spend: " + responseJson);
        
        try
        {
            CoinsResponse response = JsonUtility.FromJson<CoinsResponse>(responseJson);
            if (response != null && response.data != null && response.data.Length > 0)
            {
                CurrentMonthPoints currentMonthData = response.data[0];
                int updatedCoins = currentMonthData.points;
                
                Debug.Log("[CoinsManager] Updated coins from server: " + updatedCoins);
                
                // Update the local coin count and UI
                currentCoins = updatedCoins;
                UpdateCoinUI();
                OnCoinsUpdated?.Invoke();
            }
            else
            {
                Debug.LogError("[CoinsManager] Invalid response format or no data received");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[CoinsManager] Error parsing coins response: " + e.Message);
        }
    }

    public void FetchLocalCoinsCount()
    {
        FetchCoins();
    }

    public void AddCoins(int amount, string reason = "Alien Defeated")
    {
        Debug.Log($"[CoinsManager] Adding {amount} coins for reason: {reason}");
        Debug.Log($"[CoinsManager] Current coins before adding: {currentCoins}");
        
        // Update local coins immediately for better UX
        currentCoins += amount;
        UpdateCoinUI();
        
        // Start coroutine to update server
        StartCoroutine(SendAddCoinsRequest(amount, reason));
    }

    IEnumerator SendAddCoinsRequest(int amount, string reason)
    {
        string currentDate = System.DateTime.Now.ToString("yyyy-MM-dd");
        
        // Create request object matching the API's expected format
        var addRequest = new AddPointsRequest
        {
            studentId = userId,
            points = amount,
            reason = reason,
            date = currentDate,
            pointType = "game_points"
        };

        string json = JsonUtility.ToJson(addRequest);
        Debug.Log($"[CoinsManager] Sending add coins request: {json}");
        
        var request = new UnityWebRequest("https://api.inkstall.in/api/student-portal/studentpoints/add-game-points", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        string studentToken = PlayerPrefs.GetString("studenttoken", "");
        if (!string.IsNullOrEmpty(studentToken))
        {
            request.SetRequestHeader("Authorization", "Bearer " + studentToken);
        }

        yield return request.SendWebRequest();

        Debug.Log($"[CoinsManager] Request completed with status: {request.responseCode}");
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[CoinsManager] Failed to add coins: {request.error}");
            if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
            {
                Debug.LogError($"[CoinsManager] Error response: {request.downloadHandler.text}");
            }
            // Revert local changes if server update fails
            currentCoins -= amount;
            UpdateCoinUI();
        }
        else
        {
            Debug.Log($"[CoinsManager] Success response: {request.downloadHandler.text}");
            Debug.Log($"[CoinsManager] Successfully added {amount} coins");
            // Refresh coins from server to ensure sync
            FetchCoins();
        }
    }

    [System.Serializable]
    public class CoinResponse 
    { 
        public bool success;
        public CurrentMonthPoints currentMonthPoints;
    }

    [System.Serializable]
    public class CurrentMonthPoints
    {
        public string _id;
        public string studentId;
        public string studentName;
        public string grade;
        public string month;
        public int attendancePoints;
        public int assignmentPoints;
        public int quizPoints;
        public int gamePoints;
        public int testMarksPoints;
        public int totalPoints;
        public int points;
    }

    [System.Serializable]
    public class CoinSpendRequest
    {
        public string studentId;
        public int points;
        public string reason;
        public string month;
    }

    [System.Serializable]
    public class AddPointsRequest
    {
        public string studentId;
        public int points;
        public string reason;
        public string date;
        public string pointType;
    }

    [System.Serializable]
    public class PointsRecord
    {
        public int totalPoints;
    }

    [System.Serializable]
    public class PointsArrayWrapper
    {
        public PointsRecord[] pointsArray;
    }

    [System.Serializable]
    public class CoinsResponse
    {
        public bool success;
        public CurrentMonthPoints[] data;
    }
}
