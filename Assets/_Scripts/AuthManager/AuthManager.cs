using Firebase;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }
    public FirebaseUser CurrentUser { get; private set; }
    public Text statusText; // Assign in inspector

    private FirebaseAuth auth;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                Debug.Log("Firebase initialized");
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies");
            }
        });
    }

    public void SignInWithGoogle()
{
    if (statusText) statusText.text = "Signing in...";
    
    // Use GoogleAuthProvider directly
    var credential = GoogleAuthProvider.GetCredential("", ""); // We'll get the token from the web view
    
    auth.SignInWithCredentialAsync(credential).ContinueWith(task => {
        if (task.IsCanceled || task.IsFaulted)
        {
            if (statusText) statusText.text = "Sign in failed. Try again.";
            Debug.LogError("SignInWithCredentialAsync encountered an error: " + task.Exception);
            return;
        }

        CurrentUser = task.Result;
        Debug.Log("User signed in: " + CurrentUser.Email);
        
        // Check if user exists in your database
        StartCoroutine(CheckUserInDatabase(CurrentUser.Email));
    });
}

    private System.Collections.IEnumerator CheckUserInDatabase(string email)
    {
        // Replace with your actual API endpoint
        string url = "YOUR_WEBSITE_API/check-user";
        WWWForm form = new WWWForm();
        form.AddField("email", email);

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<UserCheckResponse>(www.downloadHandler.text);
                if (response.exists)
                {
                    if (statusText) statusText.text = "Login successful!";
                    PlayerPrefs.SetString("UserID", response.userData.userId);
                    PlayerPrefs.Save();
                    SceneManager.LoadScene("MainCityScene");
                }
                else
                {
                    if (statusText) statusText.text = "Account not registered.";
                    SignOut();
                }
            }
            else
            {
                if (statusText) statusText.text = "Server error. Try again.";
                Debug.LogError("Error: " + www.error);
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
        PlayerPrefs.DeleteKey("UserID");
        if (statusText) statusText.text = "Signed out";
    }
}

[System.Serializable]
public class UserCheckResponse
{
    public bool exists;
    public UserData userData;
}

[System.Serializable]
public class UserData
{
    public string userId;
    public string email;
}