using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleScreenManager : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene("MainScene");
    }

    public void PlayMultiplayer()
    {
        SceneManager.LoadScene("Lobby");
    }
}
