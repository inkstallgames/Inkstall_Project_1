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
        // Pause the game when panel is shown
        wasGamePaused = Time.timeScale < 0.1f; // Check if game was already paused
        if (!wasGamePaused)
        {
            Time.timeScale = 0f; // Pause the game
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
            Time.timeScale = 1f; // Resume the game
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
        if (AdManager.Instance != null)
        {
            // Set flag to indicate ad is showing
            isAdShowing = true;
            
            // Show the ad for extra time with a callback for when the ad is closed
            AdManager.Instance.ShowRewardedAdForExtraTime(() => {
                Debug.Log("[ExtraTimePanel] Extra time reward granted");
                adWatched = true;
                
                // Add the extra time immediately
                if (GameTimer.instance != null)
                {
                    GameTimer.instance.AddTime();
                }
                
                // The ad is still showing at this point, so we'll wait for it to close
                // before resuming the game
            });
            
            // The ad will be closed by the AdManager's OnAdFullScreenContentClosed event
            // We'll handle the game resumption there
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
            StartCoroutine(DisablePanelAfterDelay(0.1f));
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
