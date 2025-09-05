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
        "383598510964-51l74fgp8q3cqcqo8upqvlrndpvet7h8.apps.googleusercontent.com";

    [Header("Backend")]
    [SerializeField]
    private string databaseCheckUrl = "https://api.inkstall.in/api/auth/unity-login";

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
                // Try parsing as the expected response format
                var response = JsonUtility.FromJson<DatabaseResponse>(rawResponse);
                if (response != null && response.registered)
                {
                    // Save login data to GameDataManager
                    GameDataManager.Instance.SaveLoginData(
                        response.studentId,
                        email,
                        googleId
                    );

                    // Log success
                    Debug.Log($"Login successful. Student ID: {response.studentId}");
                    
                    // Update Status & Load next scene
                    UpdateStatus("Login successful! Loading...");
                    SceneManager.LoadScene(nextSceneName);
                }
                else if (response != null)
                {
                    UpdateStatus("❌ Not a registered user");
                    Debug.LogWarning("User not registered in database");
                }
                else
                {
                    // Try parsing as alternative response format (mobile endpoint)
                    try {
                        var mobileResponse = JsonUtility.FromJson<MobileLoginResponse>(rawResponse);
                        if (mobileResponse != null && mobileResponse.success)
                        {
                            string studentId = mobileResponse.data.studentId;
                            
                            // Save login data
                            GameDataManager.Instance.SaveLoginData(
                                studentId,
                                email,
                                googleId
                            );
                            
                            // Save JWT token if provided
                            if (!string.IsNullOrEmpty(mobileResponse.token)) {
                                PlayerPrefs.SetString("AuthToken", mobileResponse.token);
                                PlayerPrefs.Save();
                            }
                            
                            // Log success
                            Debug.Log($"Login successful via mobile endpoint. Student ID: {studentId}");
                            
                            // Update Status & Load next scene
                            UpdateStatus("Login successful! Loading...");
                            SceneManager.LoadScene(nextSceneName);
                        }
                        else
                        {
                            UpdateStatus("❌ Authentication failed");
                            Debug.LogError("Mobile authentication failed: " + (mobileResponse?.message ?? "Unknown error"));
                        }
                    }
                    catch (Exception ex) {
                        UpdateStatus("Error processing server response");
                        Debug.LogException(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("Error processing server response");
                Debug.LogException(ex);
            }
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
        public string studentId;
        public string name;
    }

    [System.Serializable]
    private class ErrorResponse
    {
        public string error;
        public string message;
    }

    // New response class for mobile endpoint
    [System.Serializable]
    private class MobileLoginResponse
    {
        public bool success;
        public string message;
        public UserData data;
        public string token;
    }

    [System.Serializable]
    private class UserData
    {
        public string _id;
        public string email;
        public string fullName;
        public string profilePhotoUrl;
        public string[] roles;
        public string studentId;
        public bool isStudent;
    }
}
