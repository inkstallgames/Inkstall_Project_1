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

    // API Endpoints - make sure these match your backend configuration
    private const string BASE_URL = "https://api.inkstall.in";
    private string getCoinsURL => $"{BASE_URL}/api/student-portal/studentpoints/";
    private string spendCoinsURL => $"{BASE_URL}/api/student-portal/studentpoints/deduct-points";
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {        
        // Get the user ID from StudentIdManager or PlayerPrefs
        GetUserId();

        // Now fetch the data
        if (!string.IsNullOrEmpty(userId))
        {
            FetchCoins();
        }
        else
        {
            // Try to get user ID again after a delay
            StartCoroutine(DelayedFetchAttempt());
        }
    }

    // Get user ID from StudentIdManager or directly from PlayerPrefs
    private void GetUserId()
    {
        // First try StudentIdManager
        if (string.IsNullOrEmpty(userId) && StudentIdManager.Instance != null)
        {
            userId = StudentIdManager.Instance.GetStudentId();
            if (!string.IsNullOrEmpty(userId))
            {
                return;
            }
        }
        
        // If still empty, try PlayerPrefs directly
        if (string.IsNullOrEmpty(userId))
        {
            userId = PlayerPrefs.GetString("StudentId", "");
            if (!string.IsNullOrEmpty(userId))
            {
                return;
            }
        }
        
        // Log if we still don't have a user ID
        if (string.IsNullOrEmpty(userId))
        {
            // Subscribe to StudentIdManager events to get the ID when it becomes available
            if (StudentIdManager.Instance != null)
            {
                StudentIdManager.Instance.OnStudentIdLoaded += HandleStudentIdLoaded;
            }
        }
    }
    
    private void HandleStudentIdLoaded(string id)
    {
        // Unsubscribe to avoid multiple calls
        if (StudentIdManager.Instance != null)
        {
            StudentIdManager.Instance.OnStudentIdLoaded -= HandleStudentIdLoaded;
        }
        
        // Set the user ID and fetch coins
        userId = id;
        FetchCoins();
    }
    
    private IEnumerator DelayedFetchAttempt()
    {
        yield return new WaitForSeconds(2f);
        
        // Try to get user ID again
        GetUserId();

        if (!string.IsNullOrEmpty(userId))
        {
            FetchCoins();
        }
    }

    public void FetchCoins()
    {
        if (!string.IsNullOrEmpty(userId))
        {
            StartCoroutine(FetchCoinsFromServer());
        }
        else
        {
        }
    }

    IEnumerator FetchCoinsFromServer()
    {
        string url = getCoinsURL + userId;
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 15;
        
        // Add auth token if available
        string authToken = PlayerPrefs.GetString("auth_token", "");
        if (!string.IsNullOrEmpty(authToken))
        {
            request.SetRequestHeader("Authorization", $"Bearer {authToken}");
        }

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            
            // Check if the response is valid JSON
            if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("{"))
            {
                yield break;
            }

            try
            {
                CoinResponse res = JsonUtility.FromJson<CoinResponse>(json);
                
                if (res == null)
                {
                    yield break;
                }

                if (res.currentMonthPoints != null)
                {
                    int newCoins = res.currentMonthPoints.totalPoints;
                    currentCoins = newCoins;
                    
                    UpdateCoinUI();
                    OnCoinsUpdated?.Invoke();
                }
                else
                {
                }
            }
            catch (System.Exception e)
            {
            }
        }
        else
        {
        }
    }

    public void SpendCoins(int amount, string reason, System.Action<bool> onComplete = null)
    {
        if (currentCoins >= amount)
        {
            // First, update the UI immediately for better responsiveness
            currentCoins -= amount;
            UpdateCoinUI();
            OnCoinsUpdated?.Invoke();

            // Then send the request to the server
            StartCoroutine(SendSpendRequest(amount, reason, (success) =>
            {
                if (!success)
                {
                    // If server request failed, revert the local changes
                    currentCoins += amount;
                    UpdateCoinUI();
                    OnCoinsUpdated?.Invoke();
                }
                onComplete?.Invoke(success);
            }));
        }
        else
        {
            onComplete?.Invoke(false);
        }
    }

    public void UpdateCoinUI()
    {
        if (coinText == null)
        {
            Debug.LogError("[CoinsManager] coinText is not assigned in the Inspector!");
            return;
        }
        
        try
        {
            coinText.text = currentCoins.ToString();
            
            // Force update the canvas
            if (coinText.canvas != null)
            {
                Canvas.ForceUpdateCanvases();
            }
        }
        catch (System.Exception e)
        {
        }
    }

    IEnumerator SendSpendRequest(int amount, string reason, System.Action<bool> onComplete)
    {
        // Format the month as yyyy-MM
        string currentMonth = System.DateTime.Now.ToString("yyyy-MM");

        CoinSpendRequest body = new CoinSpendRequest
        {
            studentId = userId,
            points = amount,
            reason = reason,
            month = currentMonth
        };
        string json = JsonUtility.ToJson(body);

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
        }

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        onComplete?.Invoke(true);
    }

    IEnumerator FetchCoinsAfterSpend()
    {
        yield return new WaitForSeconds(3.0f);

        UnityWebRequest request = UnityWebRequest.Get(getCoinsURL + userId);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            yield break;
        }

        string responseJson = request.downloadHandler.text;

        try
        {
            CoinsResponse response = JsonUtility.FromJson<CoinsResponse>(responseJson);
            if (response != null && response.data != null && response.data.Length > 0)
            {
                CurrentMonthPoints currentMonthData = response.data[0];
                currentCoins = currentMonthData.points;
                UpdateCoinUI();
                OnCoinsUpdated?.Invoke();
            }
            else
            {
            }
        }
        catch (System.Exception e)
        {
        }
    }

    public void FetchLocalCoinsCount()
    {
        FetchCoins();
    }

    public void AddCoins(int amount, string reason = "Alien Defeated")
    {
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
        // Log points being added
        Debug.Log($"[CoinsManager] Adding {amount} coins for: {reason}");

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


        if (request.result != UnityWebRequest.Result.Success)
        {
            // Log points reduction when server update fails
            Debug.Log($"[CoinsManager] Failed to add {amount} coins, reverting change. Reason: {request.error}");
            if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
            {
            }
            // Revert local changes if server update fails
            currentCoins -= amount;
            UpdateCoinUI();
        }
        else
        {
            Debug.Log($"[CoinsManager] Successfully added {amount} coins for: {reason}");
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
