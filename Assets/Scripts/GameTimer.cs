using UnityEngine;
using TMPro;
using System.Text;

public class GameTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float totalTime = 180f;
    private float currentTime;
    public TextMeshProUGUI timerText;
    public bool timerRunning = true;

    private bool warningTriggered = false;
    private bool tickingStarted = false;
    
    // Cache for string formatting to avoid GC allocations
    private StringBuilder timerStringBuilder;
    // Pre-cached strings for digits to avoid string allocations
    private string[] digitStrings = new string[60]; // For 0-59 seconds/minutes
    
    // Cache colors to avoid GC allocations
    private readonly Color normalColor = Color.white;
    private readonly Color warningColor = Color.yellow;
    private readonly Color dangerColor = Color.red;

    [Header("Tick Sound Settings")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private float tickVolume = 1f;
    private AudioSource tickSource;
    
    // Track last displayed time to avoid unnecessary UI updates
    private int lastDisplayedMinutes = -1;
    private int lastDisplayedSeconds = -1;

    void Start()
    {
        currentTime = totalTime;
        
        // Initialize string builder to avoid GC allocations
        timerStringBuilder = new StringBuilder(8);
        
        // Pre-cache all possible digit strings (0-59)
        for (int i = 0; i < 60; i++)
        {
            digitStrings[i] = i < 10 ? "0" + i : i.ToString();
        }
        
        UpdateTimerUI();

        if (tickSound != null)
        {
            tickSource = gameObject.AddComponent<AudioSource>();
            tickSource.clip = tickSound;
            tickSource.volume = tickVolume;
            tickSource.loop = true;
            tickSource.playOnAwake = false;
        }
    }

    void Update()
    {
        // Only return if timer is not running, but still process if currentTime <= 0
        // to ensure EndGame gets called
        if (!timerRunning) return;

        currentTime -= Time.deltaTime;
        
        // Only update UI when the displayed time would change
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        
        if (minutes != lastDisplayedMinutes || seconds != lastDisplayedSeconds)
        {
            UpdateTimerUI();
        }

        if (!warningTriggered && currentTime <= 60f)
        {
            warningTriggered = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Only 1 minute remaining!");
#endif
        }

        if (!tickingStarted && currentTime <= 30f)
        {
            StartTicking();
        }

        // Moved outside the main timer logic to ensure it always gets checked
        if (currentTime <= 0f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Timer reached zero, triggering EndGame");
#endif
            currentTime = 0f;
            timerRunning = false;
            StopTicking();
            EndGame();
        }
    }

    void UpdateTimerUI()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        
        // Store current values to avoid redundant updates
        lastDisplayedMinutes = minutes;
        lastDisplayedSeconds = seconds;
        
        // Use cached StringBuilder and pre-cached strings to avoid GC allocations
        timerStringBuilder.Clear();
        timerStringBuilder.Append(digitStrings[Mathf.Min(minutes, 59)]);
        timerStringBuilder.Append(':');
        timerStringBuilder.Append(digitStrings[seconds]);
        
        timerText.text = timerStringBuilder.ToString();

        // Use cached colors to avoid GC allocations
        if (currentTime <= 30f)
            timerText.color = dangerColor;
        else if (currentTime <= 60f)
            timerText.color = warningColor;
        else
            timerText.color = normalColor;
    }

    void EndGame()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("Time's Up! Game Over.");
#endif

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        // Ensure GameManager exists and call GameOver with proper error handling
        if (GameManager.Instance != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Calling GameManager.GameOver() from GameTimer.EndGame()");
#endif
            GameManager.Instance.GameOver();
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("GameManager.Instance is null in GameTimer.EndGame()! Cannot call GameOver(). Make sure GameManager exists in the scene.");
#endif
        }
    }

    void StartTicking()
    {
        if (tickSource != null)
        {
            tickSource.Play();
            tickingStarted = true;
        }
    }

    void StopTicking()
    {
        if (tickSource != null && tickSource.isPlaying)
        {
            tickSource.Stop();
        }
    }

    public void PauseTimer()
    {
        timerRunning = false;
        StopTicking();
    }

    public void ResumeTimer()
    {
        timerRunning = true;

        if (currentTime <= 30f && !tickingStarted)
        {
            StartTicking();
        }
    }

    public void StopTimer()
    {
        timerRunning = false;
        StopTicking();
    }

    public bool IsRunning() => timerRunning;
    public float GetRemainingTime() => currentTime;
}
