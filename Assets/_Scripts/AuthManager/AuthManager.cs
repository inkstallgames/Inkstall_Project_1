using Firebase;
using Firebase.Auth;
using Google;
using System;
using System.Collections;
using System.Threading.Tasks; 
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Firebase.Extensions;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }
    public TMP_Text statusText;
    public FirebaseUser CurrentUser { get; private set; }
    
    private FirebaseAuth auth;
    private string webClientId = "187710511438-jej75f8qn7k8c2h4md576e1cktuaqgb1.apps.googleusercontent.com"; // Replace with your Web Client ID
    private string apiEndpoint = "https://api.inkstall.com/api/auth/student/google-login"; // Replace with your actual API endpoint

    private void Awake()
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

    void Start()
    {
        InitializeFirebase();
    }

    void InitializeFirebase()
    {
        Debug.Log("Initializing Firebase...");
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            Debug.Log("Dependency check complete");
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                Debug.Log("Firebase initialized successfully");
                
                if (auth != null)
                {
                    Debug.Log("Firebase Auth is ready");
                    if (statusText) statusText.text = "Ready to sign in";
                }
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
                if (statusText) statusText.text = "Failed to initialize authentication.";
            }
        });
    }

    public void SignInWithGoogle()
    {
        Debug.Log("Button clicked!");
        if (statusText) statusText.text = "Button clicked!";
        
        if (auth == null)
        {
            Debug.LogError("Firebase Auth not initialized");
            if (statusText) statusText.text = "Authentication not ready. Please try again.";
            return;
        }

        if (statusText) statusText.text = "Signing in...";
        
        try
        {
            // Configure Google Sign-In
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                WebClientId = webClientId,
                RequestIdToken = true
            };
            
            // Start the Google Sign-In process
            GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(OnGoogleSignInCompleted);
        }
        catch (Exception e)
        {
            Debug.LogError("Google Sign-In error: " + e);
            if (statusText) statusText.text = "Sign in failed: " + e.Message;
        }
    }
    
    private void OnGoogleSignInCompleted(Task<GoogleSignInUser> task)
    {
        if (task.IsCanceled)
        {
            Debug.LogError("Google Sign-In was canceled");
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (statusText) statusText.text = "Sign in was canceled";
            });
            return;
        }
        
        if (task.IsFaulted)
        {
            Debug.LogError("Google Sign-In encountered an error: " + task.Exception);
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                if (statusText) statusText.text = "Sign in failed";
            });
            return;
        }
        
        // Google Sign-In completed successfully
        GoogleSignInUser googleUser = task.Result;
        Debug.Log("Google Sign-In completed, now authenticating with Firebase");
        
        // Get credential for Firebase
        Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
        
        // Sign in to Firebase with the Google credential
        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask => {
    if (authTask.IsCanceled)
    {
        Debug.LogError("Firebase sign-in was canceled");
        if (statusText) statusText.text = "Authentication was canceled";
        return;
    }
    
    if (authTask.IsFaulted)
    {
        Debug.LogError("Firebase sign-in encountered an error: " + authTask.Exception);
        if (statusText) statusText.text = "Authentication failed";
        return;
    }
    
    // Firebase sign-in completed successfully
    CurrentUser = authTask.Result;
    Debug.Log("Firebase sign-in completed successfully: " + CurrentUser.Email);
    
    // Check if user exists in your database - no need for dispatcher since we're on main thread
    StartCoroutine(CheckUserInDatabase(CurrentUser.Email));
});

    }
    
    private IEnumerator CheckUserInDatabase(string email)
    {
        if (statusText) statusText.text = "Checking registration...";
        
        WWWForm form = new WWWForm();
        form.AddField("email", email);
        
        using (UnityWebRequest www = UnityWebRequest.Post(apiEndpoint, form))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    UserCheckResponse response = JsonUtility.FromJson<UserCheckResponse>(www.downloadHandler.text);
                    
                    if (response.exists)
                    {
                        // User exists in database
                        Debug.Log("User found in database: " + response.userData.userId);
                        if (statusText) statusText.text = "Login successful!";
                        
                        // Save user data
                        PlayerPrefs.SetString("UserID", response.userData.userId);
                        PlayerPrefs.SetString("Email", email);
                        PlayerPrefs.SetInt("Keys", response.userData.keys);
                        PlayerPrefs.SetInt("Points", response.userData.points);
                        PlayerPrefs.Save();
                        
                        // Load game scene
                        SceneManager.LoadScene("MainGameScene");
                    }
                    else
                    {
                        // User not found in database
                        Debug.Log("User not found in database");
                        if (statusText) statusText.text = "Not Registered User";
                        SignOut();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Error parsing response: " + e);
                    if (statusText) statusText.text = "Error processing server response";
                }
            }
            else
            {
                Debug.LogError("Error connecting to server: " + www.error);
                if (statusText) statusText.text = "Connection error. Please try again.";
            }
        }
    }
    
    public void SignOut()
    {
        if (auth != null)
        {
            auth.SignOut();
            CurrentUser = null;
        }
        
        GoogleSignIn.DefaultInstance.SignOut();
        
        PlayerPrefs.DeleteKey("UserID");
        PlayerPrefs.DeleteKey("Email");
        PlayerPrefs.DeleteKey("Keys");
        PlayerPrefs.DeleteKey("Points");
        
        if (statusText) statusText.text = "Signed out";
    }
}
