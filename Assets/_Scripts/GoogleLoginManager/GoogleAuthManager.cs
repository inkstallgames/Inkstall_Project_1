using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using Firebase.Auth;
using UnityEngine.UI;
using TMPro;
using Google;
using System.Net.Http;
using UnityEngine.Networking;

public class GoogleAuthManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text UsernameTxt;
    public TMP_Text UserEmailTxt;
    public GameObject LoginScreen;
    public GameObject ProfileScreen;
    public Image userProfilePic;
    public TMP_Text statusText;
    public Button signInButton;

    [Header("API Settings")]
    public string GoogleWebAPI = "187710511438-f3f88n5kp87lui3gvpj332lmu32h1389.apps.googleusercontent.com";
    [SerializeField] private string databaseCheckUrl = "https://api.inkstall.in/api/auth/student/google-login";

    private GoogleSignInConfiguration config;
    private Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    private Firebase.Auth.FirebaseAuth auth;
    private Firebase.Auth.FirebaseUser user;

    private async void Awake()
    {
        Debug.Log("[GoogleAuth] Starting initialization...");

        // Initialize Firebase
        var dependencyStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus == Firebase.DependencyStatus.Available)
        {
            auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            Debug.Log("[GoogleAuth] Firebase initialized successfully");
        }
        else
        {
            Debug.LogError($"[GoogleAuth] Could not resolve all Firebase dependencies: {dependencyStatus}");
            return;
        }

        // Then your existing Google Sign-In config
        try
        {
            config = new GoogleSignInConfiguration()
            {
                WebClientId = GoogleWebAPI,
                RequestIdToken = true,
                RequestEmail = true,
                RequestProfile = true,
                UseGameSignIn = false,
                ForceTokenRefresh = true
            };
            Debug.Log("[GoogleAuth] Google Sign-In configuration created");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error creating config: {e.Message}");
        }
    }

    private void Start()
    {
        try
        {
            signInButton.onClick.AddListener(SignInWithGoogle);
            InitializeFirebase();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in Start: {e.Message}\n{e.StackTrace}");
        }
    }

    private void InitializeFirebase()
    {
        Debug.Log("[GoogleAuth] InitializeFirebase: Starting Firebase initialization");
        try
        {
            // Check Firebase dependencies
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    Debug.Log("[GoogleAuth] Firebase dependencies resolved successfully");
                    auth = FirebaseAuth.DefaultInstance;

                    // Check if user is already logged in
                    if (auth.CurrentUser != null)
                    {
                        Debug.Log($"[GoogleAuth] User already logged in: {auth.CurrentUser.Email}");
                        user = auth.CurrentUser;
                        UpdateUIWithUserInfo();
                    }
                    else
                    {
                        Debug.Log("[GoogleAuth] No user currently logged in");
                    }
                }
                else
                {
                    Debug.LogError($"[GoogleAuth] Could not resolve Firebase dependencies: {dependencyStatus}");
                    UpdateStatus($"Firebase initialization failed: {dependencyStatus}");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in InitializeFirebase: {e.Message}\n{e.StackTrace}");
        }
    }

    public void SignInWithGoogle()
    {
        Debug.Log("[GoogleAuth] Starting Google Sign-In...");
        UpdateStatus("Starting Google Sign-In...");

#if UNITY_EDITOR
        // Simulate a successful login in the editor
        Debug.LogWarning("[GoogleAuth] Running in Unity Editor - using test credentials");
        var testEmail = "lauren@inkatall.com";
        Debug.Log($"[GoogleAuth] Using test email: {testEmail}");
        
        // Skip Google Sign-In and directly check database
        CheckUserInDatabase(testEmail);
        return;
#endif

        try 
        {
            if (auth == null)
            {
                Debug.LogError("[GoogleAuth] Firebase Auth not initialized!");
                UpdateStatus("Authentication service not ready");
                return;
            }

            // First sign out to clear any previous state and force account picker
            try {
                GoogleSignIn.DefaultInstance.SignOut();
                Debug.Log("[GoogleAuth] Successfully signed out previous Google session");
            }
            catch (Exception e) {
                Debug.LogWarning($"[GoogleAuth] Error during sign out (non-critical): {e.Message}");
                // Continue with sign-in even if sign-out fails
            }
            
            // Configure with correct settings
            GoogleSignInConfiguration gsc = new GoogleSignInConfiguration
            {
                WebClientId = GoogleWebAPI,
                RequestIdToken = true,
                RequestEmail = true,
                RequestProfile = true,
                UseGameSignIn = false,
                ForceTokenRefresh = true
            };
            
            GoogleSignIn.Configuration = gsc;
            
            Debug.Log($"[GoogleAuth] Using WebClientId: {GoogleWebAPI}");
            Debug.Log("[GoogleAuth] Configured Google Sign-In with ForceTokenRefresh=true");
            
            // This will show the account picker
            GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(task => {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[GoogleAuth] Sign-in error: {task.Exception}");
                    // Add detailed error logging
                    if (task.Exception != null)
                    {
                        foreach (var ex in task.Exception.Flatten().InnerExceptions)
                        {
                            string errorDetails = "";
                            
                            // Check for specific Google Sign-In errors
                            if (ex is GoogleSignIn.SignInException signInException)
                            {
                                errorDetails = $"Status code: {signInException.Status}";
                                
                                // Common error codes and troubleshooting advice
                                switch (signInException.Status)
                                {
                                    case GoogleSignInStatusCode.DeveloperError:
                                        errorDetails += " - Check SHA-1 fingerprint in Firebase console";
                                        break;
                                    case GoogleSignInStatusCode.NetworkError:
                                        errorDetails += " - Check internet connection";
                                        break;
                                    case GoogleSignInStatusCode.Canceled:
                                        errorDetails += " - User canceled sign-in";
                                        break;
                                    case GoogleSignInStatusCode.ApiNotConnected:
                                        errorDetails += " - Google Play Services not available";
                                        break;
                                }
                            }
                            
                            Debug.LogError($"[GoogleAuth] Detailed error: {ex.GetType().Name}: {ex.Message} {errorDetails}");
                            Debug.LogError($"[GoogleAuth] Stack trace: {ex.StackTrace}");
                        }
                    }
                    UpdateStatus("Sign-in failed. Please try again.");
                    return;
                }
                
                if (task.IsCanceled)
                {
                    Debug.LogWarning("[GoogleAuth] Sign-in was canceled");
                    UpdateStatus("Sign-in canceled");
                    return;
                }
                
                var googleUser = task.Result;
                Debug.Log($"[GoogleAuth] Google user: {googleUser.DisplayName} ({googleUser.Email})");
                
                // Continue with Firebase auth
                var credential = Firebase.Auth.GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
                auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask => {
                    if (authTask.IsCanceled || authTask.IsFaulted)
                    {
                        Debug.LogError($"[GoogleAuth] Firebase auth failed: {authTask.Exception}");
                        UpdateStatus("Authentication failed");
                        return;
                    }
                    
                    user = authTask.Result;
                    Debug.Log($"[GoogleAuth] Firebase user: {user.DisplayName}");
                    CheckUserInDatabase(user.Email);
                });
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error: {e.Message}");
            UpdateStatus("Error during sign-in");
        }
    }

    void OnGoogleAuthenticatedFinished(Task<GoogleSignInUser> task)
    {
        Debug.Log("[GoogleAuth] OnGoogleAuthenticatedFinished: Google authentication completed");

        try
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[GoogleAuth] Google Sign-In failed: {task.Exception}");
                if (task.Exception != null)
                {
                    foreach (var exception in task.Exception.Flatten().InnerExceptions)
                    {
                        Debug.LogError($"[GoogleAuth] Inner exception: {exception.Message}");
                    }
                }
                UpdateStatus("Google Sign-In failed. Please try again.");
                return;
            }

            if (task.IsCanceled)
            {
                Debug.LogWarning("[GoogleAuth] Google Sign-In was canceled by user");
                UpdateStatus("Sign-in was canceled");
                return;
            }

            var googleUser = task.Result;
            Debug.Log($"[GoogleAuth] Google user authenticated: {googleUser.DisplayName} ({googleUser.Email})");
            Debug.Log($"[GoogleAuth] ID Token length: {googleUser.IdToken?.Length ?? 0}");

            // Create Firebase credential
            var credential = Firebase.Auth.GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
            Debug.Log("[GoogleAuth] Firebase credential created, signing in to Firebase");

            // Sign in to Firebase with Google credential
            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
            {
                if (authTask.IsCanceled)
                {
                    Debug.LogError("[GoogleAuth] Firebase sign-in was canceled");
                    UpdateStatus("Sign-in was canceled");
                    return;
                }

                if (authTask.IsFaulted)
                {
                    Debug.LogError($"[GoogleAuth] Firebase sign-in failed: {authTask.Exception}");
                    if (authTask.Exception != null)
                    {
                        foreach (var exception in authTask.Exception.Flatten().InnerExceptions)
                        {
                            Debug.LogError($"[GoogleAuth] Inner exception: {exception.Message}");
                        }
                    }
                    UpdateStatus("Authentication failed. Please try again.");
                    return;
                }

                user = auth.CurrentUser;
                if (user != null)
                {
                    Debug.Log($"[GoogleAuth] Firebase user logged in: {user.DisplayName} ({user.Email})");
                    Debug.Log($"[GoogleAuth] User ID: {user.UserId}");

                    // Check if user exists in your database
                    CheckUserInDatabase(user.Email);
                }
                else
                {
                    Debug.LogError("[GoogleAuth] User is null after successful authentication");
                    UpdateStatus("Authentication error. Please try again.");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in OnGoogleAuthenticatedFinished: {e.Message}\n{e.StackTrace}");
            UpdateStatus("Authentication error. Please try again.");
        }
    }

    private void CheckUserInDatabase(string email)
    {
        Debug.Log($"[GoogleAuth] CheckUserInDatabase: Checking if email {email} exists in database");
        UpdateStatus("Verifying account...");

#if UNITY_EDITOR
    Debug.Log($"[GoogleAuth] Editor mode - simulating database check for {email}");
    // Simulate a successful database check in the editor
    UpdateStatus("Account verified successfully!");
    UpdateUIWithUserInfo();
    InitializeGameWithUserData("editor_test_user_id");
    return;
#endif

        StartCoroutine(CheckUserInDatabaseCoroutine(email));
    }

    private IEnumerator CheckUserInDatabaseCoroutine(string email)
    {
        // Create request to check if user exists in your database
        string jsonData = JsonUtility.ToJson(new EmailCheckRequest { email = email });

        UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log($"[GoogleAuth] Sending request to {databaseCheckUrl}");
        yield return request.SendWebRequest();

        try
        {
            Debug.Log($"[GoogleAuth] Request completed with status: {request.result}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[GoogleAuth] Database check response: {request.downloadHandler.text}");
                // Remember: MongoDB JSON needs special parsing
                try
                {
                    var response = JsonUtility.FromJson<DatabaseCheckResponse>(request.downloadHandler.text);

                    if (response.exists)
                    {
                        Debug.Log($"[GoogleAuth] User exists in database with ID: {response.userId}");
                        UpdateStatus("Account verified successfully!");

                        UpdateUIWithUserInfo();
                        InitializeGameWithUserData(response.userId);
                    }
                    else
                    {
                        Debug.LogWarning("[GoogleAuth] User does not exist in database");
                        UpdateStatus("Account not found. Please register on our website first.");
                        auth.SignOut();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GoogleAuth] JSON parsing error: {e.Message}");
                    UpdateStatus("Error processing server response");
                }
            }
            else
            {
                Debug.LogError($"[GoogleAuth] Database check failed: {request.error}");
                UpdateStatus("Could not verify account. Please try again.");
            }
        }
        finally
        {
            request.Dispose();
        }
    }

    private void UpdateUIWithUserInfo()
    {
        Debug.Log("[GoogleAuth] UpdateUIWithUserInfo: Updating UI with user info");
        try
        {
            if (user != null)
            {
                UsernameTxt.text = user.DisplayName;
                UserEmailTxt.text = user.Email;

                LoginScreen.SetActive(false);
                ProfileScreen.SetActive(true);

                if (user.PhotoUrl != null)
                {
                    Debug.Log($"[GoogleAuth] Loading profile picture from: {user.PhotoUrl}");
                    StartCoroutine(LoadImage(CheckImageUrl(user.PhotoUrl.ToString())));
                }
                else
                {
                    Debug.Log("[GoogleAuth] No profile picture URL available");
                }
            }
            else
            {
                Debug.LogError("[GoogleAuth] Cannot update UI: User is null");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in UpdateUIWithUserInfo: {e.Message}\n{e.StackTrace}");
        }
    }

    private void InitializeGameWithUserData(string userId)
    {
        Debug.Log($"[GoogleAuth] InitializeGameWithUserData: Initializing game with user ID: {userId}");
        try
        {
            // Initialize your game managers with the user ID
            if (KeyManager.Instance != null)
            {
                Debug.Log($"[GoogleAuth] Setting KeyManager.studentId to {userId}");
                KeyManager.Instance.studentId = userId;
                KeyManager.Instance.FetchKeysFromDB();
            }
            else
            {
                Debug.LogWarning("[GoogleAuth] KeyManager.Instance is null");
            }

            if (CoinsManager.Instance != null)
            {
                Debug.Log($"[GoogleAuth] Setting CoinsManager.studentId to {userId}");
                CoinsManager.Instance.userId = userId;
                CoinsManager.Instance.FetchCoins();
            }
            else
            {
                Debug.LogWarning("[GoogleAuth] CoinsManager.Instance is null");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in InitializeGameWithUserData: {e.Message}\n{e.StackTrace}");
        }
    }

    private string CheckImageUrl(string url)
    {
        Debug.Log($"[GoogleAuth] CheckImageUrl: Checking URL: {url}");
        return string.IsNullOrEmpty(url) ? "" : url;
    }

    private IEnumerator LoadImage(string imageUri)
    {
        if (string.IsNullOrEmpty(imageUri)) yield break;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUri))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[GoogleAuth] Error loading image: " + request.error);
            }
            else
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(request);
                userProfilePic.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
    }


    private void UpdateStatus(string message)
    {
        Debug.Log($"[GoogleAuth] Status: {message}");
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    // Helper classes for JSON serialization
    [Serializable]
    private class EmailCheckRequest
    {
        public string email;
    }

    [Serializable]
    private class DatabaseCheckResponse
    {
        public bool exists;
        public string userId;
        public string message;
    }
}