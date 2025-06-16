using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    public float totalTime = 180f;
    private float currentTime;
    public TextMeshProUGUI timerText;
    public bool timerRunning = true;

    private bool warningTriggered = false;

    void Start()
    {
        currentTime = totalTime;
        UpdateTimerUI();
    }

    void Update()
    {
        if (!timerRunning || currentTime <= 0f) return;

        currentTime -= Time.deltaTime;
        UpdateTimerUI();

        if (!warningTriggered && currentTime <= 60f)
        {
            warningTriggered = true;
            Debug.Log("⚠️ Only 1 minute remaining!");
        }

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            timerRunning = false;
            EndGame();
        }
    }

    void UpdateTimerUI()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (currentTime <= 30f)
            timerText.color = Color.red;
        else if (currentTime <= 60f)
            timerText.color = Color.yellow;
        else
            timerText.color = Color.white;
    }

    void EndGame()
    {
        Debug.Log("🛑 Time’s Up! Game Over.");

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
    }

    public void PauseTimer() => timerRunning = false;
    public void ResumeTimer() => timerRunning = true;
    public void StopTimer() => timerRunning = false;
    public bool IsRunning() => timerRunning;
    public float GetRemainingTime() => currentTime;
}
