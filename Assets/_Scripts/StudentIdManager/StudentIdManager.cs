using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

public class StudentIdManager : MonoBehaviour
{
    private static StudentIdManager _instance;
    public static StudentIdManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("StudentIdManager");
                _instance = go.AddComponent<StudentIdManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Constants
    private const string STUDENT_ID_KEY = "StudentId";
    private const string USER_EMAIL_KEY = "UserEmail";
    private const string AUTH_TOKEN_KEY = "AuthToken";

    // Events
    public event Action<string> OnStudentIdLoaded;
    public event Action<string> OnStudentIdError;

    // Properties
    private string _studentId = null;
    public string StudentId 
    { 
        get 
        {
            if (string.IsNullOrEmpty(_studentId))
            {
                _studentId = GetStudentIdFromStorage();
            }
            return _studentId;
        }
    }

    // WebGL specific methods
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetLocalStorageItem(string key);

    [DllImport("__Internal")]
    private static extern void SetLocalStorageItem(string key, string value);
    #endif

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Try to load student ID on startup
        LoadStudentId();
    }

    // Load student ID from the appropriate source based on platform
    public void LoadStudentId()
    {
        string id = GetStudentIdFromStorage();
        
        if (!string.IsNullOrEmpty(id))
        {
            _studentId = id;
            Debug.Log($"[StudentIdManager] Loaded student ID: {_studentId}");
            OnStudentIdLoaded?.Invoke(_studentId);
        }
        else
        {
            Debug.LogWarning("[StudentIdManager] No student ID found in storage");
            OnStudentIdError?.Invoke("No student ID found");
        }
    }

    // Get student ID from the appropriate storage based on platform
    private string GetStudentIdFromStorage()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        // For WebGL, use localStorage
        return GetLocalStorageItem(STUDENT_ID_KEY);
        #else
        // For other platforms (including Android), use PlayerPrefs
        return PlayerPrefs.GetString(STUDENT_ID_KEY, null);
        #endif
    }

    // Save student ID to the appropriate storage based on platform
    public void SaveStudentId(string studentId)
    {
        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("[StudentIdManager] Attempted to save null or empty student ID");
            return;
        }

        _studentId = studentId;
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        // For WebGL, use localStorage
        SetLocalStorageItem(STUDENT_ID_KEY, studentId);
        #else
        // For other platforms (including Android), use PlayerPrefs
        PlayerPrefs.SetString(STUDENT_ID_KEY, studentId);
        PlayerPrefs.Save();
        #endif
        
        Debug.Log($"[StudentIdManager] Saved student ID: {studentId}");
        OnStudentIdLoaded?.Invoke(studentId);
    }

    // Save user data from Google authentication
    public void SaveUserDataFromGoogleAuth(string email, string studentId, string authToken)
    {
        if (string.IsNullOrEmpty(studentId))
        {
            Debug.LogError("[StudentIdManager] Attempted to save null or empty student ID from Google Auth");
            return;
        }

        _studentId = studentId;
        
        // Save to PlayerPrefs (works on all platforms including Android)
        PlayerPrefs.SetString(STUDENT_ID_KEY, studentId);
        PlayerPrefs.SetString(USER_EMAIL_KEY, email);
        PlayerPrefs.SetString(AUTH_TOKEN_KEY, authToken);
        PlayerPrefs.Save();
        
        Debug.Log($"[StudentIdManager] Saved user data from Google Auth - Email: {email}, StudentId: {studentId}");
        OnStudentIdLoaded?.Invoke(studentId);
    }

    // Check if student ID exists in storage
    public bool HasStudentId()
    {
        return !string.IsNullOrEmpty(GetStudentIdFromStorage());
    }

    // Clear student ID and related data
    public void ClearStudentId()
    {
        _studentId = null;
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        // For WebGL, use localStorage
        SetLocalStorageItem(STUDENT_ID_KEY, "");
        SetLocalStorageItem(USER_EMAIL_KEY, "");
        SetLocalStorageItem(AUTH_TOKEN_KEY, "");
        #else
        // For other platforms (including Android), use PlayerPrefs
        PlayerPrefs.DeleteKey(STUDENT_ID_KEY);
        PlayerPrefs.DeleteKey(USER_EMAIL_KEY);
        PlayerPrefs.DeleteKey(AUTH_TOKEN_KEY);
        PlayerPrefs.Save();
        #endif
        
        Debug.Log("[StudentIdManager] Cleared student ID and related data");
    }

    // Get auth token
    public string GetAuthToken()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        return GetLocalStorageItem(AUTH_TOKEN_KEY);
        #else
        return PlayerPrefs.GetString(AUTH_TOKEN_KEY, null);
        #endif
    }

    // Get user email
    public string GetUserEmail()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        return GetLocalStorageItem(USER_EMAIL_KEY);
        #else
        return PlayerPrefs.GetString(USER_EMAIL_KEY, null);
        #endif
    }
    
    // Added for backward compatibility
    public string GetStudentId()
    {
        return StudentId;
    }

    // Lookup student ID by email from the server
    public IEnumerator LookupStudentIdByEmail(string email, string apiUrl)
    {
        if (string.IsNullOrEmpty(email))
        {
            Debug.LogError("[StudentIdManager] Cannot lookup student ID: Email is null or empty");
            OnStudentIdError?.Invoke("Email is required");
            yield break;
        }

        string url = $"{apiUrl}/lookup?email={UnityWebRequest.EscapeURL(email)}";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[StudentIdManager] Error looking up student ID: {webRequest.error}");
                OnStudentIdError?.Invoke($"Error: {webRequest.error}");
            }
            else
            {
                try
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    Debug.Log($"[StudentIdManager] Lookup response: {jsonResponse}");
                    
                    // Parse the response (adjust this based on your API response format)
                    LookupResponse response = JsonUtility.FromJson<LookupResponse>(jsonResponse);
                    
                    if (response != null && response.success && !string.IsNullOrEmpty(response.studentId))
                    {
                        SaveStudentId(response.studentId);
                        Debug.Log($"[StudentIdManager] Found and saved student ID: {response.studentId}");
                    }
                    else
                    {
                        Debug.LogWarning("[StudentIdManager] Student ID not found for email");
                        OnStudentIdError?.Invoke("Student ID not found");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[StudentIdManager] Error parsing lookup response: {e.Message}");
                    OnStudentIdError?.Invoke("Error parsing response");
                }
            }
        }
    }

    // Response class for student ID lookup
    [Serializable]
    private class LookupResponse
    {
        public bool success;
        public string studentId;
        public string message;
    }
}
