using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// This script manages the UI interactions for the 'Extra Time' panel.
/// It should be attached to the panel GameObject.
/// </summary>
public class ExtraTimePanel : MonoBehaviour
{
    [SerializeField] private Slider countdownSlider;
    private bool adWatched = false;
    private bool wasGamePaused = false;
    private bool isAdShowing = false;
    private Coroutine countdownCoroutine;


    private void OnEnable()
    {
        countdownCoroutine = StartCoroutine(CountdownCoroutine());
        wasGamePaused = Time.timeScale < 0.1f; 

        if (!wasGamePaused)
        {
            Time.timeScale = 0f; 
        }

        // Reset states when panel is enabled
        adWatched = false;
        isAdShowing = false;
    }

    private void OnDisable()
    {
        // Only resume the game if no ad is showing
        if (!isAdShowing && !wasGamePaused)
        {
            Time.timeScale = 1f; 
        }
        
        // Stop the countdown coroutine if it's running
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        // Only trigger game over if ad wasn't watched and no ad is showing
        if (!adWatched && !isAdShowing && GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
            Debug.Log("[ExtraTimePanel] Panel disabled without watching ad, triggering game over.");
        }
    }

    private IEnumerator CountdownCoroutine()
    {
        float duration = 5f;
        float elapsedTime = 0f;

        if (countdownSlider != null)
        {
            countdownSlider.value = 1f;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            if (countdownSlider != null)
            {
                countdownSlider.value = 1 - (elapsedTime / duration);
            }
            yield return null;
        }

        // If countdown finishes, hide the panel. OnDisable will handle game over.
        gameObject.SetActive(false);
    }

    /// <summary>
    /// This method should be linked to the 'Watch Ad' button's OnClick event.
    /// It calls the AdManager to show a rewarded ad.
    /// </summary>
    public void OnWatchAdClicked()
    {
        Debug.Log("[ExtraTimePanel] 'Watch Ad' button clicked.");

        // Stop the countdown coroutine to prevent the panel from closing automatically
        if (countdownCoroutine != null)
        { 
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (AdManager.Instance != null)
        {
            isAdShowing = true;
            
            AdManager.Instance.ShowRewardedAdForExtraTime(() => {
                Debug.Log("[ExtraTimePanel] Extra time reward granted");
                adWatched = true;
                
                GameTimer timer = GameTimer.instance;
                if (timer == null) 
                {
                    Debug.LogWarning("[ExtraTimePanel] GameTimer.instance is null, trying to find it in the scene.");
                    timer = FindObjectOfType<GameTimer>();
                }

                if (timer != null)
                {
                    timer.AddTime();
                }
                else
                {
                    Debug.LogError("[ExtraTimePanel] GameTimer could not be found. Cannot add extra time.");
                }
            });
        }
        else
        {
            Debug.LogError("[ExtraTimePanel] AdManager.Instance is not found!");
            isAdShowing = false;
        }
    }

    // This method is called when the ad is fully closed
    public void OnAdClosed()
    {
        Debug.Log("[ExtraTimePanel] Ad closed");
        isAdShowing = false;
        
        // If the ad was watched, deactivate the panel
        if (adWatched)
        {
            gameObject.SetActive(false);
        }
        // If the ad wasn't watched (user closed without watching), resume the game
        else if (!wasGamePaused)
        { 
            Time.timeScale = 1f;
        }
    }
    
    private IEnumerator DisablePanelAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// This method should be linked to the 'Close' (X) button's OnClick event.
    /// It triggers game over when the player chooses not to watch an ad.
    /// </summary>
    public void OnCloseButtonClicked()
    {
        Debug.Log("[ExtraTimePanel] 'Close' button clicked. Ending game.");
        gameObject.SetActive(false);
        
        // Only call GameOver if the game manager exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }
    }
}
