using UnityEngine;
using TMPro;

/// <summary>
/// Optional UI component to display the key refresh timer countdown
/// </summary>
public class KeyTimerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject timerPanel; // Optional panel to show/hide

    private void Update()
    {
        if (KeyRefreshTimer.Instance == null)
            return;

        bool isTimerActive = KeyRefreshTimer.Instance.IsTimerActive();

        // Show/hide timer panel if assigned
        if (timerPanel != null)
        {
            timerPanel.SetActive(isTimerActive);
        }

        // Update timer text
        if (timerText != null)
        {
            if (isTimerActive)
            {
                timerText.text = KeyRefreshTimer.Instance.GetRemainingTimeFormatted();
            }
            else
            {
                timerText.text = ""; // Clear text when timer is not active
            }
        }
    }
}
