using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool isGameOver { get; private set; }

    private AudioSource audioSource;
    [SerializeField] private AudioClip doorCloseSound;
    
    public AudioClip looseSound;
    public AudioClip levelCompleteSound;
    


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
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (AudioManager.Instance != null)
        {
            audioSource.volume = AudioManager.Instance.sfxVolume;
        }


    }

    public void GameOver()
    {
        if (isGameOver) return; // Prevent multiple calls

        // Set game over flag
        isGameOver = true;
        
        // Play Loose Sound
        if (looseSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(looseSound);
        }
        
        StartCoroutine(ResetGame());
    }
    
    public void ResetGameOverState()
    {
        Debug.Log("[GameManager] Resetting game over state");
        isGameOver = false;
        StopAllCoroutines(); // Stop any pending game over sequences
    }
    
    public void LevelWin()
    {
        // Level Win effect
        
        // Play Win Sound
        if (levelCompleteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(levelCompleteSound);
        }
        StartCoroutine(ResetGame());
    }

    IEnumerator ResetGame()
    {
        yield return new WaitForSeconds(2f);
        
        // Play door close sound
        if (doorCloseSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(doorCloseSound);
        }
        
        yield return new WaitForSeconds(1f);
        
        // Reset game over state before reloading
        isGameOver = false;
        
        // Reload the current scene
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

}
