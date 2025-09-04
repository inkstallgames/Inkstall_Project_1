using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    private void Start()
    {
        // Check if user is already logged in
        if (GameDataManager.Instance.IsLoggedIn && !string.IsNullOrEmpty(GameDataManager.Instance.StudentId))
        {
            // User is logged in, load main scene
            string studentId = GameDataManager.Instance.StudentId;
            
            // Initialize managers if they exist in this scene
            if (KeyManager.Instance != null)
                KeyManager.Instance.studentId = studentId;
                
            if (CoinsManager.Instance != null)
                CoinsManager.Instance.userId = studentId;
                
            SceneManager.LoadScene("MainScene");
        }
        else
        {
            // User not logged in, stay on login scene
            Debug.Log("No active login session found");
        }
    }
}