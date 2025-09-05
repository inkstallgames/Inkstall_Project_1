using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Google;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System;

public class GoogleLoginManager : MonoBehaviour
{
    [Header("Google / Firebase")]
    private string webClientId =
        "383598510964-51l74fgp8q3cqcqo8upqvlrndpvet7h8.apps.googleusercontent.com";

    [Header("Backend")]
    private string databaseCheckUrl =
        "https://api.inkstall.in/api/auth/unity-login";

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string nextSceneName = "MainScene";

    private FirebaseAuth auth;
    private bool firebaseInitialized = false;

    private void Start()
    {
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                firebaseInitialized = true;
                Debug.Log("Firebase initialized successfully");
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
                UpdateStatus("Failed to initialize Firebase services");
            }
        });
    }

    public void OnLoginButtonClicked()
{
    if (!firebaseInitialized)
    {
        UpdateStatus("Firebase not initialized. Please try again.");
        InitializeFirebase();
        return;
    }

    UpdateStatus("Starting Google Sign-In...");
    GoogleSignIn.Configuration = new GoogleSignInConfiguration
    {
        WebClientId = webClientId,
        RequestIdToken = true,
        RequestEmail = true,
        RequestAuthCode = true,
        RequestProfile = true,  // Add profile info request
        UseGameSignIn = false   // Use web authentication flow
    };

    try
    {
        Task<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn();
        signIn.ContinueWith(HandleGoogleSignIn);
    }
    catch (Exception ex)
    {
        UpdateStatus("Google Sign-In Error: " + ex.Message);
        Debug.LogException(ex);
    }
}

    private void HandleGoogleSignIn(Task<GoogleSignInUser> task)
{
    if (task.IsCanceled)
    {
        UpdateStatus("Google Sign-In Cancelled");
        return;
    }
    if (task.IsFaulted)
    {
        UpdateStatus("Google Sign-In Failed: " + task.Exception?.Flatten().Message);
        return;
    }

    try
    {
        GoogleSignInUser googleUser = task.Result;
        if (googleUser != null && !string.IsNullOrEmpty(googleUser.AuthCode))
        {
            string authCode = googleUser.AuthCode;
            
            // Add debug information
            Debug.Log($"Auth Code: {(authCode.Length > 10 ? authCode.Substring(0, 10) + "..." : authCode)}");
            Debug.Log($"ID Token: {(googleUser.IdToken?.Length > 10 ? googleUser.IdToken.Substring(0, 10) + "..." : googleUser.IdToken)}");
            Debug.Log($"Email: {googleUser.Email}");
            
            UpdateStatus("Google Sign-In Success: " + googleUser.Email);

            // Send AuthCode to backend
            StartCoroutine(CheckUserInDatabase(authCode));
        }
        else
        {
            UpdateStatus("Google Sign-In Failed: Missing AuthCode");
            Debug.LogError("Google Sign-In returned null user or empty AuthCode");
        }
    }
    catch (Exception ex)
    {
        UpdateStatus("Google Sign-In Error: " + ex.Message);
        Debug.LogException(ex);
    }
}

    private IEnumerator CheckUserInDatabase(string authCode)
{
    UpdateStatus("Verifying with server...");

    var requestData = new AuthCodeRequest { code = authCode };
    string jsonData = JsonUtility.ToJson(requestData);
    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

    using (UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST"))
    {
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 30; // Increased timeout to 30 seconds

        Debug.Log($"Sending request to: {databaseCheckUrl}");
        Debug.Log($"Request data: {jsonData}");

        yield return request.SendWebRequest();

        string rawResponse = request.downloadHandler?.text ?? "null";
        Debug.Log($"Raw API Response: {rawResponse}");

        if (request.result != UnityWebRequest.Result.Success)
        {
            UpdateStatus($"Server Error: {request.error}");
            Debug.LogError($"Request failed: {request.error}");
            Debug.LogError($"Response code: {request.responseCode}");
            Debug.LogError($"Response body: {rawResponse}");
            yield break;
        }

        try
        {
            var response = JsonUtility.FromJson<BackendResponse>(rawResponse);

            if (response != null && response.success)
            {
                Debug.Log("✅ Login successful for " + response.user.email);

                // Save user data
                PlayerPrefs.SetString("AuthToken", response.token);
                PlayerPrefs.SetString("UserEmail", response.user.email);
                if (!string.IsNullOrEmpty(response.user.studentId))
                    PlayerPrefs.SetString("StudentId", response.user.studentId);
                PlayerPrefs.Save();

                // Save user data to StudentIdManager
                StudentIdManager.Instance.SaveUserDataFromGoogleAuth(
                    response.user.email,
                    response.user.studentId ?? response.user.id,
                    response.token
                );

                UpdateStatus("Login successful! Loading...");
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                string errorMsg = response?.message ?? "Authentication failed";
                UpdateStatus("❌ " + errorMsg);
                Debug.LogError($"Authentication failed: {errorMsg}");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus("Error processing server response");
            Debug.LogException(ex);
            Debug.LogError($"Raw response that caused error: {rawResponse}");
        }
    }
}

    private void UpdateStatus(string message)
    {
        Debug.Log($"[GoogleLogin] {message}");
        if (statusText != null) statusText.text = message;
    }

    // === Request/Response Models ===
    [Serializable]
    private class AuthCodeRequest
    {
        public string code;
    }

    [Serializable]
    private class BackendResponse
    {
        public bool success;
        public string message;
        public string token;
        public UserData user;
    }

    [Serializable]
    private class UserData
    {
        public string id;
        public string email;
        public string name;
        public string[] roles;
        public string profilePhotoUrl;
        public string studentId;
        public bool isStudent;
    }
}
