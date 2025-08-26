using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private AudioSource audioSource;
    [SerializeField] private AudioClip doorCloseSound;
   
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

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
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
        audioSource.PlayOneShot(doorCloseSound);
        yield return new WaitForSeconds(1f);     
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
    }

}
