using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VersionUpdatePopup : MonoBehaviour
{
    [Header("UI References")]
    public GameObject popupRoot;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public Button updateButton;

    [Header("Default Copy")]
    public string defaultTitle = "Update Available";
    public string defaultMessage = "A new version of Xeno Attack is available. Please update to get the latest features and fixes.";

    private System.Action onUpdateClicked;

    private void Awake()
    {
        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }

        if (updateButton != null)
        {
            updateButton.onClick.AddListener(HandleUpdateClicked);
        }
    }

    public void Show(string message, System.Action updateAction)
    {
        onUpdateClicked = updateAction;

        if (titleText != null)
        {
            titleText.text = defaultTitle;
        }

        if (messageText != null)
        {
            messageText.text = string.IsNullOrWhiteSpace(message) ? defaultMessage : message;
        }

        popupRoot.SetActive(true);
    }

    public void Hide()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    private void HandleUpdateClicked()
    {
        onUpdateClicked?.Invoke();
    }
}
