using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Google;

public class TitleScreenManager : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("MainScene");
    }

    public void SignOut()
    {
        // Sign out from Firebase
        if (FirebaseAuth.DefaultInstance != null)
        {
            FirebaseAuth.DefaultInstance.SignOut();
        }

        // Sign out from Google
        if (GoogleSignIn.DefaultInstance != null)
        { 
            GoogleSignIn.DefaultInstance.SignOut();
        }

        // Clear saved user data
        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.DeleteKey("UserEmail");
        PlayerPrefs.DeleteKey("StudentId");
        PlayerPrefs.Save();

        // Load the login scene
        SceneManager.LoadScene("LoginScreen");
    }
}