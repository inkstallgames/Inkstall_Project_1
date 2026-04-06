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
        // The UIManager now handles the ad-watching logic.
        // This ensures that all UI elements are updated correctly.
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnWatchAdClicked();
        }
        else
        {
            Debug.LogError("[WatchAdButton] UIManager.Instance is not found! Cannot trigger ad watch.");
        }
    }
}
