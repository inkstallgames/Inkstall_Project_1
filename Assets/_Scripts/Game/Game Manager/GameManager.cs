using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
   
    public AudioClip looseSound;
    public AudioClip winSound;

    // Play Loose effect
    // Play Win effect

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    public void GameOver()
    {
        // Level Loose effect
        // Play Loose Sound        
        
        StartCoroutine(ResetGame());
    }
    
    public void GameWin()
    {
        // Level Win effect
        // Play Win Sound
        
        StartCoroutine(ResetGame());
    }

    IEnumerator ResetGame()
    {
        yield return new WaitForSeconds(3f);     
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
    }

}
