using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Google;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text;
using System;

public class GoogleLoginManager : MonoBehaviour
{
    [Header("Google / Firebase")]
    [SerializeField]
    private string webClientId =
        "383598510964-51l74fgp8q3cqcqo8upqvlrndpvet7h8.apps.googleusercontent.com"; // use Web client ID from Google Cloud

    [Header("Backend")]
    [SerializeField]
    private string databaseCheckUrl =
        "https://api.inkstall.in/api/auth/student/google-login";

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string nextSceneName = "MainScene";

    private FirebaseAuth auth;
    private FirebaseUser user;
    private string idToken;
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
            RequestEmail = true
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
            if (googleUser != null && !string.IsNullOrEmpty(googleUser.IdToken))
            {
                idToken = googleUser.IdToken;
                UpdateStatus("Google Sign-In Success: " + googleUser.Email);

                if (auth == null)
                {
                    UpdateStatus("Firebase Auth not initialized");
                    return;
                }

                Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
                auth.SignInWithCredentialAsync(credential).ContinueWith(HandleFirebaseSignIn);
            }
            else
            {
                UpdateStatus("Google Sign-In Failed: Missing ID token");
                Debug.LogError("Google Sign-In returned null user or empty ID token");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus("Google Sign-In Error: " + ex.Message);
            Debug.LogException(ex);
        }
    }

    private void HandleFirebaseSignIn(Task<FirebaseUser> task)
    {
        if (task.IsCanceled)
        {
            UpdateStatus("Firebase Auth Cancelled");
            return;
        }
        if (task.IsFaulted)
        {
            UpdateStatus("Firebase Auth Failed: " + task.Exception?.Flatten().Message);
            return;
        }

        user = task.Result;
        if (user != null)
        {
            UpdateStatus("Firebase Auth Success: " + user.Email);
            StartCoroutine(CheckUserInDatabase(user.Email, user.UserId));
        }
        else
        {
            UpdateStatus("Firebase Auth Failed: User is null");
        }
    }

    private IEnumerator CheckUserInDatabase(string email, string googleId)
    {
        UpdateStatus("Checking user registration...");

        var requestData = new GoogleLoginRequest
        {
            email = email,
            googleId = googleId,
            idToken = idToken,
            client_id = webClientId
        };

        string jsonData = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

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

                try
                {
                    var errorResponse = JsonUtility.FromJson<ErrorResponse>(rawResponse);
                    if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error))
                    {
                        UpdateStatus($"Error: {errorResponse.error}");
                    }
                }
                catch { }

                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<DatabaseResponse>(rawResponse);
                if (response != null && response.registered)
                {
                    UpdateStatus("Login successful! Loading...");
                    SceneManager.LoadScene(nextSceneName);
                }
                else
                {
                    UpdateStatus("❌ Not a registered user");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Error processing server response");
                Debug.LogException(ex);
            }
        }

        if (response != null && response.registered)
        {
            // 1. Store login data
            GameDataManager.Instance.SaveLoginData(
            response.studentId,
            email,
            googleId
        );

            // 2. Initialize ID in KeyManager and CoinsManager
            if (KeyManager.Instance != null)
                KeyManager.Instance.studentId = response.studentId;

            if (CoinsManager.Instance != null)
                CoinsManager.Instance.userId = response.studentId;

            // 3. Update Status & Load next scene
            UpdateStatus("Loading successfull! Loading Game...");
            SceneManager.LoadScene("MainScene");
        }
    }

    private void UpdateStatus(string message)
    {
        Debug.Log($"[GoogleLogin] {message}");
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    [System.Serializable]
    private class GoogleLoginRequest
    {
        public string email;
        public string googleId;
        public string idToken;
        public string client_id;
    }

    [System.Serializable]
    private class DatabaseResponse
    {
        public bool registered;
        public string studentId;  // Ensure this matches your API response
        public string name;
    }

    [System.Serializable]
    private class ErrorResponse
    {
        public string error;
        public string message;
    }
}
