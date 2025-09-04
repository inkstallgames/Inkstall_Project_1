using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }
    
    // Keys for PlayerPrefs
    private const string STUDENT_ID_KEY = "StudentID";
    private const string IS_LOGGED_IN_KEY = "IsLoggedIn";
    
    public string StudentId { get; private set; }
    public bool IsLoggedIn { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveLoginData(string studentId)
    {
        StudentId = studentId;
        IsLoggedIn = true;
        
        PlayerPrefs.SetString(STUDENT_ID_KEY, studentId);
        PlayerPrefs.SetInt(IS_LOGGED_IN_KEY, 1); // 1 for true
        PlayerPrefs.Save();
    }

    public void Logout()
    {
        StudentId = "";
        IsLoggedIn = false;
        
        PlayerPrefs.DeleteKey(STUDENT_ID_KEY);
        PlayerPrefs.DeleteKey(IS_LOGGED_IN_KEY);
        PlayerPrefs.Save();
    }

    private void LoadData()
    {
        StudentId = PlayerPrefs.GetString(STUDENT_ID_KEY, "");
        IsLoggedIn = PlayerPrefs.GetInt(IS_LOGGED_IN_KEY, 0) == 1;
    }
}