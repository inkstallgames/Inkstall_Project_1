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
    [SerializeField] private string nextSceneName = "Lobby";

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
        RequestAuthCode = false,   //no need for AuthCode anymore
        RequestProfile = true,
        UseGameSignIn = false
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
        // Reset sign-in state
        GoogleSignIn.DefaultInstance.SignOut();
        return;
    }
    if (task.IsFaulted)
    {
        // Handle specific exceptions
        var exception = task.Exception?.Flatten().InnerException;
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
        GoogleSignInUser googleUser = task.Result;
        if (googleUser != null && !string.IsNullOrEmpty(googleUser.IdToken))
        {
            string idToken = googleUser.IdToken;

            Debug.Log($"ID Token: {(idToken.Length > 10 ? idToken.Substring(0, 10) + "..." : idToken)}");
            Debug.Log($"Email: {googleUser.Email}");

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
    UpdateStatus("Verifying with server...");

    var requestData = new IdTokenRequest { idToken = idToken };
    string jsonData = JsonUtility.ToJson(requestData);
    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

    using (UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST"))
    {
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 30;

        Debug.Log($"Sending request to: {databaseCheckUrl}");
        Debug.Log($"Request data: {jsonData}");

        yield return request.SendWebRequest();

        string rawResponse = request.downloadHandler?.text ?? "null";
        Debug.Log($"Raw API Response: {rawResponse}");

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
            var response = JsonUtility.FromJson<BackendResponse>(rawResponse);

            if (response != null && response.success)
            {
                Debug.Log("Login successful for " + response.user.email);

                // Save user data for persistence
                PlayerPrefs.SetString("AuthToken", response.token);
                PlayerPrefs.SetString("UserEmail", response.user.email);
                if (!string.IsNullOrEmpty(response.user.studentId))
                    PlayerPrefs.SetString("StudentId", response.user.studentId);
                PlayerPrefs.Save();

                StudentIdManager.Instance.SaveUserDataFromGoogleAuth(
                    response.user.email,
                    response.user.studentId ?? response.user.id,
                    response.token
                );

                UpdateStatus("Login successful! Loading...");
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
