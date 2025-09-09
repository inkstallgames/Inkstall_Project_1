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
        // Get the user ID from StudentIdManager or PlayerPrefs
        GetUserId();

        // Now fetch the data
        if (!string.IsNullOrEmpty(userId))
        {
            Debug.Log($"[CoinsManager] Starting to fetch coins for user: {userId}");
            FetchCoins();
        }
        else
        {
            Debug.LogError("[CoinsManager] No user ID available. Coins cannot be fetched.");
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
                Debug.Log($"[CoinsManager] Got user ID from StudentIdManager: {userId}");
                return;
            }
        }
        
        // If still empty, try PlayerPrefs directly
        if (string.IsNullOrEmpty(userId))
        {
            userId = PlayerPrefs.GetString("StudentId", "");
            if (!string.IsNullOrEmpty(userId))
            {
                Debug.Log($"[CoinsManager] Got user ID from PlayerPrefs: {userId}");
                return;
            }
        }
        
        // Log if we still don't have a user ID
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[CoinsManager] No user ID found in StudentIdManager or PlayerPrefs");
            
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
        Debug.Log($"[CoinsManager] User ID loaded from event: {userId}");
        
        FetchCoins();
    }
    
    private IEnumerator DelayedFetchAttempt()
    {
        Debug.Log("[CoinsManager] Will retry fetching user ID in 2 seconds...");
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
        Debug.Log("[CoinsManager] ===== Starting API Request =====");
        Debug.Log($"[CoinsManager] User ID being used: {userId}");
        Debug.Log($"[CoinsManager] Current coins BEFORE fetch: {currentCoins}");

        string url = getCoinsURL + userId;
        Debug.Log($"[CoinsManager] Full API Endpoint: {url}");
        
        // Log environment information
        Debug.Log($"[CoinsManager] Application Version: {Application.version}");
        Debug.Log($"[CoinsManager] Platform: {Application.platform}");
        Debug.Log($"[CoinsManager] Is Editor: {Application.isEditor}");

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 15;
        
        // Add headers for debugging
        request.SetRequestHeader("X-Debug-Request-ID", System.Guid.NewGuid().ToString());
        request.SetRequestHeader("X-Client-Version", Application.version);
        
        // Check if we have an auth token
        string authToken = PlayerPrefs.GetString("auth_token", "");
        if (!string.IsNullOrEmpty(authToken))
        {
            request.SetRequestHeader("Authorization", $"Bearer {authToken}");
            Debug.Log("[CoinsManager] Added Authorization header to request");
        }
        else
        {
            Debug.LogWarning("[CoinsManager] No auth token found in PlayerPrefs");
        }

        Debug.Log("[CoinsManager] Sending request to server...");
        float startTime = Time.time;
        yield return request.SendWebRequest();
        float endTime = Time.time;

        Debug.Log($"[CoinsManager] Request completed in {(endTime - startTime):F2} seconds");
        Debug.Log($"[CoinsManager] Response Code: {request.responseCode}");
        Debug.Log($"[CoinsManager] Response Error: {request.error}");
        
        // Log response headers if available
        if (request.GetResponseHeaders() != null)
        {
            Debug.Log("[CoinsManager] Response Headers:");
            foreach (var header in request.GetResponseHeaders())
            {
                Debug.Log($"[CoinsManager]   {header.Key}: {header.Value}");
            }
        }
        Debug.Log($"[CoinsManager] Response code: {request.responseCode}");
        Debug.Log($"[CoinsManager] Error (if any): {request.error}");

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            Debug.Log($"[CoinsManager] Raw API Response: {json}");
            
            // Log the raw response for debugging
            Debug.Log($"[CoinsManager] Response Length: {json.Length} characters");
            Debug.Log($"[CoinsManager] Response Contains 'error': {json.ToLower().Contains("error")}");
            Debug.Log($"[CoinsManager] Response Contains 'not found': {json.ToLower().Contains("not found")}");
            
            // Check if the response is valid JSON
            if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("{"))
            {
                Debug.LogError("[CoinsManager] Invalid JSON response received");
                yield break;
            }

            try
            {
                Debug.Log("[CoinsManager] Attempting to parse JSON response...");
                CoinResponse res = JsonUtility.FromJson<CoinResponse>(json);
                
                if (res == null)
                {
                    Debug.LogError("[CoinsManager] Failed to parse JSON - res is null");
                    yield break;
                }

                Debug.Log($"[CoinsManager] JSON parsed successfully. Success: {res.success}");
                Debug.Log($"[CoinsManager] currentMonthPoints is null: {res.currentMonthPoints == null}");

                if (res.currentMonthPoints != null)
                {
                    Debug.Log("[CoinsManager] Current Month Points Data:");
                    Debug.Log($"- _id: {res.currentMonthPoints._id}");
                    Debug.Log($"- studentId: {res.currentMonthPoints.studentId}");
                    Debug.Log($"- month: {res.currentMonthPoints.month}");
                    Debug.Log($"- totalPoints: {res.currentMonthPoints.totalPoints}");
                    Debug.Log($"- points: {res.currentMonthPoints.points}");

                    int oldCoins = currentCoins;
                    int newCoins = res.currentMonthPoints.totalPoints;
                    Debug.Log($"[CoinsManager] Updating coins: {oldCoins} → {newCoins}");
                    
                    currentCoins = newCoins;
                    
                    // Force immediate UI update
                    Debug.Log("[CoinsManager] Calling UpdateCoinUI...");
                    UpdateCoinUI();
                    
                    // Notify listeners
                    Debug.Log("[CoinsManager] Invoking OnCoinsUpdated event...");
                    OnCoinsUpdated?.Invoke();
                    Debug.Log("[CoinsManager] OnCoinsUpdated event invoked");
                    
                    // Double check the UI was updated
                    if (coinText != null)
                    {
                        Debug.Log($"[CoinsManager] Final UI check - coinText.text: {coinText.text}");
                    }
                }
                else
                {
                    Debug.LogError("[CoinsManager] currentMonthPoints is null in the response");
                    Debug.LogError($"[CoinsManager] Full response: {json}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CoinsManager] Exception while processing response: {e}");
                Debug.LogError($"[CoinsManager] Stack trace: {e.StackTrace}");
                Debug.LogError($"[CoinsManager] Raw JSON that caused error: {json}");
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
            StartCoroutine(SendSpendRequest(amount, reason, (success) =>
            {
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
        Debug.Log($"[CoinsManager] UpdateCoinUI called. Current coins: {currentCoins}");
        
        if (coinText == null)
        {
            Debug.LogError("[CoinsManager] ERROR: coinText reference is null! Cannot update UI.");
            
            // Try to find the TextMeshProUGUI component if it's missing
            coinText = FindObjectOfType<TextMeshProUGUI>();
            if (coinText != null)
            {
                Debug.Log("[CoinsManager] Found TextMeshProUGUI component in scene");
            }
            else
            {
                Debug.LogError("[CoinsManager] Could not find TextMeshProUGUI component in scene");
                return;
            }
        }
        
        string newTextValue = currentCoins.ToString();
        Debug.Log($"[CoinsManager] Setting coinText.text to: {newTextValue}");
        
        try
        {
            coinText.text = newTextValue;
            Debug.Log($"[CoinsManager] UI updated successfully. New value: {coinText.text}");
            
            // Force update the canvas
            if (coinText.canvas != null)
            {
                Canvas.ForceUpdateCanvases();
                Debug.Log("[CoinsManager] Canvas update forced");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CoinsManager] ERROR updating UI: {e.Message}");
            Debug.LogError($"[CoinsManager] Stack trace: {e.StackTrace}");
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
