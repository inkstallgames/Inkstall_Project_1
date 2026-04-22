using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleScreenManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string storyLobbyScene = "StoryLobby";
    public string multiplayerLobbyScene = "MultiplayerLobby";
    
    public void PlayStoryMode()
    {
        SceneManager.LoadScene(storyLobbyScene);
    }

    public void PlayMultiplayer()
    {
        SceneManager.LoadScene(multiplayerLobbyScene);
    }
}
