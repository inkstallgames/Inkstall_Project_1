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
}