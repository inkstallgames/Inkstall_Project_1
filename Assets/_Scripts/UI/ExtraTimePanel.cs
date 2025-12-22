using UnityEngine;

/// <summary>
/// This script manages the UI interactions for the 'Extra Time' panel.
/// It should be attached to the panel GameObject.
/// </summary>
public class ExtraTimePanel : MonoBehaviour
{
    private void OnDisable()
    {
        // When the panel is disabled (either by close button or other means), trigger game over
        if (GameManager.Instance != null)
        {
            Debug.Log("[ExtraTimePanel] Panel disabled, triggering game over.");
            GameManager.Instance.DeclineExtraTime();
        }
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
            // The GameManager will handle the reward via the OnRewardGranted event
            AdManager.Instance.ShowRewardedAd();
        }
        else
        {
            Debug.LogError("[ExtraTimePanel] AdManager.Instance is not found!");
        }
    }

    /// <summary>
    /// This method should be linked to the 'Close' (X) button's OnClick event.
    /// It simply hides the panel, which will trigger the OnDisable method.
    /// </summary>
    public void OnCloseButtonClicked()
    {
        Debug.Log("[ExtraTimePanel] 'Close' button clicked. Hiding panel.");
        gameObject.SetActive(false);
        GameManager.Instance.GameOver();
    }
}
