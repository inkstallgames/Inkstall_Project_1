using System.Collections;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Extensions;
using Google;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class LoginWithGoogle : MonoBehaviour
{
    public string GoogleAPI = "187710511438-jej75f8qn7k8c2h4md576e1cktuaqgb1.apps.googleusercontent.com";
    [SerializeField] private string databaseCheckUrl = "https://api.inkstall.in/api/auth/student/google-login";
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string nextSceneName = "Main"; // Name of the scene to load if user is registered
    [SerializeField] private GameObject notRegisteredPanel; // Panel to show if user is not registered
    
    private GoogleSignInConfiguration configuration;

    //Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    Firebase.Auth.FirebaseAuth auth;
    Firebase.Auth.FirebaseUser user;

    private void Awake()
    {
        configuration = new GoogleSignInConfiguration
        {
            WebClientId = GoogleAPI,
            RequestIdToken = true,
        };
        
        // // Hide the not registered panel at start
        // if (notRegisteredPanel != null)
        //     notRegisteredPanel.SetActive(false);
    }

    private void Start()
    {
        InitFirebase();
    }

    void InitFirebase()
    {
        auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
    }

    public void Login()
    {
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            RequestIdToken = true,
            // Copy this value from the google-service.json file.
            // oauth_client with type == 3
            WebClientId = GoogleAPI
        };
        GoogleSignIn.Configuration.RequestEmail = true;

        Task<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn();

        TaskCompletionSource<FirebaseUser> signInCompleted = new TaskCompletionSource<FirebaseUser>();
        signIn.ContinueWith(task => {
            if (task.IsCanceled)
            {
                signInCompleted.SetCanceled();
                Debug.Log("Cancelled");
            }
            else if (task.IsFaulted)
            {
                signInCompleted.SetException(task.Exception);
                Debug.Log("Faulted " + task.Exception);
            }
            else
            {
                Credential credential = Firebase.Auth.GoogleAuthProvider.GetCredential(((Task<GoogleSignInUser>)task).Result.IdToken, null);
                auth.SignInWithCredentialAsync(credential).ContinueWith(authTask => {
                    if (authTask.IsCanceled)
                    {
                        signInCompleted.SetCanceled();
                    }
                    else if (authTask.IsFaulted)
                    {
                        signInCompleted.SetException(authTask.Exception);
                        Debug.Log("Faulted In Auth " + task.Exception);
                    }
                    else
                    {
                        signInCompleted.SetResult(((Task<FirebaseUser>)authTask).Result);
                        Debug.Log("Success");
                        user = auth.CurrentUser;
                        
                        // Check if user exists in database
                        CheckUserInDatabase(user);
                    }
                });
            }
        });
    }
    
    // Method to check if the user exists in the database
    private void CheckUserInDatabase(FirebaseUser firebaseUser)
    {
        if (firebaseUser == null)
        {
            if (statusText != null)
                statusText.text = "Authentication failed";
            return;
        }
        
        StartCoroutine(CheckUserCoroutine(firebaseUser));
    }
    
    private IEnumerator CheckUserCoroutine(FirebaseUser firebaseUser)
    {
        // Create the form data for the request
        WWWForm form = new WWWForm();
        form.AddField("googleId", firebaseUser.UserId);
        form.AddField("email", firebaseUser.Email);
        form.AddField("displayName", firebaseUser.DisplayName);
        
        // Create and send the request
        using (UnityWebRequest www = UnityWebRequest.Post(databaseCheckUrl, form))
        {
            if (statusText != null)
                statusText.text = "Checking registration...";
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.ConnectionError || 
                www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Database check error: " + www.error);
                if (statusText != null)
                    statusText.text = "Connection error. Please try again.";
            }
            else
            {
                // Parse the response
                string responseText = www.downloadHandler.text;
                Debug.Log("Response: " + responseText);
                
                // Check if user exists in database
                // Assuming the API returns a JSON with a "exists" field
                bool userExists = responseText.Contains("\"exists\":true");
                
                if (userExists)
                {
                    // User exists, load the next scene
                    if (statusText != null)
                        statusText.text = "Login successful!";
                    LoadNextScene();
                }
                else
                {
                    // User doesn't exist, show not registered message
                    ShowNotRegisteredMessage();
                }
            }
        }
    }
    
    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError("Next scene name is not set!");
        }
    }
    
    private void ShowNotRegisteredMessage()
    {
        // if (notRegisteredPanel != null)
        // {
        //     notRegisteredPanel.SetActive(true);
        // }
        // else
        if (statusText != null)
        {
            statusText.text = "Not Registered User";
        }
    }
}