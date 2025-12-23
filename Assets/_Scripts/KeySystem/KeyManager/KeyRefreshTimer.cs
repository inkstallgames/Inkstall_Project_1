using System;
using UnityEngine;

public class KeyRefreshTimer : MonoBehaviour
{
    public static KeyRefreshTimer Instance;
    
    // Event that's triggered when keys count changes
    public event Action OnKeysCountChanged;

    [Header("Timer Settings")]
    [SerializeField] private float keyRefreshTimeInMinutes = 10f; // Time in minutes to refresh one key
    [SerializeField] public int maxKeys = 5; // Maximum number of keys

    private const string TIMER_START_KEY = "KeyRefreshTimerStart";
    private const string TIMER_ACTIVE_KEY = "KeyRefreshTimerActive";

    private bool isTimerActive = false;
    private DateTime timerStartTime;
    private float keyRefreshTimeInSeconds;

    // Property to get current keys count
    public int KeysCount 
    { 
        get { return KeyManager.Instance != null ? KeyManager.Instance.GetCurrentKeyCount() : 0; }
    }

    private void Awake()
    {
        // Set up singleton
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

        keyRefreshTimeInSeconds = keyRefreshTimeInMinutes * 60f;
    }

    private void Start()
    {
        // Check if there was an active timer when the app was closed
        LoadTimerState();
        CheckAndProcessTimer();

        // If no timer was active but keys are below max, start the timer
        if (!isTimerActive && KeyManager.Instance != null)
        {
            int currentKeys = KeyManager.Instance.GetCurrentKeyCount();
            if (currentKeys < maxKeys)
            {
                StartTimer();
                Debug.Log($"Timer started at game start. Current keys: {currentKeys}/{maxKeys}");
            }
        }
    }

    private void Update()
    {
        if (isTimerActive)
        {
            CheckAndProcessTimer();
        }
    }

    /// <summary>
    /// Start the timer when a key is used
    /// </summary>
    public void OnKeyUsed()
    {
        int currentKeys = KeyManager.Instance.GetCurrentKeyCount();

        // Only start timer if keys are less than max and timer is not already active
        if (currentKeys < maxKeys && !isTimerActive)
        {
            StartTimer();
        }
        
        // Notify listeners that keys count has changed
        OnKeysCountChanged?.Invoke();
    }

    /// <summary>
    /// Start the refresh timer
    /// </summary>
    private void StartTimer()
    {
        isTimerActive = true;
        timerStartTime = DateTime.Now;
        SaveTimerState();
        Debug.Log($"Key refresh timer started at {timerStartTime}");
    }

    /// <summary>
    /// Check the timer and add keys if time has elapsed
    /// </summary>
    private void CheckAndProcessTimer()
    {
        if (!isTimerActive)
            return;

        int currentKeys = KeyManager.Instance.GetCurrentKeyCount();

        // If keys are already at max, stop the timer
        if (currentKeys >= maxKeys)
        {
            StopTimer();
            return;
        }

        // Calculate elapsed time since timer started
        TimeSpan elapsedTime = DateTime.Now - timerStartTime;
        float elapsedSeconds = (float)elapsedTime.TotalSeconds;

        // Check if enough time has passed to add a key
        if (elapsedSeconds >= keyRefreshTimeInSeconds)
        {
            // Calculate how many keys should be added
            int keysToAdd = Mathf.FloorToInt(elapsedSeconds / keyRefreshTimeInSeconds);
            
            // Make sure we don't exceed max keys
            int newKeyCount = Mathf.Min(currentKeys + keysToAdd, maxKeys);
            int actualKeysAdded = newKeyCount - currentKeys;

            if (actualKeysAdded > 0)
            {
                // Add the keys
                KeyManager.Instance.AddKeys(actualKeysAdded);
                Debug.Log($"Added {actualKeysAdded} key(s). Current keys: {KeyManager.Instance.GetCurrentKeyCount()}");
                
                // Notify listeners that keys count has changed
                OnKeysCountChanged?.Invoke();

                // Update timer start time for remaining time
                float remainingTime = elapsedSeconds - (keysToAdd * keyRefreshTimeInSeconds);
                timerStartTime = DateTime.Now.AddSeconds(-remainingTime);
                SaveTimerState();
            }

            // Check if we've reached max keys
            if (KeyManager.Instance.GetCurrentKeyCount() >= maxKeys)
            {
                StopTimer();
            }
        }
    }

    /// <summary>
    /// Stop the timer
    /// </summary>
    private void StopTimer()
    {
        isTimerActive = false;
        ClearTimerState();
        Debug.Log("Key refresh timer stopped - max keys reached");
    }

    /// <summary>
    /// Save timer state to PlayerPrefs
    /// </summary>
    private void SaveTimerState()
    {
        PlayerPrefs.SetString(TIMER_START_KEY, timerStartTime.ToBinary().ToString());
        PlayerPrefs.SetInt(TIMER_ACTIVE_KEY, isTimerActive ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load timer state from PlayerPrefs
    /// </summary>
    private void LoadTimerState()
    {
        if (PlayerPrefs.HasKey(TIMER_ACTIVE_KEY))
        {
            isTimerActive = PlayerPrefs.GetInt(TIMER_ACTIVE_KEY) == 1;

            if (isTimerActive && PlayerPrefs.HasKey(TIMER_START_KEY))
            {
                long binaryTime = long.Parse(PlayerPrefs.GetString(TIMER_START_KEY));
                timerStartTime = DateTime.FromBinary(binaryTime);
                Debug.Log($"Loaded timer state. Timer started at: {timerStartTime}");
            }
        }
    }

    /// <summary>
    /// Clear timer state from PlayerPrefs
    /// </summary>
    private void ClearTimerState()
    {
        PlayerPrefs.DeleteKey(TIMER_START_KEY);
        PlayerPrefs.DeleteKey(TIMER_ACTIVE_KEY);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Get remaining time until next key refresh
    /// </summary>
    /// <returns>Remaining time in seconds, or 0 if timer is not active</returns>
    public float GetRemainingTime()
    {
        if (!isTimerActive)
            return 0f;

        TimeSpan elapsedTime = DateTime.Now - timerStartTime;
        float elapsedSeconds = (float)elapsedTime.TotalSeconds;
        float remainingSeconds = keyRefreshTimeInSeconds - (elapsedSeconds % keyRefreshTimeInSeconds);

        return remainingSeconds;
    }

    /// <summary>
    /// Get remaining time as formatted string (MM:SS)
    /// </summary>
    /// <returns>Formatted time string</returns>
    public string GetRemainingTimeFormatted()
    {
        if (!isTimerActive)
            return "00:00";

        float remainingSeconds = GetRemainingTime();
        int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
        int seconds = Mathf.FloorToInt(remainingSeconds % 60f);

        return $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// Check if timer is currently active
    /// </summary>
    public bool IsTimerActive()
    {
        return isTimerActive;
    }

    /// <summary>
    /// Force check timer (useful for debugging or manual refresh)
    /// </summary>
    public void ForceCheckTimer()
    {
        CheckAndProcessTimer();
    }

    private void OnApplicationQuit()
    {
        // Save timer state when app closes
        if (isTimerActive)
        {
            SaveTimerState();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // Save timer state when app is paused (mobile)
        if (pauseStatus && isTimerActive)
        {
            SaveTimerState();
        }
        // Check timer when app resumes
        else if (!pauseStatus && isTimerActive)
        {
            CheckAndProcessTimer();
        }
    }
}
