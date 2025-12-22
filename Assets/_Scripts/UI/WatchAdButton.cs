using UnityEngine;

/// <summary>
/// This component should be attached to any button that is intended to show a rewarded ad.
/// It provides a public method to be called by the button's OnClick event.
/// </summary>
public class WatchAdButton : MonoBehaviour
{
    /// <summary>
    /// This method should be linked to the button's OnClick event in the Unity Inspector.
    /// </summary>
    public void OnWatchAdClicked()
    {
        Debug.Log("[WatchAdButton] 'Watch Ad' button clicked. Attempting to show a rewarded ad.");

        if (AdManager.Instance != null)
        {
            // Call the singleton method to show the ad
            AdManager.Instance.ShowRewardedAd();
        }
        else
        {
            // Log an error if the AdManager is not available
            Debug.LogError("[WatchAdButton] AdManager.Instance is not found in the scene! Cannot show rewarded ad.");
        }
    }
}
