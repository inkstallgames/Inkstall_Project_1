using UnityEngine;
using TMPro;
using System.Text;

public class GameTimer : MonoBehaviour
{
    public static GameTimer instance;
    
    [Header("Timer Settings")]
    public float totalTime = 180f;
    private float currentTime;
    public TextMeshProUGUI timerText;
    public bool timerRunning = false; // Changed to false so timer doesn't start automatically

    private bool warningTriggered = false;
    private bool tickingStarted = false;
    private bool hasBeenTriggered = false; // Track if timer has been triggered at least once

    // Cache for string formatting to avoid GC allocations
    private StringBuilder timerStringBuilder;
    // Pre-cached strings for digits to avoid string allocations
    private string[] digitStrings = new string[60]; // For 0-59 seconds/minutes

    // Cache colors to avoid GC allocations
    private readonly Color normalColor = Color.green;
    private readonly Color warningColor = Color.yellow;
    private readonly Color dangerColor = Color.red;

    [Header("Tick Sound Settings")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private float tickVolume = 1f;
    private AudioSource tickSource;

    // Track last displayed time to avoid unnecessary UI updates
    private int lastDisplayedMinutes = -1;
    private int lastDisplayedSeconds = -1;

    void OnEnable()
    {
        StartTimer();
    }

    void Start()
    {
        currentTime = totalTime;

        // Setup audio but don't play yet
        if (tickSound != null)
        {
            tickSource = gameObject.AddComponent<AudioSource>();
            tickSource.clip = tickSound;
            tickSource.volume = tickVolume;
            tickSource.loop = true;
            tickSource.playOnAwake = false;
        }

        // Initialize string builder to avoid GC allocations
        timerStringBuilder = new StringBuilder(8);

        // Pre-cache all possible digit strings (0-59)
        for (int i = 0; i < 60; i++)
        {
            digitStrings[i] = i < 10 ? "0" + i : i.ToString();
        }
    }

    void Update()
    {
        // Only update if timer is running
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
        }

        if (!tickingStarted && currentTime <= 30f)
        {
            StartTicking();
        }

        // Check if timer reached zero
        if (currentTime <= 0f)
        {
            currentTime = 0f;
            timerRunning = false;
            StopTicking();
            GameManager.Instance.GameOver();
        }
    }

    void StartTimer()
    {
        try
        {
            Debug.Log("[GameTimer] Starting timer");
            
            // Show the timer UI if available
            if (timerText != null)
            {
                try 
                {
                    timerText.gameObject.SetActive(true);
                    Debug.Log("[GameTimer] Timer UI activated in StartTimer");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameTimer] Failed to activate timer UI in StartTimer: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[GameTimer] No timerText reference assigned in the inspector (StartTimer)");
            }

            // Reset timer state
            currentTime = totalTime;
            timerRunning = true;
            hasBeenTriggered = true;
            warningTriggered = false;
            tickingStarted = false;

            // Update UI immediately
            UpdateTimerUI();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameTimer] Error in StartTimer: {e.Message}");
            Debug.LogException(e);
        }
    }


    void UpdateTimerUI()
    {
        try
        {
            // Double null check with additional debug info
            if (timerText == null || timerText.gameObject == null)
            {
                Debug.LogWarning($"[GameTimer] Timer Text reference is missing or destroyed. Please assign a TextMeshProUGUI component in the inspector. Current state: timerText={timerText != null}, gameObject={(timerText != null ? timerText.gameObject : null)}");
                return;
            }

            // Ensure the text component is active
            if (!timerText.gameObject.activeInHierarchy)
            {
                try
                {
                    timerText.gameObject.SetActive(true);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameTimer] Failed to activate timer text: {e.Message}");
                    return;
                }
            }

            // Update the timer display
            try
            {
                int minutes = Mathf.FloorToInt(currentTime / 60f);
                int seconds = Mathf.FloorToInt(currentTime % 60f);

                // Store current values to avoid redundant updates
                lastDisplayedMinutes = minutes;
                lastDisplayedSeconds = seconds;

                // Use cached StringBuilder and pre-cached strings to avoid GC allocations
                if (timerStringBuilder != null)
                {
                    timerStringBuilder.Clear();
                    timerStringBuilder.Append(digitStrings[Mathf.Clamp(minutes, 0, 59)]);
                    timerStringBuilder.Append(':');
                    timerStringBuilder.Append(digitStrings[Mathf.Clamp(seconds, 0, 59)]);

                    if (timerText != null) // Additional null check
                    {
                        timerText.text = timerStringBuilder.ToString();

                        // Update text color based on time remaining
                        if (currentTime <= 30f)
                            timerText.color = dangerColor;
                        else if (currentTime <= 60f)
                            timerText.color = warningColor;
                        else
                            timerText.color = normalColor;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameTimer] Error updating timer display: {e.Message}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameTimer] Critical error in UpdateTimerUI: {e.Message}");
            Debug.LogException(e);
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

    // Modified to only allow starting the timer once and show UI when started
    public void ResumeTimer()
    {
        try
        {
            // Only start the timer if it hasn't been triggered before
            if (!hasBeenTriggered)
            {
                Debug.Log("[GameTimer] Starting timer for the first time");
                
                // Show the timer UI if available
                if (timerText != null)
                {
                    try 
                    {
                        timerText.gameObject.SetActive(true);
                        Debug.Log("[GameTimer] Timer UI activated");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[GameTimer] Failed to activate timer UI: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning("[GameTimer] No timerText reference assigned in the inspector");
                }

                timerRunning = true;
                hasBeenTriggered = true;

                // Update UI immediately
                UpdateTimerUI();
            }
            else
            {
                Debug.Log("[GameTimer] Timer has already been triggered, ignoring additional start attempts");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameTimer] Error in ResumeTimer: {e.Message}");
            Debug.LogException(e);
        }
    }

    public void StopTimer()
    {
        timerRunning = false;
        StopTicking();
    }

    public bool IsRunning() => timerRunning;
    public float GetRemainingTime() => currentTime;
    public bool HasBeenTriggered() => hasBeenTriggered; // New method to check if timer has been triggered
}
