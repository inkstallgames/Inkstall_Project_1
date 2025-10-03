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
        Debug.Log("[GoogleLogin] Starting LoginWithGoogle script");
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        Debug.Log("[GoogleLogin] Initializing Firebase...");
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
        Debug.Log("[GoogleLogin] Login button clicked");
        if (!firebaseInitialized)
    {
        UpdateStatus("Firebase not initialized. Please try again.");
        InitializeFirebase();
        return;
    }

    UpdateStatus("Starting Google Sign-In...");
    Debug.Log("[GoogleLogin] Configuring GoogleSignIn");
    GoogleSignIn.Configuration = new GoogleSignInConfiguration
    {
        WebClientId = webClientId,
        RequestIdToken = true,
        RequestEmail = true,
        RequestAuthCode = false,   //no need for AuthCode anymore
        RequestProfile = true,
        UseGameSignIn = false
    };

    try
    {
        Debug.Log("[GoogleLogin] Calling GoogleSignIn.DefaultInstance.SignIn()");
        Task<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn();
        Debug.Log("[GoogleLogin] SignIn task created, attaching continuation");
        signIn.ContinueWith(HandleGoogleSignIn);
        Debug.Log("[GoogleLogin] Continuation attached to SignIn task");
    }
    catch (Exception ex)
    {
        UpdateStatus("Google Sign-In Error: " + ex.Message);
        Debug.LogException(ex);
    }
}

private void HandleGoogleSignIn(Task<GoogleSignInUser> task)
{
    Debug.Log("[GoogleLogin] HandleGoogleSignIn called");
    Debug.Log($"[GoogleLogin] Task status: IsCanceled={task.IsCanceled}, IsFaulted={task.IsFaulted}, IsCompleted={task.IsCompleted}");
    
    if (task.IsCanceled)
    {
        UpdateStatus("Google Sign-In Cancelled");
        Debug.Log("[GoogleLogin] Google Sign-In was canceled by user");
        // Reset sign-in state
        Debug.Log("[GoogleLogin] Calling GoogleSignIn.DefaultInstance.SignOut()");
        GoogleSignIn.DefaultInstance.SignOut();
        return;
    }
    if (task.IsFaulted)
    {
        Debug.Log("[GoogleLogin] Google Sign-In task faulted");
        // Handle specific exceptions
        var exception = task.Exception?.Flatten().InnerException;
        Debug.LogError($"[GoogleLogin] Exception details: {exception?.GetType().FullName}: {exception?.Message}");
        if (exception?.StackTrace != null)
            Debug.LogError($"[GoogleLogin] Stack trace: {exception.StackTrace}");
        if (exception != null && exception.GetType().ToString().Contains("GoogleSignIn"))
        {
            UpdateStatus("Google Sign-In Cancelled");
        }
        else
        {
            UpdateStatus("Ready to sign in");
            Debug.LogError("Google Sign-In Error: " + exception?.Message);
        }
        
        // Reset sign-in state
        GoogleSignIn.DefaultInstance.SignOut();
        return;
    }

    try
    {
        Debug.Log("[GoogleLogin] Attempting to get task.Result");
        GoogleSignInUser googleUser = task.Result;
        Debug.Log("[GoogleLogin] Successfully got GoogleSignInUser result");
        
        if (googleUser != null && !string.IsNullOrEmpty(googleUser.IdToken))
        {
            string idToken = googleUser.IdToken;
            
            Debug.Log($"[GoogleLogin] ID Token received (length: {idToken.Length}): {(idToken.Length > 10 ? idToken.Substring(0, 10) + "..." : idToken)}");
            Debug.Log($"[GoogleLogin] Email: {googleUser.Email}");
            Debug.Log($"[GoogleLogin] Display Name: {googleUser.DisplayName}");
            Debug.Log($"[GoogleLogin] Authentication successful, proceeding with server verification");

            UpdateStatus("Google Sign-In Success: " + googleUser.Email);

            // Send IdToken (not AuthCode) to backend
            StartCoroutine(CheckUserInDatabase(idToken));
        }
        else
        {
            UpdateStatus("Google Sign-In Failed: Missing IdToken");
            Debug.LogError("Google Sign-In returned null user or empty IdToken");
        }
    }
    catch (Exception ex)
    {
        UpdateStatus("Google Sign-In Error: " + ex.Message);
        Debug.LogException(ex);
    }
}

private IEnumerator CheckUserInDatabase(string idToken)
{
    Debug.Log("[GoogleLogin] Starting CheckUserInDatabase");
    UpdateStatus("Verifying with server...");

    Debug.Log("[GoogleLogin] Preparing request data for server verification");
    var requestData = new IdTokenRequest { idToken = idToken };
    string jsonData = JsonUtility.ToJson(requestData);
    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
    Debug.Log($"[GoogleLogin] Request data prepared, JSON length: {jsonData.Length}");

    Debug.Log($"[GoogleLogin] Creating UnityWebRequest to URL: {databaseCheckUrl}");
    using (UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST"))
    {
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 30;

        Debug.Log($"Sending request to: {databaseCheckUrl}");
        Debug.Log($"Request data: {jsonData}");

        Debug.Log("[GoogleLogin] Sending web request to server...");
        yield return request.SendWebRequest();
        Debug.Log("[GoogleLogin] Web request completed");

        string rawResponse = request.downloadHandler?.text ?? "null";
        Debug.Log($"Raw API Response: {rawResponse}");

        Debug.Log($"[GoogleLogin] Request result: {request.result}, Response code: {request.responseCode}");
        if (request.result != UnityWebRequest.Result.Success)
        {
            if (request.responseCode == 404 || request.responseCode == 401)
            {
                UpdateStatus("Student Not Registered!");
                // Sign out from Google to allow account picker to appear again
                GoogleSignIn.DefaultInstance.SignOut();
            }
            else
            {
                UpdateStatus($"Server Error: {request.error}");
                Debug.LogError($"Request failed: {request.error}");
                Debug.LogError($"Response code: {request.responseCode}");
                Debug.LogError($"Response body: {rawResponse}");
            }
            yield break;
        }

        try
        {
            Debug.Log("[GoogleLogin] Parsing server response JSON");
            var response = JsonUtility.FromJson<BackendResponse>(rawResponse);
            Debug.Log($"[GoogleLogin] Response parsed, success: {response?.success}, message: {response?.message}");

            if (response != null && response.success)
            {
                Debug.Log("[GoogleLogin] Authentication successful with backend");
                Debug.Log("Login successful for " + response.user.email);

                // Save user data for persistence
                PlayerPrefs.SetString("AuthToken", response.token);
                PlayerPrefs.SetString("UserEmail", response.user.email);
                if (!string.IsNullOrEmpty(response.user.studentId))
                    PlayerPrefs.SetString("StudentId", response.user.studentId);
                PlayerPrefs.Save();

                Debug.Log("[GoogleLogin] Saving user data to StudentIdManager");
                StudentIdManager.Instance.SaveUserDataFromGoogleAuth(
                    response.user.email,
                    response.user.studentId ?? response.user.id,
                    response.token
                );
                Debug.Log("[GoogleLogin] User data saved successfully");

                UpdateStatus("Login successful! Loading...");
                Debug.Log("[GoogleLogin] Login successful, will load scene in 2 seconds");
                Invoke("LoadScene", 2f);
            }
            else
            {
                UpdateStatus("Student Not Registered!");
                Debug.LogError($"Authentication failed: {response?.message ?? "User not found"}");
                // Sign out from Google to allow account picker to appear again
                GoogleSignIn.DefaultInstance.SignOut();
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

private void LoadScene()
{
    Debug.Log($"[GoogleLogin] Loading scene: {nextSceneName}");
    SceneManager.LoadScene(nextSceneName);
}

// === Request/Response Models ===
[Serializable]
private class IdTokenRequest
{
    public string idToken;
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

private void UpdateStatus(string message)
{
    Debug.Log($"[GoogleLogin] {message}");
    if (statusText != null) statusText.text = message;
}
}
