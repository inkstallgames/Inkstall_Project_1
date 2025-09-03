using System.Collections;
using System.Threading.Tasks;
using Firebase.Auth;
using Google;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using System.Text;

public class GoogleLoginManager : MonoBehaviour
{
    [Header("Google / Firebase")]
    [SerializeField] private string webClientId = "187710511438-jej75f8qn7k8c2h4md576e1cktuaqgb1.apps.googleusercontent.com";

    [Header("Backend")]
    [SerializeField] private string databaseCheckUrl = "https://api.inkstall.in/api/auth/student/google-login";

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string nextSceneName = "MainScene";

    private FirebaseAuth auth;
    private FirebaseUser user;
    private string idToken;

    private void Start()
    {
        auth = FirebaseAuth.DefaultInstance;
    }

    public void OnLoginButtonClicked()
    {
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
        catch (System.Exception ex)
        {
            UpdateStatus("Google Sign-In Error: " + ex.Message);
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

        GoogleSignInUser googleUser = task.Result;
        idToken = googleUser.IdToken; // Store the ID token
        UpdateStatus("Google Sign-In Success: " + googleUser.Email);

        Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
        auth.SignInWithCredentialAsync(credential).ContinueWith(HandleFirebaseSignIn);
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
        UpdateStatus("Firebase Auth Success: " + user.Email);
        StartCoroutine(CheckUserInDatabase(user.Email, user.UserId));
    }

    private IEnumerator CheckUserInDatabase(string email, string googleId)
    {
        UpdateStatus("Checking user registration...");

        // Create request body with the expected format
        var requestData = new GoogleLoginRequest
        {
            email = email,
            googleId = googleId,
            idToken = idToken // Use the ID token from Google Sign-In
        };

        string jsonData = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10; // 10 seconds timeout

            // Add debug logging
            Debug.Log($"Sending request to: {databaseCheckUrl}");
            Debug.Log($"Request data: {jsonData}");

            yield return request.SendWebRequest();

            // Log the raw response
            string rawResponse = request.downloadHandler?.text ?? "null";
            Debug.Log($"Raw API Response: {rawResponse}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                UpdateStatus($"Server Error: {request.error}");
                Debug.LogError($"Request failed: {request.error}");
                Debug.LogError($"Response code: {request.responseCode}");
                
                // Try to parse error response even on HTTP error
                try {
                    var errorResponse = JsonUtility.FromJson<ErrorResponse>(rawResponse);
                    if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error)) {
                        UpdateStatus($"Error: {errorResponse.error}");
                        Debug.LogError($"Server error message: {errorResponse.error}");
                    }
                } catch {}
                
                yield break;
            }

            try
            {
                // Try to parse as successful response
                var response = JsonUtility.FromJson<DatabaseResponse>(rawResponse);
                if (response != null)
                {
                    if (response.registered)
                    {
                        UpdateStatus("Login successful! Loading...");
                        SceneManager.LoadScene(nextSceneName);
                    }
                    else
                    {
                        UpdateStatus("❌ Not a registered user");
                    }
                    yield break;
                }
            }
            catch (System.Exception jsonEx)
            {
                Debug.LogWarning($"Failed to parse response as DatabaseResponse: {jsonEx.Message}");
            }

            try
            {
                // Try to parse as error response
                var errorResponse = JsonUtility.FromJson<ErrorResponse>(rawResponse);
                if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.error))
                {
                    UpdateStatus($"Error: {errorResponse.error}");
                    yield break;
                }
            }
            catch (System.Exception jsonEx)
            {
                Debug.LogWarning($"Failed to parse response as ErrorResponse: {jsonEx.Message}");
            }

            // If we get here, the response format is unexpected
            UpdateStatus("Unexpected server response");
            Debug.LogWarning($"Unexpected response format: {rawResponse}");
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
        public string idToken; // Changed from client_id to idToken
    }

    [System.Serializable]
    private class DatabaseResponse
    {
        public bool registered;
    }

    [System.Serializable]
    private class ErrorResponse
    {
        public string error;
        public string message; // Added to capture more error details
    }
}