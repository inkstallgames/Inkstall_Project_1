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
    private string minutesStr, secondsStr;

    [Header("Tick Sound Settings")]
    [SerializeField] private AudioClip tickSound;
    [SerializeField] private float tickVolume = 1f;
    private AudioSource tickSource;

    void Start()
    {
        currentTime = totalTime;
        
        // Initialize string builder to avoid GC allocations
        timerStringBuilder = new StringBuilder(8);
        
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
        if (!timerRunning || currentTime <= 0f) return;

        currentTime -= Time.deltaTime;
        UpdateTimerUI();

        if (!warningTriggered && currentTime <= 60f)
        {
            warningTriggered = true;
            Debug.Log(" Only 1 minute remaining!");
        }

        if (!tickingStarted && currentTime <= 30f)
        {
            StartTicking();
        }

        if (currentTime <= 0f)
        {
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
        
        // Use cached StringBuilder instead of string.Format to avoid GC allocations
        timerStringBuilder.Clear();
        
        // Format minutes with leading zero if needed
        if (minutes < 10)
            timerStringBuilder.Append('0');
        timerStringBuilder.Append(minutes);
        
        timerStringBuilder.Append(':');
        
        // Format seconds with leading zero if needed
        if (seconds < 10)
            timerStringBuilder.Append('0');
        timerStringBuilder.Append(seconds);
        
        timerText.text = timerStringBuilder.ToString();

        if (currentTime <= 30f)
            timerText.color = Color.red;
        else if (currentTime <= 60f)
            timerText.color = Color.yellow;
        else
            timerText.color = Color.white;
    }

    void EndGame()
    {
        Debug.Log("Time's Up! Game Over.");

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
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
