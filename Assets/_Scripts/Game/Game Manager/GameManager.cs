using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public bool isGameOver { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject extraTimePanel; // Assign your new panel in the Inspector

    [Header("Game Settings")]
    [SerializeField] private float extraTimeAmount = 25f; // Time to add after watching an ad

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

        // Ensure the panel is disabled on start
        if (extraTimePanel != null)
        {
            extraTimePanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnRewardGranted += GrantExtraTimeReward;
        }
    }

    private void OnDisable()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnRewardGranted -= GrantExtraTimeReward;
        }
    }

    public void GameOver()
    {
        if (isGameOver) return; // Prevent multiple calls

        // Set game over flag
        isGameOver = true;
        
        // TODO: Play Loose Sound        
        
        StartCoroutine(ResetGame());
    }

    public void ShowExtraTimePanel()
    {
        if (extraTimePanel != null)
        {
            extraTimePanel.SetActive(true);
            Time.timeScale = 0f; // Pause the game
        }
        else
        {
            Debug.LogWarning("[GameManager] Extra Time Panel is not assigned. Proceeding with game over.");
            GameOver();
        }
    }

    // Called when the user closes the panel without watching an ad
    public void DeclineExtraTime()
    {
        if (extraTimePanel != null) extraTimePanel.SetActive(false);
        Time.timeScale = 1f; // Resume the game
        GameOver();
    }

    // Callback for when the ad reward is granted
    private void GrantExtraTimeReward()
    {
        Debug.Log("[GameManager] Rewarded ad completed. Granting extra time.");
        GameTimer timer = FindObjectOfType<GameTimer>();
        if (timer != null)
        {
            timer.AddTime(extraTimeAmount);
        }

        if (extraTimePanel != null) extraTimePanel.SetActive(false);
        Time.timeScale = 1f; // Resume the game
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
        yield return new WaitForSeconds(3f);
        
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
