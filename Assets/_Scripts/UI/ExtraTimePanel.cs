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

        // Reset ad watched state when panel is enabled
        adWatched = false;
        
        // Subscribe to ad reward event
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnRewardGranted += OnRewardGranted;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from ad reward event
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnRewardGranted -= OnRewardGranted;
        }
        
        // Resume game if it wasn't paused before
        if (!wasGamePaused)
        {
            Time.timeScale = 1f; // Resume the game
        }
        
        // Only trigger game over if ad wasn't watched
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        // Only trigger game over if ad wasn't watched
        if (!adWatched && GameManager.Instance != null)
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
            // Disable the button to prevent multiple clicks
            // You might want to add a loading indicator here
            
            // Show the ad for extra time
            AdManager.Instance.ShowRewardedAdForExtraTime(() => {
                Debug.Log("[ExtraTimePanel] Extra time reward callback received");
                OnRewardGranted();
            });
        }
        else
        {
            Debug.LogError("[ExtraTimePanel] AdManager.Instance is not found!");
        }
    }

    /// <summary>
    /// Callback when the rewarded ad is successfully watched
    /// </summary>
    private void OnRewardGranted()
    {
        adWatched = true;
        Debug.Log("[ExtraTimePanel] Reward granted, adding extra time.");
        
        try
        {
            // Add extra time and resume the game
            if (GameTimer.instance != null)
            {
                // Add the extra time for the current room
                GameTimer.instance.AddTime();
                
                // Start a coroutine to delay the panel deactivation
                StartCoroutine(DisablePanelAfterDelay(0.5f));
            }
            else
            {
                Debug.LogError("[ExtraTimePanel] GameTimer.instance is null when trying to add extra time");
                if (!wasGamePaused)
                {
                    Time.timeScale = 1f;
                }
                gameObject.SetActive(false);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ExtraTimePanel] Error in OnRewardGranted: {e.Message}");
            if (!wasGamePaused)
            {
                Time.timeScale = 1f;
            }
            gameObject.SetActive(false);
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
