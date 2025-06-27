using UnityEngine;
using StarterAssets; // Needed for FirstPersonController

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int totalPropsToCollect = 0;
    private int propsCollected = 0;
    private bool gameEnded = false;

    [Header("References")]
    public GameTimer gameTimer;        
    public GameObject winUI;           
    public GameObject gameOverUI;      
    public GameObject crosshair;        // Reference to crosshair UI
    [Tooltip("Assigned automatically at runtime when player is spawned")]
    public GameObject playerController; // Assigned at runtime when player is spawned

    [Header("Audio")]
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip gameOverSound;
    
    // Audio sources pool to avoid GC allocations from PlayClipAtPoint
    private AudioSource effectsAudioSource;

    void Awake()
    {
        // Ensure singleton pattern works correctly
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep GameManager between scene loads
            Debug.Log("GameManager initialized as singleton");
        }
        else
        {
            Debug.LogWarning("Multiple GameManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        totalPropsToCollect = 0;
        propsCollected = 0;
        gameEnded = false;
        
        // Create reusable audio source for sound effects
        effectsAudioSource = gameObject.AddComponent<AudioSource>();
        effectsAudioSource.playOnAwake = false;
        // effectsAudioSource.spatialBlend = 1.0f; // Make it 3D sound
    }

    public void RegisterCollectible()
    {
        totalPropsToCollect++;
    }

    public void CollectProp()
    {
        propsCollected++;
        Debug.Log($"Collected {propsCollected}/{totalPropsToCollect}");

        if (!gameEnded && propsCollected == totalPropsToCollect)
        {
            GameWin();
        }
    }

    public void GameWin()
    {
        if (gameEnded) return;
        gameEnded = true;

        Debug.Log("Game Win! All props collected.");
        
        // Log UI and component state for debugging
        Debug.Log($"WinUI is {(winUI != null ? "assigned" : "null")}, GameTimer is {(gameTimer != null ? "assigned" : "null")}");
        
        if (gameTimer != null) gameTimer.PauseTimer();
        if (winUI != null) winUI.SetActive(true);
        if (crosshair != null) crosshair.SetActive(false); // Hide crosshair

        // Play win sound using pooled audio source instead of PlayClipAtPoint
        if (winSound != null && effectsAudioSource != null)
        {
            effectsAudioSource.clip = winSound;
            effectsAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("Win sound or audio source is missing");
        }

        DisablePlayerMovement();
    }

    public void GameOver()
    {
        Debug.Log("GameOver method called in GameManager");
        
        // Check if game is already ended to avoid duplicate calls
        if (gameEnded)
        {
            Debug.Log("Game already ended, ignoring duplicate GameOver call");
            return;
        }
        
        gameEnded = true;

        Debug.Log("Game Over! Time's up.");
        
        // Log UI and component state for debugging
        Debug.Log($"GameOverUI is {(gameOverUI != null ? "assigned" : "null")}, GameTimer is {(gameTimer != null ? "assigned" : "null")}");
        
        if (gameTimer != null) gameTimer.PauseTimer();
        if (gameOverUI != null) 
        {
            gameOverUI.SetActive(true);
            Debug.Log("Game Over UI activated");
        }
        else
        {
            Debug.LogError("GameOverUI is not assigned in the inspector!");
        }
        
        if (crosshair != null) crosshair.SetActive(false); // Hide crosshair

        // Play game over sound using pooled audio source instead of PlayClipAtPoint
        if (gameOverSound != null && effectsAudioSource != null)
        {
            effectsAudioSource.clip = gameOverSound;
            effectsAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("Game over sound or audio source is missing");
        }

        DisablePlayerMovement();
    }

    public int PropsLeft()
    {
        return totalPropsToCollect - propsCollected;
    }

    private void DisablePlayerMovement()
    {
        if (playerController != null)
        {
            FirstPersonController controller = playerController.GetComponentInChildren<FirstPersonController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
        }
        else
        {
            Debug.LogWarning("PlayerController not assigned in GameManager.");
        }
    }
}
