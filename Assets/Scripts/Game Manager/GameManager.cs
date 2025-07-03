using UnityEngine;
using StarterAssets; // Needed for FirstPersonController
using System.Collections;
using System.Text;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int totalPropsToCollect = 0;
    private int propsCollected = 0;
    
    // Track fake props specifically
    private int totalFakeProps = 0;
    private int fakePropsCollected = 0;
    
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
    [SerializeField] private float winDelay = 0.5f; // Delay in seconds before triggering win condition
    
    // Audio sources pool to avoid GC allocations from PlayClipAtPoint
    private AudioSource effectsAudioSource;
    
    // Cache for string operations to reduce GC allocations
    private StringBuilder stringBuilder = new StringBuilder(128);

    void Awake()
    {
        // Ensure singleton pattern works correctly
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep GameManager between scene loads
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("GameManager initialized as singleton");
#endif
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("Multiple GameManager instances detected. Destroying duplicate.");
#endif
            Destroy(gameObject);
            return;
        }

        totalPropsToCollect = 0;
        propsCollected = 0;
        totalFakeProps = 0;
        fakePropsCollected = 0;
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
    
    // Register a fake prop
    public void RegisterFakeProp()
    {
        totalFakeProps++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Registered fake prop. Total fake props: {totalFakeProps}");
#endif
    }

    public void CollectProp()
    {
        propsCollected++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        stringBuilder.Clear();
        stringBuilder.Append("Collected ");
        stringBuilder.Append(propsCollected);
        stringBuilder.Append('/');
        stringBuilder.Append(totalPropsToCollect);
        Debug.Log(stringBuilder.ToString());
#endif

        if (!gameEnded && propsCollected == totalPropsToCollect)
        {
            // Immediately disable player movement when last prop is collected
            DisablePlayerMovement();
            
            // Start the delayed win coroutine for UI and sound effects
            StartCoroutine(DelayedWin());
        }
    }
    
    // Collect a fake prop
    public void CollectFakeProp()
    {
        fakePropsCollected++;
        CollectProp(); // Also increment the total props collected
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"Collected fake prop {fakePropsCollected}/{totalFakeProps}");
#endif

        // Check if all fake props have been collected
        if (!gameEnded && fakePropsCollected == totalFakeProps && totalFakeProps > 0)
        {
            // Immediately disable player movement when last fake prop is collected
            DisablePlayerMovement();
            
            // Start the delayed win coroutine for UI and sound effects
            StartCoroutine(DelayedWin());
        }
    }
    
    // Coroutine to delay the win condition
    private IEnumerator DelayedWin()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        stringBuilder.Clear();
        stringBuilder.Append("All props collected! Game will end in ");
        stringBuilder.Append(winDelay);
        stringBuilder.Append(" seconds");
        Debug.Log(stringBuilder.ToString());
#endif
        
        // Wait for the specified delay
        yield return new WaitForSeconds(winDelay);
        
        // Trigger the win condition (without disabling player movement again)
        GameWin(false);
    }

    public void GameWin()
    {
        GameWin(true);
    }

    private void GameWin(bool disableMovement)
    {
        if (gameEnded) return;
        gameEnded = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("Game Win! All props collected.");
        
        // Log UI and component state for debugging
        stringBuilder.Clear();
        stringBuilder.Append("WinUI is ");
        stringBuilder.Append(winUI != null ? "assigned" : "null");
        stringBuilder.Append(", GameTimer is ");
        stringBuilder.Append(gameTimer != null ? "assigned" : "null");
        Debug.Log(stringBuilder.ToString());
#endif
        
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("Win sound or audio source is missing");
#endif
        }

        // Only disable player movement if requested (not needed if already disabled)
        if (disableMovement)
        {
            DisablePlayerMovement();
        }
    }

    public void GameOver()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("GameOver method called in GameManager");
#endif
        
        // Check if game is already ended to avoid duplicate calls
        if (gameEnded)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Game already ended, ignoring duplicate GameOver call");
#endif
            return;
        }
        
        gameEnded = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("Game Over! Time's up.");
        
        // Log UI and component state for debugging
        stringBuilder.Clear();
        stringBuilder.Append("GameOverUI is ");
        stringBuilder.Append(gameOverUI != null ? "assigned" : "null");
        stringBuilder.Append(", GameTimer is ");
        stringBuilder.Append(gameTimer != null ? "assigned" : "null");
        Debug.Log(stringBuilder.ToString());
#endif
        
        if (gameTimer != null) gameTimer.PauseTimer();
        if (gameOverUI != null) 
        {
            gameOverUI.SetActive(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Game Over UI activated");
#endif
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("GameOverUI is not assigned in the inspector!");
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("Game over sound or audio source is missing");
#endif
        }

        DisablePlayerMovement();
    }

    public int PropsLeft()
    {
        return totalPropsToCollect - propsCollected;
    }
    
    public int FakePropsLeft()
    {
        return totalFakeProps - fakePropsCollected;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("PlayerController not assigned in GameManager.");
#endif
        }
    }
}
