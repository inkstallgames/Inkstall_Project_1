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
    public string GoogleWebAPI = "187710511438-jej75f8qn7k8c2h4md576e1cktuaqgb1.apps.googleusercontent.com";
    [SerializeField] private string databaseCheckUrl = "https://api.inkstall.com/api/student-portal/auth";

    private GoogleSignInConfiguration config;
    private Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    private Firebase.Auth.FirebaseAuth auth;
    private Firebase.Auth.FirebaseUser user;

    private void Awake()
    {
        Debug.Log("[GoogleAuth] Awake: Initializing Google Sign-In configuration");
        try
        {
            config = new GoogleSignInConfiguration()
            {
                WebClientId = GoogleWebAPI,
                RequestIdToken = true,
                RequestEmail = true
            };
            Debug.Log("[GoogleAuth] Configuration created successfully with WebClientId: " + GoogleWebAPI);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in Awake: {e.Message}\n{e.StackTrace}");
        }
    }

    private void Start()
    {
        Debug.Log("[GoogleAuth] Start: Setting up button listener and initializing Firebase");
        try
        {
            signInButton.onClick.AddListener(SignInWithGoogle);
            Debug.Log("[GoogleAuth] Sign in button listener added");
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
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
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

    private void SignInWithGoogle()
    {
        Debug.Log("[GoogleAuth] SignInWithGoogle: Starting Google Sign-In process");
        UpdateStatus("Starting Google Sign-In...");
        
        try
        {
            GoogleSignIn.Configuration = config;
            GoogleSignIn.Configuration.UseGameSignIn = false;
            GoogleSignIn.Configuration.RequestIdToken = true;
            GoogleSignIn.Configuration.RequestEmail = true;

            Debug.Log("[GoogleAuth] Configured Google Sign-In and showing account picker");
            GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleAuthenticatedFinished);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in SignInWithGoogle: {e.Message}\n{e.StackTrace}");
            UpdateStatus("Error during sign in. Please try again.");
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

    private async void CheckUserInDatabase(string email)
    {
        Debug.Log($"[GoogleAuth] CheckUserInDatabase: Checking if email {email} exists in database");
        UpdateStatus("Verifying account...");
        
        try
        {
            // Create request to check if user exists in your database
            string jsonData = JsonUtility.ToJson(new EmailCheckRequest { email = email });
            
            using (UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                Debug.Log($"[GoogleAuth] Sending request to {databaseCheckUrl}");
                request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[GoogleAuth] Database check response: {request.downloadHandler.text}");
                    var response = JsonUtility.FromJson<DatabaseCheckResponse>(request.downloadHandler.text);
                    
                    if (response.exists)
                    {
                        Debug.Log($"[GoogleAuth] User exists in database with ID: {response.userId}");
                        UpdateStatus("Account verified successfully!");
                        
                        // Update UI and initialize game with user data
                        UpdateUIWithUserInfo();
                        InitializeGameWithUserData(response.userId);
                    }
                    else
                    {
                        Debug.LogWarning("[GoogleAuth] User does not exist in database");
                        UpdateStatus("Account not found. Please register on our website first.");
                        // Optionally sign out the user
                        auth.SignOut();
                    }
                }
                else
                {
                    Debug.LogError($"[GoogleAuth] Database check failed: {request.error}");
                    UpdateStatus("Could not verify account. Please try again.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleAuth] Error in CheckUserInDatabase: {e.Message}\n{e.StackTrace}");
            UpdateStatus("Verification error. Please try again.");
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

    IEnumerator LoadImage(string imageUri)
    {
        Debug.Log($"[GoogleAuth] LoadImage: Loading image from {imageUri}");
        if (string.IsNullOrEmpty(imageUri))
        {
            Debug.LogWarning("[GoogleAuth] Image URI is empty");
            yield break;
        }

        using (WWW www = new WWW(imageUri))
        {
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Debug.LogError($"[GoogleAuth] Error loading image: {www.error}");
            }
            else
            {
                Debug.Log("[GoogleAuth] Image loaded successfully");
                userProfilePic.sprite = Sprite.Create(www.texture, new Rect(0, 0, www.texture.width, www.texture.height), new Vector2(0, 0));
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