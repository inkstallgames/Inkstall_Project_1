using UnityEngine;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int totalPropsToCollect = 0;
    private int propsCollected = 0;
    
    // Track fake props specifically
    private int totalFakeProps = 0;
    private int fakePropsCollected = 0;
    
    // Limited chances system
    [Header("Chances Settings")]
    [SerializeField] private int maxChances = 3;
    private int chancesRemaining;
    
    private bool gameEnded = false;

    [Header("References")]
    public GameTimer gameTimer;        
    public GameObject winUI;           
    public GameObject gameOverUI;      
    public GameObject crosshair;
    public TextMeshProUGUI chancesText;

    [Header("Feedback Text")]
    public TextMeshProUGUI fakeFoundText;
    public TextMeshProUGUI wrongGuessText;
    [SerializeField] private float feedbackTextDuration = 2.0f;

    [Header("Audio")]
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip wrongGuessSound;
    [SerializeField] private float winDelay = 0.5f;
    
    private AudioSource effectsAudioSource;
    private Coroutine fakeFoundCoroutine;
    private Coroutine wrongGuessCoroutine;

    // Room management variables
    private bool isRoomActive = false;
    private GameObject currentActiveRoom;

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

        totalPropsToCollect = 0;
        propsCollected = 0;
        totalFakeProps = 0;
        fakePropsCollected = 0;
        chancesRemaining = maxChances;
        gameEnded = false;
        
        effectsAudioSource = gameObject.AddComponent<AudioSource>();
        effectsAudioSource.playOnAwake = false;
        
        UpdateChancesUI();
        
        // Disable chances text at the start of the game
        if (chancesText != null) chancesText.gameObject.SetActive(false);
        
        if (fakeFoundText != null) fakeFoundText.gameObject.SetActive(false);
        if (wrongGuessText != null) wrongGuessText.gameObject.SetActive(false);
    }

    public void RegisterCollectible()
    {
        totalPropsToCollect++;
    }
    
    public void RegisterFakeProp()
    {
        totalFakeProps++;
    }

    public void CollectProp()
    {
        propsCollected++;

        if (!gameEnded && propsCollected == totalPropsToCollect)
        {
            StartCoroutine(DelayedWin());
        }
    }
    
    public void CollectFakeProp()
    {
        fakePropsCollected++;
        CollectProp();
        ShowFakeFoundText();

        if (!gameEnded && fakePropsCollected == totalFakeProps && totalFakeProps > 0)
        {
            StartCoroutine(DelayedWin());
        }
    }
    
    public bool HandleWrongGuess(Vector3 position)
    {
        chancesRemaining--;
        UpdateChancesUI();
        ShowWrongGuessText();
        
        if (wrongGuessSound != null && effectsAudioSource != null)
        {
            AudioSource.PlayClipAtPoint(wrongGuessSound, position, 1.0f);
        }
        
        if (chancesRemaining <= 0)
        {
            GameOver();
            return true;
        }
        
        return false;
    }
    
    private void ShowFakeFoundText()
    {
        if (fakeFoundText != null)
        {
            if (fakeFoundCoroutine != null)
            {
                StopCoroutine(fakeFoundCoroutine);
            }
            if (wrongGuessCoroutine != null)
            {
                StopCoroutine(wrongGuessCoroutine);
                wrongGuessText.gameObject.SetActive(false);
            }
            
            fakeFoundCoroutine = StartCoroutine(ShowTextTemporarily(fakeFoundText, feedbackTextDuration));
        }
    }
    
    private void ShowWrongGuessText()
    {
        if (wrongGuessText != null)
        {
            if (wrongGuessCoroutine != null)
            {
                StopCoroutine(wrongGuessCoroutine);
            }
            if (fakeFoundCoroutine != null)
            {
                StopCoroutine(fakeFoundCoroutine);
                fakeFoundText.gameObject.SetActive(false);
            }
            
            wrongGuessCoroutine = StartCoroutine(ShowTextTemporarily(wrongGuessText, feedbackTextDuration));
        }
    }
    
    private IEnumerator ShowTextTemporarily(TextMeshProUGUI text, float duration)
    {
        text.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        text.gameObject.SetActive(false);
    }
    
    private void UpdateChancesUI()
    {
        if (chancesText != null)
        {
            chancesText.text = $"Chances: {chancesRemaining}";
        }
    }
    
    // Method to show the chances text temporarily when the player first interacts with a door
    public void ShowChancesText()
    {
        if (chancesText != null)
        {
            // Make sure the chances text is updated first
            UpdateChancesUI();
            
            // Enable the chances text and keep it visible (not temporary)
            chancesText.gameObject.SetActive(true);
        }
    }

    private IEnumerator DelayedWin()
    {
        yield return new WaitForSeconds(winDelay);
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
        
        if (gameTimer != null) gameTimer.PauseTimer();
        if (winUI != null) winUI.SetActive(true);
        if (crosshair != null) crosshair.SetActive(false);
        
        // Disable all UI feedback texts
        if (chancesText != null) chancesText.gameObject.SetActive(false);
        if (fakeFoundText != null) fakeFoundText.gameObject.SetActive(false);
        if (wrongGuessText != null) wrongGuessText.gameObject.SetActive(false);

        if (winSound != null && effectsAudioSource != null)
        {
            effectsAudioSource.clip = winSound;
            effectsAudioSource.Play();
        }
    }

    public void GameOver()
    {
        if (gameEnded) return;
        gameEnded = true;
        
        if (gameTimer != null) gameTimer.PauseTimer();
        if (gameOverUI != null) 
        {
            gameOverUI.SetActive(true);
        }
        
        if (crosshair != null) crosshair.SetActive(false);
        
        // Disable all UI feedback texts
        if (chancesText != null) chancesText.gameObject.SetActive(false);
        if (fakeFoundText != null) fakeFoundText.gameObject.SetActive(false);
        if (wrongGuessText != null) wrongGuessText.gameObject.SetActive(false);

        if (gameOverSound != null && effectsAudioSource != null)
        {
            effectsAudioSource.clip = gameOverSound;
            effectsAudioSource.Play();
        }
    }

    public int PropsLeft() => totalPropsToCollect - propsCollected;
    public int FakePropsLeft() => totalFakeProps - fakePropsCollected;
    public int GetChancesRemaining() => chancesRemaining;

    // Room management methods
    
    // Call this method to check if a door can be opened
    public bool CanOpenDoor(GameObject doorObject)
    {
        // If no room is active, any door can be opened
        if (!isRoomActive)
        {
            return true;
        }
        
        // If a room is active, only allow opening the door of the current active room
        return doorObject == currentActiveRoom;
    }
    
    // Call this method when a door is opened to activate a room
    public void ActivateRoom(GameObject doorObject)
    {
        // Only activate if no other room is active
        if (!isRoomActive)
        {
            isRoomActive = true;
            currentActiveRoom = doorObject;
            Debug.Log($"Room activated: {doorObject.name}");
        }
    }
    
    // Call this method when a room is completed (all props collected or timer/chances ended)
    public void CompleteRoom()
    {
        isRoomActive = false;
        currentActiveRoom = null;
        Debug.Log("Room completed, player can now open other doors");
    }
    
    // Check if there's an active room
    public bool IsRoomActive()
    {
        return isRoomActive;
    }
}
