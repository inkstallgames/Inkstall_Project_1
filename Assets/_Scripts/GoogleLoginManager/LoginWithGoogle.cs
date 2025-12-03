// using System.Collections;
// using System.Text;
// using System.Threading.Tasks;
// using Firebase;
// using Firebase.Auth;
// using GooglePlayGames;
// using GooglePlayGames.BasicApi;
// using UnityEngine;
// using UnityEngine.Networking;
// using UnityEngine.SceneManagement;
// using TMPro;
// using System;

// public class GoogleLoginManager : MonoBehaviour
// {
//     [Header("Backend")]
//     private string databaseCheckUrl =
//         "https://api.inkstall.in/api/auth/unity-login";

//     [Header("UI")]
//     [SerializeField] private TMP_Text statusText;
//     [SerializeField] private string nextSceneName;

//     private FirebaseAuth auth;
//     private bool firebaseInitialized = false;

//     private void Start()
//     {
//         // 1. Initialize Play Games Configuration
//         PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
//             .RequestIdToken() // Request ID Token for backend validation
//             .RequestEmail()   // Request Email to show to user
//             .Build();

//         PlayGamesPlatform.InitializeInstance(config);
//         // 2. Activate the Play Games Platform
//         PlayGamesPlatform.DebugLogEnabled = true;
//         PlayGamesPlatform.Activate();

//         InitializeFirebase();
//     }

//     private void InitializeFirebase()
//     {
//         FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
//         {
//             var dependencyStatus = task.Result;
//             if (dependencyStatus == DependencyStatus.Available)
//             {
//                 auth = FirebaseAuth.DefaultInstance;
//                 firebaseInitialized = true;
//                 Debug.Log("Firebase initialized successfully");
//             }
//             else
//             {
//                 Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
//                 UpdateStatus("Failed to initialize Firebase services");
//             }
//         });
//     }

//     public void OnLoginButtonClicked()
//     {
//         if (!firebaseInitialized)
//         {
//             UpdateStatus("Firebase not initialized. Please try again.");
//             InitializeFirebase();
//             return;
//         }

//         UpdateStatus("Starting Google Sign-In...");

//         // 3. Authenticate with Play Games
//         PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
//     }

//     private void ProcessAuthentication(SignInStatus status)
//     {
//         if (status == SignInStatus.Success)
//         {
//             // Login Success
//             string name = PlayGamesPlatform.Instance.GetUserDisplayName();
//             string email = PlayGamesPlatform.Instance.GetUserEmail();
//             string idToken = PlayGamesPlatform.Instance.GetIdToken();

//             Debug.Log($"Login Success! Name: {name}, Email: {email}");
//             UpdateStatus($"Welcome {name}! Verifying...");

//             if (!string.IsNullOrEmpty(idToken))
//             {
//                 StartCoroutine(CheckUserInDatabase(idToken));
//             }
//             else
//             {
//                 UpdateStatus("Login Failed: Could not get ID Token.");
//                 Debug.LogError("Play Games returned null ID Token.");
//             }
//         }
//         else
//         {
//             // Login Failed
//             UpdateStatus($"Login Failed: {status}");
//             Debug.LogError($"Play Games Authentication failed with status: {status}");
//         }
//     }

//     private IEnumerator CheckUserInDatabase(string idToken)
//     {
//         UpdateStatus("Verifying with server...");

//         var requestData = new IdTokenRequest { idToken = idToken };
//         string jsonData = JsonUtility.ToJson(requestData);
//         byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

//         using (UnityWebRequest request = new UnityWebRequest(databaseCheckUrl, "POST"))
//         {
//             request.uploadHandler = new UploadHandlerRaw(bodyRaw);
//             request.downloadHandler = new DownloadHandlerBuffer();
//             request.SetRequestHeader("Content-Type", "application/json");
//             request.timeout = 30;

//             Debug.Log($"Sending request to: {databaseCheckUrl}");

//             yield return request.SendWebRequest();

//             string rawResponse = request.downloadHandler?.text ?? "null";
//             Debug.Log($"Raw API Response: {rawResponse}");

//             if (request.result != UnityWebRequest.Result.Success)
//             {
//                 if (request.responseCode == 404 || request.responseCode == 401)
//                 {
//                     UpdateStatus("Student Not Registered!");
//                     // Sign out to allow trying again
//                     PlayGamesPlatform.Instance.SignOut();
//                 }
//                 else
//                 {
//                     UpdateStatus($"Server Error: {request.error}");
//                     Debug.LogError($"Request failed: {request.error}");
//                 }
//                 yield break;
//             }

//             try
//             {
//                 var response = JsonUtility.FromJson<BackendResponse>(rawResponse);

//                 if (response != null && response.success)
//                 {
//                     Debug.Log("Login successful for " + response.user.email);

//                     // Save user data for persistence
//                     PlayerPrefs.SetString("AuthToken", response.token);
//                     PlayerPrefs.SetString("UserEmail", response.user.email);
//                     if (!string.IsNullOrEmpty(response.user.studentId))
//                         PlayerPrefs.SetString("StudentId", response.user.studentId);
//                     PlayerPrefs.Save();

//                     StudentIdManager.Instance.SaveUserDataFromGoogleAuth(
//                         response.user.email,
//                         response.user.studentId ?? response.user.id,
//                         response.token
//                     );

//                     UpdateStatus("Login successful! Loading...");
//                     Invoke("LoadScene", 2f);
//                 }
//                 else
//                 {
//                     UpdateStatus("Student Not Registered!");
//                     Debug.LogError($"Authentication failed: {response?.message ?? "User not found"}");
//                     PlayGamesPlatform.Instance.SignOut();
//                 }
//             }
//             catch (Exception ex)
//             {
//                 UpdateStatus("Error processing server response");
//                 Debug.LogException(ex);
//             }
//         }
//     }

//     private void LoadScene()
//     {
//         SceneManager.LoadScene(nextSceneName);
//     }

//     // === Request/Response Models ===
//     [Serializable]
//     private class IdTokenRequest
//     {
//         public string idToken;
//     }

//     [Serializable]
//     private class BackendResponse
//     {
//         public bool success;
//         public string message;
//         public string token;
//         public UserData user;
//     }

//     [Serializable]
//     private class UserData
//     {
//         public string id;
//         public string email;
//         public string name;
//         public string[] roles;
//         public string profilePhotoUrl;
//         public string studentId;
//         public bool isStudent;
//     }

//     private void UpdateStatus(string message)
//     {
//         Debug.Log($"[GoogleLogin] {message}");
//         if (statusText != null) statusText.text = message;
//     }
// }
